# 伪人 Mod 接入 Nicokobo Forge：2026-09-26 核对

> 命名迁移说明：本文记录发生在 Nicokobo Forge 命名迁移前。产品、程序集、API 与日志标签文字已统一为当前名称；其中散列值和运行结论仍属于当时构建，不能作为当前重命名产物的哈希或运行证据。

目标构建为 Probably Stolen Demo Steam Build `25382790`；内容所有者使用 `nicokobo.aug`。本文件按当前源码区分“代码已迁移”和“游戏内已验证”。

| 伪人功能 | 本次处理 | 原因与边界 |
| --- | --- | --- |
| 神经接口 `nicokobo.aug.neural_interface_module` | 目录钩子、ID 查重和委托持有改由 `ForgeNativeApi.RegisterNode`；物品工厂与数值仍由伪人提供 | 13:20 日志已见 `Applied`、工厂创建及正常开局保存/读档 |
| 超频 `nicokobo.aug.effect.overclock` | 已恢复伪人原有的节点创建时效果注册与随机池排除 | 首次尝试迁至 `ForgeNativeEffectApi.RegisterEffect` 时，Nicokobo Forge 在插件初始化阶段安装随机补丁触发 `ModuleEffectHelper` 类型初始化异常；该迁移未保留。Nicokobo Forge 已改为目录就绪后才安装效果钩子，但尚无内容 Mod 的运行验证 |
| 鉴定义眼 | `MiscItemDirectory` 注册改由 `ForgeNativeApi.RegisterItem`；模板、Sprite、电池和自动鉴定逻辑仍在伪人 | 13:20 日志已见 `Applied` 与 Sprite 创建；自动鉴定行为未单独验证 |
| 杰克逊手写单 | `MiscItemDirectory` 注册改由 `ForgeNativeApi.RegisterItem`；柜台投放、内容和保存回读仍在伪人 | 属于普通物品入口；这项在 13:20 正常路径验证后才补迁，最新 DLL 尚待新进程验证 |
| 独立开局身份 | 调用 `ForgeStartApi.RegisterStart` 认领稳定 ID 与原生 55 号承载 | Nicokobo Forge 当前只做 ID 冲突管理；选择意图、资格、首次保存、失败恢复和读档呈现仍由 `PlayableStartFlow` 等伪人代码实现 |
| 变声器 | 保留 `AmenitiesItemDirectory` 自注册 | Nicokobo Forge 尚无该目录适配，不能强行迁到普通物品目录 |
| 周目状态、初始物资、销售保护 | 保留伪人现有代码 | `ForgeRunDataApi.Stage` 只暂存内存，不替代伪人的保存事务/文件读回；通用库存 API 仅只读和预检 |

首次迁移版 `AugMechanicalAscension.dll` 于 13:12 安装，SHA-256 为 `C06945520988DE70DDEED513C30EF209E7FE2EF489AAD47372604DCF6334DC18`。13:16 启动日志证实 Start、Node、Item、Effect 声明均 `Accepted`，但 Nicokobo Forge 的效果钩子报 `TargetInvocationException`，随后日志出现 `ModuleEffectHelper` 类型初始化的 `SEHException`；开局卡显示 `enabled=False`，用户报告不可点击且语言切换失效。这份运行结果不支持保留效果迁移。

修正版已把伪人超频效果恢复原路径，Nicokobo Forge 的效果钩子改为目录就绪后才尝试安装。完整构建与伪人 `588` 项领域检查通过。13:20 安装到游戏 `Mods/` 的 `AugMechanicalAscension.dll` SHA-256 为 `F3C73AB5869FEFD3791BFE93BB60596A66C15D1814670E4C40BAC97E7B9B64CA`，`Nicokobo.Forge.dll` 为 `E64227B0E99DB95E0F3BC6039F27F6E77D456B9C043F8FBDA8D72AA6B52F2F06`；旧文件备份在 `D:\workzone\nicokobo-forge\dist\rollback\20260926-132030\`。

13:20 的完整重启中，Nicokobo Forge 对开局身份、节点、鉴定义眼均记录 `Accepted`，普通目录初始化后节点与鉴定义眼均为 `Applied`。开局卡 `enabled=True`，55 号开局实际进入游戏；神经接口工厂生成 NODE，基础值 8/8/4 与 25% 超频标识正确。初始保存及开局各步骤对槽 59 的回读均为 `StateMatched`；返回菜单后读档，`NativeSession loadRead=Ready` 且 runID 与槽位匹配。用户确认中文/英文切换恢复正常。以上证明本次正常路径；并不验证 Nicokobo Forge 效果 API、开局失败清理或物流 API 的游戏内调用。

随后发现杰克逊手写单仍自行补 `MiscItemDirectory`，现也已迁到 Nicokobo Forge 普通物品注册。新增的 `AugMechanicalAscension.dll` 于游戏退出后安装，SHA-256 为 `57F305F92BE69DAFCB0BA25BEFA1F4498FAB510F0EFC007AE562C78567B02A63`，前一版备份于 `D:\workzone\nicokobo-forge\dist\rollback\20260926-132432\`。该增量版编译零错误、领域检查 588 项通过，尚未重启验证手写单 `Applied` 与柜台实际投放。

已废弃并从伪人源码删除三处旧 `MiscItemDirectory.InitDirectory` 直接注册回调及其手工 ID 查重、IL2CPP 工厂委托持有。伪人的构建检查不再重复检查该目录签名，由 Nicokobo Forge 的独立构建门控负责。变声器仍使用 `AmenitiesItemDirectory`，超频效果仍按已验证的伪人原路径注册；它们没有对应的已验证 Nicokobo Forge 替代入口。

清理版 `AugMechanicalAscension.dll` 已在游戏退出后安装，SHA-256 为 `CD6456ED424DBB85B5A1CA95338B34C20EDE8F1407B4E25FEEF50F5F9F6BA06F`；被替换版备份在 `D:\workzone\nicokobo-forge\dist\rollback\20260926-retire-legacy-registration\`。本次编译零错误、588 项领域检查通过，但尚未在游戏内复核这一版。上文 13:20 的实机证据对应更早的 DLL。

后续仍需验证手写单在最新 DLL 的应用与投放、神经接口在机器中的实际超频计算、目录重建时的幂等性、开局失败清理和 Nicokobo Forge 效果 API 自身的安全时机。当前迁移可保留的是节点、鉴定义眼、手写单及开局 ID 认领；超频效果仍由伪人自行注册。
