using Il2Cpp;
using Il2CppInterop.Runtime;
using Nicokobo.Forge.Registration;
using Nicokobo.Forge.Runtime;

namespace Nicokobo.Forge;

internal static class ForgeManufacturingTerminalRuntime
{
    private const string HookId = "nicokobo.forge.manufacturing_terminal.save";
    private const string BasePowerTag = "MACHINERY_DRAWN_POWER_BASE";
    private static readonly LocalizedItemText Description = new(
        "用于合成原版及附属模组物品，制造范围由配方决定。\n效率：降低每批电耗。\n性能：无影响，每晚 1 批。\n质量：无影响。\n原料、电量与产物空间充足才加工。",
        "Crafts items from the base game and add-on mods according to available recipes.\nEfficiency: Lowers power use per batch.\nPerformance: No effect; 1 batch per night.\nQuality: No effect.\nRequires ingredients, power and output space.");
    private static readonly LocalizedItemText Flavor = new(
        "每一份配方，都能在这里变成实物。",
        "Every recipe can take physical form here.");
    private static readonly ForgeSpriteAtlas Atlas = ForgeAssetsApi.CreateEmbeddedSpriteAtlas(
        ForgeManufacturingTerminal.OwnerId, ForgeManufacturingTerminal.AtlasKey,
        typeof(ForgeManufacturingTerminalRuntime).Assembly,
        [new(ForgeManufacturingTerminal.SpriteKey,
            "Nicokobo.Forge.Assets.Icons.universal_manufacturing_terminal.png",
            ForgeManufacturingTerminal.Width * 16, ForgeManufacturingTerminal.Height * 16)]);
    private static Action<string>? _log;
    private static bool _installed;

    internal static void Install(bool allowed, Action<string> log)
    {
        if (_installed) return;
        _log = log;
        if (!allowed)
        {
            log("[WARN] [NicokoboForge/Terminal] built-in machine unavailable: native capability gates");
            return;
        }
        if (!NativeHookSet.Install(HookId,
            [new(typeof(SaveManager), nameof(SaveManager.DecodeNodes),
                [typeof(Il2CppSystem.Collections.Generic.List<SaveItemNode>)], typeof(GameItem),
                typeof(ForgeManufacturingTerminalRuntime), nameof(BeforeDecode), nameof(AfterDecode))], log)) return;
        try
        {
            ForgePresentationApi.RegisterOwnerDisplayName(ForgeManufacturingTerminal.OwnerId,
                "Nicokobo Forge", "Nicokobo Forge");
            var template = new ForgeMachineTemplate(
                new(ForgeManufacturingTerminal.InputWidth, ForgeManufacturingTerminal.InputHeight),
                new(ForgeMachineOutputKind.Items,
                    new(ForgeManufacturingTerminal.OutputWidth, ForgeManufacturingTerminal.OutputHeight)))
            {
                Modules = new(new(ForgeManufacturingTerminal.ModuleWidth, ForgeManufacturingTerminal.ModuleHeight),
                    ["MODULE_TYPE_FURNACE", "MODULE_TYPE_UNIVERSAL"])
            };
            var result = ForgeMachineRegistrationApi.RegisterMachine(ForgeManufacturingTerminal.OwnerId,
                new(ForgeManufacturingTerminal.ItemId, template, [],
                    new(0, context => ForgePowerApi.GetMachineCost(context.Machine)),
                    ConfigureItem: Configure,
                    ItemOptions: new(NightShopStockPolicy.Repeatable,
                        IsNightShopAvailable: () => RNG.Roll(ForgeNumbers.ManufacturingTerminal.NightShopChancePercent),
                        Name: new(ForgeManufacturingTerminal.ChineseName, ForgeManufacturingTerminal.EnglishName),
                        ShortDescription: Description, FlavorText: Flavor),
                    ProductionMarkupPercent: ForgeManufacturingTerminal.DefaultProductionMarkupPercent));
            if (result.Status != SubmitStatus.Accepted) throw new InvalidOperationException(result.Reason);
            _installed = true;
            LootRegistry.AddEntry(ForgeManufacturingTerminal.OwnerId, TableMaster.toolTable,
                ForgeManufacturingTerminal.ItemId, ForgeNumbers.ManufacturingTerminal.LootWeight);
            log("[INFO] [NicokoboForge/Terminal] shared machine staged; recipes=content contributions; legacy save IDs=compatible");
        }
        catch (Exception ex)
        {
            _installed = false;
            NativeHookSet.Remove(HookId, log);
            log($"[WARN] [NicokoboForge/Terminal] built-in machine disabled: {ex.Message}");
        }
    }

    private static void Configure(GameItem item)
    {
        if (!_installed) throw new InvalidOperationException("Forge manufacturing terminal is unavailable");
        item.SetShape(new GridShapeBuilder().SetData(
            new string('1', ForgeManufacturingTerminal.Width * ForgeManufacturingTerminal.Height),
            ForgeManufacturingTerminal.Width).Build());
        Present(item);
        item.unitBaseValue = 0;
        item.unitValue = ForgeManufacturingTerminal.BaseValue;
        item.mayStackItemFunc = null;
        item.forceDisableActivate = false;
        ApplyBasePower(item);
        ModuleHelper.ApplyModifiedPower(item);
        if (item.unitValue != ForgeManufacturingTerminal.BaseValue || item.mayStackItemFunc != null ||
            item.forceDisableActivate || !ShapeMatches(item.shape) || !ShapeMatches(item.modifiedShape) ||
            !item.IsGameItemType("MACHINE") || item.IsGameItemType("NODE") || item.IsGameItemType("MODULE"))
            throw new InvalidOperationException("Forge terminal native item contract failed");
    }

    private static bool ShapeMatches(GridShape? shape)
    {
        if (shape == null || shape.Pointer == IntPtr.Zero ||
            shape.width != ForgeManufacturingTerminal.Width || shape.height != ForgeManufacturingTerminal.Height ||
            shape.globalWidth != ForgeManufacturingTerminal.Width || shape.globalHeight != ForgeManufacturingTerminal.Height ||
            shape.orientation != 0) return false;
        for (int y = 0; y < ForgeManufacturingTerminal.Height; y++)
        for (int x = 0; x < ForgeManufacturingTerminal.Width; x++)
            if (shape.GetLocal(x, y) != 1) return false;
        return true;
    }

    private static void Present(GameItem item)
    {
        bool english = ForgePresentationApi.PreferEnglish();
        item.name = english ? ForgeManufacturingTerminal.EnglishName : ForgeManufacturingTerminal.ChineseName;
        item.shortDescription = Description.For(english);
        item.flavorText = Flavor.For(english);
        Atlas.TryInstall(null, ex => _log?.Invoke($"[WARN] [NicokoboForge/Terminal] icon unavailable: {ex.Message}"));
        Atlas.Assign(item, ForgeManufacturingTerminal.SpriteKey);
    }

    private static void ApplyBasePower(GameItem item)
    {
        var tag = item.GetTagReadonly(BasePowerTag)
            ?? throw new InvalidOperationException("Forge terminal base power tag missing");
        if (tag.GetInt() == ForgeManufacturingTerminal.BasePowerCost) return;
        item.ModifyTag(BasePowerTag, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<TagState>>(
            new Action<TagState>(state => state.SetInt(ForgeManufacturingTerminal.BasePowerCost))));
        if (item.GetTagReadonly(BasePowerTag)?.GetInt() != ForgeManufacturingTerminal.BasePowerCost)
            throw new InvalidOperationException("Forge terminal base power readback mismatch");
    }

    private static void BeforeDecode(Il2CppSystem.Collections.Generic.List<SaveItemNode> __0)
    {
        if (!_installed || !NativeItemRegistry.IsAppliedItem(ForgeManufacturingTerminal.ItemId)) return;
        int migrated = ManufacturingTerminalSaveMigration.Apply(__0);
        if (migrated != 0)
            _log?.Invoke($"[INFO] [NicokoboForge/Terminal] migrated native save nodes={migrated}; saved links and positions retained");
    }

    private static void AfterDecode(Il2CppSystem.Collections.Generic.List<SaveItemNode> __0)
    {
        if (!_installed || __0 == null) return;
        foreach (var node in __0)
        {
            var item = node?.tempLink;
            if (item == null || item.Pointer == IntPtr.Zero || item.identifier != ForgeManufacturingTerminal.ItemId) continue;
            try
            {
                Present(item);
                ApplyBasePower(item);
                ModuleHelper.ApplyModifiedPower(item);
                if (!ForgeMachineRuntimeApi.TryGetInventory(item, out _))
                    throw new InvalidOperationException("Restored terminal slots unavailable");
                item.contentWindow.SetTitle(item.name);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"[WARN] [NicokoboForge/Terminal] restored item={item.uniqueId}; {ex.Message}");
            }
        }
    }
}
