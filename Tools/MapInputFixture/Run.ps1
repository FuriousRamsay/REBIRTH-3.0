param([switch]$LoadOnly,[switch]$NegativeZoomControl)
$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$baseline=Get-Content "$root/_Documentation/MapInputFix_20261028/XUiC_RebirthMapArea.cs.baseline" -Raw
$controller=Get-Content "$root/Scripts/UI/XUiC_RebirthMapArea.cs" -Raw
$restored=$controller.Replace('RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapResponsiveWheel));','').Replace('RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthMapTeleportBounds));','')
if($restored.Replace("`r`n","`n")-cne$baseline.Replace("`r`n","`n")){throw 'Pointer/layout/marker controller changed beyond the two registrations'}
$old=Get-Content "$root/_Documentation/MapInputFix_20261028/RebirthMapOverview.cs.baseline" -Raw
$actual=Get-Content "$root/Scripts/UI/RebirthMapOverview.cs" -Raw
if($old.Substring(0,$old.IndexOf('// Retain native wheel')).Replace("`r`n","`n")-cne$actual.Substring(0,$actual.IndexOf('// Native input routing')).Replace("`r`n","`n")){throw 'Overview data/render/cache changed beyond input correction'}
$actual=$actual.Replace('using System;','').Replace('using System.Collections.Generic;','').Replace('using System.Reflection;','').Replace('using System.Reflection.Emit;','').Replace('using HarmonyLib;','').Replace('using UnityEngine;','')
$baselineZoom=$old.Substring($old.IndexOf('// Retain native wheel')).Replace('class RebirthMapOverviewZoom','class RebirthMapOverviewZoomBaseline')
$actual+="`n"+$baselineZoom
$local=[regex]::Match($controller,'public Vector3 LocalPointer\(Vector3 screen\)[^\r\n]*').Value
$pointer=[regex]::Match($controller,'(?s)\[HarmonyPatch\(typeof\(XUiC_MapArea\),"screenPosToWorldPos"\)\]\s*internal static class RebirthWideMapPointer\s*\{.*?\r?\n\}').Value
if(!$local-or!$pointer){throw 'Actual projection/pointer source missing'}
$fixture=Get-Content "$PSScriptRoot/Checks.cs.txt" -Raw
$geometry=(Get-Content "$PSScriptRoot/Geometry.cs.txt" -Raw)+(Get-Content "$PSScriptRoot/NativeCases.cs.txt" -Raw)
Add-Type -TypeDefinition $fixture.Replace('/*ACTUAL*/',$actual).Replace('/*LOCALPOINTER*/',$local).Replace('/*POINTER*/',$pointer).Replace('/*GEOMETRY*/',$geometry)
if(!$LoadOnly){[Checks]::Run($false); & "$PSScriptRoot/NativeRuntime.ps1" -NegativeZoomControl:$NegativeZoomControl}
