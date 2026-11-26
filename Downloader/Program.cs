using System.Runtime.InteropServices;

namespace EftTrainerDownloader;

internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		if (args.Length == 1 && args[0] is "--help" or "-h")
		{
			AttachConsole(-1);
			Console.WriteLine("EFT Trainer Downloader");
			Console.WriteLine("  GUI:     EFT-Trainer-Downloader.exe");
			Console.WriteLine("  Install: EFT-Trainer-Downloader.exe --install <EFT_ROOT>");
			return 0;
		}

		if (args.Length == 2 && args[0] == "--install")
		{
			AttachConsole(-1);
			try
			{
				var installer = new ReleaseInstaller();
				var result = installer
					.InstallLatestAsync(args[1], log: Console.WriteLine)
					.GetAwaiter()
					.GetResult();
				Console.WriteLine($"Installed EFT Trainer IL2CPP {result.Version}.");
				Console.WriteLine($"SHA-256: {result.Sha256}");
				Console.WriteLine($"Install directory: {result.InstallDirectory}");
				return 0;
			}
			catch (Exception exception)
			{
				Console.Error.WriteLine(exception);
				return 1;
			}
			finally
			{
				Console.Out.Flush();
				Console.Error.Flush();
			}
		}

		ApplicationConfiguration.Initialize();
		Application.Run(new MainForm());
		return 0;
	}

	[DllImport("kernel32.dll")]
	private static extern bool AttachConsole(int processId);
}
