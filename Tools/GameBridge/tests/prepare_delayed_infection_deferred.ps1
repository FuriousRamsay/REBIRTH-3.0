param([ValidateSet(0,50,100)][int]$Medicine=100)
$ErrorActionPreference='Stop'
# NOT RUN. Disposable CodexTest only; exact reviewed adoption and restart first.
$gb=Join-Path (Get-Location) 'Tools/GameBridge/gamebridge.ps1'
function Bridge([string[]]$BridgeArgs){ & powershell -NoProfile -ExecutionPolicy Bypass -File $gb @BridgeArgs; if($LASTEXITCODE -ne 0){throw 'Bridge operation failed'} }
Bridge -BridgeArgs @('cleararea','radius=40')
Bridge -BridgeArgs @('restore')
Bridge -BridgeArgs @('surroundings')
Bridge -BridgeArgs @('skillsetup','confirm=CodexTest','skill=skill.medicine',"value=$Medicine")
Bridge -BridgeArgs @('console','debuff buffInfectionAddCure','debuff buffInfectionMain')
# Seed before buff start so the native infection buff is not removed on its first update.
Bridge -BridgeArgs @('setcvar','$infectionMaxDuration','1000')
Bridge -BridgeArgs @('setcvar','infectionCounter','100')
Bridge -BridgeArgs @('setcvar','$infectionCureCounter','0')
Bridge -BridgeArgs @('console','buff buffInfectionMain')
Start-Sleep -Seconds 1
# Capture actual loaded healing rate after native buff start, before zero-rate isolation.
$nativeReply=(Bridge -BridgeArgs @('cvar','_InfectionCureRate')) -join [Environment]::NewLine
$nativeRate=($nativeReply|ConvertFrom-Json).cvars._InfectionCureRate
if($null -eq $nativeRate -or [double]::IsNaN([double]$nativeRate) -or [double]::IsInfinity([double]$nativeRate) -or [double]$nativeRate -gt -2 -or [double]$nativeRate -lt -4){throw 'Loaded native cure rate must be finite in [-4,-2] for this timed fixture. Abort; do not invent or substitute an earned recovery.'}
# All preparation occurs before genuine input. Keep the configured values aligned with JSON.
foreach($pair in @(@('$infectionMaxDuration','1000'),@('infectionCounter','100'),@('$infectionCureCounter','0'),@('$buffInfectionAddCurePerc','0'),@('_InfectionRate','0'),@('_InfectionCureRate','0'),@('$critHitNaturalHealingRate','0'))){Bridge -BridgeArgs @('setcvar',$pair[0],$pair[1])}
Bridge -BridgeArgs @('give','item=drugAntibiotics','count=1','toolbelt=1')
Bridge -BridgeArgs @('test',(Join-Path $PSScriptRoot "delayed_infection_medicine_$Medicine.json"))
# Restore captured genuine rate. Do not edit infection/cure budget or skill after consumption.
Bridge -BridgeArgs @('setcvar','_InfectionCureRate',([double]$nativeRate).ToString('R',[Globalization.CultureInfo]::InvariantCulture))
Bridge -BridgeArgs @('test',(Join-Path $PSScriptRoot "delayed_infection_completion_$Medicine.json"))
# Restore/relaunch disposable baseline afterward; fixture rates must not persist into normal play.