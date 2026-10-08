$ErrorActionPreference='Stop'
function Extract($src,$sig){$a=$src.IndexOf($sig);if($a-lt0){throw "Missing $sig"};$o=$src.IndexOf('{',$a);$n=1;$b=$o+1;while($n){if($src[$b]-eq'{'){$n++}elseif($src[$b]-eq'}'){$n--};$b++};$src.Substring($a,$b-$a)}
$te=Get-Content Tools/FarmingPlayerOriginCandidate/AdvancedFarmingTileEntities.cs -Raw
$origin=Get-Content Tools/FarmingPlayerOriginCandidate/AdvancedFarmingPlantOriginService.cs -Raw
$code='using System;public partial class TileEntityPlantGrowingRebirth { private static readonly object PlantIncarnationSync=new object();private static ulong s_nextPlantIncarnation=1UL;'
foreach($sig in @('public void BeginNewPlantIncarnation','public void EnsureAuthoritativeIncarnation','public void AdvanceStateRevision','private static ulong AllocatePlantIncarnation','private static void ObservePlantIncarnation')){$code+=Extract $te $sig};$code+='}'
Set-Content (Join-Path $PSScriptRoot 'ActualDependencies.cs') $code


