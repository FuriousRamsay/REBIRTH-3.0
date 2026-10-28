$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path;$doc="$root/_Documentation/MapFitFix_20261028"
[xml]$new=Get-Content "$root/Config/_Map/zoom_slider.xml" -Raw;[xml]$old=Get-Content "$doc/zoom_slider.xml.baseline" -Raw
if($new.configs.append[0].OuterXml-cne$old.configs.append[0].OuterXml){throw 'Slider XML changed'}
$boundary=$new.configs.append[1].rect;if(@($boundary.texture).Count-ne8-or@($boundary.ChildNodes|Where-Object Name -eq 'sprite').Count-ne0){throw 'Boundary still uses atlas sprites'}
foreach($view in $boundary.texture){if($view.type-ne'simple'-or$view.border-ne'0,0,0,0'-or$view.autounload-ne'false'){throw 'Texture not borderless/borrowed'};foreach($attr in @('on_press','on_drag','on_scroll','on_hover')){if($view.$attr-ne'false'){throw 'Boundary intercepts input'}}}
$controller=Get-Content "$root/Scripts/UI/XUiC_RebirthMapArea.cs" -Raw;$baseline=Get-Content "$doc/XUiC_RebirthMapArea.cs.baseline" -Raw
foreach($pattern in @('public Vector3 LocalPointer\(Vector3 screen\)[^\r\n]*','(?s)internal static class RebirthWideMapPointer\s*\{.*?\r?\n\}','(?s)internal static class RebirthWideMapLocalPointer\s*\{.*?\r?\n\}','(?s)internal static class RebirthWideMapRender\s*\{.*?\r?\n\}')){if([regex]::Match($controller,$pattern).Value-cne[regex]::Match($baseline,$pattern).Value){throw 'Camera/pointer/shader conversion changed'}}
foreach($name in @('XUiC_RebirthMapZoomSlider.cs','RebirthPoiMapPresentation.cs')){$path=if($name-like'*Poi*'){"Scripts/Purge/Network/$name"}else{"Scripts/UI/$name"};if((Get-FileHash "$root/$path").Hash-ne(Get-FileHash "$doc/$name.baseline").Hash){throw 'Existing slider/Purge source changed'}}
$game='C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die';Add-Type -Path "$game/Mods/0_TFP_Harmony/Mono.Cecil.dll";$asm=[Mono.Cecil.AssemblyDefinition]::ReadAssembly("$game/7DaysToDie_Data/Managed/Assembly-CSharp.dll")
try{$map=$asm.MainModule.Types|Where-Object Name -eq XUiC_MapArea;if(@($map.Methods|Where-Object Name -eq positionMap).Count-ne1){throw 'Native position target ambiguous'}
$view=$asm.MainModule.Types|Where-Object Name -eq XUiV_TextureBased;$setter=$view.Methods|Where-Object Name -eq set_Texture
if(-not($setter.Body.Instructions|Where-Object {$_.Operand -and $_.Operand.ToString().Contains('SetDirty')})){throw 'Native delayed setter contract changed'}
if($setter.Body.Instructions|Where-Object {$_.Operand -and $_.Operand.ToString().Contains('set_mainTexture')}){throw 'Native setter now commits immediately'}
$data=$view.Methods|Where-Object Name -eq updateData;if(!$data.IsPublic-or-not($data.Body.Instructions|Where-Object {$_.Operand -and $_.Operand.ToString().Contains('set_mainTexture')})){throw 'Native commit contract changed'}
$drag=$map.Methods|Where-Object Name -eq DragMap;if(-not($drag.Body.Instructions|Where-Object {$_.Operand -and $_.Operand.ToString().Contains('updateMapForScroll')})){throw 'Native incremental scroll path changed'}
}finally{$asm.Dispose()}
'PASS borderless borrowed texture XML and unchanged slider layout; original pointer/camera/shader conversions unchanged; slider and Purge guards byte-identical; native delayed Texture setter/public updateData commit and incremental DragMap scroll contracts verified'

