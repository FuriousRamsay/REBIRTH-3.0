param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Metabolism/Consumption/RebirthConsumableResolver.cs'))
$marker='public static class RebirthLiquidContainerService'
$a=$source.IndexOf($marker,[StringComparison]::Ordinal)
if($a -lt 0 -or $source.IndexOf($marker,$a+1,[StringComparison]::Ordinal) -ge 0){throw 'Unique current liquid service required'}
$liquid=$source.Substring($a)
$cooking=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Crafting/Cooking/RebirthCookingItemStats.cs'))
$a=$cooking.IndexOf('    public static bool FullIngredient(',[StringComparison]::Ordinal)
$b=$cooking.IndexOf('    public static ItemValue BaseItem(',$a,[StringComparison]::Ordinal)
if($a -lt 0 -or $b -le $a){throw 'Current cooking ingredient gate boundary changed'}
$gate=$cooking.Substring($a,$b-$a)
$body="using System;using System.Globalization;using UnityEngine;`r`n"+$liquid+"`r`npublic static class RebirthCookingItemStats {`r`n"+$gate+"}`r`n"
$path=[IO.Path]::GetFullPath($Output);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))|Out-Null
[IO.File]::WriteAllText($path,$body)
