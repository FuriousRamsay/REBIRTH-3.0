param([Parameter(Mandatory=$true)][string]$Stage,[ValidateSet('Baseline','Custody','Admission','Close')][string]$Phase)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label ('b30-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
function Ready($label){for($i=0;$i -lt 90;$i++){ $s=B ($label+'-'+$i) @('state','-Sections','player');if($s.state -eq 'ingame' -and -not $s.player.dead -and $s.player.onGround -and -not $s.player.flying){if(-not $s.guard.enabled -or -not $s.guard.heal -or -not $s.guard.reload){throw 'Guard/heal/reload required'};return $s};Start-Sleep -Seconds 1 };throw 'Arrival not ready; inspect existing process'}
$start=O ($Phase+'-before');if($start.saveName -ne 'CodexTestPurgeFresh20261102'){throw 'Wrong disposable save'}
$null=Ready ($Phase+'-ready')
if($Phase -eq 'Baseline'){
 if($start.feedbackTrace.total -ne 0){throw 'Old feedback replayed on cold load'}
 $null=B 'godmode-off' @('call','POST','/godmode','on=0');$null=B 'baseline-surroundings' @('surroundings')
 $null=B 'daylight' @('console','settime 6 9 0');return
}
if($Phase -eq 'Custody'){
 $null=B 'mine-exterior' @('console','teleport 717 55 11');$null=Ready 'mine-landed';$null=B 'mine-surroundings' @('surroundings')
 $null=B 'mine-walk-in' @('call','POST','/walkto','x=732','z=11','maxSeconds=35');$o=O 'mine-cold-custody'
 if($o.scannerNativePoi.prefab -ne 'mine_01'){throw 'Mine entry not verified'}
 if($o.savedEvidence.enrolled -lt 128 -or $o.savedEvidence.deathReceipts -lt 79 -or $o.record.epoch -ne 0){throw 'Existing mine custody was not retained'}
 $null=B 'mine-cold-shot' @('screenshot','b30-mine-custody');return
}
if($Phase -eq 'Admission'){
 $cases=(Get-Content _Documentation/Purge/FullRegression20261102/BATCH30_CANDIDATES.json -Raw|ConvertFrom-Json).candidates.ordinarySmall|Select-Object -First 3
 $witnesses=@();$reviews=@()
 foreach($c in $cases){
  $tag=$c.name;$x=$c.origin[0]-12;$y=$c.origin[1]+10;$z=$c.origin[2]+[int]($c.size[2]/2)
  $null=B ($tag+'-exterior') @('console',"teleport $x $y $z");$null=Ready ($tag+'-landed');$null=B ($tag+'-surroundings') @('surroundings')
  $o=O ($tag+'-idle-before');$witnesses+=@($o.discoveryAdmission.rows|Where-Object {$_.canOmit -and -not $_.recorded -and $_.actors -eq 0 -and $_.untouched})
  $inside=$c.origin[0]+3
  try{$null=B ($tag+'-walk-in') @('call','POST','/walkto',"x=$inside","z=$z",'maxSeconds=35')}catch{if(-not (Test-Path (Join-Path $Stage ('b30-'+$tag+'-walk-in.json')))){throw};$ping=B ($tag+'-failed-walk-status') @('status');if($ping.batch.uncertain -or $ping.batch.queued -ne 0){throw};$null=B ($tag+'-failed-walk-error-retained') @('status')}
  $after=O ($tag+'-after');$events=@($after.feedbackTrace.events|Where-Object {$_.prefab -eq $c.name -and $_.kind -like 'discovery-*'})
  $reviews+=@{prefab=$c.name;inside=($after.scannerNativePoi.prefab -eq $c.name);events=$events;record=$after.record;admission=$after.discoveryAdmission}
  $null=B ($tag+'-shot') @('screenshot',('b30-'+$tag+'-discovery'))
  # A later real visit supersedes a prior idle witness; never classify it as untouched afterward.
  $witnesses=@($witnesses|Where-Object {$w=$_; -not @($after.discoveryAdmission.rows|Where-Object {$_.key -eq $w.key -and (-not $_.untouched -or $_.recorded -or $_.actors -ne 0)}).Count})
 }
 $final=O 'admission-final';$witnesses+=@($final.discoveryAdmission.rows|Where-Object {$_.canOmit -and -not $_.recorded -and $_.actors -eq 0 -and $_.untouched})
 $witnesses=@($witnesses|Sort-Object key -Unique|Where-Object {$w=$_;-not @($final.discoveryAdmission.rows|Where-Object {$_.key -eq $w.key -and (-not $_.untouched -or $_.recorded -or $_.actors -ne 0)}).Count})
 ConvertTo-Json -InputObject $witnesses -Depth 8|Set-Content (Join-Path $Stage 'untouched-witnesses.json');ConvertTo-Json -InputObject $reviews -Depth 12|Set-Content (Join-Path $Stage 'discovery-review.json')
 if($witnesses.Count -eq 0){throw 'No positive idle candidates captured; shutdown test unqualified'}
 $null=B 'map-open' @('key','name=M');$null=B 'map-shot' @('screenshot','b30-discovery-map');$null=B 'map-close' @('key','name=M');return
}
if($Phase -eq 'Close'){
 $null=B 'safe-exit' @('console','teleport 47 42 -254');$null=Ready 'safe-landed';$null=B 'safe-surroundings' @('surroundings');$null=B 'final-godmode-off' @('call','POST','/godmode','on=0');$null=O 'final';$null=B 'normal-quit' @('call','POST','/quit')
}
