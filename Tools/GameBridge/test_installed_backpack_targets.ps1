$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Add-Type -Path 'C:\Users\Etienne\.nuget\packages\coverlet.collector\3.1.2\build\netstandard1.0\Mono.Cecil.dll'
$path=Join-Path $root '..\..\7DaysToDie_Data\Managed\Assembly-CSharp.dll'
$a=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
try {
 $checks=@(
 @('EntityPlayer','UpdateBagpackSize','System.Void',0),
 @('EntityPlayer','CalcCurrentBackpackSize','System.Int32',1),
 @('XUiM_PlayerEquipment','EquipItem','ItemStack',1),
 @('XUiC_EquipmentStack','SwapItem','System.Void',0),
 @('ItemClass','GetItemDescriptionKey','System.String',0))
 foreach($c in $checks){
  $t=$a.MainModule.Types | Where-Object Name -eq $c[0]
  $m=@($t.Methods | Where-Object {$_.Name -eq $c[1] -and $_.Parameters.Count -eq $c[3] -and $_.ReturnType.FullName -eq $c[2]})
  if($m.Count -ne 1){throw "Installed target mismatch $($c[0]).$($c[1])"}
 }
 $slots=$a.MainModule.Types | Where-Object Name -eq 'EquipmentSlots'
 if(@($slots.Fields | Where-Object {$_.Name -eq 'Backpack' -and $_.Constant -eq 12}).Count -ne 1){throw 'Native backpack enum changed'}
 $installer=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Progression/RebirthSurvivorProgressionInstaller.cs'))
 foreach($name in @('RebirthSurvivorBackpackCapacityPatches','RebirthNativeBackpackEquipmentPatches')){
  if($installer -notmatch ('PatchClassOnce\([^;]*typeof\('+[regex]::Escape($name)+'\)')){throw "Missing explicit registration: $name"}
 }
 'PASS installed native backpack targets and explicit source registration; no Harmony runtime/game execution.'
} finally {$a.Dispose()}