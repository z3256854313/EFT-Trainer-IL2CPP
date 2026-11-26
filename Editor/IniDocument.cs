using System.Globalization;

namespace TrainerEditor;

internal sealed class IniDocument
{
	private readonly List<string> _lines;

	public string Text => string.Join(Environment.NewLine, _lines) + Environment.NewLine;

	private IniDocument(IEnumerable<string> lines)
	{
		_lines = [.. lines];
	}

	public static IniDocument Empty()
	{
		return new(
		[
			"; EscapeFromTarkov-Trainer configuration",
			"; Created by TrainerEditor.exe",
			string.Empty
		]);
	}

	public static IniDocument Load(string path)
	{
		return File.Exists(path)
			? FromText(File.ReadAllText(path))
			: Empty();
	}

	public static IniDocument FromText(string text)
	{
		return new(text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
	}

	public string? Get(string key)
	{
		var prefix = key + "=";
		return _lines.FirstOrDefault(line => line.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
	}

	public bool GetBool(string key, bool fallback)
	{
		return bool.TryParse(Get(key)?.Trim().Trim('"'), out var value) ? value : fallback;
	}

	public decimal GetDecimal(string key, decimal fallback)
	{
		return decimal.TryParse(
			Get(key)?.Trim().Trim('"'),
			NumberStyles.Float,
			CultureInfo.InvariantCulture,
			out var value)
			? value
			: fallback;
	}

	public string GetString(string key, string fallback)
	{
		var value = Get(key);
		return value == null ? fallback : value.Trim().Trim('"');
	}

	public void Set(string key, string rawValue)
	{
		var newLine = $"{key}={rawValue}";
		var indexes = new List<int>();
		for (var index = 0; index < _lines.Count; index++)
		{
			if (_lines[index].StartsWith(key + "=", StringComparison.Ordinal))
				indexes.Add(index);
		}

		if (indexes.Count == 0)
		{
			var featureOrder = _lines.FindIndex(line =>
				line.StartsWith("FeatureOrder=", StringComparison.Ordinal));
			_lines.Insert(featureOrder >= 0 ? featureOrder : _lines.Count, newLine);
			return;
		}

		_lines[indexes[0]] = newLine;
		for (var index = indexes.Count - 1; index >= 1; index--)
			_lines.RemoveAt(indexes[index]);
	}

	public void SetBool(string key, bool value)
	{
		Set(key, value ? "true" : "false");
	}

	public void SetDecimal(string key, decimal value)
	{
		Set(key, value.ToString("0.###", CultureInfo.InvariantCulture));
	}

	public void SetString(string key, string value)
	{
		Set(key, $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"");
	}

	public IReadOnlyList<(string Key, string Value)> GetEntries()
	{
		var entries = new List<(string, string)>();
		var seen = new HashSet<string>(StringComparer.Ordinal);

		foreach (var line in _lines)
		{
			var separator = line.IndexOf('=');
			if (separator <= 0)
				continue;

			var key = line[..separator].Trim();
			if (key.Length == 0 || key[0] is ';' or '#' || !seen.Add(key))
				continue;

			entries.Add((key, line[(separator + 1)..]));
		}

		return entries;
	}
}
