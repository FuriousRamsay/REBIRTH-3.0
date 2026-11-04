param([Parameter(Mandatory=$true)][string]$Stage)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label ('small-scanner-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
function Check($b,$why){if(-not $b){throw $why}}
$null=B 'stage' @('console','teleport 47 42 -254');Start-Sleep -Seconds 2;$null=B 'surroundings' @('surroundings')
$before=O 'before';Check ($before.saveName -eq 'CodexTestPurgeFresh20261102' -and $before.scannerNativePoi.prefab -eq 'site_utility_07') 'Wrong disposable native station'
Check ($before.record.state -eq 'Cleared') 'Station not in expected prior cleared epoch'
$reset=B 'reset' @('console','cr');Check (-not $reset.hadError) 'Native reset command error'
$ready=$false
for($i=0;$i -lt 15;$i++){Start-Sleep -Seconds 1;$o=O ('reset-settle-'+$i);if($o.record.epoch -gt $before.record.epoch -and $o.record.state -eq 'Discovered'){$ready=$true;break}}
Check $ready 'No completed new native reset epoch; do not force state'
$null=B 'start' @('console','poisleepers start force');$live=$false
for($i=0;$i -lt 12;$i++){
 Start-Sleep -Milliseconds 250;$o=O ('live-'+$i)
 if($o.savedEvidence.enrolled -gt $o.savedEvidence.deathReceipts -and $o.scanner.rooms -eq 2){
  Check (-not $o.scanner.threshold -and $o.visibleMarkerCount -eq 0) 'Low-tier two-room station must not admit scanner targets'
  $live=$true;break
 }
}
Check $live 'Did not observe pending live native sleepers in two-room station; no boundary PASS'
$null=B 'admin-fixture' @('give','item=gunHandgunPistolAdmin','toolbelt=2')
& (Join-Path $PSScriptRoot 'Run-PurgeActorsBatch22.ps1') -Stage $Stage -ExpectedPrefab site_utility_07
$after=O 'after'
Check ($after.record.state -eq 'Cleared' -and $after.savedEvidence.enrolled -eq 3 -and $after.savedEvidence.deathReceipts -eq 3) 'Native station did not prove all three actual deaths'
Check ($after.supply.observedCredits -eq $before.supply.observedCredits+3) 'Unexpected reset/reclear credits'
@{scope='Local two-room live scanner exclusion and actual native reset/reclear; no ordinary traversal or remote coverage';epoch=$after.record.epoch;deaths=$after.savedEvidence.deathReceipts;liveThresholdExclusion=$live;result='PASS_LOCAL'}|ConvertTo-Json|Set-Content (Join-Path $Stage 'small-scanner-verdict.json')
