using BepInEx.Configuration;

namespace TrainerIL2CPP;

internal sealed class TrainerSettings
{
	public ConfigEntry<string> PanelKey { get; }
	public ConfigEntry<bool> AimbotEnabled { get; }
	public ConfigEntry<string> AimbotKey { get; }
	public ConfigEntry<string> TargetPart { get; }
	public ConfigEntry<bool> PreferHeadChest { get; }
	public ConfigEntry<bool> VisibleOnly { get; }
	public ConfigEntry<float> Smoothness { get; }
	public ConfigEntry<float> AimLeadMultiplier { get; }
	public ConfigEntry<float> AimbotDistance { get; }
	public ConfigEntry<float> FovRadius { get; }
	public ConfigEntry<bool> EspEnabled { get; }
	public ConfigEntry<bool> ShowBoxes { get; }
	public ConfigEntry<bool> XRay { get; }
	public ConfigEntry<float> BoxThickness { get; }
	public ConfigEntry<float> BoxPadding { get; }
	public ConfigEntry<float> EspDistance { get; }
	public ConfigEntry<bool> HealthEnabled { get; }
	public ConfigEntry<string> HealthLockMode { get; }
	public ConfigEntry<float> HeadMinimumHealth { get; }
	public ConfigEntry<float> ChestMinimumHealth { get; }
	public ConfigEntry<float> StomachMinimumHealth { get; }
	public ConfigEntry<float> LeftArmMinimumHealth { get; }
	public ConfigEntry<float> RightArmMinimumHealth { get; }
	public ConfigEntry<float> LeftLegMinimumHealth { get; }
	public ConfigEntry<float> RightLegMinimumHealth { get; }
	public ConfigEntry<bool> FoodWater { get; }
	public ConfigEntry<bool> RemoveNegativeEffects { get; }
	public ConfigEntry<bool> RemoveEnergyEffects { get; }
	public ConfigEntry<bool> RemoveBuffEffects { get; }
	public ConfigEntry<bool> RemoveLimbDamageEffects { get; }
	public ConfigEntry<bool> FallDamageImmunity { get; }
	public ConfigEntry<bool> InfiniteStamina { get; }
	public ConfigEntry<bool> NoMalfunctions { get; }
	public ConfigEntry<bool> NightVision { get; }
	public ConfigEntry<bool> NoFlash { get; }
	public ConfigEntry<bool> NoVisor { get; }
	public ConfigEntry<bool> LootEspEnabled { get; }
	public ConfigEntry<string> LootFilter { get; }
	public ConfigEntry<bool> ShowLootPrices { get; }
	public ConfigEntry<float> LootDistance { get; }
	public ConfigEntry<bool> ShowCorpses { get; }
	public ConfigEntry<bool> ShowQuestItems { get; }
	public ConfigEntry<bool> ShowQuestItemsInContainers { get; }
	public ConfigEntry<bool> QuestItemsActiveOnly { get; }
	public ConfigEntry<bool> ShowContainers { get; }
	public ConfigEntry<bool> QuestLocations { get; }
	public ConfigEntry<bool> ShowRegularQuests { get; }
	public ConfigEntry<bool> ShowStoryQuests { get; }
	public ConfigEntry<float> QuestDistance { get; }
	public ConfigEntry<bool> ExfilEspEnabled { get; }
	public ConfigEntry<bool> ShowUnavailableExfiltrationPoints { get; }
	public ConfigEntry<bool> ShowTransitPoints { get; }
	public ConfigEntry<bool> ShowSkeletons { get; }
	public ConfigEntry<bool> MapEnabled { get; }
	public ConfigEntry<string> MapKey { get; }
	public ConfigEntry<float> MapRange { get; }
	public ConfigEntry<float> MapZoomOut { get; }
	public ConfigEntry<bool> MapTeleportEnabled { get; }
	public ConfigEntry<string> MapTeleportMarkerKey { get; }
	public ConfigEntry<string> MapTeleportExecuteKey { get; }
	public ConfigEntry<bool> BodyTint { get; }
	public ConfigEntry<bool> GetItemMenuEnabled { get; }
	public ConfigEntry<bool> QuestSkipEnabled { get; }
	public ConfigEntry<bool> UnlockFleaMarket { get; }
	public ConfigEntry<string> EspKeys { get; }
	public ConfigEntry<string> LootKeys { get; }
	public ConfigEntry<string> QuestKeys { get; }
	public ConfigEntry<string> HealthKeys { get; }
	public ConfigEntry<string> NightVisionKeys { get; }
	public ConfigEntry<string> UnlockNearbyDoorsKey { get; }

	public TrainerSettings(ConfigFile config)
	{
		PanelKey = config.Bind("General", "PanelKey", "RightAlt", "Key used to show or hide the in-game panel.");
		AimbotEnabled = config.Bind("Aimbot", "Enabled", false, "Enable target locking.");
		AimbotKey = config.Bind("Aimbot", "HoldKey", "Slash", "Hold this key to lock the selected body part.");
		TargetPart = config.Bind("Aimbot", "TargetPart", "Head", "Head, Chest, Stomach, Arms or Legs.");
		PreferHeadChest = config.Bind("Aimbot", "PreferHeadChest", true, "Target head first, then chest, then the selected part.");
		VisibleOnly = config.Bind("Aimbot", "VisibleOnly", true, "Skip body points hidden behind geometry.");
		Smoothness = config.Bind("Aimbot", "Smoothness", 0.35f, new ConfigDescription("Aim smoothing from 0.01 to 1.", new AcceptableValueRange<float>(0.01f, 1f)));
		AimLeadMultiplier = config.Bind("Aimbot", "LeadMultiplier", 1f, new ConfigDescription("Moving-target lead multiplier. Zero disables velocity compensation.", new AcceptableValueRange<float>(0f, 2.5f)));
		AimbotDistance = config.Bind("Aimbot", "MaximumDistance", 200f, new ConfigDescription("Maximum locking distance.", new AcceptableValueRange<float>(1f, 2000f)));
		FovRadius = config.Bind("Aimbot", "FovRadius", 50f, new ConfigDescription("Locking radius in screen pixels. Zero disables the limit.", new AcceptableValueRange<float>(0f, 2000f)));

		EspEnabled = config.Bind("ESP", "Enabled", false, "Enable player ESP.");
		ShowBoxes = config.Bind("ESP", "ShowBoxes", true, "Draw player boxes.");
		XRay = config.Bind("ESP", "XRay", true, "Draw boxes through geometry.");
		BoxThickness = config.Bind("ESP", "BoxThickness", 1.5f, new ConfigDescription("Box border thickness.", new AcceptableValueRange<float>(0.5f, 8f)));
		BoxPadding = config.Bind("ESP", "BoxPadding", 0.08f, new ConfigDescription("Extra box padding as a fraction of its short side.", new AcceptableValueRange<float>(0f, 0.5f)));
		EspDistance = config.Bind("ESP", "MaximumDistance", 0f, new ConfigDescription("Maximum ESP distance. Zero means unlimited.", new AcceptableValueRange<float>(0f, 2000f)));

		HealthEnabled = config.Bind("Survival", "HealthEnabled", false, "Block all health reductions for the local player. Existing HP is preserved, not continuously healed.");
		HealthLockMode = config.Bind("Survival", "HealthLockMode", "Full", "Full = block all damage below every body part's current value. Threshold = allow damage and bleeding until each configured body-part floor.");
		HeadMinimumHealth = BindThreshold(config, "HeadMinimumHealth", 15f);
		ChestMinimumHealth = BindThreshold(config, "ChestMinimumHealth", 30f);
		StomachMinimumHealth = BindThreshold(config, "StomachMinimumHealth", 25f);
		LeftArmMinimumHealth = BindThreshold(config, "LeftArmMinimumHealth", 20f);
		RightArmMinimumHealth = BindThreshold(config, "RightArmMinimumHealth", 20f);
		LeftLegMinimumHealth = BindThreshold(config, "LeftLegMinimumHealth", 20f);
		RightLegMinimumHealth = BindThreshold(config, "RightLegMinimumHealth", 20f);
		FoodWater = config.Bind("Survival", "FoodWater", true, "Keep energy and hydration full while health lock is enabled.");
		RemoveNegativeEffects = config.Bind("Survival", "RemoveNegativeEffects", true, "Master switch for categorized negative-effect cleanup while health lock is enabled.");
		RemoveEnergyEffects = config.Bind("Survival", "RemoveEnergyEffects", true, "Remove dehydration, exhaustion, chronic fatigue and low-edge health effects.");
		RemoveBuffEffects = config.Bind("Survival", "RemoveBuffEffects", true, "Remove harmful status/debuff effects while preserving positive medicine and stimulator buffs.");
		RemoveLimbDamageEffects = config.Bind("Survival", "RemoveLimbDamageEffects", true, "Remove bleeding, fracture, wound, frostbite and muscle-pain effects.");
		FallDamageImmunity = config.Bind("Survival", "FallDamageImmunity", true, "Disable local-player fall damage while health lock is enabled.");
		InfiniteStamina = config.Bind("Survival", "InfiniteStamina", false, "Keep local-player stamina and hands stamina full.");
		NoMalfunctions = config.Bind("Survival", "NoMalfunctions", false, "Disable feed, jam, misfire, overheat and slide malfunctions on the equipped weapon.");
		NightVision = config.Bind("Vision", "NightVision", false, "Enable the camera night-vision effect.");
		NoFlash = config.Bind("Vision", "NoFlash", false, "Disable flash and eye-burn effects.");
		NoVisor = config.Bind("Vision", "NoVisor", false, "Remove the equipped visor overlay.");
		LootEspEnabled = config.Bind("LootESP", "Enabled", false, "Highlight matching world loot.");
		LootFilter = config.Bind("LootESP", "Filter", "LEDX,GPU,Bitcoin,Graphics,Intel,Lab", "Comma-separated item name fragments. Empty means all loot.");
		ShowLootPrices = config.Bind("LootESP", "ShowPrices", true, "Append the template credit price to loot labels.");
		LootDistance = config.Bind("LootESP", "MaximumDistance", 0f, new ConfigDescription("Maximum distance for drawing cached loot markers. 0 means unlimited. Does not affect the full-map cache or the fixed 30 m nearby state refresh.", new AcceptableValueRange<float>(0f, 2000f)));
		ShowCorpses = config.Bind("LootESP", "ShowCorpses", true, "Show corpse markers.");
		ShowQuestItems = config.Bind("LootESP", "ShowQuestItems", true, "Show quest-item markers as part of the quest-location feature.");
		ShowQuestItemsInContainers = config.Bind("LootESP", "ShowQuestItemsInContainers", false, "Show containers holding relevant quest items as quest markers.");
		QuestItemsActiveOnly = config.Bind("LootESP", "QuestItemsActiveOnly", true, "Only show quest items required by currently unfinished quest objectives.");
		ShowContainers = config.Bind("LootESP", "ShowContainers", true, "Show searchable world loot containers.");
		QuestLocations = config.Bind("Quests", "ShowLocations", false, "Enable markers for started-quest locations and active quest items.");
		ShowRegularQuests = config.Bind("Quests", "ShowRegularQuests", true, "Show regular quest locations and quest items.");
		ShowStoryQuests = config.Bind("Quests", "ShowStoryQuests", true, "Show story quest locations and story quest items.");
		QuestDistance = config.Bind("Quests", "MaximumDistance", 500f, new ConfigDescription("Maximum quest marker distance.", new AcceptableValueRange<float>(1f, 2000f)));
		ExfilEspEnabled = config.Bind("ESP", "ExfiltrationPoints", false, "Show exfiltration point names, status and distance.");
		ShowUnavailableExfiltrationPoints = config.Bind("ESP", "ShowUnavailableExfiltrationPoints", true, "Show exfiltration points whose requirements are not currently met.");
		ShowTransitPoints = config.Bind("ESP", "ShowTransitPoints", true, "Show map transit points in addition to exfiltration points.");
		ShowSkeletons = config.Bind("ESP", "ShowSkeletons", false, "Draw player bone skeletons.");
		MapEnabled = config.Bind("Map", "Enabled", false, "Show the full-screen top-down map.");
		MapKey = config.Bind("Map", "ToggleKey", "M", "Key used to toggle the full-screen map.");
		MapRange = config.Bind("Map", "Range", 400f, new ConfigDescription("Fallback orthographic radius used only when map border bounds are unavailable.", new AcceptableValueRange<float>(50f, 1500f)));
		MapZoomOut = config.Bind("Map", "ZoomOut", 1.15f, new ConfigDescription("Map view zoom-out multiplier. Higher values make the map smaller.", new AcceptableValueRange<float>(1f, 2.5f)));
		MapTeleportEnabled = config.Bind("Map", "ClickTeleport", false, "Legacy config key. Enables marker-based keyboard teleportation on the full-screen map.");
		MapTeleportMarkerKey = config.Bind("Hotkeys", "MapTeleportMarker", "LeftBracket", "Place the teleport marker at the map center.");
		MapTeleportExecuteKey = config.Bind("Hotkeys", "MapTeleportExecute", "RightBracket", "Teleport to the current map marker.");
		BodyTint = config.Bind("ESP", "BodyTint", false, "Tint living player body materials using faction colors.");
		GetItemMenuEnabled = config.Bind(
			"Convenience",
			"GetItemMenu",
			true,
			"Show Get Item outside raids only. Quest and story items are excluded.");
		QuestSkipEnabled = config.Bind("Convenience", "QuestSkipButton", true, "Show a skip/complete button for the selected trader quest.");
		UnlockFleaMarket = config.Bind("Convenience", "UnlockFleaMarket", true, "Raise profile experience to the flea market level and enable flea rating.");
		EspKeys = config.Bind("Hotkeys", "PlayerESP", string.Empty, "Up to three keys for the player ESP toggle, comma-separated.");
		LootKeys = config.Bind("Hotkeys", "LootESP", string.Empty, "Up to three keys for the loot ESP toggle, comma-separated.");
		QuestKeys = config.Bind("Hotkeys", "QuestLocations", string.Empty, "Up to three keys for the quest-location toggle, comma-separated.");
		HealthKeys = config.Bind("Hotkeys", "Health", string.Empty, "Up to three keys for the health-lock toggle, comma-separated.");
		NightVisionKeys = config.Bind("Hotkeys", "NightVision", string.Empty, "Up to three keys for the night-vision toggle, comma-separated.");
		UnlockNearbyDoorsKey = config.Bind("Hotkeys", "UnlockNearbyDoors", "KeypadPeriod", "Unlock locked world doors within about 2.24 metres of the local player.");
		config.Save();
	}

	public void Save()
	{
		PanelKey.ConfigFile.Save();
	}

	private static ConfigEntry<float> BindThreshold(
		ConfigFile config,
		string key,
		float defaultValue)
	{
		return config.Bind(
			"HealthThreshold",
			key,
			defaultValue,
			new ConfigDescription(
				"Minimum health retained by threshold health lock.",
				new AcceptableValueRange<float>(0f, 150f)));
	}
}
