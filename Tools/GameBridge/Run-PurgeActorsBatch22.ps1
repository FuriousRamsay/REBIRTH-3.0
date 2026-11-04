param([Parameter(Mandatory=$true)][string]$Stage,[string]$ExpectedPrefab='cabin_13',[string]$RunLabel='actor-route',[int]$StageDistance=2,[ValidateRange(1,8)][int]$Passes=2,[string]$ObservationStage,[int]$PlayerId,[switch]$RestoreFixtureBeforeFight)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B($label,$a){$r=& $h -Stage $Stage -Label ($label.Replace('actor-route',$RunLabel)) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){
 if($ObservationStage){
  if($PlayerId -le 0){throw 'Remote actor route requires exact server player ID'}
  $raw=& $h -Stage $ObservationStage -Label ($label.Replace('actor-route',$RunLabel)) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @('console',"rbpurgetestobserve player $PlayerId") -Compress)
  $r=($raw -join "`n")|ConvertFrom-Json
  $null=B ($label+'-client-projection') @('console','rbpurgetestobserve projection')
 }else{$r=B $label @('console','rbpurgetestobserve')}
 return ($r.results[0].output[0]|ConvertFrom-Json)
}
$base=O 'actor-route-before';if($base.scannerNativePoi.prefab -ne $ExpectedPrefab){throw 'Wrong staged native POI'}
$null=B 'actor-route-audio-start' @('console','rbpurgeaudiotrace start 120')
$null=B 'actor-route-select' @('select','slot=2')
$equipped=B 'actor-route-held-preflight' @('state','-Sections','player,inventory')
if($equipped.player.holding.name -ne 'gunHandgunPistolAdmin'){throw 'Actor stress fixture requires the actual held admin pistol; no unarmed combat fallback'}
$results=@()
for($pass=1;$pass -le $Passes;$pass++){
 $o=O "actor-route-pass$pass-before";$ids=@($o.authoredRoomRegistration|ForEach-Object {@($_.pendingIds)+@($_.respawnIds)}|Select-Object -Unique)
 # Exact enrolled IDs are the filter; authored hostile animals also participate.
 $entities=B "actor-route-pass$pass-entities" @('entities','radius=256','limit=512')
 $alive=@($entities.entities|Where-Object {-not $_.dead -and $ids -contains $_.id})
 foreach($enemy in $alive){
  $prefix="actor-route-p$pass-$($enemy.id)";$y=[int][Math]::Floor($enemy.position[1]+0.05);$found=$null
  $offsets=if($pass -eq 1){@(@(2,0),@(0,2),@(-2,0),@(0,-2))}else{@(@(0,-2),@(-2,0),@(0,2),@(2,0))}
  if($StageDistance -eq 1){$offsets=@(@(1,0),@(0,1),@(-1,0),@(0,-1),@(1,1),@(-1,1),@(1,-1),@(-1,-1))+$offsets}
  if($StageDistance -gt 3){$offsets=@(@($StageDistance,0),@(0,$StageDistance),@(-$StageDistance,0),@(0,-$StageDistance))+$offsets}
  $offsets+=@(@(0,-3),@(3,0),@(0,3),@(-3,0),@(2,2),@(-2,2),@(2,-2),@(-2,-2))
  $j=0
  foreach($off in $offsets){
   $x=[int][Math]::Floor($enemy.position[0])+$off[0];$z=[int][Math]::Floor($enemy.position[2])+$off[1]
   $foot=B "$prefix-floor$j" @('blockdamage',"x=$x","y=$y","z=$z")
   if($foot.block -eq 'air'){
    $head=B "$prefix-head$j" @('blockdamage',"x=$x",('y='+($y+1)),"z=$z")
    $support=B "$prefix-support$j" @('blockdamage',"x=$x",('y='+($y-1)),"z=$z")
    if($head.block -eq 'air' -and $support.block -ne 'air' -and $support.maxDamage -gt 1){$found=@{x=$x;y=$y;z=$z};break}
   };$j++
  }
  if(-not $found){$results+=@{id=$enemy.id;pass=$pass;result='NO_VERIFIED_AIR_STAGING'};$results|ConvertTo-Json -Depth 8|Set-Content (Join-Path $Stage ($RunLabel+'-progress.json'));continue}
  if($RestoreFixtureBeforeFight){$null=B "$prefix-fixture-restore" @('restore')}
  $tele=B "$prefix-stage" @('console',"teleport $($found.x) $($found.y) $($found.z)")
  if(($tele.results[0].output -join ' ') -match 'argument is not a valid'){throw 'Native teleport rejected its integer arguments'}
  $settled=$false
  for($poll=0;$poll -lt 20;$poll++){
   Start-Sleep -Milliseconds 150;$near=B "$prefix-settle$poll" @('surroundings')
   if([Math]::Abs($near.position[0]-$found.x) -lt 2 -and [Math]::Abs($near.position[2]-$found.z) -lt 2 -and [Math]::Abs($near.position[1]-$found.y) -lt 2){$settled=$true;break}
  }
  if(-not $settled){$results+=@{id=$enemy.id;pass=$pass;result='STAGING_DID_NOT_SETTLE'};continue}
  $null=O "$prefix-staged-before-fight"
  try{$fight=B "$prefix-fight" @('fight',"entity=$($enemy.id)",'single=1','maxSeconds=35');$result=$fight.result}
  catch{
   $failed=$null;try{$failed=$_.ErrorDetails.Message|ConvertFrom-Json}catch{}
   if($failed.result -eq 'timeout'){
    $status=B "$prefix-terminal-status" @('status')
    if($status.batch.uncertain -or $status.batch.queued -ne 0){throw 'Fight ownership uncertain; no next staging action'}
    $result='BOUNDED_NATIVE_FIGHT_TIMEOUT'
   }
   elseif($_.ErrorDetails.Message -match 'obstructed|no enemy found'){$result='OBSTRUCTED_OR_ALREADY_GONE'}else{throw}
  }
  $now=O "$prefix-after";$results+=@{id=$enemy.id;pass=$pass;result=$result;state=$now.record.state;deaths=$now.savedEvidence.deathReceipts;credits=$now.supply.observedCredits}
  $results|ConvertTo-Json -Depth 8|Set-Content (Join-Path $Stage ($RunLabel+'-progress.json'))
  if($now.record.state -eq 'Cleared'){break}
 }
 $now=O "actor-route-pass$pass-final";if($now.record.state -eq 'Cleared'){break}
}
$null=B 'actor-route-audio-status' @('console','rbpurgeaudiotrace status');$null=B 'actor-route-audio-stop' @('console','rbpurgeaudiotrace stop')
$final=O 'actor-route-final'
@{state=$final.record.state;record=$final.record;deaths=$final.savedEvidence.deathReceipts;enrolled=$final.savedEvidence.enrolled;feedback=$final.feedbackTrace;supply=$final.supply;scope='Actual native actor combat after collision-checked fixture staging; not normal full-room walking acceptance.'}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $Stage ($RunLabel+'-verdict.json'))
Write-Output "Native actor route finished: $($final.record.state), $($final.savedEvidence.deathReceipts)/$($final.savedEvidence.enrolled) death receipts"

