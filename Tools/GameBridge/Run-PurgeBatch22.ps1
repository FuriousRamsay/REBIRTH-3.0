param([Parameter(Mandatory=$true)][string]$Stage,[ValidateSet('Inventory','Weapons','Discovery','Snapshot')][string]$Phase='Snapshot',[ValidateRange(1,5)][int]$StartTier=1)
$ErrorActionPreference='Stop'
$results=[Collections.Generic.List[object]]::new()
$helper=Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1'
function B([string]$label,[object[]]$arguments){
 $out=& $helper -Stage $Stage -Label $label -Direct -ArgumentsJson (ConvertTo-Json -InputObject @($arguments) -Compress -Depth 10)
 return (($out -join "`n")|ConvertFrom-Json)
}
function Check($value,[string]$message){if(-not $value){throw $message}}
function Observe([string]$label){$r=B $label @('console','rbpurgetestobserve');Check (-not $r.hadError) 'Purge observer failed';return ($r.results[0].output[0]|ConvertFrom-Json)}
function LearningCount($state){$total=0;foreach($s in (@($state.toolbelt)+@($state.backpack))){if($script:reading.Contains([string]$s.name)){$total+=[int]$s.count}};return $total}
function RunCase([string]$name,[scriptblock]$body){
 $status=B ($name+'-status') @('status');Check ($status.state -eq 'ingame' -and -not $status.batch.uncertain) 'World or request custody not ready'
 try{$script:caseVerdict='PASS';& $body;$results.Add(@{case=$name;result=$script:caseVerdict})}catch{$results.Add(@{case=$name;result='FAIL';error=$_.Exception.Message});$results|ConvertTo-Json -Depth 8|Set-Content (Join-Path $Stage ($Phase+'-results.json'));throw}
 $results|ConvertTo-Json -Depth 8|Set-Content (Join-Path $Stage ($Phase+'-results.json'))
}
$start=B ($Phase+'-baseline') @('state','-Sections','player,inventory,ui')
Check ($start.guard.enabled -eq $true) 'Guard must stay enabled'
$null=B ($Phase+'-surroundings') @('surroundings')
$before=Observe ($Phase+'-before')
Check ($before.saveName -eq 'CodexTestPurgeFresh20261102') 'Wrong disposable fixture'
if($Phase -eq 'Snapshot'){
 RunCase 'load-feedback-replay' {
  Check ($before.purgeEnabled) 'Saved Purge theme was lost'
  Check ($before.feedbackTrace.total -eq 0) 'Loading saved discoveries/clears replayed feedback'
  Check ($before.record.state -eq 'Cleared') 'Previously cleared station was not restored'
  $null=B 'load-restored-map-key' @('key','name=M')
  $null=B 'load-restored-map-shot' @('screenshot','b22-restored-map')
  $null=B 'load-restored-map-close' @('key','name=M')
 }
}
if($Phase -eq 'Inventory'){
 $script:reading=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 [xml]$registry=Get-Content (Join-Path $PSScriptRoot '../../Config/_Survivor/literature.xml')
 foreach($n in $registry.SelectNodes('//item[@id]')){$null=$script:reading.Add($n.id)}
 foreach($tier in $StartTier..5){
  RunCase "bundle-tier$tier" {
   $pre=B "bundle$tier-before" @('state','-Sections','inventory')
   $null=B "bundle$tier-give" @('give',"item=questRewardT${tier}BookBundle",'toolbelt=3')
   $null=B "bundle$tier-select" @('select','slot=3')
   $null=B "bundle$tier-open" @('press','action=Primary','seconds=0.2')
   Start-Sleep -Milliseconds 1200
   $post=B "bundle$tier-after" @('state','-Sections','inventory')
   $delta=(LearningCount $post)-(LearningCount $pre)
   Check ($delta -eq ($tier+2)) "Expected $($tier+2) actual Rebirth learning items; got $delta"
  }
 }
 $export=B 'native-export-configs' @('console','exportcurrentconfigs');Check (-not $export.hadError) 'Native config export failed'
}
if($Phase -eq 'Weapons'){
 $null=B 'weapons-daylight' @('console','settime 2 9 0')
 foreach($weapon in @('gunMGT0PipeMachineGun','gunRifleT0PipeRifle')){
  foreach($quality in @(2,3,5)){
   $name="$weapon-q$quality"
   RunCase $name {
    $null=B ($name+'-loadout') @('call','POST','/loadout',"weapon=$weapon","quality=$quality",'ammo=ammo762mmBulletBall','ammoCount=60')
    $preview=B ($name+'-preview') @('call','GET','/weaponprofile','slot=0')
    Check ($preview.known) 'Weapon profile unresolved'
    if($weapon -eq 'gunMGT0PipeMachineGun'){Check ($preview.profile.reloadSeconds -gt 0 -and $preview.profile.sustainedDps -lt $preview.profile.burstDps) 'PipeMG must include reload downtime'}
    $null=B ($name+'-reload') @('key','name=R')
    Start-Sleep -Seconds 5
    $null=B ($name+'-fire') @('press','action=Primary','seconds=0.2')
    $null=B ($name+'-reload-after-shot') @('key','name=R')
    Start-Sleep -Seconds 5
    $held=B ($name+'-held-after-reload') @('call','GET','/weaponprofile')
    Check ($held.seed -eq $preview.seed -and $held.quality -eq $preview.quality) 'Test changed item instance'
    Check ($held.profile.reloadSeconds -gt 0) 'Reload time was lost'
    if($held.profile.reloadSource -ne 'measured'){$script:caseVerdict='PARTIAL_RELOAD_NOT_MEASURED'}
   }
  }
 }
}
if($Phase -eq 'Weapons'){
 foreach($weapon in @('gunMGT0PipeMachineGun','gunRifleT0PipeRifle')){
  $name=$weapon+'-q3-flashlight'
  RunCase $name {
   $null=B ($name+'-give') @('give',"item=$weapon",'quality=3','mods=modGunFlashlight','toolbelt=1')
   $null=B ($name+'-select') @('select','slot=1')
   $beforeMod=B ($name+'-profile') @('call','GET','/weaponprofile')
   Check ($beforeMod.modifications -contains 'modGunFlashlight') 'Flashlight fixture missing'
   Check ($beforeMod.profile.reloadSeconds -gt 0) 'Flashlight profile lost reload'
   if($weapon -eq 'gunMGT0PipeMachineGun'){Check ($beforeMod.profile.sustainedDps -lt $beforeMod.profile.burstDps) 'Modded MG reverted to burst'}
   $null=B ($name+'-inventory-open') @('key','name=Tab')
   $null=B ($name+'-hover') @('hover','path=toolbelt/windowToolbelt/toolbelt/inventory/0')
   $null=B ($name+'-ui') @('uitree')
   $null=B ($name+'-shot') @('screenshot',$name)
   $null=B ($name+'-inventory-close') @('key','name=Tab')
  }
 }
}
if($Phase -eq 'Discovery'){
 # Stage outside three actual generated cabin bounds, then cross on foot. No forced discovery, room or kill credit.
 $candidates=@(@{name='cabin_14';x=482;y=45;z=-1;insideX=490},@{name='cabin_10';x=-428;y=63;z=22;insideX=-420},@{name='cabin_13';x=760;y=48;z=-288;insideX=768})
 foreach($c in $candidates){
  $name='discovery-'+$c.name
  RunCase $name {
   $null=B ($name+'-stage') @('console',"teleport $($c.x) $($c.y) $($c.z)")
   Start-Sleep -Seconds 3
   $null=B ($name+'-surroundings') @('surroundings')
   $prior=Observe ($name+'-before')
   $null=B ($name+'-audio-start') @('console','rbpurgeaudiotrace start 30')
   $null=B ($name+'-walk') @('walkto',"x=$($c.insideX)","z=$($c.z)")
   $after=Observe ($name+'-after')
   $null=B ($name+'-ui') @('uitree')
   $null=B ($name+'-shot') @('screenshot',$name)
   $null=B ($name+'-audio-status') @('console','rbpurgeaudiotrace status')
   $null=B ($name+'-audio-stop') @('console','rbpurgeaudiotrace stop')
   Check ($after.scannerNativePoi.prefab -eq $c.name) 'Traversal did not enter intended POI'
   $notices=@($after.feedbackTrace.events|Where-Object {$_.sequence -gt $prior.feedbackTrace.total -and $_.prefab -eq $c.name -and $_.kind -eq 'discovery-tooltip'})
   if($notices.Count -eq 0){$script:caseVerdict='UNQUALIFIED_NEW_DISCOVERY'}
   else{
    Check ($notices.Count -eq 1) 'Duplicate discovery tooltip'
    $audioCalls=@($after.feedbackTrace.events|Where-Object {$_.sequence -gt $prior.feedbackTrace.total -and $_.prefab -eq $c.name -and $_.kind -eq 'discovery-audio'})
    Check ($audioCalls.Count -eq 1) 'Missing or duplicate discovery audio dispatch'
    Check ($notices[0].value -notmatch '\{\d+\}') 'Unresolved discovery placeholders'
   }
   # Keep raw data: an already-discovered candidate is not a new-discovery PASS.
  }
 }
}
$end=Observe ($Phase+'-after')
$null=B ($Phase+'-errors') @('errors','-Since',([string]$start.lastLogSeq))
$results|ConvertTo-Json -Depth 8







