# TrainerEditor.exe

独立的 WinForms 单文件配置编辑器，用于编辑：

`%USERPROFILE%\Documents\Escape from Tarkov\trainer.ini`

启动后会默认打开该文件，也可以使用“打开”和“另存为”选择其他配置文件。

## 页面

- “锁定”：部位、头/胸优先级、可见性、平滑度、距离、视野半径和 Silent Aim。
- “ESP / 玩家”：ESP 开关、方框、隔墙显示、人体轮廓、Renderer 包围盒、瞄具裁剪、边框粗细和内边距。
- “全部配置”：读取并编辑 `trainer.ini` 中已有的所有键。
- “原始文本”：直接编辑整个配置文件，保留注释、颜色和未知配置项。

## 保存安全

- 保存前自动创建带时间戳的 `.bak` 备份。
- 先写入临时文件，再原子替换原文件，减少写入中断造成配置损坏的风险。
- 未识别的配置项和注释不会被主动删除。
- 修改未保存时关闭编辑器会提示确认。

## 文件

发布文件：

`dist\TrainerEditor-1.0.0\TrainerEditor.exe`

源码：

- `vendor\EscapeFromTarkov-Trainer\Editor\TrainerEditor.csproj`
- `vendor\EscapeFromTarkov-Trainer\Editor\Program.cs`
- `vendor\EscapeFromTarkov-Trainer\Editor\EditorForm.cs`
- `vendor\EscapeFromTarkov-Trainer\Editor\IniDocument.cs`

