$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$source=(Get-Content "$root/Scripts/UI/XUiC_RebirthMapZoomSlider.cs" -Raw).Replace('using UnityEngine;','').Replace('using UnityEngine.Scripting;','')
$projection=Get-Content "$PSScriptRoot/Projection.cs.txt" -Raw
$fixture=Get-Content "$PSScriptRoot/SliderChecks.cs.txt" -Raw
Add-Type -TypeDefinition $fixture.Replace('/*ACTUAL*/',$source).Replace('/*PROJECTION*/',$projection)
[Checks]::Run()
