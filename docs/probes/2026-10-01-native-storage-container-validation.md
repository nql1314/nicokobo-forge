# 2026-10-01 Forge 原生储水容器槽

目标：Probably Stolen Demo Steam Build `25382790`，Forge `0.6.0`、合成扩展 `0.9.0`。版本号及稳定 ID 不变。

## 框架与声明

- Forge 的液体输入和容器输出共用 `CreateContainerSlot`：原生 `GameSlotInventory()`、`Items/water_container4` / `xl` 容器背景；名称取 `LocHelper.GetLocalizedMechanic("mech_purifier_label_container", ...)`。原生 `LocHelper.Get` 接受空参数数组和 null 参数，均走无格式参数分支。
- `ForgeMachineContainerSlot.Label` 默认留空，使用游戏本地化“储水容器”；保留显式自定义标签能力。输出列继续使用熔炉原生“输出”标题。
- 内容 Mod 只声明槽位和配方，不自行绘制容器 UI：K04 为物品与液体输入、现有容器输出；K05 声明一个可选液体输入。K04 直接填入玩家的输出容器，不生成新罐；输入输出容器都保留。
- 可选性由配方是否声明 `LiquidInputs` 决定。K05 普通物品配方为空列表，不需要容器，也不扣可选容器中的液体。内容领域计算不再把“机器存在水槽”当成“每条配方都需要水”。

原生定义来自本地 Build 25382790 的 `MachinePurifier.WaterPurifier`、`CreateMachineInventoryWindow` 和 `LocHelper.Get` ISIL，互操作签名由当前游戏 DLL 的 Cecil 元数据检查。

## 构建与安装

统一入口 `D:\workzone\probably-stolen\mods-melonloader\Build-ForgeMods.ps1 -DefaultLogLevel INFO` 通过：

- 框架原生签名 37；机器模板 43、批次计算及失败恢复 119、运行边界 88。
- 机械飞升 422；模组矩阵 89；物流 L0 21 / resource 37 / power 27；合成扩展 632。
- 核心、六个示例、四个内容调用者 Release 编译，三个机核包 Obfuscar，配套 Forge 哈希检查通过。

09:51 游戏停止时备份后安装 Forge 与合成扩展，发布与安装 SHA256 相同：

| 文件 | SHA256 |
| --- | --- |
| `Nicokobo.Forge-0.6.0.dll` | `CD33468261BD684EB5D86C3FAC8E0D3208823F14B8EF1AEAFFC5AC2FB48B9B59` |
| `MechcoreProtocol.SynthesisExpansion-0.9.0.dll` | `89AEC7E7FFAEE87E1017ABA2DF0FE70EE062DACA76F5040626F22BF3D146B3B9` |

安装记录和旧 DLL 位于 `D:\workzone\probably-stolen\_build\forge-container-check-20261001\installation.json` 及其 `backup-20261001-095137` 目录。

## 实机验证未完成

临时探针计划在新进程阻止 `PlayerStore.SaveGame/LoadGame`，创建脱离玩家库存的临时原生机器，直接调用 Forge 批次事务检查双容器、容量差 1 ml、原地灌液和 K05 的可选输入。

本轮游戏在 Mod 加载前退出，进程返回码 `53`；`--no-mods` 的对照启动也返回 `53`。没有生成探针 `result.json`，没有到达槽位或事务测试，不能据此宣称本轮的原生 UI、实际灌液或存档重载通过。启动记录位于 `_build/forge-container-check-20261001/startup-result.json` 和 `bootstrap-out.txt` / `control-out.txt`。

临时 `ForgeContainerCheck.dll` 已从游戏 `Mods` 移出；用户 `MelonPreferences.cfg` 完整恢复，SHA256 `E2B9A4B2E2EB99BEF549F4E77ED28CA226EC0EB2898B705623921240B937CE58`。先前 09:21 的布局探针只验证此前布局版本，不能代替本轮储水容器与灌液验证。
