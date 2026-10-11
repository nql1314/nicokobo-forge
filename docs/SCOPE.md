# Nicokobo Forge 的能力与扩展边界

更新：2026-10-09。Forge 是 Probably Stolen 内容 Mod 的通用前置。框架维护注册、能力门控、原生适配、公共事件和资源事务，并持有共享全域制造终端；内容 Mod 各自维护成品、配方、玩法规则与存档格式。

## 框架提供

| 能力 | 公共入口与契约 |
| --- | --- |
| 分支对话、具名来访与阅读窗 | `ForgeDialogueApi`、`ForgeClientVisitApi`、`ForgeReadingApi`；owner／请求隔离、原生对象与委托生命周期。内容方管理剧情条件、文本和交易；不新增文件或主动保存。见[契约](CONVERSATION_API.md) |
| 电话联系人及多笔供应商会面 | `ForgePhoneApi`、`ForgeSupplierApi`；原生电话簿、号码所有权和每笔预览／继续挑选。内容方持有解锁、预约、货单、扣款和交付收据；不重写原生供货库存或主动保存。见[契约](CONVERSATION_API.md) |
| 内容声明、所有者与冲突诊断 | `ForgeApi`、`ForgeItemApi`、`ForgeEffectApi`、`ForgeContentApi`；通过快照分别检查 `Staged / Applied / Conflict / Failed` |
| 普通物品、设施、节点与机器模组 | `RegisterItem`、`RegisterAmenity`、`RegisterNode`、`RegisterModule`；内容方提供工厂和稳定 ID，框架持有委托并适配目录 |
| 原版 UI 机器模板与资源事务 | `ForgeMachineRegistrationApi` 声明输入、输出、电池、模组及手册；运行时处理材料、液体、电量、容量、逐项读回和恢复 |
| 动态配方手册目录 | `ForgeMachineRegistrationApi.GuideSnapshot()` 自动收集已接受的机器及追加配方，含提供者、单批数量、分类与双语条件；版本号供界面刷新缓存，不执行生产回调 |
| 内置全域制造终端 | `ForgeManufacturingTerminal` 提供稳定 ID 与参数；Forge 注册本体、图标、拾荒和夜间商店入口，兼容两个已知旧终端存档 ID；内容 Mod 追加配方 |
| 缺失物品读档兼容 | 仅处理通过 Forge API 成功登记且正常保存已有来源记录的普通物品：原生登记缺失、当前无 Forge 声明且确认提供者 DLL 已移除时才清理；第三方物品、无记录旧档及来源不确定的项保留。玩家持有的缺失项按存档单价和数量返款，正常子物品完整返还；工厂不可用或空间不足时保存在周目 `modData`。来源记录使用 `nicokobo.forge.save.item_providers_v1`，待返还图使用 `nicokobo.forge.save.recovered_items_v1`，均随原版正常保存持久化。保留终端 ID 迁移；已售或未持有项不返款；须继续安装 Forge |
| 显式批次处理 | `ForgeMachineRuntimeApi.CreateBatchProcessor` 复用事务；调用者提供实时槽位、投料规则、时机与同夜去重 |
| 机器自动化观察 | `ForgeMachineAutomationApi` 读取真实配方及提供者，在原有加工机会的逐批前后分派回调；选择只影响该次机会，不增加加工次数 |
| 电量、液体与生产价值 | `ForgePowerApi`、`ForgeLiquidApi`、`ForgeProductionValueApi` 及纯计算类；内容方决定配方与系数 |
| Nico 工坊 | `ForgeWorkshopApi.RegisterChain` 声明独立图谱与回调；条件和奖励用于展示，实际交易由提供者完成；Forge 内置全开局名片与原版成就提供者，内容 Mod 持有各自的独立页面 |
| 夜间商店库存 | `NativeItemOptions` 声明 `Repeatable / Unique / None`；只在原生商店生成库存时加入商品，购买后不自动补货；可用条件由内容方提供，框架预检货架空间并核对实际落点 |
| NPC 供货与夜间模组池 | `NativeItemOptions.NpcTrade` 声明物品类别，包含食品与医疗品；`Suppliers` 可组合 `Thief / Inventor`，默认 `Any` 不限制白天供货人。`IncludeInNightShop` 只允许 `Module` 类别加入杰克逊原版夜间模组／节点名额，其他夜店商品保留原规则。`MinimumDay` 限制最早供货日，`SkipWhenOwned` 按玩家实际持有过滤。普通池先筛选供货人、类别、天数、持有状态及已应用工厂，合格 Mod 候选均分 `ForgeNumbers.NpcStock.ModSupplyWeight = 0.25`，每种为 `0.25 / N`。28 条入口中，掉落表抽取临时合并对应类别候选，保留原版、未知条目及被其他 Mod 占用的 `Conflict` ID 原有权重，不重复加权；直接供货同样保留 `Conflict` ID 的结果，其余按原版商品名额抽签，原版结果权重默认 1，Mod 总概率约 20%，候选数量变化不增加总概率。无合格候选时保留原版，不额外保底追加商品。矿工仍按独立 `MinerWeight`（未声明时用 `Weight`）整批选一种矿物，保留原版件数；农夫、水商及冰矿工沿用原版供货。每次供货／刷货重新筛选并分配权重，原生处理归属、阵营限制和容量；不改写全局拾荒掉落表 |
| 生命周期、模组与文本 | `ForgeLifecycleApi`、`ForgeModuleApi`、`ForgePresentationApi`；共享观察 Hook 和拥有者回调 |
| 开局与周目数据 | `ForgeStartApi` 认领开局 ID；`ForgeRunDataApi.Read / Stage` 读取及比较后暂存所属 JSON，文件保存与回读由内容方确认 |
| 开局选择栏 | Forge 自动处理原版和新增卡片的紧凑行高、裁剪、滚轮和滚动条；内容 Mod 持有卡片文本与选择回调 |
| 原版档案箱 | 内部网格扩为 16×16（256 格）；在原生创建后及读档恢复物品前原位扩容，保留原物品、位置与收纳规则；`ForgeCapabilities.Current.DossierExpansion` 表示扩容钩子已安装 |
| 图集与只读公共服务 | `ForgeAssetsApi` 提供图片发布和赋图；`ForgeSaveReadbackApi` 读取 ES3 文本，内容方校验业务结果；`ForgeHookApi` 检查原生补丁入口。见[公共服务](SHARED_SERVICES.md) |
| 库存读取与搬运预检 | `ForgeInventoryApi.CaptureDirect / PreviewWholeGridTransfer` 只读；`InventoryTransfer = false`，预检不提交搬运 |
| 整批奖励落位预检 | `ForgeInventoryPlacementApi.PlanWholeGrid` 在托管占格图中规划未附着单件物品，再用真实库存校验准入；不创建临时原生库存或修改原物品，交付与保存留调用方 |
| 单次物品搬运与倒液 | `ForgeItemTransferApi.Move` / `ForgeLiquidTransferApi.Pour` 走原生接受／转移路径并核对两端实际量；结果不确定时由调用方暂停，不代表完整网络或保存重载已验收 |
| 内容状态及多源供电事务 | `IForgeBatchTransaction` / `IForgePowerTransaction` 与真实输入、输出和电量共用一次提交/补偿；网络、唯一额度和电池范围由内容持有 |
| 隐藏真实库存与列表承载 | `ForgeInventoryEndpointApi` / `ForgeSaveGraphApi` / `ForgeListInventoryApi` 保存原生完整图、固定工厂拓扑和实际尺寸；原生跨进程行为须按最终候选验收。见[接入契约](INVENTORY_POWER_TRANSACTIONS.md) |
| 内容所属网格的附加准入 | `ForgeInventoryAdmissionApi` 按根物品 ID 与库存 ID 在原生准入后增加托管限制，保留原生拒绝，不将转换委托写入原生准入字段。内容持有设备／对象图规则，真实拖放另验 |

`NpcTradeStockOptions.PreserveLootTableWeight` 可让合格物品在 NPC 掉落表抽取中保留已有条目及其权重，不再分配共享 Mod 预算，也不补入缺失条目；仍先检查供货人、类别、天数、持有状态与工厂应用状态。该选项只影响掉落表路径，直接供货、矿工批次与全局拾荒表沿用各自规则。内容方声明基础材料与原版等权重时可使用此选项。

老拾荒客的固定重型手枪与三份弹药，以及杰克逊的大存储区专门供货，不建立 Mod 替换作用域。杰克逊的白天与夜间库存只将七类 `ItemSpawner.Spawn("random_...")` 模组／节点工厂结果作为可替换名额；设备、钥匙卡及具名专用模组保留原版，白天也不替换成材料或日用品。

其他直接供货以原物品的主要用途类别与该入口允许类别的交集筛选候选，再按合格种类数均分共享预算。医疗类型优先于附带的奢侈／日用品类型；食品、加工食品与饮品共用食品池，材料使用材料／矿料池。武器（包括带日用品分类的厨房刀）、弹药、防具、工具、机器、存储区、钥匙卡、文件，以及重要／不可转售／容器标签的商品保留原版。具名模组不参与直接替换，所有供货人只用七类原版随机模组／节点工厂的结果抽取 Mod 模组。阴谋论顾客、固定供血者和退休酿酒师／废料商／化学师不建立替换作用域；卖加工肉的特殊拾荒者不建立替换作用域，拾荒运输者与打捞飞行员接材料池。替换保留原堆叠数量，创建或数量设置失败时保留原商品。

原生注册应在目录初始化前提交。`Accepted` 表示声明暂存成功，目录 `Applied` 才表示工厂已接入。当前 API 不保证目录初始化后的迟到注册立即生效。动作前查询 `ForgeCapabilities.Current`，一项能力失效不自动停用其他能力。

G 键合成表由 Forge 持有：快捷键、真实配方关系查询、窗口、分页、拖动与输入隔离均独立于内容 Mod。已登记 Forge 机器配方自动可读；原版机器扩展可提交只读展示声明。实体手册和原版机器扩展的加工逻辑由对应内容 Mod 持有。

## 内容 Mod 负责

- 具体物品 ID、外观、名称、价格、获得途径、配方和效果。
- 独立开局的卡片文本、选择与启动逻辑、初始物资、健康／暴露、经营规则及失败处理；选择栏布局与滚动由 Forge 统一处理。
- 内容 Mod 工坊节点的资格、信用点和物品扣除、奖励、失败恢复与保存；Forge 内置原版页使用自己的周目记录和有界原生发奖事务。
- 原版熔炉扩展的投料、配方、原版优先级、夜间调度与生产 Hook。
- 物流配方卡、来源选择、调度、UI 与周目配置；资源／电力网络、容量、过滤、解锁及网络存档。

`samples/Nicokobo.Forge.LogisticsExtension` 是内容侧示例库，不装入游戏，也不编入 Forge 核心。实际插件位于 `probably-stolen/mods-melonloader/logistics-nexus/`，0.2.0 候选接入本地物流、真实网络库存、材料保留量、唯一容器与真实电池供电。新增制造/解锁数值仍待用户确定；配置暂存不代表文件保存或新进程重载成功。`InventoryTransfer` 总能力维持 `false`，新库存与供电能力按各自已安装的适配门控和最终产物验收。

## 数值与验证

框架通用数值由 [Forge BuildConfig](../BuildConfig/README.md) 集中维护；玩法数值由[内容 Mod BuildConfig](../../probably-stolen/mods-melonloader/BuildConfig/README.md)维护。两者随所属 DLL 编译，依赖边界保持明确。

签名检查、领域检查、编译、安装、游戏日志、实际资源变化及存档重载分别记录。API 存在、注册接受或能力为真都不能代替玩法验收。单次搬运不能扩大为整箱上传或网络货物存储已支持；实际写后读回、拒绝时双方不变、失败恢复与保存重载须逐层确认。当前证据和待验收项见 [FORGE_PROGRESS.md](FORGE_PROGRESS.md)。
