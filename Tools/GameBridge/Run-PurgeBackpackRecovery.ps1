param([Parameter(Mandatory=$true)][string]$Stage,[Parameter(Mandatory=$true)][int]$BagId)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1';$prefix='backpack-'+$BagId
function B($label,$a){$r=& $h -Stage $Stage -Label ($prefix+'-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
$proof=B 'save-proof' @('console','rbpurgetestobserve');$o=$proof.results[0].output[0]|ConvertFrom-Json
if($o.saveName -ne 'CodexTestPurgeFresh20261102'){throw 'Wrong disposable save'}
$entities=B 'entity-proof' @('entities','radius=4096','contains=backpack');$bags=@($entities.entities|Where-Object {$_.id -eq $BagId -and $_.class -eq 'Backpack'})
if($bags.Count -ne 1){throw 'Exact previously observed death backpack unavailable'}
$p=$bags[0].position;$found=$null;$y=[int][Math]::Floor($p[1]+.05)
foreach($offset in @(@(-1,0),@(1,0),@(0,-1),@(0,1),@(-2,0),@(2,0))){
 $x=[int][Math]::Floor($p[0])+$offset[0];$z=[int][Math]::Floor($p[2])+$offset[1];$tag="$x-$y-$z"
 $f=B ($tag+'-foot') @('blockdamage',"x=$x","y=$y","z=$z");if($f.block -ne 'air'){continue}
 $head=B ($tag+'-head') @('blockdamage',"x=$x",('y='+($y+1)),"z=$z");$floor=B ($tag+'-floor') @('blockdamage',"x=$x",('y='+($y-1)),"z=$z")
 if($head.block -eq 'air' -and $floor.block -ne 'air' -and $floor.maxDamage -gt 1){$found=@{x=$x;y=$y;z=$z};break}
}
if(-not $found){throw 'No verified backpack-side staging floor'}
$null=B 'stage' @('console',"teleport $($found.x) $($found.y) $($found.z)")
$ready=$false;for($i=0;$i -lt 60;$i++){$s=B ('ready-'+$i) @('state','-Sections','player');if($s.state -eq 'ingame' -and $s.player.onGround -and -not $s.player.dead -and -not $s.player.flying){$ready=$true;break};Start-Sleep -Seconds 1}
if(-not $ready){throw 'Native arrival not ready'}
$null=B 'arrival' @('surroundings');$null=B 'activate' @('activate',"entity=$BagId",'part=base')
$open=$false;for($i=0;$i -lt 35;$i++){$u=B ('ui-'+$i) @('uitree');if($u.openWindows -contains 'bagStorage' -and @($u.nodes|Where-Object {$_.id -eq 'contextContainerName' -and $_.text -eq 'Backpack'}).Count -eq 1){$open=$true;break};Start-Sleep -Seconds 1}
if(-not $open){throw 'Exact native backpack did not open'}
$null=B 'takeall' @('key','name=R');$u=B 'after-ui' @('uitree');if($u.openWindows -contains 'bagStorage'){$null=B 'close' @('key','name=Escape')}
$null=B 'remaining-bags' @('entities','radius=100','contains=backpack');$null=B 'after-state' @('state','-Sections','player,inventory');$null=B 'after-observe' @('console','rbpurgetestobserve')
