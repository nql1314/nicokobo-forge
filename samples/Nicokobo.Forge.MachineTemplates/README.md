# 机器模板接入示例

`MachineExamples.cs` 展示物品与液体同时输入、纯液体输入、物品输入灌入现有容器三种声明。它是可编译的开发示例库，没有 MelonLoader 入口，也不会自动增加游戏内容。`scripts/Build-P0.ps1` 会构建该库。

机器、电池槽、模组仓、手册槽和输出仓由 Forge 创建；内容方可以在 `ConfigureItem` 设置名称、图标、占格与数值。容器输出需要玩家先在输出槽装入容器。完整契约见 [Machine API](../../docs/MACHINE_API.md)。
