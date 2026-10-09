using Nicokobo.Forge;

namespace Il2Cpp
{
    public sealed partial class GameItem
    {
        public ForgeMachineLiquidSnapshot? Liquid;
        public bool ThrowLiquidRead;
    }
    public static class WaterHelper
    {
        public static Action<GameItem, GameItem>? Transfer;
        public static int TransferCalls;
        public static void TransferLiquid(GameItem source, GameItem target)
        { TransferCalls++; Transfer?.Invoke(source, target); }
        public static GameItem? GetFilterItem(GameItem item) => null;
    }
    public static class SpriteHelper { public static void UpdateWaterContainerSprite(GameItem item) { } }
}

namespace Nicokobo.Forge
{
    public static class ForgeMachineRuntimeApi
    {
        public static bool RuntimeInstalled = true;
    }
    public static class ForgeLiquidApi
    {
        public static ForgeMachineLiquidSnapshot? Capture(Il2Cpp.GameItem item) =>
            item.ThrowLiquidRead ? throw new InvalidOperationException("Liquid read failed") : item.Liquid;
        public static bool Write(Il2Cpp.GameItem item, ForgeMachineLiquidSnapshot snapshot)
        { item.Liquid = snapshot; return true; }
        public static bool Matches(ForgeMachineLiquidSnapshot? current, ForgeMachineLiquidSnapshot expected) =>
            current?.CapacityParts == expected.CapacityParts && current.Contents.SequenceEqual(expected.Contents);
    }
    public static class ForgeInventoryFreezeApi
    {
        public static bool IsFrozen(Il2Cpp.GameItem item) => false;
        public static IDisposable? TryAcquire(IEnumerable<Il2Cpp.GameItem> items, string? group = null) => new Lease();
        private sealed class Lease : IDisposable { public void Dispose() { } }
    }
}
