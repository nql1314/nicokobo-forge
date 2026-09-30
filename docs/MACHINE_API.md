# Forge 机器模板 API（0.5.0）

面向 Probably Stolen Demo Steam Build `25382790`。本版替换先前的 `NightlyItems` / `NightlyLiquid` API；已接入的内容 Mod 必须重新编译，不提供二进制兼容或旧存档迁移。

Forge 负责原版机器 UI 模板、设施物品注册、槽位回调、过夜发现、产物落点、液体组分、电池扣减和失败恢复。内容 Mod 提供物品外观、稳定 ID、配方、条件与数值；`ConfigureItem` 不应替换窗口或添加原生生产回调。数值和条件回调只能读取状态，`PrepareItem` 只能修改它收到的新产物。

## 注册机器

```csharp
var template = new ForgeMachineTemplate(
    ItemInput: new(6, 4),
    Output: new(ForgeMachineOutputKind.Items, new(6, 4)))
{
    LiquidInputs = [new("water", new(3, 3), "供水容器 / Water input")],
    Battery = new(new(2, 2), Required: true),
    Modules = new(new(4, 4), ["MODULE_TYPE_FURNACE", "MODULE_TYPE_UNIVERSAL"]),
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

工厂由 Forge 统一创建，不再要求内容方克隆熔炉后自行维护窗口子库存。UI 保留原版电池、模组仓、投料、产出和手册的布局位置与组件；液体槽按声明扩展。电池和模组仓继续使用 `MachineHelper.SetupBatterySlot` / `SetupModuleBay`；原版模组装入、移出、加成和作业通知仍走游戏接口。仓库尺寸按格声明，范围为 1–32；液体输入槽最多 8 个。

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

每台机器只有一种输出模板，每条配方必须与之匹配。物品仓与液体槽可以并用；容器只因其液体内容被扣减，不作为耗材移除。槽位条件允许限制容器类型，配方条件则校验本批需要的水质等状态。一个液体槽在一条配方中只能出现一次；多个槽可以同时参加结算。

`ForgeMachineIngredient.Count` 按件数取料，支持跨叠、重复材料规则与额外未参与材料保留。`WholeStack = true`（`Count` 必须为 1）选择整叠，按单件价值从大到小、同价值按数量从大到小选择。实际扣除数量见 `context.Items[i].Count`，不要用配方的 `Count` 推导整叠数量。

```csharp
// 纯液体输入；取 source 容器中的指定组分，灌入现有输出容器。
var template = new ForgeMachineTemplate(null,
    new(ForgeMachineOutputKind.Container, new(3, 3)))
{
    LiquidInputs = [new("source", new(3, 3))],
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

`ForgeLiquidApi.Capture` 返回组分和容量；`WaterQuality` 返回 0（未知／不满足基础水）、1（基础）、2（高品质）、3（纯净）。容器输入和输出均要求单件容器，拒绝合并容器叠。

## 电池与过夜事务

`ForgeMachinePowerRule` 是机器级规则，所有配方共用。`Cost` 默认按批扣一次；`PerOutput = true` 则乘本批物品产量。`ResolveCost` 先收到已选材料、液体容器和已解析的 `OutputCount`，适合读取机器当前电耗以保留效率模组效果。负值和溢出拒绝；零耗电不调用原生扣电。`ForgeMachinePowerMath.CalculateCost` 可在内容方的预览中复用同一计算。

机器在 `PlayerStore.EndNight` 的 Prefix 中结算，每台每夜最多尝试一批。同一会话内按存档槽、周目、日期和机器实例去重；失败也不会在同一夜重复尝试。使用 `PlayerStore.FindAllItem()` 发现机器，保留实时存档袋与网格的有界补充扫描，无逐帧轮询。

先解析和预检本批材料、液体、容量与电量，再创建及预处理产物、落到输出仓／灌入输出容器，随后扣液体、电量和物品。每次写入都有读回，提交前再次核对所有资源；被消费的整件只在提交后销毁。原生调用若写入后失败，仍会撤回该步骤；撤回失败不会阻止其他恢复步骤，并为机器记录故障标签、停用后续加工。原版保存仍由游戏的过夜流程执行。

本版不再保留旧液体生命周期进度、旧模式或版本迁移。K04 的当前规则是一批产生一个灌装罐，水不足或容器装不下整批时保持材料等待下一夜。

## 追加配方与原版机器

```csharp
// 在 OnLateInitializeMelon，一次接入已由其他 Mod 注册的自定义机器。
ForgeMachineRegistrationApi.RegisterAdditionalRecipes(contributorOwnerId, targetMachineId, recipes);

// 原版机器目前仅支持 furnace；它的原生批次优先。
ForgeMachineRegistrationApi.RegisterExistingMachine(ownerId, "furnace", glassRecipes,
    new ForgeMachinePowerRule(0, context => ForgePowerApi.GetMachineCost(context.Machine)),
    (machine, input) => NativeBatchAvailable(machine, input));
```

追加配方必须使用追加方的 owner 前缀，并符合目标机器的输入槽和输出模板。整个批次校验后发布新配置，既有事务持有原配置。追加方不创建第二台机器。

自定义机器复用公共过夜和读档事件，投料在本机槽位上绑定回调，不 Hook 原版窗口创建或存档解码。只有注册原版熔炉追加配方时，才额外安装它的投料和生产两个 Hook；这些钩子只处理已认领的原版熔炉。探针为 true 或抛错时整夜交还原版；为 false 且有注册材料时由 Forge 加工。没有探针则只接管纯注册材料及原版助溶剂的投入。原版模组装入、移出回调不被拦截。

`ForgeMachineRuntimeApi.TryGetInventory` 返回本机的实时槽位；`ForgeMachineRegistrationApi.Snapshot()` 的 `RuntimeInstalled` 分别反映模板和可选原版扩展门控。`ForgeCapabilities.Current.MachineNightProcessing` 表示模板生命周期补丁安装，不表示某台机器成功生产或保存。

可编译接入示例见 [MachineTemplates](../samples/Nicokobo.Forge.MachineTemplates/MachineExamples.cs)。领域检查、编译和打包不代替原版 UI 的游戏内操作、实际过夜、电量／液体读回和新存档保存重载验收。
