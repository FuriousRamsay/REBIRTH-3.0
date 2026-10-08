$ErrorActionPreference='Stop'
$root=(Resolve-Path "$PSScriptRoot/../..").Path
$s=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Survivor/Progression/RebirthTheoryProgressionService.cs'))
$start=$s.IndexOf('public static string GetTheoryStatus(float value)');$end=$s.IndexOf('/// <summary>Finite one-time Insight award',$start);$method=$s.Substring($start,$end-$start)
$code='using System;using System.Collections.Generic; public static class StatusFixture { public class Band {public float Min,Max;public string Status;}public static List<Band> Bands=new List<Band>();'+$method+'}'
Add-Type -TypeDefinition $code
[xml]$xml=[IO.File]::ReadAllText((Join-Path $root 'Config/_Survivor/theory_insights.xml'))
foreach($b in $xml.survivor_theory_insights.status_bands.band){$x=[StatusFixture+Band]::new();$x.Min=[float]$b.min;$x.Max=[float]$b.max;$x.Status=$b.status;[StatusFixture]::Bands.Add($x)}
$count=0
function Check([float]$v,[string]$expected){$script:count++;$got=[StatusFixture]::GetTheoryStatus($v);if($got -ne $expected){throw "$v expected $expected got $got"}}
Check 0 Uneducated;Check .5 Uneducated;Check .999 Uneducated;Check -1 Uneducated
$bands=@($xml.survivor_theory_insights.status_bands.band)
for($i=1;$i -lt $bands.Count;$i++){Check ([float]$bands[$i].min) $bands[$i].status;Check ([float]$bands[$i].min-.001) $bands[$i-1].status}
foreach($v in @(14.5,29.5,44.5,59.5,74.5,89.5,99.5)){ $expected= switch($v){14.5{'Basic Understanding'}29.5{'Familiar'}44.5{'Informed'}59.5{'Knowledgeable'}74.5{'Advanced'}89.5{'Highly Knowledgeable'}99.5{'Expert Understanding'}};Check $v $expected }
Check 100 Mastered;Check 101 Mastered;Check ([float]::NaN) Uneducated;Check ([float]::PositiveInfinity) Uneducated;Check ([float]::NegativeInfinity) Uneducated
$before=[StatusFixture]::Bands.ToArray();[StatusFixture]::Bands.Reverse();Check 14.5 'Basic Understanding';Check 99.5 'Expert Understanding';[StatusFixture]::Bands.Clear();Check 50 Uneducated
"PASS $count actual extracted method / actual authored XML threshold checks; no awards or native game execution."