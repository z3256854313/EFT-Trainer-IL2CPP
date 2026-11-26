using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Spectre.Console;

namespace Installer;

internal class Installation
{
	public Version Version { get; }
	public bool UsingSpt { get; private set; }
	public bool UsingSptButNeverRun { get; private set; }
	public bool UsingBepInEx { get; private set; }
	public bool HasManagedRuntime { get; private set; }
	public bool UsingIl2Cpp { get; private set; }
	public string? SptData { get; private set; }
	public string Location { get; }
	public string DisplayString { get; private set; } = string.Empty;

	public string Data => Path.Combine(Location, "EscapeFromTarkov_Data");
	public string Managed => Path.Combine(Data, "Managed");
	public string BepInEx => Path.Combine(Location, "BepInEx");
	public string BepInExCore => Path.Combine(BepInEx, "core");
	public string BepInExPlugins => Path.Combine(BepInEx, "plugins");

	private Installation(string location, Version version)
	{
		if (string.IsNullOrEmpty(location))
			throw new ArgumentException("empty location");

		Location = location;
		Version = version;
	}

	public override bool Equals(object? obj)
	{
		if (obj is not Installation other)
			return false;

		return other.Location == Location;
	}

	public override int GetHashCode()
	{
		return Location.GetHashCode();
	}

	[SupportedOSPlatform("windows")]
	public static Installation? GetTargetInstallation(string? path, string promptTitle, bool confirm = true)
	{
		if (path is not null && TryDiscoverInstallation(path, out var requested))
		{
			if (!confirm)
				return requested;

			return AnsiConsole.Confirm(
				$"Continue with [green]EscapeFromTarkov ({requested.Version})[/] in [blue]{requested.Location.EscapeMarkup()}[/] ?")
				? requested
				: null;
		}

		var installations = new List<Installation>();

		AnsiConsole
			.Status()
			.Start("Discovering [green]Escape From Tarkov[/] installations...", _ =>
			{
				installations = [.. DiscoverInstallations().Distinct()];

			});

		installations = [.. installations.Distinct().OrderBy(i => i.Location)];

		switch (installations.Count)
		{
			case 0:
				AnsiConsole.MarkupLine("[yellow]No [green]EscapeFromTarkov[/] installation found, please re-run this installer, passing the installation path as argument.[/]");
				return null;
			case 1:
				var first = installations.First();
				return !confirm || AnsiConsole.Confirm($"Continue with [green]EscapeFromTarkov ({first.Version})[/] in [blue]{first.Location.EscapeMarkup()}[/] ?") ? first : null;
			default:
				var prompt = new SelectionPrompt<Installation> { Title = promptTitle };
				prompt.AddChoices(installations);
				return AnsiConsole.Prompt(prompt);
		}
	}

	[SupportedOSPlatform("windows")]
	private static IEnumerable<Installation> DiscoverInstallations()
	{
		if (TryDiscoverInstallation(Environment.CurrentDirectory, out var installation))
			yield return installation;

		if (TryDiscoverInstallation(Path.GetDirectoryName(AppContext.BaseDirectory), out installation))
			yield return installation;

		// SPT default installation path
		if (TryDiscoverInstallation(Path.Combine(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System))!, "SPT"), out installation))
			yield return installation;

		// SPT locations from MUI cache
		foreach (var sptpath in Registry.GetSptInstallationsFromMuiCache())
		{
			if (string.IsNullOrEmpty(sptpath))
				continue;

			if (TryDiscoverInstallation(sptpath, out installation))
				yield return installation;

			// Newer SPT versions (>= 4.0) install in SPT subfolder, so check the parent too
			if (TryDiscoverInstallation(Path.Combine(sptpath, ".."), out installation))
				yield return installation;
		}

		if (!Registry.TryGetEscapeFromTarkovInstallationPath(out var path))
			yield break;

		if (TryDiscoverInstallation(path, out installation))
			yield return installation;

		var subFolders = Directory.EnumerateDirectories(Path.Combine(path, ".."));
		foreach (var folder in subFolders)
		{
			if (TryDiscoverInstallation(folder, out installation))
				yield return installation;
		}
	}

	private static bool TryDiscoverInstallation(string? path, [NotNullWhen(true)] out Installation? installation)
	{
		installation = null;

		try
		{
			if (string.IsNullOrEmpty(path))
				return false;

			path = Path.GetFullPath(path.Trim('\"'));
			var exe = Path.Combine(path, "EscapeFromTarkov.exe");
			if (!File.Exists(exe))
				return false;

			var vi = FileVersionInfo.GetVersionInfo(exe);
			if (vi.FileVersion == null)
				return false;

			installation = new Installation(path, new Version(vi.FileVersion));
			installation.HasManagedRuntime = Directory.Exists(installation.Managed);
			installation.UsingIl2Cpp =
				File.Exists(Path.Combine(path, "GameAssembly.dll")) ||
				Directory.Exists(Path.Combine(path, "EscapeFromTarkov_Data", "il2cpp_data"));
			installation.SptData = FindSptDataDirectory(path);
			installation.UsingSpt = installation.SptData != null;


			var battleye = Path.Combine(path, "BattlEye");
			installation.UsingSptButNeverRun = installation.UsingSpt && Directory.Exists(battleye);

			installation.UsingBepInEx = Directory.Exists(installation.BepInExPlugins);

			installation.DisplayString = installation.ComputeDisplayString();

			return true;
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
		{
			return false;
		}
	}

	private static string? FindSptDataDirectory(string path)
	{
		var directCandidates = new[]
		{
			Path.Combine(path, "SPT_Data"),
			Path.Combine(path, "SPT", "SPT_Data"),
			Path.Combine(path, "SPT_Runtime", "SPT_Data")
		};

		foreach (var candidate in directCandidates)
		{
			if (Directory.Exists(candidate))
				return candidate;
		}

		try
		{
			foreach (var directory in Directory.EnumerateDirectories(path))
			{
				var candidate = Path.Combine(directory, "SPT_Data");
				if (Directory.Exists(candidate))
					return candidate;
			}
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			// Discovery remains usable with the initial candidates.
		}

		return null;
	}

	private string ComputeDisplayString()
	{
		var sb = new StringBuilder();
		sb.Append($"{Location.EscapeMarkup()} - [[{Version}]] ");
		sb.Append(UsingSpt ? "[b]SPT[/] " : "Vanilla ");
		sb.Append(UsingIl2Cpp ? "[red](IL2CPP)[/] " : "[green](Managed)[/] ");

		return sb.ToString();
	}

	public override string ToString()
	{
		return DisplayString;
	}
}
