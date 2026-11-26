using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Xml;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Installer;

internal sealed class UninstallCommand : Command<UninstallCommand.Settings>
{
	internal class Settings : CommandSettings
	{
		[Description("Path to EFT.")]
		[CommandArgument(0, "[path]")]
		public string? Path { get; set; }

		[Description("Only show files that would be removed.")]
		[CommandOption("--dry-run")]
		public bool DryRun { get; set; }

		[Description("Skip interactive confirmation.")]
		[CommandOption("-y|--yes")]
		public bool Yes { get; set; }
	}

	[SupportedOSPlatform("windows")]
	public override int Execute(CommandContext context, Settings settings, CancellationToken ct)
	{
		try
		{
			AnsiConsole.MarkupLine("-=[[ [cyan]EscapeFromTarkov-Trainer Universal Installer[/] - [blue]https://github.com/sailro [/]]]=-");
			AnsiConsole.WriteLine();

			var installation = Installation.GetTargetInstallation(
				settings.Path,
				"Please select from where to uninstall the trainer",
				confirm: !settings.Yes);
			if (installation == null)
				return (int)ExitCode.NoInstallationFound;

			AnsiConsole.MarkupLine($"Target [green]EscapeFromTarkov ({installation.Version})[/] in [blue]{installation.Location.EscapeMarkup()}[/].");
			if (settings.DryRun)
			{
				foreach (var file in BackupManager.GetManagedFiles(installation))
					AnsiConsole.MarkupLine($"[grey]Would inspect/remove[/] {file.EscapeMarkup()}");

				AnsiConsole.MarkupLine("[green]Uninstall dry run completed. No files were changed.[/]");
				return (int)ExitCode.Success;
			}

			if (!settings.Yes && !AnsiConsole.Confirm("Uninstall the trainer files?"))
				return (int)ExitCode.Canceled;

			var backupDirectory = BackupManager.Create(installation);
			var result = RemoveTrainerFiles(installation);
			if (result != ExitCode.Success)
			{
				AnsiConsole.MarkupLine("[yellow]Uninstall failed; restoring the pre-uninstall backup.[/]");
				BackupManager.Rollback(installation, backupDirectory, dryRun: false);
				AnsiConsole.MarkupLine("[green]Automatic rollback completed.[/]");
				return (int)result;
			}

			AnsiConsole.MarkupLine("[green]Uninstall completed successfully.[/]");
			AnsiConsole.MarkupLine(
				$"[blue]Restore command:[/] Installer.exe rollback \"{installation.Location}\" --backup \"{backupDirectory}\"");
			return (int)ExitCode.Success;
		}
		catch (Exception exception)
		{
			AnsiConsole.MarkupLine($"[red]Error:[/] {exception.Message.EscapeMarkup()}");
			InstallerLog.Current?.WriteException(exception);
			return (int)ExitCode.Failure;
		}
	}

	private static ExitCode RemoveTrainerFiles(Installation installation)
	{
		if (!RemoveFile(Path.Combine(installation.Managed, "NLog.EFT.Trainer.dll")))
			return ExitCode.RemoveDllFailed;

		if (!File.Exists(Path.Combine(installation.Managed, "MonoMod.RuntimeDetour.dll")) &&
			!RemoveFile(Path.Combine(installation.Managed, "0Harmony.dll")))
			return ExitCode.RemoveHarmonyDllFailed;

		if (!RemoveFile(Path.Combine(installation.Data, "outline")))
			return ExitCode.RemoveOutlineFailed;

		if (!RemoveFile(Path.Combine(installation.BepInExPlugins, "spt-efttrainer.dll")))
			return ExitCode.RemovePluginDllFailed;

		return RemoveOrPatchConfiguration(installation)
			? ExitCode.Success
			: ExitCode.RemoveDllFailed;
	}

	private static bool RemoveFile(string filename)
	{
		try
		{
			if (!File.Exists(filename))
			{
				AnsiConsole.MarkupLine($"No [green]{Path.GetFileName(filename).EscapeMarkup()}[/] to remove.");
			}
			else
			{
				File.Delete(filename);
				AnsiConsole.MarkupLine($"Removed [green]{Path.GetFileName(filename).EscapeMarkup()}[/].");
			}

			return true;
		}
		catch (Exception exception)
		{
			AnsiConsole.MarkupLine($"[red]Unable to remove {filename.EscapeMarkup()}: {exception.Message.EscapeMarkup()}[/]");
			return false;
		}
	}

	private static bool RemoveOrPatchConfiguration(Installation installation)
	{
		const string targetName = "EFTTarget";
		var configPath = Path.Combine(installation.Managed, "NLog.dll.nlog");
		try
		{
			if (!File.Exists(configPath))
			{
				AnsiConsole.MarkupLine("No [green]NLog.dll.nlog[/] to restore.");
				return true;
			}

			var document = new XmlDocument();
			document.Load(configPath);
			var nlogNode = document.DocumentElement;
			var targetsNode = nlogNode?.FirstChild;
			if (nlogNode is not { Name: "nlog" } || targetsNode is not { Name: "targets" })
			{
				AnsiConsole.MarkupLine($"[red]Unable to unpatch {configPath.EscapeMarkup()}: unexpected XML structure.[/]");
				return false;
			}

			var removeNodes = targetsNode
				.ChildNodes
				.Cast<XmlNode>()
				.Where(targetNode =>
					targetNode.Attributes?["name"]?.Value == targetName &&
					targetNode.Attributes?["xsi:type"]?.Value == targetName)
				.ToList();

			if (removeNodes.Count == 0)
			{
				AnsiConsole.MarkupLine("NLog configuration was not patched by this installer.");
				return true;
			}

			foreach (var target in removeNodes)
				targetsNode.RemoveChild(target);

			if (targetsNode.HasChildNodes)
			{
				var builder = new StringBuilder();
				using var writer = new UTF8StringWriter(builder);
				document.Save(writer);
				File.WriteAllText(configPath, builder.ToString().Replace(" xmlns=\"\"", string.Empty));
				AnsiConsole.MarkupLine("Unpatched [green]NLog.dll.nlog[/].");
			}
			else
			{
				File.Delete(configPath);
				AnsiConsole.MarkupLine("Removed [green]NLog.dll.nlog[/].");
			}

			return true;
		}
		catch (Exception exception)
		{
			AnsiConsole.MarkupLine($"[red]Unable to unpatch or remove {configPath.EscapeMarkup()}: {exception.Message.EscapeMarkup()}[/]");
			return false;
		}
	}
}
