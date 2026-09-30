using Il2Cpp;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge;

public sealed record NativeEffectRegistration(string EffectId, Func<ModuleEffectHelper.ModuleEffect> Factory, bool RandomEligible = false);

/// <summary>Effect ID claims and native application. Content callbacks remain
/// owned by the content mod; effects are never used as generic lifecycle hooks.</summary>
public static class ForgeEffectApi
{
    public static SubmitResult RegisterEffect(string ownerId, string effectId, Func<ModuleEffectHelper.ModuleEffect> factory, bool randomEligible = false) => NativeEffectRegistry.RegisterEffect(ownerId, effectId, factory, randomEligible);
    public static SubmitResult RegisterEffects(string ownerId, IReadOnlyList<NativeEffectRegistration> effects) => NativeEffectRegistry.RegisterEffects(ownerId, effects);
    public static IReadOnlyList<NativeApplicationView> Snapshot() => NativeEffectRegistry.Snapshot();
}
