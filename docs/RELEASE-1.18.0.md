# EFT Trainer IL2CPP 1.18.0

本版本适用于与插件兼容的自建离线 SPT / EFT 1.3 IL2CPP 环境。

> 仅限离线 SPT 使用。不要在官方在线服务器使用修改后的客户端。

## 下载与安装

推荐直接下载 Windows 下载器：

```text
EFT-Trainer-Downloader.exe
```

运行后选择 EFT / SPT 根目录，点击“下载并安装最新版”。下载器会自动校验、
备份旧 DLL 并安装。

也可以手动下载附件 `EFT-Trainer-Latest.zip` 或版本化附件
`TrainerIL2CPP-1.18.0.zip`。

下载附件 `TrainerIL2CPP-1.18.0.zip`，解压后得到：

```text
TrainerIL2CPP.dll
README.txt
SHA256.txt
```

完全退出游戏、SPT Server 和启动器，然后将 DLL 放到：

```text
<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP\TrainerIL2CPP.dll
```

通过正常的 SPT 启动流程进入游戏，默认按 `Right Alt` 打开面板。

## 文件校验

`TrainerIL2CPP.dll`：

```text
4B8594D8A0240BAF13B30850055F5ECE4EDCFE78B27F2D0FE649F37D6CB7A78E
```

发布压缩包：

```text
1CBDB9B36B4359B60B2CF8296B8B8FE4922F0CC833C1AA50D307F9DD64471C51
```

下载器：

```text
B9344255B8D9EC0E1FDD124F89651DEC38805C1B3B8273782B9845F98D99D71E
```

## 主要内容

- 中文七页游戏内面板和手动多键绑定。
- 头、胸、腹部、胳膊和腿锁定，可见性过滤、移动目标提前量。
- 玩家方框、骨架、阵营颜色、隔墙显示、撤离点和转移点。
- 战利品、尸体、容器、普通任务、剧情任务和任务物品标记。
- 完全锁血、七部位阈值锁血、分类负面状态清理、无限耐力和武器无故障。
- 全屏地图、键盘标记传送、撤离点 / 转移点遍历、返回传送前位置。
- 原生 `Get Item` 右键菜单和数量选择。
- 任务跳过、跳蚤市场解锁、战斗 / 任务 / 潜行预设。

## 兼容性

- 必须使用与 EFT 版本匹配的 BepInEx IL2CPP 和 Interop 程序集。
- 替换 DLL 后必须完全重启游戏。
- GitHub 仓库不包含游戏本体、BepInEx Interop 程序集、反编译游戏源码或本机配置。

## 排错

加载成功时，在 `BepInEx\LogOutput.log` 中搜索 `EFT Trainer IL2CPP`。

异常日志位于：

```text
<EFT_ROOT>\BepInEx\ErrorLog.log
```

完整说明请查看仓库根目录的 `README.md`。
