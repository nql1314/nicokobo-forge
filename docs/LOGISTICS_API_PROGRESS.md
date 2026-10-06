# 物流所需通用 API：实现边界

更新：2026-10-06。物流玩法由[独立物流脉络](../../probably-stolen/mods-melonloader/logistics-nexus/README.md)维护，完整目标见[玩法设计](../../probably-stolen/mods-melonloader/logistics-nexus/DESIGN.md)。实现边界与验收状态以物流 README 为准，玩法设计不能作为 Forge 已支持网络搬运的证据。

| 物流需求 | Forge 提供 | 内容 Mod 当前状态 |
| --- | --- | --- |
| 记忆卡、物质注入／物质提取节点 | `ForgeItemApi.RegisterItem / RegisterNode`、目录通知与所有权检查 | 三种物品定义已接入；当前无常规获取途径 |
| 卡规则、绑定与路由 | 通用注册及周目数据承载 | 示例库提供卡规则与 codec；物流 Mod 维护自身资源／电力网络领域规则 |
| 配置暂存和保存 | `ForgeRunDataApi.Read / Stage` 核对当前周目和原值后暂存 | 自有 schema 与账本；不承载网络货物或真实电量 |
| 完整单件搬运预检 | `CaptureDirect` 快照、`PreviewWholeGridTransfer` 保守预检 | 实际搬运与跨库存恢复尚未实现 |
| 网络扩容与电量调度 | 当前没有网络货物写入或原生电量事务 API | 容量及调度仅有纯逻辑计划；UI、实体锁定、扣除和重载闭环待实现 |

`InventoryTransfer` 当前为 `false`。预检 `Ready` 只表示快照满足检查条件，不能提交事务。原生 `SlotMarker.TryAcceptOnce` 可能拆分或叠加物品，当前预检拒绝这些路径；只读快照允许非零负数实例 ID，搬运预检仍拒绝非正数 ID。

`samples/Nicokobo.Forge.LogisticsExtension` 是示例库，不作为 MelonLoader 插件安装。它的配置 codec 不含网络物品载荷。实际物流 Mod 的编译数值在[内容 BuildConfig](../../probably-stolen/mods-melonloader/BuildConfig/README.md)维护。

后续库存写入需先确认完整单件接纳、写后读回、拒绝时双方不变、失败恢复与保存重载，再支持路由和网络货物持久化。当前编译与领域检查不能证明已完成这些行为；验证范围见[当前进度](FORGE_PROGRESS.md)。
