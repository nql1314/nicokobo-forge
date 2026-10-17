# Nicokobo Forge

0.6.43 审查修复候选新增端点创建凭据。旧 Open 接口兼容；新接口调用者需配套更新 Forge。本候选只在独立副本准备，未安装或进行原生验收。

[English](#english) · [中文](#中文)

## English

Author: Nicokobo · Source version: **0.6.43** · Target: **Probably Stolen Demo, Steam Build `25382790`**

Matching Mechcore Protocol bundle: **1.0.0** (Synthesis 0.9.65, Aug 0.1.62, Matrix 1.1.7).

Forge provides native registration, lifecycle events, machine templates and transactions, dialogue, visits, reading windows, Nico Workshop, image resources and save inspection for content mods. It also owns the Universal Manufacturing Terminal, workshop cards and ten base-game achievements. Content mods own their items, recipes, prices, rewards and story rules.

Built-in workshop cards, achievement records and rewards take effect in the current session and are persisted by the game's normal night save. The workshop never requests an extra save.

Install MelonLoader, close the game and copy the matching `Nicokobo.Forge-<version>.dll` into `Mods/`. Keep one DLL per assembly when several packages include Forge. The separate Wage's Perks 1.3.4 compatibility DLL is optional; see [compatibility](compatibility/README.md).

- [Player features and installation template](docs/RELEASE_README.md)
- [Documentation index](docs/README.md), [scope](docs/SCOPE.md) and [API responsibilities](docs/API_BOUNDARIES.md)
- [Current source and validation boundaries](docs/FORGE_PROGRESS.md)
- [Development and build commands](docs/DEVELOPMENT.md)
- [Developer and AI integration guide (中文)](#开发者与-ai-接入指南): feature selection, setup, lifecycle, persistence and examples.

For integrations, use the public `Forge*Api` classes in the current source. Register declarations before native directory initialization, then inspect application snapshots before using factories. Keep gameplay rules and save schemas in the content mod. `ForgeApi.Register` declares metadata; native items require `ForgeItemApi` factories. Machine recipes use `ForgeMachineRegistrationApi`, including `RegisterAdditionalRecipes` for the shared terminal. Read the linked contracts and sample source before implementing; historical API names and validation reports are not current signatures or runtime guarantees.

Registration acceptance, native application and successful game/save behavior are separate results. `ForgeCapabilities.Current` reports installed adapters; `InventoryTransfer` remains `false`, with narrower contracts for individual move/pour APIs. Historical tests only apply to their recorded binaries.

## 中文

作者：Nicokobo · 源码版本：**0.6.43** · 适用：**Probably Stolen Demo，Steam Build `25382790`**

配套机核协议合集：**1.0.0**（合成扩展 0.9.65、机械飞升 0.1.62、模组矩阵 1.1.7）。

Forge 为内容 Mod 提供原生注册、生命周期、机器模板与资源事务、对话／来访／阅读窗、Nico 工坊、图片资源及存档读取，内置全域制造终端、全开局工坊名片与十项原版成就。内容 Mod 维护物品、配方、价格、奖励和剧情规则。

内置工坊的名片、成就记录与领奖结果在当前会话立即生效，统一随游戏正常过夜存档保存；工坊不触发额外存档。

安装 MelonLoader，关闭游戏后将配套 `Nicokobo.Forge-<版本>.dll` 放入 `Mods/`。多个包附带 Forge 时只保留一份同次构建的 DLL。Wage's Perks 1.3.4 的独立兼容补丁按需安装，见[兼容补丁说明](compatibility/README.md)。玩家功能与完整安装说明维护在[发布 README 模板](docs/RELEASE_README.md)，打包时填写实际 DLL 版本。

| 入口 | 用途 |
| --- | --- |
| [文档索引](docs/README.md) | 当前 API、内容边界、示例与历史证据 |
| [能力与扩展边界](docs/SCOPE.md) | 框架和内容方各自持有的规则 |
| [API 职责](docs/API_BOUNDARIES.md) | 按功能选择接口，核对事件和原生门控 |
| [当前进度](docs/FORGE_PROGRESS.md) | 当前实现、当轮修复与待原生验收范围 |
| [开发说明](docs/DEVELOPMENT.md) | 构建、关联项目及明确请求后的打包／安装 |

普通源码改动默认只完成相关项目编译和必要检查。在 Forge 仓库运行完整既有检查与示例构建：

```powershell
.\scripts\Build-P0.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo'
```

加 `-IncludeProbes` 才包含工坊和效果探针。该入口不打包或安装；发布入口和内容调用者见开发说明。框架内置数值在 [BuildConfig](BuildConfig/README.md)，玩法数值由所属内容 Mod 维护。

`Accepted` 只表示登记接受，`Applied` 才表示进入原生目录。`ForgeCapabilities.Current` 表示适配器安装状态，`InventoryTransfer` 总能力仍为 `false`；单件搬运／倒液使用各自的窄契约。编译、打包、安装、实际游戏操作和存档重载分别验收，旧候选结果不自动覆盖当前源码。

## 开发者与 AI 接入指南

本指南按当前 **0.6.42 源码**组织，面向内容 Mod 作者和协助开发的 AI。先按功能找到公共入口，再阅读对应契约与示例；参数、返回状态和模型以 [src/Nicokobo.Forge](src/Nicokobo.Forge/) 为准。当前构建及实机证据见[进度](docs/FORGE_PROGRESS.md)，不根据版本号推定某项玩法已验收。

### 1. 按需求选择已有能力

| 要实现什么 | 使用入口 | 接入要点与详细说明 |
| --- | --- | --- |
| 声明 Mod、内容依赖 | `ForgeApi.Register` | 元数据声明不创建原生物品；见[API 职责](docs/API_BOUNDARIES.md) |
| 新物品、设施、节点、机器模组或效果 | `ForgeItemApi`、`ForgeEffectApi` | 提供稳定 ID 和原生工厂，检查注册结果与应用快照；名称和说明用 `ForgePresentationApi` |
| 夜店上架、NPC 供货 | `NativeItemOptions.NightShop` / `.NpcTrade` | 声明类别、条件及供货人；由框架适配原生库存生成，购买后不自动补货；见[范围与供货规则](docs/SCOPE.md) |
| 游戏加载、日夜规则、模组效果观察 | `ForgeLifecycleApi`、`ForgeModuleApi`、`ForgeContentApi` | 订阅已有事件，按 owner 注销；事件顺序及原生门控见[事件契约](docs/API_BOUNDARIES.md#事件契约) |
| 自定义机器与物品／液体配方 | `ForgeMachineRegistrationApi.RegisterMachine` | 模板提供真实槽位、原版 UI 和批次资源事务；配方及数值由内容方提供；见[机器 API](docs/MACHINE_API.md) |
| 给现有机器或全域制造终端追加配方 | `ForgeMachineRegistrationApi.RegisterAdditionalRecipes`、`ForgeManufacturingTerminal` | 复用目标机器的配置与批次规则，不重复注册终端本体；见[机器 API](docs/MACHINE_API.md) |
| 原版机器扩展、显式处理一批 | `ForgeMachineRuntimeApi.CreateBatchProcessor` | 调用方负责触发时机、实时槽位及同夜去重；复用资源事务不等于自动获得夜间调度 |
| 配方手册、G 键查询、自动化 | `GuideSnapshot()`、`ForgeRecipeGuideApi`、`ForgeMachineAutomationApi` | 已登记机器配方自动进入手册目录；原版扩展用 `RegisterRecipes` 提交只读展示；自动化边界不增加加工次数 |
| 电量、液体、制造价值 | `ForgePowerApi`、`ForgeLiquidApi`、`ForgeProductionValueApi` | 使用实际资源与批次价值，不用虚拟余额替代真实电池；见[机器 API](docs/MACHINE_API.md) |
| 剧情选择、顾客来访、阅读窗口 | `ForgeDialogueApi`、`ForgeClientVisitApi`、`ForgeReadingApi` | 内容方负责条件、剧情状态及交易；见[对话 API](docs/CONVERSATION_API.md) |
| 电话联系人与多笔供应商会面 | `ForgePhoneApi`、`ForgeSupplierApi` | 内容方负责排期、货单、付款、交付及收据；见[对话 API](docs/CONVERSATION_API.md) |
| 独立工坊页与解锁图谱 | `ForgeWorkshopApi.RegisterTab / RegisterChain` | 条件／奖励声明用于展示，解锁回调重新验资并完成交易；见[工坊图谱](docs/WORKSHOP_GRAPH.md) |
| 读取库存、规划整批奖励落点 | `ForgeInventoryApi`、`ForgeInventoryPlacementApi` | 快照与预检不提交、不预留库存；见[库存契约](docs/INVENTORY_POWER_TRANSACTIONS.md) |
| 搬运一件物品、倒一次液体 | `ForgeItemTransferApi.Move`、`ForgeLiquidTransferApi.Pour` | 检查实际转移量及不确定结果，不把多次独立操作视作整体原子事务 |
| 隐藏库存、完整物品图、列表承载 | `ForgeInventoryEndpointApi`、`ForgeSaveGraphApi`、`ForgeListInventoryApi` | 工厂维护固定拓扑，未知图停止处理；临时锁用 `ForgeInventoryFreezeApi`，附加准入用 `ForgeInventoryAdmissionApi` |
| 多源供电、内容状态参与批次事务 | `ForgeExternalPowerApi`、`IForgePowerTransaction`、`IForgeBatchTransaction` | 网络范围、唯一额度及业务状态由内容方持有；见[库存与供电事务](docs/INVENTORY_POWER_TRANSACTIONS.md) |
| 原生付款、物品价格与提示 | `ForgeRunPurchaseApi`、`ForgeItemInspectionApi` | 按公共契约接入付款和只读展示，交付及业务收据仍由内容方管理 |
| 开局身份与周目 JSON | `ForgeStartApi`、`ForgeRunDataApi.Read / Stage` | 身份认领不创建完整开局；`Stage` 只修改内存中的所属数据 |
| 图集、立绘、存档读回、Hook 入口检查 | `ForgeAssetsApi`、`ForgeSaveReadbackApi`、`ForgeHookApi` | 使用所属资源和有界只读解析；见[公共服务](docs/SHARED_SERVICES.md) |

Forge 自带全域制造终端、Nico 名片与十项原版成就、开局选择栏滚动、16×16 档案箱及 G 键合成表。内容 Mod 通过扩展入口接入，具体内置行为见[玩家功能](docs/RELEASE_README.md)。缺失物品恢复仅覆盖已有来源记录且确认提供者 DLL 移除的合格 Forge 物品，不能代替内容 Mod 自己的存档迁移。

### 2. 建立项目与依赖

需要能编译 `net6.0` 的 .NET SDK、.NET 6 运行时，以及安装 MelonLoader 并已有互操作程序集的目标游戏。示例工程继承仓库根目录的 [Directory.Build.props](Directory.Build.props)；复制到其他仓库时也要显式配置目标框架、可空性和所需游戏引用。

在 Forge 仓库仅编译核心：

```powershell
dotnet build .\src\Nicokobo.Forge\Nicokobo.Forge.csproj -c Release '-p:GameDir=F:\SteamLibrary\steamapps\common\Probably Stolen Demo'
```

输出为 `src/Nicokobo.Forge/bin/Release/Nicokobo.Forge.dll`。内容 Mod 引用与运行时配套的这份 DLL；外部工程可添加以下引用片段，`ForgeAssemblyPath` 由调用方指定：

```xml
<ItemGroup>
  <Reference Include="Nicokobo.Forge">
    <HintPath>$(ForgeAssemblyPath)</HintPath>
    <Private>false</Private>
  </Reference>
</ItemGroup>
```

该片段只配置 Forge 引用；完整 MelonLoader 和游戏引用可参考[基础示例工程](samples/Nicokobo.Forge.ExampleOne/Nicokobo.Forge.ExampleOne.csproj)。关联 `probably-stolen` 内容工程已有统一引用配置，通过 `-p:ForgeAssemblyPath=<配套DLL路径>` 选择依赖，不再额外添加重复引用。部署时 Forge DLL 独立放在 `Mods/`，不要把框架源码复制进内容程序集。

### 3. 从声明到游戏运行

1. 确定稳定 owner，例如 `example.my_mod`；物品、回调、配方和存档键使用所属命名空间，发布后不随显示名修改 ID。
2. 在 Mod 初始化阶段提交声明和工厂，在原生目录初始化前完成注册。此时保留工厂回调，不提前创建依赖原生目录的实例，也不强取尚未就绪的 `PlayerStore`。
3. 检查每次注册的返回状态与原因。`ForgeApi.Register` 的 `Define` 只登记元数据；实际物品另走 `ForgeItemApi.RegisterItem / RegisterAmenity / RegisterNode / RegisterModule`，效果走 `ForgeEffectApi`。
4. 通过 `ForgeContentApi` 通知或对应 `Snapshot()` 确认原生应用状态，再使用工厂。冲突时保留已有原生对象，停用本功能；当前不保证目录初始化后的迟到注册立即生效。
5. 在动作发生时读取 `ForgeCapabilities.Current` 的相关门控及实时对象。生命周期上下文只用于本次回调；不把原生句柄跨周目缓存，写入前重检归属、容量和资源。
6. 保存订阅返回的 `IDisposable`，在功能退出或初始化失败时释放。优先复用公共生命周期和模块观察入口，新增专用 Hook 前确认现有 API 无法覆盖需求。

最小元数据声明可直接参考 [ExampleOne](samples/Nicokobo.Forge.ExampleOne/Plugin.cs)：

```csharp
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

// 放在内容 Mod 的初始化方法内；本例不生成原生物品。
var result = ForgeApi.Register("example.my_mod", "0.1.0", batch =>
{
    batch.Define(DefinitionKind.Item, "starter", "demo=metadata-only");
});
// 在 MelonMod 中记录结果，并处理被拒绝的声明。
LoggerInstance.Msg($"registration={result.Status}; {result.Reason}");
```

需要真实机器时，从 [MachineExamples.cs](samples/Nicokobo.Forge.MachineTemplates/MachineExamples.cs) 的声明接入 `RegisterMachine`，由自己的插件入口调用。追加配方使用当前 `RegisterAdditionalRecipes` 签名；历史记录中的 `ForgeMachineApi`、`ForgeNativeApi` 等名称不能直接当作当前公共类名使用。

### 4. 资源提交与保存

机器模板共用材料、液体、电量和产物的批次提交／补偿。配方条件用于当前机器及资源判定，不能在展示或条件查询里偷偷扣费、抽奖励或修改库存。内容方自定义工坊、剧情和供应商交易时，负责检查重复请求、重新验资、交付、失败恢复与持久化。

| 观察到的结果 | 可以说明什么 |
| --- | --- |
| 注册 `Accepted` | 声明已接受；尚不证明原生目录已有工厂 |
| 应用快照 `Applied` | 已观察到对应声明进入原生目录 |
| 相关能力门控为 `true` | 适配器已安装；不证明单个请求或存档成功 |
| 库存预检通过 | 当时可放置；并未预留位置或实际交付 |
| `Stage` 返回 `StagedInMemory` | 所属 JSON 已暂存于当前周目内存，尚未落盘 |
| 生命周期 After 触发 | 原方法主体执行过；不保证保存文件成功 |
| ES3 读回并核对业务字段 | 指定文件含预期结果；新进程恢复仍需单独验证 |

`Stage` 应使用读取到的旧 JSON 做比较，同时绑定当前 run ID 与槽位；遇 `Conflict` 或 `Indeterminate` 先核实实际状态，不盲目重复扣款或发奖。物品搬运及倒液同样按返回状态与两端实际量处理。`InventoryTransfer` 总能力仍为 `false`，不能据单次转移 API 推断任意整箱或整网事务已支持。

存档读取用 `ForgeSaveReadbackApi`，内容方核对目标文件、周目、槽位、业务收据及实际物品归属。`ReadInventoryItems` 读取指定原生库存图中从根可达的物品；调用方先选真实库存字段，排除 `soldInvJSON` 等非持有记录。不能用“在存档任意位置搜到 UID”作为仍持有物品的证据。完整约束见[公共服务](docs/SHARED_SERVICES.md)。

### 5. 选择示例与验证范围

| 示例 | 学习内容 | 运行方式 |
| --- | --- | --- |
| [ExampleOne](samples/Nicokobo.Forge.ExampleOne/Plugin.cs) / [ExampleTwo](samples/Nicokobo.Forge.ExampleTwo/Plugin.cs) | 元数据声明、跨 Mod 依赖 | MelonLoader 示例；不会生成示例原生物品 |
| [MachineTemplates](samples/Nicokobo.Forge.MachineTemplates/README.md) | 物品与液体混合输入、纯液体输入、容器输出 | 可编译库，无插件入口，由内容方调用 |
| [LogisticsExtension](samples/Nicokobo.Forge.LogisticsExtension/) | 路由、配方目录与配置接入 | 内容侧示例库，不安装到游戏 |
| [WorkshopProbe](samples/Nicokobo.Forge.WorkshopProbe/README.md) | 工坊页面与图谱回调 | 诊断插件，构建需 `-IncludeProbes` |
| [EffectRegistrationProbe](samples/Nicokobo.Forge.EffectRegistrationProbe/README.md) | 原生效果注册 | 诊断插件，构建需 `-IncludeProbes` |

普通开发先执行相关项目编译与针对性既有检查；修改公共核心行为或跨模块契约时使用上文 `Build-P0.ps1`。该脚本检查互操作签名、领域规则和入口保护，再编译核心与四个示例，不启动游戏；加 `-IncludeProbes` 后另编译两个探针。纯文档修改核对链接、签名和差异即可。

打包与安装按[开发说明](docs/DEVELOPMENT.md)另行执行。关联仓库的 `Build-ForgeMods.ps1` 默认会安装，只有 `-NoInstall` 才跳过安装，不应拿默认调用作为普通编译命令。原生验收使用当前候选、新进程和可丢弃存档，分别记录实际行为、失败恢复、文件读回和跨进程重载。

### 6. 给 AI 的任务上下文

给 AI 下达内容开发任务时，可复制以下内容并补充需求：

```text
在当前 Nicokobo Forge API 上实现以下内容 Mod 功能：<需求与完成条件>。
先读 Forge README 的接入指南，再按功能查 docs/API_BOUNDARIES.md 和对应专题文档。
核对 src/Nicokobo.Forge 中的实际公共签名、模型、返回状态及 samples；不从历史记录猜 API。
先检查工作区现有修改并保留它们。优先复用已有 API；玩法、数值、资源与存档格式由内容 Mod 持有。
说明使用哪些入口、注册时机、所需能力门控、状态归属及失败处理，然后实施。
涉及交易时重检周目/槽位、所有权、容量和资源，处理重复请求与不确定结果。
Accepted、Applied、Stage、保存文件读回和新进程重载分别判断，不互相替代。
只运行与改动相关的既有检查；默认完成相关项目编译，不自动打包、安装或启动游戏。
交付列出改动、检查结果和仍未验证的行为。
```

MIT License.
