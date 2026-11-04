param([Parameter(Mandatory=$true)][string]$Stage,[switch]$ResumeInside,[string]$RunLabel='tier4')
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label ($RunLabel+'-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
function Check($b,$why){if(-not $b){throw $why}}
$initial=O 'initial';Check ($initial.saveName -eq 'CodexTestPurgeFresh20261102') 'Wrong disposable save'
$pois=B 'candidates' @('pois','radius=1200','limit=150','maxTier=6')
$targets=@($pois.pois|Where-Object {$_.name -eq 'army_camp_05'});Check ($targets.Count -eq 1) 'Fort candidate missing/ambiguous'
$target=$targets[0]
$null=B 'travel-restore' @('restore')
if(-not $ResumeInside){
 $null=B 'walk-to-edge' @('call','POST','/gotopoi',"id=$($target.id)")
 $null=B 'enter' @('call','POST','/enterpoi',"id=$($target.id)")
}
$null=B 'surroundings' @('surroundings');$before=O 'before'
Check ($before.scannerNativePoi.prefab -eq 'army_camp_05') 'Did not enter intended Fort'
if($before.record.state -eq 'Cleared'){
 $null=B 'native-reset' @('console','cr');$ready=$false
 for($i=0;$i -lt 20;$i++){Start-Sleep -Seconds 1;$o=O ('reset-'+$i);if($o.record.epoch -gt $before.record.epoch -and $o.record.state -eq 'Discovered'){$ready=$true;break}}
 Check $ready 'Native reset did not complete; no forced state change'
}
$null=B 'restore' @('restore');$null=B 'admin-fixture' @('give','item=gunHandgunPistolAdmin','toolbelt=2')
$null=B 'native-activate' @('console','poisleepers start force');Start-Sleep -Seconds 2
& (Join-Path $PSScriptRoot 'Run-PurgeActorsBatch22.ps1') -Stage $Stage -ExpectedPrefab army_camp_05 -RunLabel fort-route -StageDistance 8
$after=O 'after'
@{before=$initial.supply;after=$after.supply;record=$after.record;evidence=$after.savedEvidence;scope='Actual native reset/activation/combat; scanner boundaries require independent per-fight observation review'}|ConvertTo-Json -Depth 10|Set-Content (Join-Path $Stage ($RunLabel+'-verdict.json'))
if($after.record.state -ne 'Cleared'){Write-Output 'PARTIAL: actual pending actors retained; inspect before recovery'}
