param([switch]$DetachNegativeControl,[switch]$PostfixNegativeControl)
$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$overview=Get-Content "$root/Scripts/UI/RebirthMapOverview.cs" -Raw
$overview=$overview.Substring(0,$overview.IndexOf('// Native input routing'))
foreach($using in @('using System;','using System.Collections.Generic;','using System.Reflection;','using System.Reflection.Emit;','using HarmonyLib;','using UnityEngine;')){$overview=$overview.Replace($using,'')}
$presentation=(Get-Content "$root/Scripts/UI/RebirthMapPresentation.cs" -Raw).Replace('using HarmonyLib;','').Replace('using UnityEngine;','')
$presentation=$presentation.Replace('private int zoomFrame=-3,panFrame=-3;','public int events;private int zoomFrame=-3,panFrame=-3;').Replace('internal void Zoom(){zoomFrame=Time.frameCount;}','internal void Zoom(){events++;zoomFrame=Time.frameCount;}')
$purge=Get-Content "$root/Scripts/Purge/Network/RebirthPoiMapPresentation.cs" -Raw
$start=$purge.IndexOf('[HarmonyPatch(typeof(XUiC_MapArea), "UpdateWaypointSelection")]');$end=$purge.IndexOf('[HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.Update))]',$start)
$hook=$purge.Substring($start,$end-$start)
$full=Get-Content "$root/Scripts/UI/RebirthMapOverview.cs" -Raw
$extra=$full.Substring($full.IndexOf('internal static class RebirthMapZoomInput'),$full.IndexOf('[HarmonyPatch(typeof(XUiC_MapArea),"teleportPlayerOnMap")]')-$full.IndexOf('internal static class RebirthMapZoomInput'))
if($DetachNegativeControl){$extra=$extra.Replace('textureField(widget)=texture;','widget.mainTexture=texture;')}
if($PostfixNegativeControl){$extra=$extra.Replace('if(!__runOriginal||!__state.Accepted)return;','')}
$support=Get-Content "$PSScriptRoot/Support.cs.txt" -Raw
$fixture=Get-Content "$PSScriptRoot/Checks.cs.txt" -Raw
Add-Type -TypeDefinition $fixture.Replace('/*OVERVIEW*/',$overview).Replace('/*PRESENTATION*/',$presentation).Replace('/*PURGEHOOK*/',$hook).Replace('/*EXTRA*/',$support+$extra)
& "$PSScriptRoot/NativeRuntime.ps1"



