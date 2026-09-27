# P0 启动尝试：未到 MelonLoader 加载阶段

> 命名迁移说明：本文记录发生在 Nicokobo Forge 命名迁移前。产品、程序集、API 与日志标签文字已统一为当前名称；其中散列值和运行结论仍属于当时构建，不能作为当前重命名产物的哈希或运行证据。

日期：2026-09-26，Asia/Shanghai。使用本地 Steam Build `25382790`；未进入游戏菜单，未创建或加载存档。

1. 从 `dist/p0/manifest.json` 核对 SHA-256 后，将 `Nicokobo.Forge.dll`、`Nicokobo.Forge.ExampleOne.dll`、`Nicokobo.Forge.ExampleTwo.dll` 临时复制到游戏 `Mods/`。
2. 直接从游戏目录启动 `Probably Stolen.exe`，工作目录为游戏根目录，隐藏窗口。第一次约 20 秒后进程已退出，新 MelonLoader 日志文件长度为 0。第二次同步等待得到退出码 `53`，新日志仍为空。
3. 按清单哈希核对并移除三枚 DLL，再按相同方式直接启动游戏。对照启动同样立即返回退出码 `53`，新日志为空。
4. Steam 客户端当时已运行，`appmanifest_4349200.acf` 为本地游戏清单。通过 `steam.exe -applaunch 4349200` 尝试启动，20 秒内未观察到新的游戏进程或 MelonLoader 日志。随后按哈希再次清理本轮临时复制的三枚 DLL。

结论仅限于启动路径：直接启动失败在有无 Nicokobo Forge 时相同；本轮无法判断 Nicokobo Forge 的 MelonLoader 加载、补丁安装及游戏内行为。没有 Nicokobo Forge 专属异常日志。后续应在可交互的 Steam 游戏会话中运行 P0，记录加载器日志、目录时序和两个示例 Mod 的最终状态。
