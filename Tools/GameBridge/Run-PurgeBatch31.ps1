param([Parameter(Mandatory=$true)][string]$Stage,[ValidateSet('Baseline','Quiet','Scripted','Map','Close')][string]$Phase)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label ('b31-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
function Ready($label){for($i=0;$i -lt 90;$i++){ $s=B ($label+'-'+$i) @('state','-Sections','player');if($s.state -eq 'ingame' -and -not $s.player.dead -and $s.player.onGround -and -not $s.player.flying){if(-not $s.guard.enabled -or -not $s.guard.heal -or -not $s.guard.reload){throw 'Guard/heal/reload required'};return $s};Start-Sleep -Seconds 1 };throw 'Arrival not ready; inspect same process'}
function StageAt($label,$command){$null=B ($label+'-fixture') @('console',$command);$null=Ready ($label+'-landed');$null=B ($label+'-surroundings') @('surroundings')}
$start=O ($Phase+'-before');if($start.saveName -ne 'CodexTestPurgeFresh20261102'){throw 'Wrong disposable save'};$null=Ready ($Phase+'-ready')
if($Phase -eq 'Baseline'){
 if($start.feedbackTrace.total -ne 0){throw 'Cold load replayed old feedback'}
 $null=B 'baseline-godmode-off' @('call','POST','/godmode','on=0');$null=B 'baseline-surroundings' @('surroundings');$null=B 'daylight' @('console','settime 7 9 0');return
}
if($Phase -eq 'Quiet'){
 StageAt 'known-bungalow' 'teleport -452 47 479'
 $null=B 'known-walk-in' @('call','POST','/walkto','x=-437','z=479','maxSeconds=35');$first=O 'known-entered'
 if($first.scannerNativePoi.prefab -ne 'house_old_bungalow_03'){throw 'Known target entry not verified'}
 $null=B 'known-walk-out' @('call','POST','/walkto','x=-452','z=479','maxSeconds=35');$outside=O 'known-left'
 $null=B 'known-walk-back' @('call','POST','/walkto','x=-437','z=479','maxSeconds=35');$again=O 'known-reentered'
 $events=@($again.feedbackTrace.events|Where-Object {$_.prefab -eq 'house_old_bungalow_03' -and $_.kind -like 'discovery-*'})
 $review=@{left=($outside.scannerNativePoi.prefab -ne 'house_old_bungalow_03');returned=($again.scannerNativePoi.prefab -eq 'house_old_bungalow_03');events=$events;history=$again.mapRecord}
 $review|ConvertTo-Json -Depth 8|Set-Content (Join-Path $Stage 'known-quiet-verdict.json')
 if(-not $review.left -or -not $review.returned -or $events.Count -ne 0){throw 'Known discovery reentry not quiet or traversal not verified'};return
}
if($Phase -eq 'Scripted'){
 StageAt 'wilderness-exterior' 'teleport -1005 45 -2100'
 $null=B 'wilderness-walk-in' @('call','POST','/walkto','x=-990','z=-2100','maxSeconds=35');$inside=O 'wilderness-inside'
 if($inside.scannerNativePoi.prefab -ne 'wilderness_filler_17'){throw 'Wrong native scripted target; no interior teleport fallback'}
 $null=B 'script-fixture-admin' @('give','item=gunHandgunPistolAdmin','toolbelt=2');$null=B 'script-select' @('select','slot=2');$held=B 'script-held' @('state','-Sections','player')
 if($held.player.holding.name -ne 'gunHandgunPistolAdmin'){throw 'Missing held admin fixture'}
 $force=B 'native-script-start' @('console','poisleepers start force');if($force.hadError){throw 'Native activation error'}
 $o=$null
 for($i=0;$i -lt 35;$i++){
  $o=O ('script-running-'+$i)
  if($o.scannerNativePoi.prefab -ne 'wilderness_filler_17'){throw 'Player left scripted target'}
  if($o.savedEvidence.enrolled -ge 2){break};Start-Sleep -Seconds 1
 }
 if($o.savedEvidence.enrolled -lt 2){throw 'No native script combat participants; no fake enrollment'}
 & (Join-Path $PSScriptRoot 'Run-PurgeActorsBatch22.ps1') -Stage $Stage -ExpectedPrefab wilderness_filler_17 -RunLabel b31-scripted-combat -StageDistance 1 -Passes 2
 $final=$null
 for($i=0;$i -lt 40;$i++){$final=O ('zero-remaining-'+$i);if($final.record.state -eq 'Cleared'){break};Start-Sleep -Seconds 1}
 $null=B 'script-final-shot' @('screenshot','b31-scripted-final')
 @{state=$final.record.state;record=$final.record;evidence=$final.savedEvidence;rooms=$final.authoredRoomRegistration;completion=$final.completionDiagnostic;feedback=$final.feedbackTrace;scope='Native mixed combat and zero-actor sound/trigger rooms; force activation and collision-checked native combat, not full ordinary traversal.'}|ConvertTo-Json -Depth 16|Set-Content (Join-Path $Stage 'scripted-clearance-verdict.json')
 if($final.record.state -ne 'Cleared'){throw 'Scripted target did not complete; inspect exact native room/actor blockers'};return
}
if($Phase -eq 'Map'){
 StageAt 'safe-map' 'teleport 47 42 -254';$o=O 'map-supply-before'
 $null=B 'map-open' @('key','name=M');$null=B 'map-tree' @('uitree');$null=B 'map-shot' @('screenshot','b31-supply-map');$null=B 'map-close' @('key','name=M');$null=O 'map-after';return
}
if($Phase -eq 'Close'){
 StageAt 'safe-close' 'teleport 47 42 -254';$null=B 'final-godmode-off' @('call','POST','/godmode','on=0');$o=O 'final'
 $w=@($o.discoveryAdmission.rows|Where-Object {$_.canOmit -and -not $_.recorded -and $_.untouched -and $_.actors -eq 0}|Sort-Object key -Unique)
 ConvertTo-Json -InputObject $w -Depth 8|Set-Content (Join-Path $Stage 'untouched-witnesses.json')
 @{positiveWitnesses=$w.Count;total=$o.discoveryAdmission.total;truncated=$o.discoveryAdmission.truncated;records=$o.recordCount;revision=$o.revision}|ConvertTo-Json|Set-Content (Join-Path $Stage 'idle-before-shutdown.json')
 # Even an unqualified witness collection still closes normally; verifier requires a positive witness.
 $null=B 'normal-quit' @('call','POST','/quit')
}
