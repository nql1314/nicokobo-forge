# Nicokobo Forge 的 scope 与扩展边界

更新：2026-09-27。Nicokobo Forge 是供其他 Probably Stolen Mod 使用的通用前置 API；内容 Mod 各自拥有物品、规则和存档数据。当前证据与待验收项见[进度记录](FORGE_PROGRESS.md)。

## In scope：框架提供

| 通用能力 | 当前状态 | 内容 Mod 的使用方式 |
| --- | --- | --- |
| 所有者、稳定 ID、依赖与冲突诊断 | 声明注册中心及原生物品/效果 `Snapshot()` 可用 | 查询每项 `Staged / Applied / Conflict / Failed`；声明接受不代替目录应用 |
| 原生普通物品、设施物品、节点与机器模组注册 | `RegisterItem` / `RegisterNode` 接 `MiscItemDirectory`，`RegisterAmenity` 接 `AmenitiesItemDirectory`，`RegisterModule` 接 `ModuleDirectory`；按构建门控 | 传入自己的工厂；Nicokobo Forge 持有委托、检查 ID 和实际类型 |
| 夜间商店补货 | 原生物品注册可附带 `NativeItemOptions`，声明 `Repeatable` 或 `Unique`；唯一项按玩家实际拥有状态过滤 | 内容 Mod 提供可用条件；Forge 在原生库存生成完成后创建并核对货架落点 |
| Wilds Network 升级 | `ForgeNetworkUpgradeApi.Register` 注册非重复升级，补入原生升级字典、购买动作与界面节点；`IsUnlocked` 读取原生状态；可选 `CreditCost` / `GetCreditCost` 追加信用点成本，可选 `GetRequirement` / `TryConsumeRequirement` 在购买前复核并在原生成功后消耗额外资源，失败时恢复节点、信用点与野地恩惠 | 内容 Mod 提供价格、双语文本、显示条件、前置、额外条件和解锁回调；具体玩法与资源选择仍由内容 Mod 决定 |
| 模组/节点效果注册 | `ForgeNativeEffectApi.RegisterEffect` 可编译；Nicokobo Forge 管理效果 ID、目录时机与随机池资格 | 内容 Mod 提供效果对象、回调与具体数值；当前尚无游戏内效果调用证据 |
| 独立开局身份认领 | `RegisterStart` 可编译 | 内容 Mod 实现菜单、初始内容与保存流程 |
| 周目数据承载 | `ForgeRunDataApi.Read` / `Stage` 可编译，按构建和签名门控 | 使用自己的键与 schema；`Stage` 仅写内存，保存后须读回 |
| 通用库存读取与搬运预检 | `CaptureDirect` 与 `PreviewWholeGridTransfer` 只读入口可编译；写入 API 尚未开放 | 内容 Mod 用快照识别实例、预检完整单件位置；接纳与回滚验证后再开放写入 |
| 能力查询 | `ForgeCapabilities.Current` 报告普通/设施/模块目录、夜间补货、网络升级、效果、周目暂存、库存读取和完整单件预检门控；搬运明确为 false | 内容 Mod 在动作前判断相应能力，不能把注册 Accepted 当作目录或界面已应用 |
| 扩展入口 | 公共程序集供内容 Mod 引用；示例工程说明边界 | 内容 Mod 在自己的程序集实现规则、UI、网络与适配器 |

## Out of scope：内容 Mod 自己实现

- 伪人开局的剧情、数值、神经接口效果、菜单呈现、初始物资与失败清理。
- 物流记忆卡的黑白名单策略、卡与节点绑定、周围箱子范围、压入/弹出调度、网络容量、Wilds 解锁、界面和热键。
- 具体物品 ID、价格、图标、文本、掉落与配方；物流三种物品的字段仍由内容规格确定。
- 未经验证的原生物品序列化、整箱上传、跨库存事务与网络库存持久化。Nicokobo Forge 不将其声明为已支持。

原生注册应在游戏目录初始化前提交。`Accepted` 只表示 ID 和工厂已暂存；内容 Mod 要通过 `Snapshot()` 查询 `Applied`，并在执行玩法前查看 `ForgeCapabilities.Current`。目录尚未初始化、构建门控失败或补丁不可用时，状态会保持 `Staged`；当前 API 不保证目录初始化后的迟到注册立即生效。

自定义网络升级使用原生 `NetworkUpgrade.state` 和 `PlayerStore.networkUpgrade` 持久化。Forge 会在原生升级字典初始化以及读档后补入缺失定义，并克隆指定原生 `NetworkElement` 作为购买入口；原生同 ID 升级的已保存副标题与当前定义不符、解锁动作或 UI 节点由其他 Mod 占用时停用该 ID。修改已发布升级的英文副标题需要单独迁移旧存档。`CreditCost` 与 `GetCreditCost` 可在原生野地恩惠成本之外追加静态或动态信用点成本，零野地恩惠节点也可使用；信用点和额外资源都在点击前复核。失败时 Forge 核对存档身份、恢复原生数值、尝试补偿保存，并回读当前槽位的周目、升级状态、信用点和恩惠；回读失败会报告持久化结果不确定。内容 Mod 的 `TryConsumeRequirement` 必须在返回 `false` 或抛异常时自行撤销已做的资源变更，成功后的材料存档核对仍由内容 Mod 负责。`IsVisible` 只控制节点显示，不等于已购买；内容 Mod 应在实际供货或玩法入口再次调用 `IsUnlocked`。界面位置、信用点与额外资源保存、存档重载和重复打开仍须在当前构建实机验证。

普通物品、模块目录、效果随机池、周目数据和库存观察各按自身所需签名门控。P0 只读诊断探针失败时，注册入口仍可独立尝试安装；某一能力不可用不自动关闭其他能力。

`samples/Nicokobo.Forge.LogisticsExtension` 是内容侧示例库，不装入游戏，也不属于 `Nicokobo.Forge.dll`。其记忆卡规则与网络路由展示怎样在框架通用入口之上开发；实际物流 Mod 需要在物品字段、原生搬运和保存闭环确认后接入。

## 能力证据

纯逻辑检查、编译、安装、游戏日志和存档重载分别记录。伪人 Mod 的神经接口节点与鉴定义眼已在 2026-09-26 实机日志中达到 `Applied`，55 号开局的正常保存/读档路径已验证；杰克逊手写单的 Nicokobo Forge 迁移仍待新进程验证。`ForgeRunDataApi` 与 `CaptureDirect` 只有游戏启动时的签名门控日志，尚无实际读写调用证据；内容侧物流示例只有纯逻辑/编译证据。任何跨库存移动或网络入库都必须先预检、写后读回，并在失败时验证恢复；缺少无损序列化或可靠回滚时拒绝执行。
