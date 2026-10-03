# Forge 机器模板 API

面向 Probably Stolen Demo Steam Build `25382790`。以下契约对应当前源码；版本与验证范围见[当前进度](FORGE_PROGRESS.md)，内容 Mod 需针对配套 DLL 编译。通用默认值、单位与上限集中在 [BuildConfig/ForgeNumbers.cs](../BuildConfig/ForgeNumbers.cs)，本文数值为当前编译默认值。

Forge 负责原版机器 UI 模板、设施物品注册、槽位回调、过夜发现、产物落点、液体组分、电池扣减和失败恢复。内容 Mod 提供物品外观、稳定 ID、配方、条件与数值；`ConfigureItem` 不应替换窗口或添加原生生产回调。数值和条件回调只能读取状态，`PrepareItem` 只能修改它收到的新产物。

`ForgeMachineDefinition.ProductionMarkupPercent` 是内容 Mod 为机器声明的生产倍率，合法范围为 0–1000%，省略时为 0%。Forge 将它复制到每次批处理的 `ForgeMachineBatchContext.ProductionMarkupPercent`，机器拥有者和追加配方都能读取同一值。Forge 只传递倍率，不定义或覆盖内容 Mod 的价格表。

## 注册机器

```csharp
var template = new ForgeMachineTemplate(
    ItemInput: new(9, 6),
    Output: new(ForgeMachineOutputKind.Items, new(6, 4)))
{
    LiquidInputs = [new("water", new(1, 1))],
    Battery = new(new(1, 1), Required: true),
    Modules = new(new(7, 5), ["MODULE_TYPE_FURNACE", "MODULE_TYPE_UNIVERSAL"]),
    ManualSlot = true
};

var recipe = new ForgeMachineRecipe(ownerId + ".recipe.example",
    [new ForgeMachineIngredient("scrap_metal", 2)],
    new ForgeMachineItemOutput("metal_ingot", Count: 1))
{
    LiquidInputs = [new("water", 100,
        Condition: container => ForgeLiquidApi.WaterQuality(container) >= 1)]
};

ForgeMachineRegistrationApi.RegisterMachine(ownerId,
    new ForgeMachineDefinition(ownerId + ".machine.example", template, [recipe],
        Power: new(0, context => MachineryHelper.GetMachinePowerUsage(context.Machine)),
        ConfigureItem: item => { item.name = "示例机器"; /* 图标、占格和物品数值 */ },
        ItemOptions: new NativeItemOptions(NightShopStockPolicy.None)));
```

工厂由 Forge 统一创建。标题在上、槽位在下，从左到右为电源、模组、物品输入、液体输入、输出、备注。电池和模组固定在原生助手读取的 `(0,1)` / `(1,1)`；液体输入按声明顺序排列，输出和备注随之右移，读档槽位发现使用同一规则。

默认模组仓直接保留熔炉的 `7×5` 环形定义（中心十字及四角空缺）；其他尺寸使用新建矩形仓，不缩放已有环形背景。电池默认 `1×1`，液体输入与容器输出设为 `1×1` 时使用净水器同款 `GameSlotInventory()` 和 `Items/water_container4` 的原生容器背景，装入后随物品尺寸扩展。容器名称默认取原生本地化键 `mech_purifier_label_container`（中文为“储水容器”）；`Label` 留空即可使用它，内容 Mod 无需自行画槽位或写供水标签。输出列保留原生“输出”标题。输入输出物品仓按声明尺寸使用原生网格组件。标题均使用原生 `TagElement`。电池和模组仓继续使用 `MachineHelper.SetupBatterySlot` / `SetupModuleBay`；原版模组装入、移出、加成和作业通知仍走游戏接口。仓库尺寸范围为 1–32；液体输入槽最多 8 个。

注册会先校验整个声明，再暂存设施工厂。模板、模组类型、配方及其嵌套列表在注册时冻结。重复机器 ID 返回 `Conflict`；不匹配模板的配方返回 `Invalid`，不会部分发布。`Accepted` 只表示声明被接受；设施目录是否 `Applied` 仍需检查 `ForgeItemApi.Snapshot()`。

## 输入与输出模板

| 配置 | 行为 |
| --- | --- |
| `ItemInput` 有尺寸，`LiquidInputs = []` | 物品输入 |
| `ItemInput = null`，声明液体槽 | 纯液体输入 |
| 同时声明物品仓和液体槽 | 同一批同时扣物品和液体 |
| `Output.Kind = Items` | 在有界输出仓创建配方物品；仓满取消本批并恢复已尝试的写入 |
| `Output.Kind = Container` | 向玩家放在输出槽中的现有容器灌装；容器原地保留，不生成额外物品 |
| `Battery = null` | 无电池槽，只允许无动态耗电、固定零耗电的机器 |
| `Battery.Required = false` | 零耗电批次允许没有电池；正耗电批次仍必须有可用电池 |
| `Modules = null` / `ManualSlot = false` | 省略对应模组仓／手册槽 |

每台机器只有一种输出模板，每条配方必须与之匹配。物品仓与液体槽可以并用；容器只因其液体内容被扣减，不作为耗材移除。槽位条件与配方条件都只在**批次结算时**校验（容器类型、水质、纯度等）；它们不参与原生槽位接纳。一个液体槽在一条配方中只能出现一次；多个槽可以同时参加结算。

### 原生接纳白名单

物品仓与液体槽的接纳由原生白名单承担：

- 物品仓用 `ContainerHelper.InitContainerItem(slot, machine, 空标签, 冻结配方 ID)`，即按本条机器的配方投入 ID 白名单接纳。追加配方会扩展该白名单。
- 液体输入槽与容器输出槽用 `ContainerHelper.AllowOnlyTaggedItem(slot, "LIQUID_CONTAINER_TAG")`，与原版净水器和湿气农场的水槽规则一致。

原因是游戏通过 `InvokeFuncExtensions.InvokeAllReduce` 读取该字段，而它逐项走 `Delegate.DynamicInvoke`。`DelegateSupport.ConvertDelegate` 产生的委托在 il2cpp 侧报 `Func`3.Invoke`、目标为 `Il2CppToMonoDelegateReference`，反射调用必然抛 `TargetException: Object does not match target type`；原生闭包没有这个问题。每次装配后 Forge 会把实际安装的委托读回写入 `[NicokoboForge/Admission]` 日志，出现 `interopBridge=True` 或缺委托即为异常。

接纳只是界面层过滤：形状、堆叠、是否真的是本批材料仍由原生槽位与本批事务校验，可接纳不等于加工需要。

拖到机器物品上的自动投放先询问原生电池与模组槽，再把已注册配方原料投向物品输入；水瓶等其余容器依次询问储水输入、物品输入、输出容器与手册槽。每个槽仍使用原生接纳、形状和堆叠判断；物品输出仓保留原版禁止玩家插入的规则。框架只为已注册机器接入一个 `GameItem.TryFindOneValidInventorySlot` Hook，避免原生网格从右往左搜索时先选中输出容器。合成扩展 K01–K05 的物品输入均为 `9×6`，物品输出仓同为 `9×6`（K04 为容器输出）。

物品白名单使用当前注册配置中的全部配方，包含其他 Mod 的追加配方。配置改变后刷新对应机器的白名单；刷新前撤下旧判断，避免新旧名单取交集或重复叠加原生回调。

生产向物品输出仓放置产物时，暂时撤下禁止玩家插入的原生判断，保留形状、占用和容量校验；放置结束或失败均恢复原判断。这个通用步骤同样适用于内容 Mod 提供的原版熔炉输出仓。

`ForgeMachineIngredient.Count` 按件数取料，支持跨叠、重复材料规则与额外未参与材料保留。`WholeStack = true`（`Count` 必须为 1）选择整叠，按单件价值从大到小、同价值按数量从大到小选择。实际扣除数量见 `context.Items[i].Count`，不要用配方的 `Count` 推导整叠数量。

```csharp
// 纯液体输入；取 source 容器中的指定组分，灌入现有输出容器。
var template = new ForgeMachineTemplate(null,
    new(ForgeMachineOutputKind.Container, new(1, 1)))
{
    LiquidInputs = [new("source", new(1, 1))],
    Modules = null, ManualSlot = false
};
var recipe = new ForgeMachineRecipe(ownerId + ".recipe.liquid", [],
    new ForgeMachineContainerOutput([new("water", 90)]))
{
    LiquidInputs = [new("source", 100, LiquidId: "water")]
};
```

液体声明单位为整数毫升，底层使用 `1000 part = 1 ml`。不指定 `LiquidId` 时按当前混合比例扣减，用最大余数分配剩余 part，保证总量精确；指定 ID 时只扣该组分。输入不足、组分不存在、动态体积非正、整数溢出或输出容量不足都取消本批。液体读取和写后校验覆盖原版液体目录的每个组分，撤回时恢复原组分，而非补成纯水。

`ResolveMillilitres(context)` 支持按本批实际材料价值计算扣水和灌装量。`ForgeMachineItemOutput.Contents` 可以灌装新生成的容器物品：内容方注册的产物工厂应返回空容器，每个产物分别获得声明体积。`ForgeMachineContainerOutput.Contents` 则灌入输出槽中的现有容器，允许在模板条件许可下混合已有液体。

`ForgeMachineContainerOutput.ResolveContents(context)` 可返回按实际输入组分生成的液体 part 列表。声明此委托时 `Contents` 必须为空；委托在 `context.Liquids` 的前后扣除快照确定后执行，只能读取状态。`ForgeLiquidCompositionMath.Consumed(before, after)` 提取实际取出的组分与比例价值，`ReplaceComponent(contents, sourceId, targetId)` 只替换指定组分、保留其它组分并合并目标组分。组分 ID、正体积、非负价值、唯一性和输出剩余容量均在资源变更前检查；混入已有液体和失败恢复仍走统一事务。内容 Mod 自己决定转换规则、显示名称、合成纯度门槛和价格加成。

`ForgeLiquidApi.Capture` 返回组分和容量；容量使用原生剩余容量加已占体积，均以 part 计。原版 `WaterHelper.GetCurrentCapacity` 返回装满百分比，不能作为液体容量。`WaterQuality` 返回 0（未知／不满足基础水）、1（基础）、2（高品质）、3（纯净）。容器输入和输出均要求单件容器，拒绝合并容器叠；所有权由批次运行时另外检查。

## 电池与过夜事务

`ForgeMachinePowerRule` 是机器级规则，所有配方共用。`Cost` 默认按批扣一次；`PerOutput = true` 则乘本批物品产量。`ResolveCost` 先收到已选材料、液体容器和已解析的 `OutputCount`，适合读取机器当前电耗以保留效率模组效果。负值和溢出拒绝；零耗电不调用原生扣电。`ForgeMachinePowerMath.CalculateCost` 可在内容方的预览中复用同一计算。

机器在 `PlayerStore.EndNight` 的 Prefix 中结算，每台每夜最多尝试一批。同一会话内按存档槽、周目、日期和机器实例去重；失败也不会在同一夜重复尝试。使用 `PlayerStore.FindAllItem()` 发现机器，保留实时存档袋与网格的有界补充扫描，无逐帧轮询。

先解析和预检本批材料、液体、容量与电量，再创建及预处理产物、落到输出仓／灌入输出容器，随后扣液体、电量和物品。每次写入都有读回，提交前再次核对所有资源；被消费的整件只在提交后销毁。原生调用若写入后失败，仍会撤回该步骤；撤回失败不会阻止其他恢复步骤，并为机器记录故障标签、停用后续加工。原版保存仍由游戏的过夜流程执行。

模板中声明液体输入槽不会使所有配方都需要容器。配方的 `LiquidInputs` 为空时，该槽是可选输入，物品配方可以在槽为空时加工，也不会扣其中的液体。

容器输出示例：合成扩展 K04 一批向玩家放入的现有容器灌液；供水或输出容器缺失、水不足或输出剩余容量装不下整批时保持材料等待下一夜。

## 追加配方与显式批次事务

```csharp
// 在 OnLateInitializeMelon，一次接入已由其他 Mod 注册的自定义机器。
ForgeMachineRegistrationApi.RegisterAdditionalRecipes(contributorOwnerId, targetMachineId, recipes);

// 内容方自行提供现有对象的槽位和调用时机；不会注册机器或安装 Hook。
var processor = ForgeMachineRuntimeApi.CreateBatchProcessor(ownerId, batchDefinition);
string status = processor.Process(machine, liveInventories);
```

追加配方必须使用追加方的 owner 前缀，并符合目标机器的输入槽和输出模板。整个批次校验后发布新配置，既有事务持有原配置。追加方不创建第二台机器。

自定义机器复用公共过夜和读档事件，投料在本机槽位上绑定回调。`CreateBatchProcessor` 只冻结模板与配方，并复用材料、液体、电量、容量、写后读回与恢复逻辑；`Process` 最多尝试一条配方，不会加入自动机器扫描。调用者提供属于该机器的实时槽位，并负责原生接入、投料规则、触发时机和同一夜去重。规则和条件回调仍须只读。

原版熔炉的玻璃回收配方、投料与生产 Hook、原版批次优先级及夜间调度由合成扩展实现。Forge 的自动扫描只包含已注册自定义机器；通用 UI 模板复用原版组件。

`ForgeMachineRuntimeApi.TryGetInventory` 仅返回已注册自定义机器的实时槽位；`ForgeMachineRegistrationApi.Snapshot()` 只包含这些机器。`RuntimeInstalled` 和 `ForgeCapabilities.Current.MachineNightProcessing` 表示模板生命周期补丁安装，不表示某台机器成功生产或保存。

可编译接入示例见 [MachineTemplates](../samples/Nicokobo.Forge.MachineTemplates/MachineExamples.cs)。领域检查、编译和打包不代替原版 UI 的游戏内操作、实际过夜、电量／液体读回和新存档保存重载验收。

## 逐批价值与原生类别

`ForgeMachineIngredient.ItemTag` 可代替 `ItemId` 声明原生类别；两者必须且只能填一种。框架把 ID 和类别交给原生接纳器，条件只在批次选择时检查。作为配方材料的电池或模组自动投到物品输入仓；给机器供电或安装模组时拖到对应独立槽位。

批次上下文 `Items` 含实际选中数量和原生内在价值，`Liquids` 含消费前后快照、所扣价值及品质前价值。框架使用游戏的内在／最终价值阶段，排除市场阶段与事件修正。`ForgeProductionValueApi.Calculate(context, markupPercent, itemValue?, liquidValue?)` 由内容方指定品质去重选择器；内容方可以传入 `context.ProductionMarkupPercent` 统一使用机器配置。Forge 不定义或覆盖内容物品的默认价格。`SetBasis`／`ReadBasis` 保存固体品质前价值，工厂默认值由内容方提供。

`ForgeMachineLiquidAmount.ResolveValue` 和 `ResolveQualityBasis` 可声明新增液体的实际价值与品质前价值。液体快照按组分保存二者；部分消费、混合、倾倒和回滚保留价值。没有声明价值的原生液体沿用游戏体积计价。实际灌装、原生转移及保存重载仍需单独验证。

`SetBasis` 写入生产基础价值后同步原生有效状态，`ReadBasis` 读取有效快照；内容方通过这两个方法读写基础价值。机器声明的倍率随每批上下文传递。液体账本与机器隔离标签同样遵循有效状态读回约定。
