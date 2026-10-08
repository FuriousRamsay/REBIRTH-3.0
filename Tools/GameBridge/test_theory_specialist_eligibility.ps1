#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -Path (Join-Path $taskRoot 'Scripts/Survivor/Progression/RebirthTheorySpecialistEligibility.cs')
$cases=@(@(400,400,$true),@(600,1000,$true),@(399,600,$false),@(600,399,$false),@(0,0,$false),@([single]::NaN,600,$false),@(600,[single]::PositiveInfinity,$false),@(600,[single]::NegativeInfinity,$false))
foreach($case in $cases){if([RebirthTheorySpecialistEligibility]::CanInstruct([single]$case[0],[single]$case[1])-ne $case[2]){throw 'Specialist faction eligibility assertion failed'}}
'PASS8 actual specialist faction eligibility checks; live resolver/combat/game not exercised'