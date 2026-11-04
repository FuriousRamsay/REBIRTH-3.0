param([Parameter(Mandatory=$true)][string]$Stage,[ValidateSet('Observe','RoundTrip','Access')][string]$Phase='Observe',[int]$LeaveX=47,[int]$LeaveY=42,[int]$LeaveZ=-254,[int]$CrateId=-1,[switch]$ResumeOpen)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1';$prefix='custody-'+$Phase+'-'+[datetime]::UtcNow.ToString('HHmmss')
function B($label,$a){$r=& $h -Stage $Stage -Label ($prefix+'-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($a) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
function Check($b,$why){if(-not $b){throw $why}}
$start=B 'state' @('state','-Sections','player,inventory');Check $start.guard.enabled 'Guard disabled'
$o=O 'before';Check ($o.saveName -eq 'CodexTestPurgeFresh20261102') 'Wrong disposable fixture'
if($Phase -eq 'Observe'){return}
$crates=@($o.nativeSupplyCrates|Where-Object {$_.loaded -and $_.ownedLoadedCrate -and ($CrateId -lt 0 -or $_.id -eq $CrateId)});Check ($crates.Count -eq 1) 'Need one genuinely owned loaded crate before mutation; inspect custody diagnostics'
$c=$crates[0];$id=$c.id
# Director cache positions can still contain the initial airborne height.
# Use two settled native entity observations before choosing a staging height.
$settled=$null;$previous=$null
for($i=0;$i -lt 30;$i++){
 $near=B ('landing-'+$i) @('entities','radius=4096','contains=sc_');$rows=@($near.entities|Where-Object {$_.id -eq $id})
 if($rows.Count -eq 1){$now=$rows[0].position;if($previous -and [Math]::Abs($now[1]-$previous[1]) -lt 0.1){$settled=$now;break};$previous=$now}
 Start-Sleep -Seconds 1
}
Check ($null -ne $settled) 'Owned crate has not settled; no airborne staging'
$x=[int][Math]::Floor($settled[0])-2;$y=[int][Math]::Floor($settled[1])+1;$z=[int][Math]::Floor($settled[2])
if($Phase -eq 'RoundTrip'){
 $null=B 'leave' @('console',"teleport $LeaveX $LeaveY $LeaveZ");$null=B 'leave-surroundings' @('surroundings');$unloaded=$false
 for($i=0;$i -lt 12;$i++){Start-Sleep -Seconds 2;$p=O ('away-'+$i);$row=@($p.nativeSupplyCrates|Where-Object {$_.id -eq $id});if($row.Count -eq 0 -or -not $row[0].loaded){$unloaded=$true;break}}
 Check $unloaded 'Native crate never unloaded: no round-trip qualification'
 $null=B 'return' @('console',"teleport $x $y $z");$null=B 'return-surroundings' @('surroundings');$loaded=$null
 for($i=0;$i -lt 12;$i++){Start-Sleep -Seconds 2;$p=O ('returned-'+$i);$row=@($p.nativeSupplyCrates|Where-Object {$_.id -eq $id -and $_.loaded});if($row.Count -eq 1){$loaded=$row[0];break}}
 Check ($null -ne $loaded) 'Crate did not reload';Check $loaded.ownedLoadedCrate 'Authoritative ownership lost: raw custody reasons preserved'
 Check ($p.supply.observedCredits -eq $o.supply.observedCredits -and $p.supply.deliveredDrops -eq $o.supply.deliveredDrops) 'Round trip changed ledger'
}
if($Phase -eq 'Access'){
 $null=B 'surroundings' @('surroundings')
 if(-not $ResumeOpen){$null=B 'activate-base' @('activate',"entity=$id",'part=base')}
 $opened=$false
 for($i=0;$i -lt 35;$i++){
  $ui=B ('looting-ui-'+$i) @('uitree')
  $opened=($ui.openWindows -contains 'looting') -or (($ui.openWindows -contains 'bagStorage') -and @($ui.nodes|Where-Object {$_.id -eq 'contextContainerName' -and $_.text -eq 'Supply Crate'}).Count -eq 1)
  if($opened){break}
  if($ResumeOpen){break}
  Start-Sleep -Seconds 1
 }
 Check $opened 'Native owned crate did not open after its native search timer'
 $null=B 'looting-shot' @('screenshot','custody-owned-loot');$null=B 'takeall' @('key','name=R');$afterUi=B 'after-looting-ui' @('uitree')
 if($afterUi.openWindows -contains 'looting' -or $afterUi.openWindows -contains 'bagStorage'){$null=B 'close' @('key','name=Escape')}
 $null=B 'after-inventory' @('state','-Sections','player,inventory');$null=O 'after-collection'
}
