# 机器槽位原生接纳白名单（2026-10-01）

本地候选：Forge `0.6.0`、合成扩展 `0.9.0`。目标为 Probably Stolen Demo Steam Build `25382790`。本轮未安装或启动游戏。

## 现场

26-10-1 08:25–08:30 会话（`MelonLoader/Latest.log`）共 269 个错误块、538 条
`System.Reflection.TargetException: Object does not match target type`，全部同一条路径：

`InputActionManager.Update` → `ItemMouseDragHandler.StartDrag` → `GridPixelElement/GameGridInventory.MayHaveValidInventorySlot` → `InvokeFuncExtensions.InvokeAllReduce` → `Delegate.DynamicInvokeImpl` → `MethodBase.Invoke`。

时间集中在玩家开始拖动物品之后，约每次鼠标移动一次。9-30 的探针日志抓到同样的槽位现场：`add=System.Func`3[GameItem,GameInventory,bool].Invoke/target=Il2CppInterop.Runtime.Il2CppToMonoDelegateReference`，同一时刻的原生委托（`MachineHelper+<>c.<SetupBatterySlot>b__15_0`、`MachineFurnace+<>c.<Furnace>b__0_0`）从不报错。

结论：`DelegateSupport.ConvertDelegate` 产生的托管委托在 il2cpp 侧报 `Func`3.Invoke`、目标为 `Il2CppToMonoDelegateReference`，而 `InvokeAllReduce` 逐项走 `Delegate.DynamicInvoke`，反射类型不匹配必然抛错。原生闭包（`ContainerHelper.<>c__DisplayClass*`）走同一路径没有问题，所以接纳必须由原生白名单承担。

## 改动

- `ForgeMachineUi.Bind` 不再向 `mayInventoryAddItemFunc` 写入任何托管委托，也不再持有托管委托。
- 物品仓：`ContainerHelper.InitContainerItem(slot, machine, 空标签列表, 冻结配方 ID 列表)`。白名单按本条机器（含追加配方）的投入 ID 汇总。
- 液体输入槽与容器输出槽：`ContainerHelper.AllowOnlyTaggedItem(slot, "LIQUID_CONTAINER_TAG", false, false)`，与原版净水器、湿气农场水槽一致。
- `Registration/MachineCatalog.cs` 新增 `MachineAdmission.InputIds`，作为冻结白名单的单一来源（去重、序稳定）；`MachineProfile._admitted` 改为调用它，避免两处规则漂移。
- 装配后读回实际安装的委托，每槽写一行 `[NicokoboForge/Admission]`；委托缺失或 `interopBridge=True` 记 `WARN`。这是本轮唯一新增的可观测面：以前只有拖动时才暴露。
- `InventoryDragFaultProbe` 改为扫描 `GameInventory` 的所有具体重写。原先只补 `GameSlotInventory.MayHaveValidInventorySlot`，而本轮现场的异常发生在 `GameGridInventory` 上，漏掉了。它仍只在 debug 日志级别安装，现在用于发现**外来**托管桥。
- 模板与配方的槽位／容器条件保持原样，但只在批次结算时生效（`ForgeMachineRuntime` 已经在用水质、纯度、容器条件），接纳不再消费它们。
- `Check-AdapterContracts.ps1` 增加 `ContainerHelper.AllowOnlyTaggedItem` 与 `ContainerHelper.InitContainerItem` 两条原生签名断言（32 条）。

## 本地检查

统一入口：

```powershell
& D:\workzone\probably-stolen\mods-melonloader\Build-ForgeMods.ps1 -GameDir 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo' -DefaultLogLevel INFO -Install
```

- 框架原生签名：32 条通过。
- Forge 领域检查：机器模板 42、批次数学／故障恢复 119、运行边界 88 项断言通过。
- 调用方领域检查：机械飞升 422、模组矩阵 89、物流 L0=21／resource=37／power=27、合成扩展 629 项通过。
- Forge 核心、六个示例与四个调用工程 Release 编译通过；三个机核包经 Obfuscar 生成且共享同一份 Forge。
- 伪人 Mod 与 Forge 已装入 `Mods`；三个机核 DLL 未重装（公共 API 未变，仅内部实现改动）。

产物：

| 产物 | SHA-256 |
| --- | --- |
| `Nicokobo.Forge.dll`（核心与三个机核包内一致） | `1206A77FA87A5E85D4ED90381B48CD53634AA36F543DCB28569A531309437489` |
| `AugMechanicalAscension-0.1.8.dll` | `8AF9D376B4DA78A12DC75FF5980EF2B09C8E17AC1A587A9EB2AC942AD6840662` |
| `MechcoreProtocol.SynthesisExpansion-0.9.0.dll`（Obfuscar） | `A0EA52655A9288EF1BBDEBCC3C07C78C4F2A583093196117323485183AD05BE8` |

## 实机待验收

- 拖动物品经过自定义机器槽位时不再出现 `[ERROR] [Il2CppInterop]` 与 `TargetException`。
- 每台机器在日志里出现 2–4 行 `[NicokoboForge/Admission]`，`interopBridge=False`，`item-input` 的 `detail` 为实际配方 ID 数。
- 物品仓只接纳本机配方投入 ID（含追加配方），丢弃无关物品无效；水槽只接纳 `LIQUID_CONTAINER_TAG` 容器。
- `ContainerHelper.InitContainerItem` 会同时对机器物品做容器初始化。本轮未验证它是否写入额外容器标签／提示，也未验证机器窗口、电池槽与模组仓是否仍按原布局读回；这些必须在实机与 `WarnOnSlotReadback` 日志中确认。
- 接纳只是界面层过滤：形状、堆叠和"是否真的是本批材料"仍由原生槽位与本批事务校验。
