using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace Installer;

internal static class SourceArchiveManager
{
	private const long MaximumSnapshotBytes = 256L * 1024L * 1024L;

	public static async Task<ZipArchive?> OpenAsync(CompilationContext context, CancellationToken ct)
	{
		try
		{
			if (!string.IsNullOrWhiteSpace(context.SourcePath))
			{
				var path = Path.GetFullPath(context.SourcePath.Trim('"'));
				if (!File.Exists(path))
				{
					AnsiConsole.MarkupLine($"[red]Source ZIP not found:[/] {path.EscapeMarkup()}");
					context.IsFatalFailure = true;
					return null;
				}

				AnsiConsole.MarkupLine($"[blue]Using local source:[/] {path.EscapeMarkup()}");
				var localArchive = OpenArchive(await File.ReadAllBytesAsync(path, ct), path);
				if (localArchive == null)
					context.IsFatalFailure = true;
				return localArchive;
			}

			var cachePath = GetCachePath(context);
			if (File.Exists(cachePath) && !context.RefreshSource)
			{
				AnsiConsole.MarkupLine($"[blue]Using cached source:[/] {cachePath.EscapeMarkup()}");
				var cachedArchive = OpenArchive(await File.ReadAllBytesAsync(cachePath, ct), cachePath);
				if (cachedArchive != null)
					return cachedArchive;

				if (context.Offline)
				{
					context.IsFatalFailure = true;
					return null;
				}

				AnsiConsole.MarkupLine("[yellow]Cached source is invalid; downloading a fresh snapshot.[/]");
				File.Delete(cachePath);
			}

			if (context.Offline)
			{
				AnsiConsole.MarkupLine(
					$"[red]Offline mode has no cached snapshot for branch {context.Branch.EscapeMarkup()}:[/] {cachePath.EscapeMarkup()}");
				context.IsFatalFailure = true;
				return null;
			}

			var status = $"Downloading repository snapshot ({context.Branch} branch)...";
			ZipArchive? result = null;

			await AnsiConsole.Status().StartAsync(status, async statusContext =>
			{
				using var client = new HttpClient
				{
					Timeout = TimeSpan.FromMinutes(5)
				};

				var uri = new Uri(
					$"https://github.com/sailro/EscapeFromTarkov-Trainer/archive/refs/heads/{context.Branch}.zip");
				using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
				if (!response.IsSuccessStatusCode)
				{
					AnsiConsole.MarkupLine(
						$"[yellow]Snapshot request for branch {context.Branch.EscapeMarkup()} returned {(int)response.StatusCode}.[/]");
					return;
				}

				if (response.Content.Headers.ContentLength > MaximumSnapshotBytes)
					throw new InvalidDataException("Repository snapshot is larger than the 256 MiB safety limit.");

				var buffer = await response.Content.ReadAsByteArrayAsync(ct);
				if (buffer.LongLength > MaximumSnapshotBytes)
					throw new InvalidDataException("Repository snapshot is larger than the 256 MiB safety limit.");

				result = OpenArchive(buffer, uri.ToString());
				if (result == null)
				{
					context.IsFatalFailure = true;
					return;
				}

				var directory = Path.GetDirectoryName(cachePath)!;
				Directory.CreateDirectory(directory);
				var temporary = $"{cachePath}.{Guid.NewGuid():N}.tmp";
				try
				{
					await File.WriteAllBytesAsync(temporary, buffer, ct);
					File.Move(temporary, cachePath, overwrite: true);
					AnsiConsole.MarkupLine($"[green]Cached source:[/] {cachePath.EscapeMarkup()}");
				}
				finally
				{
					if (File.Exists(temporary))
						File.Delete(temporary);
				}
			});

			return result;
		}
		catch (OperationCanceledException)
		{
			context.IsFatalFailure = true;
			throw;
		}
		catch (Exception exception)
		{
			context.IsFatalFailure = true;
			AnsiConsole.MarkupLine($"[red]Unable to load source snapshot:[/] {exception.Message.EscapeMarkup()}");
			return null;
		}
	}

	private static ZipArchive? OpenArchive(byte[] buffer, string sourceName)
	{
		try
		{
			var archive = new ZipArchive(new MemoryStream(buffer, writable: false), ZipArchiveMode.Read);
			if (!archive.Entries.Any(entry => entry.Name.Equals("NLog.EFT.Trainer.csproj", StringComparison.OrdinalIgnoreCase)))
			{
				archive.Dispose();
				AnsiConsole.MarkupLine(
					$"[red]Source snapshot does not contain NLog.EFT.Trainer.csproj:[/] {sourceName.EscapeMarkup()}");
				return null;
			}

			return archive;
		}
		catch (InvalidDataException exception)
		{
			AnsiConsole.MarkupLine($"[red]Invalid source ZIP:[/] {exception.Message.EscapeMarkup()}");
			return null;
		}
	}

	private static string GetCachePath(CompilationContext context)
	{
		var cacheDirectory = context.CacheDirectory;
		if (string.IsNullOrWhiteSpace(cacheDirectory))
		{
			cacheDirectory = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"EscapeFromTarkov-Trainer",
				"snapshots");
		}

		var safeBranch = string.Concat(
			context.Branch.Select(character =>
				Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
		return Path.Combine(Path.GetFullPath(cacheDirectory), $"{safeBranch}.zip");
	}
}
