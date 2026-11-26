using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Spectre.Console;

namespace Installer;

internal sealed class CompatibilityReport
{
	public List<string> Errors { get; } = [];
	public List<string> Warnings { get; } = [];

	public bool IsReady => Errors.Count == 0;

	public void WriteToConsole()
	{
		foreach (var warning in Warnings)
			AnsiConsole.MarkupLine($"[yellow]Preflight warning:[/] {warning.EscapeMarkup()}");

		foreach (var error in Errors)
			AnsiConsole.MarkupLine($"[red]Preflight error:[/] {error.EscapeMarkup()}");
	}
}

internal static class CompatibilityChecker
{
	private static readonly Version LastSupportedGameVersion = new(0, 16, 9, 40087);
	private static readonly Version FirstIl2CppGameVersion = new(1, 0, 0, 0);

	public static CompatibilityReport Check(Installation installation)
	{
		var report = new CompatibilityReport();

		if (!File.Exists(Path.Combine(installation.Location, "EscapeFromTarkov.exe")))
			report.Errors.Add("EscapeFromTarkov.exe was not found in the selected directory.");

		if (installation.Version >= FirstIl2CppGameVersion)
		{
			report.Errors.Add(
				$"EFT {installation.Version} is an IL2CPP build. The legacy managed trainer only supports 0.16.9.40087 or older.");
		}
		else if (installation.Version > LastSupportedGameVersion)
		{
			report.Warnings.Add(
				$"EFT {installation.Version} is newer than the last tested legacy build {LastSupportedGameVersion}.");
		}

		if (installation.UsingIl2Cpp)
			report.Errors.Add("IL2CPP metadata/GameAssembly.dll was detected; managed assembly compilation is not available.");

		if (!installation.HasManagedRuntime)
			report.Errors.Add("EscapeFromTarkov_Data\\Managed was not found.");
		else if (!File.Exists(Path.Combine(installation.Managed, "mscorlib.dll")))
			report.Errors.Add("EscapeFromTarkov_Data\\Managed\\mscorlib.dll was not found.");

		if (!installation.UsingSpt)
			report.Warnings.Add("SPT data was not detected. The installer will only work with a compatible managed vanilla installation.");

		if (!installation.UsingBepInEx)
			report.Warnings.Add("BepInEx\\plugins was not found; the NLog loading path will be used.");

		return report;
	}

	public static string FormatSummary(Installation installation)
	{
		var spt = installation.UsingSpt ? installation.SptData ?? "detected" : "not detected";
		return string.Join(
			Environment.NewLine,
			$"Game version: {installation.Version}",
			$"IL2CPP: {installation.UsingIl2Cpp}",
			$"Managed runtime: {installation.HasManagedRuntime}",
			$"SPT data: {spt}",
			$"BepInEx: {installation.UsingBepInEx}");
	}
}
