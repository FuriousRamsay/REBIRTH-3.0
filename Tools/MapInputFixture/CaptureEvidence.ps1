$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$out="$root/_Documentation/MapInputFix_20261028";$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die'
Add-Type -Path "$game/Mods/0_TFP_Harmony/Mono.Cecil.dll"
$dll="$game/7DaysToDie_Data/Managed/Assembly-CSharp.dll";$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll)
try {
 $lines=foreach($spec in @(@('World','ClampToValidWorldPos'),@('PlayerMoveController','updateRespawn'),@('EntityPlayer','Teleport'),@('FlexibleCursor','GetScreenPosition'),@('SoftCursor','GetScreenPosition'),@('LocalPlayerUI','get_camera'),@('XUiC_MapArea','Init'),@('XUiC_MapArea','updateMapObject'),@('XUiC_MapArea','worldPosToScreenPos'))){
  $type=$asm.MainModule.Types|Where-Object Name -eq $spec[0];foreach($method in $type.Methods|Where-Object Name -eq $spec[1]){$method.FullName;$method.Body.Instructions|ForEach-Object {$_.ToString()}}
 }
 $lines|Set-Content "$out/native-coordinate-and-bounds-il.txt"
 $mvid=$asm.MainModule.Mvid.ToString()
 $bindings=foreach($method in ($asm.MainModule.Types|Where-Object Name -eq XUiC_MapArea).Methods|Where-Object {$_.Name-in@('onMapScrolled','teleportPlayerOnMap','Update')}){[pscustomobject]@{Method=$method.Name;Parameters=@($method.Parameters|ForEach-Object {$_.Index.ToString()+':'+$_.Name+':'+$_.ParameterType.FullName})}}
} finally {$asm.Dispose()}
$first="$game/7DaysToDie_Data/Managed/Assembly-CSharp-firstpass.dll";$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($first)
try {($asm.MainModule.Types|Where-Object Name -eq MouseLib).Methods|Where-Object Name -eq GetLocalMousePosition|ForEach-Object {$_.FullName;$_.Body.Instructions|ForEach-Object {$_.ToString()}}|Set-Content "$out/native-raw-mouse-il.txt"}finally{$asm.Dispose()}
$log="$env:APPDATA/7DaysToDie/logs/output_log_client__2026-10-28__02-19-44.txt"
Select-String -LiteralPath $log -Pattern 'GamePref.WorldGenSize|Trying to respawn at invalid world position|reason: Teleport, position: -4006'|ForEach-Object {$_.Line}|Set-Content "$out/observed-clamp-excerpt.txt"
[pscustomobject]@{NativeDll=(Get-FileHash $dll).Hash;Mvid=$mvid;Firstpass=(Get-FileHash $first).Hash;Log=$log;LogSha256=(Get-FileHash $log).Hash;LogBytes=(Get-Item $log).Length;ControllerBaseline=(Get-FileHash "$out/XUiC_RebirthMapArea.cs.baseline").Hash;HelperBaseline=(Get-FileHash "$out/RebirthMapOverview.cs.baseline").Hash;ControllerProposal=(Get-FileHash "$root/Scripts/UI/XUiC_RebirthMapArea.cs").Hash;HelperProposal=(Get-FileHash "$root/Scripts/UI/RebirthMapOverview.cs").Hash;HarmonyBindings=@($bindings)}|ConvertTo-Json -Depth 6|Set-Content "$out/evidence-hashes.json"
'PASS native metadata/current source hashes and exact logged clamp evidence captured read-only'
