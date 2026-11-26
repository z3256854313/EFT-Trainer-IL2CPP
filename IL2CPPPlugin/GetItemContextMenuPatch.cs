using BepInEx.Logging;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;

namespace TrainerIL2CPP;

internal static class GetItemContextMenuPatch
{
	public static void Apply(ManualLogSource logger)
	{
		try
		{
			var harmony = new Harmony("com.local.efttrainer.il2cpp.getitemcontextmenu");
			harmony.PatchAll(typeof(GetItemContextMenuPatch));
			logger.LogInfo(
				"Get Item native menu patches: " +
				string.Join(
					", ",
					harmony.GetPatchedMethods().Select(method => method.FullDescription())));
		}
		catch (Exception exception)
		{
			logger.LogError("Get Item context-menu patch failed: " + exception);
		}
	}

	[HarmonyPatch(
		typeof(ItemUiContext),
		nameof(ItemUiContext.GetItemContextInteractions))]
	[HarmonyPostfix]
	private static void GetItemContextInteractionsPostfix(
		ItemUiContext __instance,
		ItemContext itemContext,
		ContextInteractions<EItemInfoButton> __result)
	{
		var item = itemContext?.Item;
		if (item == null ||
			itemContext == null ||
			__instance == null ||
			__result == null ||
			!TrainerPlugin.Settings.GetItemMenuEnabled.Value ||
			!TrainerBehaviour.IsGetItemAvailable(item))
		{
			return;
		}

		if (__result._dynamicInteractions == null)
		{
			__result._dynamicInteractions =
				new Il2CppSystem.Collections.Generic.Dictionary<
					string,
					DynamicContextInteraction>();
		}

		var interactionId = "trainer_get_item_" + item.Id;
		__result._dynamicInteractions[interactionId] =
			new DynamicContextInteraction(
				interactionId,
				"Get Item",
				(Il2CppSystem.Action)(() =>
					TrainerBehaviour.Instance?.HandleDynamicGetItemClicked(
						__instance,
						item)),
				null,
				null,
				null!);

		TrainerBehaviour.Instance?.RegisterDynamicGetItemMenu(__instance, item);
	}

	[HarmonyPatch(
		typeof(InteractionButtonsContainer),
		"CreateDynamicContextButton")]
	[HarmonyPostfix]
	private static void CreateDynamicContextButtonPostfix(
		InteractionButtonsContainer __instance,
		DynamicContextInteraction interaction)
	{
		if (__instance == null ||
			interaction == null ||
			!interaction.Id.StartsWith(
				"trainer_get_item_",
				StringComparison.Ordinal))
		{
			return;
		}

		var container = __instance._buttonsContainer;
		if (container == null || container.childCount == 0)
			return;

		var button = container
			.GetChild(container.childCount - 1)
			.GetComponent<SimpleContextMenuButton>();
		TrainerBehaviour.Instance?.RegisterDynamicGetItemButton(button);
	}
}
