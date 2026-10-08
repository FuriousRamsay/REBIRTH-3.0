#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -Path (Join-Path $taskRoot 'Scripts/Survivor/Progression/RebirthTrackingFollowEvidence.cs')
$cases=@(
@('approach',0,0,2,0,10,0,12,2),
@('away',0,0,-2,0,10,0,12,0),
@('sideways',0,0,0,2,10,0,12,0),
@('diagonal projected',0,0,2,2,10,0,12,2),
@('stationary',0,0,0,0,10,0,12,0),
@('oversized displacement',0,0,13,0,20,0,12,0),
@('tiny movement',0,0,0.05,0,10,0,12,0),
@('already at target',0,0,2,0,0,0,12,0),
@('overshoot capped',0,0,10,0,2,0,12,2),
@('negative coordinate bearing',-10,-10,-12,-10,-20,-10,12,2),
@('nonfinite target',0,0,2,0,[single]::NaN,0,12,0),
@('nonfinite position',0,0,[single]::PositiveInfinity,0,10,0,12,0),
@('invalid limit',0,0,2,0,10,0,0,0)
)
foreach($case in $cases){
$result=[RebirthTrackingFollowEvidence]::Credit([single]$case[1],[single]$case[2],[single]$case[3],[single]$case[4],[single]$case[5],[single]$case[6],[single]$case[7])
if([Math]::Abs($result-[single]$case[8])-gt 0.0001){throw ('Failed '+$case[0]+': '+$result)}
}
'PASS '+$cases.Count+' actual directional-credit checks; no native movement/award validation'