param([Parameter(Mandatory = $true)][string]$GameDir)
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $GameDir 'MelonLoader\net6\Mono.Cecil.dll')
$inventoryPowerAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly(
    (Join-Path $GameDir 'MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll'))
$inventoryPowerChecked = 0
function Assert-InventoryPowerMethod([string]$Type, [string]$Name, [string]$Return, [string[]]$Arguments) {
    $inventoryPowerType = $inventoryPowerAssembly.MainModule.GetType($Type)
    if ($null -eq $inventoryPowerType) { throw "Missing inventory/power type: $Type" }
    $inventoryPowerMatches = @($inventoryPowerType.Methods | Where-Object {
        $_.Name -eq $Name -and $_.ReturnType.FullName -ceq $Return -and
        ($_.Parameters.ParameterType.FullName -join '|') -ceq ($Arguments -join '|')
    })
    if ($inventoryPowerMatches.Count -ne 1) { throw "Inventory/power contract mismatch: $Type.$Name($($Arguments -join ',')):$Return" }
    $inventoryPowerMethod = $inventoryPowerMatches[0]
    $inventoryPowerNativeFields = @($inventoryPowerMethod.Body.Instructions.Operand | Where-Object {
        $_ -is [Mono.Cecil.FieldReference] -and $_.Name.StartsWith('NativeMethodInfoPtr_', [StringComparison]::Ordinal)
    })
    # IndexOf(..., StringComparison) is used instead of Contains(..., StringComparison): the
    # two-argument Contains overload only exists on .NET Core, not on Windows PowerShell 5.1.
    if ($inventoryPowerMethod.IsAbstract -or @($inventoryPowerNativeFields | Where-Object {
        $_.Name.IndexOf('_Abstract_', [StringComparison]::Ordinal) -ge 0
    }).Count -ne 0) { throw "Inventory/power target is a native abstract wrapper: $Type.$Name" }
    $script:inventoryPowerChecked++
}
try {
    Assert-InventoryPowerMethod 'Il2Cpp.SaveManager' 'EncodeNodes' 'Il2CppSystem.Collections.Generic.List`1<Il2Cpp.SaveItemNode>' @('Il2Cpp.GraphNodeStorage')
    Assert-InventoryPowerMethod 'Il2Cpp.SaveManager' 'DecodeNodes' 'Il2Cpp.GameItem' @('Il2CppSystem.Collections.Generic.List`1<Il2Cpp.SaveItemNode>')
    Assert-InventoryPowerMethod 'Il2Cpp.GameGridInventory' '.ctor' 'System.Void' @('System.Int32', 'System.Int32')
    Assert-InventoryPowerMethod 'Il2Cpp.PixelWindow' '.ctor' 'System.Void' @('System.Boolean', 'System.String')
    Assert-InventoryPowerMethod 'Il2Cpp.PixelWindow' 'Attach' 'System.Boolean' @('Il2Cpp.PixelElement')
    Assert-InventoryPowerMethod 'Il2Cpp.GameItem' 'SetContentWindow' 'Il2Cpp.GameItem' @('Il2Cpp.PixelWindow')
    Assert-InventoryPowerMethod 'Il2Cpp.GameGridInventory' 'SetShape' 'Il2Cpp.GameGridInventory' @('System.Int32', 'System.Int32')
    Assert-InventoryPowerMethod 'Il2Cpp.GameGridInventory' 'UncheckedAccept' 'System.Boolean' @('Il2Cpp.GameItem')
    Assert-InventoryPowerMethod 'Il2Cpp.GameGridInventory' 'MayHaveValidInventorySlot' 'System.Boolean' @('Il2Cpp.GameItem')
    Assert-InventoryPowerMethod 'Il2Cpp.GameItem' 'MayRemove' 'System.Boolean' @()
    foreach ($inventoryPowerTypeName in @('GameGridInventory', 'GameGridScrollableInventory', 'GameSlotInventory')) {
        Assert-InventoryPowerMethod "Il2Cpp.$inventoryPowerTypeName" 'Expel' 'System.Boolean' @('Il2Cpp.GameItem')
    }
    Assert-InventoryPowerMethod 'Il2Cpp.GameItemElement' 'Destroy' 'System.Void' @()
    Assert-InventoryPowerMethod 'Il2Cpp.GameItem' 'GetIncompleteSlotInInventory' 'Il2Cpp.SlotMarker' @()
    Assert-InventoryPowerMethod 'Il2Cpp.GameInventory' 'TryInventorySlot' 'Il2Cpp.SlotMarker' @('Il2Cpp.GameItem', 'System.Int32', 'Il2Cpp.GridShape', 'Il2Cpp.TagSystem')
    Assert-InventoryPowerMethod 'Il2Cpp.GameGridInventory' 'TryInventorySlot' 'Il2Cpp.SlotMarker' @('Il2Cpp.GameItem', 'System.Int32', 'UnityEngine.Vector2', 'Il2Cpp.GridShape', 'Il2Cpp.TagSystem')
    Assert-InventoryPowerMethod 'Il2Cpp.GameItem' 'GetCurrentValue' 'System.Int64' @('System.Boolean', 'System.Boolean', 'System.Boolean', 'System.Boolean', 'System.Int64')
    Assert-InventoryPowerMethod 'Il2Cpp.GameItemElement' 'GetTooltipBasic' 'Il2Cpp.RichTextBuilder' @()
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'GetAvailableEnergyFromItem' 'System.Int32' @('Il2Cpp.GameItem', 'System.Boolean')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'CanDrawPowerSource' 'System.Boolean' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'DrawPowerSource' 'System.Boolean' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'ChargeBattery' 'System.Void' @('Il2Cpp.GameItem')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'ChargeBattery' 'System.Void' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'ChargeAllRechargable' 'System.Void' @('Il2CppSystem.Collections.Generic.List`1<Il2Cpp.GameItem>')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'SetPowerSourceAt' 'System.Void' @('Il2Cpp.GameItem', 'System.Int32')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'InitPowerSourceItem' 'System.Void' @('Il2Cpp.GameItem', 'System.Int32', 'System.Int32')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'IsPowerSourceFull' 'System.Boolean' @('Il2Cpp.GameItem')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'CreatePowerSourceItemTooltip' 'System.Void' @('Il2Cpp.RichTextBuilder', 'Il2Cpp.GameItem')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'TryRemoveEnergy' 'System.Int32' @('Il2Cpp.GameItem', 'System.Int32', 'System.Boolean')
    Assert-InventoryPowerMethod 'Il2Cpp.PowerHelper' 'GetAllPowerSourceFourniture' 'Il2CppSystem.Collections.Generic.List`1<Il2Cpp.GameItem>' @('System.Boolean')
    Assert-InventoryPowerMethod 'Il2Cpp.MachineFurnace/__c__DisplayClass0_0' '_Furnace_b__5' 'System.Void' @('Il2Cpp.GameItem', 'Il2Cpp.GameInventory', 'Il2Cpp.SlotMarker')
    Assert-InventoryPowerMethod 'Il2Cpp.PlayerStore' 'FindAllItem' 'Il2CppSystem.Collections.Generic.List`1<Il2Cpp.GameItem>' @('System.Boolean')
    Assert-InventoryPowerMethod 'Il2Cpp.PlayerStore' 'SaveGame' 'System.Void' @()
    Assert-InventoryPowerMethod 'Il2Cpp.PlayerStore' 'LoadGame' 'System.Void' @()
    # Native abstractness is carried by generated field names, not CLR flags.
    $inventoryPowerBaseDestroy = @($inventoryPowerAssembly.MainModule.GetType('Il2Cpp.GameItem').Methods | Where-Object {
        $_.Name -ceq 'Destroy' -and $_.Parameters.Count -eq 0
    })[0]
    if (@($inventoryPowerBaseDestroy.Body.Instructions.Operand | Where-Object {
        $_ -is [Mono.Cecil.FieldReference] -and $_.Name.IndexOf('_Abstract_', [StringComparison]::Ordinal) -ge 0
    }).Count -eq 0) { throw 'Expected native abstract GameItem.Destroy boundary changed; inspect current native implementations' }
    $inventoryPowerChecked++
    $inventoryPowerDestroyOwners = @($inventoryPowerAssembly.MainModule.Types | Where-Object {
        $inventoryPowerCursor = $_
        while ($null -ne $inventoryPowerCursor -and $inventoryPowerCursor.FullName -cne 'Il2Cpp.GameItem') {
            if ($null -eq $inventoryPowerCursor.BaseType) { $inventoryPowerCursor = $null; break }
            $inventoryPowerCursor = $inventoryPowerAssembly.MainModule.GetType($inventoryPowerCursor.BaseType.FullName)
        }
        $null -ne $inventoryPowerCursor
    } | Where-Object {
        $_.FullName -cne 'Il2Cpp.GameItem' -and @($_.Methods | Where-Object {
            $_.Name -ceq 'Destroy' -and $_.Parameters.Count -eq 0
        }).Count -ne 0
    })
    if (($inventoryPowerDestroyOwners.FullName -join '|') -cne 'Il2Cpp.GameItemElement') {
        throw "Concrete GameItem.Destroy override coverage changed: $($inventoryPowerDestroyOwners.FullName -join ','); update the required hook set"
    }
    $inventoryPowerChecked++
    Write-Host "Inventory/power metadata contracts passed: $inventoryPowerChecked. Native custody/save/power behavior remains separately verified."
} finally { $inventoryPowerAssembly.Dispose() }
