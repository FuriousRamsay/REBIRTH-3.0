$ErrorActionPreference='Stop'
$game=Resolve-Path (Join-Path $PSScriptRoot '../../../..')
[void][Reflection.Assembly]::LoadFrom((Join-Path $game '7DaysToDie_Data/Managed/UnityEngine.CoreModule.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $game '7DaysToDie_Data/Managed/Assembly-CSharp.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $game 'Mods/0_TFP_Harmony/0Harmony.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '../../RebirthUtils.dll'))
function Check($c,$m){if(-not $c){throw $m}}
$h=New-Object HarmonyLib.Harmony 'rebirth.tests.cooking.merge'
foreach($name in @('RebirthCookingStackCompatibility','RebirthCookingMergeCompatibility','RebirthCookingPartialMergeScope','RebirthCookingPartialMergeCheck','RebirthCookingTransferCountCheck','RebirthCookingSortCompatibility')) {
 $type=[RebirthCookingItemStats].Assembly.GetType($name)
 try {[void]$h.CreateClassProcessor($type).Patch()} catch {Write-Output $name;Write-Output $_.Exception.ToString();throw}
}
function Bare($t){[Runtime.Serialization.FormatterServices]::GetUninitializedObject($t)}
$ic=Bare ([ItemClass]);[ItemClass].GetField('Stacknumber').SetValue($ic,(New-Object 'DataItem[int]' 100))
$classes=New-Object 'ItemClass[]' 20001
$classes[20000]=$ic
[ItemClass].GetField('list').SetValue($null,$classes)
[ItemClass].GetField('MaxStackSizeModifier').SetValue($null,[single]1)
function Meal($n,$count){$v=Bare ([ItemValue]);$v.type=20000;$v.SetMetadata('rebirth.cooking.nutrition',[single]$n);return New-Object ItemStack $v,$count}
$bag=New-Object Bag 2
$bag.SetSlot(0,(Meal 2 3))
$incoming=Meal 1.7 1
$result=$bag.TryStackItem(0,$incoming)
Check (-not $result.Item1 -and $incoming.count -eq 1 -and $bag.GetSlots()[0].count -eq 3) 'Native Bag merged unequal cooked stats'
$incoming=Meal 2 1
$result=$bag.TryStackItem(0,$incoming)
Check ($result.Item2 -and $incoming.count -eq 0 -and $bag.GetSlots()[0].count -eq 4) 'Native Bag cannot merge identical cooked stats'
$incoming=Meal 1.7 1
[ItemStack[]]$slots=@((Meal 2 3),(Meal 2 3))
Check (-not [XUiM_PlayerInventory]::TryStackItem(0,$incoming,$slots)) 'Native inventory array merged unequal food'
Check ($incoming.count -eq 1 -and $slots[0].count -eq 3) 'Native inventory array lost food'
Check ($slots[0].StackTransferCount($incoming) -eq 0) 'Drag transfer count permits unequal food'
Write-Output 'Native merge regression passed: Harmony patches install, Bag accepts identical stats, rejects unequal stats, inventory-array transfers and drag counts preserve food.'
# Test combining separately from localization-dependent display ordering.
Add-Type -ReferencedAssemblies (Join-Path $game '7DaysToDie_Data/Managed/Assembly-CSharp.dll') -TypeDefinition @"
public static class CookingSortTestOrder {
 public static bool Prefix(ItemStack[] _stacks, ref ItemStack[] __result) { __result=_stacks; return false; }
}
"@
$sort=[StackSortUtil].GetMethod('SortStacks')
$prefix=New-Object HarmonyLib.HarmonyMethod ([CookingSortTestOrder].GetMethod('Prefix'))
[void]$h.Patch($sort,$prefix,$null,$null,$null)
[ItemStack[]]$meals=@((Meal 2 3),(Meal 1.7 1),(Meal 2 2))
$sorted=[StackSortUtil]::CombineAndSortStacks($meals,0,$null)
Check ($sorted[0].count -eq 5 -and $sorted[1].count -eq 1 -and $sorted[2].IsEmpty()) 'Sorting combined unequal cooked stats or failed to combine identical stats'
[single]$nutrition=0
Check ($sorted[1].itemValue.TryGetMetadata('rebirth.cooking.nutrition',[ref]$nutrition) -and [Math]::Abs($nutrition-1.7) -lt .001) 'Sorting lost cooked metadata'
Write-Output 'Native sorting regression passed: unequal stats stay separate, identical stats combine without losing metadata.'
