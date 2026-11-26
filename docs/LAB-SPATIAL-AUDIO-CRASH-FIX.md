# 【Bug 修复】离线版实验室进入闪退：`lib_burst_generated.dll+0x55338`

> 适用版本：EFT `1.3.0.1.48558` / SPT `5.0.0` / Trainer IL2CPP `1.18.0`

## 适用版本

本补丁针对以下版本组合开发和验证：

| 组件 | 版本 |
|---|---|
| Escape from Tarkov 客户端 | `1.3.0.1.48558` |
| SPT 客户端版本标识 | `EFT Client SPT 5.0.0 (BEM) 048e0a` |
| SPT 服务端 | `5.0.0` |
| SPT 服务端提交 | `048e0aa` |
| Trainer IL2CPP | `1.18.0` |
| `lib_burst_generated.dll` SHA-256 | `D9680179B1C43BD51B3A2BEE07BB55C53972E7EA69632B5A797C92CED84E92BD` |

其他游戏或 SPT 版本不能假定兼容；Trainer 会校验目标 DLL 哈希，不匹配时不会应用原生补丁。

## 问题现象

离线版进入实验室后，游戏可能在加载完成、刚进入战局时直接闪退。部分情况下会在空间音频或环境声加载完成后发生，表现看起来像是某个 AI 开枪后触发，但与 AI 武器或 Black Division 势力生成没有必然关系。

典型日志：

```text
Native crash 0xC0000005 at lib_burst_generated.dll+0x55338
```

Windows 应用错误日志也会显示：

```text
出错模块：lib_burst_generated.dll
异常代码：0xc0000005
错误偏移：0x0000000000055338
```

## 根因

崩溃位置是 Unity Burst 生成的空间音频命中处理代码，不是 Trainer 的枪械伤害补丁。

崩溃指令会使用一个 `int` 命中索引查找结构体：

```asm
vmovsd xmm2, [r9 + r11 + 4]
```

实际崩溃时反算出的索引为：

```text
0xFFFFFFFF
```

这个值没有对应的音频碰撞体记录，但原代码仍把它乘以每条记录大小并访问数组，最终读取到未映射地址，触发访问冲突。

以下现象可以排除“Black Division 开枪”是唯一原因：

- 服务端日志中没有生成 Black Division 或 Wedge 时，仍可在同一地址复现。
- 客户端仍在启动阶段或空间音频初始化后崩溃，并非一定发生在 AI 交火后。
- 多次崩溃的模块和偏移完全一致：`lib_burst_generated.dll+0x55338`。
- Trainer 没有对 AI 射击、`ShotDelegate` 或 `BallisticsTick` 打补丁。

## 修复方式

Trainer IL2CPP 1.18.0 加入了运行内存保护补丁。

补丁会：

1. 校验 `lib_burst_generated.dll` 的版本哈希。
2. 校验目标位置原始机器码和代码洞。
3. 在空间音频命中循环中检查索引是否等于 `0xFFFFFFFF`。
4. 遇到无效命中时跳过该条记录，继续处理其他有效命中。
5. 正常索引仍执行原游戏逻辑。

补丁不会修改磁盘上的游戏 DLL，只会在当前游戏进程的内存中启用。

## 安全限制

补丁只适配下面这个版本的 `lib_burst_generated.dll`：

```text
SHA-256:
D9680179B1C43BD51B3A2BEE07BB55C53972E7EA69632B5A797C92CED84E92BD
```

如果游戏更新后该 DLL 发生变化，Trainer 不会强行写入补丁，而会在 BepInEx 日志中报告哈希或机器码不匹配。

## Trainer 1.18.0

```text
SHA-256:
4B8594D8A0240BAF13B30850055F5ECE4EDCFE78B27F2D0FE649F37D6CB7A78E
```

下载地址：

```text
[填写 Trainer 1.18.0 下载链接]
```

## 安装方法

1. 完全退出 EscapeFromTarkov 和 SPT Launcher。
2. 备份原有插件：

```text
<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP\TrainerIL2CPP.dll
```

3. 将 1.18.0 的 `TrainerIL2CPP.dll` 放到：

```text
<EFT_ROOT>\BepInEx\plugins\TrainerIL2CPP\TrainerIL2CPP.dll
```

4. 启动 SPT 服务端和游戏。
5. 打开 BepInEx 日志，确认日志中出现：

```text
Spatial-audio crash guard applied: invalid hit index 0xFFFFFFFF will be skipped at lib_burst_generated.dll+0x55338.
```

看到这条日志后，空间音频崩溃保护已经生效。

## 回退方法

如果需要恢复到旧版本：

1. 完全退出游戏。
2. 删除或重命名当前 `TrainerIL2CPP.dll`。
3. 将之前备份的 DLL 改回 `TrainerIL2CPP.dll`。
4. 重新启动游戏。

## 反馈问题时请提供

- BepInEx 日志：`<EFT_ROOT>\BepInEx\LogOutput.log`
- 当前游戏日志目录中的：

```text
* output_000.log
* application_000.log
* spatial-audio_000.log
```

- 原生崩溃日志：`<EFT_ROOT>\tushonka.log`
- 是否在日志中看到 `Spatial-audio crash guard applied`
- 闪退发生的地图、是否已经进入战局、距离进入战局大约多久

## 测试状态

1.18.0 已通过：

- Release 编译：0 警告、0 错误
- 补丁目标 DLL 哈希校验
- 原始机器码校验
- 补丁机器码离线反汇编验证
- 入口、正常路径和无效索引跳过路径的目标地址校验

完整战局的长期稳定性仍建议继续实测。如果仍发生闪退，请保留上述日志；如果崩溃地址发生变化，说明还存在另一个独立问题，不应继续套用本补丁。

