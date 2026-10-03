# 9×6 输入、自动投放与配方生产验证（2026-10-01）

目标游戏为 `F:\SteamLibrary\steamapps\common\Probably Stolen Demo`，Steam Build `25382790`。本轮候选为 Forge `0.6.0`、合成扩展 `0.9.1`、机械飞升 `0.1.8`、模组矩阵 `1.1.0`、物流脉络 `0.2.0`。沿用工作区已有的 API、熔炉职责拆分、原生储水槽及生物质罐外观改动，没有发布线上内容。

## 修正

- K01–K05 的物品输入均声明为 `9×6`，物品输出维持 `6×4`，模组仓保留原生 `7×5` 环形结构。
- 已注册机器的 `GameItem.TryFindOneValidInventorySlot` 优先询问原生电池与模组槽，再询问配方原料输入；水瓶询问储水输入，再询问容器输出。每个候选槽仍使用原生形状、接纳与堆叠判断。K05 的生物质罐同时带液体容器标签，但作为原料时优先进入物品输入。
- 输入白名单使用当前 `MachineProfile.Recipes`，包括机械飞升追加的七条配方。目录更新后重新绑定，先清除旧白名单，避免原生助手将新旧限制按 AND 合并。
- 新物品输出仓补上原版禁止玩家插入的回调。批次产物落点临时绕过该接纳限制，并保留原生形状与容量判断；成功与失败后均恢复原回调和插入锁状态。此前原版熔炉的扩展配方被输出回调挡住，日志显示 `item-output:warehouse full or placement failed`。
- `WaterHelper.GetCurrentCapacity` 返回装满百分比；原生水壶中 1000 ml 返回 `16`。容量改用 `GetFreeCapacity + GetTotalVolume`，以液体 part 计。容器类型由原生标签识别，所有权仍在批次阶段验证。

## 构建与安装

从 `D:\workzone\probably-stolen` 执行 `mods-melonloader/Build-ForgeMods.ps1 -DefaultLogLevel INFO`。39 项 Forge 元数据签名、机器模板 43／批次计算与失败恢复 119／运行边界 88 项断言通过。四个调用者检查为机械飞升 422、模组矩阵 89、物流 85、合成扩展 633；核心、六个示例与四个调用者 Release 编译通过，三个机核发布 DLL 经统一 Obfuscar 打包。既有 `NETSDK1138` 警告不代表原生验收。

安装 DLL 与规范发布目录逐一核对 SHA256：

| 文件 | SHA256 |
| --- | --- |
| `Nicokobo.Forge-0.6.0.dll` | `887FAB63CC0FBB42DB4257DF58C0E6C95A7F1BCD0BCFBC8B2261675ABE4696B3` |
| `MechcoreProtocol.SynthesisExpansion-0.9.1.dll` | `E48E8F4B2654CF727176F0327D19A406F651DB6690DCFF210C96FD1086BD6EA2` |
| `AugMechanicalAscension-0.1.8.dll` | `551CA101D7428DCD22C3D96B08D8C6BE5134981A9300033A8602DA66EEC5A6D8` |
| `MechcoreProtocol.ModuleMatrix-1.1.0.dll` | `322534BA6535BC9B9DBDA5881405D9832E48671C1498749C62B1BFB9EE475C5D` |
| `MechcoreProtocol.LogisticsNexus-0.2.0.dll` | `9575DF5BB5711D7B3985D93CD8CDEDA40D2056B0A8D8409E185BDFC618386EA7` |

四个配套目录里的 Forge 均为同一份 DLL。旧合成扩展 `0.9.0` 已移出 `Mods` 并保留备份；目录中仅保留一份 Forge 程序集。

## 新进程原生检查

临时检查工程与输出位于 `D:\workzone\probably-stolen\_build\forge-machine-fix-20261001`，不进入发布包。用当前安装的 DLL 启动真实游戏进程，加载原生库存场景、创建可丢弃的原生物品，并调用原生投放与批次接口。子进程明确使用游戏自带的 .NET 6 hostfxr，移除继承的 `DOTNET_ROOT` 和 `DOTNET_MULTILEVEL_LOOKUP`，未改变用户全局环境。此次进程正常退出，返回码为 `0`；先前返回码 `53` 的启动记录不作为本轮运行证据。

- `result.json` 为 `passed=true`。23 条自定义机器配方（合成扩展 16、机械飞升追加 7）及四条原版熔炉玻璃回收配方全部实际产出。材料、电池和水容器经 `GameItem.TryFindOneValidInventorySlot`／`SlotMarker.TryAcceptOnce` 装入，检查所属槽位与原生 parent 指针。
- 五台自定义机器均读回 `9×6` 输入。生产核对投入耗尽、正确产物和当前机器耗电 `8`；物品输出在生产后继续拒绝玩家插入电池。原版熔炉产出石英且输出接纳回调指针恢复。
- 三条灌注配方使用有效的 120 点投入价值样本，读回输出 `120000 part`、供水由 `1000000` 降至 `880000 part`，两个容器保持。新生物质罐读回 `2×4`、原生容量 `2000000 part`，并在公共夜间分派中实际接收液体。
- 无电、物品输出满、供水不足、液体输出满、输出容器缺失五项边界通过；相应投入、液体和电量不变。
- K01–K05 和原版熔炉置于本次可丢弃的主库存，调用实际公共 `BeforeNight` 分派。六类机器各产一批；同夜再次调用不重复消耗。K01 的四份石英只消耗两份，余下两份及一份硅保留。

## 原生保存读回与清理

六类机器装入原版 `save_bag`，由 `PlayerStore.EncodeItem` 编码，JSON 写到检查目录的 `machine-save.json`。另一个全新游戏进程使用 `PlayerStore.DecodeSaveItem` 将该原生存档容器解码到可丢弃库存；`reload.json` 为 `passed=true`。六台机器的库存与电量 `992`、K01 的剩余材料和输出、K04 的供水 `880000 part` 与生物质罐中 `120000 part` 均保持；自定义机器输入为 `9×6`，读回后的自动投料仍选中原料输入。

临时探针拦截 `PlayerStore.SaveGame`／`LoadGame`，没有加载或覆盖玩家的六个正式存档。库存场景的新游戏初始化仍写入了空存档索引条目；清理时恢复原字段与顺序，并以运行前 SHA256 `9E20219B0EC6423C3F520ED2F2A126E7A8B2DC1D1CA95F909A607ACAE8EB5B1D` 确认 `saves_index.es3` 完整还原。正式存档、`SaveFile.es3` 和索引共八个文件的前后哈希一致，见 `save-hashes-before.json`／`save-hashes-after.json`。临时探针已移出 `Mods`，原 `MelonPreferences.cfg` 完整恢复。

本轮验证的是原生接口投放、实际事务、公共 BeforeNight 分派以及独立 JSON 编解码。没有模拟正常画面中的鼠标手势，也没有调用完整的 `PlayerStore.EndNight` 或对玩家存档执行保存／载入往返；这些不应由上述探针成功推断。存档容器用于编码包装，夜间生产检查中的机器位于主库存。
