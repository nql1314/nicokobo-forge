# 自动化物流的公共 API 接入边界

实际内容项目位于 [automated-logistics](../../probably-stolen/mods-melonloader/automated-logistics/README.md)，程序集为 `Nicokobo.AutomatedLogistics`，owner／ID 前缀为 `nicokobo.automated_logistics`。具体玩法和当前验证见[技术参考](../../probably-stolen/mods-melonloader/automated-logistics/TECHNICAL_REFERENCE.md)。

| 内容职责 | Forge 提供的公共能力 |
| --- | --- |
| 物品、模组、制造注册 | `ForgeItemApi`、`ForgeModuleApi`、共享制造终端追加配方 |
| 十类补货／弹出机器的批次观察 | 注册机器库存、`BeforeBatch`／`AfterBatch` 自动化回调；熔炉、净水器、收集器和解序器的物流观察由内容侧接入 |
| 固体与液体搬运 | `ForgeItemTransferApi.Move`、`ForgeLiquidTransferApi.Pour`、实例冻结与原生准入 |
| 实体创建与失败补偿 | 创建凭据、库存图及事务能力；内容方验证容量、所有权与本次创建身份 |
| 耗电设备天然接网 | `CanSupplyDevice`、`GetDevicePowerSource` 及原生动作／注册批次事务；内容物品通过 `RegisterConsumer` 与 `GetDeviceEnergy`／`TryDebitDevice` 接入。不要求补货／弹出模组或模组舱，本地优先、共享储量及容量规则由内容侧持有 |
| 状态保存 | `ForgeRunDataApi.Read / Stage`；当前周目键为 `nicokobo.automated_logistics.runtime_v5` |
| 原生详情与价值 | 实例原生提示、物品展示及计价接口 |

自动化物流按真实加工消耗补回原料，不读取配方选择目标；虚空容器、水罐、电网、扩容规则及只读统计均由内容 Mod 实现。Forge 不持有它的物品 ID、材料匹配规则、容量或晨间统计。

`Stage` 仅暂存内存。当前候选的编译、离线检查与真实存档／跨进程恢复分开记录；旧候选的原生证据保留原始身份，不用于证明改名后的候选已经原生验收。

`samples/Nicokobo.Forge.LogisticsExtension` 是独立的通用物流示例库，不是自动化物流插件，也不作为 MelonLoader 内容 Mod 安装。
