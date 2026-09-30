# Forge 职责整理候选安装

安装时间：2026-09-30 13:22（Asia/Shanghai）。目标为 `F:\SteamLibrary\steamapps\common\Probably Stolen Demo`，Steam AppID `4349200`、Build `25382790`。安装前游戏进程未运行。

采用上一轮验证的 `forge-refactor-20260930` 候选包：Forge 取包内 DLL，三个机核 Mod 取规范构建入口生成的 Obfuscar 发布 DLL；机械飞升取同轮 Release DLL，并核对 Forge `0.3.3.0` 引用及 `OnLateInitializeMelon` 配方接入方法。

| 安装文件 | 版本 | SHA-256 |
| --- | --- | --- |
| `Nicokobo.Forge.dll` | 0.3.3.0 | `60444EA3157AB1D96A18C771ABE8CCEB95B8CEE3C5D3A614FD96D57F996F0279` |
| `AugMechanicalAscension.dll` | 0.1.5.0 | `F6B5F46C1BB1CB73C9C51B1F4E86991903ADD8C22E3E00EC1B9AA6767E61E323` |
| `MechcoreProtocol.LogisticsNexus-0.1.0.dll` | 0.1.0.0 | `50B3BA89E3782593C0AAEB1501FB057772306AFFA5B4A01B238BA9920491EF72` |
| `MechcoreProtocol.ModuleMatrix-1.0.1.dll` | 1.0.1.0 | `10E09682C04EBCFC2D3E3AC8B6C07581C5D834AF7A541910EEAAC74A89BD6729` |
| `MechcoreProtocol.SynthesisExpansion-0.5.17.dll` | 0.5.17.0 | `1F367692ABA9AB2898BDD0510B5EE2471B37162A4826D5B87E0CA6CCA702DE62` |

旧 DLL 保存在 `D:\workzone\probably-stolen\_build\backups\forge-refactor-install-20260930-132223-a15e136f`。该目录的 `installation.json` 记录旧文件和新文件的路径、版本及哈希。备份哈希均符合安装前记录；安装后独立扫描确认五个目标程序集各只有一份 DLL，5/5 安装哈希与候选一致。

本轮未启动游戏。以上只确认文件安装和读回；新进程加载、原生钩子、图集、机器加工及保存重载尚待验证。
