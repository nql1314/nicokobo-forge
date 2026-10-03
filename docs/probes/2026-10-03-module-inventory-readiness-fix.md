# 模组仓读取就绪检查与配方配置修复

完成日期：2026-10-03，Asia/Shanghai；错误日志来自 2026-10-02 23:46–23:47 会话。目标：Probably Stolen Demo，Steam Build `25382790`，Forge `0.6.5`、合成扩展 `0.9.14`。

## 当前错误

此前启动补丁修复的 Forge（哈希 `CFAB22F4...`）已在该会话加载，游戏进入店铺，原生目录应用和 NEI 运行时目录合并完成。该日志没有再次出现启动阶段的零地址闪退。

合成扩展随后记录 `[Manufacturing] machine refresh unavailable`，原生堆栈为 `MachineHelper.GetModuleInv` 的空引用。Forge 的 `GetInventory` 只检查物品指针后就调用该助手。当前原生实现直接读取 `GameItem.contentWindow`、窗口布局及 `(1,1)`；缺少窗口或布局时抛空引用。普通物品和构建中的机器不能由有效物品指针推断模组仓已存在。

同时，UserData 中的 `recipes.json` 为 schema 7，包含旧液体资源 `Kind=Water`，当前合成扩展 DLL 要求 schema 8。旧文件因此解析失败，整份回退到内置默认配方。

## 修改与验证

- `ForgeModuleApi.GetInventory` 改由 `NativeModuleInventory` 读取已存在的原生窗口，检查窗口、布局、尺寸、模块位置和库存类型。未就绪或没有模组仓时返回 `null`，沿用原生 `(1,1)` 位置；保留公共能力门控。
- 新增 14 项离线检查，覆盖构建各阶段、普通物品内容、窄／短布局、空或错误类型槽位、原生位置、无效指针、仓位移除和窗口替换。它们执行生产读取代码，使用图形结构样本，不运行游戏。
- 92 项互操作签名、原有框架领域检查、8 项补丁入口保护检查、核心与六个示例通过。四个内容 Mod 的领域检查、Release 与正式机核 Obfuscar 打包通过；1287 项公开签名和 Forge 程序集身份不变。构建日志保留 .NET 6 SDK 及独立检查工程的依赖版本提示。
- 根据当前配方代码生成 schema 8 文件，含完整 18 条配方和 5 项生产倍率；写入前和安装后均通过实际解析、应用及定义校验。当前倍率为 K01–K05 的 50%／25%／25%／25%／15%。旧文件完整备份，未添加自动迁移或旧格式兼容。

## 安装与边界

只替换游戏中的 `Nicokobo.Forge-0.6.5.dll` 与上述 `recipes.json`。另外 14 个 Mod 的 DLL 及 `MelonPreferences.cfg` 哈希不变；没有启动游戏、写入玩家存档或发布网站。合成扩展原安装 DLL 的 schema 常量为 8，Forge 引用版本为 `0.6.5.0`。

| Forge | SHA-256 |
| --- | --- |
| 修改前 | `CFAB22F4E9296768F88376BAB5A72F851A8F4D3827BEF8991BF2BA7315DF7433` |
| 修改后 | `200DB945FCDE580327AEE651CFB8E0579B62D8CB2AA2DE04F84DC999D9C7FC74` |

独立 Forge 包及四个内容包已使用同一新 Forge DLL。备份、错误日志、构建日志、兼容性比较、生成配方和 [安装清单](../../../probably-stolen/_build/runtime-errors-fix-20261002/installation.json)位于 `D:\workzone\probably-stolen\_build\runtime-errors-fix-20261002`。

实际读档后的机器效果、过夜处理和新进程日志仍待验证。旧会话的成功启动只适用于上一次修复产物。
