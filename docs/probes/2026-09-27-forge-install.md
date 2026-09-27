# 2026-09-27 Nicokobo Forge 安装记录

目标：`F:\SteamLibrary\steamapps\common\Probably Stolen Demo\Mods`。安装前确认游戏进程未运行；`GameAssembly.dll` SHA-256 为 `3BEA17EEEC77ADAB6418918C28A8AA9A06F290582A2972639A48BD7E5A01AB44`，生成的 `Assembly-CSharp.dll` 为 `9618787115686C0AF6DBC3B65BEE6DEA9DD33A8A998D01572CDC190A42657ACC`，均与 Forge 当前构建门控常量一致。

重新运行 `Build-P0.ps1`，领域检查通过，核心、两个 P0 示例与物流示例 Release 编译成功；领域检查有 .NET 6 生命周期提示。工坊与效果探针另行 Release 编译成功。`Pack-P0.ps1` 重新生成核心与两个 P0 示例的包及哈希清单；逐项核对包内哈希后，将这三枚和已经安装的两枚探针一同更新到 `Mods`。物流示例库没有安装。

| 安装文件 | 安装后 SHA-256 |
| --- | --- |
| `Nicokobo.Forge.dll` | `CBF0E264846B546B9EDE626566820B3179A0F8808F85C742626D08C5767190D1` |
| `Nicokobo.Forge.ExampleOne.dll` | `FDDB1AB698F7F1580DFA112FEF6BD8ECDEF1D0FBB89B2E909824061F87B67A48` |
| `Nicokobo.Forge.ExampleTwo.dll` | `E5C9F55E5A63EF72829412388EF02571AAC2913A3DCC28C8C6C3C75B70EBA74E` |
| `Nicokobo.Forge.EffectRegistrationProbe.dll` | `1A54C0D636099EE1BD54452461A8E9BFE641747D52027E61B69FC9F796B939B6` |
| `Nicokobo.Forge.WorkshopProbe.dll` | `BA3DEBFBD085E3FACD4F4135DE6BD4F591B21AB199FD8398409FEDB2C276E006` |

安装前的五枚 DLL 与旧哈希保存在 `dist/rollback/forge-20260927-101229785/`，该目录的 `install-receipt.json` 记录旧版与新版对应关系。安装后独立回读 `Mods` 与备份文件，五项 SHA-256 均匹配收据。其他 Mod 文件未替换。

安装完成时游戏仍未运行；`MelonLoader/Latest.log` 最后修改时间为 2026-09-27 09:58:50，早于本次 10:12 安装。因此本记录仅证明构建、打包、文件安装与哈希一致；当前 DLL 的加载、钩子安装、原生应用、工坊/网络升级行为及存档重载仍待新进程验证。

随后 10:15 启动的新进程观察见[运行记录](2026-09-27-1015-forge-runtime.md)。
