# 真实库存端点与外部供电事务

Forge 0.6.33 的通用接入。玩法容量、网络身份、材料保留量、电池范围、制造唯一额度及解锁成本由内容 Mod 持有。签名、离线逻辑、编译和原生验收分别记录；这些 API 的存在不表示跨进程存档或完整网络已验收。

`ForgeInventoryAdmissionApi.Register(owner, itemId, inventoryId, allows)` 只给所属物品的直接原生 GameGridInventory 增加限制。Forge 在原生 `MayHaveValidInventorySlot` 成功后调用托管判据，false 不翻为 true；锁、归属、循环、尺寸及具体落位继续由原生校验。回调不写入原生准入字段，因为其 DynamicInvoke 不兼容转换后的托管委托。保存期间保持登记 lease，工厂只清除自有模板不适用的限制；普通库存和其他库存 ID 不受影响，底层 UncheckedAccept 不属于此准入契约。钩子不可用时登记拒绝，内容不能启用依赖端点。

`ForgeMachineRecipe.Transaction` 可规划 `IForgeBatchTransaction`，返回 null 拒绝当批。Validate 只读；Apply 收到实际创建并已放入原输出槽的产物。内容状态与材料、输出、本地或外部电量共用同一事务，写入前登记 Restore，所有补偿都尝试，无法确认时 Fault 后隔离机器。Dispose 在规划失败、成功和补偿后都执行。唯一额度须在 Apply 中确认真实产物后提交，不能在预览、登记或工厂创建时占用。

`ForgeExternalPowerApi.Register(owner, connectorId, energy, plan)` 为所属电线声明读函数和 `IForgePowerTransaction`。无连接返回负数；有效但空电网返回 0。Forge 拦截原电池槽读电、扣电和所有已知充能入口，电线不写 energy/max/recharge；普通电池走原路径。内容选择实际电池，`ForgePowerBatchMath.Plan` 按剩余绝对电量降序、稳定实例 ID 同值排序，并在不足时不给部分计划。原版炉另在一次原生加工回调内保存输入/旧输出快照，失败恢复与电量补偿共同读回；不增加炉的加工机会。

原生电池工厂会在赋identifier之前调用供电初始化。null／empty／unknown ID不能查询外部provider或中断原方法；只有已登记的完整ID才被识别为电线。供电读取／连接判断／规划共用一次安全解析，直接使用捕获的provider，保持普通初始化与已识别电线的充能拒绝边界。

`ForgeInventoryEndpointApi.Open` 只管理原生保存图外的隐藏根，正常可见物品不可重复登记。内容工厂必须在原生 Decode 的子项配对前重建同一库存拓扑及稳定库存 ID。没有存档时仅显式 createIfMissing 才创建；有错、缺工厂或身份冲突不替换成空库存。端点完整图随正常 SaveGame 写入 `nicokobo.forge.inventory.endpoints_v1`；写入无法确认时阻止这次原生保存。隐藏图加入实际 FindAllItem 周目枚举，真实设备自然参加其原生夜结阶段，不另行追加回调。

外部管理电网端点须显式 `chargingOnly: true`，保存数据保留此标记。其真实子图从所有原生全局电源设施查询中排除；CheckAvailablePower 与 HandlePower 的全局可用量／放电列表都不能使用这些电池。仅在原生 `ChargeAllRechargable` 一次调用中替换为独立列表副本，加入该区域真实 power_source_item；HandlePower 保留的原列表不变。该方法仅原生自充，PowerBlock 和 Recharger 仍分别通过真实 EndNight 前／后阶段执行一次原回调。

原生 `FireOnGameLoadedLate` 在 `LoadGame` 方法体内同步发出，不能等 LoadGame postfix 才解除端点的加载锁。端点在共享 `AfterGameLoadedLate` dispatcher 中以 `int.MinValue` 顺序清旧缓存并解除加载锁，再由内容重建端点；不使用另一个无序的 Late postfix 清理图。此准备不修改准入登记，随后的 LoadGame 返回与 finalizer 不再清理刚恢复的根。跨进程重复加载仍须验证完整库存图与UID，不能只读相同的内容状态 JSON。

原生炉最终提交读回由 `NativeBatchFinalization` 保护：包括输入／输出句柄和参与者 Verify 的任何异常都尝试全部补偿、Fault，并无条件 Dispose／清除批次上下文。补偿或 Fault 回调抛异常不能跳过后续资源与清理。

原生 `GameItem.Destroy` 为抽象方法，CLR生成wrapper的非abstract标志不能用于选择补丁目标。当前游戏的具体实现为 `GameItemElement.Destroy`，所有当前派生类型的销毁 override 都纳入元数据检查，运行时另核对真实methodPointer非零。事务只延迟快照中原实例的破坏：核对UID／identifier／parent后先执行原Expel，保留窗口、UID与Unity handler；失败恢复原槽形状、列表序号与handler本地位置并读回，成功先清除批次上下文再销毁。新物品及普通电池路径不受这条延迟影响。

`ForgeSaveGraphApi.Encode/Decode/Items` 复用原生图与载入修复，拒绝未知工厂、重复/冲突身份和不支持的人物节点。`ForgeListInventoryApi.Create/Move` 使用合法 GameGridInventory 单节点作为后台承载；owner 的原生状态保存实际尺寸，插入及读回恢复布局。格子只承载形状；内容按真实物品件数及嵌套内容验证容量，并提供分页 UI，不让玩家整理后台网格。

`ForgeInventoryFreezeApi.TryAcquire(items, group)` 提供临时真实实例锁。匿名事务互斥；同一命名 group 的多个长期介质锁可共享祖先、按引用数解除，其他 group 仍互斥。内容同时保存/恢复实际容器的 insert/remove 锁。原生 UncheckedAccept/Expel 属于底层入口，不能把它们当用户准入证明。

`ForgeItemTransferApi.Move` 核对原生搬运后的真实身份与数量。`ForgeLiquidTransferApi.Pour(source,target,max,condition)` 按完整液体混合物限制量，携带每组分的价值/品质基准并对两端登记补偿；有过滤器的中途转换不进入受限倒液。`ForgeItemInspectionApi` 读取普通原生单价及实际实例原生富文本提示；读取不复制货物或执行加工。`ForgeRunPurchaseApi.Purchase` 把原生付款及内容步骤纳入同批事务。

调用者仍须在同一最终 DLL 上验证真实出入、拒收双方不变、失败回退、实际夜结和新进程 ES3 读回。不要用托管 stub、序列化字符串或后台大格子替代这些验收。
