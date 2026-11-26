# Installer 3.3.0 修改说明

最终可执行文件：

`dist\installer-3.3.0\Installer.exe`

SHA-256：

`1DF01F58D6A669BFEDC4C18E3DB5CA76FEB143ED7423CED42667257B43E5910A`

## 已实现

### G1 本地源码 ZIP

```powershell
Installer.exe install "<EFT_ROOT>" --source ".\trainer-source.zip"
```

`--source` 直接读取 ZIP，不访问网络。ZIP 必须包含 `NLog.EFT.Trainer.csproj`。

### G2 源码缓存

默认缓存目录：

`%LOCALAPPDATA%\EscapeFromTarkov-Trainer\snapshots`

成功下载后缓存按分支保存，例如 `master.zip`。再次安装会优先使用缓存。

```powershell
# 仅使用缓存，不联网
Installer.exe install "<EFT_ROOT>" --offline

# 强制刷新已有缓存
Installer.exe install "<EFT_ROOT>" --refresh

# 自定义缓存目录
Installer.exe install "<EFT_ROOT>" --cache-dir "D:\EFT-Cache"
```

### G3 SPT_Runtime 识别

现在支持：

- `SPT_Data`
- `SPT\SPT_Data`
- `SPT_Runtime\SPT_Data`
- 游戏根目录下任意一级子目录中的 `SPT_Data`

### G4 兼容性预检

安装前检查：

- `EscapeFromTarkov.exe`
- 游戏版本
- IL2CPP / `GameAssembly.dll` / `il2cpp_data`
- `EscapeFromTarkov_Data\Managed`
- `Managed\mscorlib.dll`
- SPT 数据目录
- BepInEx

当前旧训练器支持上限为 EFT `0.16.9.40087`。检测到 EFT 1.x IL2CPP 时会停止并返回退出码 `5`，不会写入游戏目录。

### G5 选择性编译

可以通过排除列表、包含列表或 JSON 配置决定编译内容。

```powershell
Installer.exe install "<EFT_ROOT>" --profile ".\profiles\user-required.json"
Installer.exe install "<EFT_ROOT>" --include-feature Players,LootItems,Health
Installer.exe install "<EFT_ROOT>" --exclude-feature Radar,Hud,Speed
Installer.exe install "<EFT_ROOT>" --exclude-command Spawn,SpawnBot
```

`profiles\user-required.json` 已按本轮需求保存排除项。

### G6 备份与一键回滚

实际写入游戏目录前会备份：

- `NLog.EFT.Trainer.dll`
- `0Harmony.dll`
- `NLog.dll.nlog`
- `outline`
- `spt-efttrainer.dll`

默认备份目录：

`%LOCALAPPDATA%\EscapeFromTarkov-Trainer\backups`

```powershell
# 自动恢复该游戏目录的最新备份
Installer.exe rollback "<EFT_ROOT>"

# 恢复指定备份
Installer.exe rollback "<EFT_ROOT>" --backup "C:\Path\To\Backup"

# 只查看恢复计划
Installer.exe rollback "<EFT_ROOT>" --dry-run
```

安装或卸载中途失败时，会自动执行回滚。

### G7 Dry-run

```powershell
Installer.exe install "<EFT_ROOT>" --dry-run --offline
```

`--dry-run` 会执行兼容性预检、源码加载和内存编译，但不创建备份、不写 DLL、不修改 NLog 配置。

### G8 自动日志

日志默认写入：

`%LOCALAPPDATA%\EscapeFromTarkov-Trainer\logs`

若该目录不可写，会自动回退到系统临时目录。日志包含命令行、预检结果、缓存来源、编译诊断、写入/删除操作和完整异常。

## 兼容性边界

安装器改进不改变训练器本体的运行时兼容性。当前 `<EFT_ROOT>` 是 EFT 1.3.0.1 IL2CPP，旧训练器的托管源码不能直接加载。所有未排除的游戏功能需要在新 IL2CPP 插件工程中实现；`G1`–`G8` 已完成，并可继续复用于新插件安装流程。

