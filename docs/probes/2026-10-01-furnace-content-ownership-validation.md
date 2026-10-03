# 原版熔炉扩展归属检查（2026-10-01）

本地候选：Forge `0.6.0`、合成扩展 `0.9.0`。目标为 Probably Stolen Demo Steam Build `25382790`。本轮未安装或启动游戏。

## 改动

- 合成扩展的 `Integration/NativeFurnaceRuntime.cs` 持有原版熔炉配方接入、槽位读取、投料和生产两个 Hook、原版批次优先级和夜间调度。Hook owner 为 `nicokobo.mechcore.synthesis.furnace`，仅针对原版 `furnace`。
- 配方仍由合成扩展的 `recipes.json` 加载并冻结；投料索引取加载后的投入 ID，产物取加载后的输出 ID。配方、物品 ID 与 schema 本轮不变。
- Forge 移除 `RegisterExistingMachine`、原版熔炉目录分支、专用运行状态、原版批次探针和助溶剂判定。自动扫描及机器快照只包含已注册自定义机器；通用 UI 模板仍复用原版组件。
- `ForgeMachineRuntimeApi.CreateBatchProcessor` 只冻结规则并复用一批资源事务，调用方提供实时槽位并拥有触发与去重。它不注册物品、不安装玩法 Hook、不自动扫描现有机器。
- 熔炉当夜归属保留到原版夜间循环结束；最后一批玻璃耗尽、资源不足或交易失败均不会在同夜重新选择。原版炼锭、纯度和助溶剂规则仍由游戏执行。
- 两项熔炉签名检查移到合成扩展的 `Check-FurnaceContracts.ps1`，由既有机核发布入口调用；Obfuscar 排除项保留该内容适配器的 Hook 方法名。

## 本地验证

统一入口：

```powershell
& D:\workzone\probably-stolen\mods-melonloader\Build-ForgeMods.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo' -DefaultLogLevel INFO
```

- Forge：模板目录 40、批次数学／故障恢复 119、运行边界 88 项断言通过；既有注册、物流和液体领域检查通过。
- 调用方领域检查：机械飞升 422、模组矩阵 89、物流 L0=21／resource=37／power=27、合成扩展 629 项通过。
- Forge 核心、六个示例和四个调用工程 Release 编译通过；三个机核发布 DLL 经 Obfuscar 生成。合成扩展新增的空值编译警告修复后，通过 `Build-MechcoreProtocol.ps1 -OnlyProject mechcore-protocol-synthesis-expansion` 重建并打包，0 警告／0 错误。
- 框架 30 项通用元数据签名与内容侧 2 项熔炉元数据签名通过；上述检查不初始化原生类。
- Cecil 读回最终 DLL：Forge 无 `RegisterExistingMachine`、`NativeFurnaceExtensionInstalled`、`NativeOwnsNight`、`RegisterNative` 或 `NativeMachine` 成员；无原版熔炉投料／生产闭包引用和小写 `furnace` 扩展注册 ID。模板工厂 `MachineFurnace.Furnace()` 仍存在。
- 混淆后的合成扩展仍保留 `NativeFurnaceRuntime.AdmissionPrefix` / `CyclePrefix`，Harmony owner 正确，且调用通用 `ForgeMachineBatchProcessor.Process`。
- 四个调用程序集均引用 Forge `0.6.0.0`；三个机核包内 Forge SHA-256 与核心一致。Forge 与合成扩展的本地开发日志默认均为 `INFO`。
- 框架本地网站 ZIP 由 `scripts/Pack-ModSite.ps1` 生成，路径为 `dist/Nicokobo.Forge-0.6.0.zip`；没有线上发布。

最终 DLL SHA-256：

| 产物 | SHA-256 |
| --- | --- |
| `Nicokobo.Forge.dll` | `06A5749E4AEB14EAEEE729C45670C439A2A830731794B69D35BD1A7E9F0472D4` |
| `MechcoreProtocol.SynthesisExpansion-0.9.0.dll`（Obfuscar） | `7243D39E688076EDE009C3BC480CFD926A307725BBD7C0419D6C83B07EE7F7F4` |

## 实机待验收

单独安装 Forge 时的原版行为、加载合成扩展后的补丁安装、玻璃材料拖入、金属／垃圾原版优先、最后一批玻璃消耗、同夜唯一批次、真实电耗、缺电／满仓恢复及新档保存重载，均须新进程和可丢弃存档验证。源码、签名、领域检查、编译和 DLL 哈希不代替这些结果。
