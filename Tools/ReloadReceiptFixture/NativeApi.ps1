$ErrorActionPreference='Stop'
$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die'
$out=Join-Path $PSScriptRoot '../../_Documentation/ReloadReceipt_20261029'
Add-Type -Path "$game/Mods/0_TFP_Harmony/Mono.Cecil.dll"
$path="$game/7DaysToDie_Data/Managed/Assembly-CSharp.dll"
$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
try {
 $t=$asm.MainModule.Types|Where-Object Name -eq ItemActionRanged
 $expected=@{ReloadGun='ItemActionData';CompleteReload='ItemActionRanged/ItemActionDataRanged';CancelReload='ItemActionData,System.Boolean';StopHolding='ItemActionData'}
 $abi=@();$lines=@()
 foreach($name in $expected.Keys){
  $methods=@($t.Methods|Where-Object {$_.Name-eq$name-and(($_.Parameters.ParameterType.FullName)-join',')-eq$expected[$name]})
  if($methods.Count-ne1-or$methods[0].ReturnType.FullName-ne'System.Void'){throw "Native ABI changed: $name"}
  $m=$methods[0];$abi+=$m.FullName;$lines+=$m.FullName;$lines+=@($m.Body.Instructions|ForEach-Object ToString)
 }
 $lines|Set-Content "$out/native-il.txt"
 $extra=@()
 foreach($name in @('ItemInventoryData','Inventory','Hand','AnimatorRangedReloadState','EntityAlive')){
  $type=$asm.MainModule.Types|Where-Object Name -eq $name
  foreach($m in ($type.Methods|Where-Object Name -in @('get_itemValue','get_ItemValue','get_Held','get_holdingEntity','get_holdingItemData','get_holdingItemItemValue','OnStateEnter','OnStateExit','OnReloadStart'))){$extra+=$m.FullName;$extra+=@($m.Body.Instructions|ForEach-Object ToString)}
 }
 $extra|Set-Content "$out/native-il-extra.txt"
 $callers=@()
 foreach($type in $asm.MainModule.Types){foreach($m in $type.Methods){if(!$m.HasBody){continue};foreach($i in $m.Body.Instructions){if($i.Operand-is[Mono.Cecil.MethodReference]-and$i.Operand.DeclaringType.FullName-eq'ItemActionRanged'-and$i.Operand.Name-eq'CompleteReload'){$callers+=$m.FullName}}}}
 @{status='PASS';sha256=(Get-FileHash $path).Hash;signatures=$abi;completeReloadCallers=$callers;scope='Native metadata and IL only; no native code execution'}|ConvertTo-Json -Depth 4|Set-Content "$out/native-api.json"
 'PASS exact native b9 reload/cancel/stop ABI; metadata IL captured'
} finally {$asm.Dispose()}
