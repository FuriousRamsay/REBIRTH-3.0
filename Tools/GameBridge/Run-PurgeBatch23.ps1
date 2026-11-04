param([Parameter(Mandatory=$true)][string]$Stage,[ValidateSet('Snapshot','Weapons','Supply','Statistics')][string]$Phase,[int]$Attempt=1,[string]$OnlyWeapon)
$ErrorActionPreference='Stop';$h=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
$prefix=if($Attempt -eq 1){$Phase}else{$Phase+'-attempt'+$Attempt}
function B($label,$argsList){$r=& $h -Stage $Stage -Label ($prefix+'-'+$label) -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($argsList) -Compress);return (($r -join "`n")|ConvertFrom-Json)}
function O($label){$r=B $label @('console','rbpurgetestobserve');return ($r.results[0].output[0]|ConvertFrom-Json)}
function Check($ok,$why){if(-not $ok){throw $why}}
$start=B 'baseline' @('state','-Sections','player,inventory');Check $start.guard.enabled 'Guard disabled';$null=B 'surroundings' @('surroundings')
$before=O 'before';Check ($before.saveName -eq 'CodexTestPurgeFresh20261102') 'Wrong disposable fixture'
try{
 if($Phase -eq 'Snapshot'){
  Check $before.purgeEnabled 'Purge theme lost';Check ($before.feedbackTrace.total -eq 0) 'Reload replayed old feedback'
  $null=B 'map-open' @('key','name=M');$null=B 'map-ui' @('uitree');$null=B 'map-shot' @('screenshot','b23-restored-owned-supply');$null=B 'map-close' @('key','name=M')
  Check (@($before.nativeSupplyCrates|Where-Object {$_.ownedLoadedCrate}).Count -eq 1) 'Delivered owned crate did not recover'
 }
 if($Phase -eq 'Weapons'){
  $null=B 'safe-stage' @('console','teleport 47 42 -254','settime 3 9 0');Start-Sleep -Seconds 2;$null=B 'safe-surroundings' @('surroundings');$null=B 'restore' @('restore')
  foreach($weapon in @('gunMGT0PipeMachineGun','gunRifleT0PipeRifle')|Where-Object {-not $OnlyWeapon -or $_ -eq $OnlyWeapon}){
   $null=B ($weapon+'-fixture') @('call','POST','/loadout',"weapon=$weapon",'quality=3','ammo=ammo762mmBulletBall','ammoCount=60')
   $p=B ($weapon+'-cold-held') @('call','GET','/weaponprofile');Check $p.known 'No held profile'
   $null=B ($weapon+'-select-other') @('select','slot=2')
   $unheld=B ($weapon+'-true-unheld') @('call','GET','/weaponprofile','slot=0');Check ($unheld.seed -eq $p.seed) 'Unheld item changed'
   Check ($unheld.profile.reloadSeconds -gt 0) 'Unheld reload fallback missing'
   $null=B ($weapon+'-select') @('select','slot=1');$null=B ($weapon+'-initial-reload') @('key','name=R');Start-Sleep -Seconds 5
   $pre=B ($weapon+'-before-shot') @('call','GET','/weaponprofile')
   $null=B ($weapon+'-shot') @('press','action=Primary','seconds=0.2');Start-Sleep -Seconds 1;$null=B ($weapon+'-reload') @('key','name=R')
   $done=$false
   for($i=0;$i -lt 8;$i++){
    Start-Sleep -Seconds 2;$post=B ($weapon+'-timing-'+$i) @('call','GET','/weaponprofile')
    if($post.reloadTiming -and ($post.reloadTiming.acceptedCount+$post.reloadTiming.rejectedCount) -gt ($pre.reloadTiming.acceptedCount+$pre.reloadTiming.rejectedCount)){$done=$true;break}
   }
   Check $done 'No completed native reload receipt';Check ($post.seed -eq $p.seed) 'Native reload changed seed'
   if($post.reloadTiming.accepted){Check ($post.profile.reloadSource -eq 'measured') 'Accepted reload not used'}
   Check ($post.profile.reloadSeconds -gt 0 -and $post.profile.reloadSeconds -lt 10) 'Invalid/stall-sized reload contaminated profile'
   $null=B ($weapon+'-inventory-open') @('key','name=Tab');$null=B ($weapon+'-hover-away') @('hover','path=toolbelt/windowToolbelt/toolbelt/inventory/1');$null=B ($weapon+'-hover') @('hover','path=toolbelt/windowToolbelt/toolbelt/inventory/0')
   $null=B ($weapon+'-ui') @('uitree');$null=B ($weapon+'-tooltip') @('screenshot',('b23-'+$weapon));$null=B ($weapon+'-inventory-close') @('key','name=Escape')
  }
 }
 if($Phase -eq 'Supply'){
  $crates=@($before.nativeSupplyCrates|Where-Object {$_.loaded -and $_.ownedLoadedCrate})
  if($crates.Count -eq 0){
   # Weapon phase visits the safe station, unloading the actual restored crate.
   # Recover only its observed location, then require fresh loaded ownership proof.
   $snapshot=Get-Content (Join-Path $Stage 'Snapshot-before.json') -Raw|ConvertFrom-Json
   $snapshot=$snapshot.results[0].output[0]|ConvertFrom-Json
   Check ($snapshot.saveName -eq $before.saveName -and $snapshot.supply.deliveredDrops -eq $before.supply.deliveredDrops) 'Supply context changed since snapshot'
   $crates=@($snapshot.nativeSupplyCrates|Where-Object {$_.loaded -and $_.ownedLoadedCrate})
  }
  Check ($crates.Count -eq 1) 'Expected one observed owned crate location'
  $c=$crates[0];$x=[int]$c.position[0]-2;$y=[int]$c.position[1]+1;$z=[int]$c.position[2]
  $null=B 'stage' @('console',"teleport $x $y $z");Start-Sleep -Seconds 2;$null=B 'arrival-surroundings' @('surroundings')
  $arrived=O 'arrived';$liveCrates=@($arrived.nativeSupplyCrates|Where-Object {$_.loaded -and $_.ownedLoadedCrate});Check ($liveCrates.Count -eq 1) 'Crate not loaded/owned on arrival';$c=$liveCrates[0]
  $null=B 'open-base-aim' @('activate',"entity=$($c.id)",'part=base');$ui=B 'open-ui' @('uitree');Check ($ui.openWindows -contains 'looting') 'Owned crate did not open'
  $null=B 'open-shot' @('screenshot','b23-owned-supply-loot');$null=B 'takeall' @('key','name=R');$null=B 'after-ui' @('uitree');$null=B 'close' @('key','name=Escape')
  Start-Sleep -Seconds 2;$after=O 'after';Check ($after.supply.observedCredits -eq $before.supply.observedCredits) 'Collection duplicated supply credits'
  Check ($after.supply.deliveredDrops -eq $before.supply.deliveredDrops) 'Collection changed delivered award count'
  Check (@($after.nativeSupplyCrates|Where-Object {$_.ownedLoadedCrate}).Count -eq 0) 'Owned crate remains: inspect loot/capacity before retry'
  $null=B 'map-open' @('key','name=M');$null=B 'map-ui' @('uitree');$null=B 'map-shot' @('screenshot','b23-collected-supply-map');$null=B 'map-close' @('key','name=M')
 }
 if($Phase -eq 'Statistics'){
  $null=B 'safe-stage' @('console','teleport 47 42 -254');Start-Sleep -Seconds 2;$null=B 'arrival-surroundings' @('surroundings')
  $null=B 'inventory' @('key','name=Tab');$null=B 'character' @('click','text=CHARACTER');$null=B 'purge-tab' @('click','id=btnSurvivorTabPurge')
  $null=B 'summary-ui' @('uitree');$null=B 'summary-shot' @('screenshot','b23-purge-statistics')
  foreach($filter in @(1,2,0)){$null=B ('filter-'+$filter) @('click',"id=purgeStatsFilter$filter");$null=B ('filter-ui-'+$filter) @('uitree');$null=B ('filter-shot-'+$filter) @('screenshot',"b23-purge-filter-$filter")}
  foreach($delta in @(-12,12)){$null=B ('scroll-'+$delta) @('call','POST','/ui/scroll','id=purgeStatsPoiScroll',"delta=$delta");$null=B ('scroll-ui-'+$delta) @('uitree');$null=B ('scroll-shot-'+$delta) @('screenshot',"b23-purge-scroll-$delta")}
  $null=B 'close' @('key','name=Escape')
 }
 $null=B 'errors' @('errors','-Since',([string]$start.lastLogSeq))
 @{phase=$Phase;result='STRUCTURAL_PASS_REQUIRES_EVIDENCE_REVIEW';scope='Native actions/observations only; visual, timing accuracy, inventory deltas and statistics totals require independent review.'}|ConvertTo-Json|Set-Content (Join-Path $Stage ($prefix+'-verdict.json'))
}catch{@{phase=$Phase;result='FAIL';error=$_.Exception.Message}|ConvertTo-Json|Set-Content (Join-Path $Stage ($prefix+'-verdict.json'));throw}
