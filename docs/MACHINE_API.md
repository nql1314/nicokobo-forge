# Forge 机器模板 API

同一机器的批次在规划、写入和回滚期间禁止重入，嵌套调用返回 `machine-busy`；独立机器仍可加工。规则、工厂与 `PrepareItem` 回调应保持本机资源不变，事务结束后才能再次加工。

面向 Probably Stolen Demo Steam Build `25382790`。以下契约对应当前源码；版本与验证范围见[当前进度](FORGE_PROGRESS.md)，内容 Mod 需针对配套 DLL 编译。通用默认值、单位与上限集中在 [BuildConfig/ForgeNumbers.cs](../BuildConfig/ForgeNumbers.cs)，本文数值为当前编译默认值。

Forge 负责原版机器 UI 模板、设施物品注册、槽位回调、过夜发现、产物落点、液体组分、电池扣减和失败恢复。内容 Mod 提供物品外观、稳定 ID、配方、条件与数值；`ConfigureItem` 不应替换窗口或添加原生生产回调。数值和条件回调只能读取状态，`PrepareItem` 只能修改它收到的新产物。

`ForgeMachineDefinition.ProductionMarkupPercent` 是内容 Mod 为机器声明的生产倍率，合法范围为 0–1000%，省略时为 0%。Forge 将它复制到每次批处理的 `ForgeMachineBatchContext.ProductionMarkupPercent`，机器拥有者和追加配方都能读取同一值。Forge 只传递倍率，不定义或覆盖内容 Mod 的价格表。

`ForgeMachineItemOutput.ResolveItemId`（0.6.28）可按本批上下文返回一个原生目录中的物品 ID，用于随机或条件产物。事务在资源规划后、创建产物和扣除资源前只调用一次；整批使用同一 ID，工厂产物及 `PrepareItem` 后的身份按它校验。返回空白／带首尾空白的 ID、工厂失败或输出仓放不下时，走现有恢复流程。该回调不能修改游戏资源；声明的 `ItemId` 仍提供默认手册图示，动态产物名称和范围由 `.Guide` 说明。手册读取不调用随机回调，修改选择规则时需同步 `RuleRevision`。

## 注册机器

### 手册动态配方目录

`ForgeMachineRegistrationApi.GuideSnapshot()` 从同一个机器注册目录自动生成只读手册数据，包含每条已接受配方的 owner、机器名、直接原料、单批数量、输出和双语展示信息。新建机器和 `RegisterAdditionalRecipes` 追加配方都自动加入；无需另注册一份手册配方，也无需手册引用提供者的程序集。返回值的 `Revision` 在成功注册机器或追加配方时增加，阅读界面可据此在重新打开时更新缓存。该快照不调用物品工厂、条件或动态数量委托；接受声明不代表已应用到原生目录。

`ForgeMachineDefinition.Guide` 声明默认分类和备注，默认分类为 `synthesis`。配方省略 `ForgeMachineRecipe.Guide` 时沿用机器分类；显式设置则覆盖分类，备注为空时沿用机器备注。分类 ID 是开放字符串；目前合成扩展的三个配方手册分别读取 `synthesis`、`refining` 和 `food_processing`，其他阅读界面可增加自己的分类。

```csharp
var recipe = new ForgeMachineRecipe(ownerId + ".recipe.example",
    [new ForgeMachineIngredient("metal_ingot", 2, IsAllowedIngot)
        { Guide = new(Requirement: new("高纯度及以上", "High purity or better")) }],
    new ForgeMachineItemOutput(ownerId + ".product", Count: 1))
{
    Guide = new("synthesis"),
    RuleRevision = "ingot-minimum-high:v1"
};
// IsAllowedIngot 由内容 Mod 提供，只读取候选材料。
ForgeMachineRegistrationApi.RegisterAdditionalRecipes(ownerId, targetMachineId, [recipe]);
```

物品原料、液体原料、物品输出和容器输出均可通过 `.Guide` 的 `ForgeRecipeGuideDisplay` 提供双语 `Name / Requirement / Quantity` 与 `IllustrationItemId`。件数和毫升数直接来自真实配方；类别原料可另指定代表性图示，图示不会限制实际材料。条件委托不能自动转成文字，内容方应提供实际要求；省略条件说明时显示“需满足配方条件”。动态产量或体积省略数量说明时显示“依配方”，整叠输入显示“整叠”。这些展示字段不改变实际准入、扣料或生产规则。

### Forge G 键合成表（0.6.29）

G 键入口与阅读界面由 Forge 提供；内容 Mod 不再各自监听 G。`ForgeRecipeGuideApi.Snapshot()` 返回已接受机器配方与原版机器扩展展示声明的合并目录；`IsVisible / Close()` 供实体手册避开共享窗口。物品会查询作为产物和材料的相关配方，机器还会显示自己的加工配方；匹配读取真实固体 ID、材料类别或液体图示，不执行配方条件和动态产出回调。

通过 `ForgeMachineRegistrationApi` 注册的新机器与追加配方自动加入，无需重复声明。只有在 Forge 机器事务之外加工的配方（例如内容 Mod 为原版熔炉增加的玻璃回收），才调用 `ForgeRecipeGuideApi.RegisterRecipes(ownerId, entries)` 提交 `ForgeRecipeGuideEntry` 列表。该操作按 owner 整批替换其展示声明，验证身份、配方 ID、数量及重复 ID，冻结原料列表，并增加目录版本；拒绝时原目录不变。提供者应提交自己的真实配方，图示 ID 对应材料和产物；这条接口不创建机器、不执行加工，也不证明生产逻辑已应用到游戏。接受机器声明与目录 Applied 的边界保持不变。

### 自动化读取与批次边界（0.6.20）

内容 Mod 仍以 `RegisterMachine / RegisterAdditionalRecipes` 提供真实配方。物流等调用方使用 `ForgeMachineAutomationApi.Recipes(machineId)` 或 `RegisteredRecipes(machineId)` 读取冻结声明，后者附带提供者 owner；不依赖提供者程序集。条件委托只读，不能借目录读取执行生产。

`ForgeMachineRecipe.RuleRevision`（0.6.26）由配方提供者声明规则版本。条件或动态数量回调捕获配置时，必须包含配置的实际阈值、产量规则等语义；更改这些配置也要更改该值。委托所在程序集的版本不能表达闭包中的数值。读取方将它与声明中的原料、液体、产物和辅助物品一起校验，过期的保存配方需重新登记；纯展示说明修改无需改变规则版本。

`Subscribe(owner, callbackId, BeforeBatch, callback)` 在每次原有批次机会、事务规划之前执行；上下文含机器与真实槽位。内容方可独立搬运／供电，再 `SelectRecipe(recipeId)` 限定本次机会。多个调用方选择冲突或回调抛出异常时本次批次停止。无选择时保持原生声明顺序；接口不新增批次，不改变其他机器或全局配方排序。`AfterBatch` 在执行尝试返回结果后分派，调用方检查 `Result`，不能将失败当作已生产。

单次正常搬运使用 `ForgeItemTransferApi.Move`；倒液使用 `ForgeLiquidTransferApi.Pour`。二者返回实际数量和 `Indeterminate`，不跨搬运与加工执行退料。拒绝条件、真实锁定及写后读回与本机加工事务分开。`Pour` 依赖已安装的机器／液体价值适配；内容方负责液体与混合品质准入。candidate003 已覆盖本地固体链、附着后异常守恒、多液源逐步重查及受测 24 台机器的跨 PID 完整存档快照，范围见[本次进度](FORGE_PROGRESS.md#2026-10-06-联合-review-候选)；其余转移分支、完整夜结和网络不据此计为通过。既有 `InventoryTransfer` 总能力仍为 false。

物品搬运在目标拒收或附着前抛错时，只恢复该次操作：整叠物品恢复原位置并重新检查空位，拆分物品合回原叠；不会重建替代物品或撤销此前成功的搬运。数量读回确认已经送达时，即使回调抛错也按已完成返回。无法确认守恒或恢复结果时仍返回 `Indeterminate`，调用方须暂停相应搬运。

倒液原生回调抛错后仍读取两端；各组分均须单向从来源减少并等量进入目标，同时核对每组分按比例转移的 `Value / QualityBasis`。读回确认已提交时返回实际 `PartsMoved` 与 `poured-readback`，明确拒绝且两端未变时返回 `rejected`；不匹配或无法读取时返回 `Indeterminate`，调用方停止后续搬运。

### Forge 内置全域制造终端

Forge 0.6.19 自行注册 `ForgeManufacturingTerminal.ItemId`，owner 为 `ForgeManufacturingTerminal.OwnerId`。物品、图标、售价、基础电耗、槽位与供货归 Forge，内容 Mod 只调用 `RegisterAdditionalRecipes(ownerId, ForgeManufacturingTerminal.ItemId, recipes)` 追加自己的配方。无内容 Mod 时也有机器，配方为空，不加工。默认 3×3 外部占格、9×6 输入／输出、7×5 原生环形模组舱，基础价值 400、电耗 25，每晚一批。

`ConfigureManufacturingTerminalMarkup(ownerId, percent)` 在追加配方前设置这台共享机器的生产增值率，默认 15%，范围 0–1000%。首个设置者认领该配置，其他提供者修改返回 `Conflict`；所有追加配方读取同一批次倍率。合成扩展用此入口传入已加载配方文件中的 K05 自定义倍率。

Forge 在原生 `SaveManager.DecodeNodes` 创建物品前，将两个已知旧 ID `nicokobo.mechcore.synthesis.universal_manufacturing_terminal` 和 `nicokobo.mechcore.synthesis.mechanical_manufacturer` 映射到当前 ID，并更新图集路径及机器分类。原 UUID、状态、价值、占格、子物品及槽位索引继续由原生解码器恢复；恢复后重设当前基础电耗和文本、重新绑定槽位。新 ID 由游戏下次正常保存写入，Forge 不直接重写 ES3 文件。这些离线检查不代表实际旧档重载已验收。

### 内容 Mod 自定义机器

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

机器声明允许空配方列表，供共享机器先注册、内容 Mod 后续追加；`RegisterAdditionalRecipes` 仍要求至少一条配方。

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

拖到机器物品上的自动投放先询问原生电池与模组槽，再把已注册配方原料投向物品输入；水瓶等其余容器依次询问储水输入、物品输入、输出容器与手册槽。每个槽仍使用原生接纳、形状和堆叠判断；物品输出仓保留原版禁止玩家插入的规则。框架只为已注册机器接入一个 `GameItem.TryFindOneValidInventorySlot` Hook，避免原生网格从右往左搜索时先选中输出容器。当前合成扩展的五台机器与 Forge K05 均使用 `9×6` 物品输入／输出仓；K04 另有液体输入槽。

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

机器在 `PlayerStore.EndNight` 的 Prefix 中结算，默认每台每夜最多尝试一批。Forge 0.6.18 新增 `ForgeMachineDefinition.ResolveNightlyBatchCount(GameItem)` 只读委托，由机器拥有者声明正整数批次上限，省略时为一批；追加配方沿用机器拥有者的上限。夜晚开始先确定本夜上限，额外批次前重读委托；上限下降会提前停止，上升留到下一夜生效。

每批重新读取实时槽位、选择配方并核对材料条件、液体、电量、输出容量及物主，独立提交资源事务；仅当前批次成功时继续下一批。条件不足或事务失败即结束本机当夜加工，不重试，已完成的批次保留，当前失败批次仍按原有事务恢复及隔离规则处理。同一会话内按存档槽、周目、日期和机器实例对整个夜间周期去重。显式 `ForgeMachineBatchProcessor.Process` 仍只尝试一批，不读取夜间上限。使用 `PlayerStore.FindAllItem()` 发现机器，保留实时存档袋与网格的有界补充扫描，无逐帧轮询。

先解析和预检本批材料、液体、容量与电量，再创建及预处理产物、落到输出仓／灌入输出容器，随后扣液体、电量和物品。每次写入都有读回，提交前再次核对所有资源；被消费的整件只在提交后销毁。原生调用若写入后失败，仍会撤回该步骤；撤回失败不会阻止其他恢复步骤，并为机器记录故障标签、停用后续加工。原版保存仍由游戏的过夜流程执行。

模板中声明液体输入槽不会使所有配方都需要容器。配方的 `LiquidInputs` 为空时，该槽是可选输入，物品配方可以在槽为空时加工，也不会扣其中的液体。

容器输出模板向玩家放入的现有容器灌液；必需输入或输出容器缺失、液体不足或输出剩余容量装不下整批时，保持材料等待下一夜。当前合成扩展 K04 使用物品输出，不能作为这一模板的实机验收样本。

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

Forge 0.6.15 增加纯计算 `ForgeProductionValueMath.PerOutputWithOverhead(inputValue, markupPercent, batchOverhead, outputCount)`：材料先乘倍率，再加一次整批附加成本，最后按产量分摊并向上取整一次。附加成本必须非负；数量与溢出边界沿用机器事务。既有 `PerOutput` 和 `ForgeProductionValueApi.Calculate` 的签名及无附加成本行为保留。Forge 不指定电价；机械飞升与合成扩展仍自行读取基础电耗并按自己的定价规则传入，保留容器也由内容方决定是否计价。
