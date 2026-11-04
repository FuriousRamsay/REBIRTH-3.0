param([ValidateSet('Docks','Ranger')][string]$Phase,[int]$Batch=36)
$ErrorActionPreference='Stop';$base="Tools/Profiler/out/purge-b$Batch";$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($role,$label,$a){$r=& $h -Stage "$base/$role" -Label ("b$Batch-$Phase-"+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
$context=Get-Content "$base/context.json" -Raw|ConvertFrom-Json
function O($label){$r=B server $label @('console',"rbpurgetestobserve player $($context.player)");return ($r.results[0].output[0]|ConvertFrom-Json)}
function P($label){$r=B client $label @('console','rbpurgetestobserve projection');return ($r.results[0].output[0]|ConvertFrom-Json)}
function Ready($label){for($i=0;$i -lt 60;$i++){$s=B client ($label+'-'+$i) @('state','-Sections','player');if($s.state -eq 'ingame' -and $s.player.onGround -and -not $s.player.dead -and -not $s.player.flying){if(-not $s.guard.enabled -or -not $s.guard.heal -or -not $s.guard.reload){throw 'Guard/heal/reload required'};return};Start-Sleep -Seconds 1};throw 'Native arrival not ready'}
$expected=if($Phase -eq 'Docks'){'docks_01'}else{'ranger_station_07'}
if($Phase -eq 'Ranger'){
 $null=B client 'restore-before-entry' @('restore');$null=B client 'exterior' @('console','teleport -1246 68 438');Ready 'exterior-landed';$null=B client 'arrival-surroundings' @('surroundings');$null=B client 'walk-in' @('walkto','x=-1231','z=438','maxSeconds=35')
}
Ready 'native-ready';$before=O 'before';if($before.prefab -ne $expected){throw 'Wrong actual retained POI; no replacement target'}
$null=P 'before-projection';$null=B client 'before-shot' @('screenshot',("b$Batch-$Phase-before"))
$inventory=B client 'loadout' @('state','-Sections','player,inventory');if(@($inventory.toolbelt|Where-Object {$_.slot -eq 1 -and $_.name -eq 'gunHandgunPistolAdmin'}).Count -ne 1){$null=B client 'fixture-pistol' @('give','item=gunHandgunPistolAdmin','toolbelt=2')}
$null=B client 'select-pistol' @('select','slot=2')
& "$PSScriptRoot/Invoke-PurgeSleeperHelpBatch35.ps1" -Stage "$base/client" -Action Start -Label ("b$Batch-$Phase-help-start")
for($round=0;$round -lt 3;$round++){
 & "$PSScriptRoot/Run-PurgeActorsBatch22.ps1" -Stage "$base/client" -ObservationStage "$base/server" -PlayerId $context.player -ExpectedPrefab $expected -RunLabel ("b$Batch-$Phase-remaining-$round") -StageDistance 1 -Passes 2 -RestoreFixtureBeforeFight
 for($i=0;$i -lt 8;$i++){$after=O ("after-$round-$i");$projection=P ("after-projection-$round-$i");if($after.record.state -eq 'Cleared'){break};Start-Sleep -Seconds 1}
 if($after.record.state -eq 'Cleared'){break}
 $alive=B client ("remaining-entities-$round") @('entities','radius=128','limit=256');$ids=@($after.rooms|ForEach-Object {@($_.respawnIds)+@($_.pendingIds)}|Select-Object -Unique)
 if(-not @($alive.entities|Where-Object {-not $_.dead -and $ids -contains $_.id}).Count){break}
}
$null=B client 'final-shot' @('screenshot',("b$Batch-$Phase-final"))
@{before=$before;after=$after;client=$projection;scope='Actual retained native actors after cold load; native authenticated Start and real staged combat. No synthetic clearance/credit or room changes.'}|ConvertTo-Json -Depth 30|Set-Content "$base/$Phase-clearance-verdict.json"
if($after.record.state -ne 'Cleared'){throw 'Retained target not cleared; preserve exact native blockers'}
