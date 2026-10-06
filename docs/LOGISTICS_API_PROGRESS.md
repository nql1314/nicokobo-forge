# 物流所需通用 API：实现边界

更新：2026-10-06。当前独立[物流脉络](../../probably-stolen/mods-melonloader/logistics-nexus/README.md)为 0.1.13，配套 Forge 0.6.26。物流 README 记录已实现行为与待验收项，[玩法设计](../../probably-stolen/mods-melonloader/logistics-nexus/DESIGN.md)记录完整目标。

| 需求 | Forge 提供 | 物流当前状态 |
| --- | --- | --- |
| 物流物品与制造 | `ForgeItemApi` 与共享终端追加配方 | 自动填料模组、产物弹出模组、网络终端共三种物品；可在全域制造终端制造，不注册实体配方卡 |
| 真实配方与调度 | `ForgeMachineAutomationApi.Recipes / RegisteredRecipes / Subscribe` | 终端配置卡保存真实配方引用、规则版本、条件与优先级；Forge 机器逐批前填料、成功后弹出；原版熔炉仅过夜前后接入 |
| 本地物品搬运 | `ForgeItemTransferApi.Move` 走原生接受并读回两端数量 | 从已选本地容器或另行启用的主仓库补一批缺额；产物依次尝试已选机器输入、已选容器和启用的主仓库 |
| 本地液体运输 | `ForgeLiquidTransferApi.Pour` 保留容器，核对体积、组分与价值 | 倒入已安装的输入容器；水按卡的固定等级匹配，散装产物先余液桶再空桶；液体准入由物流维护 |
| 配置暂存 | `ForgeRunDataApi.Read / Stage` 核对周目与原值 | `nicokobo.logistics_nexus.terminal_state`，结构版本 2；保存卡归属、名称、配方和机器配置，不读取旧实体卡配置 |
| 完整单件搬运预检 | `CaptureDirect / PreviewWholeGridTransfer` 只读快照与保守预检 | 不提交搬运；与上面的单次原生接受接口是不同入口 |
| 资源／电力网络 | 尚无完整网络库存、容量及双向电量转移闭环 | 网络货物、过滤、容量、保留量、库存目标、每晚收电及机器补电待实现；非空网络绑定不会暗中改用本地 |

`InventoryTransfer` 总能力仍为 `false`，不能用它概括单次接口是否存在。`PreviewWholeGridTransfer` 的 `Ready` 只表示快照满足预检，且拒绝可能拆分或叠加的路径；`Move` 则允许原生接受决定实际转移量。当前单次物品搬运在拒收或附着前异常时尝试恢复该次来源，确认已送达的数量不退回，也不撤销此前独立成功操作。任一接口返回 `Indeterminate` 时，调用方暂停相应端点并检查实际状态。

物流 `Save()` 使用 `Stage` 暂存周目 JSON，实际文件保存由原生游戏流程执行。candidate003 的本地固体链、多液源重查、卡扣费、结构版本 2 的 ES3／index 读回，以及新 PID 的 24 台完整机器快照与 15 张卡恢复通过；这是实际原生结果，不能由暂存或编译推得。完整夜结受前置阻塞，用户已停止本轮排查与绕路；鼠键 UI 和三类高级故障注入未执行。本地多物品整体摆放预演、原版熔炉额外批次逐批补料也未完成。逐项范围见[当前进度](FORGE_PROGRESS.md#2026-10-06-联合-review-候选)与物流的[技术参考](../../probably-stolen/mods-melonloader/logistics-nexus/TECHNICAL_REFERENCE.md#2026-10-06-联合-review-候选)。

物流每次写入前重新核对当前卡、终端、物主和真实槽位，不复用上一笔搬运的来源许可或材料缺额。`Pour` 在原生回调异常后仍核对两端；确认组分单向等量移动、每组分价值与质量基数按比例转移时按已提交返回，其余不明结果继续暂停。配置暂存异常同样读回旧／新 JSON，仅在同一周目内确认提交或恢复明确未提交的扣卡，不向切换后的周目重放旧操作。

`samples/Nicokobo.Forge.LogisticsExtension` 是独立规则示例库，不作为 MelonLoader 插件安装，其 codec 不承载网络货物。物流的制造成本在内容仓库 `logistics-nexus/Integration/Content.cs` 维护。
