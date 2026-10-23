using MelonLoader;
using Nicokobo.CompatibilityPatches.Patches.PCExpansionScalars;
using Nicokobo.CompatibilityPatches.Patches.WagePerksDestinyDice;
using Nicokobo.CompatibilityPatches.Patches.WagePerksStartup;

[assembly: MelonInfo(typeof(Nicokobo.CompatibilityPatches.Plugin), "Nicokobo Compatibility Patches", "0.1.7", "Nicokobo")]
[assembly: MelonGame("Questing Goose Studio", "Probably Stolen")]
[assembly: MelonPriority(-10000)]

namespace Nicokobo.CompatibilityPatches;

/// <summary>One optional Mod containing the individual compatibility patches.</summary>
public sealed class Plugin : MelonMod
{
    private readonly WagePerksStartupPatch _wagePerksStartup;
    private readonly WagePerksDestinyDicePatch _wagePerksDestinyDice;
    private readonly PCExpansionScalarsPatch _pcExpansionScalars;

    public Plugin()
    {
        _wagePerksStartup = new WagePerksStartupPatch(this);
        _wagePerksDestinyDice = new WagePerksDestinyDicePatch(this);
        _pcExpansionScalars = new PCExpansionScalarsPatch(this);
    }

    public override void OnInitializeMelon()
    {
        _wagePerksStartup.Initialize();
        _wagePerksDestinyDice.Initialize();
        _pcExpansionScalars.Initialize();
    }
    public override void OnUpdate() => _wagePerksStartup.Update();
    public override void OnDeinitializeMelon()
    {
        _wagePerksStartup.Deinitialize();
        _wagePerksDestinyDice.Deinitialize();
        _pcExpansionScalars.Deinitialize();
    }
}
