# EFT Trainer Downloader

Windows x64 单文件下载器。它会：

- 从本仓库最新 Release 下载 `EFT-Trainer-Latest.zip`。
- 读取包内 `release.json`，校验 `TrainerIL2CPP.dll` 的 SHA-256。
- 检查所选目录包含 `EscapeFromTarkov.exe` 和 `BepInEx`。
- 备份已有 `TrainerIL2CPP.dll`。
- 将新 DLL 安装到 `BepInEx\plugins\TrainerIL2CPP`。

## 构建

```powershell
dotnet publish .\Downloader\EFT-Trainer-Downloader.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

输出位于：

```text
Downloader\bin\Release\net8.0-windows\win-x64\publish\EFT-Trainer-Downloader.exe
```

下载器不会下载或分发 EFT 游戏本体、BepInEx Interop 程序集、游戏源码或玩家配置。
