param([Parameter(Mandatory=$true)][string]$Stage,[string]$ExpectedPrefab='cabin_13')
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label $label -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
$before=O 'native-room-before';if($before.scannerNativePoi.prefab -ne $ExpectedPrefab){throw 'Wrong native POI'}
$null=B 'native-room-arrival' @('surroundings');$null=B 'native-room-restore' @('restore')
$null=B 'native-room-admin-fixture' @('give','item=gunHandgunPistolAdmin','toolbelt=2');$null=B 'native-room-select' @('select','slot=2')
$start=B 'native-room-start' @('console','poisleepers start force')
if($start.hadError -or (($start.results[0].output -join ' ') -match 'not fully registered|ERROR|unavailable')){throw 'Native sleeper start refused; inspect reply'}
$rooms=@($before.authoredRoomRegistration|Where-Object {$_.authoredMax -gt 0 -or $_.script})
$report=@()
for($pass=1;$pass -le 2;$pass++){
 foreach($room in $rooms){
  $label="native-room-p$pass-$($room.nativeId)";$x=[int][Math]::Floor(($room.minimum[0]+$room.maximum[0])/2.0);$y=$room.minimum[1]+1;$z=[int][Math]::Floor(($room.minimum[2]+$room.maximum[2])/2.0)
  $current=O ($label+'-before');if($current.record.state -eq 'Cleared'){break}
  # Test staging only; not ordinary full-room traversal acceptance.
  $null=B ($label+'-stage') @('console',"teleport $x $y $z")
  Start-Sleep -Milliseconds 500
  $near=B ($label+'-surroundings') @('surroundings')
  if($near.count -gt 0){try{$null=B ($label+'-fight') @('fight','contains=zombie')}catch{if($_.ErrorDetails.Message -notmatch 'no enemy found'){throw}}}
  $after=O ($label+'-after')
  $report+=@{pass=$pass;nativeRoom=$room.nativeId;record=$after.record;savedEvidence=$after.savedEvidence;position=$after.position}
  $report|ConvertTo-Json -Depth 10|Set-Content (Join-Path $Stage 'native-room-progress.json')
 }
 $current=O ("native-room-pass$pass-complete");if($current.record.state -eq 'Cleared'){break}
}
$final=O 'native-room-final'
@{prefab=$ExpectedPrefab;state=$final.record.state;evidence=$final.savedEvidence;supply=$final.supply;feedback=$final.feedbackTrace;scope='Actual native activation/staged combat only; review raw fights and pending rooms. No synthetic death/clear credits.'}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $Stage 'native-room-verdict.json')
if($final.record.state -ne 'Cleared'){Write-Output 'PARTIAL: native remaining rooms retained; no clearance forced'}else{Write-Output 'Native record Cleared; inspect actual death receipts and clear feedback'}

