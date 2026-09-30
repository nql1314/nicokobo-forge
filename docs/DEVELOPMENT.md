# Forge 开发与关联构建

Forge 持有通用声明、能力门控、原生适配和事件分派。物品内容、配方数值、奖励交易和资源由内容 Mod 持有。内容项目的图集加载工具位于 `probably-stolen/mods-melonloader/shared/EmbeddedSpriteAtlas.cs`，不属于 Forge API。

## 机器职责

| 文件 | 职责 |
| --- | --- |
| `Plugin.cs` | MelonLoader 生命周期与日志适配 |
| `ForgeBootstrap.cs` | 安装各功能并发布能力门控 |
| `ForgeMachineApi.cs` / `ForgeMachineModels.cs` | 公共声明入口和数据模型 |
| `Registration/MachineCatalog.cs` | 校验、整批冲突检查、不可变配置和投料索引 |
| `ForgeMachineHooks.cs` | 原生回调、钩子安装和机器工厂适配 |
| `ForgeMachineRuntime.cs` / `ForgeMachineLiquidRuntime.cs` | 机器发现、固体与液体加工事务 |
| `ForgeMachineTransaction.cs` | 共用产物创建、放置、动作通知及电量恢复 |

投料判定使用注册时建立的索引；夜间扫描直接检查目录是否为空，发现机器时保留同次配置。输出预处理没有回调时不构造投入展开列表。固体和液体共享原生步骤，各自保留完整回滚。扣料、耗电、耗水和保存所需的逐批重检与读回不能以性能优化为由删除。

## 一个入口完成关联检查和本地打包

在 `D:\workzone\probably-stolen` 执行：

```powershell
.\mods-melonloader\Build-ForgeMods.ps1
```

入口先运行 Forge 领域检查，再用一个 MSBuild 会话编译核心和五个示例，核心只构建一次。随后运行机械飞升、模块矩阵、物流枢纽和合成扩展的领域检查，编译机械飞升，并调用既有 `Build-MechcoreProtocol.ps1` 编译和混淆三个机核项目。构建前后及三个包内的 Forge 哈希必须相同。它只生成本地文件。

`-GameDir` 指定目标游戏，`-DistRoot` 指定机核发布目录，`-DefaultLogLevel` 显式选择日志级别。默认开发日志为 `INFO`；`DEBUG` 必须显式启用，游戏中的用户偏好仍优先。

单独验证 Forge 时执行 `scripts/Build-P0.ps1 -GameDir <游戏目录>`；加 `-IncludeProbes` 构建工坊和效果探针。它们参与检查，不被加入机核发布包。

四个内容工程从 `mods-melonloader/Directory.Build.targets` 统一引用本地 Forge 构建产物。单项目构建前先构建 Forge，也可以用 `-p:ForgeAssemblyPath=<配套DLL>` 选择明确的依赖；兼容旧参数 `ForgeDll`，同时传入不同依赖会报错。内容项目不再通过 `ProjectReference` 重建 Forge。

公共 API 签名兼容、领域检查、Release 编译和混淆产物只能验证对应层次。图集发布、原生钩子、夜间加工、失败回滚与存档重载仍须用当前产物在新进程及可丢弃档中验证。
