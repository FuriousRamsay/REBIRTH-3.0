param([switch]$LinearNegativeControl)
$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
if((Get-FileHash "$root/Scripts/UI/XUiC_RebirthMapArea.cs").Hash-ne'E7E38E323147C31743B06B1B2FBEB595CC79FC91490D0A18D052C435479DCE17'-or(Get-FileHash "$root/Scripts/UI/RebirthMapOverview.cs").Hash-ne'6F6154485969DDEED6575427F08DECEEE230FBB51D035B5BE5B7CA71648820C3'){throw 'Current input correction dependencies changed; review baseline before testing'}
$before=Get-Content "$root/_Documentation/MapZoomSlider_20261028/windows.xml.baseline" -Raw
$after=Get-Content "$root/Config/XUi_InGame/windows.xml" -Raw
$line='  <include filename="../_Map/zoom_slider.xml" />'
if($after.Replace($line+"`r`n",'').Replace($line+"`n",'').Replace("`r`n","`n")-cne$before.Replace("`r`n","`n")){throw 'Aggregate XML differs beyond one include'}
[xml]$xml=Get-Content "$root/Config/_Map/zoom_slider.xml"
$append=$xml.configs.append
if($append.xpath-cne"/windows/window[@name='mapArea' and @controller='RebirthMapArea, RebirthUtils']"){throw 'Slider not scoped to custom full map window'}
$control=$append.rect
if($control.controller-cne'RebirthMapZoomSlider, RebirthUtils'-or$control.width-ne348-or$control.height-ne76){throw 'Controller/layout contract'}
$track=$control.sprite|Where-Object name -eq zoomTrack
$background=$control.sprite|Where-Object name -eq zoomBackground
foreach($surface in @($track,$background,($control.sprite|Where-Object name -eq zoomThumb))){if($surface.on_press-ne'true'-or$surface.on_drag-ne'true'-or$surface.on_scroll-ne'true'){throw 'Overlay surface leaks pointer events'}}
if($track.color-ne'85,75,105,255'-or$control.filledsprite.color-ne'150,101,215,255'){throw 'POI slider style drift'}
$source=Get-Content "$root/Scripts/UI/XUiC_RebirthMapZoomSlider.cs" -Raw
$source=$source.Replace('using UnityEngine;','').Replace('using UnityEngine.Scripting;','')
if($LinearNegativeControl){$source=$source.Replace('return Mathf.Exp(Mathf.Lerp(Mathf.Log(.7f),Mathf.Log(maximum),fraction));','return Mathf.Lerp(.7f,maximum,fraction);')}
$projection=Get-Content "$PSScriptRoot/Projection.cs.txt" -Raw
$fixture=Get-Content "$PSScriptRoot/Checks.cs.txt" -Raw
Add-Type -TypeDefinition $fixture.Replace('/*ACTUAL*/',$source).Replace('/*PROJECTION*/',$projection)
[Checks]::Run()
