param([Parameter(Mandatory=$true)][string]$Stage)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1';$run='room-entry-'+[datetime]::UtcNow.ToString('HHmmss')
function B($label,$a){$r=& $h -Stage $Stage -Label ($run+'-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
$before=O 'before';if($before.saveName -ne 'CodexTestPurgeFresh20261102' -or $before.scannerNativePoi.prefab -ne 'army_camp_05'){throw 'Wrong disposable Fort'}
$rows=@()
foreach($room in @($before.authoredRoomRegistration|Where-Object {-not $_.cleared})){
 $label='r'+$room.nativeId;$found=$null;$cx=[int][Math]::Floor(($room.minimum[0]+$room.maximum[0])/2);$cz=[int][Math]::Floor(($room.minimum[2]+$room.maximum[2])/2)
 for($y=[int]$room.minimum[1];$y -lt [int]$room.maximum[1] -and -not $found;$y++){
  foreach($off in @(@(0,0),@(1,0),@(0,1),@(-1,0),@(0,-1))){
   $x=$cx+$off[0];$z=$cz+$off[1];if($x -lt $room.minimum[0] -or $x -ge $room.maximum[0] -or $z -lt $room.minimum[2] -or $z -ge $room.maximum[2]){continue}
   $f=B "$label-$x-$y-$z-foot" @('blockdamage',"x=$x","y=$y","z=$z");if($f.block -ne 'air'){continue}
   $head=B "$label-$x-$y-$z-head" @('blockdamage',"x=$x",('y='+($y+1)),"z=$z");$floor=B "$label-$x-$y-$z-floor" @('blockdamage',"x=$x",('y='+($y-1)),"z=$z")
   if($head.block -eq 'air' -and $floor.block -ne 'air' -and $floor.maxDamage -gt 1){$found=@{x=$x;y=$y;z=$z};break}
  }
 }
 if(-not $found){$rows+=@{room=$room.nativeId;result='NO_VERIFIED_INTERIOR_FLOOR'};continue}
 $null=B ($label+'-stage') @('console',"teleport $($found.x) $($found.y) $($found.z)");Start-Sleep -Seconds 1;$null=B ($label+'-surroundings') @('surroundings')
 $after=O ($label+'-after');$rows+=@{room=$room.nativeId;point=$found;position=$after.position;state=$after.record.state;enrolled=$after.savedEvidence.enrolled;deaths=$after.savedEvidence.deathReceipts}
 $rows|ConvertTo-Json -Depth 6|Set-Content (Join-Path $Stage ($run+'-progress.json'))
}
$null=O 'final';Write-Output $run
