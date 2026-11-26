using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace TrainerIL2CPP;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class TrainerPlugin : BasePlugin
{
	public const string PluginGuid = "com.local.efttrainer.il2cpp";
	public const string PluginName = "EFT Trainer IL2CPP";
	public const string PluginVersion = "1.18.0";

	internal static ManualLogSource Logger { get; private set; } = null!;
	internal static TrainerSettings Settings { get; private set; } = null!;

	public override void Load()
	{
		Logger = Log;
		Settings = new TrainerSettings(Config);
		QuestObjectivePatch.Apply(Log);
		HealthLockPatch.Apply(Log);
		GetItemHoverPatch.Apply(Log);
		GetItemContextMenuPatch.Apply(Log);
		SpatialAudioCrashGuard.TryApply(Log);
		AddComponent<TrainerBehaviour>();
		Log.LogInfo($"{PluginName} {PluginVersion} loaded. Press Right Alt to open the panel.");
	}
}
