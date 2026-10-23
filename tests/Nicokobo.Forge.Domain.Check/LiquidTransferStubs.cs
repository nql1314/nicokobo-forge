using Nicokobo.Forge;

namespace Il2Cpp
{
    public sealed partial class GameItem
    {
        public ForgeMachineLiquidSnapshot? Liquid;
        public bool ThrowLiquidRead;
        public ItemFeature WaterFeature = new();
    }
    public sealed class ItemFeature
    {
        private bool _exposed;
        public int RealPurity;
        public int FakePurity;
        public bool IsExposed() => _exposed;
        public void SetExposed(bool value) => _exposed = value;
        public int DisplayedPurity => _exposed ? RealPurity : FakePurity;
    }
    public static class WaterHelper
    {
        public static Action<GameItem, GameItem>? Transfer;
        public static int TransferCalls;
        public static void TransferLiquid(GameItem source, GameItem target)
        { TransferCalls++; Transfer?.Invoke(source, target); }
        public static GameItem? GetFilterItem(GameItem item) => null;
        public static int GetWaterPurity(GameItem item) => item.Liquid is { TotalParts: > 0 } liquid
            ? (int)(liquid.Contents.Where(part => part.LiquidId == "water").Sum(part => (long)part.Parts) * 10000 / liquid.TotalParts) : 0;
        public static void EmptyContainer(GameItem item)
        {
            item.Liquid = item.Liquid! with { Contents = item.Liquid.Contents.Select(part =>
                part with { Parts = 0, Value = null, QualityBasis = null }).ToArray() };
            WaterFeatureHelper.UpdateWaterFeature(item, true);
            item.WaterFeature.SetExposed(false);
        }
        public static void AddLiquid(GameItem item, string id, int parts)
        {
            item.Liquid = MachineBatchMath.Add(item.Liquid!, [new(id, parts)]);
            WaterFeatureHelper.UpdateWaterFeature(item, false);
        }
    }
    public static class WaterFeatureHelper
    {
        public static ItemFeature GetWaterFeature(GameItem item) => item.WaterFeature;
        public static void UpdateWaterFeature(GameItem item, bool expose)
        {
            item.WaterFeature.RealPurity = WaterHelper.GetWaterPurity(item);
            item.WaterFeature.SetExposed(expose);
        }
    }
    public static class SpriteHelper { public static void UpdateWaterContainerSprite(GameItem item) { } }
}

namespace Nicokobo.Forge
{
    public static class ForgeMachineRuntimeApi
    {
        public static bool RuntimeInstalled = true;
    }
    public static partial class ForgeLiquidApi
    {
        public static ForgeMachineLiquidSnapshot? Capture(Il2Cpp.GameItem item) =>
            item.ThrowLiquidRead ? throw new InvalidOperationException("Liquid read failed") : item.Liquid;
        public static bool Matches(ForgeMachineLiquidSnapshot? current, ForgeMachineLiquidSnapshot expected) =>
            current?.CapacityParts == expected.CapacityParts && current.Contents.SequenceEqual(expected.Contents);
    }
    internal static class ForgeLiquidValueRuntime
    {
        internal static void Store(Il2Cpp.GameItem item, ForgeMachineLiquidSnapshot snapshot) =>
            item.Liquid = item.Liquid! with { Contents = item.Liquid.Contents.Select(part => part with
            {
                Value = snapshot.Contents.Single(expected => expected.LiquidId == part.LiquidId).Value,
                QualityBasis = snapshot.Contents.Single(expected => expected.LiquidId == part.LiquidId).QualityBasis
            }).ToArray() };
    }
    public static class ForgeInventoryFreezeApi
    {
        public static bool IsFrozen(Il2Cpp.GameItem item) => false;
        public static IDisposable? TryAcquire(IEnumerable<Il2Cpp.GameItem> items, string? group = null) => new Lease();
        private sealed class Lease : IDisposable { public void Dispose() { } }
    }
}
