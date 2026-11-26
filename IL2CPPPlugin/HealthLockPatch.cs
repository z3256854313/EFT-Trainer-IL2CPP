using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using HarmonyLib;

namespace TrainerIL2CPP;

internal static class HealthLockPatch
{
	private static Harmony? _harmony;
	private static ManualLogSource? _logger;
	private static readonly HashSet<System.Reflection.MethodInfo> PatchedMethods = [];
	private static bool _patchesActive;

	public static void Apply(ManualLogSource logger)
	{
		_logger = logger;
		_harmony = new Harmony(TrainerPlugin.PluginGuid + ".health");
		SyncEnabled();
	}

	internal static void SyncEnabled()
	{
		var enabled = TrainerPlugin.Settings.HealthEnabled.Value;
		if (enabled == _patchesActive)
			return;

		if (!enabled)
		{
			RemovePatches();
			_patchesActive = false;
			_logger?.LogInfo("Health-lock Harmony patches removed.");
			return;
		}

		ApplyPatches();
		_patchesActive = PatchedMethods.Count > 0;
	}

	private static void ApplyPatches()
	{
		if (_harmony == null || _logger == null || PatchedMethods.Count > 0)
			return;

		try
		{
			var applyDamage = AccessTools.Method(
				typeof(ActiveHealthController),
				nameof(ActiveHealthController.ApplyDamage));
			var applyDamagePrefix = AccessTools.Method(
				typeof(HealthLockPatch),
				nameof(BlockApplyDamage));
			if (applyDamage == null || applyDamagePrefix == null)
			{
				_logger.LogError("Unable to resolve ActiveHealthController.ApplyDamage for health lock.");
				return;
			}

			_harmony.Patch(applyDamage, prefix: new HarmonyMethod(applyDamagePrefix));
			PatchedMethods.Add(applyDamage);
			TryPatchSecondary(
				typeof(ActiveHealthController),
				"NetworkSyncApplyDamage",
				nameof(BlockNetworkDamage),
				_logger);
			TryPatchSecondary(
				typeof(ActiveHealthController),
				nameof(ActiveHealthController.ChangeHealth),
				nameof(BlockHealthReduction),
				_logger);
			_logger.LogInfo("True health-lock patches enabled.");
		}
		catch (Exception exception)
		{
			_logger.LogError("Health-lock patch failed: " + exception);
			RemovePatches();
		}
	}

	private static void RemovePatches()
	{
		if (_harmony == null)
			return;

		foreach (var method in PatchedMethods)
			_harmony.Unpatch(method, HarmonyPatchType.All, _harmony.Id);
		PatchedMethods.Clear();
	}

	private static void TryPatchSecondary(
		Type targetType,
		string targetMethod,
		string prefixMethod,
		ManualLogSource logger)
	{
		try
		{
			var target = AccessTools.Method(targetType, targetMethod);
			var prefix = AccessTools.Method(typeof(HealthLockPatch), prefixMethod);
			if (target == null || prefix == null)
			{
				logger.LogWarning($"Health-lock fallback not found: {targetType.Name}.{targetMethod}");
				return;
			}

			_harmony?.Patch(target, prefix: new HarmonyMethod(prefix));
			PatchedMethods.Add(target);
			logger.LogInfo($"Health-lock fallback enabled: {targetType.Name}.{targetMethod}");
		}
		catch (Exception exception)
		{
			logger.LogWarning($"Health-lock fallback failed: {targetType.Name}.{targetMethod}: {exception.Message}");
		}
	}

	private static bool BlockApplyDamage(
		ActiveHealthController __instance,
		EBodyPart bodyPart,
		ref float damage,
		ref float __result)
	{
		// LimitDamage returns true when the original method should run.
		if (LimitDamage(__instance, bodyPart, ref damage))
			return true;

		__result = 0f;
		return false;
	}

	private static bool BlockNetworkDamage(
		ActiveHealthController __instance,
		EBodyPart bodyPart,
		ref float damage)
	{
		return LimitDamage(__instance, bodyPart, ref damage);
	}

	private static bool BlockHealthReduction(
		ActiveHealthController __instance,
		EBodyPart bodyPart,
		ref float value)
	{
		if (value >= 0f)
			return true;

		var reduction = -value;
		if (!LimitDamage(__instance, bodyPart, ref reduction))
		{
			value = 0f;
			return true;
		}

		value = -reduction;
		return true;
	}

	private static bool LimitDamage(
		ActiveHealthController healthController,
		EBodyPart bodyPart,
		ref float damage)
	{
		if (damage <= 0f ||
			!TrainerPlugin.Settings.HealthEnabled.Value)
			return true;

		try
		{
			var player = healthController.Player;
			var localPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
			var localHealthController = localPlayer?.ActiveHealthController;
			if (player == null ||
				localPlayer == null ||
				localHealthController == null ||
				player.IsAI ||
				!player.IsYourPlayer ||
				healthController.Pointer != localHealthController.Pointer ||
				player.Pointer != localPlayer.Pointer)
				return true;

			if (!string.Equals(
				TrainerPlugin.Settings.HealthLockMode.Value,
				"Threshold",
				StringComparison.OrdinalIgnoreCase))
			{
				damage = 0f;
				return false;
			}

			var current = healthController.GetBodyPartHealth(bodyPart).Current;
			var minimum = GetMinimumHealth(bodyPart);
			if (current <= minimum)
			{
				damage = 0f;
				return false;
			}

			var allowedDamage = Math.Max(0f, current - minimum);
			if (damage >= allowedDamage)
				damage = allowedDamage;

			if (damage <= 0.0001f)
			{
				damage = 0f;
				return false;
			}

			return true;
		}
		catch
		{
			return true;
		}
	}

	internal static float GetMinimumHealth(EBodyPart bodyPart)
	{
		var settings = TrainerPlugin.Settings;
		return bodyPart switch
		{
			EBodyPart.Head => settings.HeadMinimumHealth.Value,
			EBodyPart.Chest => settings.ChestMinimumHealth.Value,
			EBodyPart.Stomach => settings.StomachMinimumHealth.Value,
			EBodyPart.LeftArm => settings.LeftArmMinimumHealth.Value,
			EBodyPart.RightArm => settings.RightArmMinimumHealth.Value,
			EBodyPart.LeftLeg => settings.LeftLegMinimumHealth.Value,
			EBodyPart.RightLeg => settings.RightLegMinimumHealth.Value,
			_ => 0f
		};
	}
}
