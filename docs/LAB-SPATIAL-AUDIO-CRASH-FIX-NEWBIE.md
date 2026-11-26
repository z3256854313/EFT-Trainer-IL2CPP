# 【新手教程】修复离线版实验室进入闪退（EFT 1.3.0.1.48558 / SPT 5.0.0）

> 适用版本：EFT `1.3.0.1.48558` / SPT `5.0.0` / Trainer IL2CPP `1.18.0`

## 这篇帖子讲什么

这篇帖子面向第一次安装离线版辅助的玩家，内容包括：

- 实验室闪退有什么表现。
- 为什么会出现这个问题。
- 新版辅助做了什么修复。
- 辅助应该怎么正确安装。
- 如何确认修复已经生效。
- 安装失败、闪退仍然发生或想卸载时应该怎么办。
- 发反馈帖时需要提供哪些日志。

请先完整看完“适用版本”和“安装前准备”，不要只替换文件后立刻进游戏。

## 先说结论

这次处理的是一种固定的游戏原生崩溃：

```text
lib_burst_generated.dll+0x55338
```

它发生在游戏的空间音频处理代码中，不是普通 Trainer 功能错误。

Trainer IL2CPP 1.18.0 会检查这个游戏版本和对应的游戏文件；确认完全匹配后，才会在本次游戏进程的内存里安装保护逻辑。

它不会修改磁盘上的游戏 DLL，也不会修改你的角色存档。

## 适用版本

| 组件 | 适配版本 |
|---|---|
| Escape from Tarkov 客户端 | `1.3.0.1.48558` |
| SPT 客户端版本标识 | `EFT Client SPT 5.0.0 (BEM) 048e0a` |
| SPT 服务端 | `5.0.0` |
| SPT 服务端提交 | `048e0aa` |
| Trainer IL2CPP | `1.18.0` |

不要在其他游戏版本上直接套用这个补丁。Trainer 会检查以下游戏文件：

```text
<EFT_ROOT>\EscapeFromTarkov_Data\Plugins\x86_64\lib_burst_generated.dll
```

目标文件 SHA-256 必须是：

```text
D9680179B1C43BD51B3A2BEE07BB55C53972E7EA69632B5A797C92CED84E92BD
```

版本不匹配时，Trainer 会停止安装原生保护补丁，不会强行修改游戏内存。

## 闪退表现

常见表现包括：

- 点击进入实验室后，在加载过程中闪退。
- 地图刚加载完成、游戏刚开始时闪退。
- 进入实验室后几秒到几十秒内闪退。
- 看起来像某个 AI 刚开枪，随后游戏退出。
- 游戏直接消失，不一定弹出正常的错误提示。

在 BepInEx 或游戏日志中可能看到：

```text
Native crash 0xC0000005 at lib_burst_generated.dll+0x55338
```

如果你看到的崩溃模块或偏移不是这个地址，说明可能是另一个问题，不能保证由本补丁解决。

## 为什么会出现

简单来说：

1. 实验室会加载空间音频和声音反射数据。
2. 游戏会处理从声音位置到玩家位置之间的命中记录。
3. 某条记录携带了无效索引 `0xFFFFFFFF`。
4. 游戏原本仍会使用这个无效索引读取数据。
5. 读取到不存在的内存地址后，Windows 触发访问冲突并关闭游戏。

因此，问题不一定和 Black Division 有关。

实际测试中，服务端没有生成 Black Division，游戏仍可能在同一位置崩溃。它只是很容易被枪声或空间音频计算触发，所以看起来像“某个 AI 一开枪就闪退”。

## 新版辅助做了什么

Trainer IL2CPP 1.18.0 增加了空间音频崩溃保护：

- 检查游戏文件版本。
- 检查目标位置是否仍是指定版本的原始机器码。
- 检查备用代码区域是否可用。
- 遇到索引 `0xFFFFFFFF` 时，跳过这条无效记录。
- 遇到正常索引时，继续执行原游戏逻辑。
- 只修改当前游戏进程的内存。
- 不修改 `lib_burst_generated.dll` 文件本身。

补丁启用后，BepInEx 日志中会出现：

```text
Spatial-audio crash guard applied: invalid hit index 0xFFFFFFFF will be skipped at lib_burst_generated.dll+0x55338.
```

没有出现这条日志，就说明补丁没有生效。

## 下载

Trainer IL2CPP 1.18.0 下载地址：

```text
[填写下载链接]
```

建议同时记录 DLL SHA-256 是否与发布帖一致：

```text
4B8594D8A0240BAF13B30850055F5ECE4EDCFE78B27F2D0FE649F37D6CB7A78E
```

如果下载到的文件哈希不一致，请不要安装，重新下载。

## 安装前准备

安装前请确认：

- 已经安装 SPT 5.0.0。
- 游戏目录中已经存在可用的 `BepInEx`。
- 游戏和 SPT Launcher 已经关闭。
- 知道自己的游戏安装在哪个目录。本文示例为 `<EFT_ROOT>`。
- 最好先备份角色资料和原有插件。

只要游戏仍在运行，Windows 可能锁定 `TrainerIL2CPP.dll`，导致覆盖失败。因此必须先退出游戏。

## 正确的文件结构

安装完成后，目录结构应该是：

```text
<EFT_ROOT>\
└─ BepInEx\
   └─ plugins\
      └─ TrainerIL2CPP\
         └─ TrainerIL2CPP.dll
```

以下位置都是错误的：

```text
<EFT_ROOT>\TrainerIL2CPP.dll
<EFT_ROOT>\BepInEx\TrainerIL2CPP.dll
<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP.dll
```

DLL 必须放在 `plugins\TrainerIL2CPP\` 文件夹内部。

## 安装步骤

### 第一步：关闭游戏

关闭以下进程：

- EscapeFromTarkov
- SPT Launcher
- SPT Server

只退回游戏主菜单不算退出，必须关闭整个游戏窗口。

### 第二步：进入插件目录

打开文件资源管理器，进入：

```text
<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP
```

如果该文件夹不存在，可以手动创建。

### 第三步：备份旧插件

如果目录里已经有 `TrainerIL2CPP.dll`：

1. 右键它，选择“复制”。
2. 在同一文件夹粘贴。
3. 把它重命名为：

```text
TrainerIL2CPP.old.dll.bak
```

如果目录中没有旧 DLL，可以跳过这一步。

### 第四步：替换 DLL

把下载的 1.18.0 文件：

```text
TrainerIL2CPP.dll
```

复制到：

```text
<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP\
```

如果提示是否覆盖，选择“是”。

再次确认最终路径：

```text
<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP\TrainerIL2CPP.dll
```

### 第五步：启动服务端和游戏

1. 启动 SPT Launcher。
2. 确认 SPT Server 已启动。
3. 启动游戏。
4. 进入角色主界面。

辅助面板默认使用：

```text
Right Alt
```

如果快捷键被修改过，请检查 BepInEx 配置。

### 第六步：确认补丁生效

打开：

```text
<EFT_ROOT>\BepInEx\LogOutput.log
```

搜索：

```text
Spatial-audio crash guard applied
```

正常找到完整日志：

```text
Spatial-audio crash guard applied: invalid hit index 0xFFFFFFFF will be skipped at lib_burst_generated.dll+0x55338.
```

表示保护补丁已加载。

如果没有找到这条日志，请先不要反复进实验室，继续查看“常见问题”。

### 第七步：进入实验室测试

确认日志生效后，再进入实验室。

建议第一次测试时：

- 使用普通装备。
- 不要同时开启大量 ESP 功能。
- 进入后停留一到三分钟。
- 观察普通 AI 和枪声是否仍触发原来的闪退地址。

如果游戏稳定，说明原来的空间音频崩溃点已经被绕过。

## 常见问题

### 1. 覆盖 DLL 时提示文件被占用

原因：游戏或 SPT Launcher 没有完全退出。

处理方法：

1. 打开任务管理器。
2. 结束 `EscapeFromTarkov.exe`。
3. 如果仍被占用，再结束 SPT Launcher 和 SPT Server。
4. 重新复制 DLL。

不要直接强制覆盖正在使用的 DLL。

### 2. 没有 `BepInEx` 文件夹

说明你的离线版可能没有正确安装 BepInEx，或者你选择的不是游戏实际安装目录。

不要把 Trainer DLL 直接放到游戏根目录。先确认离线版本身能够正常加载 BepInEx 插件。

### 3. `LogOutput.log` 里没有补丁成功提示

依次检查：

- DLL 是否放在 `BepInEx\plugins\TrainerIL2CPP\`。
- 文件名是否仍然叫 `TrainerIL2CPP.dll`。
- 是否误下载成压缩包却没有解压。
- 是否同时存在多个 Trainer DLL。
- 是否启动的是另一个版本的离线版。
- 日志中是否出现文件哈希不匹配。

### 4. 日志提示 `hash mismatch`

说明游戏目录中的 `lib_burst_generated.dll` 与补丁适配版本不同。

常见原因：

- 游戏更新了。
- 离线版客户端版本不是 `1.3.0.1.48558`。
- 游戏文件被其他补丁修改过。

不要强制安装原生补丁，应等待适配当前版本的 Trainer。

### 5. 仍然闪退

查看新的 `tushonka.log` 或 `LogOutput.log`。

如果仍在同一个地址：

```text
lib_burst_generated.dll+0x55338
```

说明补丁可能没有加载，或加载后被其他程序覆盖。

如果地址发生变化，例如变成 `+0x55340` 或其他模块，说明还存在另一个问题，需要重新分析，不能继续套用当前结论。

### 6. 杀毒软件拦截

Trainer 和 BepInEx 属于游戏注入类工具，可能被杀毒软件误报。

请先核对下载来源和文件哈希。不要为了安装未知文件而彻底关闭系统安全防护；如果不确定文件来源，请不要运行。

## 如何回退

### 方法一：恢复备份

1. 完全退出游戏。
2. 删除或重命名当前 `TrainerIL2CPP.dll`。
3. 把之前备份的 `TrainerIL2CPP.old.dll.bak` 改回：

```text
TrainerIL2CPP.dll
```

4. 重新启动游戏。

### 方法二：暂时移除 Trainer

1. 完全退出游戏。
2. 把 `TrainerIL2CPP.dll` 移动到其他文件夹。
3. 启动游戏测试。

## 反馈时需要提供什么

请至少提供：

- `<EFT_ROOT>\BepInEx\LogOutput.log`
- 最新 `<EFT_ROOT>\Logs\log_...` 文件夹中的：
  - `output_000.log`
  - `application_000.log`
  - `spatial-audio_000.log`
- `<EFT_ROOT>\tushonka.log`
- `lib_burst_generated.dll+偏移` 的完整崩溃行
- 是否出现 `Spatial-audio crash guard applied`
- 在哪个地图、进入战局多久后闪退
- 闪退前是否开枪、是否听到 AI 枪声

日志比“感觉是某个 AI 导致的”更有用。相同现象不代表相同原因。

## 测试状态

Trainer 1.18.0 已通过：

- Release 编译：0 警告、0 错误
- 目标 DLL 哈希校验
- 原始机器码校验
- 补丁机器码离线反汇编验证
- 入口、正常路径和无效索引跳过路径的地址校验

完整战局的长期稳定性仍建议继续实测。

如果补丁生效后仍出现相同偏移，请保留日志并等待下一版修复；如果崩溃偏移发生变化，则属于新的独立问题。

## 重要提醒

本补丁只处理已经确认的 `lib_burst_generated.dll+0x55338` 空间音频命中索引崩溃。

它不会：

- 修复所有实验室闪退。
- 修复显卡、内存或驱动导致的崩溃。
- 删除 Black Division。
- 改变实验室地图配置。
- 修改游戏磁盘 DLL 或角色存档。

请根据自己的游戏版本使用对应 Trainer，不要混用多个版本的辅助 DLL。

