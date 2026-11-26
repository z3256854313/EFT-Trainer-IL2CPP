# Trainer IL2CPP 1.18.0

## 当前安装

插件文件：

`<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP\TrainerIL2CPP.dll`

SHA-256：

`4B8594D8A0240BAF13B30850055F5ECE4EDCFE78B27F2D0FE649F37D6CB7A78E`

## 1.18.0 构建产物

文件：

`vendor\EscapeFromTarkov-Trainer\IL2CPPPlugin\bin\Release\net10.0\TrainerIL2CPP.dll`

SHA-256：

`4B8594D8A0240BAF13B30850055F5ECE4EDCFE78B27F2D0FE649F37D6CB7A78E`

## 空间音频崩溃保护

- 仅当 `lib_burst_generated.dll` 的 SHA-256 为
  `D9680179B1C43BD51B3A2BEE07BB55C53972E7EA69632B5A797C92CED84E92BD`
  且目标机器码与预期一致时启用。
- 在运行内存中拦截空间音频命中任务的无效索引 `0xFFFFFFFF`，跳过该条无效命中后继续处理其他命中。
- 不修改磁盘上的游戏 DLL；版本或机器码不匹配时只记录错误，不执行补丁。

## 游戏内面板

默认快捷键：

`Right Alt`

面板支持：

- 锁定开关
- 头、胸、腹部、胳膊、腿
- 头 > 胸 > 当前部位优先级
- 只锁定可见部位
- 平滑度、锁定距离、FOV 半径和按住锁定键
- 玩家 ESP、方框、隔墙显示、边框粗细、内边距和距离
- 完全锁血与阈值锁血两种模式，阈值可按七个身体部位分别设置
- 分类负面状态清理：能量类、负面 Buff 类、肢体伤害类
- 水分能量和坠落伤害保护
- 无限耐力
- 武器故障保护
- 保存位置和传送
- 夜视、防闪光和去面罩遮挡
- 正午/午夜、晴朗天气和火车召唤
- 可筛选战利品 ESP、估价和距离
- 尸体、任务物品、容器与 stash 标记
- 撤离点全集名称和距离，包括条件未满足、停用、Scav、秘密及付费撤离点
- 地图转移点名称和距离
- BTR 司机按 Boss 红色渲染
- 一键或快捷键解锁玩家约 2.24 米内的锁门
- 玩家骨架 ESP
- 按 Boss、邪教徒、Raider、Marksman、Rogue、BEAR、USEC 和 Scav 分类的玩家颜色
- 仅显示进行中任务、未完成条件的踩点和放置地点标记
- 当前进行中任务的物品精确筛选
- 战斗、任务、潜行三套一键预设
- 游戏内面板中文界面
- 游戏控制台 `trainer` 命令
- 全屏俯视地图，支持玩家朝向、阵营颜色、战利品、任务地点和撤离点
- 地图缩小倍率滑条，默认 `1.15×`
- 地图传送遍历同时包含撤离点和转移点
- 阵营颜色人体材质着色，关闭时恢复原始颜色
- 空投触发
- 760×760 的六页式游戏内面板
- 手动捕获式快捷键绑定，每个功能最多三个键
- 右键物品原生一级 Get Item 按钮，点击复制 `spt give <模板ID>`、任务跳过按钮、跳蚤市场解锁
- 任务目标级跳过按钮，适配自 Terkoiz.Skipper 的任务条件推进方式
- 面板快捷键可在游戏内修改
- 保存面板设置

设置保存在：

`<EFT_ROOT>\BepInEx\config\com.local.efttrainer.il2cpp.cfg`

首次成功加载插件时会自动创建。

## 验证

- 已使用本机 EFT 1.3 IL2CPP Interop 程序集编译通过，0 警告、0 错误。
- 锁血已移除每 0.2 秒调用 `RestoreFullHealth()` 的自动回血逻辑。
- 锁血通过 Harmony 在本机玩家 `ActiveHealthController.ApplyDamage` 执行前拦截伤害。
- `NetworkSyncApplyDamage` 和负向 `ChangeHealth` 作为额外兜底同样被拦截。
- 开启锁血时保留当前生命值，不会先把生命打掉再补满。
- 完全锁血模式保留当前行为；阈值锁血模式允许受击、流血和持续伤害正常扣除，直到对应身体部位达到自定义最低血量。
- 阈值判断直接修改 `ApplyDamage`、`NetworkSyncApplyDamage` 和负向 `ChangeHealth` 的传入伤害值，不会先扣穿阈值再补血。
- 各身体部位的最大生命值从游戏 `GetBodyPartHealth` 每秒缓存；面板为头、胸、腹、左右臂和左右腿分别提供最低血量滑条。
- 负面状态通过游戏原生 `ResidueEffectsWhere` 分类清理；能量类、负面 Buff 类和肢体伤害类可以独立开关。
- 负面 Buff 类别只处理明确列出的负面效果，正面药物、兴奋剂和恢复类 Buff 保留。
- 任务地点只读取状态为 `Started` 的任务以及 `AvailableForFinish` 条件。
- 任务地点通过 `ConditionZone`、`ConditionInZone`、`ConditionVisitPlace` 和 `ConditionLocationTrigger` 的触发 ID 建白名单。
- 白名单递归进入 `ConditionCounterCreator` 和子条件，已完成的踩点、放置条件不会生成标记。
- 任务地点标签按“任务名 · 踩点/放置”显示；同一地点被多个进行中任务使用时合并任务名。
- 任务物品已从战利品 ESP 迁入“任务地点 / 任务物品”功能，关闭战利品 ESP 后仍独立显示。
- 散落任务物品标签按“物品名 · 任务名 · 任务物品”显示，容器任务物品标记也由任务功能控制。
- 任务名优先通过 `QuestTemplate.NameLocaleKey` 和任务本地化字典解析，不再使用可能返回 ID 的 `Quest.Name` 作为主要来源。
- 24 位 MongoID 和 `<ID> name` 模板键会被拒绝；无名称数据的任务显示“未命名任务”。
- 高倍镜 ESP 改用 `OpticCameraManager.Camera` 投影，并通过主相机尺寸和中心偏移映射到屏幕。
- 普通视角的 X/Y 使用统一高度比例，修复开镜移动时方框方向相反和移动速度被额外放大的问题。
- 高倍镜标记中心改为实际 `OpticSight.LensRenderer` 的屏幕位置，不再使用错误的相机宽度偏移，修复开镜后整体向左平移。
- 锁定移动目标时使用 `Player.Velocity`、当前弹药 `InitialSpeed` 和枪口迭代计算三维拦截点。
- 新增“移动目标提前量”系数，默认 `1.00x`，可在面板中调整到 `2.50x`，设为 `0` 关闭。
- 高倍镜投影不再重复扣除镜内相机中心，修复所有标记固定向左平移半个镜片的问题。
- 任务触发器按地图缓存，任务物品和任务容器在战利品缓存刷新时预筛选，避免每秒全场景扫描造成卡顿。
- 任务显示名已缓存，避免每秒重复执行本地化查找。
- 全屏地图边界按 `GameWorld.BorderZones` → 已加载 `LocationScene.BorderZones` → `LevelBorder.Points` 顺序自动计算并居中。
- `LevelBorder.Points` 会通过边界对象的 Transform 转成世界坐标；地图场景加载较慢时最多每秒重试一次，不会每帧执行全场景搜索。
- 地图标题会显示实际边界来源和完整宽深；日志会记录 `Map bounds ready from ...`，边界读取失败时记录一次警告。
- 地图传送改为键盘标记模式：地图打开后第一次按 `[` 在全图屏幕中心放置传送标记，之后每次按 `[` 依次切换到当前 PMC/Scav 阵营的下一个撤离点或转移点。
- 撤离点遍历只跳过状态为 `NotPresent` 的点，包含条件未满足、付费、手动、Scav 和秘密撤离点；方向键仍可连续微调，`]` 执行传送。
- 地图关闭后重新打开会重置传送目标遍历；执行传送后，下一次按 `[` 继续选择列表中的下一个目标。
- 地图相机的正交尺寸会乘以 `Map.ZoomOut`，面板滑条范围为 `1.00×` 到 `2.50×`，默认 `1.15×`。
- 传送标记通过正交地图相机向地面或建筑表面发射物理射线，忽略触发器和玩家碰撞体，并限制在地图边界内。
- 每次执行传送都会保存传送前位置，并把标记切换到该位置；连续按传送键可在最新两处位置间往返。
- 旧 `Ctrl + 左键` 地图传送输入已停用；“世界 / 地图”页仍保留返回上次传送前位置按钮。
- 地图点击传送开启时，地图打开期间会临时解锁并显示鼠标；关闭地图或关闭该功能后恢复游戏原来的鼠标锁定和可见状态。
- 剧情任务章节现在会沿 `ConditionQuest` 追踪当前已经接取且未完成的子任务分支；子任务中的踩点和放置目标会使用剧情章节名，并追加“剧情”分类。
- 剧情任务物品也使用剧情章节名标签，例如 `物品名 · 门票 · 剧情 · 任务物品`。
- 已完成剧情章节，以及剧情链中尚未接取或已完成的未来分支，不会重新生成地图标记。
- “战利品 / 任务”页会显示当前识别到的进行中剧情任务名称，便于确认任务状态是否读取成功。
- 任务标记按“普通任务”和“剧情任务”拆分为两个独立开关；每个模块同时控制自己的任务位置和任务物品。
- 青色始终仅表示任务物品，紫色始终仅表示踩点、放置和任务位置；普通/剧情来源通过模块开关控制。
- 同一任务物品或地点同时被普通任务和剧情任务使用时，会分别记录来源，只要任一启用模块需要就会显示。
- 容器任务物品改为使用 `GetAllItems()` 完整遍历容器树，不再只检查 `GetAllVisibleItems()` 中已经搜索或当前可见的物品；未搜索容器中的任务物品也会生成标记。
- 活动任务过滤开启时，青色标记不再要求物品自身的 `QuestItem=true`；只要模板 ID 属于当前未完成普通或剧情任务要求，普通 FIR 物品和容器内物品也会标记。
- 高倍镜简化玩家框不再使用不包含镜头倍率的 `1800 / 距离` 高度公式，而是投影目标头顶和脚部两个世界点，计算镜内真实屏幕高度。
- 因此长距离目标即使使用简化框，开镜后的框宽和框高也会随瞄准镜倍率同步放大，并继续使用原有正确的位置映射。
- Get Item 点击行为从复制命令改为直接向 Commando 对话发送 `spt give <模板ID> <数量>`。
- Get Item 现在只在对局外显示；存在活动 `GameWorld` 和本地玩家时，一级按钮、悬停子菜单与最终发送命令都会被拒绝。
- `Item.QuestItem` 或物品模板 `QuestItem` 为真的任务/剧情专用道具不显示 Get Item 选项。
- 原生右键菜单提供 `Get Item`、`Get Item 3`、`Get Item 5`、`Get Item 10`、`Get Item 200` 和 `Get Item 10000` 六个独立按钮。
- 每个按钮使用独立菜单标识和数量参数，不会因为复用回调而发送错误数量。
- 缺失的数量按钮改为每帧最多创建一个，通过连续几帧完成全部六个按钮，避免按钮池在同一 OnGUI 调用中连续实例化造成菜单重建异常。
- Get Item 按钮现在把当前右键物品的实例 ID 编入按钮名称；切换到另一件物品时隐藏上一件物品的按钮并为当前物品重新创建回调，避免原生按钮池继续调用旧物品闭包。
- 修正插件显示版本常量，日志和面板现在会与实际构建版本一致。
- Get Item 改为原生一级菜单项，悬浮后显示独立二级数量菜单：`1 / 3 / 5 / 10 / 100 / 10000 / 50000 / 1000000`。
- 二级菜单改为原生 `ContextInteractions` 方案：使用游戏自带的 `BaseEmptyContextInteractions` 作为纯动态交互容器，注入八个数量交互，再由一级 `Get Item` 调用原生 `SetSubInteractions`。
- `BaseEmptyContextInteractions` 不包含当前物品的原生操作项，避免二级菜单里拼接出普通一级菜单内容。
- 按原生 `CreateInternalContextButton` 顺序补上 `InteractionButtonsContainer.BindButton`：创建原生按钮后必须由容器绑定指针和布局事件，否则首次打开可能不显示，子菜单也可能在指针进入后立即关闭。
- `Get Item` 在 `ItemUiContext.GetItemContextInteractions` 返回前注册为原生 `DynamicContextInteraction`，由原生菜单生成过程直接创建一级按钮。
- `InteractionButtonsContainer.CreateDynamicContextButton` 后置记录游戏实际创建的 `Get Item` 按钮对象；悬浮与点击按对象身份绑定，不依赖 GameObject 名称猜测。
- 不再在菜单生成后向 `_buttonsContainer` 追加对象，也不再通过 `Update`、`SetActive`、`SetAsLastSibling` 或布局重建维护按钮。
- 原生动态交互闭包绑定当前 `ItemContext.Item`，不会复用其他物品的回调。
- `SimpleContextMenuButton.OnPointerEnter` 检测原生的 `Get Item` 动态按钮，并调用原生 `SetSubInteractions` 显示数量菜单。
- 二级数量菜单继续使用 `BaseEmptyContextInteractions`，其创建、定位和关闭由游戏原生代码管理。
- 不再强制调用 `LayoutRebuilder.ForceRebuildLayoutImmediate`；布局交给原生容器在下一帧正常刷新，避免首次右键顶部原生项目被裁剪。
- 启动日志列出实际成功安装的原生菜单 Harmony 补丁，右键后日志记录原生钩子收到的物品。
- 按钮无法注入时按物品记录一次明确诊断，列出 `menu/buttons/container/template/activeInHierarchy` 状态。
- 同一物品、同一原生容器和同一一级按钮已经拥有激活子菜单时，重复的悬浮或点击回调直接复用现有子菜单，不再先关闭再重建。
- `OnGUI` 只保留菜单已经打开后的兜底注入；当前物品上下文暂时不可用时不再主动关闭由原生容器的子菜单。
- 原生 `SetSubInteractions` 负责子菜单实例化、显示、定位、背景、指针事件和关闭生命周期，不再手动实例化模板或直接塞按钮。
- 为 `SimpleContextMenuButton.OnPointerEnter` 增加 Harmony 后置钩子；原生 `_onHover` 未触发时，会为当前物品的 Get Item 一级按钮补触发子菜单，并按帧去重。
- 子菜单模板不可用或创建成功时都会写入明确日志，便于区分悬浮事件和模板初始化问题。
- 发送复用游戏已有的 `SocialNetwork.SendMessage`，无需打开聊天窗口；目标对话 ID 为 SPT 的 Commando `6723fd51c5924c57ce0ca01e`。
- SocialNetwork 或 Commando 对话尚未初始化时会自动回退到剪贴板，并复制包含数量 `1` 的完整命令。
- 正交相机同时按照地图宽度、深度和屏幕宽高比选择缩放，打开地图即显示全图；旧范围参数仅作无边界数据时的兜底。
- 任务地点不再每秒轮询：订阅任务 `OnConditionChanged` 和 `OnStatusChanged`，任务变化时立即重建一次标记。
- 任务地点的定时刷新只保留 15 秒低频兜底；任务物品候选扫描周期改为 10 秒，并支持任务事件立即刷新。
- 武器故障模板只在换枪/切换功能时处理并恢复；生存效果刷新降为 1 秒。
- 战局开始时对完整战利品和容器列表执行一次全量扫描，缓存所有对象并生成匹配标记；30 米空间网格只用于后续刷新玩家附近对象状态。
- 任务物品初始标记按 `QuestDistance` 全量生成，不再被 30 米附近查询半径截断；任务变化时重建，平时只刷新玩家 30 米内的失效状态。
- 战利品显示范围滑条只过滤屏幕和地图上的标记绘制，0 表示不限；不会删除远处缓存标记，也不会改变 30 米附近状态刷新。
- 地图容器缓存改为遍历 `GameWorld.LootItems` 中根物品为容器的世界对象；不再读取全部 `GameWorld.ItemOwners`，避免把 Profile 中的仓库和默认物品栏误标为地图容器。
- 任务物品优先遍历游戏专用的 `GameWorld.QuestItemsList`，仅在专用集合不可用时回退到完整战利品缓存；重建日志记录专用物品数、活动任务模板数和最终标记数。
- 撤离点和转移点统一使用状态颜色：当前可用为绿色，条件未满足、停用或其他不可用状态为黄色。
- 撤离点按 PMC/Scav 官方全集分别读取，并单独缓存 `EligiblePoints` 或 Scav 撤离位掩码结果。
- 转移点按 `Enabled` 和 `IsActive` 缓存可用状态，颜色状态与名称标签同步每 2 秒刷新。
- 撤离点和转移点标签只显示名称，不再追加 `PMC/Scav/秘密/转移`、状态或目标地点 ID。
- 转移点名称优先使用 `TransitPoint.Description`，随后尝试 `parameters.name` 和 `parameters.description`，并拒绝无法读取的 24 位纯 ID。
- 锁血补丁只对原生对象指针与 `GameWorld.MainPlayer` 完全相同的 HealthController 生效，不再依赖 `IsYourPlayer` 或字符串身份比较；其他玩家和全部 AI 的伤害路径直接放行。
- 锁血关闭时会从 Harmony 真正移除 `ApplyDamage`、`NetworkSyncApplyDamage` 和 `ChangeHealth` 补丁，开启时再重新安装。
- 修正负向 `ChangeHealth` 命中锁血条件后未把扣血量清零的问题。
- 转移点通过 `LocationScene.GetAllObjectsAndWhenISayAllIActuallyMeanIt<TransitPoint>()` 按战局世界缓存；仅在地图切换或 2 秒状态刷新时读取游戏属性。
- BTR 司机使用精确角色枚举 `WildSpawnType.shooterBTR` 判断并归入 Boss 红色，不再落入默认 Scav 黄色。
- 开锁功能对照 EFT Trainer `WorldInteractiveObjects`：一次性读取世界交互对象，只把玩家距离平方不超过 `5`（约 2.24 米）且状态为 `EDoorState.Locked` 的门改为 `EDoorState.Shut`。
- 开锁只在点击按钮或按下快捷键时执行，没有每帧 `LocationScene.GetAllObjects` 扫描；默认快捷键为小键盘 `.`，可在快捷键页重新绑定。
- 锁定先做 500 米和屏幕 FOV 过滤，再做物理可见性检测。
- ESP 对不可见或超过 500 米的目标使用廉价屏幕框；近距可见目标才计算完整 9 点包围框和骨架。
- 瞄准镜投影恢复为仓库原有 ESP 算法：高倍镜固定使用 `BaseOpticCamera(Clone)`，主相机负责屏幕比例和相机尺寸偏移；普通视角直接使用主相机。
- Get Item 只保留一个原生一级菜单按钮，不再创建或绑定二级菜单。
- 一级按钮通过 `InteractionButtonsContainer.CreateContextButton` 创建，每次显示时移动到菜单末尾。
- 点击回调直接写入 `GUIUtility.systemCopyBuffer`，内容严格为 `spt give <模板ID>`，不带数量。
- 已删除旧的悬浮检测、数量菜单和 SPT 聊天投递代码。
- 武器等物品自带的“从预设加载”和“添加到愿望单”二级菜单不再被插件修改。
- 已安装到 BepInEx。
- 直接启动 `EscapeFromTarkov.exe` 的测试没有进入 Unity 初始化阶段，因此 BepInEx 尚未执行插件加载。
- 请通过正常的 SPT 启动流程进入游戏，然后按 `Right Alt`。

加载成功时，在以下文件搜索 `EFT Trainer IL2CPP`：

`<EFT_ROOT>\BepInEx\LogOutput.log`

插件异常会写入：

`<EFT_ROOT>\BepInEx\ErrorLog.log`

## 卸载

删除：

`<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP`

可选删除配置文件：

`<EFT_ROOT>\BepInEx\config\com.local.efttrainer.il2cpp.cfg`

## 当前范围

锁血只作用于本地玩家自己的 `ActiveHealthController`。AI、`IsAI` 控制器和非本地控制器均直接放行伤害。

1.4.0 已接通中文多页游戏内面板、手动多键绑定、Skipper 式任务目标跳过、便捷物品与任务操作、锁定、玩家/骨架/战利品/尸体/任务物品/容器/撤离点/任务地点 ESP、阵营颜色、人体着色、任务过滤、预设、控制台命令、全屏地图、空投、基础生存、传送、视觉和基础世界操作。

