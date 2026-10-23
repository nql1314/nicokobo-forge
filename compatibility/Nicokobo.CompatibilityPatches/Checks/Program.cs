using Mono.Cecil;
using Nicokobo.CompatibilityPatches.Patches.WagePerksStartup;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}

string[] sensitiveTargets = { "Il2Cpp.ModuleEffectHelper", "Il2Cpp.AugHelper", "Il2Cpp.ItemFeatureList", "Il2Cpp.StoreClientListTierSubstance" };
foreach (string target in sensitiveTargets)
    Check(DeferredStartupPatches.RequiresLocalization(target), $"localized native target escapes deferral: {target}");
Check(!DeferredStartupPatches.RequiresLocalization("Il2Cpp.PlayerStore") &&
    !DeferredStartupPatches.RequiresLocalization(null), "ordinary patch or missing target is deferred");

var gate = new DeferredStartupPatches();
int installs = 0;
bool replayPassed = false;
Check(!gate.Intercept(() => { replayPassed = gate.Intercept(() => throw new Exception("unexpected queued replay")); installs++; }), "early call was not deferred");
Check(installs == 0 && gate.PendingCount == 1, "early interception installed a hook");
for (int i = 0; i < 3; i++) gate.Observe(LocalizationState.Pending);
Check(installs == 0 && gate.PendingCount == 1, "pending initialization ran a hook");
Check(gate.Observe(LocalizationState.Succeeded) == 1, "successful initialization did not drain");
Check(installs == 1 && replayPassed && gate.PendingCount == 0, "replay recursed or changed invocation count");
gate.Observe(LocalizationState.Succeeded);
Check(installs == 1 && gate.Intercept(() => installs++), "ready state replays or suppresses ordinary calls");

var batch = new DeferredStartupPatches();
var order = new List<int>();
for (int i = 0; i < 5; i++)
{
    int callIndex = i;
    batch.Intercept(() => order.Add(callIndex));
}
batch.Observe(LocalizationState.Pending);
Check(order.Count == 0 && batch.PendingCount == 5, "multi-target batch ran early");
Check(batch.Observe(LocalizationState.Succeeded) == 5 && order.SequenceEqual(Enumerable.Range(0, 5)), "deferred calls lost their original order");
batch.Observe(LocalizationState.Succeeded);
Check(order.Count == 5, "multi-target batch repeated");

var failed = new DeferredStartupPatches();
failed.Intercept(() => installs++);
failed.Observe(LocalizationState.Failed);
failed.Observe(LocalizationState.Succeeded);
Check(failed.Failed && failed.PendingCount == 0 && installs == 1, "failed initialization retried a hook");
Check(!failed.Intercept(() => installs++) && failed.PendingCount == 0, "failed state accepted new work");

var throwing = new DeferredStartupPatches();
throwing.Intercept(() => throw new InvalidOperationException("fixture installation failure"));
try { throwing.Observe(LocalizationState.Succeeded); throw new Exception("failure hidden"); }
catch (InvalidOperationException) { }
Check(throwing.Failed && throwing.PendingCount == 0, "installation failure did not disable the gate");
Check(throwing.Observe(LocalizationState.Succeeded) == 0, "installation failure retried");

string gameDir = args[0];
ScalarAliasChecks.Run(Check);
string? wagePath = Directory.EnumerateFiles(Path.Combine(gameDir, "Mods"), "*.dll")
    .FirstOrDefault(path => Path.GetFileName(path).Contains("WagePerks", StringComparison.OrdinalIgnoreCase));
using var wage = wagePath == null ? null : AssemblyDefinition.ReadAssembly(wagePath);
if (wage?.CustomAttributes.Single(a => a.AttributeType.FullName == "MelonLoader.MelonInfoAttribute")
    .ConstructorArguments[2].Value as string == "1.3.4")
{
var info = wage.CustomAttributes.Single(a => a.AttributeType.FullName == "MelonLoader.MelonInfoAttribute");
Check((string)info.ConstructorArguments[2].Value == "1.3.4", "installed WagePerks is not the supported version");
var priority = wage.CustomAttributes.SingleOrDefault(a => a.AttributeType.FullName == "MelonLoader.MelonPriorityAttribute");
Check(priority == null || (int)priority.ConstructorArguments[0].Value > -10000, "compatibility patch would initialize too late");
var patcher = wage.MainModule.Types.Single(t => t.FullName == "WagePerks.ManualPatcher");
var method = patcher.Methods.Single(m => m.Name == "TryPatch");
string[] expected = { "System.Type", "System.String", "System.String", "System.String", "System.Type[]", "System.Type", "System.Nullable`1<System.Int32>", "System.String" };
Check(method.IsStatic && method.IsAssembly && method.ReturnType.FullName == "System.Void" &&
    method.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(expected), "TryPatch signature changed");
var okCount = patcher.Properties.Single(p => p.Name == "PatchOkCount");
Check(okCount.PropertyType.FullName == "System.Int32" && okCount.GetMethod.IsStatic, "success-count contract changed");
var registry = wage.MainModule.Types.Single(t => t.FullName == "WagePerks.PatchRegistryTable");
var registryTypes = registry.Methods.Where(m => m.HasBody).SelectMany(m => m.Body.Instructions)
    .Select(i => i.Operand).OfType<TypeReference>().Select(t => t.FullName).ToHashSet();
foreach (string target in sensitiveTargets)
    Check(registryTypes.Contains(target), $"expected native target was not found: {target}");

var dice = wage.MainModule.Types.Single(t => t.FullName == "WagePerks.DestinyDice");
var diceRegister = dice.Methods.Single(m => m.Name == "RegisterToDirectory");
Check(diceRegister.IsPublic && diceRegister.IsStatic && diceRegister.ReturnType.FullName == "System.Void" &&
    diceRegister.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "Il2Cpp.ItemDirectory" }),
    "original dice registration contract changed");
Check(dice.Fields.Any(f => f.Name == "_diceFactory" && f.IsStatic &&
    f.FieldType.FullName == "Il2CppSystem.Func`1<Il2Cpp.GameItem>"), "original dice factory no longer retains its native delegate");
var directoryCallback = wage.MainModule.Types.Single(t => t.FullName == "WagePerks.Patches")
    .Methods.Single(m => m.Name == "PostfixInitDirectory");
Check(directoryCallback.IsPublic && directoryCallback.IsStatic && directoryCallback.ReturnType.FullName == "System.Void" &&
    directoryCallback.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[] { "Il2Cpp.ItemDirectory" }),
    "existing directory callback contract changed");
foreach (string directory in new[] { "Il2Cpp.ContainerItemDirectory", "Il2Cpp.AmenitiesItemDirectory", "Il2Cpp.ModItemDirectory" })
    Check(registryTypes.Contains(directory), $"original directory initialization hook is missing: {directory}");
}
else Console.WriteLine("Wage's Perks 1.3.4 target-contract checks skipped: this optional target is not installed.");
using var native = AssemblyDefinition.ReadAssembly(Path.Combine(gameDir, "MelonLoader", "Il2CppAssemblies", "Assembly-CSharp.dll"));
var has = native.MainModule.Types.Single(t => t.FullName == "Il2Cpp.Directory`1")
    .Methods.Single(m => m.Name == "Has" && m.Parameters.Count == 1 &&
        m.Parameters[0].ParameterType.FullName == "System.String");
Check(has.IsPublic && !has.IsStatic && has.ReturnType.FullName == "System.Boolean" &&
    has.Parameters[0].ParameterType.FullName == "System.String", "native directory verification contract changed");

using var localization = AssemblyDefinition.ReadAssembly(Path.Combine(gameDir, "MelonLoader", "Il2CppAssemblies", "Unity.Localization.dll"));
var settings = localization.MainModule.Types.Single(t => t.FullName == "UnityEngine.Localization.Settings.LocalizationSettings");
Check(settings.Properties.Any(p => p.Name == "HasSettings" && p.GetMethod.IsStatic), "missing settings-readiness API");
Check(settings.Properties.Any(p => p.Name == "m_InitializingOperationHandle" && p.GetMethod.IsPublic), "missing existing initialization handle");

using var binary = AssemblyDefinition.ReadAssembly(args[1]);
var loadPriority = binary.CustomAttributes.Single(a => a.AttributeType.FullName == "MelonLoader.MelonPriorityAttribute");
Check((int)loadPriority.ConstructorArguments[0].Value == -10000, "compiled priority changed");
Check(!binary.MainModule.AssemblyReferences.Any(r => r.Name is "WagePerks" or "Nicokobo.Forge" or "Assembly-CSharp"), "compatibility DLL gained a target-Mod or Forge dependency");
Check(binary.Name.Name == "Nicokobo.CompatibilityPatches", "unified assembly name changed");
var modInfo = binary.CustomAttributes.Single(a => a.AttributeType.FullName == "MelonLoader.MelonInfoAttribute");
Check((string)modInfo.ConstructorArguments[1].Value == "Nicokobo Compatibility Patches", "unified Mod name changed");
Check((string)modInfo.ConstructorArguments[2].Value == "0.1.7" && binary.Name.Version == new Version(0, 1, 7, 0), "Mod and assembly versions differ");
Check(!binary.MainModule.Types.Any(t => t.Namespace.EndsWith(".AugOpeningReputation", StringComparison.Ordinal)),
    "retired reputation patch is still compiled into the compatibility DLL");
Check(!binary.MainModule.Types.Any(t => t.Namespace.EndsWith(".AugOpeningInventory", StringComparison.Ordinal)),
    "retired inventory patch would override the fixed Aug identity reader");
var patch = binary.MainModule.Types.Single(t => t.FullName == "Nicokobo.CompatibilityPatches.Patches.WagePerksStartup.WagePerksStartupPatch");
var reader = patch.Methods.Single(m => m.Name == "ReadLocalizationState");
var calls = reader.Body.Instructions.Select(i => i.Operand).OfType<MethodReference>().ToArray();
Check(calls.Any(m => m.Name == "get_m_InitializingOperationHandle") &&
    !calls.Any(m => m.Name is "WaitForCompletion" or "get_InitializationOperation"), "readiness check starts or blocks initialization");
var prefix = patch.Methods.Single(m => m.Name == "BeforeTryPatch");
Check(prefix.ReturnType.FullName == "System.Boolean" && prefix.Parameters.Count == 1 &&
    prefix.Parameters[0].Name == "__args" && prefix.Parameters[0].ParameterType.FullName == "System.Object[]", "compiled Harmony prefix contract changed");
var dicePatch = binary.MainModule.Types.Single(t => t.FullName ==
    "Nicokobo.CompatibilityPatches.Patches.WagePerksDestinyDice.WagePerksDestinyDicePatch");
var dicePostfix = dicePatch.Methods.Single(m => m.Name == "AfterInitDirectory");
Check(dicePostfix.ReturnType.FullName == "System.Void" && dicePostfix.Parameters.Count == 2 &&
    dicePostfix.Parameters[0].Name == "__args" && dicePostfix.Parameters[0].ParameterType.FullName == "System.Object[]" &&
    dicePostfix.Parameters[1].Name == "__runOriginal" && dicePostfix.Parameters[1].ParameterType.FullName == "System.Boolean",
    "compiled dice Harmony postfix contract changed");
var pcPatch = binary.MainModule.Types.Single(t => t.FullName ==
    "Nicokobo.CompatibilityPatches.Patches.PCExpansionScalars.PCExpansionScalarsPatch");
foreach (string name in new[] { "BeforeApply", "BeforeCompare" })
    Check(pcPatch.Methods.Single(m => m.Name == name).ReturnType.FullName == "System.Void",
        "field compatibility must not short-circuit original restoration or validation");
var gameItem = native.MainModule.Types.Single(t => t.FullName == "Il2Cpp.GameItem");
Check(gameItem.Properties.Single(p => p.Name == "isDebugMenu").SetMethod == null &&
    gameItem.Properties.Single(p => p.Name == "_isDebugMenu_k__BackingField").SetMethod != null,
    "native readonly-property/backing-field contract changed");
string targetDirectory = args.Length > 2 ? args[2] : Path.Combine(gameDir, "Mods");
var pcPath = Path.Combine(targetDirectory, "PCExpansion.dll");
if (File.Exists(pcPath))
{
    using var pc = AssemblyDefinition.ReadAssembly(pcPath);
    Check(pc.Name.Version == new Version(2, 6, 0, 0), "local PC Expansion version changed");
    var codec = pc.MainModule.Types.Single(t => t.FullName == "PCExpansion.WorkroomItemCodec");
    var apply = codec.Methods.Single(m => m.Name == "ApplyScalars");
    Check(apply.IsStatic && apply.ReturnType.FullName == "System.Void" && apply.Parameters.Count == 2 &&
        apply.Parameters[0].ParameterType.FullName == "System.Object" &&
        apply.Parameters[1].ParameterType.FullName == "System.Collections.Generic.Dictionary`2<System.String,System.Text.Json.JsonElement>",
        "PC Expansion restore signature changed");
    var equivalent = codec.Methods.Single(m => m.Name == "Equivalent");
    Check(equivalent.IsStatic && equivalent.ReturnType.FullName == "System.Boolean" && equivalent.Parameters.Count == 2 &&
        equivalent.Parameters.All(p => p.ParameterType.FullName == "PCExpansion.WorkroomItemCodec/Snapshot"),
        "PC Expansion full readback contract changed");
    var preserved = pc.MainModule.Types.Single(t => t.FullName == "PCExpansion.WorkroomMachineRepair")
        .Methods.Single(m => m.Name == "VerifyPreserved");
    Check(preserved.IsStatic && preserved.ReturnType.FullName == "System.Void" && preserved.Parameters.Count == 2 &&
        preserved.Parameters.All(p => p.ParameterType.FullName == "PCExpansion.WorkroomItemCodec/Node"),
        "PC Expansion repair readback contract changed");
    ScalarAliasChecks.RunPCExpansion(pcPath, gameDir, Check);
}
var brewingPath = Path.Combine(targetDirectory, "BrewingExpansion.dll");
if (File.Exists(brewingPath))
{
    using var brewing = AssemblyDefinition.ReadAssembly(brewingPath);
    var chargeCalls = brewing.MainModule.GetMemberReferences().OfType<MethodReference>()
        .Where(m => m.DeclaringType.FullName == "Il2Cpp.PowerHelper" && m.Name == "ChargeBattery").ToArray();
    Check(chargeCalls.Length > 0 && chargeCalls.All(m => m.Parameters.Select(p => p.ParameterType.FullName)
        .SequenceEqual(new[] { "Il2Cpp.GameItem", "System.Int32", "System.Boolean" })),
        "local Brewing Expansion still references an obsolete battery-charge overload");
    Console.WriteLine("Local Brewing Expansion already uses the current three-parameter battery API; no replacement installed.");
}
var shuPath = Path.Combine(targetDirectory, "PSItemPack_Shu.dll");
if (File.Exists(shuPath))
{
    using var shu = AssemblyDefinition.ReadAssembly(shuPath);
    var bubble = shu.MainModule.Types.Single(t => t.FullName == "ShuMerchant.ItemPack.ShuNianBubble");
    Check(!bubble.Methods.Any(m => m.Name == "PlaceIntoFurnace"), "local Shu package regained the old furnace insertion path");
    Console.WriteLine("Local Shu package has no legacy PlaceIntoFurnace target; old forced-player-delivery patch not installed.");
}
Console.WriteLine($"Nicokobo Compatibility Patches: {checks} checks passed.");
