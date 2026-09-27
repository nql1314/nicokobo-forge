# 伪人 Mod 所需 API：当前接入

> 命名迁移说明：本文记录发生在 Nicokobo Forge 命名迁移前。产品、程序集、API 与日志标签文字已统一为当前名称；其中散列值和运行结论仍属于当时构建，不能作为当前重命名产物的哈希或运行证据。

日期：2026-09-26。当前持久标识以 `nicokobo.aug` 为准。目标仍是 Steam Build `25382790`。

## 本轮实现

| 能力 | Nicokobo Forge 负责 | 伪人 Mod 保留 | 状态 |
| --- | --- | --- | --- |
| 神经接口节点 | `ForgeNativeApi.RegisterNode` 认领原生 ID，在 `MiscItemDirectory.InitDirectory` 后安装并持有工厂委托，检查目录冲突及工厂结果的 ID/NODE 类型 | 1×1 模板、8%/8%/4% 基础属性、25% 超频、原生效果与出售/丢弃保护 | 12:30 已装伪人 DLL 日志为 `Accepted`，未见 `Applied`；当前伪人源码已改回自行注册，下一次构建须重新接入 |
| 模组/节点效果 | 新增 `ForgeNativeEffectApi.RegisterEffect`，管理 owner/ID、原生效果表安装和随机池资格 | 超频回调、显示文本和具体数值 | 框架与领域检查通过；伪人当前源码尚未接入此入口，游戏内效果待验证 |
| 独立开局身份 | `ForgeStartApi.RegisterStart` 认领稳定开局 ID 与当前构建的原生 55 号承载，拒绝重复占用 | 菜单选择意图、首次保存、槽位/runID 绑定、初始物资与读档恢复 | 12:30 已装伪人 DLL 日志为 `Accepted`；当前源码未调用 Nicokobo Forge，下一次构建须重新接入 |
| 普通物品、NPC、看板等 | 本轮不扩展 | 原有逻辑照常 | 暂缓 |

当前 `ForgeNativeApi.Snapshot()` 和 `ForgeNativeEffectApi.Snapshot()` 可按 ID 查询最后观察到的 `Staged / Applied / Conflict / Failed`。节点和效果应在目录初始化前注册；`Accepted` 不是应用成功。模块目录钩子独立于普通物品目录钩子，模块签名不匹配时只关闭模块注册。

伪人当前节点 ID 为 `nicokobo.aug.neural_interface_module`，开局 ID 为 `nicokobo.aug.mechanical_ascension.start`。注册中心检查覆盖当前 ID、重复调用、异工厂/异开局冲突和跨 owner 冒用。Nicokobo Forge 不将编译或磁盘安装算作运行成功。

12:30 的伪人 DLL 与随后源码曾不一致。最新源码已重新接入 Nicokobo Forge：开局 ID、神经接口节点和鉴定义眼采用 Nicokobo Forge 注册；超频效果在首次迁移试验中触发过早的随机补丁安装，现已恢复伪人原路径。13:20 最新 DLL 的正常开局、目录应用及读档证据见[迁移核对](AUG_MECHANICAL_ASCENSION_MIGRATION_AUDIT.md)。本文件前面的 12:30 表格与下方旧安装记录是历史阶段，不代表当前运行状态。

## 构建与安装

- `Build-P0.ps1` 的注册中心检查通过；框架与两个示例编译为零警告、零错误。
- 伪人 Mod 编译为零错误；本地 NuGet 漏洞源不可达产生 `NU1900` 警告。其独立领域检查为 `Passed 588 domain checks.`
- 本轮已将 Nicokobo Forge 三枚 DLL 和 `AugMechanicalAscension.dll` 放入游戏 `Mods/`，逐项核对安装后的 SHA-256。替换前文件与收据保存在 `dist/rollback/20260926-120608/`；采用 `nicokobo.aug` 标识重新构建的伪人 DLL 另有 `dist/rollback/20260926-120908/` 的前版备份。

## 下一次游戏验证

2026-09-26 12:28 的启动因 `Mods/Nicokobo.Forge.dll` 缺失导致伪人插件初始化失败；三枚 Nicokobo Forge DLL 已重新放回并核对哈希，但该进程不会自动重新加载。见[记录](probes/2026-09-26-1228-missing-runtime.md)。以下验收必须在完整重启后执行。

1. 启动后确认日志出现 `[NicokoboForge/NativeItem] miscHook=installed`，且伪人当前构建确实提交 `[NicokoboForge/Start] ... status=Accepted` 与 `[NicokoboForge/Node] ... status=Accepted`；目录初始化后查 `ForgeNativeApi.Snapshot()` 的节点结果为 `Applied`，没有 `Conflict`、`Failed` 或补丁错误。
2. 用可丢弃开局确认 55 号卡仍可选择，神经接口创建、初始发放、首次存档与读档回读使用当前 `nicokobo.aug` ID 和既定数值；重进菜单或读档不重复注册。旧命名空间存档的兼容性需单独核对。
3. 上述闭环成立后，再把超频效果注册与随机池排除迁入 Nicokobo Forge 的模组/效果 API，并逐步抽取开局选择与保存适配。完整开局失败清理和其他注册类别不因本轮节点 API 自动成立。

最新 Nicokobo Forge 包的构建、安装哈希与尚待运行的边界见[12:58 安装记录](probes/2026-09-26-1258-installed.md)。
