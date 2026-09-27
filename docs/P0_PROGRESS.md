# P0 开发记录

> 当前能力状态、审查修复及下一次验收见[Forge 当前进度](FORGE_PROGRESS.md)。本文保留 P0 阶段的历史证据。

> 命名迁移说明：本文记录发生在 Nicokobo Forge 命名迁移前。产品、程序集、API 与日志标签文字已统一为当前名称；其中散列值和运行结论仍属于当时构建，不能作为当前重命名产物的哈希或运行证据。

日期：2026-09-26。目标构建：Steam Build `25382790`。

## 已实现

- 独立 Git 项目和 .NET 6 MelonLoader 工程。
- 启动时读取 `GameAssembly.dll`、生成的 `Assembly-CSharp.dll` SHA-256，检查六个关键方法的完整签名与已有 Harmony 补丁 owner。
- 构建与签名匹配后安装四个只读探针：普通物品目录初始化完成、Perk 面板打开、首次存档进入、游戏加载晚期完成。安装不完整时已有探针保持惰性。
- 跨程序集公共入口 `ForgeApi.Register`：按 owner 存放定义；验证命名空间 ID、版本、最低依赖版本、重复项、同批次引用和声明重放；依赖乱序时等待。快照明确显示“依赖已解析，原生内容尚未应用”。
- 两个独立示例 Mod：一方声明物品/效果元数据，另一方依赖前者。示例不会写入游戏目录。
- 无游戏依赖的注册中心检查程序及 P0 构建/打包脚本。

## 已验证

| 检查 | 结果 | 证据边界 |
| --- | --- | --- |
| 当前本地游戏与方案基线的哈希 | 匹配 | 只证明目标文件身份 |
| 框架及两个示例 Mod 编译 | 通过，零警告/错误 | 不证明 MelonLoader 实际加载 |
| 注册中心检查 | 通过：乱序、同 Mod 引用、重复、冲突、依赖环、最低版本 | 仅纯逻辑；.NET 10 SDK 对游戏所需的 net6.0 报 EOL 提示 |
| 诊断包 | 三枚 DLL 与 SHA-256 清单生成成功 | 只证明包内文件完整 |
| 游戏启动尝试 | 直接启动与无 Nicokobo Forge 对照均立即退出码 53，Steam 入口未产生新进程/日志 | 见 [记录](probes/2026-09-26-startup-attempt.md)；Nicokobo Forge 尚无游戏内加载证据 |
| 完整游戏运行 | 三枚 Nicokobo Forge DLL 加载，六个签名匹配；四个只读探针各触发一次，两个示例最终均为 `DependenciesResolved` | 见 [运行记录](probes/2026-09-26-p0-runtime.md)；仅证明本次新局、首次存档与读档路径，原生内容尚未应用 |

## 已取得及仍需补充的游戏内证据

1. 已在一轮可丢弃存档中核对三枚 DLL 的加载、程序集身份和四个探针的实际日志；仍需在游戏更新后重新核对构建与签名。
2. 当前初始化顺序下两个示例均为 `DependenciesResolved`；反向初始化顺序尚未验证。若加载器将来因引用缺失拒绝加载，再验证依赖元数据，必要时拆分公共契约程序集。
3. 已记录目录初始化完成早于首次保存、读档晚期回调；仍需确认原生内容在存档解析前可用的实际接入时机。
4. 针对首次存档、Perk 点数重算、看板和机器成功工作分别补完整探针；此时再启用 P2 原生注册。

P0 的四个只读探针已在一次新局和读档中触发，但完整 P0 方案仍有未验证入口。本文件记录当时的诊断版；随后已新增面向伪人 Mod 的节点注册与开局身份 API，当前 DLL 不再仅供只读诊断，其范围和待验收项见[伪人 API 接入进度](AUG_MECHANICAL_ASCENSION_API_PROGRESS.md)。

## 当前安装状态

2026-09-26 重新运行 `Build-P0.ps1` 与 `Pack-P0.ps1`，注册中心检查通过，框架及两个示例编译均为零警告、零错误。安装前核对了 `GameAssembly.dll` 和生成的 `Assembly-CSharp.dll` SHA-256，均与本文件目标构建的基线一致。已将清单中的 `Nicokobo.Forge.dll`、`Nicokobo.Forge.ExampleOne.dll`、`Nicokobo.Forge.ExampleTwo.dll` 各一份复制到游戏 `Mods/`；三枚安装文件的 SHA-256 均与 `dist/p0/manifest.json` 一致。

## 首次游戏启动观察

2026-09-26 11:27:50 的 MelonLoader 会话确认三枚 Nicokobo Forge DLL 加载，哈希均与清单一致；目标游戏构建及六个方法签名匹配。两个示例的声明均被接受，最终日志状态均为 `DependenciesResolved`，原生内容仍未应用。该会话中 `readOnlyProbesInstalled=False`：先前加载的 `AugMechanicalAscension.dll` 在补丁 `MiscItemDirectory.InitDirectory()` 时留下 Harmony 参数 `directory` 不匹配错误，Nicokobo Forge 随后安装同一方法的只读探针失败，四个探针因此全部保持惰性。此会话的 `AugMechanicalAscension.dll` 日志哈希为 `0CC7C1BBC67276A6BB643CC8722C3654EB1EDE96AD18354F7E1107BB7BEFBF09`；会话运行期间磁盘文件已更新为另一哈希。11:49:06 的完整重启后，四个只读探针均已安装并各触发一次，详见[运行记录](probes/2026-09-26-p0-runtime.md)。
## 2026-09-26 命名迁移

- 项目根目录统一为 `D:\workzone\nicokobo-forge`；工作区引用同步更新。
- 主程序集和命名空间统一为 `Nicokobo.Forge`；公共入口统一为 `ForgeApi`、`ForgeNativeApi`、`ForgeNativeEffectApi`、`ForgeNativeInventoryApi`、`ForgeRunDataApi`、`ForgeStartApi` 与 `ForgeCapabilities`。
- 示例、效果探针、领域检查工程及示例 owner ID 统一使用 `Nicokobo.Forge.*` / `nicokobo.forge.*`。
- `probably-stolen/mods-melonloader/aug-mechanical-ascension` 已改为引用新项目与新 API；588 项领域检查通过，Release 编译零警告、零错误。
- Nicokobo Forge 领域检查通过，框架、两个 P0 示例、物流示例和效果探针均 Release 编译零错误；`dist/p0/manifest.json` 已按新文件名重建。
- 当前安装哈希：`Nicokobo.Forge.dll` `DC407ECD999C3DE4D4E870046B42CF89F2E46DE6C8E6DEB475BCD8DA036B30F7`；`Nicokobo.Forge.ExampleOne.dll` `FF5AF16A3A26AE95C12A28025ACBE47327B127DA5272960E65FF06EA14D62390`；`Nicokobo.Forge.ExampleTwo.dll` `E2D0616474E2F25A2B24CD007C0C1D6521A952AF7C4F2466CA04D87EBDC32F40`；效果探针 `AAA58D9B222F1ACE956826F3D174AEC1B241EA977E716AF306034FADBF7D8982`；`AugMechanicalAscension.dll` `C7875462523F46E159752C6D02FB229E6B9C28EEA8A656700274BF655B2D579E`。
- 旧名 DLL 已备份并从游戏 `Mods/` 移除。上述证据止于源码、领域检查、编译、打包和安装哈希；新程序集身份仍待下一次完整启动读取 MelonLoader 日志并复核游戏内行为。
