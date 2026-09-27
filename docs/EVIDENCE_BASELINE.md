# Nicokobo Forge：证据基线与接口探针

日期：2026-09-26。本文件记录方案所用的本地证据及证据级别；方案中的拟建 API 不等于游戏已有 API。

## 目标构建

游戏位置：`F:\SteamLibrary\steamapps\common\Probably Stolen Demo`。本次只读核对了下列 SHA-256；它们与现有 `AugMechanicalAscension.Integration.BuildProfile` 的前两项常量一致。

| 文件 | SHA-256 |
| --- | --- |
| `GameAssembly.dll` | `3BEA17EEEC77ADAB6418918C28A8AA9A06F290582A2972639A48BD7E5A01AB44` |
| `MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll` | `9618787115686C0AF6DBC3B65BEE6DEA9DD33A8A998D01572CDC190A42657ACC` |
| `Probably Stolen_Data/il2cpp_data/Metadata/global-metadata.dat` | `EFDCA95772FDCE327B51E0C7E8D320E7055917BE5C669DB47C1F7B36389F1F41` |

解包签名位置：`D:\workzone\probably-stolen\tools\Il2CppDumper\out_049-Rev5-L_20260923_b25382790\dump.cs`。方法行为参考：`D:\workzone\probably-stolen\output\aug-mechanical-ascension-p0-isil\IsilDump\Assembly-CSharp\`。`dump.cs` 可证明成员声明、字段类型及候选调用点，不能单独证明游戏实际调用顺序或补丁有效。旧 RVA 与字段偏移不进入公共 API。

## 接入证据表

| 领域 | 当前本地证据 | 用于 Nicokobo Forge 的结论 | 尚待验证 |
| --- | --- | --- | --- |
| 原生目录 | `dump.cs:2918-2926` 的 `ItemDirectory.InitDirectory`/`Add`，`:7390` `MiscItemDirectory`，`:8024` `ModuleDirectory`，`:7859` `ModItemDirectory` | 为不同物品种类建目录适配器；注册先检查现有 ID | 具体目录初始化顺序、重建次数、晚注册是否影响读档 |
| 物品实例 | `aug-mechanical-ascension/Integration/NeuralModuleDirectory.cs` 从 `node_small` 模板创建、查重、保持 IL2CPP 工厂委托，并检查形状、类型和效果槽 | 抽取工厂持有、模板隔离与工厂结果自检 | 所有拟支持模板的完整克隆语义；某些目录是否可复用同一模式 |
| 效果 | `dump.cs:20219-20229` 的类型/委托字段，`:20525-20575` 的静态字典、创建、重算、工作和随机效果入口 | 将注册、随机分发、计算回调、成功工作回调拆成不同能力 | 原版及第三方效果与 Nicokobo Forge 效果的执行顺序、随机池隔离策略 |
| 模组/节点 | `MODULE_NODE_EFFECT_SYSTEM.md` §1、§3–7；`ModuleEffectHelper.InitNode` 和 `ModuleHelper.InitModuleItem` 在 dump 中存在 | `Module` 与 `Node` 是同一物品体系的两种类型；定义层共享物品模型 | 复杂邻接、转换、隔离、熔断的原生贡献边界及混合舱行为 |
| Perk | `dump.cs:29945-29960` 的 `StartingPerk` 可保存字段，`:30110-30119` 的列表，`:29730-29803` 的点数、格数与 UI 方法 | 定义、点数、可选数量、选择校验使用同一规则服务 | `OpenUI`/`OnChange`/`CanStart` 的准确重算顺序，负费用和互斥在多 Mod 下的表现 |
| 既有开局框架 | `D:\workzone\custom-start-framwork\framework\CustomStartProfile.cs`、`PerkPatches.cs`、`Plugin.cs` | 已有配置驱动的原生职业附加 Perk、柜台物品与数值增减；先做兼容桥与对照样例 | 与 Nicokobo Forge 同时安装时的 UI、点数和发放所有权冲突；版本描述与程序集实际版本以实测为准 |
| 独立开局 | `dump.cs:31268-31328` 的 `StartType`/`NewGameData`，`:32000-32015` 的 `StartNewGame`/`InitialSave`/`SaveGame`；`aug-mechanical-ascension/Integration/PlayableStartFlow.cs` 包含 55 号接入 | 用稳定业务 ID 隐藏原生编号，选择意图要贯穿菜单与首次保存 | 55 号路线及候选原生承载路线是否安全；新槽索引副作用、失败清理、读档可见性 |
| 存档 | `dump.cs:31660-31671` 的 `PlayerStore.modData`、`runID`、`saveSlotId`；`aug-mechanical-ascension/Persistence/ModDataWriter.cs` 与 `Es3SlotReadback.cs` | 候选周目数据位置与运行身份；应用后须原生保存并从目标槽读回 | 多 Mod 共享字典的限额、保存时序、崩溃/部分写入对账 |
| 初始物品 | `aug-mechanical-ascension/Integration/OpeningGrantNativeAdapter.cs` 的库存快照、蓝图检查、槽位检查、写后数量和实例身份核对 | 将发放拆为预检、准备、接纳、读回、提交 | 此候选适配器的调用边界和完整原生新局闭环仍须实机验证 |
| 生命周期 | `dump.cs:69268-69293`、`:69643-69670` 的 `ModHook` 目录、顾客生成、游戏加载事件 | 优先评估游戏原生事件，缺口使用局部 Harmony 补丁 | 事件真实触发阶段、异常隔离和与 MelonLoader 顺序的关系 |
| 状态 UI | `aug-mechanical-ascension/UI/StatusOverlay.cs` 是基于周目状态的只读 IMGUI 叠层；`dump.cs:44583` 的 `AdvCalendarUIManager` 有状态按钮 | 状态数据层可参考，展示层先做独立面板探针 | 用户所指左上角看板的真实对象、刷新入口和可注入区域尚未定位 |
| NPC | `dump.cs:39315-39379` 的 `StoreClientManager` 与 `:69276-69284` 顾客生成事件 | NPC 作为后续独立能力，区分定义、当日队列和实例 | 自定义顾客的模板、对话、生成权重、存档和复访规则 |

## 已有行为与限制

`probably-stolen/docs/MODULE_NODE_EFFECT_SYSTEM.md:348-360` 记载了神经接口物品、原生存档回读和局部效果的先前可丢弃档观察，也列出随机节点和多个原生效果尚未直接实测。该记录支持优先用神经接口做 Nicokobo Forge 内容样例，不能推出 Nicokobo Forge 当前已可运行。

`custom-start-framwork` 的项目 README 称 `v1.0.2`，而 `framework/Plugin.cs` 中的 `MelonInfo` 字符串为 `1.0.1`。迁移或互操作前先检查实际 DLL 的程序集身份与加载日志，方案不把文档字符串当作唯一版本依据。

方案形成时没有游戏行为测试。后续 P0 曾临时安装只读诊断包并尝试启动游戏，但未进入 MelonLoader 加载阶段；对照启动得到相同结果，详见 [启动探针记录](probes/2026-09-26-startup-attempt.md)。因此目录重建、首次存档失败恢复、Perk 重算、混合 Mod 排序、左上角看板定位、NPC 创建与保存仍须实机验收。

## P0 探针清单与记录格式

每个探针记录：游戏三项哈希、MelonLoader/Interop 版本、目标完整签名、补丁 owner、回调时间线、输入实例 ID、存档槽/runID、预期与观察、失败后的状态。把结果写入 `docs/probes/`，按能力更新 `Verified / Partial / Disabled`；不得仅凭编译成功改为 `Verified`。

1. **目录与效果**：同一进程内新局、读档、换槽各触发多少次目录初始化；用两个测试 Mod 注册相邻 ID，试一次重复 ID 和一次缺失效果。验证随机池未混入禁用效果。
2. **机器工作**：对一件测试模组记录添加、重算、成功工作、失败工作、旋转、取出顺序与次数；确认回调是否重入。机器类型至少覆盖扩展模组计划里 P0 所列的目标机型。
3. **Perk**：打开/关闭面板、选择/取消正负费用 Perk、切换开局角色并确认；同时读取内部预算、界面值和实际确认资格。
4. **新局与存档**：菜单取消、新局提交、首次存档失败、重进、满库存、换槽、缺失内容 Mod；用可丢弃档读回并与操作流水比对。先备份既有测试档，再执行任何写入试验。
5. **UI 与 NPC**：定位左上角看板树/刷新方法；验证叠层、窗口重开和切换场景；在后续阶段探测顾客生成事件和同日重复进入。
