using System;
using System.ComponentModel;
using System.Runtime.Versioning;
using System.Threading;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Installer;

internal sealed class RollbackCommand : Command<RollbackCommand.Settings>
{
	internal class Settings : CommandSettings
	{
		[Description("Path to EFT.")]
		[CommandArgument(0, "[path]")]
		public string? Path { get; set; }

		[Description("Use a specific backup snapshot directory.")]
		[CommandOption("-b|--backup")]
		public string? Backup { get; set; }

		[Description("Only show what would be restored.")]
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
			var installation = Installation.GetTargetInstallation(
				settings.Path,
				"Please select where to roll back the trainer",
				confirm: !settings.Yes);
			if (installation == null)
				return (int)ExitCode.NoInstallationFound;

			var snapshot = BackupManager.FindLatest(installation, settings.Backup);
			if (snapshot == null)
			{
				AnsiConsole.MarkupLine("[red]No matching backup snapshot was found.[/]");
				return (int)ExitCode.BackupNotFound;
			}

			AnsiConsole.MarkupLine($"Target: [blue]{installation.Location.EscapeMarkup()}[/]");
			AnsiConsole.MarkupLine($"Backup: [blue]{snapshot.EscapeMarkup()}[/]");
			if (!settings.Yes && !settings.DryRun &&
				!AnsiConsole.Confirm("Restore this backup and overwrite current trainer files?"))
				return (int)ExitCode.Canceled;

			var changed = BackupManager.Rollback(installation, snapshot, settings.DryRun);
			AnsiConsole.MarkupLine(
				settings.DryRun
					? $"[green]Dry run completed.[/] {changed} file actions would be applied."
					: $"[green]Rollback completed.[/] {changed} file actions applied.");
			return (int)ExitCode.Success;
		}
		catch (Exception exception)
		{
			AnsiConsole.MarkupLine($"[red]Rollback failed:[/] {exception.Message.EscapeMarkup()}");
			InstallerLog.Current?.WriteException(exception);
			return (int)ExitCode.RollbackFailed;
		}
	}
}
