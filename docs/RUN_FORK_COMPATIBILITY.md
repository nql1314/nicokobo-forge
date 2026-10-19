# 存档分叉与内容状态

`ForgeRunForkApi` 处理已有周目再次调用原生 `InitialSave()` 的情况。已核对的 RestartAtAnyDay 0.3.0 在 `WriteSlotFile` 前缀中用这个入口分配分支；原生同时改变 `runID` 和 `saveSlotId`，随后嵌套执行 `SaveGame()`。普通 `LoadGame` 不应视为新开局。

## 接入

每个内容提供者为自己的身份绑定 JSON 注册 `RegisterState(ownerId, key, rebind, rebound, ready)`，并保留返回的 lease，在卸载时释放。Forge 内置原版工坊、Aug 的四份状态及扒窃状态已接入。玩法数据、格式验证和交易规则仍由各提供者持有。

- `rebind(json, source, destination)` 必须是纯转换。先用 `source == destination` 验证来源和未完成交易，再用实际新身份生成完整 JSON；不得发放、扣款或保存。只改变原生寻址字段，保留逻辑周目 ID、修订、随机状态、物品 UID、交易与领取收据。
- `rebound(store)` 在全部状态暂存后重绑会话、失效旧回调。不得重新载入物品图、重建 NPC 货物或执行交易。回调失败后禁止继续写入该失败分支，需重新载入源档。
- `ready(store)` 在原生分配新槽之前拒绝忙碌事务。JSON 中的 pending／未知版本／损坏数据必须由 `rebind` 拒绝；不能因为当前进度不可读而重建空状态。
- 开局发放入口先检查 `IsForking(store)`，在分叉时跳过。正常发放的收据与原生物品一起进入 `modData`，当前是否仍持有物品不等于是否已经领取。

只有成功的原生晚期加载事件或真正的新局初始化完成后，才认领当前来源。`StartNewGame` 声明真正新局；已有周目再进入 `InitialSave` 时，在最高优先级前缀建立作用域。迁移在内层 `SaveGame` 的其他内容前缀之前完成，不能放在 `InitialSave` 后置回调。

写入后回读目标槽的原生类型、run／slot 和各已登记状态在写入前的完整 JSON。内存暂存、`SaveGame` 返回、被其他前缀跳过的保存都不算分叉成功。原生分槽后失败可能已改变索引，Forge 不猜测恢复整个索引或自动重试，而是阻止该会话继续写入，要求重新载入源档。

`ForgeRunDataApi.Stage` 原有的 run／slot、预期 JSON 和提交前复核继续生效。未经过成功加载或分叉的身份变化会拒绝保存。

## 已失配的旧分支

`RepairFromSnapshot(store, sourceSnapshotPath)` 是显式恢复入口，不在普通读档时自动调用。调用者选择原槽或历史 ES3 快照；需为已确认加载的当前周目、相同开局类型、不同原生身份，且当前每份已登记状态都与源快照逐字节相同并通过所属提供者验证。来源不符、数据损坏、pending 或忙碌时不迁移。

成功结果仅为 `ReboundInMemory`：不写盘、不发物、不清空原记录，等待游戏正常保存。没有可验证源快照时保留原数据。此入口不是对任意跨槽数据的放行，也不声称修复原生索引或已经丢失的物品。

## 本轮验证边界

离线检查执行生产分叉钩子，模拟 RestartAtAnyDay 在外层 `WriteSlotFile` 中调用 `InitialSave`、分配新身份和嵌套保存；使用真实临时 JSON 文件验证回读，覆盖多提供者迁移、普通新局、普通保存、忙碌／pending 拒绝、失配来源、跳过保存、缺文件、错误身份、I/O 失败及显式旧档恢复。内容检查覆盖 Aug 状态／会话、扒窃数据保留、指南重入和已领取终端移除后再初始化。

离线端口不验证真实 IL2CPP 补丁顺序、ES3 写盘与加载、RestartAtAnyDay 联合运行。当前未打包、安装或执行原生测试；部署需配套使用本轮 Forge 和内容 Mod。
