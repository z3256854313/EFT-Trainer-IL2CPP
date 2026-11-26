using Comfort.Common;
using BepInEx.Configuration;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.MovingPlatforms;
using EFT.Quests;
using EFT.Weather;
using UnityEngine;

namespace TrainerIL2CPP;

internal sealed class TrainerBehaviour : MonoBehaviour
{
	internal static TrainerBehaviour? Instance { get; private set; }

	private static readonly string[] TargetParts = ["Head", "Chest", "Stomach", "Arms", "Legs"];
	private static readonly string[] TargetPartLabels = ["头", "胸", "腹部", "胳膊", "腿"];
	private static readonly string[] HealthLockModeLabels = ["完全锁血", "阈值锁血"];
	private static readonly EBodyPart[] ThresholdBodyParts =
	[
		EBodyPart.Head,
		EBodyPart.Chest,
		EBodyPart.Stomach,
		EBodyPart.LeftArm,
		EBodyPart.RightArm,
		EBodyPart.LeftLeg,
		EBodyPart.RightLeg
	];
	private static readonly string[] ThresholdBodyPartLabels =
	[
		"头部",
		"胸部",
		"腹部",
		"左臂",
		"右臂",
		"左腿",
		"右腿"
	];
	private readonly float[] _thresholdBodyPartMaximums = [100f, 100f, 100f, 100f, 100f, 100f, 100f];
	private static readonly int[] GetItemQuantities = [1, 3, 5, 10, 100, 10000, 50000, 1000000];
	private const string CommandoDialogueId = "6723fd51c5924c57ce0ca01e";
	private const float LootSpatialCellSize = 30f;
	private const float NearbyLootRadius = 30f;
	private const float MapBoundsRetryInterval = 1f;
	private const float MapTeleportRayDistance = 10000f;
	private const float MapTeleportMarkerSpeed = 60f;

	private static readonly string[] PanelPages =
	[
		"主控",
		"快捷键",
		"锁定",
		"玩家 ESP",
		"战利品 / 任务",
		"生存 / 视觉",
		"世界 / 地图"
	];

	private Rect _window = new(24, 40, 920, 760);
	private readonly Vector2[] _pageScrolls = new Vector2[PanelPages.Length];
	private GUIStyle? _panelWindowStyle;
	private Texture2D? _panelBackgroundTexture;
	private int _panelPage;
	private bool _visible;
	private string _lastError = string.Empty;
	private float _lastErrorTime;
	private float _nextSurvivalUpdate;
	private float _nextDoorUnlockNotice;
	private Vector3? _savedPosition;
	private Quaternion _savedRotation;
	private float _originalFallDamageMultiplier;
	private bool _fallDamageCaptured;
	private bool _nightVisionApplied;
	private static Camera? _nativeOpticCamera;
	private static bool _scopeProjectionActive;
	private static float _scopeProjectionScale = 1f;
	private static Vector2 _scopeProjectionOffset;
	private readonly List<LootMarker> _lootMarkers = [];
	private readonly Dictionary<string, LootMarker> _lootMarkersByKey = [];
	private float _nextLootScan;
	private Player? _lootCachePlayer;
	private int _cachedLootItemCount = -1;
	private bool _lootMarkersBuilt;
	private string _lootMarkerMode = string.Empty;
	private readonly Dictionary<Vector2Int, List<EFT.Interactive.LootItem>> _lootSpatialBuckets = [];
	private readonly Dictionary<Vector2Int, List<CachedLootContainer>> _containerSpatialBuckets = [];
	private readonly HashSet<string> _cachedContainerIds = [];
	private readonly List<LootMarker> _questMarkers = [];
	private readonly List<LootMarker> _questItemMarkers = [];
	private readonly HashSet<string> _questItemMarkerKeys = [];
	private bool _questItemMarkersDirty = true;
	private string _questItemMarkerMode = string.Empty;
	private readonly List<string> _activeStoryQuestNames = [];
	private readonly List<EFT.Interactive.ExperienceTrigger> _cachedExperienceTriggers = [];
	private readonly List<EFT.Interactive.PlaceItemTrigger> _cachedPlaceItemTriggers = [];
	private readonly Dictionary<string, string> _questNameCache = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _activeQuestZones = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _regularQuestZoneIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _storyQuestZoneIds = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<Quest> _watchedQuests = [];
	private System.Action<Quest>? _questConditionChangedHandler;
	private System.Action<Quest, bool>? _questStatusChangedHandler;
	private float _nextQuestRecoveryRefresh;
	private bool _questMarkersDirty = true;
	private string _questMarkerMode = string.Empty;
	private Player? _questTriggerCachePlayer;
	private Player? _questSubscriptionPlayer;
	private readonly HashSet<string> _activeQuestItems = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _regularQuestItemTemplates = new(StringComparer.OrdinalIgnoreCase);
	private readonly HashSet<string> _storyQuestItemTemplates = new(StringComparer.OrdinalIgnoreCase);
	private readonly Dictionary<string, string> _activeQuestItemLabels = new(StringComparer.OrdinalIgnoreCase);
	private float _nextQuestItemRefresh;
	private float _nextQuestItemMarkerScan;
	private bool _consoleCommandsRegistered;
	private GameObject? _mapCameraObject;
	private Camera? _mapCamera;
	private GameWorld? _mapBoundsWorld;
	private Bounds _mapWorldBounds;
	private bool _mapBoundsReady;
	private float _nextMapBoundsAttempt;
	private GameWorld? _mapBoundsDiagnosticWorld;
	private string _mapBoundsSource = "未取得";
	private Vector3? _mapTeleportOrigin;
	private Quaternion _mapTeleportOriginRotation;
	private Vector3? _mapTeleportMarker;
	private int _mapTeleportCycleIndex = -1;
	private Player? _mapTeleportCyclePlayer;
	private bool _mapCursorOverrideActive;
	private CursorLockMode _mapCursorOriginalLockState;
	private bool _mapCursorOriginalVisible;
	private readonly Dictionary<Renderer, Color> _originalRendererColors = [];
	private WeaponTemplate? _malfunctionTemplate;
	private bool _malfunctionFeed;
	private bool _malfunctionJam;
	private bool _malfunctionMisfire;
	private bool _malfunctionOverheat;
	private bool _malfunctionSlide;
	private readonly List<EFT.Interactive.ExfiltrationPoint> _exfiltrationPoints = [];
	private readonly HashSet<EFT.Interactive.ExfiltrationPoint> _eligibleExfiltrationPoints = [];
	private readonly Dictionary<EFT.Interactive.ExfiltrationPoint, string> _exfiltrationLabels = [];
	private readonly List<EFT.Interactive.TransitPoint> _transitPoints = [];
	private readonly Dictionary<EFT.Interactive.TransitPoint, string> _transitPointLabels = [];
	private readonly Dictionary<EFT.Interactive.TransitPoint, bool> _transitPointAvailability = [];
	private GameWorld? _transitPointCacheWorld;
	private float _nextTransitPointRefresh;
	private Player? _exfiltrationCachePlayer;
	private float _nextExfiltrationRefresh;
	private string? _captureAction;
	private int _captureSlot;
	private EFT.UI.ItemUiContext? _dynamicGetItemUiContext;
	private Item? _dynamicGetItem;
	private EFT.UI.SimpleContextMenuButton? _dynamicGetItemButton;
	private EFT.UI.BaseEmptyContextInteractions? _getItemSubContext;
	private EFT.UI.InteractionButtonsContainer? _getItemSubContextButtons;
	private EFT.UI.SimpleContextMenuButton? _getItemSubContextParentButton;
	private string _getItemSubContextItemId = string.Empty;
	private int _lastGetItemPointerEnterFrame = -1;
	private bool _getItemSubMenuTemplateWarningLogged;
	private bool _getItemSubMenuReadyLogged;
	private bool _fleaUnlockApplied;

	private static TrainerSettings Settings => TrainerPlugin.Settings;

	private void Awake()
	{
		Instance = this;
	}

	private void Update()
	{
		try
		{
			SpatialAudioCrashGuard.TryApply(TrainerPlugin.Logger);
			HealthLockPatch.SyncEnabled();

			if (_captureAction == null)
			{
				if (IsAnyKeyDown(Settings.PanelKey.Value))
					_visible = !_visible;
				if (IsAnyKeyDown(Settings.MapKey.Value))
					Settings.MapEnabled.Value = !Settings.MapEnabled.Value;
				if (IsAnyKeyDown(Settings.EspKeys.Value))
					Settings.EspEnabled.Value = !Settings.EspEnabled.Value;
				if (IsAnyKeyDown(Settings.LootKeys.Value))
					Settings.LootEspEnabled.Value = !Settings.LootEspEnabled.Value;
				if (IsAnyKeyDown(Settings.QuestKeys.Value))
					Settings.QuestLocations.Value = !Settings.QuestLocations.Value;
				if (IsAnyKeyDown(Settings.HealthKeys.Value))
					Settings.HealthEnabled.Value = !Settings.HealthEnabled.Value;
				if (IsAnyKeyDown(Settings.NightVisionKeys.Value))
					Settings.NightVision.Value = !Settings.NightVision.Value;
				if (IsAnyKeyDown(Settings.UnlockNearbyDoorsKey.Value))
					UnlockNearbyDoors();
			}

			if (Settings.AimbotEnabled.Value && IsAnyKeyHeld(Settings.AimbotKey.Value))
				UpdateAimbot();

			UpdateMapTeleportInput();
			UpdateLocalUtilities();
			TryRegisterConsoleCommands();
		}
		catch (Exception exception)
		{
			ReportError(exception);
		}
	}

	private void TryRegisterConsoleCommands()
	{
		if (_consoleCommandsRegistered)
			return;

		var processor = EFT.UI.ConsoleScreen.Processor;
		if (processor == null)
			return;

		processor.RegisterCommand(
			"trainer",
			new Action<string>(ExecuteConsoleCommand),
			"EFT Trainer: status, combat, quest, stealth, save, panel");
		_consoleCommandsRegistered = true;
	}

	private static void ExecuteConsoleCommand(string arguments)
	{
		var command = (arguments ?? string.Empty).Trim().ToLowerInvariant();
		switch (command)
		{
			case "status":
				ShowConsoleMessage(
					$"Aimbot={Settings.AimbotEnabled.Value}, ESP={Settings.EspEnabled.Value}, Loot={Settings.LootEspEnabled.Value}, Survival={Settings.HealthEnabled.Value}");
				break;
			case "combat":
				ApplyCombatPreset();
				ShowConsoleMessage("Combat preset applied.");
				break;
			case "quest":
				ApplyQuestPreset();
				ShowConsoleMessage("Quest preset applied.");
				break;
			case "stealth":
				ApplyStealthPreset();
				ShowConsoleMessage("Stealth preset applied.");
				break;
			case "save":
				Settings.Save();
				ShowConsoleMessage("Trainer settings saved.");
				break;
			case "panel":
				var behaviour = FindObjectOfType<TrainerBehaviour>();
				if (behaviour != null)
					behaviour._visible = !behaviour._visible;
				break;
			default:
				ShowConsoleMessage("Commands: trainer status|combat|quest|stealth|save|panel");
				break;
		}
	}

	private static void ShowConsoleMessage(string message)
	{
		EFT.UI.ConsoleScreen.Log("[Trainer] " + message);
	}

	private void OnGUI()
	{
		try
		{
			if (_visible)
				CaptureKeyboardInput();

			if (Settings.EspEnabled.Value && !Settings.MapEnabled.Value)
				DrawEsp();

			if (Settings.MapEnabled.Value)
				DrawMapOverlay();

			DrawConvenienceUi();

			if (_visible)
			{
				var maximumWidth = Mathf.Max(560f, Mathf.Min(920f, Screen.width - 40f));
				var maximumHeight = Mathf.Max(500f, Mathf.Min(820f, Screen.height - 40f));
				_window.width = Mathf.Clamp(_window.width, Mathf.Min(760f, maximumWidth), maximumWidth);
				_window.height = Mathf.Clamp(_window.height, Mathf.Min(580f, maximumHeight), maximumHeight);
				_window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, Screen.width - _window.width));
				_window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, Screen.height - _window.height));
				EnsurePanelWindowStyle();
				Action<int> drawPanel = DrawPanel;
				_window = GUI.Window(731921, _window, drawPanel, "EFT Trainer — 辅助面板", _panelWindowStyle);
			}
		}
		catch (Exception exception)
		{
			ReportError(exception);
		}
	}

	private void EnsurePanelWindowStyle()
	{
		if (_panelWindowStyle != null)
			return;

		_panelBackgroundTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
		{
			hideFlags = HideFlags.DontSave
		};
		_panelBackgroundTexture.SetPixel(0, 0, new Color(0.035f, 0.045f, 0.06f, 1f));
		_panelBackgroundTexture.Apply();

		_panelWindowStyle = new GUIStyle(GUI.skin.window);
		_panelWindowStyle.normal.background = _panelBackgroundTexture;
		_panelWindowStyle.hover.background = _panelBackgroundTexture;
		_panelWindowStyle.active.background = _panelBackgroundTexture;
		_panelWindowStyle.focused.background = _panelBackgroundTexture;
		_panelWindowStyle.onNormal.background = _panelBackgroundTexture;
		_panelWindowStyle.onHover.background = _panelBackgroundTexture;
		_panelWindowStyle.onActive.background = _panelBackgroundTexture;
		_panelWindowStyle.onFocused.background = _panelBackgroundTexture;
	}

	private void OnDestroy()
	{
		if (Instance == this)
			Instance = null;
		_dynamicGetItemUiContext = null;
		_dynamicGetItem = null;
		_dynamicGetItemButton = null;
		RestoreMapCursor();
		if (_panelBackgroundTexture != null)
			Destroy(_panelBackgroundTexture);
	}

	private void DrawPanel(int windowId)
	{
		GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

		GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(176f), GUILayout.ExpandHeight(true));
		GUILayout.Label("EFT TRAINER", GUILayout.Height(34f));
		GUILayout.Label("功能导航", GUI.skin.box);
		GUILayout.Space(4f);
		var previousBackground = GUI.backgroundColor;
		for (var page = 0; page < PanelPages.Length; page++)
		{
			GUI.backgroundColor = page == _panelPage
				? new Color(0.24f, 0.67f, 0.45f, 1f)
				: previousBackground;
			if (GUILayout.Button(PanelPages[page], GUILayout.Height(36f)))
				_panelPage = page;
		}
		GUI.backgroundColor = previousBackground;

		GUILayout.FlexibleSpace();
		if (GUILayout.Button("保存设置", GUILayout.Height(34f)))
		{
			Settings.Save();
			_lastError = "设置已保存。";
			_lastErrorTime = Time.time;
		}
		if (GUILayout.Button("隐藏面板", GUILayout.Height(34f)))
			_visible = false;
		GUILayout.Label($"v{TrainerPlugin.PluginVersion}");
		GUILayout.EndVertical();

		GUILayout.BeginVertical(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
		GUILayout.Label(PanelPages[_panelPage], GUI.skin.box, GUILayout.Height(30f));
		_pageScrolls[_panelPage] = GUILayout.BeginScrollView(
			_pageScrolls[_panelPage],
			GUILayout.ExpandHeight(true));

		switch (_panelPage)
		{
			case 0:
				DrawGeneralPage();
				break;
			case 1:
				DrawHotkeysPage();
				break;
			case 2:
				DrawAimbotPage();
				break;
			case 3:
				DrawEspPage();
				break;
			case 4:
				DrawLootPage();
				break;
			case 5:
				DrawSurvivalPage();
				break;
			case 6:
				DrawWorldPage();
				break;
		}

		GUILayout.EndScrollView();

		if (!string.IsNullOrEmpty(_lastError) && Time.time - _lastErrorTime < 4f)
			GUILayout.Label(_lastError);
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();

		GUI.DragWindow(new Rect(0, 0, _window.width, 24));
	}

	private void DrawConvenienceUi()
	{
		if (Event.current.type != EventType.Repaint && Event.current.type != EventType.MouseDown)
			return;

		if (Settings.UnlockFleaMarket.Value && !_fleaUnlockApplied)
			TryUnlockFleaMarket();

	}

	internal void RegisterDynamicGetItemMenu(
		EFT.UI.ItemUiContext itemUiContext,
		Item item)
	{
		_dynamicGetItemUiContext = itemUiContext;
		_dynamicGetItem = item;
		_dynamicGetItemButton = null;
	}

	internal void RegisterDynamicGetItemButton(
		EFT.UI.SimpleContextMenuButton button)
	{
		if (button == null)
			return;

		_dynamicGetItemButton = button;
		if (button._subMenuArrow != null)
			button._subMenuArrow.gameObject.SetActive(true);
	}

	internal void HandleDynamicGetItemClicked(
		EFT.UI.ItemUiContext itemUiContext,
		Item item)
	{
		if (itemUiContext == null || !IsGetItemAvailable(item))
		{
			HideGetItemSubMenu();
			return;
		}

		var buttons = itemUiContext.ContextMenu?._interactionButtonsContainer;
		if (buttons == null || _dynamicGetItemButton == null)
			return;

		ShowGetItemSubMenu(
			buttons,
			_dynamicGetItemButton,
			itemUiContext,
			item);
	}

	internal void HandleGetItemButtonPointerEnter(EFT.UI.SimpleContextMenuButton button)
	{
		var item = _dynamicGetItem;
		if (!Settings.GetItemMenuEnabled.Value ||
			button == null ||
			button != _dynamicGetItemButton ||
			_dynamicGetItemUiContext == null ||
			item == null ||
			!IsGetItemAvailable(item) ||
			_lastGetItemPointerEnterFrame == Time.frameCount)
		{
			return;
		}

		_lastGetItemPointerEnterFrame = Time.frameCount;
		var buttons =
			_dynamicGetItemUiContext.ContextMenu?._interactionButtonsContainer;
		if (buttons == null)
			return;

		if (button._subMenuArrow != null)
			button._subMenuArrow.gameObject.SetActive(true);
		ShowGetItemSubMenu(
			buttons,
			button,
			_dynamicGetItemUiContext,
			item);
	}

	private void ShowGetItemSubMenu(
		EFT.UI.InteractionButtonsContainer buttons,
		EFT.UI.SimpleContextMenuButton? parentButton,
		EFT.UI.ItemUiContext itemUiContext,
		Item item)
	{
		if (!IsGetItemAvailable(item))
		{
			HideGetItemSubMenu();
			return;
		}

		_lastGetItemPointerEnterFrame = Time.frameCount;
		if (_getItemSubContext != null &&
			_getItemSubContextButtons == buttons &&
			_getItemSubContextParentButton == parentButton &&
			string.Equals(_getItemSubContextItemId, item.Id, StringComparison.OrdinalIgnoreCase) &&
			buttons._subMenu != null &&
			buttons._subMenu.gameObject.activeInHierarchy)
		{
			return;
		}

		HideGetItemSubMenu();
		if (buttons == null || parentButton == null)
		{
			if (!_getItemSubMenuTemplateWarningLogged)
			{
				_getItemSubMenuTemplateWarningLogged = true;
				TrainerPlugin.Logger.LogWarning(
					"Get Item submenu cannot be created: menu container or parent button is unavailable.");
			}
			return;
		}

		try
		{
			var subContext = new EFT.UI.BaseEmptyContextInteractions(itemUiContext);
			if (subContext._dynamicInteractions == null)
			{
				subContext._dynamicInteractions =
					new Il2CppSystem.Collections.Generic.Dictionary<
						string,
						EFT.UI.DynamicContextInteraction>();
			}
			else
			{
				subContext._dynamicInteractions.Clear();
			}

			foreach (var quantity in GetItemQuantities)
			{
				var interactionId = $"trainer_get_item_{item.Id}_{quantity}";
				subContext._dynamicInteractions[interactionId] =
					new EFT.UI.DynamicContextInteraction(
					interactionId,
					quantity.ToString(),
					(Il2CppSystem.Action)(() => SendItemCommandThroughCommando(item, quantity)),
					null,
					TryGetDynamicButtonType(),
					null!);
			}

			_getItemSubContext = subContext;
			_getItemSubContextButtons = buttons;
			_getItemSubContextParentButton = parentButton;
			_getItemSubContextItemId = item.Id;
			buttons._subMenuButton = parentButton;
			buttons.SetSubInteractions(subContext);
			if (!_getItemSubMenuReadyLogged)
			{
				_getItemSubMenuReadyLogged = true;
				var nativeSubMenu = buttons._subMenu;
				TrainerPlugin.Logger.LogInfo(
					$"Native Get Item submenu requested for item {item.Id}: " +
					string.Join(", ", GetItemQuantities) +
					$"; dynamic={subContext._dynamicInteractions.Count}; " +
					$"nativeMenu={(nativeSubMenu != null)}; " +
					$"active={(nativeSubMenu?.gameObject.activeInHierarchy == true)}; " +
					$"bound={(buttons._subMenuButton == parentButton)}");
			}
		}
		catch (Exception exception)
		{
			HideGetItemSubMenu();
			TrainerPlugin.Logger.LogWarning(
				"Unable to create the Get Item submenu: " + exception.Message);
		}
	}

	private static Il2CppSystem.Enum? TryGetDynamicButtonType()
	{
		try
		{
			var enumType = Il2CppSystem.Type.GetType(
				"EFT.InventoryLogic.EItemInfoButton, Assembly-CSharp",
				true);
			return (Il2CppSystem.Enum)Il2CppSystem.Enum.Parse(enumType, "Open");
		}
		catch
		{
			return null;
		}
	}

	private void HideGetItemSubMenu()
	{
		var owner = _getItemSubContextButtons;
		var parentButton = _getItemSubContextParentButton;
		_getItemSubContext = null;
		_getItemSubContextButtons = null;
		_getItemSubContextParentButton = null;
		_getItemSubContextItemId = string.Empty;
		if (owner == null)
			return;

		if (owner._subMenuButton == parentButton)
			owner.CloseSubMenu();
	}

	private void SendItemCommandThroughCommando(Item item, int quantity)
	{
		if (!IsGetItemAvailable(item) ||
			string.IsNullOrWhiteSpace(item.TemplateId) ||
			quantity <= 0)
			return;

		var command = $"spt give {item.TemplateId} {quantity}";
		try
		{
			var social = ResolveSocialNetwork();
			if (social == null)
				throw new InvalidOperationException("SocialNetwork is not available.");
			if (!social.TryGetDialog(CommandoDialogueId, out var dialogue) || dialogue == null)
				throw new InvalidOperationException("Commando dialogue is not loaded.");
			if (social.PlayerMember == null)
				throw new InvalidOperationException("Player chat member is not initialized.");

			var message = new ChatShared.DialogueChatMessage
			{
				_id = Guid.NewGuid().ToString("N")[..24],
				Type = ChatShared.EMessageType.UserMessage,
				Text = command,
				LocalDateTime = Il2CppSystem.DateTime.UtcNow,
				UtcDateTime = Il2CppSystem.DateTime.UtcNow,
				Member = social.PlayerMember
			};

			social.SendMessage(
				dialogue,
				ChatShared.EMessageType.UserMessage,
				message,
				(Il2CppSystem.Action)(() =>
					TrainerPlugin.Logger.LogInfo("Commando request completed: " + command)));
			_lastError = "已提交 Commando 获取请求：" + command;
			_lastErrorTime = Time.time;
			TrainerPlugin.Logger.LogInfo(
				$"Sent Commando command for item {item.Id} ({item.TemplateId}): {command}");
		}
		catch (Exception exception)
		{
			GUIUtility.systemCopyBuffer = command;
			_lastError = $"Commando 发送失败，已复制命令：{command}";
			_lastErrorTime = Time.time;
			TrainerPlugin.Logger.LogWarning(
				$"Unable to send Commando command '{command}': {exception.Message}");
		}
	}

	internal static bool IsGetItemAvailable(Item? item)
	{
		if (item == null ||
			item.QuestItem ||
			item.Template?.QuestItem == true)
		{
			return false;
		}

		var world = Singleton<GameWorld>.Instance;
		return world == null || world is HideoutGameWorld;
	}

	private static EFT.SocialNetwork? ResolveSocialNetwork()
	{
		try
		{
			if (Singleton<EFT.SocialNetwork>.Instance is { } singleton)
				return singleton;
		}
		catch
		{
			// Fall back to the chat screen's initialized social network.
		}

		var chatScreens = Resources.FindObjectsOfTypeAll<EFT.UI.Chat.ChatScreen>();
		foreach (var chatScreen in chatScreens)
		{
			if (chatScreen?._social != null)
				return chatScreen._social;
		}

		return FindObjectOfType<EFT.UI.Chat.ChatScreen>()?._social;
	}

	private static void DrawQuestSkipButton()
	{
		var questView = FindObjectOfType<EFT.UI.QuestView>();
		if (questView == null || !questView.gameObject.activeInHierarchy)
			return;

		var buttonRect = new Rect(Screen.width - 230f, Screen.height - 78f, 210f, 36f);
		if (!GUI.Button(buttonRect, "跳过此任务并领取奖励"))
			return;

		var player = Singleton<GameWorld>.Instance?.MainPlayer;
		var questId = questView.QuestId;
		var status = player?.Profile?.QuestsData?
			.FirstOrDefault(quest => quest?.Id == questId);
		var quest = status?.Quest;
		if (quest == null)
			return;

		quest.SetStatus(EQuestStatus.AvailableForFinish, true, true);
	}

	private void TryUnlockFleaMarket()
	{
		var player = Singleton<GameWorld>.Instance?.MainPlayer;
		var profile = player?.Profile;
		if (profile?.Info == null)
			return;

		var requiredExperience = ProfileInfo.GetExperience(15);
		if (profile.Info.Experience < requiredExperience)
			profile.Info.Experience = requiredExperience;

		if (profile.RagfairInfo != null)
		{
			profile.RagfairInfo.Rating = Math.Max(profile.RagfairInfo.Rating, 1f);
			profile.RagfairInfo.IsRatingGrowing = true;
		}
		_fleaUnlockApplied = true;
	}

	private void DrawGeneralPage()
	{
		GUILayout.Label("面板", GUI.skin.box);
		GUILayout.Label($"呼出按键：{FormatKeys(Settings.PanelKey.Value)}");
		if (GUILayout.Button("打开快捷键绑定页"))
			_panelPage = 1;
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("战斗预设"))
			ApplyCombatPreset();
		if (GUILayout.Button("任务预设"))
			ApplyQuestPreset();
		if (GUILayout.Button("潜行预设"))
			ApplyStealthPreset();
		GUILayout.EndHorizontal();

		GUILayout.Space(10);
		GUILayout.Label("当前状态", GUI.skin.box);
		GUILayout.Label($"锁定：{OnOff(Settings.AimbotEnabled.Value)}   玩家 ESP：{OnOff(Settings.EspEnabled.Value)}");
		GUILayout.Label($"战利品：{OnOff(Settings.LootEspEnabled.Value)}   生存：{OnOff(Settings.HealthEnabled.Value)}");
		GUILayout.Label($"地图：{OnOff(Settings.MapEnabled.Value)}   面板状态：已加载");
		GUILayout.Space(10);
		GUILayout.Label("便利功能", GUI.skin.box);
		Settings.GetItemMenuEnabled.Value = GUILayout.Toggle(
			Settings.GetItemMenuEnabled.Value,
			" 右键物品显示 Get Item + 数量二级菜单（仅非对局，排除剧情/任务道具）");
		Settings.QuestSkipEnabled.Value = GUILayout.Toggle(Settings.QuestSkipEnabled.Value, " 任务详情显示跳过按钮");
		Settings.UnlockFleaMarket.Value = GUILayout.Toggle(Settings.UnlockFleaMarket.Value, " 解锁跳蚤市场");
		if (GUILayout.Button($"解锁附近锁门（{FormatKeys(Settings.UnlockNearbyDoorsKey.Value)}）"))
			UnlockNearbyDoors();
		GUILayout.Space(8);
		GUILayout.Label("快捷按键", GUI.skin.box);
		DrawBindingRow("打开 / 隐藏面板", "panel", Settings.PanelKey);
		GUILayout.Space(10);
		GUILayout.Label("控制台命令", GUI.skin.box);
		GUILayout.Label("trainer status");
		GUILayout.Label("trainer combat / quest / stealth");
		GUILayout.Label("trainer save / panel");
	}

	private void DrawAimbotPage()
	{
		GUILayout.Label("锁定 / 瞄准", GUI.skin.box);
		Settings.AimbotEnabled.Value = GUILayout.Toggle(Settings.AimbotEnabled.Value, " 启用锁定");
		GUILayout.Label($"锁定部位：{TargetPartLabels[GetSelectedTargetPartIndex()]}");
		var selectedPart = GUILayout.Toolbar(GetSelectedTargetPartIndex(), TargetPartLabels);
		Settings.TargetPart.Value = TargetParts[Mathf.Clamp(selectedPart, 0, TargetParts.Length - 1)];
		Settings.PreferHeadChest.Value = GUILayout.Toggle(Settings.PreferHeadChest.Value, " 优先级：头 > 胸 > 当前部位");
		Settings.VisibleOnly.Value = GUILayout.Toggle(Settings.VisibleOnly.Value, " 只锁定可见部位");
		GUILayout.Label($"平滑度：{Settings.Smoothness.Value:0.00}");
		Settings.Smoothness.Value = GUILayout.HorizontalSlider(Settings.Smoothness.Value, 0.01f, 1f);
		GUILayout.Label($"移动目标提前量：{Settings.AimLeadMultiplier.Value:0.00}×");
		Settings.AimLeadMultiplier.Value = GUILayout.HorizontalSlider(
			Settings.AimLeadMultiplier.Value,
			0f,
			2.5f);
		GUILayout.Label($"最大锁定距离：{Settings.AimbotDistance.Value:0} m");
		Settings.AimbotDistance.Value = GUILayout.HorizontalSlider(Settings.AimbotDistance.Value, 10f, 500f);
		GUILayout.Label($"锁定视野半径：{Settings.FovRadius.Value:0} px");
		Settings.FovRadius.Value = GUILayout.HorizontalSlider(Settings.FovRadius.Value, 0f, 600f);
		GUILayout.Space(6);
		GUILayout.Label("快捷键", GUI.skin.box);
		DrawBindingRow("按住锁定", "aimbot", Settings.AimbotKey);
		GUILayout.Label("点击槽位后直接按键；最多绑定三个键。");
	}

	private void DrawEspPage()
	{
		GUILayout.Label("玩家 ESP", GUI.skin.box);
		Settings.EspEnabled.Value = GUILayout.Toggle(Settings.EspEnabled.Value, " 启用玩家 ESP");
		DrawBindingRow("开关玩家 ESP", "esp", Settings.EspKeys);
		Settings.ShowBoxes.Value = GUILayout.Toggle(Settings.ShowBoxes.Value, " 显示玩家方框");
		Settings.ShowSkeletons.Value = GUILayout.Toggle(Settings.ShowSkeletons.Value, " 显示玩家骨架");
		Settings.BodyTint.Value = GUILayout.Toggle(Settings.BodyTint.Value, " 玩家身体着色");
		Settings.XRay.Value = GUILayout.Toggle(Settings.XRay.Value, " 隔墙显示");
		Settings.ExfilEspEnabled.Value = GUILayout.Toggle(Settings.ExfilEspEnabled.Value, " 显示撤离点");
		Settings.ShowUnavailableExfiltrationPoints.Value = GUILayout.Toggle(
			Settings.ShowUnavailableExfiltrationPoints.Value,
			" 显示条件未满足 / 停用撤离点");
		Settings.ShowTransitPoints.Value = GUILayout.Toggle(
			Settings.ShowTransitPoints.Value,
			" 显示转移点");
		GUILayout.Label($"边框粗细：{Settings.BoxThickness.Value:0.0}");
		Settings.BoxThickness.Value = GUILayout.HorizontalSlider(Settings.BoxThickness.Value, 0.5f, 6f);
		GUILayout.Label($"方框内边距：{Settings.BoxPadding.Value:0.00}");
		Settings.BoxPadding.Value = GUILayout.HorizontalSlider(Settings.BoxPadding.Value, 0f, 0.4f);
		GUILayout.Label($"ESP 距离：{(Settings.EspDistance.Value <= 0 ? "不限" : $"{Settings.EspDistance.Value:0} m")}");
		Settings.EspDistance.Value = GUILayout.HorizontalSlider(Settings.EspDistance.Value, 0f, 1000f);
		GUILayout.Space(8);
		GUILayout.Label("阵营颜色说明", GUI.skin.box);
		GUILayout.Label("Boss：红色    邪教徒：紫色    Raider：青色");
		GUILayout.Label("BTR 司机：按 Boss 红色显示");
		GUILayout.Label("Rogue：灰色    Marksman：紫蓝色");
		GUILayout.Label("BEAR：蓝色    USEC：绿色    Scav：黄色");
	}

	private void DrawLootPage()
	{
		GUILayout.Label("战利品 ESP", GUI.skin.box);
		Settings.LootEspEnabled.Value = GUILayout.Toggle(Settings.LootEspEnabled.Value, " 启用战利品标记");
		DrawBindingRow("开关战利品 ESP", "loot", Settings.LootKeys);
		Settings.ShowLootPrices.Value = GUILayout.Toggle(Settings.ShowLootPrices.Value, " 显示估价");
		Settings.ShowCorpses.Value = GUILayout.Toggle(Settings.ShowCorpses.Value, " 显示尸体");
		Settings.ShowContainers.Value = GUILayout.Toggle(Settings.ShowContainers.Value, " 显示地图中的可搜索容器");
		GUILayout.Label("名称或模板 ID 筛选（逗号分隔）");
		Settings.LootFilter.Value = GUILayout.TextField(Settings.LootFilter.Value ?? string.Empty, 100);
		var lootDisplayDistance = Settings.LootDistance.Value;
		GUILayout.Label(
			lootDisplayDistance <= 0f
				? "战利品显示范围：不限"
				: $"战利品显示范围：{lootDisplayDistance:0} m");
		Settings.LootDistance.Value = GUILayout.HorizontalSlider(
			lootDisplayDistance,
			0f,
			1500f);
		GUILayout.Label("0 = 不限；只影响标记绘制，不影响全量缓存和 30 m 状态刷新。");
		GUILayout.Space(8);
		GUILayout.Label("任务地点 / 任务物品", GUI.skin.box);
		Settings.QuestLocations.Value = GUILayout.Toggle(Settings.QuestLocations.Value, " 启用任务地点和任务物品标记");
		DrawBindingRow("开关任务地点", "quest", Settings.QuestKeys);
		Settings.ShowRegularQuests.Value = GUILayout.Toggle(
			Settings.ShowRegularQuests.Value,
			" 普通任务：紫色位置 + 青色任务物品");
		Settings.ShowStoryQuests.Value = GUILayout.Toggle(
			Settings.ShowStoryQuests.Value,
			" 剧情任务：紫色位置 + 青色任务物品");
		Settings.ShowQuestItems.Value = GUILayout.Toggle(Settings.ShowQuestItems.Value, " 显示任务物品");
		Settings.ShowQuestItemsInContainers.Value = GUILayout.Toggle(
			Settings.ShowQuestItemsInContainers.Value,
			" 显示容器内的任务物品");
		Settings.QuestItemsActiveOnly.Value = GUILayout.Toggle(Settings.QuestItemsActiveOnly.Value, " 只显示当前任务需要的物品");
		GUILayout.Label(
			"当前剧情任务：" +
			(_activeStoryQuestNames.Count == 0
				? "无"
				: string.Join(" / ", _activeStoryQuestNames)));
		GUILayout.Label("剧情章节只按当前进行中的子任务生成地图标记。");
		GUILayout.Label($"任务标记距离：{Settings.QuestDistance.Value:0} m");
		Settings.QuestDistance.Value = GUILayout.HorizontalSlider(Settings.QuestDistance.Value, 10f, 1500f);
	}

	private void DrawSurvivalPage()
	{
		GUILayout.Label("生存", GUI.skin.box);
		Settings.HealthEnabled.Value = GUILayout.Toggle(Settings.HealthEnabled.Value, " 启用锁血");
		DrawBindingRow("开关锁血", "health", Settings.HealthKeys);
		var thresholdMode = string.Equals(
			Settings.HealthLockMode.Value,
			"Threshold",
			StringComparison.OrdinalIgnoreCase);
		var lockMode = GUILayout.Toolbar(
			thresholdMode ? 1 : 0,
			HealthLockModeLabels);
		Settings.HealthLockMode.Value = lockMode == 1 ? "Threshold" : "Full";
		thresholdMode = lockMode == 1;

		if (thresholdMode)
		{
			GUILayout.Label("达到阈值前，受击、流血和持续伤害会正常扣血；达到阈值后停止下降。");
			for (var index = 0; index < ThresholdBodyParts.Length; index++)
			{
				var bodyPart = ThresholdBodyParts[index];
				var maximum = _thresholdBodyPartMaximums[index];
				var currentValue = GetConfiguredMinimumHealth(bodyPart);
				maximum = Mathf.Max(1f, maximum);
				currentValue = Mathf.Clamp(currentValue, 0f, maximum);
				GUILayout.Label(
					$"{ThresholdBodyPartLabels[index]}最低血量：{currentValue:0} / {maximum:0}");
				var newValue = GUILayout.HorizontalSlider(
					currentValue,
					0f,
					maximum);
				SetConfiguredMinimumHealth(bodyPart, Mathf.Round(newValue));
			}
		}
		else
		{
			GUILayout.Label("完全锁血：保留当前生命值，所有来源的生命值下降都会被拦截。");
		}

		Settings.FoodWater.Value = GUILayout.Toggle(Settings.FoodWater.Value, " 水分和能量保持满值");
		Settings.RemoveNegativeEffects.Value = GUILayout.Toggle(
			Settings.RemoveNegativeEffects.Value,
			" 启用负面状态分类清理");
		using (new GUIEnabledScope(Settings.RemoveNegativeEffects.Value))
		{
			Settings.RemoveEnergyEffects.Value = GUILayout.Toggle(
				Settings.RemoveEnergyEffects.Value,
				" 能量类：脱水、力竭、慢性疲劳、低血能量");
			Settings.RemoveBuffEffects.Value = GUILayout.Toggle(
				Settings.RemoveBuffEffects.Value,
				" 负面 Buff：闪光、震颤、疼痛、方向感等，保留正面药物 Buff");
			Settings.RemoveLimbDamageEffects.Value = GUILayout.Toggle(
				Settings.RemoveLimbDamageEffects.Value,
				" 肢体伤害类：出血、骨折、创伤、冻伤、肌肉疼痛");
		}
		Settings.FallDamageImmunity.Value = GUILayout.Toggle(Settings.FallDamageImmunity.Value, " 防止坠落伤害");
		Settings.InfiniteStamina.Value = GUILayout.Toggle(Settings.InfiniteStamina.Value, " 无限耐力");
		Settings.NoMalfunctions.Value = GUILayout.Toggle(Settings.NoMalfunctions.Value, " 武器无故障");
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("保存位置"))
			SavePlayerPosition();
		using (new GUIEnabledScope(_savedPosition.HasValue))
		{
			if (GUILayout.Button("传送"))
				RestorePlayerPosition();
		}
		GUILayout.EndHorizontal();

		GUILayout.Space(8);
		GUILayout.Label("视觉", GUI.skin.box);
		Settings.NightVision.Value = GUILayout.Toggle(Settings.NightVision.Value, " 夜视");
		DrawBindingRow("开关夜视", "night", Settings.NightVisionKeys);
		Settings.NoFlash.Value = GUILayout.Toggle(Settings.NoFlash.Value, " 防闪光 / 防眼部灼伤");
		Settings.NoVisor.Value = GUILayout.Toggle(Settings.NoVisor.Value, " 去面罩遮挡");
	}

	private void DrawWorldPage()
	{
		GUILayout.Space(8);
		GUILayout.Label("全屏地图", GUI.skin.box);
		Settings.MapEnabled.Value = GUILayout.Toggle(Settings.MapEnabled.Value, " 显示俯视地图");
		DrawBindingRow("开关地图", "map", Settings.MapKey);
		GUILayout.Label("地图范围：自动显示完整地图");
		GUILayout.Label($"地图缩小倍率：{Settings.MapZoomOut.Value:0.00}×");
		Settings.MapZoomOut.Value = GUILayout.HorizontalSlider(
			Settings.MapZoomOut.Value,
			1f,
			2.5f);
		Settings.MapTeleportEnabled.Value = GUILayout.Toggle(
			Settings.MapTeleportEnabled.Value,
			" 启用键盘标记传送");
		DrawBindingRow("地图中心 / 下一个传送点", "mapmarker", Settings.MapTeleportMarkerKey);
		DrawBindingRow("传送到标记", "mapteleport", Settings.MapTeleportExecuteKey);
		GUILayout.Label("第一次按标记键放到地图中心；之后依次切换撤离点和转移点。");
		GUILayout.Label("方向键连续移动标记；执行键传送到标记。地图关闭后重新打开会重置传送点遍历。");
		using (new GUIEnabledScope(_mapTeleportOrigin.HasValue))
		{
			if (GUILayout.Button("返回上次地图传送前位置"))
				RestoreMapTeleportOrigin();
		}

		GUILayout.Space(8);
		GUILayout.Label("世界", GUI.skin.box);
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("正午"))
			SetWorldTime(12f);
		if (GUILayout.Button("午夜"))
			SetWorldTime(0f);
		GUILayout.EndHorizontal();
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("晴朗天气"))
			SetClearWeather();
		if (GUILayout.Button("召唤火车"))
			SummonTrain();
		if (GUILayout.Button("空投"))
			TriggerAirDrop();
		GUILayout.EndHorizontal();
	}

	private static float GetConfiguredMinimumHealth(EBodyPart bodyPart)
	{
		return bodyPart switch
		{
			EBodyPart.Head => Settings.HeadMinimumHealth.Value,
			EBodyPart.Chest => Settings.ChestMinimumHealth.Value,
			EBodyPart.Stomach => Settings.StomachMinimumHealth.Value,
			EBodyPart.LeftArm => Settings.LeftArmMinimumHealth.Value,
			EBodyPart.RightArm => Settings.RightArmMinimumHealth.Value,
			EBodyPart.LeftLeg => Settings.LeftLegMinimumHealth.Value,
			EBodyPart.RightLeg => Settings.RightLegMinimumHealth.Value,
			_ => 0f
		};
	}

	private static void SetConfiguredMinimumHealth(EBodyPart bodyPart, float value)
	{
		switch (bodyPart)
		{
			case EBodyPart.Head:
				Settings.HeadMinimumHealth.Value = value;
				break;
			case EBodyPart.Chest:
				Settings.ChestMinimumHealth.Value = value;
				break;
			case EBodyPart.Stomach:
				Settings.StomachMinimumHealth.Value = value;
				break;
			case EBodyPart.LeftArm:
				Settings.LeftArmMinimumHealth.Value = value;
				break;
			case EBodyPart.RightArm:
				Settings.RightArmMinimumHealth.Value = value;
				break;
			case EBodyPart.LeftLeg:
				Settings.LeftLegMinimumHealth.Value = value;
				break;
			case EBodyPart.RightLeg:
				Settings.RightLegMinimumHealth.Value = value;
				break;
		}
	}

	private void DrawHotkeysPage()
	{
		GUILayout.Label("快捷键绑定", GUI.skin.box);
		GUILayout.Label("点击一个空槽位，然后直接在键盘上按下需要的按键。");
		GUILayout.Label("每个功能最多三个键；任意一个键都会触发该功能。");
		GUILayout.Label("按 Esc 取消当前捕获。");
		if (_captureAction != null)
			GUILayout.Label($"正在等待按键：{GetActionLabel(_captureAction)} 槽位 {_captureSlot + 1}");

		GUILayout.Space(8);
		DrawBindingRow("打开 / 隐藏面板", "panel", Settings.PanelKey);
		DrawBindingRow("按住锁定", "aimbot", Settings.AimbotKey);
		DrawBindingRow("开关地图", "map", Settings.MapKey);
		DrawBindingRow("地图中心 / 下一个传送点", "mapmarker", Settings.MapTeleportMarkerKey);
		DrawBindingRow("执行地图传送", "mapteleport", Settings.MapTeleportExecuteKey);
		DrawBindingRow("开关玩家 ESP", "esp", Settings.EspKeys);
		DrawBindingRow("开关战利品 ESP", "loot", Settings.LootKeys);
		DrawBindingRow("开关任务地点", "quest", Settings.QuestKeys);
		DrawBindingRow("开关锁血", "health", Settings.HealthKeys);
		DrawBindingRow("开关夜视", "night", Settings.NightVisionKeys);
		DrawBindingRow("解锁附近锁门", "unlockdoors", Settings.UnlockNearbyDoorsKey);
	}

	private void DrawBindingRow(string label, string action, ConfigEntry<string> entry)
	{
		var keys = ReadKeys(entry.Value);
		GUILayout.BeginHorizontal();
		GUILayout.Label(label, GUILayout.Width(170f));
		for (var slot = 0; slot < keys.Length; slot++)
		{
			var capturing = _captureAction == action && _captureSlot == slot;
			var text = capturing ? "[按键…]" : string.IsNullOrWhiteSpace(keys[slot]) ? "空" : FormatKey(keys[slot]);
			if (GUILayout.Button(text, GUILayout.Width(92f)))
			{
				_captureAction = action;
				_captureSlot = slot;
			}
		}
		if (GUILayout.Button("清除", GUILayout.Width(64f)))
		{
			entry.Value = string.Empty;
			_captureAction = null;
			Settings.Save();
		}
		GUILayout.EndHorizontal();
	}

	private void CaptureKeyboardInput()
	{
		if (_captureAction == null || Event.current.type != EventType.KeyDown)
			return;

		var key = Event.current.keyCode;
		if (key == KeyCode.None)
			return;

		Event.current.Use();
		if (key == KeyCode.Escape)
		{
			_captureAction = null;
			return;
		}

		var entry = GetKeyEntry(_captureAction);
		if (entry == null)
		{
			_captureAction = null;
			return;
		}

		var keys = ReadKeys(entry.Value);
		for (var index = 0; index < keys.Length; index++)
		{
			if (index != _captureSlot &&
				string.Equals(keys[index], key.ToString(), StringComparison.OrdinalIgnoreCase))
				keys[index] = string.Empty;
		}

		keys[_captureSlot] = key.ToString();
		entry.Value = string.Join(",", keys).TrimEnd(',');
		_lastError = $"已绑定 {GetActionLabel(_captureAction)}：{FormatKey(key.ToString())}";
		_lastErrorTime = Time.time;
		_captureAction = null;
		Settings.Save();
	}

	private static ConfigEntry<string>? GetKeyEntry(string action)
	{
		return action switch
		{
			"panel" => Settings.PanelKey,
			"aimbot" => Settings.AimbotKey,
			"map" => Settings.MapKey,
			"mapmarker" => Settings.MapTeleportMarkerKey,
			"mapteleport" => Settings.MapTeleportExecuteKey,
			"esp" => Settings.EspKeys,
			"loot" => Settings.LootKeys,
			"quest" => Settings.QuestKeys,
			"health" => Settings.HealthKeys,
			"night" => Settings.NightVisionKeys,
			"unlockdoors" => Settings.UnlockNearbyDoorsKey,
			_ => null
		};
	}

	private static string GetActionLabel(string action)
	{
		return action switch
		{
			"panel" => "面板",
			"aimbot" => "锁定",
			"map" => "地图",
			"mapmarker" => "地图标记 / 传送点遍历",
			"mapteleport" => "执行地图传送",
			"esp" => "玩家 ESP",
			"loot" => "战利品 ESP",
			"quest" => "任务地点",
			"health" => "生命保护",
			"night" => "夜视",
			"unlockdoors" => "解锁附近锁门",
			_ => action
		};
	}

	private static string[] ReadKeys(string? value)
	{
		var parts = (value ?? string.Empty).Split(',', StringSplitOptions.None);
		var result = new[] { string.Empty, string.Empty, string.Empty };
		for (var index = 0; index < result.Length && index < parts.Length; index++)
			result[index] = parts[index].Trim();
		return result;
	}

	private static bool IsAnyKeyDown(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;

		return ReadKeys(value).Any(key =>
			!string.IsNullOrWhiteSpace(key) &&
			Input.GetKeyDown(ParseKey(key, KeyCode.None)));
	}

	private static bool IsAnyKeyHeld(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;

		return ReadKeys(value).Any(key =>
			!string.IsNullOrWhiteSpace(key) &&
			Input.GetKey(ParseKey(key, KeyCode.None)));
	}

	private static string FormatKeys(string? value)
	{
		var keys = ReadKeys(value)
			.Where(key => !string.IsNullOrWhiteSpace(key))
			.Select(FormatKey)
			.ToArray();
		return keys.Length == 0 ? "未绑定" : string.Join(" / ", keys);
	}

	private static string FormatKey(string value)
	{
		if (!Enum.TryParse<KeyCode>(value, ignoreCase: true, out var key))
			return value;

		return key switch
		{
			KeyCode.RightAlt => "右 Alt",
			KeyCode.LeftAlt => "左 Alt",
			KeyCode.RightShift => "右 Shift",
			KeyCode.LeftShift => "左 Shift",
			KeyCode.RightControl => "右 Ctrl",
			KeyCode.LeftControl => "左 Ctrl",
			KeyCode.Space => "空格",
			KeyCode.Escape => "Esc",
			KeyCode.Return => "Enter",
			KeyCode.Backspace => "退格",
			KeyCode.UpArrow => "方向键上",
			KeyCode.DownArrow => "方向键下",
			KeyCode.LeftArrow => "方向键左",
			KeyCode.RightArrow => "方向键右",
			KeyCode.LeftBracket => "[",
			KeyCode.RightBracket => "]",
			KeyCode.Mouse0 => "鼠标左键",
			KeyCode.Mouse1 => "鼠标右键",
			KeyCode.Mouse2 => "鼠标中键",
			KeyCode.KeypadPeriod => "小键盘 .",
			_ => key.ToString()
		};
	}

	private static string OnOff(bool value)
	{
		return value ? "开启" : "关闭";
	}

	private static void ApplyCombatPreset()
	{
		Settings.AimbotEnabled.Value = true;
		Settings.EspEnabled.Value = true;
		Settings.ShowBoxes.Value = true;
		Settings.XRay.Value = false;
		Settings.HealthEnabled.Value = true;
		Settings.InfiniteStamina.Value = true;
		Settings.NoMalfunctions.Value = true;
		Settings.LootEspEnabled.Value = false;
		Settings.QuestLocations.Value = false;
		Settings.Save();
	}

	private static void ApplyQuestPreset()
	{
		Settings.AimbotEnabled.Value = false;
		Settings.EspEnabled.Value = true;
		Settings.ShowBoxes.Value = true;
		Settings.XRay.Value = false;
		Settings.HealthEnabled.Value = true;
		Settings.InfiniteStamina.Value = true;
		Settings.NoMalfunctions.Value = true;
		Settings.LootEspEnabled.Value = true;
		Settings.ShowQuestItems.Value = true;
		Settings.QuestItemsActiveOnly.Value = true;
		Settings.ShowContainers.Value = true;
		Settings.QuestLocations.Value = true;
		Settings.ShowRegularQuests.Value = true;
		Settings.ShowStoryQuests.Value = true;
		Settings.Save();
	}

	private static void ApplyStealthPreset()
	{
		Settings.AimbotEnabled.Value = false;
		Settings.EspEnabled.Value = true;
		Settings.ShowBoxes.Value = false;
		Settings.ShowSkeletons.Value = true;
		Settings.XRay.Value = false;
		Settings.LootEspEnabled.Value = false;
		Settings.QuestLocations.Value = false;
		Settings.HealthEnabled.Value = true;
		Settings.InfiniteStamina.Value = true;
		Settings.NoMalfunctions.Value = true;
		Settings.Save();
	}

	private void UpdateLocalUtilities()
	{
		var world = Singleton<GameWorld>.Instance;
		var player = world?.MainPlayer;
		if (world == null || player == null)
			return;

		if (Settings.EspEnabled.Value || Settings.AimbotEnabled.Value)
			UpdateScopeProjection(Camera.main, player);
		else
			_scopeProjectionActive = false;

		if (Settings.InfiniteStamina.Value)
		{
			var physical = player.Physical;
			if (physical?.Stamina != null)
				physical.Stamina.Current = physical.Stamina.TotalCapacity.Value;
			if (physical?.HandsStamina != null)
				physical.HandsStamina.Current = physical.HandsStamina.TotalCapacity.Value;
		}

		if (Settings.HealthEnabled.Value && Time.time >= _nextSurvivalUpdate)
		{
			_nextSurvivalUpdate = Time.time + 1f;
			var health = player.ActiveHealthController;
			if (health != null)
			{
				EnforceHealthThresholds(health);

				if (Settings.FoodWater.Value)
				{
					health.ChangeEnergy(1000f);
					health.ChangeHydration(1000f);
				}

				if (Settings.RemoveNegativeEffects.Value)
					RemoveSelectedNegativeEffects(health);
			}
		}

		var physicalBase = player.Physical;
		if (physicalBase != null)
		{
			if (Settings.HealthEnabled.Value && Settings.FallDamageImmunity.Value)
			{
				if (!_fallDamageCaptured)
				{
					_originalFallDamageMultiplier = physicalBase.FallDamageMultiplier;
					_fallDamageCaptured = true;
				}
				physicalBase.FallDamageMultiplier = 0f;
			}
			else if (_fallDamageCaptured)
			{
				physicalBase.FallDamageMultiplier = _originalFallDamageMultiplier;
				_fallDamageCaptured = false;
			}
		}

		UpdateNoMalfunctions(player);

		RefreshActiveQuestItems(player);
		UpdateLootMarkers(world, player);
		UpdateQuestMarkers(player);
		UpdateQuestItemMarkers(world, player);
		UpdateVisionUtilities(Camera.main);
		UpdateMapCamera(world, player);
		UpdateBodyTint(world, player);
	}

	private void EnforceHealthThresholds(ActiveHealthController health)
	{
		if (!string.Equals(
			Settings.HealthLockMode.Value,
			"Threshold",
			StringComparison.OrdinalIgnoreCase))
		{
			return;
		}

		for (var index = 0; index < ThresholdBodyParts.Length; index++)
		{
			var bodyPart = ThresholdBodyParts[index];
			var bodyPartHealth = health.GetBodyPartHealth(bodyPart);
			_thresholdBodyPartMaximums[index] = bodyPartHealth.Maximum;
			var minimum = GetConfiguredMinimumHealth(bodyPart);
			var current = bodyPartHealth.Current;
			if (current + 0.01f >= minimum)
				continue;

			health.Heal(bodyPart, minimum - current);
		}
	}

	private static void RemoveSelectedNegativeEffects(ActiveHealthController health)
	{
		if (!Settings.RemoveEnergyEffects.Value &&
			!Settings.RemoveBuffEffects.Value &&
			!Settings.RemoveLimbDamageEffects.Value)
		{
			return;
		}

		var predicate =
			(Il2CppSystem.Func<ActiveHealthController.Effect, bool>)(
				effect => ShouldRemoveNegativeEffect(effect));
		foreach (EBodyPart bodyPart in Enum.GetValues(typeof(EBodyPart)))
			health.ResidueEffectsWhere(bodyPart, predicate);
	}

	private static bool ShouldRemoveNegativeEffect(ActiveHealthController.Effect effect)
	{
		if (effect == null)
			return false;

		if (Settings.RemoveLimbDamageEffects.Value &&
			(effect is IBleeding ||
				effect is IFracture ||
				effect is ActiveHealthController.Wound ||
				effect is ActiveHealthController.Frostbite ||
				effect is ActiveHealthController.MusclePain))
		{
			return true;
		}

		if (Settings.RemoveEnergyEffects.Value &&
			(effect is ActiveHealthController.Dehydration ||
				effect is ActiveHealthController.Exhaustion ||
				effect is ActiveHealthController.ChronicStaminaFatigue ||
				effect is ActiveHealthController.LowEdgeHealth))
		{
			return true;
		}

		return Settings.RemoveBuffEffects.Value &&
			(effect is ActiveHealthController.Flash ||
				effect is ActiveHealthController.Stun ||
				effect is ActiveHealthController.Disorientation ||
				effect is ActiveHealthController.Contusion ||
				effect is ActiveHealthController.Pain ||
				effect is ActiveHealthController.Tremor ||
				effect is ActiveHealthController.TunnelVision ||
				effect is ActiveHealthController.PanicEffect ||
				effect is ActiveHealthController.SandingScreen ||
				effect is ActiveHealthController.TearGas ||
				effect is ActiveHealthController.Intoxication ||
				effect is ActiveHealthController.LethalIntoxication ||
				effect is ActiveHealthController.RadExposure ||
				effect is ActiveHealthController.ZombieInfection);
	}

	private void UnlockNearbyDoors()
	{
		var player = Singleton<GameWorld>.Instance?.MainPlayer;
		if (player == null || Time.time < _nextDoorUnlockNotice)
			return;

		var unlocked = 0;
		var objects = LocationScene.GetAllObjects<EFT.Interactive.WorldInteractiveObject>();
		if (objects != null)
		{
			foreach (var worldObject in objects)
			{
				if (worldObject == null ||
					worldObject.DoorState != EFT.Interactive.EDoorState.Locked)
					continue;

				var offset =
					player.Transform.Original.position -
					worldObject.transform.position;
				if (offset.sqrMagnitude > 5f)
					continue;

				worldObject.DoorState = EFT.Interactive.EDoorState.Shut;
				unlocked++;
			}
		}

		_lastError = unlocked > 0
			? $"已解锁附近 {unlocked} 扇锁门。"
			: "附近约 2.24 米内没有锁门。";
		_lastErrorTime = Time.time;
		_nextDoorUnlockNotice = Time.time + 0.5f;
		TrainerPlugin.Logger.LogInfo(_lastError);
	}

	private void UpdateNoMalfunctions(Player player)
	{
		WeaponTemplate? currentTemplate = null;
		if (Settings.NoMalfunctions.Value &&
			player.HandsController is Player.FirearmController controller &&
			controller.Item?.Template is WeaponTemplate weaponTemplate)
			currentTemplate = weaponTemplate;

		if (_malfunctionTemplate == currentTemplate)
			return;

		if (_malfunctionTemplate != null)
		{
			_malfunctionTemplate.AllowFeed = _malfunctionFeed;
			_malfunctionTemplate.AllowJam = _malfunctionJam;
			_malfunctionTemplate.AllowMisfire = _malfunctionMisfire;
			_malfunctionTemplate.AllowOverheat = _malfunctionOverheat;
			_malfunctionTemplate.AllowSlide = _malfunctionSlide;
		}

		_malfunctionTemplate = currentTemplate;
		if (currentTemplate == null)
			return;

		_malfunctionFeed = currentTemplate.AllowFeed;
		_malfunctionJam = currentTemplate.AllowJam;
		_malfunctionMisfire = currentTemplate.AllowMisfire;
		_malfunctionOverheat = currentTemplate.AllowOverheat;
		_malfunctionSlide = currentTemplate.AllowSlide;
		currentTemplate.AllowFeed = false;
		currentTemplate.AllowJam = false;
		currentTemplate.AllowMisfire = false;
		currentTemplate.AllowOverheat = false;
		currentTemplate.AllowSlide = false;
	}

	private void UpdateBodyTint(GameWorld world, Player localPlayer)
	{
		if (!Settings.BodyTint.Value)
		{
			foreach (var pair in _originalRendererColors)
			{
				if (pair.Key != null && pair.Key.material != null)
					pair.Key.material.color = pair.Value;
			}
			_originalRendererColors.Clear();
			return;
		}

		foreach (var player in world.AllAlivePlayersList)
		{
			if (player == null || player.IsYourPlayer || player.HealthController?.IsAlive != true)
				continue;

			var color = GetPlayerEspColor(player);
			var skins = player.PlayerBody?.BodySkins;
			if (skins == null)
				continue;

			foreach (var skin in skins.Values)
			{
				if (skin == null)
					continue;

				foreach (var renderer in skin.GetRenderers())
				{
					if (renderer == null || renderer.material == null)
						continue;

					if (_originalRendererColors.TryAdd(renderer, renderer.material.color))
						renderer.material.color = color;
				}
			}
		}
	}

	private void UpdateMapCamera(GameWorld world, Player player)
	{
		UpdateMapCursorMode();
		if (!Settings.MapEnabled.Value)
		{
			if (_mapCamera != null)
				_mapCamera.enabled = false;
			return;
		}

		var mainCamera = Camera.main;
		if (mainCamera == null)
			return;

		if (_mapCameraObject == null || _mapCamera == null)
		{
			_mapCameraObject = new GameObject("EFT_Trainer_MapCamera");
			GameObject.DontDestroyOnLoad(_mapCameraObject);
			_mapCamera = _mapCameraObject.AddComponent<Camera>();
			_mapCamera.clearFlags = CameraClearFlags.SolidColor;
			_mapCamera.backgroundColor = new Color(0.015f, 0.02f, 0.025f, 1f);
			_mapCamera.orthographic = true;
			_mapCamera.allowHDR = false;
			_mapCamera.depth = -100f;
		}

		_mapCamera.enabled = true;
		_mapCamera.cullingMask = mainCamera.cullingMask;
		var mapSize = Settings.MapRange.Value;
		var mapCenter = player.Transform.Original.position;
		if (TryGetMapWorldBounds(world, out var worldBounds))
		{
			var horizontalSize = Mathf.Max(1f, worldBounds.size.x);
			var depthSize = Mathf.Max(1f, worldBounds.size.z);
			var aspect = Screen.width / Mathf.Max(1f, Screen.height);
			mapSize = Mathf.Max(depthSize * 0.5f, horizontalSize / (2f * aspect)) * 1.06f;
			mapCenter = worldBounds.center;
		}

		mapSize *= Settings.MapZoomOut.Value;
		_mapCamera.orthographicSize = mapSize;
		_mapCamera.nearClipPlane = 0.1f;
		_mapCamera.farClipPlane = Mathf.Max(1500f, mapSize * 8f);
		_mapCamera.pixelRect = new Rect(0f, 0f, Screen.width, Screen.height);
		_mapCamera.aspect = Screen.width / Mathf.Max(1f, Screen.height);

		var cameraHeight = mapCenter.y +
			(_mapBoundsReady ? _mapWorldBounds.extents.y + mapSize * 2f : mapSize * 2f);
		_mapCamera.transform.SetPositionAndRotation(
			new Vector3(mapCenter.x, cameraHeight, mapCenter.z),
			Quaternion.Euler(90f, 0f, 0f));
	}

	private void UpdateMapCursorMode()
	{
		var shouldOverride = Settings.MapEnabled.Value && Settings.MapTeleportEnabled.Value;
		if (shouldOverride == _mapCursorOverrideActive)
			return;

		if (!shouldOverride)
		{
			RestoreMapCursor();
			return;
		}

		_mapCursorOriginalLockState = Cursor.lockState;
		_mapCursorOriginalVisible = Cursor.visible;
		_mapCursorOverrideActive = true;
		Cursor.lockState = CursorLockMode.None;
		Cursor.visible = true;
	}

	private void RestoreMapCursor()
	{
		if (!_mapCursorOverrideActive)
			return;

		_mapCursorOverrideActive = false;
		Cursor.lockState = _mapCursorOriginalLockState;
		Cursor.visible = _mapCursorOriginalVisible;
	}

	private bool TryGetMapWorldBounds(GameWorld world, out Bounds bounds)
	{
		if (_mapBoundsReady && _mapBoundsWorld == world)
		{
			bounds = _mapWorldBounds;
			return true;
		}

		var now = Time.unscaledTime;
		if (_mapBoundsWorld == world && now < _nextMapBoundsAttempt)
		{
			bounds = default;
			return false;
		}

		_mapBoundsWorld = world;
		_nextMapBoundsAttempt = now + MapBoundsRetryInterval;
		bounds = default;

		if (TryEncapsulateBorderZones(world.BorderZones, ref bounds))
			_mapBoundsSource = "GameWorld.BorderZones";
		else if (TryEncapsulateLocationSceneBounds(ref bounds))
			_mapBoundsSource = "LocationScene.BorderZones";
		else if (TryEncapsulateLevelBorderPoints(ref bounds))
			_mapBoundsSource = "LevelBorder.Points";
		else
		{
			_mapBoundsReady = false;
			if (_mapBoundsDiagnosticWorld != world)
			{
				_mapBoundsDiagnosticWorld = world;
				TrainerPlugin.Logger.LogWarning(
					"Map bounds unavailable: no usable BorderZones or LevelBorder points were found; " +
					"using the player-centered fallback.");
			}
			return false;
		}

		_mapWorldBounds = bounds;
		_mapBoundsReady = true;
		TrainerPlugin.Logger.LogInfo(
			$"Map bounds ready from {_mapBoundsSource}: " +
			$"center=({bounds.center.x:0},{bounds.center.z:0}), " +
			$"size=({bounds.size.x:0}x{bounds.size.z:0})m");
		return true;
	}

	private static bool TryEncapsulateBorderZones(
		Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EFT.Interactive.BorderZone>? borderZones,
		ref Bounds bounds)
	{
		if (borderZones == null)
			return false;

		var hasBounds = false;
		for (var index = 0; index < borderZones.Length; index++)
		{
			var collider = borderZones[index]?.Collider;
			if (collider == null)
				continue;

			Encapsulate(ref bounds, collider.bounds, ref hasBounds);
		}

		return hasBounds && IsUsableMapBounds(bounds);
	}

	private static bool TryEncapsulateLocationSceneBounds(ref Bounds bounds)
	{
		var scenes = LocationScene.LoadedScenes;
		if (scenes == null)
			return false;

		var hasBounds = false;
		for (var sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++)
		{
			var scene = scenes[sceneIndex];
			if (scene == null)
				continue;

			var borderZones = scene.BorderZones;
			if (borderZones == null)
				continue;

			for (var zoneIndex = 0; zoneIndex < borderZones.Length; zoneIndex++)
			{
				var collider = borderZones[zoneIndex]?.Collider;
				if (collider == null)
					continue;

				Encapsulate(ref bounds, collider.bounds, ref hasBounds);
			}
		}

		return hasBounds && IsUsableMapBounds(bounds);
	}

	private static bool TryEncapsulateLevelBorderPoints(ref Bounds bounds)
	{
		var borders = FindObjectsOfType<global::LevelBorder>();
		var hasBounds = false;
		foreach (var border in borders)
		{
			if (border == null)
				continue;

			var points = border.Points;
			if (points == null)
				continue;

			for (var index = 0; index < points.Length; index++)
			{
				var worldPoint = border.transform.TransformPoint(points[index]);
				EncapsulatePoint(ref bounds, worldPoint, ref hasBounds);
			}
		}

		return hasBounds && IsUsableMapBounds(bounds);
	}

	private static void Encapsulate(ref Bounds bounds, Bounds other, ref bool hasBounds)
	{
		if (!hasBounds)
		{
			bounds = other;
			hasBounds = true;
			return;
		}

		bounds.Encapsulate(other);
	}

	private static void EncapsulatePoint(ref Bounds bounds, Vector3 point, ref bool hasBounds)
	{
		if (!hasBounds)
		{
			bounds = new Bounds(point, Vector3.zero);
			hasBounds = true;
			return;
		}

		bounds.Encapsulate(point);
	}

	private static bool IsUsableMapBounds(Bounds bounds) =>
		bounds.size.x >= 10f && bounds.size.z >= 10f &&
		float.IsFinite(bounds.center.x) && float.IsFinite(bounds.center.z) &&
		float.IsFinite(bounds.size.x) && float.IsFinite(bounds.size.z);

	private void UpdateQuestMarkers(Player player)
	{
		if (!Settings.QuestLocations.Value)
		{
			_questMarkers.Clear();
			_activeStoryQuestNames.Clear();
			_regularQuestZoneIds.Clear();
			_storyQuestZoneIds.Clear();
			_questMarkersDirty = true;
			return;
		}

		var moduleMode =
			$"{Settings.ShowRegularQuests.Value}|" +
			$"{Settings.ShowStoryQuests.Value}";
		if (!string.Equals(_questMarkerMode, moduleMode, StringComparison.Ordinal))
			_questMarkersDirty = true;
		if (_questSubscriptionPlayer != player)
			_questMarkersDirty = true;
		if (!_questMarkersDirty && Time.time < _nextQuestRecoveryRefresh)
			return;

		EnsureQuestSubscriptions(player);
		_questMarkersDirty = false;
		_questMarkerMode = moduleMode;
		_nextQuestRecoveryRefresh = Time.time + 15f;
		_questMarkers.Clear();
		_activeQuestZones.Clear();
		_regularQuestZoneIds.Clear();
		_storyQuestZoneIds.Clear();
		CollectActiveQuestZones(player, _activeQuestZones);
		if (_activeQuestZones.Count == 0)
			return;

		EnsureQuestTriggerCache(player);
		var localPosition = player.Transform.Original.position;

		foreach (var trigger in _cachedExperienceTriggers)
		{
			if (_questMarkers.Count >= 500)
				break;
			if (trigger == null ||
				string.IsNullOrWhiteSpace(trigger.Id) ||
				!_activeQuestZones.TryGetValue(trigger.Id, out var label))
				continue;
			if (!ShouldShowQuestZone(trigger.Id))
				continue;

			var position = trigger.transform.position;
			if (Vector3.Distance(position, localPosition) <= Settings.QuestDistance.Value)
				_questMarkers.Add(new LootMarker(
					label,
					position,
					0));
		}

		foreach (var trigger in _cachedPlaceItemTriggers)
		{
			if (_questMarkers.Count >= 700)
				break;
			if (trigger == null ||
				string.IsNullOrWhiteSpace(trigger.Id) ||
				!_activeQuestZones.TryGetValue(trigger.Id, out var label))
				continue;
			if (!ShouldShowQuestZone(trigger.Id))
				continue;

			var position = trigger.transform.position;
			if (Vector3.Distance(position, localPosition) <= Settings.QuestDistance.Value)
				_questMarkers.Add(new LootMarker(
					label,
					position,
					0));
		}
	}

	private void EnsureQuestTriggerCache(Player player)
	{
		if (_questTriggerCachePlayer == player)
			return;

		_cachedExperienceTriggers.Clear();
		_cachedPlaceItemTriggers.Clear();
		foreach (var trigger in FindObjectsOfType<EFT.Interactive.ExperienceTrigger>())
		{
			if (trigger != null)
				_cachedExperienceTriggers.Add(trigger);
		}

		foreach (var trigger in FindObjectsOfType<EFT.Interactive.PlaceItemTrigger>())
		{
			if (trigger != null)
				_cachedPlaceItemTriggers.Add(trigger);
		}

		_questTriggerCachePlayer = player;
	}

	private void EnsureQuestSubscriptions(Player player)
	{
		if (_questSubscriptionPlayer != player)
		{
			UnsubscribeFromQuests();
			_questSubscriptionPlayer = player;
		}

		_questConditionChangedHandler ??= OnWatchedQuestConditionChanged;
		_questStatusChangedHandler ??= OnWatchedQuestStatusChanged;

		var profile = player.Profile;
		if (profile?.QuestsData == null)
			return;

		foreach (var questData in profile.QuestsData)
		{
			if (questData?.Status != EQuestStatus.Started ||
				questData.Quest == null ||
				!_watchedQuests.Add(questData.Quest))
				continue;

			try
			{
				questData.Quest.OnConditionChanged += _questConditionChangedHandler;
				questData.Quest.OnStatusChanged += _questStatusChangedHandler;
			}
			catch (Exception exception)
			{
				TrainerPlugin.Logger.LogWarning(
					"Unable to subscribe to quest marker updates: " + exception.Message);
			}
		}
	}

	private void UnsubscribeFromQuests()
	{
		foreach (var quest in _watchedQuests)
		{
			try
			{
				if (_questConditionChangedHandler != null)
					quest.OnConditionChanged -= _questConditionChangedHandler;
				if (_questStatusChangedHandler != null)
					quest.OnStatusChanged -= _questStatusChangedHandler;
			}
			catch
			{
				// Quest objects may already belong to an unloaded map.
			}
		}

		_watchedQuests.Clear();
		_questMarkersDirty = true;
	}

	private void OnWatchedQuestConditionChanged(Quest quest)
	{
		_questMarkersDirty = true;
		_nextQuestItemRefresh = 0f;
		_nextQuestItemMarkerScan = 0f;
	}

	private void OnWatchedQuestStatusChanged(Quest quest, bool succeeded)
	{
		_questMarkersDirty = true;
		_nextQuestItemRefresh = 0f;
		_nextQuestItemMarkerScan = 0f;
	}

	private void CollectActiveQuestZones(
		Player player,
		Dictionary<string, string> zones)
	{
		var profile = player.Profile;
		if (profile?.QuestsData == null)
			return;

		var storyQuestLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		CollectActiveStoryQuestLabels(player, storyQuestLabels);

		foreach (var questData in profile.QuestsData)
		{
			if (questData?.Status != EQuestStatus.Started ||
				questData.Template?.Conditions == null)
				continue;

			var quest = questData.Quest;
			if (quest == null ||
				!quest.Template.Conditions.TryGetValue(
					EQuestStatus.AvailableForFinish,
					out var conditions) ||
				conditions == null)
				continue;

			var questName = GetQuestDisplayName(quest, questData);
			var questId = questData.Id ?? quest.Template.Id;
			var storyQuestName = string.Empty;
			var isStory =
				!string.IsNullOrWhiteSpace(questId) &&
				storyQuestLabels.TryGetValue(questId, out storyQuestName);
			if (isStory)
				questName = $"{storyQuestName} · 剧情";

			foreach (var condition in conditions)
			{
				CollectConditionZones(
					quest,
					condition,
					questName,
					zones,
					_regularQuestZoneIds,
					_storyQuestZoneIds,
					isStory,
					0);
			}
		}
	}

	private bool ShouldShowQuestZone(string zoneId)
	{
		return
			(Settings.ShowRegularQuests.Value &&
				_regularQuestZoneIds.Contains(zoneId)) ||
			(Settings.ShowStoryQuests.Value &&
				_storyQuestZoneIds.Contains(zoneId));
	}

	private void CollectActiveStoryQuestLabels(
		Player player,
		Dictionary<string, string> labels)
	{
		var profile = player.Profile;
		if (profile?.QuestsData == null)
			return;

		_activeStoryQuestNames.Clear();
		foreach (var storyQuestData in profile.QuestsData)
		{
			if (storyQuestData?.Status != EQuestStatus.Started ||
				storyQuestData.Template?.IsMainQuest != true ||
				storyQuestData.Quest == null ||
				storyQuestData.Template.Conditions == null ||
				!storyQuestData.Template.Conditions.TryGetValue(
					EQuestStatus.AvailableForFinish,
					out var storyConditions))
				continue;

			var storyId = storyQuestData.Id ?? storyQuestData.Template.Id;
			if (string.IsNullOrWhiteSpace(storyId))
				continue;

			var storyName = GetQuestDisplayName(storyQuestData.Quest, storyQuestData);
			if (!_activeStoryQuestNames.Contains(storyName, StringComparer.Ordinal))
				_activeStoryQuestNames.Add(storyName);
			labels[storyId] = storyName;
			CollectActiveStoryQuestBranches(
				storyQuestData.Quest,
				storyConditions,
				profile,
				labels,
				new HashSet<string>(StringComparer.OrdinalIgnoreCase) { storyId },
				storyName);
		}
	}

	private void CollectActiveStoryQuestBranches(
		Quest ownerQuest,
		IEnumerable<Condition>? conditions,
		EFT.Profile profile,
		Dictionary<string, string> labels,
		HashSet<string> visited,
		string storyName)
	{
		if (conditions == null)
			return;

		foreach (var condition in conditions)
		{
			if (condition == null || IsConditionDone(ownerQuest, condition))
				continue;

			if (condition is ConditionQuest questLink &&
				!string.IsNullOrWhiteSpace(questLink.target))
			{
				var targetId = questLink.target.Trim();
				if (visited.Add(targetId))
				{
					var targetData = FindQuestStatusData(profile, targetId);
					if (targetData?.Status == EQuestStatus.Started &&
						targetData.Quest != null &&
						targetData.Template?.Conditions != null &&
						targetData.Template.Conditions.TryGetValue(
							EQuestStatus.AvailableForFinish,
							out var targetConditions))
					{
						labels[targetId] = storyName;
						CollectActiveStoryQuestBranches(
							targetData.Quest,
							targetConditions,
							profile,
							labels,
							visited,
							storyName);
					}
				}
			}

			if (condition is ConditionCounterCreator counter)
			{
				CollectActiveStoryQuestBranches(
					ownerQuest,
					counter.Conditions,
					profile,
					labels,
					visited,
					storyName);
			}

			CollectActiveStoryQuestBranches(
				ownerQuest,
				condition.ChildConditions,
				profile,
				labels,
				visited,
				storyName);
		}
	}

	private static EFT.Quests.QuestStatusData? FindQuestStatusData(
		EFT.Profile profile,
		string questId)
	{
		if (profile.QuestsData == null)
			return null;

		foreach (var questData in profile.QuestsData)
		{
			if (questData == null)
				continue;

			var currentId = questData.Id ?? questData.Template?.Id;
			if (string.Equals(currentId, questId, StringComparison.OrdinalIgnoreCase))
				return questData;
		}

		return null;
	}

	private static void CollectConditionZones(
		Quest quest,
		Condition? condition,
		string questName,
		Dictionary<string, string> zones,
		HashSet<string> regularZones,
		HashSet<string> storyZones,
		bool isStory,
		int depth)
	{
		if (condition == null || depth > 8)
			return;

		if (IsConditionDone(quest, condition))
			return;

		switch (condition)
		{
			case ConditionZone zone:
				AddQuestZones(
					zones,
					regularZones,
					storyZones,
					zone.zoneIds,
					$"{questName} · 放置",
					isStory);
				break;
			case ConditionInZone inZone:
				AddQuestZones(
					zones,
					regularZones,
					storyZones,
					inZone.zoneIds,
					$"{questName} · 踩点",
					isStory);
				break;
			case ConditionVisitPlace visit:
				AddQuestZone(
					zones,
					regularZones,
					storyZones,
					visit.target,
					$"{questName} · 踩点",
					isStory);
				break;
			case ConditionLocationTrigger locationTrigger:
				AddQuestZone(
					zones,
					regularZones,
					storyZones,
					locationTrigger.target,
					$"{questName} · 踩点",
					isStory);
				break;
		}

		if (condition is ConditionCounterCreator counter &&
			counter.Conditions != null)
		{
			foreach (var child in counter.Conditions)
			{
				CollectConditionZones(
					quest,
					child,
					questName,
					zones,
					regularZones,
					storyZones,
					isStory,
					depth + 1);
			}
		}

		if (condition.ChildConditions != null)
		{
			foreach (var child in condition.ChildConditions)
			{
				CollectConditionZones(
					quest,
					child,
					questName,
					zones,
					regularZones,
					storyZones,
					isStory,
					depth + 1);
			}
		}
	}

	private static bool IsConditionDone(Quest quest, Condition condition)
	{
		try
		{
			return quest.IsConditionDone(condition);
		}
		catch
		{
			return false;
		}
	}

	private static void AddQuestZones(
		Dictionary<string, string> zones,
		HashSet<string> regularZones,
		HashSet<string> storyZones,
		IEnumerable<string>? zoneIds,
		string label,
		bool isStory)
	{
		if (zoneIds == null)
			return;

		foreach (var zoneId in zoneIds)
			AddQuestZone(zones, regularZones, storyZones, zoneId, label, isStory);
	}

	private static void AddQuestZone(
		Dictionary<string, string> zones,
		HashSet<string> regularZones,
		HashSet<string> storyZones,
		string? zoneId,
		string label,
		bool isStory)
	{
		if (string.IsNullOrWhiteSpace(zoneId))
			return;

		var key = zoneId.Trim();
		(isStory ? storyZones : regularZones).Add(key);
		if (!zones.TryGetValue(key, out var existing))
		{
			zones[key] = label;
			return;
		}

		if (!existing.Split(" / ", StringSplitOptions.RemoveEmptyEntries)
				.Contains(label, StringComparer.Ordinal))
			zones[key] = existing + " / " + label;
	}

	private void UpdateLootMarkers(GameWorld world, Player player)
	{
		if (!Settings.LootEspEnabled.Value)
		{
			_lootMarkers.Clear();
			_lootMarkersByKey.Clear();
			_lootMarkersBuilt = false;
			return;
		}

		EnsureLootCache(world, player);

		var mode =
			$"{Settings.LootFilter.Value}|" +
			$"{Settings.ShowLootPrices.Value}|" +
			$"{Settings.ShowCorpses.Value}|" +
			$"{Settings.ShowContainers.Value}";
		if (!_lootMarkersBuilt ||
			!string.Equals(_lootMarkerMode, mode, StringComparison.Ordinal))
		{
			RebuildLootMarkers(player);
			_lootMarkersBuilt = true;
			_lootMarkerMode = mode;
		}

		if (Time.time < _nextLootScan)
			return;

		_nextLootScan = Time.time + 5f;
		RefreshNearbyLootMarkers(player);
	}

	private void UpdateQuestItemMarkers(GameWorld world, Player player)
	{
		if (!Settings.QuestLocations.Value ||
			(!Settings.ShowQuestItems.Value && !Settings.ShowQuestItemsInContainers.Value))
		{
			_questItemMarkers.Clear();
			_questItemMarkerKeys.Clear();
			_questItemMarkersDirty = true;
			return;
		}

		EnsureLootCache(world, player);
		var mode =
			$"{Settings.ShowQuestItems.Value}|" +
			$"{Settings.ShowQuestItemsInContainers.Value}|" +
			$"{Settings.QuestItemsActiveOnly.Value}|" +
			$"{Settings.ShowRegularQuests.Value}|" +
			$"{Settings.ShowStoryQuests.Value}|" +
			$"{Settings.QuestDistance.Value:0}";
		if (_questItemMarkersDirty ||
			!string.Equals(_questItemMarkerMode, mode, StringComparison.Ordinal))
		{
			RebuildQuestItemMarkers(world, player);
			_questItemMarkersDirty = false;
			_questItemMarkerMode = mode;
		}

		if (Time.time < _nextQuestItemMarkerScan)
			return;

		_nextQuestItemMarkerScan = Time.time + 10f;
		RefreshNearbyQuestItemMarkers(player);
	}

	private void RebuildQuestItemMarkers(GameWorld world, Player player)
	{
		_questItemMarkers.Clear();
		_questItemMarkerKeys.Clear();
		var localPosition = player.Transform.Original.position;
		var maximumDistance = Settings.QuestDistance.Value;
		var dedicatedQuestItemCount = 0;

		if (Settings.ShowQuestItems.Value)
		{
			var questItems = world.QuestItemsList;
			if (questItems != null && questItems.Count > 0)
			{
				foreach (var lootItem in questItems)
				{
					dedicatedQuestItemCount++;
					AddQuestItemMarker(lootItem, localPosition, maximumDistance);
				}
			}

			foreach (var bucket in _lootSpatialBuckets.Values)
			{
				foreach (var lootItem in bucket)
					AddQuestItemMarker(lootItem, localPosition, maximumDistance);
			}
		}

		if (!Settings.ShowQuestItemsInContainers.Value)
			return;

		foreach (var bucket in _containerSpatialBuckets.Values)
		{
			foreach (var cachedContainer in bucket)
				AddQuestItemContainerMarker(
					cachedContainer,
					localPosition,
					maximumDistance);
		}

		TrainerPlugin.Logger.LogInfo(
			$"Quest item markers rebuilt: dedicatedItems={dedicatedQuestItemCount}; " +
			$"activeTemplates={_activeQuestItems.Count}; " +
			$"markers={_questItemMarkers.Count}; " +
			$"lootCache={_cachedLootItemCount}; " +
			$"containers={_containerSpatialBuckets.Values.Sum(bucket => bucket.Count)}.");
	}

	private void RefreshNearbyQuestItemMarkers(Player player)
	{
		var localPosition = player.Transform.Original.position;
		foreach (var marker in _questItemMarkers.ToArray())
		{
			if (Vector3.Distance(marker.Position, localPosition) > NearbyLootRadius)
				continue;

			var invalid = marker.Key.StartsWith("quest-item:", StringComparison.Ordinal)
				? marker.Source == null || marker.Source.Item == null
				: marker.RootItem == null;
			if (!invalid)
				continue;

			if (_questItemMarkerKeys.Remove(marker.Key))
				_questItemMarkers.Remove(marker);
		}

		var centerCell = GetLootCell(localPosition);
		for (var x = centerCell.x - 1; x <= centerCell.x + 1; x++)
		{
			for (var z = centerCell.y - 1; z <= centerCell.y + 1; z++)
			{
				if (Settings.ShowQuestItems.Value &&
					_lootSpatialBuckets.TryGetValue(
						new Vector2Int(x, z),
						out var itemBucket))
				{
					foreach (var lootItem in itemBucket)
					{
						if (lootItem?.Item == null ||
							!_questItemMarkerKeys.Contains(
								"quest-item:" + lootItem.Item.Id))
						{
							AddQuestItemMarker(
								lootItem!,
								localPosition,
								NearbyLootRadius);
						}
					}
				}

				if (Settings.ShowQuestItemsInContainers.Value &&
					_containerSpatialBuckets.TryGetValue(
						new Vector2Int(x, z),
						out var containerBucket))
				{
					foreach (var cachedContainer in containerBucket)
					{
						if (cachedContainer.RootItem == null ||
							!_questItemMarkerKeys.Contains(
								"quest-container:" + cachedContainer.RootItem.Id))
						{
							AddQuestItemContainerMarker(
								cachedContainer,
								localPosition,
								NearbyLootRadius);
						}
					}
				}
			}
		}
	}

	private void AddQuestItemMarker(
		EFT.Interactive.LootItem lootItem,
		Vector3 localPosition,
		float maximumDistance)
	{
		var item = lootItem?.Item;
		if (item == null || !IsQuestItemRelevant(item))
			return;

		var templateId = item.TemplateId.ToString();
		var position = lootItem!.transform.position;
		if (Vector3.Distance(position, localPosition) > maximumDistance)
			return;

		var key = "quest-item:" + item.Id;
		if (!_questItemMarkerKeys.Add(key))
			return;

		var itemName = GetLocalizedItemName(item);
		var label = _activeQuestItemLabels.TryGetValue(templateId, out var questName)
			? $"{itemName} · {questName} · 任务物品"
			: $"{itemName} · 任务物品";
		_questItemMarkers.Add(new LootMarker(
			label,
			position,
			0,
			lootItem,
			null,
			key));
	}

	private bool IsQuestItemRelevant(Item item)
	{
		if (!Settings.QuestItemsActiveOnly.Value)
			return item.QuestItem;

		var templateId = item.TemplateId.ToString();
		return _activeQuestItems.Contains(templateId) &&
			ShouldShowQuestItemTemplate(templateId);
	}

	private void AddQuestItemContainerMarker(
		CachedLootContainer cachedContainer,
		Vector3 localPosition,
		float maximumDistance)
	{
		var rootItem = cachedContainer.RootItem;
		var transform = cachedContainer.Transform;
		if (rootItem == null || transform == null)
			return;

		var position = transform.position;
		if (Vector3.Distance(position, localPosition) > maximumDistance)
			return;

		var containedQuestItems = GetContainerQuestItemNames(rootItem);
		if (containedQuestItems.Count == 0)
			return;

		var key = "quest-container:" + rootItem.Id;
		if (!_questItemMarkerKeys.Add(key))
			return;

		_questItemMarkers.Add(new LootMarker(
			$"{GetLocalizedItemName(rootItem)} · {string.Join(" / ", containedQuestItems)} · 任务物品容器",
			position,
			0,
			null,
			rootItem,
			key));
	}

	private void EnsureLootCache(GameWorld world, Player player)
	{
		var lootItemCount = world.LootItems?.Count ?? 0;
		if (_lootCachePlayer != player ||
			_cachedLootItemCount != lootItemCount)
		{
			RebuildLootCache(world, player);
		}
	}

	private void RebuildLootCache(GameWorld world, Player player)
	{
		_lootSpatialBuckets.Clear();
		_containerSpatialBuckets.Clear();
		_cachedContainerIds.Clear();
		_lootMarkersBuilt = false;
		_questItemMarkersDirty = true;

		var lootItems = world.LootItems;
		_cachedLootItemCount = lootItems?.Count ?? 0;
		if (lootItems != null)
		{
			for (var index = 0; index < lootItems.Count; index++)
			{
				var lootItem = lootItems.GetByIndex(index);
				if (lootItem != null)
				{
					AddSpatialItem(lootItem);
					if (lootItem.Item?.IsContainer == true)
					{
						AddSpatialContainer(
							new CachedLootContainer(lootItem.Item, lootItem.transform));
					}
				}
			}
		}

		_lootCachePlayer = player;
	}

	private void AddSpatialItem(EFT.Interactive.LootItem lootItem)
	{
		var cell = GetLootCell(lootItem.transform.position);
		if (!_lootSpatialBuckets.TryGetValue(cell, out var bucket))
		{
			bucket = [];
			_lootSpatialBuckets[cell] = bucket;
		}

		bucket.Add(lootItem);
	}

	private void AddSpatialContainer(CachedLootContainer container)
	{
		if (container.RootItem == null ||
			container.Transform == null ||
			! _cachedContainerIds.Add(container.RootItem.Id))
		{
			return;
		}

		var cell = GetLootCell(container.Transform.position);
		if (!_containerSpatialBuckets.TryGetValue(cell, out var bucket))
		{
			bucket = [];
			_containerSpatialBuckets[cell] = bucket;
		}

		bucket.Add(container);
	}

	private static Vector2Int GetLootCell(Vector3 position)
	{
		return new Vector2Int(
			Mathf.FloorToInt(position.x / LootSpatialCellSize),
			Mathf.FloorToInt(position.z / LootSpatialCellSize));
	}

	private static bool ContainsQuestItem(Item rootItem)
	{
		try
		{
			foreach (var item in rootItem.GetAllItems())
			{
				if (item?.QuestItem == true)
					return true;
			}
		}
		catch
		{
			// Ignore containers whose contents cannot be enumerated.
		}

		return false;
	}

	private void RebuildLootMarkers(Player player)
	{
		_lootMarkers.Clear();
		_lootMarkersByKey.Clear();

		var filters = (Settings.LootFilter.Value ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		foreach (var bucket in _lootSpatialBuckets.Values)
		{
			foreach (var lootItem in bucket)
				AddLootItemMarker(lootItem, filters);
		}

		if (Settings.ShowContainers.Value)
		{
			foreach (var bucket in _containerSpatialBuckets.Values)
			{
				foreach (var cachedContainer in bucket)
					AddLootContainerMarker(cachedContainer);
			}
		}
	}

	private void RefreshNearbyLootMarkers(Player player)
	{
		var localPosition = player.Transform.Original.position;
		var centerCell = GetLootCell(localPosition);
		var filters = (Settings.LootFilter.Value ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (var marker in _lootMarkers.ToArray())
		{
			if (string.IsNullOrWhiteSpace(marker.Key) ||
				Vector3.Distance(marker.Position, localPosition) > NearbyLootRadius)
			{
				continue;
			}

			var invalid = marker.Key.StartsWith("loot:", StringComparison.Ordinal)
				? marker.Source == null || marker.Source.Item == null
				: marker.RootItem == null;
			if (invalid)
			{
				RemoveLootMarker(marker.Key);
			}
		}

		for (var x = centerCell.x - 1; x <= centerCell.x + 1; x++)
		{
			for (var z = centerCell.y - 1; z <= centerCell.y + 1; z++)
			{
				if (_lootSpatialBuckets.TryGetValue(
						new Vector2Int(x, z),
						out var itemBucket))
				{
					foreach (var lootItem in itemBucket)
					{
						var item = lootItem?.Item;
						if (item == null || item.QuestItem)
							continue;

						AddLootItemMarker(lootItem!, filters);
					}
				}

				if (Settings.ShowContainers.Value &&
					_containerSpatialBuckets.TryGetValue(
						new Vector2Int(x, z),
						out var containerBucket))
				{
					foreach (var cachedContainer in containerBucket)
					{
						if (cachedContainer.RootItem != null)
							AddLootContainerMarker(cachedContainer);
					}
				}
			}
		}
	}

	private void AddLootItemMarker(
		EFT.Interactive.LootItem lootItem,
		IReadOnlyList<string> filters)
	{
		var item = lootItem?.Item;
		if (item == null || item.QuestItem)
			return;

		var key = "loot:" + item.Id;
		if (_lootMarkersByKey.ContainsKey(key))
			return;

		var isCorpse = lootItem is EFT.Interactive.Corpse;
		if (isCorpse && !Settings.ShowCorpses.Value)
			return;

		var name = isCorpse ? "尸体" : GetLocalizedItemName(item);
		var templateId = item.TemplateId.ToString();
		if (!isCorpse &&
			filters.Count > 0 &&
			!filters.Any(filter =>
				name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
				templateId.Equals(filter, StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}

		var marker = new LootMarker(
			name,
			lootItem!.transform.position,
			Settings.ShowLootPrices.Value ? item.Template.CreditsPrice : 0,
			lootItem,
			null,
			key);
		_lootMarkers.Add(marker);
		_lootMarkersByKey[key] = marker;
	}

	private void AddLootContainerMarker(CachedLootContainer cachedContainer)
	{
		var rootItem = cachedContainer.RootItem;
		var transform = cachedContainer.Transform;
		if (rootItem == null || transform == null)
			return;

		var key = "container:" + rootItem.Id;
		if (_lootMarkersByKey.ContainsKey(key))
			return;

		var marker = new LootMarker(
			$"容器：{GetLocalizedItemName(rootItem)}",
			transform.position,
			0,
			null,
			rootItem,
			key);
		_lootMarkers.Add(marker);
		_lootMarkersByKey[key] = marker;
	}

	private void RemoveLootMarker(string? key)
	{
		if (string.IsNullOrWhiteSpace(key) ||
			!_lootMarkersByKey.Remove(key, out var marker))
		{
			return;
		}

		_lootMarkers.Remove(marker);
	}

	private List<string> GetContainerQuestItemNames(Item rootItem)
	{
		var names = new List<string>();

		var seenTemplates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			foreach (var item in rootItem.GetAllItems())
			{
				if (item == null || !IsQuestItemRelevant(item))
					continue;

				var templateId = item.TemplateId.ToString();
				if (!seenTemplates.Add(templateId))
					continue;

				names.Add(GetLocalizedItemName(item));
				if (names.Count >= 3)
					break;
			}
		}
		catch
		{
			// Ignore containers that cannot expose their contents.
		}

		return names;
	}

	private void RefreshActiveQuestItems(Player player)
	{
		if (Time.time < _nextQuestItemRefresh)
			return;

		_nextQuestItemRefresh = Time.time + 10f;
		var previousFingerprint = GetActiveQuestItemFingerprint();
		_activeQuestItems.Clear();
		_activeQuestItemLabels.Clear();
		_regularQuestItemTemplates.Clear();
		_storyQuestItemTemplates.Clear();

		var profile = player.Profile;
		if (profile?.QuestsData == null)
		{
			if (!string.Equals(
					previousFingerprint,
					GetActiveQuestItemFingerprint(),
					StringComparison.Ordinal))
			{
				_questItemMarkersDirty = true;
			}
			return;
		}

		var storyQuestLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		CollectActiveStoryQuestLabels(player, storyQuestLabels);

		foreach (var questData in profile.QuestsData)
		{
			if (questData?.Status != EQuestStatus.Started || questData.Template?.Conditions == null)
				continue;

			var quest = questData.Quest;
			if (quest == null)
				continue;

			if (!questData.Template.Conditions.TryGetValue(EQuestStatus.AvailableForFinish, out var conditions) ||
				conditions == null)
				continue;

			var questName = GetQuestDisplayName(quest, questData);
			var questId = questData.Id ?? questData.Template.Id;
			var storyQuestName = string.Empty;
			var isStory =
				!string.IsNullOrWhiteSpace(questId) &&
				storyQuestLabels.TryGetValue(questId, out storyQuestName);
			if (isStory)
				questName = $"{storyQuestName} · 剧情";

			foreach (var condition in conditions)
				CollectQuestItemTargets(quest, condition, questName, isStory, 0);
		}

		if (!string.Equals(
				previousFingerprint,
				GetActiveQuestItemFingerprint(),
				StringComparison.Ordinal))
		{
			_questItemMarkersDirty = true;
		}
	}

	private string GetActiveQuestItemFingerprint()
	{
		return string.Join(
			"|",
			_activeQuestItems
				.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
				.Select(item =>
					(_activeQuestItemLabels.TryGetValue(item, out var label)
						? item + "=" + label
						: item) +
					(_storyQuestItemTemplates.Contains(item) ? "[story]" : "[regular]")));
	}

	private void CollectQuestItemTargets(
		Quest quest,
		Condition? condition,
		string questName,
		bool isStory,
		int depth)
	{
		if (condition == null || depth > 8 || IsConditionDone(quest, condition))
			return;

		switch (condition)
		{
			case ConditionItem itemCondition:
				if (itemCondition.target != null)
				{
					foreach (var target in itemCondition.target)
						AddActiveQuestItem(target, questName, isStory);
				}
				break;
			case ConditionCompletableItem completable:
				AddActiveQuestItem(completable.target, questName, isStory);
				break;
		}

		if (condition is ConditionCounterCreator counter &&
			counter.Conditions != null)
		{
			foreach (var child in counter.Conditions)
				CollectQuestItemTargets(quest, child, questName, isStory, depth + 1);
		}

		if (condition.ChildConditions != null)
		{
			foreach (var child in condition.ChildConditions)
				CollectQuestItemTargets(quest, child, questName, isStory, depth + 1);
		}
	}

	private void AddActiveQuestItem(
		string? templateId,
		string questName,
		bool isStory)
	{
		if (string.IsNullOrWhiteSpace(templateId))
			return;

		var key = templateId.Trim();
		_activeQuestItems.Add(key);
		(isStory ? _storyQuestItemTemplates : _regularQuestItemTemplates).Add(key);
		if (!_activeQuestItemLabels.TryGetValue(key, out var existing))
		{
			_activeQuestItemLabels[key] = questName;
			return;
		}

		if (!existing.Split(" / ", StringSplitOptions.RemoveEmptyEntries)
				.Contains(questName, StringComparer.Ordinal))
			_activeQuestItemLabels[key] = existing + " / " + questName;
	}

	private bool ShouldShowQuestItemTemplate(string templateId)
	{
		if (!Settings.QuestItemsActiveOnly.Value)
			return Settings.ShowRegularQuests.Value || Settings.ShowStoryQuests.Value;

		return
			(Settings.ShowRegularQuests.Value &&
				_regularQuestItemTemplates.Contains(templateId)) ||
			(Settings.ShowStoryQuests.Value &&
				_storyQuestItemTemplates.Contains(templateId));
	}

	private void UpdateVisionUtilities(Camera? camera)
	{
		if (camera == null)
			return;

		if (Settings.NightVision.Value || _nightVisionApplied)
		{
			var nightVision = camera.GetComponent<BSG.CameraEffects.NightVision>();
			if (nightVision != null && Settings.NightVision.Value != _nightVisionApplied)
			{
				nightVision.StartSwitch(Settings.NightVision.Value);
				_nightVisionApplied = Settings.NightVision.Value;
			}
			if (nightVision != null && Settings.NightVision.Value)
			{
				nightVision.Intensity = 0f;
				nightVision.NoiseIntensity = 0f;
			}
		}

		if (Settings.NoFlash.Value)
		{
			if (camera.GetComponent<GrenadeFlashScreenEffect>() is { } flash)
			{
				flash.enabled = false;
				flash.EffectStrength = 0f;
			}

			if (camera.GetComponent<EyeBurn>() is { } eyeBurn)
			{
				eyeBurn.enabled = false;
				eyeBurn.EyesBurn = false;
			}
		}

		if (Settings.NoVisor.Value &&
			camera.GetComponent<VisorEffect>() is { } visor)
			visor.Intensity = 0f;
	}

	private static void SavePlayerPosition()
	{
		var player = Singleton<GameWorld>.Instance?.MainPlayer;
		var behaviour = FindObjectOfType<TrainerBehaviour>();
		if (player == null || behaviour == null)
			return;

		behaviour._savedPosition = player.transform.position;
		behaviour._savedRotation = player.transform.rotation;
	}

	private static void RestorePlayerPosition()
	{
		var player = Singleton<GameWorld>.Instance?.MainPlayer;
		var behaviour = FindObjectOfType<TrainerBehaviour>();
		if (player == null || behaviour?._savedPosition == null)
			return;

		player.transform.SetPositionAndRotation(behaviour._savedPosition.Value, behaviour._savedRotation);
	}

	private void RestoreMapTeleportOrigin()
	{
		var player = Singleton<GameWorld>.Instance?.MainPlayer;
		if (player == null || !_mapTeleportOrigin.HasValue)
			return;

		player.transform.SetPositionAndRotation(_mapTeleportOrigin.Value, _mapTeleportOriginRotation);
		_lastError = "已返回上次地图传送前的位置。";
		_lastErrorTime = Time.time;
	}

	private static void SetWorldTime(float hour)
	{
		var sky = FindObjectOfType<TOD_Sky>();
		if (sky == null)
			return;

		sky.Components.Time.GameDateTime = null;
		sky.Cycle.Hour = hour;
	}

	private static void SetClearWeather()
	{
		var controller = WeatherController.Instance;
		if (controller == null)
			return;

		var weather = controller.WeatherDebug;
		weather.Enabled = true;
		weather.CloudDensity = -0.7f;
		weather.Fog = 0.004f;
		weather.LightningThunderProbability = 0f;
		weather.Rain = 0f;
	}

	private static void SummonTrain()
	{
		var locomotive = FindObjectOfType<Locomotive>();
		if (locomotive != null)
			locomotive.Init(Il2CppSystem.DateTime.UtcNow);
	}

	private static void TriggerAirDrop()
	{
		const string redSignalFlareId = "624c09cfbc2e27219346d955";
		var player = Singleton<GameWorld>.Instance?.MainPlayer;
		var factory = Singleton<EFT.ItemFactory>.Instance;
		if (player == null || factory == null)
			return;

		AmmoTemplate? template = null;
		foreach (var pair in factory.ItemTemplates)
		{
			if (pair.Key.ToString().Equals(redSignalFlareId, StringComparison.OrdinalIgnoreCase))
			{
				template = pair.Value as AmmoTemplate;
				break;
			}
		}

		if (template != null)
			player.HandleFlareSuccessEvent(player.Transform.Original.position, template);
	}

	private void UpdateAimbot()
	{
		var world = Singleton<GameWorld>.Instance;
		var localPlayer = world?.MainPlayer;
		var mainCamera = Camera.main;
		if (world == null || localPlayer == null || mainCamera == null)
			return;

		var camera = mainCamera;
		if (!TryGetTarget(world, localPlayer, camera, out var targetPosition))
			return;

		AimAtPosition(localPlayer, targetPosition, Settings.Smoothness.Value);
	}

	private static bool TryGetTarget(GameWorld world, Player localPlayer, Camera camera, out Vector3 targetPosition)
	{
		targetPosition = default;
		var bestPriority = int.MaxValue;
		var bestDistance = float.MaxValue;
		var selected = ParseTargetPart(Settings.TargetPart.Value);
		var projectileSpeed = GetProjectileSpeed(localPlayer);

		foreach (var hostile in world.AllAlivePlayersList)
		{
			if (hostile == null || hostile.IsYourPlayer || hostile.HealthController?.IsAlive != true)
				continue;

			if (Settings.PreferHeadChest.Value)
			{
				TryConsiderPlayer(localPlayer, hostile, camera, AimPart.Head, 0, projectileSpeed, ref bestPriority, ref bestDistance, ref targetPosition);
				TryConsiderPlayer(localPlayer, hostile, camera, AimPart.Chest, 1, projectileSpeed, ref bestPriority, ref bestDistance, ref targetPosition);
			}

			var selectedPriority = Settings.PreferHeadChest.Value && selected is not (AimPart.Head or AimPart.Chest)
				? 2
				: 0;
			TryConsiderPlayer(localPlayer, hostile, camera, selected, selectedPriority, projectileSpeed, ref bestPriority, ref bestDistance, ref targetPosition);
		}

		return bestPriority != int.MaxValue;
	}

	private static void TryConsiderPlayer(
		Player localPlayer,
		Player targetPlayer,
		Camera camera,
		AimPart part,
		int priority,
		float projectileSpeed,
		ref int bestPriority,
		ref float bestDistance,
		ref Vector3 bestPosition)
	{
		var transform = GetTargetTransform(targetPlayer, part, camera);
		if (transform == null)
			return;

		var position = transform.position;
		var distance = Vector3.Distance(camera.transform.position, position);
		if (distance > Mathf.Min(Settings.AimbotDistance.Value, 500f))
			return;

		var screen = WorldToScreen(camera, position);
		if (screen == Vector2.zero)
			return;

		if (Settings.FovRadius.Value > 0f &&
			Vector2.Distance(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), screen) > Settings.FovRadius.Value)
			return;

		if (Settings.VisibleOnly.Value && !HasLineOfSight(localPlayer, targetPlayer, camera, transform))
			return;

		if (priority > bestPriority || (priority == bestPriority && distance >= bestDistance))
			return;

		bestPriority = priority;
		bestDistance = distance;
		bestPosition = CalculateInterceptPoint(
			localPlayer,
			position,
			targetPlayer.Velocity,
			projectileSpeed);
	}

	private static float GetProjectileSpeed(Player player)
	{
		try
		{
			if (player.HandsController?.Item is Weapon weapon &&
				weapon.CurrentAmmoTemplate is { InitialSpeed: > 0f } ammoTemplate)
				return ammoTemplate.InitialSpeed;
		}
		catch
		{
			// Use a conservative fallback for the aiming calculation.
		}

		return 800f;
	}

	private static Vector3 CalculateInterceptPoint(
		Player player,
		Vector3 targetPosition,
		Vector3 targetVelocity,
		float projectileSpeed)
	{
		var leadMultiplier = Mathf.Max(0f, Settings.AimLeadMultiplier.Value);
		if (leadMultiplier <= 0f || targetVelocity.sqrMagnitude < 0.01f)
			return targetPosition;

		var origin = player.Fireport?.position ?? player.Transform.Original.position;
		var predicted = targetPosition;
		for (var iteration = 0; iteration < 4; iteration++)
		{
			var travelTime = Mathf.Clamp(
				Vector3.Distance(origin, predicted) /
				Mathf.Max(0.01f, projectileSpeed) *
				leadMultiplier,
				0f,
				1.5f);
			var next = targetPosition + targetVelocity * travelTime;
			if ((next - predicted).sqrMagnitude < 0.0001f)
				break;

			predicted = next;
		}

		return predicted;
	}

	private static Transform? GetTargetTransform(Player player, AimPart part, Camera camera)
	{
		var bones = player.PlayerBones;
		if (bones == null)
			return null;

		return part switch
		{
			AimPart.Head => bones.Head.Original,
			AimPart.Chest => bones.Ribcage.Original,
			AimPart.Stomach => bones.Spine1,
			AimPart.Arms => GetBestTransform(camera, bones.Upperarms[0], bones.Upperarms[1]),
			AimPart.Legs => GetBestTransform(camera, bones.LeftThigh2.Original, bones.RightThigh2.Original),
			_ => null
		};
	}

	private static Transform GetBestTransform(Camera camera, Transform left, Transform right)
	{
		var origin = camera.transform.position;
		return Vector3.SqrMagnitude(left.position - origin) <= Vector3.SqrMagnitude(right.position - origin)
			? left
			: right;
	}

	private static bool HasLineOfSight(Player localPlayer, Player targetPlayer, Camera camera, Transform target)
	{
		var origin = localPlayer.PlayerBones?.Head.Original.position ??
			localPlayer.Fireport?.position ??
			camera.transform.position;

		if (!Physics.Linecast(
				origin,
				target.position,
				out var hit,
				Physics.DefaultRaycastLayers,
				QueryTriggerInteraction.Ignore))
			return true;

		var hitTransform = hit.transform;
		if (hitTransform == null)
			return true;

		if (hitTransform.IsChildOf(targetPlayer.Transform.Original))
			return true;

		return localPlayer.Transform.Original != null &&
			hitTransform.IsChildOf(localPlayer.Transform.Original);
	}

	private static void AimAtPosition(Player player, Vector3 targetPosition, float smoothness)
	{
		var fireport = player.Fireport;
		if (fireport == null)
			return;

		var firingAngle = fireport.position - fireport.up;
		var normalized = (targetPosition - firingAngle).normalized;
		if (normalized == Vector3.zero)
			return;

		var euler = Quaternion.LookRotation(normalized).eulerAngles;
		if (euler.x > 180f)
			euler.x -= 360f;

		var current = player.MovementContext.Rotation;
		var target = new Vector2(euler.y, euler.x);
		var delta = current - target;
		NormalizeAngle(ref delta);
		player.MovementContext.Rotation = current - delta * Mathf.Clamp01(smoothness);
	}

	private static void NormalizeAngle(ref Vector2 angle)
	{
		if (angle.x <= -180f)
			angle.x += 360f;
		else if (angle.x > 180f)
			angle.x -= 360f;

		if (angle.y > 90f)
			angle.y -= 180f;
		else if (angle.y <= -90f)
			angle.y += 180f;
	}

	private void DrawEsp()
	{
		if (Event.current.type != EventType.Repaint)
			return;

		var world = Singleton<GameWorld>.Instance;
		var localPlayer = world?.MainPlayer;
		var mainCamera = Camera.main;
		if (world == null || localPlayer == null || mainCamera == null)
			return;

		var camera = mainCamera;
		var oldColor = GUI.color;
		foreach (var player in world.AllAlivePlayersList)
		{
			if (player == null || player.IsYourPlayer || player.HealthController?.IsAlive != true)
				continue;

			var distance = Vector3.Distance(localPlayer.Transform.Original.position, player.Transform.Original.position);
			if (Settings.EspDistance.Value > 0f && distance > Settings.EspDistance.Value)
				continue;

			if (!TryGetSimplePlayerBox(player, camera, distance, out var simpleBox))
				continue;

			var detailedRange = distance <= Mathf.Min(
				Settings.EspDistance.Value > 0f ? Settings.EspDistance.Value : 500f,
				500f);
			var detailed = detailedRange &&
				(Settings.XRay.Value ||
				 HasAnyVisibleBodyPoint(localPlayer, player, camera));
			var color = GetPlayerEspColor(player);
			GUI.color = color;

			if (Settings.ShowBoxes.Value)
			{
				var box = detailed && TryGetPlayerBox(player, camera, out var detailedBox)
					? detailedBox
					: simpleBox;
				DrawOutline(box, Settings.BoxThickness.Value);
				GUI.color = Color.white;
				GUI.Label(new Rect(box.x, box.y - 20f, 100f, 20f), $"{distance:0} m");
			}

			if (detailed && Settings.ShowSkeletons.Value)
				DrawSkeleton(player, camera, color, Settings.BoxThickness.Value);
		}
		GUI.color = oldColor;

		if (Settings.LootEspEnabled.Value)
		{
			foreach (var marker in _lootMarkers)
			{
				var screen = WorldToScreen(camera, marker.Position);
				if (screen == Vector2.zero)
					continue;

				var distance = Vector3.Distance(localPlayer.Transform.Original.position, marker.Position);
				if (Settings.LootDistance.Value > 0f &&
					distance > Settings.LootDistance.Value)
				{
					continue;
				}

				var price = marker.Price >= 1000 ? $"  {marker.Price / 1000f:0.#}K" : string.Empty;
				GUI.color = new Color(0.25f, 0.95f, 1f, 0.95f);
				GUI.Label(
					new Rect(screen.x + 8f, screen.y - 8f, 260f, 20f),
					$"{marker.Name}{price}  {distance:0}m");
			}
			GUI.color = oldColor;
		}

		if (Settings.ExfilEspEnabled.Value)
			DrawExfiltrationPoints(world, localPlayer, camera);

		if (Settings.QuestLocations.Value)
		{
			foreach (var marker in _questItemMarkers)
			{
				var screen = WorldToScreen(camera, marker.Position);
				if (screen == Vector2.zero)
					continue;

				var distance = Vector3.Distance(localPlayer.Transform.Original.position, marker.Position);
				GUI.color = new Color(0.25f, 0.95f, 1f, 0.95f);
				GUI.Label(new Rect(screen.x + 8f, screen.y - 8f, 360f, 20f), $"{marker.Name}  {distance:0}m");
			}

			foreach (var marker in _questMarkers)
			{
				var screen = WorldToScreen(camera, marker.Position);
				if (screen == Vector2.zero)
					continue;

				var distance = Vector3.Distance(localPlayer.Transform.Original.position, marker.Position);
				GUI.color = new Color(1f, 0.25f, 0.95f, 0.95f);
				GUI.Label(new Rect(screen.x + 8f, screen.y - 8f, 260f, 20f), $"{marker.Name}  {distance:0}m");
			}
			GUI.color = oldColor;
		}
	}

	private void DrawMapOverlay()
	{
		if (Event.current.type != EventType.Repaint || _mapCamera == null || !_mapCamera.enabled)
			return;

		var world = Singleton<GameWorld>.Instance;
		var localPlayer = world?.MainPlayer;
		if (world == null || localPlayer == null)
			return;

		GUI.color = new Color(0f, 0f, 0f, 0.72f);
		GUI.DrawTexture(
			new Rect(16f, 12f, 760f, Settings.MapTeleportEnabled.Value ? 54f : 32f),
			Texture2D.whiteTexture);
		GUI.color = Color.white;
		GUI.Label(
			new Rect(24f, 17f, 580f, 24f),
			_mapBoundsReady
				? $"全屏地图  全图 {_mapWorldBounds.size.x:0}×{_mapWorldBounds.size.z:0}m ({_mapBoundsSource})  [{Settings.MapKey.Value}] 关闭"
				: $"全屏地图  自动范围  [{Settings.MapKey.Value}] 关闭");
		if (Settings.MapTeleportEnabled.Value)
		{
			GUI.Label(
				new Rect(24f, 39f, 740f, 20f),
				$"{FormatKeys(Settings.MapTeleportMarkerKey.Value)} 中心 / 下一个传送点 · " +
				$"按住方向键连续移动 · {FormatKeys(Settings.MapTeleportExecuteKey.Value)} 传送");
		}

		var localScreen = MapWorldToScreen(localPlayer.Transform.Original.position);
		if (localScreen != Vector2.zero)
		{
			DrawMapPlayerHeading(localPlayer, localScreen);
			GUI.color = Color.white;
			GUI.DrawTexture(new Rect(localScreen.x - 4f, localScreen.y - 4f, 8f, 8f), Texture2D.whiteTexture);
		}

		foreach (var player in world.AllAlivePlayersList)
		{
			if (player == null || player.IsYourPlayer || player.HealthController?.IsAlive != true)
				continue;

			var screen = MapWorldToScreen(player.Transform.Original.position);
			if (screen == Vector2.zero)
				continue;

			GUI.color = GetPlayerEspColor(player);
			GUI.DrawTexture(new Rect(screen.x - 4f, screen.y - 4f, 8f, 8f), Texture2D.whiteTexture);
		}

		foreach (var marker in _lootMarkers)
		{
			var distance = Vector3.Distance(
				localPlayer.Transform.Original.position,
				marker.Position);
			if (Settings.LootDistance.Value > 0f &&
				distance > Settings.LootDistance.Value)
			{
				continue;
			}

			var screen = MapWorldToScreen(marker.Position);
			if (screen == Vector2.zero)
				continue;
			GUI.color = new Color(0.2f, 0.95f, 1f, 0.95f);
			GUI.Label(new Rect(screen.x + 4f, screen.y - 8f, 230f, 18f), marker.Name);
		}

		foreach (var marker in _questItemMarkers)
		{
			var screen = MapWorldToScreen(marker.Position);
			if (screen == Vector2.zero)
				continue;
			GUI.color = new Color(0.2f, 0.95f, 1f, 0.95f);
			GUI.Label(new Rect(screen.x + 4f, screen.y - 8f, 320f, 18f), marker.Name);
		}

		foreach (var marker in _questMarkers)
		{
			var screen = MapWorldToScreen(marker.Position);
			if (screen == Vector2.zero)
				continue;
			GUI.color = new Color(1f, 0.2f, 0.95f, 0.95f);
			GUI.Label(new Rect(screen.x + 4f, screen.y - 8f, 230f, 18f), marker.Name);
		}

		if (Settings.ExfilEspEnabled.Value)
		{
			if (world.ExfiltrationController != null)
			{
				EnsureExfiltrationCache(world, localPlayer);
				foreach (var point in _exfiltrationPoints)
				{
					if (point?.transform == null ||
						!ShouldDrawExfiltrationPoint(point))
						continue;

					var screen = MapWorldToScreen(point.transform.position);
					if (screen == Vector2.zero)
						continue;

					GUI.color = GetExfiltrationPointColor(point);
					GUI.Label(
						new Rect(screen.x + 4f, screen.y - 8f, 250f, 18f),
						_exfiltrationLabels.TryGetValue(point, out var label) ? label : "撤离点");
				}
			}

			if (Settings.ShowTransitPoints.Value)
			{
				EnsureTransitPointCache(world);
				foreach (var point in _transitPoints)
				{
					if (point?.transform == null)
						continue;

					var screen = MapWorldToScreen(point.transform.position);
					if (screen == Vector2.zero)
						continue;

					GUI.color = GetTransitPointColor(point);
					GUI.Label(
						new Rect(screen.x + 4f, screen.y - 8f, 340f, 18f),
						_transitPointLabels.TryGetValue(point, out var label)
							? label
							: "转移点");
				}
			}
		}

		if (Settings.MapTeleportEnabled.Value && _mapTeleportMarker.HasValue)
		{
			var markerScreen = MapWorldToScreen(_mapTeleportMarker.Value);
			if (markerScreen != Vector2.zero)
			{
				GUI.color = new Color(1f, 0.85f, 0.15f, 1f);
				DrawLine(
					markerScreen + new Vector2(-10f, 0f),
					markerScreen + new Vector2(10f, 0f),
					2f,
					GUI.color);
				DrawLine(
					markerScreen + new Vector2(0f, -10f),
					markerScreen + new Vector2(0f, 10f),
					2f,
					GUI.color);
				GUI.Label(
					new Rect(markerScreen.x + 12f, markerScreen.y - 10f, 280f, 22f),
					$"传送标记 ({_mapTeleportMarker.Value.x:0}, {_mapTeleportMarker.Value.z:0})");
			}
		}

		GUI.color = Color.white;
	}

	private void UpdateMapTeleportInput()
	{
		if (_captureAction != null)
			return;

		if (!Settings.MapTeleportEnabled.Value ||
			!Settings.MapEnabled.Value ||
			_mapCamera == null ||
			!_mapCamera.enabled ||
			!_mapBoundsReady)
		{
			_mapTeleportCycleIndex = -1;
			_mapTeleportCyclePlayer = null;
			return;
		}

		var world = Singleton<GameWorld>.Instance;
		var player = world?.MainPlayer;
		if (world == null || player == null)
			return;

		if (_mapTeleportCyclePlayer != player)
		{
			_mapTeleportCyclePlayer = player;
			_mapTeleportCycleIndex = -1;
		}

		if (IsAnyKeyDown(Settings.MapTeleportMarkerKey.Value))
		{
			if (_mapTeleportCycleIndex < 0)
			{
				var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
				if (TryGetMapTeleportTarget(center, player, out var centerTarget))
				{
					_mapTeleportMarker = centerTarget;
					_mapTeleportCycleIndex = 0;
					_lastError =
						$"传送标记已放置在地图中心 ({centerTarget.x:0}, {centerTarget.z:0})；" +
						"再次按同一按键开始遍历撤离点/转移点。";
					_lastErrorTime = Time.time;
				}
			}
			else
				SelectNextTeleportMarker(world, player);
		}

		if (_mapTeleportMarker.HasValue)
		{
			var horizontal = 0f;
			var vertical = 0f;
			if (Input.GetKey(KeyCode.RightArrow))
				horizontal += 1f;
			if (Input.GetKey(KeyCode.LeftArrow))
				horizontal -= 1f;
			if (Input.GetKey(KeyCode.UpArrow))
				vertical += 1f;
			if (Input.GetKey(KeyCode.DownArrow))
				vertical -= 1f;

			if (horizontal != 0f || vertical != 0f)
			{
				var mapRight = _mapCamera.transform.right;
				var mapForward = _mapCamera.transform.up;
				mapRight.y = 0f;
				mapForward.y = 0f;
				var movement =
					mapRight.normalized * horizontal +
					mapForward.normalized * vertical;
				var distance = MapTeleportMarkerSpeed *
					Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f);
				var candidate = _mapTeleportMarker.Value + movement.normalized * distance;
				var candidateScreen = MapWorldToScreen(candidate);
				if (candidateScreen != Vector2.zero &&
					TryGetMapTeleportTarget(candidateScreen, player, out var movedTarget))
				{
					_mapTeleportMarker = movedTarget;
				}
			}
		}

		if (IsAnyKeyDown(Settings.MapTeleportExecuteKey.Value) &&
			_mapTeleportMarker.HasValue)
		{
			ExecuteMapMarkerTeleport(player, _mapTeleportMarker.Value);
		}
	}

	private void SelectNextTeleportMarker(
		GameWorld world,
		Player player)
	{
		EnsureExfiltrationCache(world, player);
		var candidates = new List<(Vector3 Position, string Label)>();
		foreach (var exfiltrationPoint in _exfiltrationPoints)
		{
			if (exfiltrationPoint?.transform == null ||
				exfiltrationPoint.Status == EFT.Interactive.EExfiltrationStatus.NotPresent)
			{
				continue;
			}

			var label = _exfiltrationLabels.TryGetValue(
				exfiltrationPoint,
				out var exfiltrationLabel)
				? exfiltrationLabel
				: "撤离点";
			candidates.Add((exfiltrationPoint.transform.position, label));
		}

		EnsureTransitPointCache(world);
		foreach (var transitPoint in _transitPoints)
		{
			if (transitPoint?.transform == null)
				continue;

			var label = _transitPointLabels.TryGetValue(
				transitPoint,
				out var transitLabel)
				? transitLabel
				: "转移点";
			candidates.Add((transitPoint.transform.position, label));
		}

		if (candidates.Count == 0)
		{
			_mapTeleportCycleIndex = 0;
			_lastError = "当前地图没有可遍历的撤离点或转移点。";
			_lastErrorTime = Time.time;
			return;
		}

		var index = _mapTeleportCycleIndex % candidates.Count;
		if (index < 0)
			index = 0;

		var destination = candidates[index];
		var target = destination.Position;
		var pointScreen = MapWorldToScreen(target);
		if (pointScreen != Vector2.zero &&
			TryGetMapTeleportTarget(pointScreen, player, out var resolvedTarget))
		{
			target = resolvedTarget;
		}

		_mapTeleportMarker = target;
		_mapTeleportCycleIndex = (index + 1) % candidates.Count;
		_lastError =
			$"传送目标 {index + 1}/{candidates.Count}：{destination.Label}；" +
			$"按 {FormatKeys(Settings.MapTeleportExecuteKey.Value)} 传送。";
		_lastErrorTime = Time.time;
	}

	private void ExecuteMapMarkerTeleport(Player player, Vector3 target)
	{
		var previousPosition = player.Transform.Original.position;
		var previousRotation = player.Transform.Original.rotation;
		_mapTeleportOrigin = previousPosition;
		_mapTeleportOriginRotation = previousRotation;
		player.transform.SetPositionAndRotation(target, player.transform.rotation);
		_mapTeleportMarker = previousPosition;
		_lastError =
			$"已传送到 ({target.x:0}, {target.z:0})；标记已切换到传送前位置。";
		_lastErrorTime = Time.time;
	}

	private bool TryGetMapTeleportTarget(
		Vector2 mousePosition,
		Player player,
		out Vector3 target)
	{
		target = default;
		if (_mapCamera == null || !_mapBoundsReady)
			return false;

		var screenPoint = new Vector3(mousePosition.x, Screen.height - mousePosition.y, 0f);
		var ray = _mapCamera.ScreenPointToRay(screenPoint);
		var hits = Physics.RaycastAll(
			ray,
			MapTeleportRayDistance,
			~0,
			QueryTriggerInteraction.Ignore);

		var bestDistance = float.MaxValue;
		var hasTarget = false;
		var bestPoint = default(Vector3);
		var bestNormal = default(Vector3);
		foreach (var hit in hits)
		{
			var collider = hit.collider;
			if (collider == null || collider.GetComponentInParent<Player>() != null)
				continue;

			var candidate = hit.point + hit.normal * 0.15f;
			if (!IsPointInsideMapBounds(candidate))
				continue;

			if (hit.distance >= bestDistance)
				continue;

			bestDistance = hit.distance;
			bestPoint = hit.point;
			bestNormal = hit.normal;
			hasTarget = true;
		}

		if (!hasTarget)
			return false;

		target = bestPoint + bestNormal * 0.15f;
		return true;
	}

	private bool IsPointInsideMapBounds(Vector3 position)
	{
		var minimum = _mapWorldBounds.min;
		var maximum = _mapWorldBounds.max;
		const float margin = 1f;
		return position.x >= minimum.x + margin &&
			position.x <= maximum.x - margin &&
			position.z >= minimum.z + margin &&
			position.z <= maximum.z - margin;
	}

	private void DrawMapPlayerHeading(Player localPlayer, Vector2 origin)
	{
		var forward = Camera.main?.transform.forward ?? localPlayer.Transform.Original.forward;
		forward.y = 0f;

		if (forward.sqrMagnitude < 0.001f)
		{
			forward = localPlayer.Transform.Original.forward;
			forward.y = 0f;
		}
		if (forward.sqrMagnitude < 0.001f)
			return;

		var directionPoint = MapWorldToScreen(
			localPlayer.Transform.Original.position + forward.normalized * 4f);
		if (directionPoint == Vector2.zero)
			return;

		var direction = directionPoint - origin;
		if (direction.sqrMagnitude < 0.01f)
			return;
		direction.Normalize();

		var tip = origin + direction * 34f;
		var perpendicular = new Vector2(-direction.y, direction.x);
		var arrowCenter = tip - direction * 9f;
		var arrowLeft = arrowCenter + perpendicular * 5f;
		var arrowRight = arrowCenter - perpendicular * 5f;

		DrawLine(origin, tip, 2.5f, Color.white);
		DrawLine(tip, arrowLeft, 2.25f, Color.white);
		DrawLine(tip, arrowRight, 2.25f, Color.white);
	}

	private Vector2 MapWorldToScreen(Vector3 world)
	{
		if (_mapCamera == null)
			return Vector2.zero;

		var point = _mapCamera.WorldToScreenPoint(world);
		if (point.z <= 0.01f)
			return Vector2.zero;

		return new Vector2(
			point.x * Screen.width / Mathf.Max(1f, _mapCamera.pixelWidth),
			Screen.height - point.y * Screen.height / Mathf.Max(1f, _mapCamera.pixelHeight));
	}

	private static void DrawSkeleton(Player player, Camera camera, Color color, float thickness)
	{
		var bones = player.PlayerBones;
		if (bones == null)
			return;

		var head = bones.Head.Original.position;
		var chest = bones.Ribcage.Original.position;
		var stomach = bones.Spine1.position;
		var pelvis = bones.Pelvis.Original.position;
		DrawBone(head, chest);
		DrawBone(chest, stomach);
		DrawBone(stomach, pelvis);

		var leftShoulder = bones.LeftShoulder.Original.position;
		var rightShoulder = bones.RightShoulder.Original.position;
		DrawBone(chest, leftShoulder);
		DrawBone(chest, rightShoulder);
		DrawBone(leftShoulder, bones.Upperarms[0].position);
		DrawBone(bones.Upperarms[0].position, bones.Forearms[0].position);
		DrawBone(rightShoulder, bones.Upperarms[1].position);
		DrawBone(bones.Upperarms[1].position, bones.Forearms[1].position);

		DrawBone(pelvis, bones.LeftThigh1.Original.position);
		DrawBone(bones.LeftThigh1.Original.position, bones.LeftThigh2.Original.position);
		DrawBone(pelvis, bones.RightThigh1.Original.position);
		DrawBone(bones.RightThigh1.Original.position, bones.RightThigh2.Original.position);

		void DrawBone(Vector3 fromWorld, Vector3 toWorld)
		{
			var from = WorldToScreen(camera, fromWorld);
			var to = WorldToScreen(camera, toWorld);
			if (from == Vector2.zero || to == Vector2.zero)
				return;

			DrawLine(from, to, thickness, color);
		}
	}

	private static Color GetPlayerEspColor(Player player)
	{
		var info = player.Profile?.Info;
		var roleType = info?.Settings?.Role;
		var role = roleType?.ToString() ?? string.Empty;

		if (roleType == WildSpawnType.shooterBTR ||
			role.Contains("boss", StringComparison.OrdinalIgnoreCase) ||
			role.Contains("knight", StringComparison.OrdinalIgnoreCase) ||
			role.Contains("bigpipe", StringComparison.OrdinalIgnoreCase) ||
			role.Contains("birdEye", StringComparison.OrdinalIgnoreCase))
			return new Color(1f, 0.15f, 0.15f, 0.98f);
		if (role.Contains("sectant", StringComparison.OrdinalIgnoreCase))
			return new Color(0.85f, 0.2f, 1f, 0.98f);
		if (role.Contains("pmcBot", StringComparison.OrdinalIgnoreCase))
			return new Color(0.1f, 0.9f, 1f, 0.98f);
		if (role.Contains("marksman", StringComparison.OrdinalIgnoreCase))
			return new Color(0.55f, 0.2f, 1f, 0.98f);
		if (role.Contains("exUsec", StringComparison.OrdinalIgnoreCase))
			return new Color(0.65f, 0.65f, 0.65f, 0.98f);
		if (role.Contains("pmcBEAR", StringComparison.OrdinalIgnoreCase) || info?.Side == EPlayerSide.Bear)
			return new Color(0.2f, 0.45f, 1f, 0.98f);
		if (role.Contains("pmcUSEC", StringComparison.OrdinalIgnoreCase) || info?.Side == EPlayerSide.Usec)
			return new Color(0.2f, 1f, 0.35f, 0.98f);

		return new Color(1f, 0.9f, 0.1f, 0.98f);
	}

	private static void DrawLine(Vector2 from, Vector2 to, float thickness, Color color)
	{
		var delta = to - from;
		var length = delta.magnitude;
		if (length <= 0.1f)
			return;

		var oldMatrix = GUI.matrix;
		var oldColor = GUI.color;
		GUI.color = color;
		GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
		GUI.DrawTexture(new Rect(from.x, from.y - thickness * 0.5f, length, thickness), Texture2D.whiteTexture);
		GUI.matrix = oldMatrix;
		GUI.color = oldColor;
	}

	private void DrawExfiltrationPoints(GameWorld world, Player localPlayer, Camera camera)
	{
		EnsureExfiltrationCache(world, localPlayer);
		var oldColor = GUI.color;
		foreach (var point in _exfiltrationPoints)
		{
			if (point?.transform == null ||
				!ShouldDrawExfiltrationPoint(point))
				continue;

			var screen = WorldToScreen(camera, point.transform.position);
			if (screen == Vector2.zero)
				continue;

			var distance = Vector3.Distance(localPlayer.Transform.Original.position, point.transform.position);
			var label = _exfiltrationLabels.TryGetValue(point, out var cachedLabel)
				? cachedLabel
				: "撤离点";
			GUI.color = GetExfiltrationPointColor(point);
			GUI.Label(
				new Rect(screen.x - 70f, screen.y - 24f, 240f, 22f),
				$"{label} {distance:0}m");
		}

		if (Settings.ShowTransitPoints.Value)
		{
			EnsureTransitPointCache(world);
			foreach (var point in _transitPoints)
			{
				if (point?.transform == null)
					continue;

				var screen = WorldToScreen(camera, point.transform.position);
				if (screen == Vector2.zero)
					continue;

				var distance = Vector3.Distance(
					localPlayer.Transform.Original.position,
					point.transform.position);
				GUI.color = GetTransitPointColor(point);
				GUI.Label(
					new Rect(screen.x - 90f, screen.y - 24f, 360f, 22f),
					(_transitPointLabels.TryGetValue(point, out var label)
						? label
						: "转移点") +
					$" {distance:0}m");
			}
		}
		GUI.color = oldColor;
	}

	private void EnsureExfiltrationCache(GameWorld world, Player localPlayer)
	{
		if (_exfiltrationCachePlayer == localPlayer &&
			Time.time < _nextExfiltrationRefresh)
			return;

		_exfiltrationPoints.Clear();
		_eligibleExfiltrationPoints.Clear();
		_exfiltrationLabels.Clear();
		var controller = world.ExfiltrationController;
		var profile = localPlayer.Profile;
		if (controller != null && profile?.Info != null)
		{
			if (profile.Info.Side == EPlayerSide.Savage)
			{
				var scavPoints = controller.ScavExfiltrationPoints;
				if (scavPoints != null)
				{
					for (var index = 0; index < scavPoints.Length; index++)
							AddPoint(scavPoints[index]);
				}

				var mask = controller.GetScavExfiltrationMask(profile.Id);
				for (var index = 0;
					scavPoints != null &&
					index < 31 &&
					index < scavPoints.Length;
					index++)
				{
					if ((mask & (1 << index)) != 0)
						_eligibleExfiltrationPoints.Add(scavPoints[index]);
				}
			}
			else
			{
				var points = controller.ExfiltrationPoints;
				if (points != null)
				{
					for (var index = 0; index < points.Length; index++)
						AddPoint(points[index]);
				}

				var eligiblePoints = controller.EligiblePoints(profile);
				if (eligiblePoints != null)
				{
					for (var index = 0; index < eligiblePoints.Length; index++)
						_eligibleExfiltrationPoints.Add(eligiblePoints[index]);
				}
			}

			var secretPoints = controller.SecretExfiltrationPoints;
			if (secretPoints != null)
			{
				for (var index = 0; index < secretPoints.Length; index++)
					AddPoint(secretPoints[index]);
			}

			var eligibleSecretPoints = controller.SecretEligiblePoints();
			if (eligibleSecretPoints != null)
			{
				for (var index = 0; index < eligibleSecretPoints.Length; index++)
					_eligibleExfiltrationPoints.Add(eligibleSecretPoints[index]);
			}
		}

		_exfiltrationCachePlayer = localPlayer;
		_nextExfiltrationRefresh = Time.time + 2f;

		void AddPoint(EFT.Interactive.ExfiltrationPoint? point)
		{
			if (point?.Settings == null)
				return;

			_exfiltrationPoints.Add(point);
			_exfiltrationLabels[point] =
				ResolveLocalizedText(point.Settings.Name, "撤离点");
		}
	}

	private void EnsureTransitPointCache(GameWorld world)
	{
		if (_transitPointCacheWorld == world &&
			Time.time < _nextTransitPointRefresh)
			return;

		if (_transitPointCacheWorld != world || _transitPoints.Count == 0)
		{
			_transitPoints.Clear();
			try
			{
				var points =
					LocationScene.GetAllObjectsAndWhenISayAllIActuallyMeanIt<EFT.Interactive.TransitPoint>();
				if (points != null)
				{
					foreach (var point in points)
					{
						if (point?.parameters != null)
							_transitPoints.Add(point);
					}
				}
			}
			catch (Exception exception)
			{
				TrainerPlugin.Logger.LogWarning(
					"Transit point cache failed: " + exception.Message);
			}
		}

		_transitPointLabels.Clear();
		_transitPointAvailability.Clear();
		foreach (var point in _transitPoints)
		{
			_transitPointLabels[point] = GetTransitPointLabel(point);
			try
			{
				_transitPointAvailability[point] = point.Enabled && point.IsActive;
			}
			catch
			{
				_transitPointAvailability[point] = false;
			}
		}

		_transitPointCacheWorld = world;
		_nextTransitPointRefresh = Time.time + 2f;
	}

	private Color GetExfiltrationPointColor(EFT.Interactive.ExfiltrationPoint point)
	{
		return _eligibleExfiltrationPoints.Contains(point)
			? new Color(0.2f, 1f, 0.35f, 0.95f)
			: new Color(1f, 0.9f, 0.1f, 0.95f);
	}

	private Color GetTransitPointColor(EFT.Interactive.TransitPoint point)
	{
		return _transitPointAvailability.TryGetValue(point, out var available) && available
			? new Color(0.2f, 1f, 0.35f, 0.95f)
			: new Color(1f, 0.9f, 0.1f, 0.95f);
	}

	private bool ShouldDrawExfiltrationPoint(EFT.Interactive.ExfiltrationPoint point)
	{
		return _eligibleExfiltrationPoints.Contains(point) ||
			Settings.ShowUnavailableExfiltrationPoints.Value;
	}

	private static string GetTransitPointLabel(EFT.Interactive.TransitPoint point)
	{
		var parameters = point.parameters;
		if (parameters == null)
			return "转移点";

		string? description = null;
		try
		{
			description = point.Description;
		}
		catch
		{
			// Fall back to the serialized transit parameters.
		}

		foreach (var candidate in new[]
		{
			description,
			parameters.name,
			parameters.description
		})
		{
			var resolved = ResolveLocalizedText(candidate, candidate ?? string.Empty);
			if (IsReadableTransitName(resolved))
				return resolved;
		}

		return "转移点";
	}

	private static bool IsReadableTransitName(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;

		if (value.Length != 24)
			return true;

		return value.Any(character => !Uri.IsHexDigit(character));
	}

	private static string GetExfiltrationStatusText(EFT.Interactive.EExfiltrationStatus status)
	{
		return status switch
		{
			EFT.Interactive.EExfiltrationStatus.NotPresent => "不在地图",
			EFT.Interactive.EExfiltrationStatus.UncompleteRequirements => "条件未满足",
			EFT.Interactive.EExfiltrationStatus.Countdown => "撤离倒计时",
			EFT.Interactive.EExfiltrationStatus.RegularMode => "可撤离",
			EFT.Interactive.EExfiltrationStatus.Pending => "等待中",
			EFT.Interactive.EExfiltrationStatus.AwaitsManualActivation => "需手动开启",
			EFT.Interactive.EExfiltrationStatus.Hidden => "未发现",
			_ => status.ToString()
		};
	}

	private static bool TryGetSimplePlayerBox(
		Player player,
		Camera camera,
		float distance,
		out Rect box)
	{
		box = default;
		var screen = WorldToScreen(camera, player.Transform.Original.position);
		if (screen == Vector2.zero ||
			screen.x < -40f || screen.x > Screen.width + 40f ||
			screen.y < -40f || screen.y > Screen.height + 40f)
			return false;

		var feet = player.Transform.Original.position;
		var headWorld = player.PlayerBones?.Head.Original.position ?? feet + Vector3.up * 1.8f;
		var headScreen = WorldToScreen(camera, headWorld);
		var projectedHeight = headScreen == Vector2.zero
			? 0f
			: Mathf.Abs(screen.y - headScreen.y);
		var height = projectedHeight >= 2f
			? projectedHeight
			: 1800f / Mathf.Max(1f, distance);
		height = Mathf.Clamp(height, 5f, Screen.height * 0.9f);
		var width = height * 0.5f;
		box = new Rect(
			screen.x - width * 0.5f,
			screen.y - height,
			width,
			height);
		return true;
	}

	private static bool TryGetPlayerBox(Player player, Camera camera, out Rect box)
	{
		box = default;
		var min = new Vector2(float.MaxValue, float.MaxValue);
		var max = new Vector2(float.MinValue, float.MinValue);
		var points = 0;

		void Consider(Vector3 position)
		{
			var screen = WorldToScreen(camera, position);
			if (screen == Vector2.zero)
				return;

			min = Vector2.Min(min, screen);
			max = Vector2.Max(max, screen);
			points++;
		}

		var bones = player.PlayerBones;
		if (bones == null)
			return false;

		Consider(bones.Head.Original.position);
		Consider(bones.Ribcage.Original.position);
		Consider(bones.Spine1.position);
		Consider(bones.Pelvis.Original.position);
		Consider(bones.Upperarms[0].position);
		Consider(bones.Upperarms[1].position);
		Consider(bones.LeftThigh2.Original.position);
		Consider(bones.RightThigh2.Original.position);
		Consider(player.Transform.Original.position);

		if (points == 0)
			return false;

		box = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
		var padding = Mathf.Clamp(Settings.BoxPadding.Value, 0f, 0.5f) * Mathf.Min(box.width, box.height);
		box = new Rect(box.x - padding, box.y - padding, box.width + padding * 2f, box.height + padding * 2f);
		return box.width >= 1f && box.height >= 1f;
	}

	private static bool HasAnyVisibleBodyPoint(Player localPlayer, Player targetPlayer, Camera camera)
	{
		var bones = targetPlayer.PlayerBones;
		return bones != null &&
			(HasLineOfSight(localPlayer, targetPlayer, camera, bones.Head.Original) ||
			 HasLineOfSight(localPlayer, targetPlayer, camera, bones.Ribcage.Original) ||
			 HasLineOfSight(localPlayer, targetPlayer, camera, bones.Pelvis.Original));
	}

	private static void UpdateScopeProjection(Camera? mainCamera, Player player)
	{
		_scopeProjectionActive = false;
		if (mainCamera == null)
			return;

		try
		{
			var handsController = player.HandsController;
			var weaponAnimation = player.ProceduralWeaponAnimation;
			var aimingMod = weaponAnimation?.CurrentAimingMod;
			if (handsController?.IsAiming != true ||
				aimingMod == null ||
				aimingMod.ScopesCount <= 0 ||
				aimingMod.GetCurrentOpticZoom() <= 1f)
				return;

			if (_nativeOpticCamera == null)
			{
				_nativeOpticCamera = Camera.allCameras.FirstOrDefault(candidate =>
					candidate != null &&
					candidate.name.Equals(
						"BaseOpticCamera(Clone)",
						StringComparison.OrdinalIgnoreCase));
			}

			if (_nativeOpticCamera == null ||
				!_nativeOpticCamera.gameObject.activeInHierarchy)
				return;

			_scopeProjectionScale =
				Screen.height / Mathf.Max(1f, mainCamera.scaledPixelHeight);
			_scopeProjectionOffset = new Vector2(
				mainCamera.pixelWidth / 2f - _nativeOpticCamera.pixelWidth / 2f,
				mainCamera.pixelHeight / 2f - _nativeOpticCamera.pixelHeight / 2f);
			_scopeProjectionActive = true;
		}
		catch
		{
			_scopeProjectionActive = false;
		}
	}

	private string GetQuestDisplayName(
		Quest quest,
		EFT.Quests.QuestStatusData questData)
	{
		var questId = questData.Id ?? quest.Template?.Id;
		if (!string.IsNullOrWhiteSpace(questId) &&
			_questNameCache.TryGetValue(questId, out var cachedName))
			return cachedName;

		var template = questData.Template ?? quest.Template;
		string?[] candidates =
		[
			template?.NameLocaleKey,
			template?.Name,
			quest.Name
		];

		foreach (var candidate in candidates)
		{
			var resolved = ResolveLocalizedText(candidate, candidate ?? string.Empty);
			if (IsReadableQuestName(resolved, questId, candidate))
				return CacheQuestName(questId, resolved);
		}

		var key = template?.NameLocaleKey;
		if (template?.Localization != null && !string.IsNullOrWhiteSpace(key))
		{
			string[] preferredLanguages = ["ch", "zh-cn", "en", "ge", "ru"];
			foreach (var language in preferredLanguages)
			{
				if (!template.Localization.TryGetValue(language, out var values) ||
					values == null ||
					!values.TryGetValue(key, out var localized) ||
					!IsReadableQuestName(localized, questId, key))
					continue;

				return CacheQuestName(questId, localized);
			}

			foreach (var language in template.Localization)
			{
				if (language.Value == null ||
					!language.Value.TryGetValue(key, out var localized) ||
					!IsReadableQuestName(localized, questId, key))
					continue;

				return CacheQuestName(questId, localized);
			}
		}

		return "未命名任务";
	}

	private string CacheQuestName(string? questId, string value)
	{
		var displayName = value.Trim();
		if (!string.IsNullOrWhiteSpace(questId))
			_questNameCache[questId] = displayName;
		return displayName;
	}

	private static bool IsReadableQuestName(
		string? value,
		string? questId,
		string? localizationKey)
	{
		if (string.IsNullOrWhiteSpace(value))
			return false;

		var display = value.Trim();
		if (!string.IsNullOrWhiteSpace(questId) &&
			(display.Equals(questId, StringComparison.OrdinalIgnoreCase) ||
				display.StartsWith(questId + " ", StringComparison.OrdinalIgnoreCase)))
			return false;

		if (!string.IsNullOrWhiteSpace(localizationKey) &&
			display.Equals(localizationKey, StringComparison.OrdinalIgnoreCase))
			return false;

		if (display.Length != 24)
			return true;

		foreach (var character in display)
		{
			if (!Uri.IsHexDigit(character))
				return true;
		}

		return false;
	}

	private static string ResolveLocalizedText(string? localizationKey, string fallback)
	{
		if (string.IsNullOrWhiteSpace(localizationKey))
			return fallback;

		try
		{
			var manager = LocalizationManager.Instance;
			if (manager != null && manager.HasValidLocalization(localizationKey))
			{
				var localized = manager.LocalizedValue(localizationKey);
				if (!string.IsNullOrWhiteSpace(localized))
					return localized;
			}
		}
		catch
		{
			// Fall through to a readable label.
		}

		return fallback;
	}

	private static string GetLocalizedItemName(EFT.InventoryLogic.Item item)
	{
		try
		{
			var localized = item.LocalizedShortName();
			if (!string.IsNullOrWhiteSpace(localized) &&
				!localized.Equals(item.TemplateId.ToString(), StringComparison.OrdinalIgnoreCase))
				return localized;
		}
		catch
		{
			// Try the full item name below.
		}

		try
		{
			var localized = item.LocalizedName();
			if (!string.IsNullOrWhiteSpace(localized) &&
				!localized.Equals(item.TemplateId.ToString(), StringComparison.OrdinalIgnoreCase))
				return localized;
		}
		catch
		{
			// Fall back to the item instance fields below.
		}

		if (!string.IsNullOrWhiteSpace(item.ShortName))
			return item.ShortName;
		if (!string.IsNullOrWhiteSpace(item.Name))
			return item.Name;
		return "未知物品";
	}

	private static Vector2 WorldToScreen(Camera camera, Vector3 world)
	{
		var projectionCamera = _scopeProjectionActive && _nativeOpticCamera != null
			? _nativeOpticCamera
			: camera;
		var point = projectionCamera.WorldToScreenPoint(world);
		if (point.z <= 0.01f)
			return Vector2.zero;

		if (_scopeProjectionActive)
		{
			var scopePoint = (Vector2)point + _scopeProjectionOffset;
			return new Vector2(
				scopePoint.x * _scopeProjectionScale,
				Screen.height - scopePoint.y * _scopeProjectionScale);
		}

		return new Vector2(
			point.x * Screen.width / Mathf.Max(1f, camera.pixelWidth),
			Screen.height - point.y * Screen.height / Mathf.Max(1f, camera.pixelHeight));
	}

	private static void DrawOutline(Rect rect, float thickness)
	{
		GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
		GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
	}

	private int GetSelectedTargetPartIndex()
	{
		return Math.Max(0, Array.FindIndex(TargetParts, part =>
			part.Equals(Settings.TargetPart.Value, StringComparison.OrdinalIgnoreCase)));
	}

	private static AimPart ParseTargetPart(string value)
	{
		return Enum.TryParse<AimPart>(value, ignoreCase: true, out var part) ? part : AimPart.Head;
	}

	private static KeyCode ParseKey(string value, KeyCode fallback)
	{
		return Enum.TryParse<KeyCode>(value, ignoreCase: true, out var key) ? key : fallback;
	}

	private void ReportError(Exception exception)
	{
		var message = exception.GetType().Name + ": " + exception.Message;
		if (message == _lastError && Time.time - _lastErrorTime < 5f)
			return;

		_lastError = message;
		_lastErrorTime = Time.time;
		TrainerPlugin.Logger.LogError(exception);
	}

	private enum AimPart
	{
		Head,
		Chest,
		Stomach,
		Arms,
		Legs
	}

	private sealed record LootMarker(
		string Name,
		Vector3 Position,
		int Price,
		EFT.Interactive.LootItem? Source = null,
		Item? RootItem = null,
		string Key = "");
	private sealed record CachedLootContainer(Item RootItem, Transform Transform);

	private sealed class GUIEnabledScope : IDisposable
	{
		private readonly bool _previous;

		public GUIEnabledScope(bool enabled)
		{
			_previous = GUI.enabled;
			GUI.enabled = enabled;
		}

		public void Dispose()
		{
			GUI.enabled = _previous;
		}
	}
}
