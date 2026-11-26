using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Spectre.Console;

namespace Installer;

internal sealed class BackupManifest
{
	public string InstallationRoot { get; set; } = string.Empty;
	public DateTimeOffset CreatedUtc { get; set; }
	public List<BackupEntry> Entries { get; set; } = [];
}

internal sealed class BackupEntry
{
	public string RelativePath { get; set; } = string.Empty;
	public bool Existed { get; set; }
	public string? BackupPath { get; set; }
}

internal static class BackupManager
{
	private const string ManifestFileName = "manifest.json";

	public static IReadOnlyList<string> GetManagedFiles(Installation installation)
	{
		return
		[
			Path.Combine(installation.Managed, "NLog.EFT.Trainer.dll"),
			Path.Combine(installation.Managed, "0Harmony.dll"),
			Path.Combine(installation.Managed, "NLog.dll.nlog"),
			Path.Combine(installation.Data, "outline"),
			Path.Combine(installation.BepInExPlugins, "spt-efttrainer.dll")
		];
	}

	public static string Create(Installation installation)
	{
		var backupsRoot = GetBackupsRoot();
		Directory.CreateDirectory(backupsRoot);

		var snapshotName = $"{DateTime.Now:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}";
		var snapshotDirectory = Path.Combine(backupsRoot, snapshotName);
		Directory.CreateDirectory(snapshotDirectory);

		var manifest = new BackupManifest
		{
			InstallationRoot = installation.Location,
			CreatedUtc = DateTimeOffset.UtcNow
		};

		foreach (var sourcePath in GetManagedFiles(installation))
		{
			var relativePath = Path.GetRelativePath(installation.Location, sourcePath);
			if (relativePath.StartsWith("..", StringComparison.Ordinal))
				continue;

			var entry = new BackupEntry
			{
				RelativePath = relativePath,
				Existed = File.Exists(sourcePath)
			};

			if (entry.Existed)
			{
				var backupPath = Path.Combine(snapshotDirectory, "files", relativePath);
				Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
				File.Copy(sourcePath, backupPath, overwrite: true);
				entry.BackupPath = Path.GetRelativePath(snapshotDirectory, backupPath);
			}

			manifest.Entries.Add(entry);
		}

		File.WriteAllText(
			Path.Combine(snapshotDirectory, ManifestFileName),
			JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

		AnsiConsole.MarkupLine($"[green]Backup created:[/] {snapshotDirectory.EscapeMarkup()}");
		return snapshotDirectory;
	}

	public static string? FindLatest(Installation installation, string? explicitSnapshot = null)
	{
		if (!string.IsNullOrWhiteSpace(explicitSnapshot))
		{
			var path = Path.GetFullPath(explicitSnapshot.Trim('"'));
			return Directory.Exists(path) ? path : null;
		}

		var root = GetBackupsRoot();
		if (!Directory.Exists(root))
			return null;

		return Directory.EnumerateDirectories(root)
			.Select(directory => new
			{
				Directory = directory,
				Manifest = ReadManifest(directory)
			})
			.Where(item =>
				item.Manifest != null &&
				PathsEqual(item.Manifest.InstallationRoot, installation.Location))
			.OrderByDescending(item => item.Manifest!.CreatedUtc)
			.Select(item => item.Directory)
			.FirstOrDefault();
	}

	public static int Rollback(Installation installation, string snapshotDirectory, bool dryRun)
	{
		var manifest = ReadManifest(snapshotDirectory)
			?? throw new InvalidDataException($"Backup manifest is missing or invalid: {snapshotDirectory}");

		if (!PathsEqual(manifest.InstallationRoot, installation.Location))
		{
			throw new InvalidDataException(
				$"Backup belongs to '{manifest.InstallationRoot}', not '{installation.Location}'.");
		}

		var changed = 0;
		foreach (var entry in manifest.Entries)
		{
			var destination = Path.GetFullPath(Path.Combine(installation.Location, entry.RelativePath));
			if (!IsInsideRoot(destination, installation.Location))
				throw new InvalidDataException($"Backup path escapes the game directory: {entry.RelativePath}");

			if (dryRun)
			{
				AnsiConsole.MarkupLine(
					$"[grey]Would {(entry.Existed ? "restore" : "remove")}[/] {destination.EscapeMarkup()}");
				continue;
			}

			if (entry.Existed)
			{
				if (string.IsNullOrWhiteSpace(entry.BackupPath))
					throw new InvalidDataException($"Backup payload is missing for {entry.RelativePath}.");

				var source = Path.GetFullPath(Path.Combine(snapshotDirectory, entry.BackupPath));
				if (!File.Exists(source))
					throw new FileNotFoundException("Backup payload is missing.", source);

				Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
				File.Copy(source, destination, overwrite: true);
			}
			else if (File.Exists(destination))
			{
				File.Delete(destination);
			}

			changed++;
		}

		return changed;
	}

	private static BackupManifest? ReadManifest(string snapshotDirectory)
	{
		try
		{
			var path = Path.Combine(snapshotDirectory, ManifestFileName);
			if (!File.Exists(path))
				return null;

			return JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(path));
		}
		catch
		{
			return null;
		}
	}

	private static string GetBackupsRoot()
	{
		var configured = Environment.GetEnvironmentVariable("EFT_TRAINER_BACKUP_DIR");
		if (!string.IsNullOrWhiteSpace(configured))
			return Path.GetFullPath(configured);

		return Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"EscapeFromTarkov-Trainer",
			"backups");
	}

	private static bool PathsEqual(string first, string second)
	{
		return string.Equals(
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(first)),
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(second)),
			StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsInsideRoot(string candidate, string root)
	{
		var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))
			+ Path.DirectorySeparatorChar;
		return candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
	}
}
