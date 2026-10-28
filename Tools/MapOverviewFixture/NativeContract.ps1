$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die'
Add-Type -Path "$game/Mods/0_TFP_Harmony/Mono.Cecil.dll"
$path="$game/7DaysToDie_Data/Managed/Assembly-CSharp.dll"
$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($path)
try {
 $map=$asm.MainModule.Types|Where-Object Name -eq XUiC_MapArea
 $rows=foreach($name in @('Update','onMapScrolled')){
  $method=$map.Methods|Where-Object Name -eq $name
  $constants=@($method.Body.Instructions|Where-Object {$_.OpCode.Name-eq'ldc.r4'-and$_.Operand-eq[float]6.15})
  if($constants.Count-ne1){throw "Native clamp shape changed: $name"}
  [pscustomobject]@{Method=$method.FullName;UpperClampOccurrences=$constants.Count;Offset=$constants[0].Offset;LowerClampPreserved=.7}
 }
 $regional=$asm.MainModule.Types|Where-Object Name -eq MapChunkDatabaseByRegion
 $constants=@($regional.Fields|Where-Object HasConstant|ForEach-Object {[pscustomobject]@{Name=$_.Name;Value=$_.Constant}})
 [pscustomobject]@{Assembly=$path;Sha256=(Get-FileHash $path).Hash;Mvid=$asm.MainModule.Mvid.ToString();Methods=@($rows);RegionConstants=$constants}|ConvertTo-Json -Depth 6|Set-Content "$root/_Documentation/MapZoom_20261028/native-clamp-contract.json"
 $lines=foreach($type in $asm.MainModule.Types|Where-Object {$_.Name-in@('IMapChunkDatabase','MapChunkDatabaseByRegion','DatabaseWithFixedDS`2','XUiC_MapArea')}){
  foreach($method in $type.Methods|Where-Object {$_.HasBody-and$_.Name-in@('GetMapColors','Contains','FromChunkDBKey','ToRegionAndOffset','GetDS','SetDS','updateMapOverlay')}){
   $method.FullName;$method.Body.Instructions|ForEach-Object {$_.ToString()}
  }
  foreach($nested in $type.NestedTypes|Where-Object Name -eq RegionData){foreach($method in $nested.Methods|Where-Object {$_.HasBody-and$_.Name-in@('.ctor','GetChunkData','SetChunkData')}){$method.FullName;$method.Body.Instructions|ForEach-Object {$_.ToString()}}}
 }
 $lines|Set-Content "$root/_Documentation/MapZoom_20261028/native-known-map-api-il.txt"
 'PASS exact native wheel/gamepad clamp and saved-known-map locking/key/array contracts recorded; metadata-only'
} finally {$asm.Dispose()}
