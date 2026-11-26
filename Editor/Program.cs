using TrainerEditor;

internal static class Program
{
	[STAThread]
	private static int Main(string[] args)
	{
		ApplicationConfiguration.Initialize();

		if (args.Length == 2 && args[0].Equals("--check", StringComparison.OrdinalIgnoreCase))
		{
			try
			{
				var document = IniDocument.Load(args[1]);
				_ = document.GetEntries();
				return 0;
			}
			catch
			{
				return 1;
			}
		}

		var defaultPath = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
			"Escape from Tarkov",
			"trainer.ini");
		var path = args.Length > 0 ? args[0] : defaultPath;

		Application.Run(new EditorForm(path));
		return 0;
	}
}
