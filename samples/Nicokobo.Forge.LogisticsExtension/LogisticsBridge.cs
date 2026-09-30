using Il2Cpp;
using Nicokobo.Forge;
using Nicokobo.Forge.Registration;

namespace Nicokobo.Forge.LogisticsExtension;

/// <summary>Content-side use of Nicokobo Forge's generic item and run-data services.
/// A logistics Mod supplies its own IDs, factories, UI and game adapter.</summary>
public static class LogisticsBridge
{
    public static SubmitResult RegisterCard(string ownerId, string itemId,
        Func<GameItem> factory) =>
        ForgeItemApi.RegisterItem(ownerId, itemId, factory);

    public static SubmitResult RegisterPushNode(string ownerId, string itemId,
        Func<GameItem> factory) =>
        ForgeItemApi.RegisterNode(ownerId, itemId, factory);

    public static SubmitResult RegisterPullNode(string ownerId, string itemId,
        Func<GameItem> factory) =>
        ForgeItemApi.RegisterNode(ownerId, itemId, factory);

    public static SubmitResult RegisterNodeEffect(string ownerId, string effectId,
        Func<ModuleEffectHelper.ModuleEffect> factory,
        bool randomEligible = false) =>
        ForgeEffectApi.RegisterEffect(ownerId, effectId, factory,
            randomEligible);

    public static (RunDataResult Native, LogisticsConfigRead? Parsed) ReadConfig(
        PlayerStore? store, string ownerId, string runId, int slotId)
    {
        var native = ForgeRunDataApi.Read(store, ownerId, ownerId + ".logistics");
        return native.Status == RunDataStatus.Present
            ? (native, LogisticsConfigCodec.Read(native.Json, runId, slotId))
            : (native, null);
    }

    public static RunDataResult StageConfig(PlayerStore? store, string ownerId,
        LogisticsConfig next, string? expectedJson)
    {
        var json = LogisticsConfigCodec.Write(next);
        return ForgeRunDataApi.Stage(store, ownerId, ownerId + ".logistics",
            next.RunId, next.SlotId, expectedJson, json);
    }
}
