# 锁定与 ESP 方框修改

## Aimbot

新增配置：

- `EFT.Trainer.Features.Aimbot.TargetPart`
  - `Head`
  - `Chest`
  - `Stomach`
  - `Arms`
  - `Legs`
- `EFT.Trainer.Features.Aimbot.PreferHeadChest`
  - `true`：头优先，其次胸，最后使用选择的部位
  - `false`：只锁定选择的部位
- `EFT.Trainer.Features.Aimbot.VisibleOnly`
  - `true`：只锁定射线可见的骨骼点
  - `false`：允许隔墙锁定
- `EFT.Trainer.Features.Aimbot.Smoothness`
  - 范围按 `0.01`–`1.0` 生效
  - 数值越大，转向越快；默认值改为 `0.35`

锁定逻辑现在会：

- 根据选择的头、胸（Ribcage）、腹部（Spine1）、上臂或大腿选择真实骨骼 Transform。
- 胳膊和腿会在左右两侧中选择更合适、且启用可见性检查时优先可见的一侧。
- 优先级固定为“头 > 胸 > 其他部位”；关闭优先级后不跨部位抢目标。
- 普通锁定和 Silent Aim 共用相同目标选择与可见性设置。
- Silent Aim 使用根据目标速度和弹速计算后的提前量位置。

## ESP 方框

新增配置：

- `EFT.Trainer.Features.Players.UseRendererBounds=true`
- `EFT.Trainer.Features.Players.BoxPadding=0.08`
- `EFT.Trainer.Features.Players.ClampBoxToScope=true`

新方框算法不再使用“头顶到左肩”的两点估高，而是：

1. 收集玩家 BodySkins 中所有有效 Renderer 的本地包围盒。
2. 用各 Renderer 的世界变换把包围盒 8 个角转换到世界空间，再投影到当前普通摄像机或光学瞄具摄像机。
3. 合并投影点，得到每帧真实的屏幕空间包围矩形。
4. 按方框短边百分比增加内边距。
5. 开镜时裁剪到当前瞄具镜片范围。

没有 Renderer 时，会回退到头部、胸、腹部、骨盆、左右上臂、左右大腿和脚底骨骼点。

这样方框大小会跟随高倍镜当前投影变化，不再用固定比例把方框放大。

## 文件

- `vendor\EscapeFromTarkov-Trainer\Features\Aimbot.cs`
- `vendor\EscapeFromTarkov-Trainer\Features\Players.cs`
- 示例配置：`config\trainer-aim-esp.example.ini`

本次修改已通过 Roslyn 语法检查，但当前安装目录是 EFT 1.3 IL2CPP，无法用旧托管项目直接进行游戏内运行验证。

