# 物流所需通用 API：接入进度

日期：2026-09-29。依据 [`probably-stolen` 的物流脉络设计](../../probably-stolen/docs/MECHCORE_PROTOCOL_LOGISTICS_NEXUS.md)；该设计描述资源网络与电力网络玩法，不能当作 Nicokobo Forge 的实现指令或运行证据。项目边界见 [scope](SCOPE.md)。

| 物流需求 | Nicokobo Forge 通用能力 | 内容 Mod 负责 | 证据 |
| --- | --- | --- | --- |
| 记忆卡、压入/弹出节点注册 | `RegisterItem`、`RegisterNode` 共用 ID 所有权与原生目录钩子 | 三种物品 ID、模板、外观、槽位、获得途径 | 注册中心检查、编译通过；未进游戏核对 |
| 卡规则、绑定与路由 | 提供公共注册及周目存档承载 | 规则和绑定实现在 `samples/Nicokobo.Forge.LogisticsExtension`，将来由物流 Mod 接管 | 黑白名单、实际实例标签、失效条件、默认网络、显式失效绑定的领域检查通过 |
| 卡/节点/网络配置保存 | `ForgeRunDataApi` 对 owner 键做当前周目身份检查、原值比较与内存暂存 | 内容 schema、迁移、保存回调与文件读回 | 编译通过；未验证游戏保存/读档 |
| 机器与资源网络间的整件搬运 | `ForgeNativeInventoryApi.CaptureDirect` 只读快照及 `PreviewWholeGridTransfer` 保守预检；写入仍待核对接纳/回滚语义 | 合法机器输入/输出、网络路由、调度与过滤 | 两个只读入口编译通过；候选写入签名诊断已加，搬运未实现 |
| 资源/电力网络与扩容 | 通用实例序列化、容量、电量与事务能力待验证 | 资源主/子网、电网、容器/电池扩容、UI、Wilds 解锁 | 物流 Mod 已有纯领域计划与账本；不保存货物、电量或扣除实体 |

内容侧示例不会作为 MelonLoader 插件安装。它的配置 codec **不含网络物品载荷**；在无损序列化和跨库存恢复路径未验证前，不能据此上线存储网络。12:30 的重启已确认新增 `Nicokobo Forge/NativeItem`、`Nicokobo Forge/RunData` 和 `Nicokobo Forge/Inventory` 门控日志，见[运行记录](probes/2026-09-26-1230-universal-api-runtime.md)。仍需在可丢弃档中验证一件普通物品的目录创建、库存快照与 `modData` 暂存/保存/重载。随后再开发通用库存读写适配，先完成“完整移动一件、拒绝时双方不变、保存重载一致”三个门槛。

原生 `SlotMarker.TryAcceptOnce` 的静态路径会按可取数量截断，并可能走拆分或叠加；当前只读预检刻意拒绝这两类路径。已打包安装的最新框架增加逐项应用状态查询，见[12:58 安装记录](probes/2026-09-26-1258-installed.md)。库存写入仍未开放，不能把预检 `Ready` 当作可提交事务。

13:00 实机日志出现 `dossier=-101`、`trashcan=-102` 等负数原生实例 ID。`CaptureDirect` 的只读快照因此允许非零负数 ID；完整搬运预检继续拒绝非正数 ID，直到特殊物品的移动与存档语义被验证。

## 2026-09-26 能力结论

物流所需 API **部分实现**。Nicokobo Forge 具备普通物品/节点/效果注册、按 owner 的 `modData` 读取与内存暂存、直接子项库存快照、完整单件搬运的只读预检，以及候选写入方法的签名诊断；`InventoryTransfer` 仍为 `false`。`samples/Nicokobo.Forge.LogisticsExtension` 只实现记忆卡规则、绑定路由和配置 codec，不是安装后会运行的物流 Mod。实际物流 Mod 已增加资源/电力网络纯领域规则，但尚无机器网络 UI、物品实际搬运、网络货物无损序列化、原生电量适配、容量实体锁定、整箱上传、失败回滚及保存重载闭环。因此不能把它标为“物流 Mod 已可用”。
