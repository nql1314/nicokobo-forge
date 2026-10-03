# Nicokobo Forge 的能力与扩展边界

更新：2026-10-03。Forge 是 Probably Stolen 内容 Mod 的通用前置。框架维护注册、能力门控、原生适配、公共事件和资源事务；内容 Mod 各自维护物品、玩法规则与存档格式。

## 框架提供

| 能力 | 公共入口与契约 |
| --- | --- |
| 内容声明、所有者与冲突诊断 | `ForgeApi`、`ForgeItemApi`、`ForgeEffectApi`、`ForgeContentApi`；通过快照分别检查 `Staged / Applied / Conflict / Failed` |
| 普通物品、设施、节点与机器模组 | `RegisterItem`、`RegisterAmenity`、`RegisterNode`、`RegisterModule`；内容方提供工厂和稳定 ID，框架持有委托并适配目录 |
| 原版 UI 机器模板与资源事务 | `ForgeMachineRegistrationApi` 声明输入、输出、电池、模组及手册；运行时处理材料、液体、电量、容量、逐项读回和恢复 |
| 显式批次处理 | `ForgeMachineRuntimeApi.CreateBatchProcessor` 复用事务；调用者提供实时槽位、投料规则、时机与同夜去重 |
| 电量、液体与生产价值 | `ForgePowerApi`、`ForgeLiquidApi`、`ForgeProductionValueApi` 及纯计算类；内容方决定配方与系数 |
| Nico 工坊 | `ForgeWorkshopApi.RegisterChain` 声明独立图谱与回调；条件和奖励用于展示，实际交易由提供者完成；Forge 内置全开局名片与原版成就提供者，内容 Mod 持有各自的独立页面 |
| 夜间商店补货 | `NativeItemOptions` 声明 `Repeatable / Unique / None`；可用条件由内容方提供，框架核对实际货架落点 |
| 生命周期、模组与文本 | `ForgeLifecycleApi`、`ForgeModuleApi`、`ForgePresentationApi`；共享观察 Hook 和拥有者回调 |
| 开局与周目数据 | `ForgeStartApi` 认领开局 ID；`ForgeRunDataApi.Read / Stage` 读取及比较后暂存所属 JSON，文件保存与回读由内容方确认 |
| 图集与只读公共服务 | `ForgeAssetsApi` 提供图片发布和赋图；`ForgeSaveReadbackApi` 读取 ES3 文本，内容方校验业务结果；`ForgeHookApi` 检查原生补丁入口。见[公共服务](SHARED_SERVICES.md) |
| 库存读取与搬运预检 | `ForgeInventoryApi.CaptureDirect / PreviewWholeGridTransfer` 只读；`InventoryTransfer = false`，预检不提交搬运 |

原生注册应在目录初始化前提交。`Accepted` 表示声明暂存成功，目录 `Applied` 才表示工厂已接入。当前 API 不保证目录初始化后的迟到注册立即生效。动作前查询 `ForgeCapabilities.Current`，一项能力失效不自动停用其他能力。

## 内容 Mod 负责

- 具体物品 ID、外观、名称、价格、获得途径、配方和效果。
- 独立开局的菜单、初始物资、健康／暴露、经营规则及失败处理。
- 内容 Mod 工坊节点的资格、信用点和物品扣除、奖励、失败恢复与保存；Forge 内置原版页使用自己的周目记录和有界原生发奖事务。
- 原版熔炉扩展的投料、配方、原版优先级、夜间调度与生产 Hook。
- 物流卡规则、节点绑定、网络容量、调度、UI、Wilds 解锁及网络存档。

`samples/Nicokobo.Forge.LogisticsExtension` 是内容侧示例库，不装入游戏，也不编入 Forge 核心。实际物流 Mod 已有物品定义和网络领域规则，实际搬运、货物持久化及原生电量仍待实现。

## 数值与验证

框架通用数值由 [Forge BuildConfig](../BuildConfig/README.md) 集中维护；玩法数值由[内容 Mod BuildConfig](../../probably-stolen/mods-melonloader/BuildConfig/README.md)维护。两者随所属 DLL 编译，依赖边界保持明确。

签名检查、领域检查、编译、安装、游戏日志、实际资源变化及存档重载分别记录。API 存在、注册接受或能力为真都不能代替玩法验收。未开放的库存写入、整箱上传和网络货物存储不能声明为已支持；相关实现应先完成写后读回、拒绝时双方不变、失败恢复与保存重载。当前证据和待验收项见 [FORGE_PROGRESS.md](FORGE_PROGRESS.md)。
