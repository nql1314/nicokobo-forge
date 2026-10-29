# Demo Forge 开发与关联构建

本仓库维护 Demo IL2CPP / MelonLoader 框架。正式版公共层位于关联仓库的 [mods-official/nicokobo-forge](../../probably-stolen/mods-official/nicokobo-forge/README.md)，其构建与验收从正式版文档进入。

Forge 的职责与事件契约见 [API_BOUNDARIES.md](API_BOUNDARIES.md)。Demo 核心和内容调用者需使用配套的 Forge DLL。`scripts/Build-P0.ps1` 执行互操作元数据签名、领域规则、原生供电适配替身及独立原生入口保护检查，再编译核心与示例；合成扩展单独核对熔炉与液体 Hook。检查使用合成元数据、图形结构和故障注入端口，不启动游戏；数量与结果统一见[当前进度](FORGE_PROGRESS.md)。

## 职责与数值入口

Forge 持有通用声明、能力门控、原生适配和事件分派。物品内容、配方数值、奖励交易和资源由内容 Mod 持有。图集加载、ES3 文件读取和原生入口检查已提取为 Forge API，接入见 [SHARED_SERVICES.md](SHARED_SERVICES.md)。图片嵌入其所属内容程序集。

框架可调整数值集中在 [`BuildConfig/ForgeNumbers.cs`](../BuildConfig/ForgeNumbers.cs)，机核三个组件的内置数值集中在关联仓库的 `mods-melonloader/BuildConfig/`；食品加工数值在其 `Definitions/FoodNumbers.cs`，小偷规则与目录在其 `Domain/` 和 `Data/`。这些源码随所属 DLL 编译，无新增运行时设置；分类、单位与构建方式见[编译内置数值](../BuildConfig/README.md)。

## 机器职责

| 文件 | 职责 |
| --- | --- |
| `Plugin.cs` | MelonLoader 生命周期与日志适配 |
| `ForgeBootstrap.cs` | 安装各功能并发布能力门控 |
| `ForgeMachineRegistrationApi.cs` / `ForgeMachineModels.cs` | 公共声明入口和数据模型 |
| `ForgeManufacturingTerminal.cs` / `ForgeManufacturingTerminalRuntime.cs` / `ManufacturingTerminalSaveMigration.cs` | 内置共享终端参数、本体注册及旧原生存档节点迁移；配方由内容 Mod 追加 |
| `Registration/MachineCatalog.cs` | 校验、整批冲突检查、不可变配置和投料索引 |
| `ForgeMachineHooks.cs` | 自定义机器订阅公共读档／过夜事件 |
| `ForgeMachineBatchProcessor.cs` | 内容方显式调用的通用批次事务；不注册机器、不安装玩法 Hook |
| `ForgeMachineUi.cs` | 原版 UI 组件、可配置输入输出仓、电池、模组及本机槽位回调 |
| `ForgeMachineRuntime.cs` | 机器发现、批次选择及统一资源计划 |
| `ForgeLiquidApi.cs` / `MachineBatchMath.cs` | 原生液体快照、毫升／组分计算、液体价值保留、电池计费 |
| `ForgeLiquidValueRuntime.cs` / `LiquidValueLedger.cs` | 容器变更／倾倒／读取钩子；液体价值账本的保存、解析与按比例重算 |
| `NativeProductionValue.cs` | 原生内在／最终价值阶段与产物价值合成适配 |
| `ForgeProductionValueApi.cs` / `ForgeProductionValueMath.cs` | 逐批生产价值汇总、加工倍率折算及产物基础价值的标签写入／读取 |
| `ForgeMachineTransaction.cs` / `MachineTransaction.cs` | 统一资源事务、写后校验、反向恢复及错误诊断 |

投料判定使用注册时建立的索引；夜间扫描直接检查目录是否为空，发现机器时保留同次配置。输出预处理没有回调时不构造投入展开列表。固体和液体共享原生步骤，各自保留完整回滚。扣料、耗电、耗水和保存所需的逐批重检与读回不能以性能优化为由删除。

## Demo 检查、打包与安装

普通源码改动只编译相关项目并执行必要检查。核心和内容调用者使用配套的 Demo Forge DLL，直接构建时传入 Demo 游戏目录：

```powershell
dotnet build .\src\Nicokobo.Forge\Nicokobo.Forge.csproj -c Release -p:GameDir="F:\SteamLibrary\steamapps\common\Probably Stolen Demo"
```

需要完整既有检查与示例构建时，使用 `scripts/Build-P0.ps1 -GameDir <Demo游戏目录>`；工坊和效果探针另外加 `-IncludeProbes`。`-DefaultLogLevel` 可显式覆盖本轮编译日志级别，未指定时为 `INFO`。这些构建入口不打包或安装。

关联内容仓库中的 `Build-ForgeMods.ps1` 和 `Build-MechcoreProtocol.ps1` 已移除。正式版使用其根目录 `Build-Mods.ps1`，见[正式版开发说明](../../probably-stolen/docs/DEVELOPMENT.md)；Demo DLL 与下方 Demo 前置包分别维护。

### 启动时的多版本选择

本机 MelonLoader 0.7.3 的 `MelonPreprocessor` 在加载程序集前汇总本次扫描目录中的候选，按内部 `AssemblyName.Name` 分组并保留最高 `AssemblyVersion`；Forge 与其他 Mod 共用此规则。版本按数字段比较，`0.6.48` 高于 `0.6.9`，不比较文件名、显示名称或修改时间。相同程序集版本保留扫描到的第一份，不保证它是较新的构建；程序集名称不同则分别处理。实现见 [MelonLoader v0.7.3 源码](https://github.com/LavaGang/MelonLoader/blob/v0.7.3/MelonLoader/Melons/MelonPreprocessor.cs)。

Forge 和自维护内容 Mod 的工程 `<Version>` 会生成程序集版本，需与 `MelonInfo` 声明同步，不能把 `AssemblyVersion` 固定为旧值。该选择由加载器在 Forge 初始化前完成，普通 Forge Mod 无需另装去重钩子。安装脚本清理旧 DLL 属于文件整理，自动选版本身不删除或改名任何文件。

单独的玩家前置包使用 `scripts/Pack-ModSite.ps1`，从已验证版本的 Release DLL 更新 `dist/nicokobo-forge/` 和固定文件名的 `dist/nicokobo-forge.zip`。安装说明由 `docs/RELEASE_README.md` 模板生成，版本从实际 DLL 读取；默认输入同时校验核心工程版本，`-ForgeDll` 可选择与内容 Mod 配套的明确 DLL。发布目录内维护的 `change.log` 等文件随目录一起压缩，ZIP 保留 `nicokobo-forge/` 顶层目录。

同一入口还把可选兼容补丁放进 Forge 包：默认取 `dist/compatibility/nicokobo-compatibility-patches/` 中已发布的 `Nicokobo.CompatibilityPatches-*.dll`，没有打包产物时回退到 `compatibility/Nicokobo.CompatibilityPatches/bin/Release/`，两者都不存在时警告并跳过，`-CompatibilityDll` 可显式指定。该文件按程序集版本命名并清理同前缀旧文件；Forge 本身不自动安装或加载它。

该入口同时在关联 `probably-stolen` 仓库的 `mods-melonloader/dist/` 放置相同的 `nicokobo-forge/` 目录及 `nicokobo-forge.zip`，可用 `-ModDistRoot` 指定同步位置。这是 Demo 历史发布目录的同步行为，不属于正式版打包流程。

内容工程从 `mods-melonloader/Directory.Build.targets` 统一引用本地 Forge 构建产物。单项目构建前先构建 Forge，也可以用 `-p:ForgeAssemblyPath=<配套DLL>` 选择明确的依赖；兼容旧参数 `ForgeDll`，同时传入不同依赖会报错。内容项目不再通过 `ProjectReference` 重建 Forge。

公共 API 签名兼容、领域检查、Release 编译和混淆产物只能验证对应层次。图集发布、原生钩子、夜间加工、失败回滚与存档重载仍须用当前产物在新进程及可丢弃档中验证。
