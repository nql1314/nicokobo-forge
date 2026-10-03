# 0.6.3 生产与品质标签有效状态同步修复

日期：2026-10-01。游戏：Probably Stolen Demo，Steam Build 25382790。

## 故障与证据

用户在 Forge 0.6.2／合成扩展 0.9.7 的新进程中通过 NEI 创建物品。22:04 启动后的 `Latest.log` 有 20 次组件 `Production basis readback mismatch` 和 4 次 `Biomass canister body price mismatch`。前者涉及 `advanced_circuit`、`control_chip`、`mechanical_core`、`power_regulator`、`precision_parts`；后者涉及 `biomass_canister`。机器及熔炉扩展的初始化正常，本轮故障发生在物品工厂配置阶段。

对当前 `GameAssembly.dll` 的离线反汇编确认：`GameItem.state` 与 `modifiedState` 是两个独立状态；`GetTagReadonly` 从后者取标签并克隆，`WaterHelper.GetContainerPrice` 也通过该接口读标签。直接向基础状态字典写入标签后必须调用 `SyncModifiedState`，否则读回的是旧快照。

## 修复

- Forge `SetBasis`、液体价值账本写入、机器隔离标签写入后同步有效状态；隔离标签改用原生只读接口核对。
- 合成扩展品质标签、生物质罐价格与手册内容写入后同步有效状态。
- Forge 升至 0.6.3；合成扩展升至 0.9.8，并要求 Forge 至少 0.6.3。

原有物品 ID、配方、默认价格、品质规则及图标不变。

## 离线验证和发布包

- 框架当前原生元数据签名 50 项通过，新增 `GameItem.SyncModifiedState()`。
- 机器目录 45、批次／恢复 119、价值／账本 32、运行边界 88 项断言通过；六个示例编译通过。
- 回归模型保留基础状态、独立有效状态及只读克隆。用修复前实际 `ForgeProductionValueApi.cs` 编译运行同一检查，稳定复现 `Production basis readback mismatch`；修复代码通过新标签、已有标签更新、零值、大值及只读克隆隔离检查。此模型不等于游戏运行验证。
- 合成扩展领域检查 757 项通过；四个调用者按当前 Forge 编译，三个机核协议包经规范 Release／Obfuscar 流程生成。
- 四个配套包仅保留一份 Forge 0.6.3，核心 DLL 哈希完全一致；框架网站 ZIP 已重新生成。
- Forge SHA-256：`54D08B0237BBD540EFA18C6104A12286CDE36DEFDF3A9E0380D99ADCA99A08B9`。
- 合成扩展发布 DLL SHA-256：`1AC677E3C307C67A4C3BE976865A6663DF7AA6366EA1DB4EB1DD66D251299946`。

## 安装与运行状态

确认用户的游戏进程退出后，重新备份当时的全部存档与配置，并替换为 Forge 0.6.3 和经过 Obfuscar 的合成扩展 0.9.8；旧安装 DLL 已移入本轮备份目录。游戏目录只保留一份 Forge 和一份合成扩展，安装哈希与上面的发布包一致。

22:38 的独立进程 PID 24676 在批处理模式进入 `InventoryScene`。临时检查创建全部 26 个合成扩展物品，结果 26／26 通过；五类组件的基础价值、普通／完美品质标签、品质计价与重复设置幂等性，生物质罐价格、容量、液体账本及清空，以及手册只读标题均通过读回。加载日志确认 Forge 0.6.3／合成扩展 0.9.8，原报错与 `ERROR` 块均为零。该检查阻止了一次新档初始化，未加载玩家存档，只操作无库存归属的临时物品，完成后自动退出。

检查后九个玩家存档、全局设置、索引和 MelonPreferences 共 12 个文件与退出后所备份的当前文件哈希一致；没有新增存档，也无需恢复任何文件。临时检查 DLL 已移出 `Mods`，正式 DLL 哈希再次核对一致。过夜生产、玩家手动 NEI 操作及存档往返不在这项检查范围内。

运行证据：`factory-verification.json`、`factory-latest.log`、`factory-player.log` 和 `final-verification-readback.json`。

证据目录：`D:\workzone\probably-stolen\_build\game-item-error-fix-20261001-90a7df`。原日志、修改前代码、原安装 DLL、反汇编、构建输出及回归对照均保存在该目录。
