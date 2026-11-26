using System;
using System.Runtime.Versioning;
using Spectre.Console.Cli;

namespace Installer;

internal class Program
{
	[SupportedOSPlatform("windows")]
	private static int Main(string[] args)
	{
		using var log = InstallerLog.Start();
		Console.WriteLine($"Installer log: {log.Path}");

		var app = new CommandApp<InstallCommand>();
		app.Configure(config =>
		{
			config
				.AddCommand<InstallCommand>("install")
				.WithDescription("Install the trainer");

			config
				.AddCommand<UninstallCommand>("uninstall")
				.WithDescription("Uninstall the trainer");

			config
				.AddCommand<RollbackCommand>("rollback")
				.WithDescription("Restore files from an installation backup");
		});

		int result;
		try
		{
			result = app.Run(args);
		}
		catch (Exception exception)
		{
			Console.Error.WriteLine(exception);
			log.WriteException(exception);
			result = (int)ExitCode.Failure;
		}

		if (!StartedByExplorer())
			return result;

		Console.WriteLine();
		Console.WriteLine("Press a key to exit...");
		Console.ReadKey();
		return result;
	}

	[SupportedOSPlatform("windows")]
	private static bool StartedByExplorer()
	{
		try
		{
			var parent = ParentProcessHelper.GetParentProcess();
			return parent?.ProcessName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ?? false;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
