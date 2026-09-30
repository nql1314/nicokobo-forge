# 工坊星图安装记录

日期：2026-09-29。目标目录：`F:\SteamLibrary\steamapps\common\Probably Stolen Demo\Mods`。

Steam 清单为 Build `25382790`；`GameAssembly.dll` SHA-256 为 `3BEA17EEEC77ADAB6418918C28A8AA9A06F290582A2972639A48BD7E5A01AB44`，`MelonLoader/Il2CppAssemblies/Assembly-CSharp.dll` 为 `9618787115686C0AF6DBC3B65BEE6DEA9DD33A8A998D01572CDC190A42657ACC`，均与 Forge 的已知构建常量一致。安装前没有正在运行的 Probably Stolen 进程。

先以 `WARN` 为默认日志等级运行 `scripts/Build-P0.ps1`，领域检查通过，核心、两个 P0 示例及物流示例 Release 编译均为零错误；再串行编译工坊探针和机械飞升 Mod，均为零错误。工坊探针与机械飞升编译有 NuGet 漏洞源不可达的 `NU1900` 警告。

替换前将三项旧 DLL 备份到 `D:\workzone\nicokobo-forge\dist\rollback\workshop-star-preinstall-20260929-014055`。沿用游戏 `Mods` 顶层的原有文件名，未额外放置第二份 Forge 主 DLL。

| 安装文件 | 安装后 SHA-256 |
| --- | --- |
| `Nicokobo.Forge-0.1.0.dll` | `80C57E0DEAEA5AF643730AD205796F771850D09C42B08E4BF556ECC4B116792E` |
| `Nicokobo.Forge.WorkshopProbe.dll` | `3E42232C1656CDA3A26DBC0C933BFB950FA7A482BE3CE5EC40DDD65F0A817131` |
| `AugMechanicalAscension.dll` | `E7776F0E9F5744760AC35F77142E6F7ABBA80D29F3E987F7E904D5A07846E63D` |

三项安装文件与各自本轮 Release 构建文件的 SHA-256 完全相同。以上只证明磁盘安装；尚未启动新游戏进程，星图布局、两条探针链、机械飞升交易、奖励及存档重载均未在本轮验证。
