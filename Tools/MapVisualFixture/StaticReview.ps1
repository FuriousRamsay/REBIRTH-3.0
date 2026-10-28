$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path;$doc="$root/_Documentation/MapVisualFix_20261028"
$old=Get-Content "$doc/RebirthPoiMapPresentation.cs.baseline" -Raw;$new=Get-Content "$root/Scripts/Purge/Network/RebirthPoiMapPresentation.cs" -Raw
$start=$new.IndexOf('        if(__instance is XUiC_RebirthMapArea full && full.SuppressMapHover)');$end=$new.IndexOf('        var icon = __instance.closestMouseOverNavObject;',$start)
if($start-lt0-or$end-lt$start-or$new.Remove($start,$end-$start).Replace("`r`n","`n").TrimEnd()-cne$old.Replace("`r`n","`n").TrimEnd()){throw 'Purge diff extends beyond authorized postfix insertion'}
$old=Get-Content "$doc/XUiC_RebirthMapArea.cs.baseline" -Raw;$new=Get-Content "$root/Scripts/UI/XUiC_RebirthMapArea.cs" -Raw
foreach($pattern in @('public Vector3 LocalPointer\(Vector3 screen\)[^\r\n]*','(?s)internal static class RebirthWideMapPointer\s*\{.*?\r?\n\}','(?s)internal static class RebirthWideMapLocalPointer\s*\{.*?\r?\n\}')){if([regex]::Match($new,$pattern).Value-cne[regex]::Match($old,$pattern).Value){throw 'Unrelated pointer projection changed'}}
[xml]$xml=Get-Content "$root/Config/_Map/zoom_slider.xml" -Raw
$boundary=@($xml.configs.append|Where-Object xpath -match 'mapView').rect
if($boundary.name-ne'rebirthWorldBoundary'-or@($boundary.sprite).Count-ne8){throw 'Missing boundary mask/border XML'}
foreach($view in @($boundary)+@($boundary.sprite)){foreach($attr in @('on_press','on_drag','on_scroll','on_hover')){if($view.$attr-ne'false'){throw 'Boundary intercepts map input'}}}
foreach($view in $boundary.sprite|Where-Object name -match 'Outside'){if($view.color-ne'0,0,0,255'-or$view.globalopacitymod-ne'0'){throw 'Outside mask is not solid black'}}
Get-Content "$root/Tools/GameBridge/tests/full_map_visual_correction_20261028.json" -Raw|ConvertFrom-Json|Out-Null
$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die'
Add-Type -Path "$game/Mods/0_TFP_Harmony/Mono.Cecil.dll"
$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly("$game/7DaysToDie_Data/Managed/Assembly-CSharp.dll")
try{$type=$asm.MainModule.Types|Where-Object Name -eq XUiC_MapArea
foreach($name in @('UpdateWaypointSelection','updateMapObjectList','onMapPressed','OpenWaypointPopup','onMapDragged','DragMap','teleportPlayerOnMap','onMapScrolled')){if(@($type.Methods|Where-Object Name -eq $name).Count-ne1){throw "Ambiguous/missing native patch target $name"}}
$method=$type.Methods|Where-Object Name -eq onMapDragged;if($method.Parameters[1].ParameterType.Name-ne'EDragType'){throw 'Native drag argument changed'}
$world=$asm.MainModule.Types|Where-Object Name -eq World;$clamp=($world.Methods|Where-Object Name -eq ClampToValidWorldPos).Body.Instructions
if(-not($clamp|Where-Object {$_.Operand -and $_.Operand.ToString().Contains('GetWorldExtent')})){throw 'Native clamp no longer derives world extents'}
}finally{$asm.Dispose()}
'PASS current production Purge postfix-only insertion; pointer/camera projection unchanged; 8 noninteractive boundary sprites and solid-black exterior XML; native patch targets/drag signature/world-extents contract; scenario JSON parsed NOT RUN'
