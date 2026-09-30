# 2026-09-30 原版批次探针本地验证

目标：Probably Stolen Demo Build `25382790`，Forge `0.3.8`，合成扩展 `0.5.23`。本记录只对应本地源码、检查和发布包，不代表过夜或读档验收。

## 背景

用户在已安装的 0.5.19 + Forge 0.3.5 上报告两类熔炉混放没有产出：玻璃刀×2＋碎玻璃×1＋废金属×1、金属矿石×1＋空啤酒瓶×1。旧版在这两种投入下整夜既不出石英也不出金属锭：Forge 因槽内存在非玻璃原料而整体跳过，原版循环又凑不够自己的批量。用户要求的分工是“先判原版能不能生成，能则原版生成；原版不生成时才走新配方”。

## 改动

- `ForgeMachineApi.RegisterExistingMachine` 增加可选参数 `Func<GameItem, GameInventory, bool>? nativeBatchProbe`。探针回答“游戏自己的熔炉循环对该投入还有没有事可做”。
- `MachineProfile.NativeBatchProbe` 保存探针：注册时冻结，追加注册沿用既有探针，声明在非原版机器返回 `Invalid`。
- `ForgeMachineRuntime.Process`：原版机器有探针时，探针为 `true` 直接返回 `native-batch-night`，注册配方整夜不参与；为 `false` 时照常选择注册配方加工。没有探针的旧注册保持“任何非玻璃原料都返回 `mixed-native-input`”的行为。
- `ForgeMachineHooks.CyclePrefix`：探针为 `true` 时原生循环照常执行；探针为 `false` 且槽内确有注册原料（玻璃）时跳过原生循环，避免空转；槽内没有注册原料时一律不干预，垃圾处理等原版玩法不受影响。探针抛错按 `true` 处理并记 `WARN`。
- 合成扩展按目标构建的规则实现探针：`common_ore` 与 `scrap_metal` 合计 ≥2 件（机器装了 `furnace_module_blast` 时 ≥3 件），或装了 `furnace_module_junk` 且槽内有 `junk` 时归原版；玻璃、酒瓶、助溶剂与垃圾（垃圾模组未装时）都不计入。
- `MachineCatalogChecks` 增加探针注册、跨注册保留与自定义机器拒绝的断言。

## 纯度门槛（合成扩展 0.5.21）

- 配方配置升级为 `SchemaVersion=3`：P03 精密零件要求高纯度及以上金属锭×2，P04 高级电路元件要求高纯度及以上硅×1。
- 加载 v1/v2 配置时备份为 `recipes.json.before-schema-3.bak` 并补上缺少的纯度门槛，保留既有的材料数量、产物件数与已写明的条件；v3 文件不再迁移，用户改动不会被覆盖。
- 纯度条件的适用物品统一由 `SynthesisDefinitions.CarriesPurity` 判定：原版 `metal_ingot` 与本扩展的硅、钛合金（后两者由精炼机写入原版纯度标签）。校验器与运行时条件匹配都改用它，硅因此可以按纯度筛选；没有纯度标签的旧硅会被门槛拒绝。

## 液体整叠投入（Forge 0.3.7 / 合成扩展 0.5.22）

- `ForgeMachineRecipe.AcceptsStackedInput` 只对 `NightlyLiquid` 有效：置位后整叠投入是一份批次投入，整叠保留到目标件数完成才一次消耗，进度里的 `InitialValue` 记整叠的合并价值（`MachineLiquidMath.MergedStackValue`，饱和处理）；未置位的配方继续拒绝单位数量不为 1 的投入，声明在 `NightlyItems` 上按无效注册处理。
- 合成扩展新增 P17：K04 接收 `nutrifruit`×1（整叠），水质与水量沿用每件 100 ml 优质水及以上，目标件数按合并价值计算（`ProductionRules.MergedValue`，`max(1, ceil(合并价值/100))`）。
- 配方配置升级为 `SchemaVersion=4`（20 条）：加载 v1–v3 文件时备份为 `recipes.json.before-schema-4.bak` 并补入 P17；配方“引入版本”由 `IntroducedSchema` 决定，比文件版本新的配方自动补入，而文件本应包含的配方缺失仍整份回退。

## K04 按整百凑件（Forge 0.3.8 / 合成扩展 0.5.23）

- 产量改为 `floor(投入价值 / 100)`：不再向上取整、不再保底 1 件；`ResolveLifetimeOutputCount` 返回 0 时 Forge 记 `input-below-batch`，不扣料、不扣水、不扣电。
- 整叠配方按价值从大到小取用：同一配方的候选物品里挑单位价值最高的（同价值取数量多者）。
- 完成一批时用 `ForgeMachineRecipe.ResolveConsumedUnits(item, targetCount)` 只扣应付的整件（`ProductionRules.SpentUnits`：整价值量向下取整、至少 1 件、不超过整叠），剩余整件留在投料槽；回滚时恢复被扣数量，越界返回 `spent-units-invalid`。
- 例：单价 12 的营养果 9 个一叠（108）→ 1 件凝胶、扣 8 个、剩 1 个；8 个（96）→ 不出件且不消耗。连带的规则影响：生肉（120）每件出 1 件（此前 2 件），基础价值 60 的小块生肉不再够一件。

## 核对与验证

- `scripts/Build-P0.ps1 -GameDir "F:\SteamLibrary\steamapps\common\Probably Stolen Demo"`：领域检查（含新增机器目录断言）、核心、两个 P0 示例及物流示例 Release 构建通过；仅剩原有的 .NET 6 生命周期提示。
- 合成扩展 `dotnet run --project .\Checks\MechcoreProtocol.SynthesisExpansion.Checks.csproj`：619 条断言通过，含硅纯度档位、助溶剂移除后的手册文案、本文两个报告用例的探针判定、P03/P04 纯度门槛、合并价值与整叠取件（含向下取整、余料保留、饱和）、v1–v3 迁移补齐配方/门槛并保留用户数量与备份字节。
- 合成扩展经 `mods-melonloader/Build-MechcoreProtocol.ps1 -OnlyProject mechcore-protocol-synthesis-expansion` Release 编译（零警告零错误）与 Obfuscar 打包，发布目录同时复制配套 Forge。
- 静态核对 `_Furnace_b__5`：原版先按谓词过滤输入槽，只把金属件计入批量（含 `common_ore`、`scrap_metal` 两条过滤），过滤列表不足时直接返回；消费也只发生在过滤列表内，因此判定与取料都不涉及玻璃。`furnace_module_blast` 会把批量提到 3 件并产出第二块金属锭。过滤谓词的完整集合仍需实机复核。

本地发布产物：

| 文件 | SHA-256 |
| --- | --- |
| `Nicokobo.Forge.dll`（0.3.8，源码 Release 与发布包一致） | `DB4ECD2FEBBD71CF048327D6801FF81A4D391C1E9178FE81B1F9F6C5C0254DDC` |
| `MechcoreProtocol.SynthesisExpansion-0.5.23.dll` | `6B85F2DA7D624A7EE60FBF9037EED8A92F2999CCA3EA6345D03F3BF496A190EF` |

发布目录：`D:\workzone\probably-stolen\mods-melonloader\dist\mechcore-protocol-synthesis-expansion`（含 README、change.log、recipes.default.json、CHECKLIST.md）。

## 安装记录

按用户请求安装本轮候选，两次安装时游戏进程均未运行。

10-01 00:51 安装 Forge 0.3.6 与合成扩展 0.5.20，旧文件备份到 `D:\workzone\probably-stolen\_build\backups\synthesis-native-priority-20261001-0051`：`Nicokobo.Forge.dll` `06F1392D481A29C4…`（0.3.5）、`MechcoreProtocol.SynthesisExpansion-0.5.19.dll` `3FAC1C100454AD1B…`。

10-01 01:07 安装含纯度门槛的合成扩展 0.5.21，0.5.20 与同版 Forge 备份到 `D:\workzone\probably-stolen\_build\backups\synthesis-purity-gates-20261001-0107`：

| 文件 | 安装前 SHA-256（已备份） | 安装后 SHA-256 |
| --- | --- | --- |
| `Nicokobo.Forge.dll` | `A239957A131266AA…`（0.3.6） | `9E4FA16F391A5F5E…`（0.3.7） |
| `MechcoreProtocol.SynthesisExpansion-*.dll` | `02B2D38E4FAC4805…`（0.5.21） | `912BE648DDE7EBBA…`（0.5.22） |

10-01 01:14 安装合成扩展 0.5.22 与 Forge 0.3.7，被替换的 0.5.21 与 0.3.6 备份到 `D:\workzone\probably-stolen\_build\backups\synthesis-bio-nutrifruit-20261001-0114`。

安装后 `Mods` 顶层只剩一份 Forge 与一份合成扩展，旧版 0.5.19/0.5.20/0.5.21 已移出目录避免重复加载。游戏内 `UserData/MechcoreProtocol.SynthesisExpansion/recipes.json` 仍是 `SchemaVersion=2`，下次启动会备份为 `recipes.json.before-schema-4.bak` 并补入 P17 与两条纯度门槛。

10-01 01:2x 拟安装合成扩展 0.5.23 与 Forge 0.3.8，但游戏进程正在运行，替换被中止（脚本在检测到 `Probably Stolen` 进程时直接抛出，未改动任何文件）。当前 `Mods` 内仍是 0.5.22 + Forge 0.3.7；待游戏关闭后再执行同一条备份并替换流程，安装结果会补记在本节。本轮仍未启动新版本：加载日志、过夜产出、读档与上表之外的文件均未验证。

## 证据边界

本轮安装前没有启动或过夜测试：探针的运行时判定、原版循环实际取料范围、玻璃配方产出与读档都尚未在游戏内确认。静态结论只到签名、注册校验与调度分支：有探针时程序集内原版机器只走一条加工分支，`CyclePrefix` 与 `Process` 使用同一探针结果。

## 下一次实机核对

1. 金属矿石×1＋空啤酒瓶×1：应产出石英×1，矿石保留。
2. 玻璃刀×2＋碎玻璃×1＋废金属×1：应产出石英×1，碎玻璃与废金属保留。
3. 玻璃刀×2＋金属矿石×2：应出金属锭，玻璃留到次夜；记录原版消耗对象与纯度。
4. 装 `furnace_module_blast` 后金属×2＋玻璃刀×2 应回收玻璃，补到金属×3 后改为原版炼锭（2 块）。
5. 装 `furnace_module_junk` 时垃圾＋玻璃刀×2 让位原版，无垃圾时正常回收玻璃。
6. P03 只吃高纯度及以上金属锭，P04 只吃高纯度及以上硅；普通纯度与无标签硅不投料、不耗料，手册同步显示门槛。
7. K04 投入一叠营养果（例如 9 个单价 12）：应出 1 件凝胶、扣 8 个、剩 1 个留在投料槽；整叠在完成前数量不变，每件精确扣 100 ml 优质水及以上；8 个（96）应完全不出件也不消耗。
8. 启动日志确认配置迁移：`before-schema-4.bak` 已生成、20 条配方加载（含 P17）、P03/P04 门槛生效。
9. 复测纯玻璃回收、原版矿石熔炼、模块装入/移出回调、电量与保存重载，确认 0.3.6/0.3.7 未改变既有行为。
