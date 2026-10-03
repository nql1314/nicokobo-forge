# Forge 与配套 Mod 安装记录

日期：2026-10-02，Asia/Shanghai。目标：`F:\SteamLibrary\steamapps\common\Probably Stolen Demo`，Steam Build `25382790`。

## 构建与安装

安装前确认游戏未运行，并核对当前原生二进制为支持的构建。机械飞升在上次打包后有源码更新，已重新执行 `Build-ForgeMods.ps1`：82 项框架签名检查、70 项成就检查、核心与六个示例、四个依赖 Mod 的领域检查和配套 Release 打包通过；机核系列沿用正式 Obfuscar 打包流程。

将本次包冻结至安装备份目录，备份旧 DLL 后替换以下五个程序集。通过文件读回、SHA-256、程序集版本、Forge 引用版本及同程序集唯一性检查；其余十个已安装 Mod 的 DLL 哈希保持一致。

| 已安装文件 | SHA-256 |
| --- | --- |
| Nicokobo.Forge-0.6.5.dll | `B83E485C802D9383970ECA18C26D50B61DB180C915DF89C0A8A3AE35B4E1DA0E` |
| AugMechanicalAscension-0.1.18.dll | `D94663E4BC871478337B286D5F5BAE89886BA70034E7FAD9DBB8E14806E371CE` |
| MechcoreProtocol.ModuleMatrix-1.1.0.dll | `EB2769440359A54B23BBAD32B23EE8456E35690EE49588793C555A65E0F58C64` |
| MechcoreProtocol.LogisticsNexus-0.2.0.dll | `7238F3804F30B1AB7C46EE16C025038936963BB744F5C9FFCDA8E88FB7F10AE6` |
| MechcoreProtocol.SynthesisExpansion-0.9.14.dll | `83FE2C8B9E57514AD0BC6D6F4355D5392EAD1489848072D85B443D5129D27E9C` |

四个内容 Mod 的程序集均引用 Forge `0.6.5.0`。旧 Forge `0.6.4`、机械飞升 `0.1.15`、合成扩展 `0.9.13` 以及同版本的旧矩阵／物流 DLL 已移出 `Mods`，保留在备份的 `originals/` 中。

备份与本次冻结的包位于 `D:\workzone\probably-stolen\_build\installations\forge-workshop-20261002-224630-6b670195`；[安装清单](../../../probably-stolen/_build/installations/forge-workshop-20261002-224630-6b670195/installation.json)记录安装前后文件和哈希。

## 验证边界

本轮只更换上述 DLL，没有启动游戏。目录注册、名片与工坊操作、奖励、义体效果和存档重载仍需游戏内验收。本记录证明当前文件已安装，与之前仅完成构建的[源码验证](2026-10-02-native-workshop-implementation-validation.md)分开。
