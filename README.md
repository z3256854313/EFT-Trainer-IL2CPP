# EFT Trainer IL2CPP

适用于自建离线 SPT / EFT 环境的 BepInEx IL2CPP 插件，当前版本为
`1.18.0`。项目源自
[sailro/EscapeFromTarkov-Trainer](https://github.com/sailro/EscapeFromTarkov-Trainer)，
但本仓库的当前插件、中文面板和功能集已与旧版托管训练器不同，不能把旧版
功能列表直接套用到本版本。

> 仅在自建离线 SPT 环境中使用。不要在官方在线服务器使用修改后的客户端；
> 这可能违反游戏规则并导致封禁。

## 当前状态

- 插件名：`EFT Trainer IL2CPP`
- 插件版本：`1.18.0`
- 目标环境：与本插件兼容的 EFT 1.3 IL2CPP + BepInEx IL2CPP
- 面板：中文七页式游戏内界面
- 构建验证：使用本机 EFT 1.3 Interop 程序集构建通过，0 警告、0 错误

仓库内还保留了上游安装器、旧托管项目和配置编辑器源码，但当前游戏内插件是
[`IL2CPPPlugin`](IL2CPPPlugin/) 目录。旧项目不能直接加载到 EFT 1.x IL2CPP。

## 安装

1. 安装与 EFT / SPT 版本匹配的 BepInEx IL2CPP。
2. 至少通过 SPT 启动一次游戏，确保 BepInEx 已生成 Interop 程序集。
3. 完全退出游戏、SPT Server 和启动器。
4. 创建目录并复制 DLL：

   ```text
   <EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP\TrainerIL2CPP.dll
   ```

5. 通过正常的 SPT 启动流程进入游戏。
6. 默认按 `Right Alt` 呼出面板。

首次成功加载后会自动创建配置：

```text
<EFT_ROOT>\BepInEx\config\com.local.efttrainer.il2cpp.cfg
```

## 游戏内功能

面板共七页：主控、快捷键、锁定、玩家 ESP、战利品 / 任务、生存 / 视觉、
世界 / 地图。

### 锁定

- 开关锁定，默认按住 `/` 生效。
- 目标部位可选头、胸、腹部、胳膊、腿。
- 支持“头 > 胸 > 当前部位”优先顺序。
- 可限制只锁定射线可见的部位。
- 可调平滑度、移动目标提前量、最大锁定距离和屏幕 FOV 半径。

### 玩家 ESP

- 玩家方框、骨架和阵营颜色。
- 可隔墙显示，也可关闭透视只显示可见目标。
- 可调整方框粗细、内边距和最大距离。
- 人体材质按阵营着色，关闭后恢复原始材质。
- 显示撤离点、条件未满足的撤离点和地图转移点。
- 颜色区分 Boss、邪教徒、Raider、Marksman、Rogue、BEAR、USEC 和 Scav；
  BTR 司机按 Boss 红色显示。

### 战利品与任务

- 按物品名称片段或模板 ID 筛选战利品，可显示估价。
- 显示尸体、可搜索容器和地图中的战利品。
- 可显示普通任务和剧情任务的踩点、放置地点及任务物品。
- 支持只显示当前进行中任务需要的物品。
- 可显示未搜索容器中的相关任务物品。
- 任务地点使用紫色，任务物品使用青色；普通任务和剧情任务可独立开关。

### 生存与视觉

- 两种锁血模式：
  - 完全锁血：保留当前生命值，拦截本地玩家受到的伤害。
  - 阈值锁血：允许正常扣血、流血和持续伤害，直到七个身体部位分别达到设定下限。
- 水分和能量保持满值。
- 可分别清理能量类负面状态、负面 Buff 和肢体伤害效果，保留正面药物效果。
- 防止坠落伤害、无限耐力、武器无故障。
- 保存当前位置并传送。
- 夜视、防闪光 / 防眼部灼伤、去面罩遮挡。

锁血只作用于本地玩家自己的 `ActiveHealthController`，不会拦截 AI 或其他玩家。

### 世界与地图

- `M` 打开全屏俯视地图，默认缩小倍率为 `1.15×`，可调至 `2.50×`。
- 地图显示玩家朝向、阵营颜色、战利品、任务地点、撤离点和转移点。
- 地图标记模式：
  - `[`：第一次将标记放在地图中心，之后依次切换撤离点和转移点。
  - 方向键：连续移动标记。
  - `]`：传送到当前标记。
- 可返回上一次地图传送前的位置。
- 提供正午、午夜、晴朗天气、召唤火车和空投按钮。
- 默认按小键盘 `.` 解锁本地玩家约 2.24 米内的锁门。

### 便利功能

- 原生右键菜单 `Get Item`，仅在对局外且 SocialNetwork 已初始化时可用。
- Get Item 子菜单提供数量：
  `1 / 3 / 5 / 10 / 100 / 10000 / 50000 / 1000000`。
- 点击数量后向 SPT Commando 发送 `spt give <模板ID> <数量>`。
- Commando 不可用时会把完整命令复制到剪贴板。
- 任务和剧情专用物品不显示 Get Item。
- 任务详情提供跳过并领取奖励按钮。
- 可解锁跳蚤市场。
- 提供战斗、任务和潜行三套一键预设。

## 快捷键

默认快捷键如下，均可在“快捷键”页重新绑定：

| 功能 | 默认按键 |
|---|---|
| 呼出面板 | `Right Alt` |
| 按住锁定 | `/` |
| 全屏地图 | `M` |
| 地图标记 / 下一个传送点 | `[` |
| 执行地图传送 | `]` |
| 解锁附近锁门 | 小键盘 `.` |

玩家 ESP、战利品 ESP、任务地点、锁血和夜视也可以绑定快捷键；每个功能最多
绑定三个键。

游戏控制台支持：

```text
trainer status
trainer combat
trainer quest
trainer stealth
trainer save
trainer panel
```

## 未包含的旧版功能

当前 IL2CPP 版本不包含旧托管训练器中的以下功能：

- 无后坐力、无武器摆动、无限弹药、武器强制全自动、最大耐久。
- 穿墙射击、快速投掷、命中标记、自定义准星、热成像。
- 小地图雷达、指南针、HUD 和坐标。
- 无碰撞、自定义移动速度、自由镜头和扩大交互距离。
- 迫击炮打击、生成 Bot、生成任务物品、生成藏身处物品、生成任意物品。
- 全物品已检视、玩家血量 / 护甲详细信息。

旧版功能清单仅作为上游资料保存在
[docs/UPSTREAM-README.md](docs/UPSTREAM-README.md)，不代表当前插件状态。

## 构建

仓库不包含 EFT 游戏文件、BepInEx Interop 程序集或反编译游戏源码。需要安装
.NET 10 SDK，并把 `EFT_ROOT` 指向已经由 BepInEx 生成 Interop 程序集的
EFT 安装目录：

```powershell
$env:EFT_ROOT = "E:\EFT"
dotnet build .\IL2CPPPlugin\TrainerIL2CPP.csproj -c Release
```

输出文件：

```text
IL2CPPPlugin\bin\Release\net10.0\TrainerIL2CPP.dll
```

## 日志与排错

加载成功时，在日志中搜索 `EFT Trainer IL2CPP`：

```text
<EFT_ROOT>\BepInEx\LogOutput.log
```

插件异常：

```text
<EFT_ROOT>\BepInEx\ErrorLog.log
```

如果插件不加载，请先确认 EFT、SPT、BepInEx 和 Interop 程序集版本一致，再检查
`BepInEx\LogOutput.log`。替换 DLL 后必须完全重启游戏。

## 卸载

删除：

```text
<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP\
```

如需清除配置，再删除：

```text
<EFT_ROOT>\BepInEx\config\com.local.efttrainer.il2cpp.cfg
```

## 许可证与致谢

原始项目采用 MIT License，详见 [LICENSE](LICENSE)。上游版权和署名归
Sebastien Lebreton 及原项目贡献者所有。

详细变更和实现说明：

- [IL2CPP 插件说明](docs/IL2CPP-PLUGIN.md)
- [锁定与 ESP 修改](docs/AIM-ESP-CHANGES.md)
- [功能范围](docs/FEATURE-SCOPE.md)
- [配置编辑器](docs/TRAINER-EDITOR.md)
- [安装器修改](docs/INSTALLER-MODIFICATIONS.md)
