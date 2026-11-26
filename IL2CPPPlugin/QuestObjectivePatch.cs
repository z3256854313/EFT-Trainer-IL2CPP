using BepInEx.Logging;
using EFT;
using EFT.Quests;
using EFT.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TrainerIL2CPP;

internal static class QuestObjectivePatch
{
	private static Harmony? _harmony;

	public static void Apply(ManualLogSource logger)
	{
		try
		{
			_harmony = new Harmony(TrainerPlugin.PluginGuid + ".quests");
			var target = AccessTools.Method(typeof(QuestObjectiveView), nameof(QuestObjectiveView.Show));
			var postfix = AccessTools.Method(typeof(QuestObjectivePatch), nameof(Postfix));
			if (target == null || postfix == null)
			{
				logger.LogError("Unable to resolve QuestObjectiveView.Show for quest skipping.");
				return;
			}

			_harmony.Patch(target, postfix: new HarmonyMethod(postfix));
			logger.LogInfo("Quest objective skip patch enabled.");
		}
		catch (Exception exception)
		{
			logger.LogError("Quest skip patch failed: " + exception);
		}
	}

	private static void Postfix(
		QuestController questController,
		Condition condition,
		Quest quest,
		QuestObjectiveView __instance)
	{
		var handoverButton = __instance._handoverButton;
		if (!TrainerPlugin.Settings.QuestSkipEnabled.Value || handoverButton == null)
			return;

		if (__instance.transform.parent == null)
			return;

		var existingSkipButton = handoverButton.transform.parent.Find("EFT_Trainer_SkipObjective");
		if (quest.IsConditionDone(condition))
		{
			if (existingSkipButton != null)
				UnityEngine.Object.Destroy(existingSkipButton.gameObject);
			return;
		}

		if (existingSkipButton == null)
		{
			var skipButton = UnityEngine.Object.Instantiate(
				handoverButton,
				handoverButton.transform.parent);
			skipButton.SetRawText("跳过", 22);
			skipButton.gameObject.name = "EFT_Trainer_SkipObjective";
			skipButton.gameObject.SetActive(true);
			if (skipButton.gameObject.GetComponent<LayoutElement>() is { } layout)
				layout.minWidth = 100f;

			skipButton.OnClick.RemoveAllListeners();
			System.Action action = () => SkipObjective(questController, quest, condition, skipButton);
			skipButton.OnClick.AddListener(action);
		}
	}

	private static void SkipObjective(
		QuestController questController,
		Quest quest,
		Condition condition,
		DefaultUIButton button)
	{
		try
		{
			if (quest.ProgressCheckers.TryGetValue(condition, out var progressChecker))
				progressChecker.SetCurrentValueGetter(_ => condition.value);

			var manager = questController.ConditionsConnectorsManager;
			if (manager == null)
			{
				TrainerPlugin.Logger.LogError("Unable to locate the quest conditions connectors manager.");
				return;
			}

			TrainerPlugin.Logger.LogInfo($"Skipping quest objective {condition.id} ({condition.value}).");
			manager.SetConditionCurrentValue(
				questController.Profile,
				quest,
				condition,
				condition.value,
				true);
			button.gameObject.SetActive(false);
			TrainerPlugin.Logger.LogInfo($"Quest objective {condition.id} completion submitted.");
		}
		catch (Exception exception)
		{
			TrainerPlugin.Logger.LogError("Unable to skip quest objective: " + exception);
		}
	}
}
