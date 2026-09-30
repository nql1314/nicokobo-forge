# 2026-10-01 液体产物与按批用水量本地验证

目标：Probably Stolen Demo Build `25382790`，Forge `0.3.9`，合成扩展 `0.6.0`，伪人 Mod `0.1.6`。本记录只对应本地源码、检查和发布包，不代表过夜或读档验收。

## 背景

用户要求把 K04 生物合成槽改成**液体灌注塔**：两个输入位（物料 + 水容器），产物改为装在容器里的**液体生物质**，毫升数按投入肉或营养果的总价值生成，并消耗“基础水以上”的水，比例按 1 ml 生物质 = 1 ml 水；同时把 K05 机械制造机改名**量子制造机**并保持按配方表制造，原先用生物凝胶的四条伪人义体配方改用容器输入。

技术上先确认了边界：游戏的原生液体注册表（`Liquid.Liquids`）只读，内容 Mod 无法注册新液体类型，只能把已有液体灌进容器（`WaterHelper.AddLiquid`，1000 内部量 = 1 ml）；因此“液体生物质”由原生 `protein` 液承载，界面文案统一称其为液体生物质。Forge 原本只有固定的“每件产物毫升数”和物品模式的 `PrepareOutput`，没有按批用水量，也不会在液体模式下调用内容方的收尾回调，因此本版补上两处能力，并给液体进度记录加上批次语义版本。

## 改动（Forge 0.3.9）

- `ForgeMachineRecipe.ResolveWaterMillilitres(machine, input)`：把固定的 `MillilitresPerOutput` 换成按批计算，返回值同时用于可用量判断与扣水；返回非正值或抛错按 `water-rule-invalid` 失败且不动任何状态。声明该回调时允许 `MillilitresPerOutput` 为 0，未声明时仍要求大于 0。
- 液体模式在扣料前逐件调用 `PrepareOutput(machine, input, batchInputs, product)`：此时产物已生成并落入输出区，电量、水位与投入物尚未改动；回调抛错则召回产物并恢复水位与电量，内容方可以在产物上灌装液体并校验读回。
- `ForgeMachineRecipe.ProgressVersion`（默认 1，必须 ≥1）：写入液体进度记录时一并落盘。读到版本不符、或来自更早记录格式（记录格式 `LiquidProgressVersion` 由 1 升到 2）的进度时，Forge 丢弃记录、清空标签并从槽内投入重新开批；清空失败记 `liquid-progress-clear-failed` 且保持不动。取不到工件时不再因旧记录锁死投入物（`Damaged` 仍保持原锁死行为）。
- `MachineCatalog.TryFreeze` 校验相应组合：`NightlyItems` 上声明按批水量按无效注册处理，`NightlyLiquid` 缺少水规则或 `ProgressVersion < 1` 同样无效。
- `MachineCatalogChecks` 增加按批水量（可配 0 毫升下限）、追加注册沿用、`Water = null` 拒绝与 `ProgressVersion = 0` 拒绝的断言。

## 改动（合成扩展 0.6.0）

- 新增物品 `nicokobo.mechcore.synthesis.biomass_canister`（生物质罐，C08，1×1，基础价值 30）：工厂克隆原版液体容器模板 `large_bottled_water`，从而继承原生液体容器标签与容量；图标沿用生物凝胶图案（`Assets/Icons/biomass_canister.png`）。
- K04 改名液体灌注塔，**物品 ID 改为 `…liquid_infusion_tower`**（C# 成员 `InfusionTowerItem`），配方 P10/P16/P17 改为：物料×1 + 水项 `Basis=PerInputValue`、`Amount=1`、最低水质基础水 → 生物质罐×1，灌装量 = 投入的总价值（1 点价值 = 1 ml）。营养果保持整叠投入并按合并价值灌装、整叠一次用完。三条配方 ID 由 `recipe.bio_gel_from_*` 改为 `recipe.biomass_from_*`。
- 灌装走新增的液体 `PrepareOutput`：`WaterHelper.GetCurrentCapacityML` 先校验罐容量，再 `WaterHelper.AddLiquid(product, "protein", ml×1000)` 并校验 `GetTotalVolume` 读回；读回不符或容量不足直接抛错，整夜回滚（不扣料、不扣水、不扣电）。
- 去掉跨夜进度模型：`RecipeDefinition.SupportsPartialProcessing`、`ProductionRules.SpentUnits`、`BioProgress.RemainingValue/Matches`、`RationalValue`、`CraftPlan.RetainedMeat/CompletesMeat/BioProgress*` 与 `CraftPlanning` 的 `activeProgress` 分支一并移除；`BatchNumbers` 增加 `OutputMillilitres`，新增 `BioVolumeMillilitres` 与 `InfusionWaterMillilitres`（按价值计水），`BioProgress` 目标恒为 1 只罐子。整叠投入在领域模型里也按整叠取用（`InputSelectionRules` 对 `ConsumesStackedInput` 的物品取整叠全部可动件）。
- K05 改名全域制造终端（曾用显示名量子制造机），**物品 ID 改为 `…universal_manufacturing_terminal`**（C# 成员 `UniversalManufacturerItem`）；G05 改为全域制造手册，**ID 为 `…universal_manufacturing_guide`**；机器说明与手册文案改为“配方驱动、可制造配方表登记的任意物品”。物品来源标签改为 `liquid_infusion`／`universal_manufacturing`，机器、手册与图标资源名跟随新 ID（`Assets/Icons` 与 `IconSources` 同步改名，`tools/` 下的图标脚本同步）。
- **移除生物凝胶（C07）**：物品定义、图标、文案与引用全部删除（材料与组件 11→10、物品总数 27→26），旧存档里的存量成为未知物品。
- 配方配置升到 `SchemaVersion=5` 并**删除全部迁移与兼容代码**（旧 schema 分支、`*.bak` 备份、补配方、补纯度门槛、灌注规则重写、啤酒瓶 1→1 修补）：`Parse` 只接受当前 schema 且必须列全内置清单（`RecipeDefinition` 的 `Id`／`Code`／`MachineId` 一致、条数相等），`Initialize` 不再改写文件、不再生成备份。`DefinitionVersion` 升到 5，`recipes.default.json` 同步重新生成。
- 依赖门改为 Forge ≥ 0.3.9；`NativeContractProbe` 增加 `WaterHelper.AddLiquid(GameItem, string, int)` 的签名核对。

## 改动（伪人 Mod 0.1.6）

- 人工心脏（生物质罐×2）、拟态皮肤（×4）、合成声带（×2）、身份伪装模块（×3）四条配方把生物凝胶换成生物质罐，件数不变；物品说明同步改名全域制造终端。
- 目标机器归属固定：新增 `Domain/SynthesisMachineTarget`（`OwnerId` + `MachineId` + `IsOwnedBySynthesis`），追加配方目标写为 `nicokobo.mechcore.synthesis.universal_manufacturing_terminal`；伪人 Mod 只按 ID 追加配方，不注册、改名或删除该机器，领域检查新增 3 条断言（目标等于该终端 ID、ID 仍属合成扩展前缀、绝不指向本 Mod 自己的机器）。README 里过时的 “Mechanical Manufacturer” 字样也改为该终端并注明物品 ID。生物凝胶在合成扩展中已彻底移除。

## 核对与验证

- `scripts/Build-P0.ps1 -GameDir "F:\SteamLibrary\steamapps\common\Probably Stolen Demo"`：领域检查（含新增按批水量与进度版本断言）、核心、P0 示例与物流、工坊、效果示例 Release 构建通过。
- `mods-melonloader/Build-ForgeMods.ps1` 整链通过：伪人 422 条、模块矩阵 89 条、物流 L0=21/resource=37/power=27、合成扩展 603 条领域检查全部通过；伪人 Mod Release 编译通过；三个 Mechcore 包生成且共享同一份 Forge。
- 合成扩展 603 条断言覆盖：灌装量 = 价值（生肉 120 → 120 ml、小块生肉 60 → 60 ml、营养果整叠合并价值）、按价值水耗与水质门槛（基础水可加工、未知水质拒绝、水量差 1 ml 整批不动手）、罐容量/输出区满时的失败路径、整叠一次用完、K01 精炼与熔炉配方不受影响、旧 schema 与不完整清单的拒绝路径（文件字节不变、无备份、回退内置默认）、末版默认文件与编辑数量的加载、手册与物品文案，以及四台机器与罐子的显示名、物品 ID 与原生模板。
- 两个仓库 `git diff --check` 无空白错误（仅 LF/CRLF 提示）。

本地发布产物：

| 文件 | SHA-256 |
| --- | --- |
| `Nicokobo.Forge.dll`（0.3.9，源码 Release 与发布包一致） | `45BF5DE93B918D2FCF77D0596F112D53606903C15E39B5C334E23596D060DBBD` |
| `MechcoreProtocol.SynthesisExpansion-0.6.0.dll` | `6CBD3CCBEB6CC9328011B9CE6BC1848554AF0ABFF3454C70C808154214570057` |
| `recipes.default.json`（SchemaVersion 5） | `CC5EA55930B669204FCC6413A5C973FF706927B38F7F0D34951F2B707AC7FB2A` |
| `AugMechanicalAscension.dll`（0.1.6，本地 Release 构建） | `3F2DD92BB9E0890288C9E25FB098406AD71A160CC6712730AECB4242A0CA4F8F` |

发布目录：`D:\workzone\probably-stolen\mods-melonloader\dist\mechcore-protocol-synthesis-expansion`（含 README、change.log、recipes.default.json、CHECKLIST.md）。伪人 Mod 不在打包脚本的发布列表中，产物位于 `mods-melonloader\aug-mechanical-ascension\bin\Release`。

## 安装记录

10-01 02:31 按用户请求把本轮候选装入 `F:\SteamLibrary\steamapps\common\Probably Stolen Demo\Mods`，安装时游戏未运行；被替换的文件与旧配方备份到 `D:\workzone\probably-stolen\_build\backups\synthesis-universal-terminal-20261001-0231`：

| 文件 | 安装前（已备份） | 安装后 |
| --- | --- | --- |
| `Nicokobo.Forge.dll` | `9E4FA16F391A5F5E…`（0.3.7） | `45BF5DE93B918D2F…`（0.3.9） |
| `MechcoreProtocol.SynthesisExpansion-*.dll` | `912BE648DDE7EBBA…`（0.5.22） | `6CBD3CCBEB6CC932…`（0.6.0） |
| `AugMechanicalAscension.dll` | `F6B5F46C1BB1CB73…`（0.1.5） | `3F2DD92BB9E08902…`（0.1.6） |
| `UserData/MechcoreProtocol.SynthesisExpansion/recipes.json` | `C2CB0B25F57BF7CC…` | 已移出，下次启动按当前默认重建 |

- 旧版 `MechcoreProtocol.SynthesisExpansion-0.5.22.dll` 备份后从 `Mods` 顶层删除，顶层现在只有一份 Forge、一份合成扩展与一份伪人 Mod；`Mods\rollback\**` 里的历史 Forge 副本不在加载路径（上次启动日志只加载顶层 DLL）。
- `UserData/MechcoreProtocol.SynthesisExpansion` 目录里仍留有更早轮次的 `recipes.json.before-*.bak`，本次未清理。
10-01 02:34 按用户请求追加安装模组矩阵（同一次会话，游戏未运行）：`MechcoreProtocol.ModuleMatrix-1.0.1.dll` 由 `10E09682C04EBCFC…` 换成本地 Release 构建 `7EDEC37A5E32162E…`（版本号仍是 1.0.1；本地源码相对该发布版还有 `Integration/ModuleIconRuntime.cs` 与工程文件的改动），旧 DLL 备份到 `D:\workzone\probably-stolen\_build\backups\module-matrix-20261001-0234`。物流 Nexus 的本地构建与本机已装的 `50B3BA89E3782593…` 也不同（`dist` 为 `0165416C527E39BA…`），本轮未安装。

- 上次启动（02:27）仍是合成扩展 0.5.22 + Forge 0.3.7；安装后尚未启动，加载日志、过夜产出与读档都还没有本轮证据。

## 证据边界

本轮已装入游戏但尚未启动，没有任何游戏内证据：`WaterHelper.AddLiquid` 能否在“克隆自原版容器的自定义物品”上成功灌装、`LIQUID_CONTAINER_CAPACITY` 的实际数值、灌装后物品在存档与读档中是否保留液体、以及游戏 UI 是否会显示罐内液体，全部只能靠实机确认。静态结论只到签名核对、注册校验与领域规则：容量不足或读回不符会抛错并让整夜回滚，因此失败表现应为“机器不动手”而不是丢料。K04 旧存档若留有 schema 4 的未完成进度，本轮会因 `ProgressVersion=2` 与记录格式升级被丢弃并从槽内投入重新开批。

## 下一次实机核对

1. K04 投入标准生肉×1 + 基础水容器：产出一只生物质罐、扣 120 ml 水，罐内 120 ml；读档后毫升数与液体种类不变。
2. 小块生肉×1：罐内 60 ml、扣 60 ml；营养果整叠（9 个单价 12）：罐内 108 ml、整叠一次用完、扣 108 ml。
3. 记录 `large_bottled_water` 模板的实际容量；投入价值超过容量时机器整批不动手，投入物与水位不变。
4. 水质门槛：基础水可加工，未知水质或非水液体不加工；水量比灌装量少 1 ml 时不加工。
5. 质量 0/99/100/200 的灌装量与扣水量都等于价值 ml（质量不改变毫升数）。
6. 全域制造终端：六条本扩展配方与七条伪人配方照常产出；四条生物材料配方按生物质罐件数消耗。
7. 启动日志确认新配置：加载 20 条配方（`SchemaVersion 5`）、手册显示新文案；若沿用上一候选的 `recipes.json`，日志应给出 `WARN` 并整份回退到内置默认（文件不被改写、无备份文件）。
8. 旧存档清理：上一候选存档中的旧 ID 机器（`bio_synthesizer`／`mechanical_manufacturer`／`mechanical_manufacturing_guide`）与生物凝胶显示为未知物品，需在新存档复测全部机器；确认新 ID 的 K04/K05/G05 正常放置与运行。
