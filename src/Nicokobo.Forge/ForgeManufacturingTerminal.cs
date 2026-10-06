namespace Nicokobo.Forge;

/// <summary>The shared, Forge-owned manufacturing machine. Content mods
/// contribute recipes through RegisterAdditionalRecipes; Forge supplies the item.</summary>
public static class ForgeManufacturingTerminal
{
    public const string OwnerId = "nicokobo.forge.machines";
    public const string ItemId = OwnerId + ".universal_manufacturing_terminal";
    public const string LegacyItemId = "nicokobo.mechcore.synthesis.universal_manufacturing_terminal";
    public const string LegacyManufacturerId = "nicokobo.mechcore.synthesis.mechanical_manufacturer";
    public const string ChineseName = "全域制造终端";
    public const string EnglishName = "Universal Manufacturing Terminal";
    public const string AtlasKey = "Nicokobo/Forge/Machines";
    public const string SpriteKey = "nicokobo_forge_universal_manufacturing_terminal";
    public const int Width = ForgeNumbers.ManufacturingTerminal.Width;
    public const int Height = ForgeNumbers.ManufacturingTerminal.Height;
    public const int BaseValue = ForgeNumbers.ManufacturingTerminal.BaseValue;
    public const int InputWidth = ForgeNumbers.ManufacturingTerminal.InputWidth;
    public const int InputHeight = ForgeNumbers.ManufacturingTerminal.InputHeight;
    public const int OutputWidth = ForgeNumbers.ManufacturingTerminal.OutputWidth;
    public const int OutputHeight = ForgeNumbers.ManufacturingTerminal.OutputHeight;
    public const int ModuleWidth = ForgeNumbers.ManufacturingTerminal.ModuleWidth;
    public const int ModuleHeight = ForgeNumbers.ManufacturingTerminal.ModuleHeight;
    public const int BasePowerCost = ForgeNumbers.ManufacturingTerminal.BasePowerCost;
    public const int DefaultProductionMarkupPercent = ForgeNumbers.ManufacturingTerminal.ProductionMarkupPercent;

    public static string NormalizeItemId(string itemId) =>
        itemId is LegacyItemId or LegacyManufacturerId ? ItemId : itemId;
}
