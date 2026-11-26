using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Installer;

internal sealed class FeatureProfile
{
	public string[] IncludeFeatures { get; set; } = [];
	public string[] ExcludeFeatures { get; set; } = [];
	public string[] IncludeCommands { get; set; } = [];
	public string[] ExcludeCommands { get; set; } = [];
}

internal sealed class FeatureSelection
{
	private static readonly string[] KnownFeatures =
	[
		"Aimbot",
		"AirDrop",
		"Ammunition",
		"AutomaticGun",
		"Bones",
		"Commands",
		"CrossHair",
		"Durability",
		"Examine",
		"ExfiltrationPoints",
		"FovChanger",
		"FreeCamera",
		"GameState",
		"Ghost",
		"Grenades",
		"Health",
		"Hits",
		"Hud",
		"Interact",
		"LootableContainers",
		"LootItems",
		"Map",
		"Mortar",
		"NightVision",
		"NoCollision",
		"NoFlash",
		"NoMalfunctions",
		"NoRecoil",
		"NoSway",
		"NoVisor",
		"Players",
		"Quests",
		"QuickThrow",
		"Radar",
		"Skills",
		"Speed",
		"Stamina",
		"Teleport",
		"ThermalVision",
		"Train",
		"WallShoot",
		"Weather",
		"WorldInteractiveObjects"
	];

	private static readonly string[] KnownCommands =
	[
		"Dump",
		"List",
		"ListRare",
		"ListSuperRare",
		"LoadTrackList",
		"SaveTrackList",
		"Spawn",
		"SpawnBot",
		"SpawnHideoutItems",
		"SpawnQuestItems",
		"Status",
		"Template",
		"Track",
		"TrackList",
		"TrackRare",
		"TrackSuperRare",
		"UnTrack"
	];

	private static readonly Dictionary<string, string> FeatureAliases = new(StringComparer.OrdinalIgnoreCase)
	{
		["esp"] = "Players",
		["players"] = "Players",
		["玩家"] = "Players",
		["aim"] = "Aimbot",
		["aimbot"] = "Aimbot",
		["loot"] = "LootItems",
		["战利品"] = "LootItems",
		["exfil"] = "ExfiltrationPoints",
		["撤离点"] = "ExfiltrationPoints",
		["quest"] = "Quests",
		["任务"] = "Quests",
		["map"] = "Map",
		["地图"] = "Map",
		["stash"] = "LootableContainers"
	};

	public string[] ExcludedFeatures { get; }
	public string[] ExcludedCommands { get; }

	private FeatureSelection(IEnumerable<string> features, IEnumerable<string> commands)
	{
		ExcludedFeatures = [.. features
			.Select(name => $"Features\\{name}.cs")
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];

		ExcludedCommands = [.. commands
			.Select(name => $"ConsoleCommands\\{name}.cs")
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)];
	}

	public static FeatureSelection Create(
		string[]? includeFeatures,
		string[]? excludeFeatures,
		string[]? includeCommands,
		string[]? excludeCommands,
		string? profilePath,
		out FeatureProfile effectiveProfile)
	{
		var profile = new FeatureProfile();
		if (!string.IsNullOrWhiteSpace(profilePath))
		{
			var fullPath = Path.GetFullPath(profilePath.Trim('"'));
			if (!File.Exists(fullPath))
				throw new FileNotFoundException("Feature profile was not found.", fullPath);

			profile = JsonSerializer.Deserialize<FeatureProfile>(
				File.ReadAllText(fullPath),
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
				?? throw new InvalidDataException("Feature profile is empty or invalid.");
		}

		var featureIncludes = Normalize((profile.IncludeFeatures ?? []).Concat(includeFeatures ?? []), KnownFeatures, FeatureAliases);
		var featureExcludes = Normalize((profile.ExcludeFeatures ?? []).Concat(excludeFeatures ?? []), KnownFeatures, FeatureAliases);
		var commandIncludes = Normalize((profile.IncludeCommands ?? []).Concat(includeCommands ?? []), KnownCommands, null);
		var commandExcludes = Normalize((profile.ExcludeCommands ?? []).Concat(excludeCommands ?? []), KnownCommands, null);

		ValidateNoConflicts(featureIncludes, featureExcludes, "feature");
		ValidateNoConflicts(commandIncludes, commandExcludes, "command");

		if (featureIncludes.Count > 0)
		{
			featureExcludes.UnionWith(KnownFeatures.Except(featureIncludes, StringComparer.OrdinalIgnoreCase));
		}

		if (commandIncludes.Count > 0)
		{
			commandExcludes.UnionWith(KnownCommands.Except(commandIncludes, StringComparer.OrdinalIgnoreCase));
		}

		effectiveProfile = new FeatureProfile
		{
			IncludeFeatures = [.. featureIncludes.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)],
			ExcludeFeatures = [.. featureExcludes.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)],
			IncludeCommands = [.. commandIncludes.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)],
			ExcludeCommands = [.. commandExcludes.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)]
		};

		return new(featureExcludes, commandExcludes);
	}

	private static HashSet<string> Normalize(
		IEnumerable<string> values,
		IReadOnlyCollection<string> knownNames,
		IReadOnlyDictionary<string, string>? aliases)
	{
		var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var separators = new[] { ',', ';' };
		foreach (var rawValue in values.SelectMany(value => value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
		{
			if (string.IsNullOrWhiteSpace(rawValue))
				continue;

			var value = Path.GetFileNameWithoutExtension(rawValue.Trim().Trim('"'));

			if (aliases is not null && aliases.TryGetValue(value, out var aliased))
				value = aliased;

			var canonical = knownNames.FirstOrDefault(name => name.Equals(value, StringComparison.OrdinalIgnoreCase));
			if (canonical == null)
				throw new ArgumentException($"Unknown selectable feature or command '{rawValue}'.");

			result.Add(canonical);
		}

		return result;
	}

	private static void ValidateNoConflicts(
		IReadOnlySet<string> includes,
		IReadOnlySet<string> excludes,
		string kind)
	{
		var conflicts = includes.Intersect(excludes, StringComparer.OrdinalIgnoreCase).ToArray();
		if (conflicts.Length > 0)
			throw new ArgumentException($"The same {kind} is both included and excluded: {string.Join(", ", conflicts)}.");
	}
}
