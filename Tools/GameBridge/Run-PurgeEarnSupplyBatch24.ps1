param([Parameter(Mandatory=$true)][string]$Stage,[string]$OnlyPrefab)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label $label -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
$initial=O 'fresh-supply-initial';if($initial.saveName -ne 'CodexTestPurgeFresh20261102'){throw 'Wrong disposable fixture'}
foreach($c in @(@{name='cabin_10';x=-420;y=63;z=22},@{name='cabin_13';x=768;y=48;z=-288},@{name='cabin_14';x=490;y=45;z=-1})|Where-Object {-not $OnlyPrefab -or $_.name -eq $OnlyPrefab}){
 $prefix='fresh-supply-'+$c.name;$current=O ($prefix+'-quota');if($current.supply.earnedDrops -gt $initial.supply.earnedDrops){break}
 $null=B ($prefix+'-stage') @('console',"teleport $($c.x) $($c.y) $($c.z)");Start-Sleep -Seconds 2;$null=B ($prefix+'-surroundings') @('surroundings')
 $o=O ($prefix+'-before');if($o.scannerNativePoi.prefab -ne $c.name){throw 'Staging did not enter intended native POI'}
 if($o.record.state -eq 'Cleared'){
  $null=B ($prefix+'-native-reset') @('console','cr');$reset=$false
  for($i=0;$i -lt 15;$i++){Start-Sleep -Seconds 1;$next=O ($prefix+'-reset-'+$i);if($next.record.epoch -gt $o.record.epoch -and $next.record.state -eq 'Discovered'){$reset=$true;break}}
  if(-not $reset){throw 'Native reset did not complete; no synthetic retry/state change'}
 }
 $null=B ($prefix+'-restore') @('restore');$null=B ($prefix+'-admin') @('give','item=gunHandgunPistolAdmin','toolbelt=2')
 $null=B ($prefix+'-activate') @('console','poisleepers start force');Start-Sleep -Seconds 2
 & (Join-Path $PSScriptRoot 'Run-PurgeActorsBatch22.ps1') -Stage $Stage -ExpectedPrefab $c.name -RunLabel $prefix
 $after=O ($prefix+'-verified');if($after.record.state -ne 'Cleared'){throw 'Actual native deaths incomplete; preserve remaining actors'}
}
$final=O 'fresh-supply-final';@{before=$initial.supply;after=$final.supply;scope='Actual native activation/reset/combat only; no credit or room-state assignment'}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $Stage 'fresh-supply-verdict.json')
if($final.supply.earnedDrops -le $initial.supply.earnedDrops){Write-Output 'PARTIAL: actual kills retained but next real entitlement not reached'}
