# Forge 开发与关联构建

Forge 的职责与事件契约见 [API_BOUNDARIES.md](API_BOUNDARIES.md)。核心和所有内容调用者需使用同一份 Forge DLL；机核发布包必须经统一构建与 Obfuscar。`scripts/Build-P0.ps1` 执行互操作元数据签名、领域规则和独立原生入口保护检查，再编译核心与示例；合成扩展单独核对熔炉与液体 Hook。检查使用合成元数据、图形结构和故障注入端口，不启动游戏；数量与结果统一见[当前进度](FORGE_PROGRESS.md)。

## 职责与数值入口

Forge 持有通用声明、能力门控、原生适配和事件分派。物品内容、配方数值、奖励交易和资源由内容 Mod 持有。图集加载、ES3 文件读取和原生入口检查已提取为 Forge API，接入见 [SHARED_SERVICES.md](SHARED_SERVICES.md)。旧图集共享源码及编译开关已移除，图片仍嵌入内容程序集。

框架可调整数值集中在 [`BuildConfig/ForgeNumbers.cs`](../BuildConfig/ForgeNumbers.cs)，四个内容 Mod 的内置数值集中在关联仓库的 `mods-melonloader/BuildConfig/`。这些源码随所属 DLL 编译，无新增运行时设置；分类、单位与构建方式见[编译内置数值](../BuildConfig/README.md)。

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
| `ForgeMachineTransaction.cs` / `MachineTransaction.cs` | 统一资源事务、写后校验、反向恢复及故障隔离 |

投料判定使用注册时建立的索引；夜间扫描直接检查目录是否为空，发现机器时保留同次配置。输出预处理没有回调时不构造投入展开列表。固体和液体共享原生步骤，各自保留完整回滚。扣料、耗电、耗水和保存所需的逐批重检与读回不能以性能优化为由删除。

## 一个入口完成关联检查和本地打包

在 `D:\workzone\probably-stolen` 执行：

```powershell
.\mods-melonloader\Build-ForgeMods.ps1
```

入口先运行 Forge 签名与领域检查，再用一个 MSBuild 会话编译核心和六个示例，核心只构建一次。随后运行机械飞升、模组矩阵、物流脉络和合成扩展的领域检查，编译机械飞升，并调用 `Build-MechcoreProtocol.ps1` 编译和混淆三个机核项目。构建前后及四个内容包内的 Forge 哈希必须相同。默认只生成本地文件；显式 `-Install` 安装合集内的 Forge、合成扩展、机械飞升和模组矩阵四份 DLL。

`-GameDir` 指定目标游戏，`-DistRoot` 指定内容发布目录，`-DefaultLogLevel` 显式选择日志级别。未覆盖时 Forge、合成扩展和物流使用 `INFO`，机械飞升与矩阵使用 `WARN`。参数或环境变量 `ModDefaultLogLevel` 显式覆盖本轮各项目；`DEBUG` 必须显式启用，用户偏好仍优先。

单独验证 Forge 时执行 `scripts/Build-P0.ps1 -GameDir '<游戏目录>'`，默认构建核心与四个示例；加 `-IncludeProbes` 构建工坊和效果探针。它们参与检查，不被加入内容发布包。核心输出为 `src/Nicokobo.Forge/bin/Release/Nicokobo.Forge.dll`；`scripts/Pack-P0.ps1` 另将核心与两个 P0 示例打包到 `dist/p0/`，并同步更新独立 Forge 玩家包。

构建需要能编译 `net6.0` 的 .NET SDK、.NET 6 运行时、本地游戏、MelonLoader 及游戏互操作程序集。脚本使用目标游戏目录的引用，签名核对不初始化游戏类。

单独的玩家前置包使用 `scripts/Pack-ModSite.ps1`，从已验证版本的 Release DLL 更新 `dist/nicokobo-forge/` 和固定文件名的 `dist/nicokobo-forge.zip`。安装说明由 `docs/RELEASE_README.md` 模板生成，版本从实际 DLL 读取；默认输入同时校验核心工程版本，`-ForgeDll` 可选择与内容 Mod 配套的明确 DLL。发布目录内维护的 `CHANGELOG.md` 等文件随目录一起压缩，ZIP 保留 `nicokobo-forge/` 顶层目录。

该入口同时在关联 `probably-stolen` 仓库的 `mods-melonloader/dist/` 放置相同的 `nicokobo-forge/` 目录及 `nicokobo-forge.zip`，可用 `-ModDistRoot` 指定内容发布根目录。`Build-MechcoreProtocol.ps1` 正常打包成功后调用同一入口，将本轮 `-DistRoot` 作为 Forge 独立包的同步位置；`Build-ForgeMods.ps1` 通过该流程同步更新，无需再手动打包 Forge。`-OnlyProject` 沿用该行为，`-ValidateExistingBinaries` 只检查混淆，不写发布目录。

四个内容工程从 `mods-melonloader/Directory.Build.targets` 统一引用本地 Forge 构建产物。单项目构建前先构建 Forge，也可以用 `-p:ForgeAssemblyPath=<配套DLL>` 选择明确的依赖；兼容旧参数 `ForgeDll`，同时传入不同依赖会报错。内容项目不再通过 `ProjectReference` 重建 Forge。

公共 API 签名兼容、领域检查、Release 编译和混淆产物只能验证对应层次。图集发布、原生钩子、夜间加工、失败回滚与存档重载仍须用当前产物在新进程及可丢弃档中验证。
