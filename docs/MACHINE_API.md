# Forge 机器与配方 API

面向 Probably Stolen Demo Steam Build `25382790`。`ForgeMachineApi` 提交声明；注册目录负责校验、冲突和投料索引，原生适配器负责设施目录、窗口、循环隔离和夜间事务。内容 Mod 提供机器工厂、物品 ID、配方、材料条件和数值回调。

```csharp
ForgeMachineApi.RegisterMachine(ownerId, machineId, CreateMachine,
    new NativeItemOptions(NightShopStockPolicy.None),
    [new ForgeMachineRecipe(recipeId,
        [new ForgeMachineIngredient(inputItemId, 2)],
        outputItemId, 1, 0,
        ResolvePowerCost: MachineryHelper.GetMachinePowerUsage)]);

// Native batch probe: true while the game's own furnace cycle still has
// something to do with this input. The registered recipes then stay idle and
// the native cycle keeps the night; only an unservable slot falls back to
// them. Omit it to keep the earlier feedstock-only rule.
ForgeMachineApi.RegisterExistingMachine(ownerId, "furnace",
    [new ForgeMachineRecipe(glassRecipeId,
        [new ForgeMachineIngredient("empty_beer_bottle", 1)],
        quartzItemId, 1, 0,
        ResolvePowerCost: MachineryHelper.GetMachinePowerUsage)],
    (machine, input) => NativeBatchAvailable(machine, input));

// The target custom machine must already be registered. The second Mod owns
// only its recipe IDs and output item factories.
ForgeMachineApi.RegisterAdditionalItemRecipes(contributorOwnerId,
    machineId, [new ForgeMachineRecipe(contributorRecipeId,
        [new ForgeMachineIngredient(inputItemId, 1)], outputItemId, 1, 0,
        ResolvePowerCost: MachineryHelper.GetMachinePowerUsage)]);
```

新机器的工厂应基于原版熔炉模板保留机器窗口和子库存，并返回新 ID 的 `GameItem`。Forge 清除模板自带的原生循环回调，注册设施物品并根据声明的材料 ID 接纳投入。`RegisterExistingMachine` 当前只接纳原版 `furnace`；其他原版机器需要相应的原生投料与循环适配器。可选的第三个参数是“原版批次探针”：`true` 表示游戏自己的熔炉循环对该投入还有事可做（能炼金属，或装了垃圾模组且槽内有垃圾）。有探针时它同时决定两件事——探针为 `true` 时注册配方整夜不起作用，原生循环照常执行；探针为 `false` 且槽内确有注册原料时，Forge 跳过原生循环并自行加工注册配方。因此玻璃原料和原版矿石可以共用投料槽，各自不会吃掉对方的材料；槽内没有注册原料时原生循环一律照常执行，非玻璃玩法不受影响。不声明探针时保持旧的“仅纯玻璃投入才跳过原生循环”规则。探针抛错时按 `true` 处理，把该夜交还原生流程。探针只在原版机器上有效，注册到自定义机器返回 `Invalid`。

`RegisterAdditionalItemRecipes` 允许另一内容 Mod 给已注册的自定义 `NightlyItems` 机器追加配方，不能追加到原版熔炉或液体机器。追加方负责注册产物，并使用自己的 owner 前缀命名配方 ID；目标机器未注册时返回 `Invalid`。初始声明在 `OnInitializeMelon` 提交，跨 Mod 配方在 `OnLateInitializeMelon` 一次接入，避免轮询整个目录。重复配方 ID 返回 `Conflict`。此 API 不注册第二台机器，也不改变机器工厂。

注册成功会复制输入和辅助物品列表；之后修改调用方列表不会改变已暂存配方。追加配方时发布新的完整机器配置，正在处理的事务保留自己的配置。内容委托仍由 Forge 持有，数值规则归内容 Mod。

`NightlyItems` 在 `PlayerStore.EndNight` 前发现机器并结算，让结果进入本轮原生保存。扫描原生物品树，并保留实时存档袋、主背包、后备背包和网格的有界补充扫描；新建档未读档时也走此路径。它检查投入条件、所有权、电量；产物逐件寻找输出区空格，找不到可用空格才使用原版的叠放接纳路径；逐项写入并读回投入、产物和电量，失败时尝试恢复。同一会话按存档槽、周目、日期和机器实例去重。每台机器每晚只执行一批；液体机器每晚只处理一件投入物或继续其已有进度，即使完成也不会在同夜加工另一件。`ResolveBatchCount` 只保留成员签名兼容旧内容 DLL，运行时不再读取或调用它。原版熔炉不再由 Forge 重复调用原生加工，纯度和助溶剂计算继续由游戏处理。投料按注册配方的投入及辅助物品索引接纳；新增原料在原生矿石判定前放行，其余原版熔炉物品继续使用原生判定。模块装入/移出回调继续执行。原版熔炉回收玻璃时，投入区中的基础或高级助溶剂可保留，但不参与该玻璃配方。声明了原版批次探针的熔炉按“原版优先”分工：原版循环对该投入还有事可做时，本夜留给游戏自己的批次（例如够件的金属矿石或废金属），注册配方留到下一夜；原版做不了任何事而槽内又有注册原料时，由 Forge 加工注册配方并跳过原生循环，避免原版循环空转时把玻璃或残余材料留成死料。内容侧使用 `ResolvePowerCost: MachineryHelper.GetMachinePowerUsage` 让同一机器的所有配方按机器当前电耗扣电；电耗可为 0，负值拒绝。`PowerCost` 和解析回调保留以兼容已有提供者。数值回调不应改动库存。

`ForgeMachineRecipe.AuxiliaryItemIds` 允许自定义机器接纳不计入配方的辅助物品。`PrepareOutput` 在扣除材料前逐件调用，可依据机器和选中的投入物设置产物状态，例如调用原版纯度接口；固态与液体模式都会调用它——液体模式下传入本次批次选中的那一件投料，抛错同样取消该批事务（产物被召回、投入物、水位与电量恢复原状），因此内容 Mod 也可以在产物生成后灌装液体并校验读回。

`ForgeMachineRecipe.AcceptsStackedInput` 只对 `NightlyLiquid` 配方有效：置位后整叠投入被视为一份批次投入，投料槽里价值最大的那一件优先（同价值取数量更多的），整叠保留在投料槽直到目标件数完成；不置位时单位数量不为 1 的投入仍返回 `input-stack-unsupported`。配方仍声明一件投入，内容方可以在 `ResolveLifetimeOutputCount` 里读 `unitCount` 并按合并价值决定目标件数；回调返回 0 表示这次投入不够一批，Forge 记 `input-below-batch` 且不动任何状态。进度里记录的 `InitialValue` 在整叠模式下是整叠的合并价值。

完成时的消耗量由可选的 `ForgeMachineRecipe.ResolveConsumedUnits(item, targetCount)` 决定：不声明时消耗整个投入物；声明后只扣回调返回的整件数量（必须在 1 与该叠数量之间），剩余整件留在投料槽继续参与以后夜晚的批次，失败回滚时同时恢复数量。因此“按整百凑件、余料留下”的规则由内容方计算，Forge 只负责按量扣除与恢复。

液体模式 `NightlyLiquid` 使用熔炉窗口第六槽作为独立水源槽。Forge 读取原生水质条件和液体组成；原生体积每 1000 part 为 1 ml，每件产物按 `ForgeMachineWaterRequirement` 扣水。首次加工冻结整件投入物的总目标，每晚产量同时受水量及配方的 `MaxOutputsPerNight` 限制，并将累计进度写在机器实例标签中；最终完成才移除投入物。内容 Mod 应依据输出物占格和输出区面积设置该上限。失败时按原液体组分恢复，恢复失败会隔离该机器。

`ForgeMachineRecipe.ResolveWaterMillilitres(machine, input)` 把固定的“每件产物毫升数”换成按批计算：返回值就是本次产量中每件产物的毫升数，Forge 用它做可用量判断与扣水，因此内容方可以让用水量跟着本批实际投入走（例如按投入价值 1:1 计）。回调返回非正值或抛错时该夜按 `water-rule-invalid` 判失败，不扣任何状态。声明了该回调的配方可以同时把 `MillilitresPerOutput` 写成 0。

`ForgeMachineRecipe.ProgressVersion` 标记配方的批次计价语义，默认 1。写入进度记录时会一并记下该值：读到的记录版本与当前配方不同，或记录来自更早的 Forge 记录格式时，Forge 丢弃该记录并清空进度标签，让机器从槽内投入重新开一批。修改了逐批产量/扣水规则的内容 Mod 应递增该值，避免存档里按旧目标继续加工。

`ForgeMachineApi.Snapshot()` 返回注册项和运行门控；`ForgeCapabilities.Current.MachineNightProcessing` 只表示原生补丁已安装，不表示某台机器成功加工。运行日志使用 `[NicokoboForge/Machine]` 前缀。当前仅支持上述两种夜间模式。纯编译、注册和补丁安装不能替代可丢弃存档中的投料、产物、电量、耗水和重载检查。
