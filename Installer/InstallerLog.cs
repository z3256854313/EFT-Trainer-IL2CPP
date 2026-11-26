using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Installer;

internal sealed class InstallerLog : IDisposable
{
	private readonly TextWriter _originalOut;
	private readonly TextWriter _originalError;
	private readonly StreamWriter _fileWriter;
	private readonly TeeTextWriter _outWriter;
	private readonly TeeTextWriter _errorWriter;
	private bool _disposed;

	public string Path { get; }
	public static InstallerLog? Current { get; private set; }

	private InstallerLog(string path, TextWriter originalOut, TextWriter originalError)
	{
		Path = path;
		_originalOut = originalOut;
		_originalError = originalError;
		Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
		_fileWriter = new StreamWriter(path, append: true, Encoding.UTF8)
		{
			AutoFlush = true
		};

		_outWriter = new TeeTextWriter(originalOut, _fileWriter);
		_errorWriter = new TeeTextWriter(originalError, _fileWriter);
		Console.SetOut(_outWriter);
		Console.SetError(_errorWriter);
	}

	public static InstallerLog Start()
	{
		var candidates = new[]
		{
			Environment.GetEnvironmentVariable("EFT_TRAINER_LOG_DIR"),
			System.IO.Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				"EscapeFromTarkov-Trainer",
				"logs"),
			System.IO.Path.Combine(
				System.IO.Path.GetTempPath(),
				"EscapeFromTarkov-Trainer",
				"logs")
		};

		var fileName = $"installer-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log";
		Exception? lastException = null;
		foreach (var directory in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
		{
			try
			{
				Directory.CreateDirectory(directory!);
				var log = new InstallerLog(System.IO.Path.Combine(directory!, fileName), Console.Out, Console.Error);
				Current = log;
				log.WriteLine("EscapeFromTarkov-Trainer installer log");
				log.WriteLine($"Started: {DateTimeOffset.Now:O}");
				log.WriteLine($"Command: {Environment.CommandLine}");
				log.WriteLine(string.Empty);
				return log;
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				lastException = exception;
			}
		}

		throw new IOException("Unable to create an installer log file.", lastException);
	}

	public void WriteLine(string message)
	{
		_fileWriter.WriteLine(message);
	}

	public void WriteException(Exception exception)
	{
		_fileWriter.WriteLine();
		_fileWriter.WriteLine($"Unhandled exception: {exception}");
		_fileWriter.WriteLine();
	}

	public void Dispose()
	{
		if (_disposed)
			return;

		_disposed = true;
		_outWriter.Flush();
		_errorWriter.Flush();
		Console.SetOut(_originalOut);
		Console.SetError(_originalError);
		_fileWriter.Dispose();
		if (ReferenceEquals(Current, this))
			Current = null;
	}

	private sealed class TeeTextWriter(TextWriter first, TextWriter second) : TextWriter
	{
		private readonly object _sync = new();

		public override Encoding Encoding => first.Encoding;

		public override void Flush()
		{
			lock (_sync)
			{
				first.Flush();
				second.Flush();
			}
		}

		public override void Write(char value)
		{
			lock (_sync)
			{
				first.Write(value);
				second.Write(value);
			}
		}

		public override void Write(string? value)
		{
			lock (_sync)
			{
				first.Write(value);
				second.Write(value);
			}
		}

		public override void Write(char[] buffer, int index, int count)
		{
			lock (_sync)
			{
				first.Write(buffer, index, count);
				second.Write(buffer, index, count);
			}
		}
	}
}
