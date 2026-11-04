param([Parameter(Mandatory=$true)][string]$Stage,[ValidateSet('Baseline','Ordinary','ThreeRoom','Exclusions','Options')][string]$Phase,[int]$Attempt=1)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1';$prefix='ordinary28-'+$Phase+'-'+$Attempt
function B($label,$a){$r=& $h -Stage $Stage -Label ($prefix+'-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
function TerminalFailure($label){$s=B ($label+'-status') @('status');if($s.batch.uncertain -or $s.batch.queued -ne 0){throw 'Uncertain input ownership: reconcile exact action before continuing'}}
$baseline=O 'baseline';if($baseline.saveName -ne 'CodexTestPurgeFresh20261102'){throw 'Wrong disposable save'}
$s=B 'state' @('state','-Sections','player,world');if(-not $s.guard.enabled -or -not $s.guard.heal -or -not $s.guard.reload){throw 'Guard/heal/reload must stay enabled'}
if($Phase -eq 'Baseline'){
 if(-not $baseline.biomeSpawnTrace.installed){throw 'Native spawn observation hook not installed'}
 $null=B 'surroundings' @('surroundings');$null=B 'daylight' @('console','settime 4 9 0')
 $null=B 'ui' @('uitree');$null=B 'shot' @('screenshot','b28-baseline');return
}
if($Phase -eq 'Options'){
 $null=B 'safe-stage' @('console','teleport 47 42 -254');$null=B 'arrival' @('surroundings')
 $null=B 'pause' @('key','name=Escape');$null=B 'open' @('click','id=btnSandboxSettings');$null=B 'ui' @('uitree');$null=B 'shot' @('screenshot','b28-saved-purge-options');return
}
if($Phase -eq 'Exclusions'){
 foreach($c in @(@{name='rwg_tile_gateway_t';x=114;y=46;z=-336},@{name='lot_rural_filler_27';x=-328;y=43;z=503})){
  $before=O ($c.name+'-before');$null=B ($c.name+'-stage') @('console',"teleport $($c.x) $($c.y) $($c.z)")
  Start-Sleep -Seconds 2;$null=B ($c.name+'-arrival') @('surroundings');$o=O ($c.name+'-after')
  $targets=@($o.manualResetDiagnostic.targets|Where-Object {$_.prefab -eq $c.name})
  @{candidate=$c;targets=$targets;beforeProgress=$before.progression;afterProgress=$o.progression;scope='Raw native target must be present and have zero definitions/no durable record; overlapping eligible POIs are not this target.'}|ConvertTo-Json -Depth 9|Set-Content (Join-Path $Stage ($prefix+'-'+$c.name+'-review.json'))
 }
 return
}
$c=if($Phase -eq 'Ordinary'){@{name='cabin_05';x=-589;y=48;z=-860}}else{@{name='site_shack_10';x=745;y=49;z=-830}}
$null=B 'outside-stage' @('console',"teleport $($c.x) $($c.y) $($c.z)");Start-Sleep -Seconds 2;$null=B 'outside-arrival' @('surroundings');$outside=O 'outside'
if($outside.scannerNativePoi.prefab -eq $c.name){throw 'Fixture landed inside; ordinary entry not qualified'}
$null=B 'nearby-pois' @('pois','radius=250','limit=80','maxTier=6')
$null=B 'approach' @('call','POST','/gotopoi',('name='+$c.name),'maxSeconds=35');Start-Sleep -Seconds 2;$null=O 'boundary'
try{$null=B 'enter' @('call','POST','/enterpoi',('name='+$c.name),'maxSeconds=40')}catch{TerminalFailure 'enter';Write-Output 'Native entry refused; retaining raw failure and checking actual position'}
Start-Sleep -Seconds 2;$inside=O 'inside';$null=B 'entry-shot' @('screenshot',('b28-'+$c.name+'-ordinary-entry'))
if($inside.scannerNativePoi.prefab -ne $c.name){throw 'Did not enter expected native prefab; no inside teleport fallback'}
$rows=@();$rooms=@($inside.authoredRoomRegistration|Where-Object {$_.authoredMax -gt 0 -or $_.script})
foreach($room in $rooms){
 $label='room-'+$room.nativeId;$o=O ($label+'-before');if($o.record.state -eq 'Cleared'){break}
 if(@($o.authoredRoomRegistration|Where-Object {$_.nativeId -eq $room.nativeId -and $_.cleared}).Count){continue}
 $found=$null;$cx=[int][Math]::Floor(($room.minimum[0]+$room.maximum[0])/2);$cz=[int][Math]::Floor(($room.minimum[2]+$room.maximum[2])/2)
 for($y=[int]$room.minimum[1];$y -lt [int]$room.maximum[1] -and -not $found;$y++){
  foreach($off in @(@(0,0),@(1,0),@(0,1),@(-1,0),@(0,-1))){
   $x=$cx+$off[0];$z=$cz+$off[1];if($x -lt $room.minimum[0] -or $x -ge $room.maximum[0] -or $z -lt $room.minimum[2] -or $z -ge $room.maximum[2]){continue}
   $f=B "$label-$x-$y-$z-foot" @('blockdamage',"x=$x","y=$y","z=$z");if($f.block -ne 'air'){continue}
   $head=B "$label-$x-$y-$z-head" @('blockdamage',"x=$x",('y='+($y+1)),"z=$z");$floor=B "$label-$x-$y-$z-floor" @('blockdamage',"x=$x",('y='+($y-1)),"z=$z")
   if($head.block -eq 'air' -and $floor.block -ne 'air' -and $floor.maxDamage -gt 1){$found=@{x=$x;y=$y;z=$z};break}
  }
 }
 if(-not $found){$rows+=@{room=$room.nativeId;result='NO_VERIFIED_INTERIOR_FLOOR'};continue}
 try{$null=B ($label+'-route') @('call','POST','/routeto',('x='+$found.x),('y='+$found.y),('z='+$found.z),'maxSeconds=35')}catch{TerminalFailure $label}
 $after=O ($label+'-after');$rows+=@{room=$room.nativeId;requested=$found;position=$after.position;record=$after.record;scanner=$after.scanner;markers=$after.visibleMarkerCount}
 $rows|ConvertTo-Json -Depth 8|Set-Content (Join-Path $Stage ($prefix+'-route-observations.json'))
}
$final=O 'final';$null=B 'final-shot' @('screenshot',('b28-'+$c.name+'-ordinary-final'))
@{prefab=$c.name;record=$final.record;evidence=$final.savedEvidence;scanner=$final.scanner;spawnTrace=$final.biomeSpawnTrace;scope='Native approach, entry and real 3D routes only; no sleeper-force/reset, inside teleport, godmode or direct damage. Raw routes/positions determine traversal acceptance.'}|ConvertTo-Json -Depth 12|Set-Content (Join-Path $Stage ($prefix+'-verdict.json'))
