param([Parameter(Mandatory=$true)][string]$Stage,[ValidateSet('Baseline','Visits','Tier5')][string]$Phase,[int]$Attempt=1,[string]$OnlyPrefab)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1';$prefix='batch29-'+$Phase+'-'+$Attempt
function B($label,$a){$r=& $h -Stage $Stage -Label ($prefix+'-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
function Ready($label){
 for($i=0;$i -lt 90;$i++){
  $s=B ($label+'-'+$i) @('state','-Sections','player')
  if($s.state -eq 'ingame' -and -not $s.player.dead -and $s.player.onGround -and -not $s.player.flying){return $s}
  Start-Sleep -Seconds 1
 }
 throw 'Native arrival not ready; preserve same process and inspect'
}
$before=O 'before';if($before.saveName -ne 'CodexTestPurgeFresh20261102'){throw 'Wrong disposable save'}
$s=Ready 'ready';if(-not $s.guard.enabled -or -not $s.guard.heal -or -not $s.guard.reload){throw 'Guard/heal/reload must stay on'}
if($Phase -eq 'Baseline'){
 if($before.feedbackTrace.total -ne 0){throw 'Cold load replayed old feedback'}
 $null=B 'godmode-off-proof' @('call','POST','/godmode','on=0');$null=B 'surroundings' @('surroundings');$null=B 'daylight' @('console','settime 5 9 0');return
}
if($Phase -eq 'Visits'){
 $cases=@(@{name='house_old_bungalow_03';x=-440;y=37;z=458;sx=42;sz=42},@{name='house_old_bungalow_02';x=-471;y=34;z=510;sx=42;sz=42})
 $rows=@()
 foreach($c in $cases|Where-Object {-not $OnlyPrefab -or $_.name -eq $OnlyPrefab}){
  $tag=$c.name;$outsideX=$c.x-12;$z=$c.z+[int]($c.sz/2);$insideX=$c.x+3
  $null=B ($tag+'-exterior') @('console',"teleport $outsideX $($c.y+10) $z");$null=Ready ($tag+'-landed');$null=B ($tag+'-surroundings') @('surroundings')
  $start=O ($tag+'-outside');if($start.scannerNativePoi.prefab -eq $c.name){throw 'Exterior fixture already inside target'}
  $null=B ($tag+'-walk-in') @('call','POST','/walkto',"x=$insideX","z=$z",'maxSeconds=35')
  $o=$null
  for($i=0;$i -lt 8;$i++){$o=O ($tag+'-visit-'+$i);if($o.scannerNativePoi.prefab -eq $c.name -and $o.mapRecordFound){break};Start-Sleep -Seconds 1}
  $null=B ($tag+'-notice-shot') @('screenshot',('b29-first-visit-'+$tag))
  $events=@($o.feedbackTrace.events|Where-Object {$_.prefab -eq $c.name -and $_.kind -like 'discovery-*'})
  $rows+=@{prefab=$c.name;inside=($o.scannerNativePoi.prefab -eq $c.name);mapFound=$o.mapRecordFound;history=$o.mapRecord;firstVisit=$o.firstVisitFeedback;events=$events;enrolled=$o.savedEvidence.enrolled;deaths=$o.savedEvidence.deathReceipts}
  $rows|ConvertTo-Json -Depth 9|Set-Content (Join-Path $Stage ($prefix+'-visits-review.json'))
  if($o.scannerNativePoi.prefab -ne $c.name){continue}
  $null=B ($tag+'-walk-out') @('call','POST','/walkto',"x=$outsideX","z=$z",'maxSeconds=35');$left=O ($tag+'-left')
  $null=B ($tag+'-walk-back') @('call','POST','/walkto',"x=$insideX","z=$z",'maxSeconds=35');$again=O ($tag+'-again')
  @{left=($left.scannerNativePoi.prefab -ne $c.name);returned=($again.scannerNativePoi.prefab -eq $c.name);beforeEvents=$events;afterEvents=@($again.feedbackTrace.events|Where-Object {$_.prefab -eq $c.name -and $_.kind -like 'discovery-*'});firstVisit=$again.firstVisitFeedback}|ConvertTo-Json -Depth 8|Set-Content (Join-Path $Stage ($prefix+'-'+$tag+'-dedupe.json'))
 }
 $null=B 'map-open' @('key','name=M');$null=B 'map-ui' @('uitree');$null=B 'map-shot' @('screenshot','b29-visited-poi-map');$null=B 'map-close' @('key','name=M');$null=O 'after';return
}
if($Phase -eq 'Tier5'){
 $null=B 'exterior' @('console','teleport 717 55 11');$null=Ready 'landed';$null=B 'arrival' @('surroundings')
 $null=B 'walk-in' @('call','POST','/walkto','x=732','z=11','maxSeconds=35');$o=O 'inside'
 if($o.scannerNativePoi.prefab -ne 'mine_01' -or $o.scannerNativePoi.tier -ne 5){throw 'No correct native tier5 target; no inside teleport fallback'}
 $inventory=B 'inventory' @('state','-Sections','player,inventory')
 if(@($inventory.toolbelt|Where-Object {$_.slot -eq 1 -and $_.name -eq 'gunHandgunPistolAdmin'}).Count -ne 1){$null=B 'fixture' @('give','item=gunHandgunPistolAdmin','toolbelt=2')}
 $null=B 'select' @('select','slot=2');$held=B 'held' @('state','-Sections','player');if($held.player.holding.name -ne 'gunHandgunPistolAdmin'){throw 'Held weapon preflight failed'}
 $force=B 'native-force' @('console','poisleepers start force');if($force.hadError){throw 'Native force error'};Start-Sleep -Seconds 2;$null=O 'native-started'
 & (Join-Path $PSScriptRoot 'Run-PurgeActorsBatch22.ps1') -Stage $Stage -ExpectedPrefab mine_01 -RunLabel ('batch29-tier5-'+$Attempt) -StageDistance 1
 $null=O 'after'
}
