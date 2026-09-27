# 12:59 重启：新包装载与效果门控缺陷

> 命名迁移说明：本文记录发生在 Nicokobo Forge 命名迁移前。产品、程序集、API 与日志标签文字已统一为当前名称；其中散列值和运行结论仍属于当时构建，不能作为当前重命名产物的哈希或运行证据。

日期：2026-09-26。Steam 启动游戏后，`MelonLoader/Latest.log` 记录本次 Nicokobo Forge 包的目标 Build `25382790` 哈希匹配；四个只读探针、普通物品目录钩子和模块目录钩子均安装成功。普通目录完成、首次保存入口、晚期读档探针均实际触发。伪人这版仍自行注册内容：Nicokobo Forge 的 `stagedNodes=0`、`stagedEffects=0`，因此没有可观察的 Nicokobo Forge `Applied` 项。

发现 `ModuleEffectHelper.moduleEffects` 在 IL2CPP 包装程序集对外呈现为**静态属性**；探针误以反射字段读取，日志为 `moduleEffects:MISSING; match=False`，关闭了 `NativeEffectRegistration`。已改为检查静态属性，并经 `ilspycmd` 对照包装程序集确认 getter 与原生字段对应。修正版已重新通过领域检查与编译，包内 `Nicokobo.Forge.dll` SHA-256 为 `EE69D0485207D4E5475BA910DD97D1D9FC6A677F37BE25BA9BADA01F81D07E82`。本次游戏会话仍运行旧的 `87F1623BDDBE59C16B7CED9B2B69A6705A5B2394AD1588746A735FA542A7CB3D`，须退出后再安装和完整重启，不能用本次运行证明修正版门控。

库存读取与周目暂存只通过签名门控，尚无这些 API 的实际调用或读回结果。首次保存探针在原生身份尚未形成时记录 `slot=-1; run=`，这时 `ForgeRunDataApi.Stage` 会拒绝写入；内容 Mod 应在有效 `runID` 和槽位出现后暂存，并在游戏保存与重载后读回。

13:02 的再次启动使用修正版，日志显示 `moduleEffects ... match=True` 和 `registrationAllowed=True`。由于当前伪人插件自行注册效果、Nicokobo Forge 的 `stagedEffects=0`，按需效果钩子没有安装；因此尚未验证 Nicokobo Forge 的效果 `Applied` 或随机池排除行为。13:00 的可丢弃档日志还记录原生初始物品使用负数 `uniqueId`（如 `dossier=-101`）；这触发后续对库存只读快照的修正，13:02 运行的 DLL 尚未包含该修正。

13:04 游戏退出后，含库存快照修正的最新 `Nicokobo.Forge.dll` 已安装，包与 `Mods/` SHA-256 同为 `E297887AC9A89D0EFF5D8679A44DA9A92895753EB924934411551AA8E0B064A7`；两个 P0 示例 DLL 也与清单一致。被替换文件备份于 `dist/rollback/20260926-130418/`。这次库存修正仍只具备编译与安装证据，尚无新一轮游戏内 `CaptureDirect` 调用。

后续静态复核发现 P0 只读探针与原生注册入口被同一个安装结果耦合。已拆分为能力各自门控并通过完整构建；13:04 安装包仍是拆分前的版本，拆分后的包需另行安装与启动核对。

13:06 游戏已退出，拆分门控后的三枚包文件已安装并逐项匹配 SHA-256：`Nicokobo.Forge.dll` `AF0EE22286B78ED4CDCD2124B231C026560A03361FA4E1DFDAAB2E00C658001A`，`Nicokobo.Forge.ExampleOne.dll` `55877A4C41304E7CFE8DDB6EB5964290F3B48D44520C159F05174FE741197D78`，`Nicokobo.Forge.ExampleTwo.dll` `54C6D7B2F5A097A711FEC16D800EBD8A916F1523CB0F3399F98E5FAA0A95F7F0`。备份在 `dist/rollback/20260926-130620/`。此最终包具备领域检查、编译和安装证据；13:02 的游戏运行属于前一版，最终包仍待下一次自然启动核对。
