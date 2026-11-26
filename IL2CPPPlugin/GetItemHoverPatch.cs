using BepInEx.Logging;
using EFT.UI;
using HarmonyLib;

namespace TrainerIL2CPP;

internal static class GetItemHoverPatch
{
	public static void Apply(ManualLogSource logger)
	{
		try
		{
			new Harmony("com.local.efttrainer.il2cpp.getitemhover").PatchAll(typeof(GetItemHoverPatch));
		}
		catch (Exception exception)
		{
			logger.LogError("Get Item hover patch failed: " + exception);
		}
	}

	[HarmonyPatch(typeof(SimpleContextMenuButton), nameof(SimpleContextMenuButton.OnPointerEnter))]
	[HarmonyPostfix]
	private static void OnPointerEnterPostfix(SimpleContextMenuButton __instance)
	{
		if (__instance == null)
			return;

		TrainerBehaviour.Instance?.HandleGetItemButtonPointerEnter(__instance);
	}
}
