# 2026-10-01 机器容器排版与原生槽位

目标：Probably Stolen Demo Steam Build `25382790`，Forge `0.6.0`、合成扩展 `0.9.0`。版本号及稳定 ID 保持不变。

## 原因与修正

原生 `GridPixelElement` 同时提供 `Attach(PixelElement, minimumWidth, minimumHeight)` 和 `Attach(PixelElement, x, y, minimumWidth, minimumHeight)`。C# 三参数调用选中了前者，电源、模组、输入、输出和备注按顺序填格，标题没有进入标题行。08:52 会话同时出现 `MachineHelper.GetModuleInv` 把 `TagElement` 转成 `GameGridInventory` 的异常及模块槽指针不一致。

- `ForgeMachineUi.Attach` 改为没有歧义的 `AttachPos`，每次读取 `(x,y)` 核对实际附加对象。
- 电池与模块继续占原生固定地址 `(0,1)` / `(1,1)`；物品输入在 `(2,1)`；液体输入从第 3 列起；输出在 `3 + LiquidInputs.Count`，备注再右移一列。标题统一在第 0 行，库存在第 1 行；创建和清空缓存后的发现使用同一规则。
- 默认及 K01–K05 模组使用原生熔炉完整 `7×5` 环形仓（四角和中心十字缺格）。保留原对象及背景，不再缩为 `4×4`。自定义矩形大小创建新原生库存，避免修改已绘制的环形背景。
- 电池默认与供水槽均改为 `1×1` 原生槽；与净水器相同，装入容器后按物品大小扩展。物品输入输出仍为内容侧声明的 `6×4` 网格。
- 液体标签改用原生 `TagElement`，不再使用不同样式的 `RichTextElement`。

原生尺寸和构造调用以当前 `GameAssembly.dll` 与 Build 25382790 的 dump 为依据：熔炉和净水器都用 `GameSlotInventory()`；其默认尺寸为 `1×1`；模块仓构造宽度为 7，形状共 35 格。库存形状中 `0` 为可用格、`1` 为阻挡格。

## 构建与安装

统一入口 `probably-stolen/mods-melonloader/Build-ForgeMods.ps1 -DefaultLogLevel INFO` 通过：

- 框架原生元数据签名 35 项；合成扩展原版熔炉签名 2 项。
- Forge 模板 42、批次数学／失败恢复 119、运行边界 88 项断言。
- 机械飞升 422、模组矩阵 89、物流 L0=21／resource=37／power=27、合成扩展 629 项断言。
- Forge 核心、六个示例、四个调用者 Release 构建以及三个机核 Obfuscar 发布包。

| 已安装产物 | SHA-256 |
| --- | --- |
| `Mods/Nicokobo.Forge-0.6.0.dll` | `32C31367132A780C1732541D38DFD838A98407F888A533C9724B164A44B791D1` |
| `Mods/MechcoreProtocol.SynthesisExpansion-0.9.0.dll` | `FB131BB9BDEC35166FC2FDDF431ACCEDF4F4264EF9F899365858EB477282EEB1` |

安装前游戏未运行。旧 DLL、原日志及用户偏好备份在 `D:\workzone\probably-stolen\_build\forge-layout-check-20261001\backup-20261001-091130`；安装清单在同级 `installation.json`。独立 Forge 发布目录与四个配套包使用同一份 Forge DLL。

## 原生 UI 验证

临时检查工程：`D:\workzone\probably-stolen\_build\forge-layout-check-20261001`。检查仅在 `--forge-layout-check` 启动参数下运行，阻止 `PlayerStore.LoadGame/SaveGame`，进入当前构建的 `InventoryScene` 初始化原生目录，创建不入库的临时机器，不推进游戏日期。

09:21 新进程检查通过，结果为 `result.json` 中 `passed=true`，共 8 台临时机器：

- 五台合成扩展机器的标题均为第 0 行的原生 `TagElement`，库存均在第 1 行；原生电池与模块助手指针全部一致。
- 五台机器的环形可用格均为 `0111110/1110111/1100011/1110111/0111110`（此处 `1` 表示可用，与原生字节编码相反），共 26 个可用格，与原生熔炉逐格相等。
- K04 装入原版 `water_jug`（4×4）成功，容器槽扩大至足够尺寸；输出在第 4 列，备注在第 5 列。
- 自定义矩形仓保持 4×4 共 16 个可用格，无残留环形缺格。
- 两个液体槽使用第 3、4 列，输出和备注相应位于第 5、6 列。纯液体机器省略电池、模块、物品输入和备注的情况通过。
- 8 台机器清空 Forge 窗口指针缓存后，重新发现的输出、备注及各液体槽指针均与原槽一致。

检测到一次被阻止的存档读写入口。临时机器销毁后游戏自动退出，检查 DLL 随后移除，用户 `MelonPreferences.cfg` 从备份恢复。最终日志保存在检查目录 `native-layout.log`；未出现 `TargetException`、槽位类型转换错误或原生助手地址不一致。

批处理模式的截图为黑帧，没有作为画面验收证据。完整排版画面须在正常游戏会话中确认。

此处清空缓存后重新发现槽位只验证窗口读回路径，不等于实际保存或重载。没有验证过夜生产、拖放手势、模组升级和玩家旧存档。
