using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

internal static class ManufacturingTerminalChecks
{
    internal static void Run()
    {
        int checks = 0;
        void Expect(bool value, string message)
        { checks++; if (!value) throw new InvalidOperationException("Manufacturing terminal: " + message); }

        var catalog = new MachineCatalog();
        var definition = new ForgeMachineDefinition(ForgeManufacturingTerminal.ItemId,
            new(new(9, 6), new(ForgeMachineOutputKind.Items, new(9, 6))), [], new(25),
            ProductionMarkupPercent: 15);
        Expect(catalog.Register(ForgeManufacturingTerminal.OwnerId, definition,
            _ => new(SubmitStatus.Accepted, "staged")).Status == SubmitStatus.Accepted,
            "Forge must register the terminal without a content mod or placeholder recipe");
        Expect(catalog.TryGet(definition.MachineId, out var empty) && empty!.Recipes.Count == 0 &&
            empty.Definition.ResolveNightlyBatchCount == null && empty.OwnerId == ForgeManufacturingTerminal.OwnerId,
            "an empty Forge terminal must retain Forge ownership and the default one-night batch");
        Expect(catalog.RegisterAdditional("test.synthesis", definition.MachineId, []).Status == SubmitStatus.Invalid,
            "empty recipe contributions must still be rejected");
        var first = new ForgeMachineRecipe("test.synthesis.recipe", [new("part", 2)], new ForgeMachineItemOutput("product"));
        var second = new ForgeMachineRecipe("test.aug.recipe", [new("core", 1)], new ForgeMachineItemOutput("cybernetic"));
        Expect(catalog.RegisterAdditional("test.synthesis", definition.MachineId, [first]).Status == SubmitStatus.Accepted,
            "synthesis recipes must attach to the Forge item");
        Expect(catalog.ConfigureManufacturingTerminalMarkup("test.synthesis", 20).Status == SubmitStatus.Accepted,
            "a custom K05 markup must carry over to the shared terminal");
        Expect(catalog.RegisterAdditional("test.aug", definition.MachineId, [second]).Status == SubmitStatus.Accepted,
            "a second mod must contribute without creating another terminal");
        Expect(catalog.TryGet(definition.MachineId, out var shared) && shared!.Recipes.Count == 2 &&
            shared.Definition.ProductionMarkupPercent == 20 && shared.OwnerId == ForgeManufacturingTerminal.OwnerId &&
            shared.Recipes.Select(recipe => recipe.OwnerId).SequenceEqual(new[] { "test.synthesis", "test.aug" }),
            "contributions must preserve the Forge item and both recipe owners with the shared rate");
        Expect(catalog.ConfigureManufacturingTerminalMarkup("test.aug", 30).Status == SubmitStatus.Conflict,
            "another provider must not overwrite an already configured shared rate");
        Expect(catalog.ConfigureManufacturingTerminalMarkup("test.synthesis", 1001).Status == SubmitStatus.Invalid &&
            catalog.ConfigureManufacturingTerminalMarkup("test.synthesis", 20).Status == SubmitStatus.AlreadyPresent &&
            catalog.TryGet(definition.MachineId, out var unchanged) && unchanged!.Recipes.Count == 2 &&
            unchanged.Definition.ProductionMarkupPercent == 20,
            "invalid or repeated configuration must leave recipes and pricing intact");

        foreach (string oldId in new[] { ForgeManufacturingTerminal.LegacyItemId, ForgeManufacturingTerminal.LegacyManufacturerId })
        {
            var tags = new object();
            var shape = new object();
            var links = new Il2CppSystem.Collections.Generic.List<long> { 11, 12, 13 };
            var slots = new Il2CppSystem.Collections.Generic.List<int> { 2, 3, 6 };
            var terminal = new Il2Cpp.SaveItemNode
            {
                identifier = oldId, uuid = 10, uniqueId = 645, unitCount = 1, unitValue = 431,
                itemState = tags, itemShape = shape, itemModifiedShape = shape,
                itemTypes = new() { "CUSTOM_CATEGORY" }, childItems = links, childItemInventoryNode = slots,
                spriteAtlasPath = "old/atlas", spritePath = "old_terminal"
            };
            var battery = new Il2Cpp.SaveItemNode { identifier = "energy_credit_ext", uuid = 11, unitValue = 72, itemState = new object() };
            var module = new Il2Cpp.SaveItemNode { identifier = "custom.module", uuid = 12, itemModifiedShape = new object() };
            var output = new Il2Cpp.SaveItemNode { identifier = "custom.output", uuid = 13, unitCount = 2, unitValue = 1000 };
            var nodes = new Il2CppSystem.Collections.Generic.List<Il2Cpp.SaveItemNode> { terminal, battery, module, output };
            Expect(ManufacturingTerminalSaveMigration.Apply(nodes) == 1 && terminal.identifier == definition.MachineId,
                "a saved legacy terminal must select the Forge factory before decode");
            Expect(terminal.uuid == 10 && terminal.uniqueId == 645 && terminal.unitCount == 1 && terminal.unitValue == 431 &&
                ReferenceEquals(terminal.itemState, tags) && ReferenceEquals(terminal.itemShape, shape) &&
                ReferenceEquals(terminal.itemModifiedShape, shape), "migration must preserve identity, tags, value and placement");
            Expect(ReferenceEquals(terminal.childItems, links) && ReferenceEquals(terminal.childItemInventoryNode, slots) &&
                links.SequenceEqual(new long[] { 11, 12, 13 }) && slots.SequenceEqual(new[] { 2, 3, 6 }) &&
                ReferenceEquals(nodes[1], battery) && ReferenceEquals(nodes[2], module) && ReferenceEquals(nodes[3], output) &&
                battery.unitValue == 72 && output.unitCount == 2 && output.unitValue == 1000,
                "battery, module and output records and their saved inventory indices must survive unchanged");
            Expect(terminal.itemTypes.SequenceEqual(new[] { "CUSTOM_CATEGORY", "MACHINE" }) &&
                terminal.spriteAtlasPath == ForgeManufacturingTerminal.AtlasKey &&
                terminal.spritePath == ForgeManufacturingTerminal.SpriteKey,
                "native decode must receive the machine category and the Forge sprite");
            Expect(ManufacturingTerminalSaveMigration.Apply(nodes) == 0 && terminal.itemTypes.Count == 2,
                "reloading an already migrated record must not duplicate categories or items");
        }
        Expect(ManufacturingTerminalSaveMigration.Apply(null) == 0, "missing inventory graphs must be harmless");
        Console.WriteLine($"Manufacturing terminal registration and save migration checks passed: {checks}.");
    }
}

namespace Il2Cpp
{
    // Native save-node fields used by the production migration, with opaque
    // state/shape fixtures. These checks do not simulate native saving or loading.
    public sealed class SaveItemNode
    {
        public string identifier = "", spriteAtlasPath = "", spritePath = "";
        public long uuid, unitValue;
        public int uniqueId, unitCount;
        public object? itemState, itemShape, itemModifiedShape;
        public Il2CppSystem.Collections.Generic.List<string>? itemTypes;
        public Il2CppSystem.Collections.Generic.List<long>? childItems;
        public Il2CppSystem.Collections.Generic.List<int>? childItemInventoryNode;
    }
}

namespace Il2CppSystem.Collections.Generic
{
    public sealed class List<T> : System.Collections.Generic.List<T> { }
}
