param([string]$GameDir = 'F:\SteamLibrary\steamapps\common\Probably Stolen Demo')
$ErrorActionPreference = 'Stop'
# Metadata inspection never initializes IL2CPP classes or invokes game methods.
Add-Type -Path (Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll')
$adapterAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly(
    (Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))
$adapterChecked = 0
function Assert-AdapterMethod([string]$Type, [string]$Name, [string]$Return, [string[]]$Arguments) {
    $adapterType = $adapterAssembly.MainModule.GetType($Type)
    if ($null -eq $adapterType) { throw "Missing adapter type: $Type" }
    $adapterMatches = @($adapterType.Methods | Where-Object {
        $_.Name -eq $Name -and $_.ReturnType.FullName -eq $Return -and
        ($_.Parameters.ParameterType.FullName -join '|') -ceq ($Arguments -join '|')
    })
    if ($adapterMatches.Count -ne 1) { throw "Adapter contract mismatch: $Type.$Name($($Arguments -join ',')):$Return" }
    $script:adapterChecked++
}
try {
    foreach ($adapterDirectory in @('MiscItemDirectory', 'ModuleDirectory', 'AmenitiesItemDirectory')) {
        Assert-AdapterMethod "Il2Cpp.$adapterDirectory" 'InitDirectory' 'System.Void' @()
    }
    foreach ($adapterMethod in @('LoadGame', 'EndNight', 'EndDay')) {
        Assert-AdapterMethod 'Il2Cpp.PlayerStore' $adapterMethod 'System.Void' @()
    }
    foreach ($adapterMethod in @('FireOnHandlingNightlyServicesEarly', 'FireOnHandlingNightlyServicesLate', 'FireOnGoingSleepLate')) {
        Assert-AdapterMethod 'Il2Cpp.ModHook' $adapterMethod 'System.Void' @()
    }
    $adapterItemList = 'Il2CppSystem.Collections.Generic.List`1<Il2Cpp.GameItem>'
    Assert-AdapterMethod 'Il2Cpp.ModuleHelper' 'ComputeModuleEffect' 'System.Void' @($adapterItemList, 'Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.ModuleEffectHelper' 'ComputeAllModuleTempStat' 'System.Void' @($adapterItemList)
    foreach ($adapterMethod in @('OnAddedModuleToGrid', 'OnRemovedModuleFromGrid')) {
        Assert-AdapterMethod 'Il2Cpp.ModuleEffectHelper' $adapterMethod 'System.Void' @('Il2Cpp.GameItem', 'Il2Cpp.GameItem', $adapterItemList)
    }
    Assert-AdapterMethod 'Il2Cpp.ModuleEffectHelper' 'OnUsedLearningAlgo' 'System.Void' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.ModuleEffectHelper' 'ActivateOnUseAction' 'System.Void' @('Il2Cpp.GameItem', 'Il2Cpp.GameInventory', 'Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.ModuleEffectHelper' 'InitRandomEffect' 'System.Void' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.MachineHelper' 'SetupModuleBay' 'System.Void' @('Il2Cpp.GameInventory', 'Il2Cpp.GameItem', 'Il2CppSystem.Action', 'Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray')
    Assert-AdapterMethod 'Il2Cpp.MachineHelper' 'GetModuleInv' 'Il2Cpp.GameGridInventory' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.StoreClientList' 'PlaceInventorInventory' 'System.Void' @('System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.PlayerStore' 'OnItemBought' 'System.Void' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.PlayerStore' 'FindAllItem' $adapterItemList @('System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.GeneralHelper' 'IsItemOwned' 'System.Boolean' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.GameItemElement' 'GetTooltipBasic' 'Il2Cpp.RichTextBuilder' @()
    Assert-AdapterMethod 'Il2Cpp.ModuleHelper' 'CreateModuleTooltip' 'System.Void' @('Il2Cpp.RichTextBuilder', 'Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.MachineFurnace/__c__DisplayClass0_0' '_Furnace_b__1' 'System.Boolean' @('Il2Cpp.GameItem', 'Il2Cpp.GameInventory')
    Assert-AdapterMethod 'Il2Cpp.MachineFurnace/__c__DisplayClass0_0' '_Furnace_b__5' 'System.Void' @('Il2Cpp.GameItem', 'Il2Cpp.GameInventory', 'Il2Cpp.SlotMarker')
    Assert-AdapterMethod 'Il2Cpp.MachineryHelper' 'GetMachinePowerUsage' 'System.Int32' @('Il2Cpp.GameItem')
    foreach ($adapterMethod in @('CanDrawPowerSource', 'DrawPowerSource')) {
        Assert-AdapterMethod 'Il2Cpp.PowerHelper' $adapterMethod 'System.Boolean' @('Il2Cpp.GameItem', 'System.Int32')
    }
    Assert-AdapterMethod 'Il2Cpp.PowerHelper' 'SetPowerSourceAt' 'System.Void' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.PowerHelper' 'TryRemoveEnergy' 'System.Int32' @('Il2Cpp.GameItem', 'System.Int32', 'System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.PowerHelper' 'GetAvailableEnergyFromItem' 'System.Int32' @('Il2Cpp.GameItem', 'System.Boolean')
    Write-Host "Adapter metadata contracts passed: $adapterChecked. Native behavior remains separately verified." -ForegroundColor Green
} finally { $adapterAssembly.Dispose() }
