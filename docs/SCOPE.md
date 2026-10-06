# Nicokobo Forge 的能力与扩展边界

更新：2026-10-06。Forge 是 Probably Stolen 内容 Mod 的通用前置。框架维护注册、能力门控、原生适配、公共事件和资源事务，并持有共享全域制造终端；内容 Mod 各自维护成品、配方、玩法规则与存档格式。

## 框架提供

| 能力 | 公共入口与契约 |
| --- | --- |
| 内容声明、所有者与冲突诊断 | `ForgeApi`、`ForgeItemApi`、`ForgeEffectApi`、`ForgeContentApi`；通过快照分别检查 `Staged / Applied / Conflict / Failed` |
| 普通物品、设施、节点与机器模组 | `RegisterItem`、`RegisterAmenity`、`RegisterNode`、`RegisterModule`；内容方提供工厂和稳定 ID，框架持有委托并适配目录 |
| 原版 UI 机器模板与资源事务 | `ForgeMachineRegistrationApi` 声明输入、输出、电池、模组及手册；运行时处理材料、液体、电量、容量、逐项读回和恢复 |
| 动态配方手册目录 | `ForgeMachineRegistrationApi.GuideSnapshot()` 自动收集已接受的机器及追加配方，含提供者、单批数量、分类与双语条件；版本号供界面刷新缓存，不执行生产回调 |
| 内置全域制造终端 | `ForgeManufacturingTerminal` 提供稳定 ID 与参数；Forge 注册本体、图标、拾荒和夜间商店入口，兼容两个已知旧终端存档 ID；内容 Mod 追加配方 |
| 显式批次处理 | `ForgeMachineRuntimeApi.CreateBatchProcessor` 复用事务；调用者提供实时槽位、投料规则、时机与同夜去重 |
| 机器自动化观察 | `ForgeMachineAutomationApi` 读取真实配方及提供者，在原有加工机会的逐批前后分派回调；选择只影响该次机会，不增加加工次数 |
| 电量、液体与生产价值 | `ForgePowerApi`、`ForgeLiquidApi`、`ForgeProductionValueApi` 及纯计算类；内容方决定配方与系数 |
| Nico 工坊 | `ForgeWorkshopApi.RegisterChain` 声明独立图谱与回调；条件和奖励用于展示，实际交易由提供者完成；Forge 内置全开局名片与原版成就提供者，内容 Mod 持有各自的独立页面 |
| 夜间商店库存 | `NativeItemOptions` 声明 `Repeatable / Unique / None`；只在原生商店生成库存时加入商品，购买后不自动补货；可用条件由内容方提供，框架预检货架空间并核对实际落点 |
| NPC 供货与夜间模组池 | `NativeItemOptions.NpcTrade` 声明物品类别，包含食品与医疗品；`Suppliers` 可组合 `Thief / Inventor`，默认 `Any` 不限制白天供货人。`IncludeInNightShop` 只允许 `Module` 类别加入杰克逊原版夜间模组／节点名额，其他夜店商品保留原规则。`MinimumDay` 限制最早供货日，`SkipWhenOwned` 按玩家实际持有过滤。普通池先筛选供货人、类别、天数、持有状态及已应用工厂，合格 Mod 候选均分 `ForgeNumbers.NpcStock.ModSupplyWeight = 0.25`，每种为 `0.25 / N`。36 条入口中，掉落表抽取临时合并对应类别候选，保留原版、未知条目及被其他 Mod 占用的 `Conflict` ID 原有权重，不重复加权；直接供货同样保留 `Conflict` ID 的结果，其余按原版商品名额抽签，原版结果权重默认 1，Mod 总概率约 20%，候选数量变化不增加总概率。无合格候选时保留原版，不额外保底追加商品。矿工仍按独立 `MinerWeight`（未声明时用 `Weight`）整批选一种矿物，保留原版件数；农夫、水商及冰矿工沿用原版供货。每次供货／刷货重新筛选并分配权重，原生处理归属、阵营限制和容量；不改写全局拾荒掉落表 |
| 生命周期、模组与文本 | `ForgeLifecycleApi`、`ForgeModuleApi`、`ForgePresentationApi`；共享观察 Hook 和拥有者回调 |
| 开局与周目数据 | `ForgeStartApi` 认领开局 ID；`ForgeRunDataApi.Read / Stage` 读取及比较后暂存所属 JSON，文件保存与回读由内容方确认 |
| 开局选择栏 | Forge 自动处理原版和新增卡片的紧凑行高、裁剪、滚轮和滚动条；内容 Mod 持有卡片文本与选择回调 |
| 原版档案箱 | 内部网格扩为 16×16（256 格）；在原生创建后及读档恢复物品前原位扩容，保留原物品、位置与收纳规则；`ForgeCapabilities.Current.DossierExpansion` 表示扩容钩子已安装 |
| 图集与只读公共服务 | `ForgeAssetsApi` 提供图片发布和赋图；`ForgeSaveReadbackApi` 读取 ES3 文本，内容方校验业务结果；`ForgeHookApi` 检查原生补丁入口。见[公共服务](SHARED_SERVICES.md) |
| 库存读取与搬运预检 | `ForgeInventoryApi.CaptureDirect / PreviewWholeGridTransfer` 只读；`InventoryTransfer = false`，预检不提交搬运 |
| 整批奖励落位预检 | `ForgeInventoryPlacementApi.PlanWholeGrid` 在托管占格图中规划未附着单件物品，再用真实库存校验准入；不创建临时原生库存或修改原物品，交付与保存留调用方 |
| 单次物品搬运与倒液 | `ForgeItemTransferApi.Move` / `ForgeLiquidTransferApi.Pour` 走原生接受／转移路径并核对两端实际量；结果不确定时由调用方暂停，不代表完整网络或保存重载已验收 |

原生注册应在目录初始化前提交。`Accepted` 表示声明暂存成功，目录 `Applied` 才表示工厂已接入。当前 API 不保证目录初始化后的迟到注册立即生效。动作前查询 `ForgeCapabilities.Current`，一项能力失效不自动停用其他能力。

## 内容 Mod 负责

- 具体物品 ID、外观、名称、价格、获得途径、配方和效果。
- 独立开局的卡片文本、选择与启动逻辑、初始物资、健康／暴露、经营规则及失败处理；选择栏布局与滚动由 Forge 统一处理。
- 内容 Mod 工坊节点的资格、信用点和物品扣除、奖励、失败恢复与保存；Forge 内置原版页使用自己的周目记录和有界原生发奖事务。
- 原版熔炉扩展的投料、配方、原版优先级、夜间调度与生产 Hook。
- 物流配方卡、来源选择、调度、UI 与周目配置；资源／电力网络、容量、过滤、解锁及网络存档。

`samples/Nicokobo.Forge.LogisticsExtension` 是内容侧示例库，不装入游戏，也不编入 Forge 核心。实际插件位于 `probably-stolen/mods-melonloader/logistics-nexus/`，当前已接入本地固体／液体填料、附近产物弹出、终端配置卡与周目配置暂存。资源／电力网络和网络货物仍待实现；配置暂存不代表文件保存或新进程重载成功。详细边界见[物流 API](LOGISTICS_API_PROGRESS.md)，`InventoryTransfer` 总能力维持 `false`。

## 数值与验证

框架通用数值由 [Forge BuildConfig](../BuildConfig/README.md) 集中维护；玩法数值由[内容 Mod BuildConfig](../../probably-stolen/mods-melonloader/BuildConfig/README.md)维护。两者随所属 DLL 编译，依赖边界保持明确。

签名检查、领域检查、编译、安装、游戏日志、实际资源变化及存档重载分别记录。API 存在、注册接受或能力为真都不能代替玩法验收。单次搬运不能扩大为整箱上传或网络货物存储已支持；实际写后读回、拒绝时双方不变、失败恢复与保存重载须逐层确认。当前证据和待验收项见 [FORGE_PROGRESS.md](FORGE_PROGRESS.md)。
