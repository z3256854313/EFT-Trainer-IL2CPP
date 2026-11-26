using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace EftTrainerDownloader;

internal sealed record DownloadProgress(long ReceivedBytes, long? TotalBytes)
{
	public int? Percent => TotalBytes is > 0
		? (int)Math.Clamp(ReceivedBytes * 100L / TotalBytes.Value, 0L, 100L)
		: null;
}

internal sealed record InstallResult(
	string Version,
	string Sha256,
	string InstallDirectory,
	string? BackupPath);

internal sealed class ReleaseInstaller
{
	internal const string RepositoryUrl = "https://github.com/z3256854313/EFT-Trainer-IL2CPP";
	internal const string LatestPackageUrl =
		"https://github.com/z3256854313/EFT-Trainer-IL2CPP/releases/latest/download/EFT-Trainer-Latest.zip";

	private const string PluginFileName = "TrainerIL2CPP.dll";
	private const string ManifestFileName = "release.json";

	private static readonly HttpClient HttpClient = CreateHttpClient();
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true
	};

	public async Task<InstallResult> InstallLatestAsync(
		string eftRoot,
		IProgress<DownloadProgress>? progress = null,
		Action<string>? log = null,
		CancellationToken cancellationToken = default)
	{
		var normalizedRoot = ValidateEftRoot(eftRoot);
		var tempRoot = Path.Combine(
			Path.GetTempPath(),
			"EFT-Trainer-Downloader",
			Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(tempRoot);

		try
		{
			var packagePath = Path.Combine(tempRoot, "EFT-Trainer-Latest.zip");
			log?.Invoke($"正在连接 GitHub：{LatestPackageUrl}");
			await DownloadFileAsync(packagePath, progress, cancellationToken);

			var extractPath = Path.Combine(tempRoot, "package");
			Directory.CreateDirectory(extractPath);
			log?.Invoke("下载完成，正在校验并解压安装包。");
			ExtractSafely(packagePath, extractPath);

			var manifestPath = Directory
				.EnumerateFiles(extractPath, ManifestFileName, SearchOption.AllDirectories)
				.FirstOrDefault()
				?? throw new InvalidDataException("安装包内缺少 release.json。");
			var manifest = JsonSerializer.Deserialize<ReleaseManifest>(
				await File.ReadAllTextAsync(manifestPath, cancellationToken),
				JsonOptions)
				?? throw new InvalidDataException("release.json 内容无效。");

			if (string.IsNullOrWhiteSpace(manifest.Version) ||
				string.IsNullOrWhiteSpace(manifest.Dll) ||
				string.IsNullOrWhiteSpace(manifest.DllSha256))
			{
				throw new InvalidDataException("release.json 缺少版本、DLL 文件名或 SHA-256。");
			}

			var manifestDirectory = Path.GetDirectoryName(manifestPath)
				?? throw new InvalidDataException("无法确定安装包目录。");
			var sourceDll = Path.GetFullPath(Path.Combine(manifestDirectory, manifest.Dll));
			if (!File.Exists(sourceDll))
				throw new FileNotFoundException("安装包内缺少插件 DLL。", sourceDll);

			var actualHash = await ComputeSha256Async(sourceDll, cancellationToken);
			if (!string.Equals(actualHash, manifest.DllSha256, StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidDataException(
					$"DLL 校验失败。期望 {manifest.DllSha256}，实际 {actualHash}。");
			}

			log?.Invoke($"版本 {manifest.Version} 校验通过：{actualHash}");
			var installDirectory = Path.Combine(
				normalizedRoot,
				"BepInEx",
				"plugins",
				"TrainerIL2CPP");
			Directory.CreateDirectory(installDirectory);

			var targetDll = Path.Combine(installDirectory, PluginFileName);
			var tempDll = Path.Combine(
				installDirectory,
				$"{PluginFileName}.new-{Guid.NewGuid():N}");
			var backupPath = File.Exists(targetDll)
				? Path.Combine(
					installDirectory,
					$"{PluginFileName}.bak-{DateTime.Now:yyyyMMdd-HHmmss}")
				: null;

			File.Copy(sourceDll, tempDll, overwrite: true);
			try
			{
				if (backupPath != null)
				{
					if (File.Exists(backupPath))
						File.Delete(backupPath);
					File.Move(targetDll, backupPath);
					log?.Invoke($"已备份旧插件：{backupPath}");
				}

				File.Move(tempDll, targetDll);
			}
			catch
			{
				if (File.Exists(tempDll))
					File.Delete(tempDll);
				if (backupPath != null && File.Exists(backupPath) && !File.Exists(targetDll))
					File.Move(backupPath, targetDll);
				throw;
			}

			log?.Invoke($"已安装到：{targetDll}");
			return new InstallResult(manifest.Version, actualHash, installDirectory, backupPath);
		}
		finally
		{
			try
			{
				if (Directory.Exists(tempRoot))
					Directory.Delete(tempRoot, recursive: true);
			}
			catch
			{
				// A locked antivirus scan must not hide the real install result.
			}
		}
	}

	public static string? TryAutoDetectEftRoot()
	{
		var candidates = new List<string>();
		foreach (var drive in DriveInfo.GetDrives())
		{
			if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
				continue;

			var driveRoot = drive.RootDirectory.FullName;
			candidates.Add(Path.Combine(driveRoot, "EFT"));
			candidates.Add(Path.Combine(driveRoot, "EscapeFromTarkov"));
			candidates.Add(Path.Combine(driveRoot, "SPT"));
		}

		candidates.Add(Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
			"EscapeFromTarkov"));
		candidates.Add(Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
			"EscapeFromTarkov"));

		return candidates
			.Select(TryNormalizePath)
			.Where(path => path != null)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.FirstOrDefault();
	}

	public static string ValidateEftRoot(string eftRoot)
	{
		if (string.IsNullOrWhiteSpace(eftRoot))
			throw new DirectoryNotFoundException("请选择 EFT / SPT 根目录。");

		var normalizedRoot = TryNormalizePath(eftRoot)
			?? throw new DirectoryNotFoundException($"目录不存在：{eftRoot}");
		if (!File.Exists(Path.Combine(normalizedRoot, "EscapeFromTarkov.exe")))
			throw new DirectoryNotFoundException("所选目录中没有 EscapeFromTarkov.exe。");
		if (!Directory.Exists(Path.Combine(normalizedRoot, "BepInEx")))
			throw new DirectoryNotFoundException("所选目录中缺少 BepInEx。请先安装兼容的 BepInEx IL2CPP。");

		return normalizedRoot;
	}

	private static async Task DownloadFileAsync(
		string destinationPath,
		IProgress<DownloadProgress>? progress,
		CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Get, LatestPackageUrl);
		request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
		{
			NoCache = true
		};
		using var response = await HttpClient.SendAsync(
			request,
			HttpCompletionOption.ResponseHeadersRead,
			cancellationToken);
		response.EnsureSuccessStatusCode();

		var totalBytes = response.Content.Headers.ContentLength;
		await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
		await using var destination = new FileStream(
			destinationPath,
			FileMode.Create,
			FileAccess.Write,
			FileShare.None,
			81920,
			useAsync: true);

		var buffer = new byte[81920];
		long receivedBytes = 0;
		int read;
		while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
		{
			await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
			receivedBytes += read;
			progress?.Report(new DownloadProgress(receivedBytes, totalBytes));
		}
	}

	private static void ExtractSafely(string archivePath, string destinationDirectory)
	{
		var destinationRoot = Path.GetFullPath(destinationDirectory)
			.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
			+ Path.DirectorySeparatorChar;
		using var archive = ZipFile.OpenRead(archivePath);
		foreach (var entry in archive.Entries)
		{
			var destination = Path.GetFullPath(
				Path.Combine(destinationDirectory, entry.FullName));
			if (!destination.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
				throw new InvalidDataException($"安装包包含非法路径：{entry.FullName}");
		}
		archive.ExtractToDirectory(destinationDirectory, overwriteFiles: true);
	}

	private static async Task<string> ComputeSha256Async(
		string filePath,
		CancellationToken cancellationToken)
	{
		await using var stream = new FileStream(
			filePath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			81920,
			useAsync: true);
		var hash = await SHA256.HashDataAsync(stream, cancellationToken);
		return Convert.ToHexString(hash);
	}

	private static string? TryNormalizePath(string? path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return null;
		try
		{
			var fullPath = Path.GetFullPath(path.Trim().Trim('"'));
			return Directory.Exists(fullPath)
				? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
				: null;
		}
		catch
		{
			return null;
		}
	}

	private static HttpClient CreateHttpClient()
	{
		var client = new HttpClient
		{
			Timeout = TimeSpan.FromMinutes(10)
		};
		client.DefaultRequestHeaders.UserAgent.ParseAdd(
			"EFT-Trainer-Downloader/1.0 (+https://github.com/z3256854313/EFT-Trainer-IL2CPP)");
		client.DefaultRequestHeaders.Accept.ParseAdd("application/octet-stream");
		return client;
	}

	private sealed class ReleaseManifest
	{
		public string Version { get; init; } = string.Empty;
		public string Dll { get; init; } = string.Empty;
		public string DllSha256 { get; init; } = string.Empty;
	}
}
