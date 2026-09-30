# Nicokobo Forge 当前进度

## 2026-10-01 0.3.9 按批用水量、液体收尾与进度版本候选

按用户要求（合成扩展把 K04 改成按投入价值灌装液体生物质），这一版补三处液体配方能力。其一，`ForgeMachineRecipe.ResolveWaterMillilitres(machine, input)` 把“每件产物固定毫升数”换成按批计算：返回值参与可用量判断与扣水，因此内容方可以让水耗跟着本批实际投入走（合成扩展用它实现每 1 点投入价值扣 1 ml），返回非正值或抛错按 `water-rule-invalid` 失败且不动任何状态；声明该回调时 `MillilitresPerOutput` 可以是 0，未声明时仍要求大于 0。其二，液体模式同样在扣料前逐件调用内容方的 `PrepareOutput(machine, input, batchInputs, product)`：产物已生成并落到输出区、但电量、水位与投入物尚未变动，回调抛错则召回产物并恢复水位与电量，内容方因此可以在产物上灌装液体并校验读回。其三，`ForgeMachineRecipe.ProgressVersion`（默认 1）标记配方的批次计价语义，写进度时一并落盘；读到版本不符或更早记录格式（`LiquidProgressVersion` 由 1 升到 2）的进度时，Forge 丢弃记录、清空标签并从槽内投入重新开批，未清空成功则记 `liquid-progress-clear-failed` 并保持不动，避免存档按旧目标继续加工。合成扩展 0.6.0 用这三项实现“灌装量 = 投入总价值、水耗 1:1、基础水及以上”，并把灌注配方标为 `ProgressVersion=2`。领域检查与 Release 编译结果见[本轮记录](probes/2026-10-01-infusion-liquid-output-validation.md)。

## 2026-10-01 0.3.8 整叠按需消耗候选

按用户要求，整叠液体配方改为按价值从大到小取用：选择投入时在同一配方的候选物品里挑单位价值最高的（同价值取数量更多者），完成一批时用可选的 `ForgeMachineRecipe.ResolveConsumedUnits(item, targetCount)` 决定只扣多少整件（未声明仍消耗整个投入物），剩余整件留在投料槽；失败回滚恢复被扣数量，越界或 0 件返回 `spent-units-invalid`。`ResolveLifetimeOutputCount` 返回 0 视为“这次投入不够一批”，Forge 记 `input-below-batch` 并保持状态不变。合成扩展 0.5.23 用它在 K04 实现“每 100 价值 1 件、向下取整、余料留下”。领域检查与 Release 编译结果见[本轮记录](probes/2026-09-30-native-batch-probe-validation.md)。

## 2026-10-01 0.3.7 液体整叠投入候选

按用户要求，`ForgeMachineRecipe.AcceptsStackedInput` 让 `NightlyLiquid` 配方接受整叠投入：整叠视为一份批次投入，保留到目标件数完成才一次消耗，进度记录里的 `InitialValue` 改记整叠的合并价值（`MachineLiquidMath.MergedStackValue`，饱和处理）。未声明的配方保持原有“单位数量必须为 1”的拒绝行为，`NightlyItems` 上声明该属性按无效注册处理。合成扩展 0.5.22 用它在 K04 接收营养果并按整叠合并价值计算凝胶件数，配方配置同时升到 `SchemaVersion=4`。领域检查与 Release 编译结果见[本轮记录](probes/2026-09-30-native-batch-probe-validation.md)。

## 2026-09-30 0.3.6 原版批次探针候选

按用户要求，`RegisterExistingMachine` 增加可选的"原版批次探针"参数：探针为 `true`（游戏自己的熔炉循环对该投入还有事可做）时该机器注册的配方整夜不参与，原生循环保留该夜；探针为 `false` 且槽内确有注册原料时，Forge 跳过原生循环并自行加工注册配方，槽内的其他材料原样留下。槽内没有注册原料时原生循环一律照常执行，垃圾处理等非玻璃玩法不受影响。探针抛错按 `true` 处理，把该夜交还原生流程；探针只能声明在原版机器上，注册到自定义机器返回 `Invalid`；后续追加注册沿用既有探针。未声明探针的旧注册保持"仅纯玻璃投入才跳过原生循环"的行为。合成扩展 0.5.20 用该探针按目标构建的规则判断：`common_ore` 与 `scrap_metal` 凑够 2 件（装 `furnace_module_blast` 时 3 件）即原版优先，装了 `furnace_module_junk` 且槽内有垃圾时也让位原版；其余情况回收玻璃，因此玻璃与原版矿石可以共用投料槽而互不吃料。领域检查与 Release 编译结果见[本轮记录](probes/2026-09-30-native-batch-probe-validation.md)。同轮合成扩展 0.5.21 另把配方配置升到 `SchemaVersion=3`（P03 要求高纯度金属锭、P04 要求高纯度硅），旧配置备份后自动补门槛；随后按用户请求安装了 Forge 0.3.6 与合成扩展 0.5.21，安装与哈希见同一记录。

## 2026-09-30 0.3.5 每晚单批与配方输入候选

按用户要求移除性能加批：扩展固体和液体机器每晚只处理一批，原版熔炉不再额外调用加工回调。`ResolveBatchCount` 保留成员签名兼容已有内容 DLL，但运行时不再调用；合成扩展 0.5.19 与伪人 K05 追加配方也已去掉该回调。熔炉在原生矿石判定前接纳已注册配方的输入，包含碎玻璃和酒瓶，其他原版物品继续使用原生判定。配方材料与辅助物品索引保持注册时冻结。

Forge 领域检查与核心、两个 P0 示例及物流示例 Release 构建通过；合成扩展 522 条、伪人 Mod 419 条领域检查通过，两者 Release 编译零警告零错误，合成扩展 Obfuscar 发布包及六项文件 ZIP 哈希检查通过。编译产物确认单批分派与无熔炉重复调用。本轮读取的启动日志加载已安装的合成扩展 0.5.17 和 17 条配方；构建完成后观察到游戏已退出，本轮候选尚未安装，不能将此前日志归于新代码。构建和发布包记录见[单批验证记录](probes/2026-09-30-single-night-recipe-input-validation.md)。

下方性能重复加工候选及其验证记录为历史实现，已由 0.3.5 的每晚单批规则取代。

## 2026-09-30 0.3.4 机器电耗与熔炉重复加工候选

原版熔炉只在加工回调中按内容方声明的性能次数复用原生批次。观察到投入减少、产物增加和电池能量正常后才继续；模块装入/移出回调保持原生执行，原版纯度及助溶剂计算不被覆盖。扩展固体机器每次重新选择可用配方并读取电耗，支持机器原生零电耗，负值拒绝。合成扩展 0.5.18 及伪人 K05 追加配方统一读取机器当前电耗；配方文件升级为 SchemaVersion=2，补入碎玻璃和酒瓶回收，硅加工不再强制推导金属纯度。

Forge 领域检查（含原生批次成功/失败/零电耗继续条件）与 P0 Release 全构建通过；合成扩展 529 条、伪人 Mod 419 条领域检查通过，两者 Release 编译零警告零错误，合成扩展 Obfuscar 发布包已生成。本地包位于 `D:\workzone\probably-stolen\_build\releases\synthesis-machine-20260930`。这组产物尚未安装或进入游戏；过夜、原版纯度表现及保存重载仍需复测，详见[本轮本地验证记录](probes/2026-09-30-machine-rules-local-validation.md)。

## 2026-09-30 职责与重复流程整理候选

13:22 已按用户请求将本轮 Forge 与四个调用 Mod 安装到 Demo Build `25382790`。游戏关闭时备份旧 DLL，再替换五份候选；安装后独立读回确认 5/5 哈希一致且每个目标程序集只有一份。备份与逐项哈希见[本轮安装记录](probes/2026-09-30-forge-refactor-install.md)。本轮尚未启动新进程，文件安装不代替加载和游戏行为验证。

`Plugin` 只保留加载器适配，`ForgeBootstrap` 统一安装功能并发布门控。机器声明模型、注册目录、原生钩子和加工公共步骤分开维护；注册校验合并，输入和辅助物品列表在注册时冻结，追加配方发布完整配置。投料使用预先建立的索引，夜间发现机器时保留同次配置，避免排序快照和重复目录查询。固体与液体共享产物创建、放置和动作通知；没有产物预处理回调时省去投入展开列表，空位探测恢复真实 ID 后仅接纳一次。逐批验料、能源和液体读回、失败恢复及同夜去重仍保留。

四个调用工程共用 Forge DLL 和原生程序集引用配置；机械飞升在 `OnLateInitializeMelon` 一次追加配方，替代逐帧时间判断、目录快照和反射。机械飞升、模块矩阵、合成扩展共用内容侧 PNG 图集工具，等待就绪的查询节流到 250 ms，安装完成后停止工作，失败时恢复旧缓存并释放可安全回收的资源。物流探针在不可用的目录门控下停止扫描，工厂验证逐件释放临时物品。

统一入口为 `probably-stolen/mods-melonloader/Build-ForgeMods.ps1`，详见[开发说明](DEVELOPMENT.md)。Forge 及五个示例用一个 MSBuild 会话完成，核心只构建一次；全部内容工程引用明确 DLL，不再隐式重建 Forge。最终 Forge 检查和新增机器目录检查通过；机械飞升 419 条、模块矩阵 89 条、物流 L0/资源/电力共 85 条、合成扩展 495 条领域检查通过。核心、全部示例、四个调用工程 Release 编译通过，三个机核 Obfuscar 发布包通过入口、混淆排除和重命名检查；领域检查仍有原有 .NET 6 生命周期提示。

最终公共类型、成员及默认参数的 641 项签名与重构前安装版一致，且三个候选包的 Forge SHA-256 均为 `60444EA3157AB1D96A18C771ABE8CCEB95B8CEE3C5D3A614FD96D57F996F0279`。候选包在 `D:\workzone\probably-stolen\_build\releases\forge-refactor-20260930`，清单为该目录的 `manifest.json`；验证日志在 `D:\workzone\output\forge-refactor-validation.log`。安装状态见本节上方记录；图集发布、钩子安装、加工和保存重载仍须新进程及可丢弃档验证。

## 2026-09-30 合成扩展规则调整候选

机器配方支持每夜多次独立加工：内容 Mod 提供次数，Forge 对每次加工重新检查并扣除材料、电量，产物逐件优先找空位；原版熔炉的扩展玻璃配方使用同一放置路径。`ForgeTrashCleanup.Install` 已按用户澄清停用，垃圾桶交由原版清洁服务处理。开发日志默认 `INFO`。当前 Forge 与合成扩展分别通过 Release 构建和 493 条领域断言，混淆包已安装到本地 Demo `Mods` 并核对哈希：Forge `48318CC6D50AF88E55F690CEB6D1523A4729382C2CB34400A42312CA4C437B88`，合成扩展 `4CEB79CE0D34679A65A639F05F258615EFBFF8D1419188B3A6113FF61005E4E3`。旧版 DLL 备份于 `D:\workzone\probably-stolen\_build\backups\synthesis-performance-20260930-0930`。09:35 新进程日志确认加载两份 DLL，17 条配方加载且旧啤酒瓶配置从 2→2 迁移为 1→1，原文件备份已读回；启动日志未见 `ERROR` 或 `Exception`。过夜扣料、纯度与占格尚无本轮运行证据。

## 2026-09-30 Forge 0.3.3 每日垃圾回收候选

机器配方新增辅助物品接纳与产物预处理回调，并保留旧版构造函数以兼容伪人 Mod。固体、液体产物逐件查找空位，再使用原版叠放路径；原版熔炉玻璃回收时可保留基础与高级助溶剂。垃圾桶每日回收单独挂在 `EndNight`，只清理仍留在垃圾桶内的 Forge 注册物品，不调用会重置 `garbageLevel` 的付费清洁路径。开发打包默认 `INFO` 日志。领域检查与 P0 Release 全构建通过；当前 Forge DLL SHA-256 为 `20549A8622ECBE4DB0FBD60FD510CCA9AC902001DE8B3B56D5C66852A67315F1`、合成扩展 DLL SHA-256 为 `4A402C0B4DB608C60C1358A983D59842E24E527C70EE532E579E4CBD3DDCC3C7`，均与安装文件一致；旧 DLL 已备份于 `D:\workzone\probably-stolen\_build\backups\synthesis-0517-trash-info-20260930`。04:55 新进程确认加载 Forge 0.3.3、合成扩展 0.5.17 和 17 条配置配方，未见 `ERROR` 或构造函数异常。现有 `[NicokoboForge] LogLevel = "WARN"` 会过滤 Forge 的 `INFO` 安装日志，补丁与过夜回收结果仍待验证。

## 2026-09-30 熔炉与材料精炼机跨夜未产出修复候选

存档槽 19 在新建档首夜前摆放了两台原版熔炉和 K01 材料精炼机。夜间探针确认 `EndNight`、`BeginDay`、`SaveGame` 依次执行，次日存档仍保留玻璃刀×2、助溶剂与 K01 的石英×2，三台机器的电池均有余量。Forge 旧版只在 `DecodeSaveItem` 回调缓存主、后备背包，新档未读档即过夜时可能漏扫后备背包；原版熔炉的玻璃投入区有 `advanced_flux_agent` 时，旧版还会将其判为混合原版材料并跳过玻璃回收。当前源码已改为跨夜时扫描 `saveBags` 内的实时主、后备存档袋，并允许助溶剂与玻璃原料共处投入区但不消耗助溶剂，同时阻止该组合进入原版矿石循环。

Forge 领域检查、P0 Release 全构建与合成扩展 466 条领域断言及混淆包构建通过。候选 Forge DLL SHA-256 为 `F442294E3DC7ABFA2FB976D9B9CDF5F07D122E605EBA55C3F88D07F96650DEFD`，已随本地合成扩展包生成。游戏退出后已将该 DLL 安装到 Demo `Mods`，源文件与目标文件 SHA-256 相同；旧 DLL 备份位于 `D:\workzone\probably-stolen\_build\backups\machine-night-20260930\Nicokobo.Forge.dll`。

03:54 新进程加载 Forge 0.3.1 与合成扩展 0.5.15，机器补丁 owner 和内容目录 26/26、工厂 26/26 均可见。槽 19 从第 2 天过夜到第 3 天，日志确认 `EndNight`、`BeginDay`、`SaveGame`、`WriteSlotFile`。保存文件读回：熔炉实例 222 的玻璃刀×2 消失，输出区新增石英×1，助溶剂仍在投入区，电池 250→242；K01 实例 176 的石英×2 消失，输出区新增硅×1，电池 250→238。该证据确认本轮原生库存与保存文件结果；尚未做退出重进后的读档验收。日志中另有既存 K04 槽位拖动 `TargetException`，由诊断补丁拒绝该候选槽位，未归入本轮加工结论。

## 2026-09-30 0.3.1 已安装候选

新增 `ForgeMachineApi.RegisterAdditionalItemRecipes`，允许伪人 Mod 向合成扩展 K05 机械制造机追加自有配方。Forge、合成扩展和伪人 Mod 的 Release 构建及领域检查通过；合成扩展混淆包附带 Forge 0.3.1。三份 DLL 已在游戏关闭时安装到 Demo Build `25382790`，哈希与构建产物一致，旧版本已备份；详见 [安装记录](probes/2026-09-30-mechanical-manufacturer-install.md)。尚未启动新进程，机器加工、义眼自动鉴定及下方库存拖动故障均无新的实机结论。

## 2026-09-29 10:07 库存拖动复现与诊断版

10:01 新进程已加载上一轮修复候选 DLL（Forge SHA-256 `727EB0380B72BA1F949B87C155996283FE69BC7406919242E1BB2E945DC676D5`，合成扩展 `1912C1522D6203D69ED0B969FBEC5E621383526ACB47A45EC5B10CAA08206012`），`factoryCreated=18/18`，但拖动时仍从 `GameSlotInventory.MayHaveValidInventorySlot` 内的 `InvokeAllReduce` 抛出同一 `TargetException`。因此上一轮对 K04 委托的修改并未解决报告的问题，具体槽位仍未知。

新诊断版在已知构建上记录每个被检查槽位的 ID、上层物品与委托元数据；仅对该 `TargetException` 令当前槽位判定失败，避免单个坏槽位中断整次拖动。领域检查、Forge 核心及 P0 示例、合成扩展 Release/混淆构建均通过。已安装 Forge SHA-256 `7F1126CB6522D7B2319A615A85DB3A2D98CCA48E0ADF355D9701EFC1E5B643DB` 与合成扩展 `865D6790C1134FD1F58E32E48855B71FE59EA6CAB45EC7998D5E52921942EDD9`；上一版备份在 `D:\workzone\probably-stolen\_build\backups\inventory-drag-probe-20260929-1007`。尚无新进程或拖动结果，需从下次日志确定根因，再决定是否保留诊断补丁。

## 2026-09-29 物品拖动故障修复候选

09:35 新进程已加载 Forge 0.3.0 与合成扩展 0.5.13，内容目录和工厂均达到 18/18。拖动物品时，游戏从 `GameSlotInventory.MayHaveValidInventorySlot` 调用槽位委托，连续抛出 `TargetException: Object does not match target type`，导致拖动中断。Forge 的 K04 液体机器会向水槽与投入槽安装托管转原生委托，是当前代码中与此堆栈直接相关的候选路径；日志没有给出具体槽位，因此尚不能把异常唯一归因于 K04。

本地候选修复改为由长期持有的实例承载这两个委托，并在发布到槽位前执行原生委托自检；安装任一步骤失败时恢复原槽位委托。Forge 领域检查、核心与 P0 示例构建通过，合成扩展 Release/混淆打包通过。配套 DLL 已在游戏关闭时安装，安装 SHA-256 为 Forge `727EB0380B72BA1F949B87C155996283FE69BC7406919242E1BB2E945DC676D5`、合成扩展 `1912C1522D6203D69ED0B969FBEC5E621383526ACB47A45EC5B10CAA08206012`；旧 DLL 备份在 `D:\workzone\probably-stolen\_build\backups\inventory-drag-20260929-0945`。仍需新进程检查内容工厂、普通背包和机器槽位拖动，并用可丢弃存档验证机器加工及保存重载。

## 2026-09-29 机器 API 当前状态

Forge 0.3.0 的 `ForgeMachineApi` 注册熔炉模板机器与原版熔炉追加配方，处理投料和每夜单批结算。固体配方使用 `NightlyItems`；液体配方使用 `NightlyLiquid`，从独立水源槽按原生水质和组成扣水，在机器实例标签中记录跨夜进度。合成扩展 0.5.13 的 K01/K02/K03 使用固体模式，K04 使用液体模式；K02 只有机电工作台。接口见 [MACHINE_API.md](MACHINE_API.md)。

静态反汇编确认 `WaterHelper.GetCurrentCapacityML` 的换算为 `1000 part = 1 ml`。本地 `Build-P0.ps1` 的领域检查及核心、三个示例 Release 构建通过，合成扩展 376 条领域断言及 Release/Obfuscar 构建通过。配套包位于 `D:\workzone\probably-stolen\_build\releases\synthesis-forge-0.5.13.zip`；Forge DLL SHA-256 为 `AA6A6C6CA5DADFD73EDE1F5DDAEF7FC553A59307F1D6630B2CE5AA579A286F03`，Mod DLL 为 `979271C07F1A4060FF99630408245F24962BADC32A5F084DA1787D66FE9725D7`。该段所述安装状态是打包时的历史记录；当前安装状态以上方修复候选记录为准。投料、产物、耗水、标签存档与读档后重绑均待可丢弃存档验证。

## 历史验证记录

以下记录涉及 2026-09-27 至 09-29 的较早 Forge 构建及探针；其中的安装与日志证据不代表上方 0.3.0 本地包已在游戏中运行。详情见 [P0 开发记录](P0_PROGRESS.md)和 [9 月 26 日运行记录](AUG_MECHANICAL_ASCENSION_MIGRATION_AUDIT.md)。

## 2026-09-29 库存搬运候选签名诊断

当前本地 `GameAssembly.dll` 与生成的 `Assembly-CSharp.dll` 哈希仍分别等于 Build `25382790` 的已知值。`BuildProbe` 现在逐项诊断 `SlotMarker.TryAcceptOnce(int):int`、`GameGridInventory.Expel(GameItem):bool`、`GameGridInventory.UncheckedAccept(GameItem):bool`、`PlayerStore.SaveGame():void` 和 `PlayerStore.LoadGame():void` 的声明类型、参数及返回类型，并记录补丁 owner；汇总字段为 `transferCandidateSignaturesMatch`。该字段仅用于发现接口变化，**不参与** `ForgeCapabilities.Current.InventoryTransfer`，后者仍为 `false`。`Build-P0.ps1` 的领域检查与核心、两个 P0 示例、物流示例 Release 编译通过；核心编译零警告、零错误。当前核心已随[工坊星图安装](probes/2026-09-29-workshop-star-install.md)替换到游戏，新诊断尚无新进程日志。

静态 `SlotMarker.TryAcceptOnce` 路径会按 `MaxNumRemove` 截断数量，且可调用 `GameItem.GetSplitItem` 或 `SlotMarker.StackItemUnchecked`；目标接纳和原位恢复是否对完整单件保持身份及全部状态，不能由签名证明。仍需在可丢弃存档中分别观察真实源/目标库存的预检、成功接纳、拒收后双方不变、异常后的恢复、保存文件回读及重新载入。任一环节未闭合前不开放通用写入 API。2026-09-28 09:41 的物流 L0 日志只确认该 Mod 的原生定义 `Applied` 与工厂闭包；其中 `inventoryTransfer=False`、`persistence=not-tested`，不构成库存事务证据。

## 能力与证据

| 能力 | 当前实现 | 已取得的证据 | 待验证 |
| --- | --- | --- | --- |
| 声明、原生物品与节点注册 | 按 owner 和 ID 管理声明；普通物品、设施、模块与节点按各自目录及构建签名门控 | 领域检查与 Release 构建通过；10:15 当前核心哈希已加载；9 月 26 日较早安装版的神经接口节点及鉴定义眼日志达到 `Applied` | 当前 DLL 的目录应用、重建和迟到注册；手写单迁移版的实际应用与投放 |
| 原生效果注册 | 效果目录与随机池资格由独立能力门控 | 领域检查、核心与效果探针编译通过；10:15 当前效果探针日志达到 `Applied`，`callbackCount=0` | 随机池隔离与实际效果回调 |
| Nico 工坊 | 多 Mod 解锁链、放射状星图、依赖连线、信用点/物品条件与奖励描述；内容侧解锁回调 | 2026-09-29 核心、工坊探针和机械飞升 Mod 编译并安装，三项源/目标哈希相同，见[安装记录](probes/2026-09-29-workshop-star-install.md) | 新进程加载、星图布局、多链切换、条件门控、回调次数、具体资源交易和奖励存档 |
| 开局身份与周目数据 | 开局 ID 冲突管理；按 owner 的 `modData` 读取与内存暂存 | 旧安装版的 55 号正常开局、保存/读档路径已有记录；`RunData` 有签名门控日志 | 当前 DLL 的开局路径、失败清理；`Stage` 后原生保存与目标槽回读 |
| 库存与物流 | 直接子项只读快照、完整单件搬运预检；物流示例只实现规则与配置 | 领域检查与编译通过 | 游戏内快照和预检；实际搬运、网络货物存储与事务仍未开放 |

`ForgeCapabilities.Current` 为安装门控快照，不证明某个内容项已 `Applied`。注册返回 `Accepted` 也只表示声明被接收。历史游戏日志、当前源码、当前构建和下一次游戏会话分别对应不同产物与证据级别。

## 2026-09-27 审查修复

- 新增[工坊星图设计与调用约定](WORKSHOP_GRAPH.md)：链注册、可配坐标、前置连线、可选消耗的信用点/物品条件和信用点/物品奖励描述；机械飞升内容链同步提供依赖和消耗条件。Forge 只展示和分派，资源交易仍由内容提供者实现。本轮新核心、工坊探针与机械飞升内容 Mod 的 Release 编译通过，尚未重新安装或进入游戏验证。

- 工坊切换到首个可用标签页时复用同轮已校验的 `Snapshot()`，避免再次调用内容提供方得到空值或无效数据。

本次运行 `Build-P0.ps1`：领域检查通过，核心、两个 P0 示例和物流示例 Release 编译均为零错误；领域检查仅出现 .NET 6 生命周期的 SDK 提示。另行构建工坊与效果探针，均为零警告、零错误。`git diff --check` 通过。

本轮已重新打包并将核心、两个 P0 示例、工坊与效果探针安装到游戏 `Mods`；五项安装哈希和旧版备份见[安装记录](probes/2026-09-27-forge-install.md)。10:15 的[新进程记录](probes/2026-09-27-1015-forge-runtime.md)确认当前核心和两枚探针的哈希加载、效果探针 `Applied` 及工坊标签 `Accepted`。同一会话在 NEI 面板打开后持续出现原生输入射线检测空引用错误，归属未确定；工坊交互及存档重载仍未验收。

已移除 Wilds Network 升级能力：删除 `ForgeNetworkUpgradeApi`、`NetworkUpgradeCatalog`、`NetworkUpgradeSaveReadback`，以及 `Plugin`、`BuildProbe`、`ForgeCapabilities`、领域检查和文档中的对应引用。移除前核对过工作区内全部内容 Mod，没有任何 Mod 调用该 API。

已按 `mods-melonloader/dist` 的玩家说明格式准备双语 `README.md`，并用 `scripts/Pack-ModSite.ps1` 生成 `dist/nicokobo-forge/` 与 `dist/Nicokobo.Forge-0.1.0.zip`。网站包只含一份核心 DLL 和 `README.md`，不含 P0 诊断示例或探针；`CHANGELOG.md` 位于 `dist/nicokobo-forge/` 独立维护、不参与打包。包内核心 SHA-256 为 `CBF0E264846B546B9EDE626566820B3179A0F8808F85C742626D08C5767190D1`。这是本地发布素材，尚未上传 Mod 网站。

另已生成 1672×941 的 `cover.png`，并同步到 `dist/nicokobo-forge/cover.png` 供网站单独上传；下载 ZIP 不含封面。

## 下一次验证门槛

1. 在可丢弃存档的新进程中继续核对目标 Build、能力门控和补丁安装；逐项确认内容声明与 `Applied` 状态，并排查[10:15 会话](probes/2026-09-27-1015-forge-runtime.md)的重复输入异常。
2. 对工坊用两个内容标签页验证快照变化、失效回退、重复打开与输入拦截；解锁回调的支付和存档仍由内容 Mod 自行验收。
3. 单独推进效果注册、`RunData` 保存回读与库存快照的游戏内调用。完整物品搬运和网络库存保持未开放，直至接纳、失败恢复及重载一致性形成闭环。
