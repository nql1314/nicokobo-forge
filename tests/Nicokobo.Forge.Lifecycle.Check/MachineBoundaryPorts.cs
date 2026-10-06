using System.Reflection;
using Il2Cpp;
using Nicokobo.Forge.Registration;

// Resource planning/commit and native UI are boundaries for this lifecycle
// check. The production machine installer and its actual nightly attempt and
// quarantine ledgers are linked; no resource write is simulated as native work.
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static MethodInfo? Method(Type type, string name, Type[] arguments) =>
            type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance,
                null, arguments, null);
    }
}
namespace Nicokobo.Forge.Registration
{
    public sealed record NativeItemOptions;
    internal sealed record RegisteredMachineRecipe(string OwnerId, ForgeMachineRecipe Value);
    internal sealed class MachineProfile(ForgeMachineDefinition definition)
    {
        internal ForgeMachineDefinition Definition => definition;
        internal string MachineId => definition.MachineId;
        internal ForgeMachineTemplate Template => definition.Template;
        internal ForgeMachinePowerRule Power => definition.Power;
        internal IReadOnlyList<RegisteredMachineRecipe> Recipes => [];
    }
}
namespace Nicokobo.Forge
{
    public sealed record ForgeRecipeGuideDisplay;
    public sealed record ForgeRecipeGuideOptions;
    internal static class ForgeMachineRegistrationApi
    {
        internal static MachineProfile Profile = new(new("lifecycle-machine",
            new(null, new(ForgeMachineOutputKind.Items, new(1, 1))), [], new()));
        internal static bool HasMachines => true;
        internal static bool TryGet(string id, out MachineProfile? profile)
        { profile = id == Profile.MachineId ? Profile : null; return profile != null; }
        internal static void Log(string message) { }
    }
    internal static class ForgeMachineRuntimeApi { internal static bool RuntimeInstalled => ForgeMachineHooks.Installed; }
    internal static class ForgeMachineUi
    {
        internal static int Reads, Resets;
        internal static void Reset() => Resets++;
        internal static bool TryGet(GameItem item, out ForgeMachineInventory? inventory)
        { Reads++; inventory = null; return false; }
        internal static SlotMarker? FindDropSlot(GameItem item, GameItem offered) => null;
    }
    internal static class NativeProductionValue
    { internal static void Validate() { } internal static long ItemValue(GameItem item) => item.unitValue; }
    internal static class ForgeLiquidValueRuntime
    { internal static bool Install(Action<string> log) => true; internal static void Uninstall() { } }
    internal static class ForgePowerApi
    {
        internal static int? ReadSource(GameItem item) => null;
        internal static bool CanDrawSource(GameItem item, int cost) => false;
    }
    internal static class ForgeLiquidApi
    {
        internal static ForgeMachineLiquidSnapshot? Capture(GameItem item) => null;
        internal static bool Matches(ForgeMachineLiquidSnapshot? a, ForgeMachineLiquidSnapshot b) => false;
        internal static decimal ConsumedValue(GameItem item, ForgeMachineLiquidSnapshot before, ForgeMachineLiquidSnapshot after) => 0;
    }
    internal enum ForgeMachineAutomationPhase { BeforeBatch, AfterBatch }
    internal sealed class ForgeMachineAutomationContext(GameItem machine, ForgeMachineInventory inventory)
    {
        internal GameItem Machine { get; } = machine;
        internal ForgeMachineInventory Inventory { get; } = inventory;
        internal bool Blocked { get; set; }
        internal string? RecipeId { get; set; }
        internal string? Result { get; set; }
    }
    internal static class ForgeMachineAutomationApi
    { internal static void Dispatch(ForgeMachineAutomationPhase phase, ForgeMachineAutomationContext context) { } }
    internal static class ForgeMachinePowerMath
    { internal static int CalculateCost(int cost, int count, bool perOutput) => cost; }
    internal static class MachineNightCycle
    {
        internal static (int Completed, string Status) Run(Func<int> limit, Func<string> process)
        { return (0, process()); }
    }
    internal static class MachineLiquidMath
    { internal static int ToInputParts(int value) => value; internal static int ToParts(int value) => value; }
    internal static class MachineBatchMath
    {
        internal static ForgeMachineLiquidSnapshot Consume(ForgeMachineLiquidSnapshot before, int parts, string? id) => throw new NotSupportedException();
        internal static ForgeMachineLiquidSnapshot Add(ForgeMachineLiquidSnapshot before, IReadOnlyList<ForgeMachineLiquidPart> parts) => throw new NotSupportedException();
        internal static decimal QualityBasis(ForgeMachineLiquidSnapshot before) => 0;
    }
    internal static partial class ForgeMachineRuntime
    {
        private static string Execute(BatchPlan plan) => throw new NotSupportedException("Resource commit is outside this lifecycle fixture");
    }
}
