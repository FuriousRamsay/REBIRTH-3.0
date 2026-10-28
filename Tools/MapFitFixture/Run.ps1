param([switch]$EmptyPublishNegativeControl,[switch]$DelayedBindNegativeControl,[switch]$UnboundedZoomNegativeControl)
$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$overview=Get-Content "$root/Scripts/UI/RebirthMapOverview.cs" -Raw
$overview=$overview.Substring(0,$overview.IndexOf('// Native input routing'))
foreach($using in @('using System;','using System.Collections.Generic;','using System.Reflection;','using System.Reflection.Emit;','using HarmonyLib;','using UnityEngine;')){$overview=$overview.Replace($using,'')}
if($EmptyPublishNegativeControl){$overview=$overview.Replace('if(reframe){Array.Clear(pixels,0,pixels.Length);','if(reframe){texture.SetPixels32(new Color32[pixels.Length]);texture.Apply(false,false);Array.Clear(pixels,0,pixels.Length);')}
if($DelayedBindNegativeControl){$overview=$overview.Replace('map.xuiTexture.updateData();','')}
$presentation=(Get-Content "$root/Scripts/UI/RebirthMapPresentation.cs" -Raw).Replace('using HarmonyLib;','').Replace('using UnityEngine;','')
if($UnboundedZoomNegativeControl){$presentation=$presentation.Replace('Mathf.Max(.7f,Mathf.Min(atlasMaximum,worldHeight/(Mathf.Max(1,canvasHeight)*RebirthMapOverview.MetersPerUnit)))','atlasMaximum')}
$purge=Get-Content "$root/Scripts/Purge/Network/RebirthPoiMapPresentation.cs" -Raw
$start=$purge.IndexOf('[HarmonyPatch(typeof(XUiC_MapArea), "UpdateWaypointSelection")]');$end=$purge.IndexOf('[HarmonyPatch(typeof(XUiC_MapArea), nameof(XUiC_MapArea.Update))]',$start)
$hook=$purge.Substring($start,$end-$start)
$fixture=Get-Content "$PSScriptRoot/Checks.cs.txt" -Raw
Add-Type -TypeDefinition $fixture.Replace('/*OVERVIEW*/',$overview).Replace('/*PRESENTATION*/',$presentation).Replace('/*PURGEHOOK*/',$hook)
[Checks]::Run()

