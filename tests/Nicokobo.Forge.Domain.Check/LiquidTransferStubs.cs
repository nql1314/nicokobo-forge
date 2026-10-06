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
    }
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
    }
}
