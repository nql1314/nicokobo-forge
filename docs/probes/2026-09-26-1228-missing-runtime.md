# 12:28 启动：Nicokobo Forge 运行时缺失

> 命名迁移说明：本文记录发生在 Nicokobo Forge 命名迁移前。产品、程序集、API 与日志标签文字已统一为当前名称；其中散列值和运行结论仍属于当时构建，不能作为当前重命名产物的哈希或运行证据。

2026-09-26 12:28:22 游戏进程启动。`MelonLoader/Latest.log` 在 12:28:29 报 `AugMechanicalAscension.Plugin.OnInitializeMelon()` 无法加载 `Nicokobo.Forge, Version=0.1.0.0`；当时 `Mods/` 列表确实没有三枚 Nicokobo Forge DLL。因此本次启动不能用于判断新的注册、开局或物流通用 API 是否正常运行。

12:29 左右在进程仍运行、三枚目标文件都不存在的情况下，将 `dist/p0/` 的 `Nicokobo.Forge.dll`、`Nicokobo.Forge.ExampleOne.dll`、`Nicokobo.Forge.ExampleTwo.dll` 复制到 `Mods/`，并逐项核对源/目标 SHA-256。Framework 哈希为 `2E0108738AE01A4962057BCCD8946D659A1989BAC6296ABE9F46D113B9454DA0`。本次进程已经完成 Mod 初始化，复制文件不会补救这次加载；须完全退出并重启后读取新日志。
