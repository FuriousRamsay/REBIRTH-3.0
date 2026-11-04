param([Parameter(Mandatory=$true)][string]$Stage,[int]$Attempt=1,[switch]$Resume)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label ('three-room-stress-attempt'+$Attempt+'-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
$before=O 'before';if($before.saveName -ne 'CodexTestPurgeFresh20261102'){throw 'Wrong save'}
# Return to the actual previously observed exterior position. This phase explicitly
# tests native force activation/scanner, not ordinary traversal acceptance.
$null=B 'stage-prior-exterior' @('console','teleport 772 44 -821');Start-Sleep -Seconds 2;$null=B 'arrival' @('surroundings')
$o=O 'staged';if($o.scannerNativePoi.prefab -ne 'site_shack_10' -or $o.scanner.rooms -ne 3){throw 'Wrong actual native three-room target'}
$null=B 'admin-fixture' @('give','item=gunHandgunPistolAdmin','toolbelt=2');$null=B 'select-admin' @('select','slot=2')
if(-not $Resume){
 $force=B 'native-force' @('console','poisleepers start force')
 if($force.hadError -or ($force.results[0].output -join ' ') -match 'not fully registered|unavailable|ERROR'){throw 'Native force refused'}
}
Start-Sleep -Seconds 2;$null=O 'native-started'
& (Join-Path $PSScriptRoot 'Run-PurgeActorsBatch22.ps1') -Stage $Stage -ExpectedPrefab site_shack_10 -RunLabel ('three-room-stress-attempt'+$Attempt+'-actors') -StageDistance 1
$null=O 'after'
