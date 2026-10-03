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
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'get_contentWindow' 'Il2Cpp.PixelWindow' @()
    Assert-AdapterMethod 'Il2Cpp.PixelWindow' 'get_childElement' 'Il2Cpp.PixelElement' @()
    Assert-AdapterMethod 'Il2Cpp.GridPixelElement' 'get_gridWidth' 'System.Int32' @()
    Assert-AdapterMethod 'Il2Cpp.GridPixelElement' 'get_gridHeight' 'System.Int32' @()
    Assert-AdapterMethod 'Il2Cpp.GridPixelElement' 'AttachPos' 'System.Boolean' @('Il2Cpp.PixelElement', 'System.Int32', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.GridPixelElement' 'GetElement' 'Il2Cpp.PixelElement' @('System.Int32', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.TagElement' 'SetText' 'Il2Cpp.TagElement' @('System.String', 'System.Int32', 'Il2Cpp.RenderHandler/ColorPalette')
    Assert-AdapterMethod 'Il2Cpp.LocHelper' 'GetLocalizedMechanic' 'System.String' @('System.String', 'Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1<Il2CppSystem.Object>')
    Assert-AdapterMethod 'Il2Cpp.GameSlotInventory' 'SetBackgroundFadeSprite' 'Il2Cpp.GameSlotInventory' @('System.String', 'System.String', 'System.Single', 'System.Single')
    $adapterStringList = 'Il2CppSystem.Collections.Generic.List`1<System.String>'
    Assert-AdapterMethod 'Il2Cpp.ContainerHelper' 'AllowOnlyTaggedItem' 'System.Void' @('Il2Cpp.GameInventory', 'System.String', 'System.Boolean', 'System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.ContainerHelper' 'InitContainerItem' 'System.Void' @('Il2Cpp.GameInventory', 'Il2Cpp.GameItem', $adapterStringList, $adapterStringList)
    Assert-AdapterMethod 'Il2Cpp.StoreClientList' 'PlaceInventorInventory' 'System.Void' @('System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.StoreClientList' 'PlaceSupplierInventory' 'System.Void' @()
    foreach ($adapterMethod in @('_CreateMiner_b__38_0', '_CreateJunker_b__21_0',
        '_CreateScrapper_b__35_0', '_CreateLowerLevelRareMerchant_b__64_0', '_CreateInventorStorage_b__31_0',
        '_CreateThief_b__47_0', '_CreatePettyThief_b__48_0', '_CreateBrokeUpperLevel_b__69_0',
        '_CreateFoodThief_b__49_0',
        '_CreateShadyPharmacist_b__34_0', '_CreateScavBlood_b__58_0', '_CreateRareLowerLevelChemist_b__65_0')) {
        Assert-AdapterMethod 'Il2Cpp.StoreClientList/__c' $adapterMethod 'System.Void' @()
    }
    Assert-AdapterMethod 'Il2Cpp.StoreClientList/__c__DisplayClass22_0' '_CreateJunkerSellOnly_b__0' 'System.Void' @()
    Assert-AdapterMethod 'Il2Cpp.StoreClientList/__c__DisplayClass37_0' '_CreateLowerLevelChemist_b__0' 'System.Void' @()
    Assert-AdapterMethod 'Il2Cpp.StoreClientList/__c__DisplayClass41_0' '_CreateScavGeneral_b__0' 'System.Void' @()
    Assert-AdapterMethod 'Il2Cpp.StoreClientList/__c__DisplayClass42_0' '_CreateScavCrate_b__0' 'System.Void' @()
    foreach ($adapterMethod in @('_CreateScavHaul_b__8_0', '_CreateSalvagePilot_b__14_0', '_CreateOldScav_b__17_0',
        '_CreateConspiracyClient_b__22_0', '_CreatePeatClient_b__21_0', '_CreateNurse1_b__5_0')) {
        Assert-AdapterMethod 'Il2Cpp.StoreClientListMinor/__c' $adapterMethod 'System.Void' @()
    }
    foreach ($adapterMethod in @('_CreateRevRaider_b__0_0', '_CreateRevQuartermaster_b__1_0')) {
        Assert-AdapterMethod 'Il2Cpp.StoreClientListRev/__c' $adapterMethod 'System.Void' @()
    }
    foreach ($adapterMethod in @('_CreateRetiredWinemaker_b__4_0',
        '_CreateRetiredJunker_b__5_0', '_CreateRetiredChemist_b__7_0')) {
        Assert-AdapterMethod 'Il2Cpp.StoreClientListSpec/__c' $adapterMethod 'System.Void' @()
    }
    foreach ($adapterMethod in @('_CreateFoodSurplusClient_b__0_0', '_CreateFoodSurplusClient_b__0_1',
        '_CreateMedicalSurplusClient_b__1_0', '_CreateMedicalSurplusClient_b__1_1',
        '_CreateMaterialSurplusClient_b__4_0', '_CreateMaterialSurplusClient_b__4_1')) {
        Assert-AdapterMethod 'Il2Cpp.StoreClientListSurplus/__c' $adapterMethod 'System.Void' @()
    }
    Assert-AdapterMethod 'Il2Cpp.StoreClientListEvent/__c' '_CreateScavengerHouseholdClient_b__8_0' 'System.Void' @()
    Assert-AdapterMethod 'Il2Cpp.RNG' 'GetRandomDouble' 'System.Double' @('System.Double', 'System.Double')
    Assert-AdapterMethod 'Il2Cpp.PlayerStore' 'AddDirectSellingItemToTable' 'System.Void' @(
        'Il2Cpp.GameItem', 'System.Boolean', 'System.Boolean', 'System.Boolean', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.PlayerStore' 'OnItemBought' 'System.Void' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.PlayerStore' 'FindAllItem' $adapterItemList @('System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.GeneralHelper' 'IsItemOwned' 'System.Boolean' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.GameItemElement' 'GetTooltipBasic' 'Il2Cpp.RichTextBuilder' @()
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'TryFindOneValidInventorySlot' 'Il2Cpp.SlotMarker' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.WaterHelper' 'GetFreeCapacity' 'System.Int32' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'GetRefreshedValue' 'System.Int64' @()
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'SyncModifiedState' 'System.Void' @()
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'AccumulateFeatureStages' 'System.Void' @('System.Int64&', 'System.Int64&', 'System.Double&', 'System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'ComposeStagedValue' 'System.Int64' @('System.Int64', 'System.Int64', 'System.Int64', 'System.Double', 'System.Int32', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.GraphUtils' 'FindAllChildrenType' 'Il2CppSystem.Collections.Generic.List`1<T>' @('Il2Cpp.GraphNodeStorage', 'Il2CppSystem.Func`2<T,System.Boolean>', 'Il2CppSystem.Func`2<T,System.Boolean>')
    Assert-AdapterMethod 'Il2Cpp.WaterHelper' 'TransferLiquid' 'System.Void' @('Il2Cpp.GameItem', 'Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.WaterHelper' 'AddLiquid' 'System.Void' @('Il2Cpp.GameItem', 'System.String', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.WaterHelper' 'EmptyContainer' 'System.Void' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.WaterHelper' 'GetContainerPrice' 'System.Int32' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.WaterFeatureHelper' 'UpdateWaterFeatureFake' 'System.Void' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.WaterFeatureHelper' 'InitWaterFeature' 'System.Void' @('Il2Cpp.GameItem', 'System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.ModuleHelper' 'CreateModuleTooltip' 'System.Void' @('Il2Cpp.RichTextBuilder', 'Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.MachineryHelper' 'GetMachinePowerUsage' 'System.Int32' @('Il2Cpp.GameItem')
    foreach ($adapterMethod in @('CanDrawPowerSource', 'DrawPowerSource')) {
        Assert-AdapterMethod 'Il2Cpp.PowerHelper' $adapterMethod 'System.Boolean' @('Il2Cpp.GameItem', 'System.Int32')
    }
    Assert-AdapterMethod 'Il2Cpp.PowerHelper' 'SetPowerSourceAt' 'System.Void' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.PowerHelper' 'TryRemoveEnergy' 'System.Int32' @('Il2Cpp.GameItem', 'System.Int32', 'System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.PowerHelper' 'GetAvailableEnergyFromItem' 'System.Int32' @('Il2Cpp.GameItem', 'System.Boolean')
    # Built-in achievement provider and bounded, no-stacking native reward placement.
    foreach ($adapterMethod in @('StartNewGame', 'InitialSave', 'SaveGame')) {
        Assert-AdapterMethod 'Il2Cpp.PlayerStore' $adapterMethod 'System.Void' @()
    }
    Assert-AdapterMethod 'Il2Cpp.PlayerStore' 'ExecuteGameOver' 'System.Void' @('System.String')
    Assert-AdapterMethod 'Il2Cpp.StoreClient' 'SetBudget' 'System.Void' @('System.Int32')
    Assert-AdapterMethod 'Il2Cpp.StoreClient' 'SetBudget' 'System.Void' @('System.Int32', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.StoreClient' 'SetClientBudget' 'System.Void' @('System.Int32', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.StoreClient' 'GetBudget' 'System.Int32' @()
    Assert-AdapterMethod 'Il2Cpp.StoreClient' 'OverrideBudget' 'System.Void' @('System.Int32')
    Assert-AdapterMethod 'Il2Cpp.StoreClientInstance' 'CreateClientInstance' 'Il2Cpp.StoreClientInstance' @('Il2Cpp.StoreClient')
    foreach ($adapterMethod in @('GetTotalEstimatedValue', 'GetCurrentStoreAttractiveness')) {
        Assert-AdapterMethod 'Il2Cpp.PlayerStore' $adapterMethod 'System.Int32' @()
    }
    Assert-AdapterMethod 'Il2Cpp.ItemMouseDoubleClickHandler' 'DoubleClickAction' 'System.Void' @('Il2Cpp.GameItem', 'UnityEngine.Vector2')
    Assert-AdapterMethod 'Il2Cpp.InputActionManager' 'Update' 'System.Void' @()
    Assert-AdapterMethod 'Il2Cpp.StoreUIManager' 'CheckRaycast' 'System.Void' @()
    Assert-AdapterMethod 'Il2Cpp.StoreUIManager' 'ResolveEscape' 'System.Boolean' @()
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'GetTagReadonly' 'Il2Cpp.TagState' @('System.String')
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'ModifyTag' 'Il2Cpp.GameItem' @('System.String', 'Il2CppSystem.Action`1<Il2Cpp.TagState>', 'System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'SetUnitCount' 'Il2Cpp.GameItem' @('System.Int32')
    foreach ($adapterMethod in @('EnableTag', 'DisableTag')) {
        Assert-AdapterMethod 'Il2Cpp.GameItem' $adapterMethod 'System.Boolean' @('System.String', 'System.Boolean')
    }
    Assert-AdapterMethod 'Il2Cpp.ItemMouseDragHandler' 'get_IsDraggingItem' 'System.Boolean' @()
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'FindItemFeatureByID' 'Il2Cpp.ItemFeature' @('System.String')
    Assert-AdapterMethod 'Il2Cpp.GameItem' 'SetShape' 'Il2Cpp.GameItem' @('Il2Cpp.GridShape')
    foreach ($adapterTypeName in @('GameInventory', 'GameGridInventory', 'GameSlotInventory',
        'GameGridScrollableInventory', 'GameCharacterRaidInventory')) {
        foreach ($adapterMethod in @('UncheckedAccept', 'Expel')) {
            Assert-AdapterMethod "Il2Cpp.$adapterTypeName" $adapterMethod 'System.Boolean' @('Il2Cpp.GameItem')
        }
    }
    Assert-AdapterMethod 'Il2Cpp.GameInventory' 'TryInventorySlot' 'Il2Cpp.SlotMarker' @('Il2Cpp.GameItem', 'System.Int32', 'Il2Cpp.GridShape', 'Il2Cpp.TagSystem')
    Assert-AdapterMethod 'Il2Cpp.GameGridInventory' 'TryFindOneValidInventorySlot' 'Il2Cpp.SlotMarker' @('Il2Cpp.GameItem', 'System.Boolean')
    Assert-AdapterMethod 'Il2Cpp.SlotMarker' 'IsValid' 'System.Boolean' @()
    Assert-AdapterMethod 'Il2Cpp.SlotMarker' 'TryAcceptOnce' 'System.Int32' @('System.Int32')
    Assert-AdapterMethod 'Il2Cpp.GridShape' 'Clone' 'Il2Cpp.GridShape' @()
    Assert-AdapterMethod 'Il2Cpp.GridShape' 'Get' 'System.Byte' @('System.Int32', 'System.Int32')
    Assert-AdapterMethod 'Il2Cpp.WaterHelper' 'IsFull' 'System.Boolean' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.WaterHelper' 'GetWaterPurity' 'System.Int32' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.WaterHelper' 'GetTotalVolume' 'System.Int32' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.WaterFeatureHelper' 'GetPurityArrayIndex' 'System.Int32' @('System.Int32')
    Assert-AdapterMethod 'Il2Cpp.HusbandryHelper' 'IsAnimalDead' 'System.Boolean' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.IngotPurityHelper' 'GetPurity' 'System.String' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.WineHelper' 'IsFinishedWine' 'System.Boolean' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.WineHelper' 'GetWineQualityTier' 'System.Int32' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.AgableHelper' 'GetAge' 'System.Int32' @('Il2Cpp.GameItem')
    Assert-AdapterMethod 'Il2Cpp.StoreReputation' 'GetReputationExact' 'System.Double' @()
    Assert-AdapterMethod 'Il2Cpp.StoreReputation' 'IsFactionValid' 'System.Boolean' @('System.String')
    Write-Host "Adapter metadata contracts passed: $adapterChecked. Native behavior remains separately verified." -ForegroundColor Green
} finally { $adapterAssembly.Dispose() }
