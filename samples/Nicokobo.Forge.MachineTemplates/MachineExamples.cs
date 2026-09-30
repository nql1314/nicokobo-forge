using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge.MachineTemplates;

// A compilable declaration sample; no loader entry point or automatic content.
public static class MachineExamples
{
    public const string OwnerId = "nicokobo.forge.machine_examples";

    public static ForgeMachineDefinition MixedInput() => new(OwnerId + ".mixed",
        new(new(6, 4), new(ForgeMachineOutputKind.Items, new(6, 4)))
        { LiquidInputs = [new("water", new(3, 3), "供水容器 / Water input")] },
        [new(OwnerId + ".mixed_recipe", [new("scrap_metal", 2)], new ForgeMachineItemOutput("metal_ingot"))
        { LiquidInputs = [new("water", 100, Condition: item => ForgeLiquidApi.WaterQuality(item) >= 1)] }],
        new(8), item => { item.name = "混合输入示例 / Mixed input example"; });

    public static ForgeMachineDefinition LiquidOnly() => new(OwnerId + ".liquid",
        new(null, new(ForgeMachineOutputKind.Container, new(3, 3)))
        { LiquidInputs = [new("source", new(3, 3))], Modules = null, ManualSlot = false },
        [new(OwnerId + ".liquid_recipe", [], new ForgeMachineContainerOutput([new("water", 90)]))
        { LiquidInputs = [new("source", 100, LiquidId: "water")] }],
        new(8), item => { item.name = "液体输入示例 / Liquid input example"; });

    public static ForgeMachineDefinition ItemToContainer() => new(OwnerId + ".container",
        new(new(6, 4), new(ForgeMachineOutputKind.Container, new(3, 3))),
        [new(OwnerId + ".container_recipe", [new("raw_meat", 1)],
            new ForgeMachineContainerOutput([new("protein", 100)]))],
        new(8), item => { item.name = "容器输出示例 / Container output example"; });

    public static IReadOnlyList<SubmitResult> Register() => new[]
    {
        ForgeMachineRegistrationApi.RegisterMachine(OwnerId, MixedInput()),
        ForgeMachineRegistrationApi.RegisterMachine(OwnerId, LiquidOnly()),
        ForgeMachineRegistrationApi.RegisterMachine(OwnerId, ItemToContainer())
    };
}
