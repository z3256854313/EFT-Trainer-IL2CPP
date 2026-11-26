using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Installer.Properties;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Installer;

internal sealed class InstallCommand : AsyncCommand<InstallCommand.Settings>
{
	internal class Settings : CommandSettings
	{
		[Description("Path to EFT.")]
		[CommandArgument(0, "[path]")]
		public string? Path { get; set; }

		[Description("Use a specific trainer branch.")]
		[CommandOption("-b|--branch")]
		public string? Branch { get; set; }

		[Description("Disable a feature (comma-separated values are accepted).")]
		[CommandOption("-f|--feature")]
		public string[]? DisabledFeatures { get; set; }

		[Description("Also exclude a feature from compilation.")]
		[CommandOption("-x|--exclude-feature")]
		public string[]? ExcludedFeatures { get; set; }

		[Description("Compile only this feature. Repeat for multiple features.")]
		[CommandOption("--include-feature")]
		public string[]? IncludedFeatures { get; set; }

		[Description("Disable a console command.")]
		[CommandOption("-c|--command")]
		public string[]? DisabledCommands { get; set; }

		[Description("Also exclude a console command from compilation.")]
		[CommandOption("--exclude-command")]
		public string[]? ExcludedCommands { get; set; }

		[Description("Compile only this console command. Repeat for multiple commands.")]
		[CommandOption("--include-command")]
		public string[]? IncludedCommands { get; set; }

		[Description("Load an include/exclude profile from JSON.")]
		[CommandOption("--profile")]
		public string? Profile { get; set; }

		[Description("Language.")]
		[CommandOption("-l|--language")]
		public string Language { get; set; } = "";

		[Description("Use a local repository ZIP and do not access the network.")]
		[CommandOption("--source")]
		public string? Source { get; set; }

		[Description("Use cached source only and do not access the network.")]
		[CommandOption("--offline")]
		public bool Offline { get; set; }

		[Description("Ignore an existing cached snapshot and download it again.")]
		[CommandOption("--refresh")]
		public bool RefreshSource { get; set; }

		[Description("Override the source snapshot cache directory.")]
		[CommandOption("--cache-dir")]
		public string? CacheDirectory { get; set; }

		[Description("Check compatibility and compile in memory without writing files.")]
		[CommandOption("--dry-run")]
		public bool DryRun { get; set; }

		[Description("Skip interactive confirmation.")]
		[CommandOption("-y|--yes")]
		public bool Yes { get; set; }
	}

	[SupportedOSPlatform("windows")]
	public override async Task<int> ExecuteAsync(CommandContext commandContext, Settings settings, CancellationToken ct)
	{
		try
		{
			AnsiConsole.MarkupLine("-=[[ [cyan]EscapeFromTarkov-Trainer Universal Installer[/] - [blue]https://github.com/sailro [/]]]=-");
			AnsiConsole.WriteLine();

			var selection = FeatureSelection.Create(
				settings.IncludedFeatures,
				[.. settings.DisabledFeatures ?? [], .. settings.ExcludedFeatures ?? []],
				settings.IncludedCommands,
				[.. settings.DisabledCommands ?? [], .. settings.ExcludedCommands ?? []],
				settings.Profile,
				out var effectiveProfile);

			var installation = Installation.GetTargetInstallation(
				settings.Path,
				"Please select where to install the trainer",
				confirm: !settings.Yes);
			if (installation == null)
				return (int)ExitCode.NoInstallationFound;

			AnsiConsole.MarkupLine($"Target [green]EscapeFromTarkov ({installation.Version})[/] in [blue]{installation.Location.EscapeMarkup()}[/].");
			AnsiConsole.MarkupLine(CompatibilityChecker.FormatSummary(installation).EscapeMarkup());

			var compatibility = CompatibilityChecker.Check(installation);
			compatibility.WriteToConsole();
			if (!compatibility.IsReady)
			{
				AnsiConsole.MarkupLine("[red]Installation stopped by the compatibility preflight.[/]");
				return (int)ExitCode.CompatibilityCheckFailed;
			}

			AnsiConsole.MarkupLine(
				$"[blue]Build profile:[/] include {FormatSelection(effectiveProfile.IncludeFeatures)}; exclude {FormatSelection(effectiveProfile.ExcludeFeatures)}");

			if (installation.UsingSpt && !settings.Yes && !settings.DryRun)
			{
				AnsiConsole.MarkupLine("[green][[SPT]][/] detected. The game must have completed at least one SPT patch run before compilation.");
				if (installation.UsingSptButNeverRun)
					AnsiConsole.MarkupLine("[yellow]Warning: BattlEye support files are still present; this usually means SPT has not been run yet.[/]");

				if (!await AnsiConsole.ConfirmAsync("Continue installation?", cancellationToken: ct))
					return (int)ExitCode.Canceled;
			}

			const string featuresFolder = "Features";
			const string commandsFolder = "ConsoleCommands";
			var result = await BuildTrainerAsync(settings, installation, selection, ct, featuresFolder, commandsFolder);

			if (result.Compilation == null)
			{
				AnsiConsole.MarkupLine(
					$"[red]Unable to compile trainer for version {installation.Version}. See the log for diagnostics.[/]");
				return (int)ExitCode.CompilationFailed;
			}

			CSharpCompilation? pluginCompilation = null;
			const string bepInExPluginProject = "BepInExPlugin.csproj";
			if (installation.UsingBepInEx && result.Archive!.Entries.Any(e => e.Name == bepInExPluginProject))
			{
				var pluginContext = new CompilationContext(installation, "plugin", bepInExPluginProject)
				{
					Archive = result.Archive,
					Branch = GetInitialBranch(settings),
					Language = settings.Language
				};
				var pluginResult = await GetCompilationAsync(pluginContext, ct);
				pluginCompilation = pluginResult.Compilation;
				if (pluginCompilation == null)
				{
					AnsiConsole.MarkupLine($"[red]Unable to compile BepInEx plugin for version {installation.Version}.[/]");
					return (int)ExitCode.PluginCompilationFailed;
				}
			}

			if (settings.DryRun)
			{
				PrintDryRunPlan(installation, settings, selection, pluginCompilation != null);
				return (int)ExitCode.Success;
			}

			var backupDirectory = BackupManager.Create(installation);
			var installResult = InstallFiles(installation, result, pluginCompilation);
			if (installResult != ExitCode.Success)
			{
				AnsiConsole.MarkupLine("[yellow]Installation failed; restoring the pre-install backup.[/]");
				try
				{
					BackupManager.Rollback(installation, backupDirectory, dryRun: false);
					AnsiConsole.MarkupLine("[green]Automatic rollback completed.[/]");
				}
				catch (Exception exception)
				{
					AnsiConsole.MarkupLine($"[red]Automatic rollback failed:[/] {exception.Message.EscapeMarkup()}");
					AnsiConsole.MarkupLine(
						$"[yellow]Run 'rollback \"{installation.Location}\" --backup \"{backupDirectory}\"' after resolving the file lock.[/]");
				}

				return (int)installResult;
			}

			TryCreateGameDocumentFolder();
			AnsiConsole.MarkupLine("[green]Installation completed successfully.[/]");
			AnsiConsole.MarkupLine(
				$"[blue]Rollback command:[/] Installer.exe rollback \"{installation.Location}\" --backup \"{backupDirectory}\"");
		}
		catch (OperationCanceledException)
		{
			AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
			return (int)ExitCode.Canceled;
		}
		catch (Exception exception)
		{
			AnsiConsole.MarkupLine($"[red]Error:[/] {exception.Message.EscapeMarkup()}");
			AnsiConsole.MarkupLine("[grey]The complete exception was written to the installer log.[/]");
			InstallerLog.Current?.WriteException(exception);
			return (int)ExitCode.Failure;
		}

		return (int)ExitCode.Success;
	}

	private static ExitCode InstallFiles(
		Installation installation,
		CompilationResult result,
		CSharpCompilation? pluginCompilation)
	{
		if (!CreateDll(
			installation,
			"NLog.EFT.Trainer.dll",
			dllPath => result.Compilation!.Emit(dllPath, manifestResources: result.Resources)))
			return ExitCode.CreateDllFailed;

		if (!CreateDll(
			installation,
			"0Harmony.dll",
			dllPath => File.WriteAllBytes(dllPath, Resources._0Harmony),
			overwrite: false))
			return ExitCode.CreateHarmonyDllFailed;

		if (!CreateOutline(installation, result.Archive!))
			return ExitCode.CreateOutlineFailed;

		if (pluginCompilation != null)
		{
			if (!CreateDll(
				installation,
				Path.Combine(installation.BepInExPlugins, "spt-efttrainer.dll"),
				dllPath => pluginCompilation.Emit(dllPath)))
				return ExitCode.CreatePluginDllFailed;
		}
		else
		{
			var version = new Version(0, 13, 0, 21531);
			if (installation.Version >= version)
			{
				AnsiConsole.MarkupLine(
					$"[yellow]Warning: EFT {version} or later cannot load this trainer through NLog configuration.[/]");
			}

			if (!CreateOrPatchConfiguration(installation))
				return ExitCode.CreateConfigurationFailed;
		}

		return ExitCode.Success;
	}

	private static async Task<CompilationResult> BuildTrainerAsync(
		Settings settings,
		Installation installation,
		FeatureSelection selection,
		CancellationToken ct,
		params string[] folders)
	{
		var context = CreateCompilationContext(settings, installation, selection);
		var result = await GetCompilationAsync(context, ct);
		if (context.IsFatalFailure)
			return result;

		if (result.Compilation == null)
		{
			var retryBranch = GetRetryBranch(installation, context);
			if (retryBranch != null)
			{
				context.Branch = retryBranch;
				result = await GetCompilationAsync(context, ct);
			}
		}

		var files = result.ErrorFiles;
		if (!HasFaultingFeatureOrCommand(result, folders, files))
			return result;

		AnsiConsole.MarkupLine(
			$"[yellow]Trying to disable faulting feature/command: [red]{GetFaultingNames(files)}[/].[/]");

		context.Exclude = [.. files, .. selection.ExcludedFeatures, .. selection.ExcludedCommands];
		context.Branch = GetFallbackBranch(settings);
		result = await GetCompilationAsync(context, ct);

		if (result.Errors.Length == 0)
			AnsiConsole.MarkupLine("[yellow]Compilation succeeded after excluding the faulting modules.[/]");

		return result;
	}

	private static CompilationContext CreateCompilationContext(
		Settings settings,
		Installation installation,
		FeatureSelection selection)
	{
		return new CompilationContext(installation, "trainer", "NLog.EFT.Trainer.csproj")
		{
			Exclude = [.. selection.ExcludedFeatures, .. selection.ExcludedCommands],
			Branch = GetInitialBranch(settings),
			Defines = installation.UsingSpt ? [] : ["EFT_LIVE"],
			Language = settings.Language,
			SourcePath = settings.Source,
			Offline = settings.Offline,
			RefreshSource = settings.RefreshSource,
			CacheDirectory = settings.CacheDirectory
		};
	}

	private static bool HasFaultingFeatureOrCommand(CompilationResult result, string[] folders, string[] files)
	{
		return result.Compilation == null &&
			files.Length != 0 &&
			files.All(file => folders.Any(file.StartsWith));
	}

	private static string GetFaultingNames(string[] files)
	{
		return string.Join(", ", files
			.Select(Path.GetFileNameWithoutExtension)
			.Where(file => !file!.StartsWith("Base", StringComparison.Ordinal))
			.Distinct()
			.OrderBy(file => file));
	}

	private static string GetInitialBranch(Settings settings)
	{
		return settings.Branch ?? CompilationContext.DefaultBranch;
	}

	private static string? GetRetryBranch(Installation installation, CompilationContext context)
	{
		if (!string.IsNullOrWhiteSpace(context.SourcePath))
			return null;

		var dedicated = "dev-" + installation.Version;
		return dedicated == context.Branch ? null : dedicated;
	}

	private static string GetFallbackBranch(Settings settings)
	{
		return GetInitialBranch(settings);
	}

	private static string FormatSelection(string[] values)
	{
		return values.Length == 0 ? "(none)" : string.Join(", ", values.Select(Path.GetFileNameWithoutExtension));
	}

	private static void PrintDryRunPlan(
		Installation installation,
		Settings settings,
		FeatureSelection selection,
		bool includesPlugin)
	{
		AnsiConsole.MarkupLine("[green]Dry run succeeded.[/]");
		AnsiConsole.MarkupLine("No game files were written.");
		AnsiConsole.MarkupLine($"Source: {(settings.Source ?? (settings.Offline ? "cached snapshot" : settings.Branch ?? CompilationContext.DefaultBranch)).EscapeMarkup()}");
		AnsiConsole.MarkupLine($"Feature exclusions: {FormatSelection(selection.ExcludedFeatures).EscapeMarkup()}");
		AnsiConsole.MarkupLine($"Command exclusions: {FormatSelection(selection.ExcludedCommands).EscapeMarkup()}");
		AnsiConsole.MarkupLine($"Would install trainer: {Path.Combine(installation.Managed, "NLog.EFT.Trainer.dll").EscapeMarkup()}");
		AnsiConsole.MarkupLine($"Would install outline: {Path.Combine(installation.Data, "outline").EscapeMarkup()}");
		AnsiConsole.MarkupLine($"Would install BepInEx plugin: {includesPlugin}");
	}

	private static void TryCreateGameDocumentFolder()
	{
		var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Escape from Tarkov");
		if (Directory.Exists(folder))
			return;

		try
		{
			Directory.CreateDirectory(folder);
			AnsiConsole.MarkupLine($"Created [blue]{folder.EscapeMarkup()}[/] folder.");
		}
		catch (Exception)
		{
			AnsiConsole.MarkupLine($"Unable to create [blue]{folder.EscapeMarkup()}[/].");
		}
	}

	private static async Task<CompilationResult> GetCompilationAsync(CompilationContext context, CancellationToken ct)
	{
		var errors = Array.Empty<Diagnostic>();
		ResourceDescription[] resources = [];
		var archive = context.Archive ?? await SourceArchiveManager.OpenAsync(context, ct);
		if (archive == null)
		{
			context.Try++;
			return new(null, null, errors, resources);
		}

		CSharpCompilation? compilation = null;
		AnsiConsole.Status().Start($"Compiling {context.ProjectTitle}", _ =>
		{
			var compiler = new Compiler(archive, context);
			compilation = compiler.Compile(Path.GetFileNameWithoutExtension(context.Project));
			errors = [.. compilation
				.GetDiagnostics()
				.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

			if (errors.Length != 0)
			{
				AnsiConsole.MarkupLine(
					$">> [blue]Try #{context.Try}[/] [yellow]Compilation failed for {context.Branch.EscapeMarkup()}.[/]");
				foreach (var error in errors)
				{
					InstallerLog.Current?.WriteLine(
						$"{error.Id} [{error.Location.SourceTree?.FilePath}]: {error.GetMessage()}");
				}

				foreach (var error in errors.Take(8))
					AnsiConsole.MarkupLine(
						$"[grey]>> {error.Id} [[{error.Location.SourceTree?.FilePath.EscapeMarkup()}]]: {error.GetMessage().EscapeMarkup()}[/]");
				compilation = null;
			}
			else
			{
				resources = [.. compiler.GetResources(context)];
				if (compiler.IsLocalizationSupported() && resources.Length == 0)
				{
					AnsiConsole.MarkupLine($"No localization support for language '{context.Language.EscapeMarkup()}'.");
					compilation = null;
					context.IsFatalFailure = true;
				}
				else
				{
					AnsiConsole.MarkupLine(
						$">> [blue]Try #{context.Try}[/] Compilation [green]succeeded[/] for [blue]{context.Branch.EscapeMarkup()}[/].");
				}
			}
		});

		context.Try++;
		return new(compilation, archive, errors, resources);
	}

	private static bool CreateOrPatchConfiguration(Installation installation)
	{
		const string targetName = "EFTTarget";
		var configPath = Path.Combine(installation.Managed, "NLog.dll.nlog");
		try
		{
			if (File.Exists(configPath))
			{
				var document = new XmlDocument();
				document.Load(configPath);
				var nlogNode = document.DocumentElement;
				var targetsNode = nlogNode?.FirstChild;
				if (nlogNode is not { Name: "nlog" } || targetsNode is not { Name: "targets" })
				{
					AnsiConsole.MarkupLine($"[red]Unable to patch {configPath.EscapeMarkup()}: unexpected XML structure.[/]");
					return false;
				}

				if (targetsNode.ChildNodes.Cast<XmlNode>().Any(targetNode =>
					targetNode.Attributes?["name"]?.Value == targetName &&
					targetNode.Attributes?["xsi:type"]?.Value == targetName))
				{
					AnsiConsole.MarkupLine($"Already patched [green]{Path.GetFileName(configPath).EscapeMarkup()}[/].");
					return true;
				}

				var entry = document.CreateElement("target");
				var name = document.CreateAttribute("name");
				name.Value = targetName;
				var type = document.CreateAttribute("xsi", "type", "http://www.w3.org/2001/XMLSchema-instance");
				type.Value = targetName;
				entry.Attributes.Append(name);
				entry.Attributes.Append(type);
				targetsNode.AppendChild(entry);

				var builder = new StringBuilder();
				using var writer = new UTF8StringWriter(builder);
				document.Save(writer);
				File.WriteAllText(configPath, builder.ToString().Replace(" xmlns=\"\"", string.Empty));
				AnsiConsole.MarkupLine($"Patched [green]{Path.GetFileName(configPath).EscapeMarkup()}[/].");
				return true;
			}

			var content = $"""
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <targets>
    <target name="{targetName}" xsi:type="{targetName}" />
  </targets>
</nlog>
""";
			File.WriteAllText(configPath, content);
			AnsiConsole.MarkupLine($"Created [green]{Path.GetFileName(configPath).EscapeMarkup()}[/].");
			return true;
		}
		catch (Exception exception)
		{
			AnsiConsole.MarkupLine($"[red]Unable to patch or create {configPath.EscapeMarkup()}: {exception.Message.EscapeMarkup()}[/]");
			return false;
		}
	}

	private static bool CreateOutline(Installation installation, ZipArchive archive)
	{
		var outlinePath = Path.Combine(installation.Data, "outline");
		try
		{
			var entry = archive.Entries.FirstOrDefault(archiveEntry =>
				archiveEntry.Name.Equals(Path.GetFileName(outlinePath), StringComparison.OrdinalIgnoreCase));
			if (entry == null)
			{
				AnsiConsole.MarkupLine("[red]Unable to find outline in the source archive.[/]");
				return false;
			}

			using var input = entry.Open();
			using var output = File.Create(outlinePath);
			input.CopyTo(output);
			AnsiConsole.MarkupLine($"Created [green]{Path.GetFileName(outlinePath).EscapeMarkup()}[/].");
			return true;
		}
		catch (Exception exception)
		{
			AnsiConsole.MarkupLine($"[red]Unable to create {outlinePath.EscapeMarkup()}: {exception.Message.EscapeMarkup()}[/]");
			return false;
		}
	}

	private static bool CreateDll(
		Installation installation,
		string filename,
		Action<string> creator,
		bool overwrite = true)
	{
		return CreateDll(installation, filename, path =>
		{
			creator(path);
			return null;
		}, overwrite);
	}

	private static bool CreateDll(
		Installation installation,
		string filename,
		Func<string, EmitResult?> creator,
		bool overwrite = true)
	{
		var dllPath = Path.IsPathRooted(filename) ? filename : Path.Combine(installation.Managed, filename);
		var bepInExPath = Path.IsPathRooted(filename) ? null : Path.Combine(installation.BepInExCore, filename);
		try
		{
			if (bepInExPath != null && File.Exists(bepInExPath))
				return true;

			if (!overwrite && File.Exists(dllPath))
				return true;

			Directory.CreateDirectory(Path.GetDirectoryName(dllPath)!);
			var result = creator(dllPath);
			if (result != null)
			{
				var errors = result.Diagnostics
					.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
					.ToArray();
				if (!result.Success)
					throw new Exception(errors.FirstOrDefault()?.GetMessage() ?? "Unknown error while emitting assembly.");
			}

			AnsiConsole.MarkupLine($"Created [green]{Path.GetFileName(dllPath).EscapeMarkup()}[/].");
			return true;
		}
		catch (Exception exception)
		{
			AnsiConsole.MarkupLine($"[red]Unable to create {dllPath.EscapeMarkup()}: {exception.Message.EscapeMarkup()}[/]");
			return false;
		}
	}
}
