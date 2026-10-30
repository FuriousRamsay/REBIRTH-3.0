param([Parameter(Mandatory)][string]$CapturePath,[Parameter(Mandatory)][string]$PlanPath,
 [Parameter(Mandatory)][ValidateSet('loaded','unloaded','returned','rejoined','withdrawn')][string]$Phase)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$c=Get-Content -LiteralPath $CapturePath -Raw|ConvertFrom-Json
$p=Get-Content -LiteralPath $PlanPath -Raw|ConvertFrom-Json
function Require($condition,$message){if(!$condition){throw $message}}
Require ($p.schema -eq 1 -and $c.schema -eq 1) 'Unsupported plan/capture schema'
Require ($p.rewardEntityId -gt 0 -and $c.entityId -eq $p.rewardEntityId) 'Exact reward ID required'
Require ($p.gameName -ceq 'CodexTest') 'Only disposable CodexTest is accepted'
Require ($c.phase -ceq $Phase) 'Capture phase does not match requested phase'
foreach($s in @($c.before,$c.after)){
 Require ($s.ok -eq $true -and $s.world.gameName -ceq $p.gameName -and $s.world.gameWorld -ceq $p.gameWorld) 'World/save mismatch or unavailable state'
 Require ($s.player.dead -eq $false -and $s.world.paused -eq $false) 'Live unpaused player required'
}
foreach($n in @($c.navBefore,$c.navAfter)){
 Require ($n.schema -eq 1 -and $n.observationOnly -eq $true -and $n.entityId -eq $p.rewardEntityId -and $n.world -ceq $p.nativeWorldGuid) 'Wrong/unavailable world or nav observation'
 Require ($n.navIndexAvailable -eq $true) 'Nav index unavailable'
}
Require ($c.entities.ok -eq $true -and $c.entities.count -eq @($c.entities.entities).Count) 'Loaded entity list unavailable/truncated'
$rows=@($c.entities.entities|Where-Object { $_.id -eq $p.rewardEntityId })
Require ($rows.Count -le 1) 'Duplicate loaded entity ID'
if($Phase -in @('loaded','returned')){
 Require ($rows.Count -eq 1 -and $rows[0].class -ceq 'RB_EventLootContainer') 'Exact original reward must be loaded'
 $track='Entity'
}elseif($Phase -in @('unloaded','rejoined')){
 Require ($rows.Count -eq 0) 'Expected reward absent from complete loaded entity list'
 $track='Position'
}else{Require ($rows.Count -eq 0) 'Withdrawn checkpoint requires entity absent';$track=''}
foreach($n in @($c.navBefore,$c.navAfter)){
 Require ($n.entityNavPresent -eq ($Phase -ne 'withdrawn')) 'Unexpected native nav-index presence'
 Require ($n.navTrackType -ceq $track) 'Native tracking type differs'
 if($Phase -ne 'withdrawn'){
  $expectedName=if($Phase -in @('loaded','returned')){$p.loadedNativeDisplayName}else{$p.ownerDisplayName}
  Require (![string]::IsNullOrWhiteSpace($expectedName) -and $n.navDisplayName -ceq $expectedName) 'Native displayed name differs'
 }
}
Require ($c.eventStatus.ok -eq $true -and $c.eventStatus.hadError -eq $false) 'Event status command failed'
$text=(@($c.eventStatus.results|ForEach-Object { @($_.output)+@($_.log|ForEach-Object {$_.msg}) }) -join "`n")
$eventLine=@($text -split "`r?`n"|Where-Object {$_ -match ('^\s*'+[regex]::Escape($p.eventId)+'\s+state=')})
if($Phase -ne 'withdrawn'){
 Require ($eventLine.Count -ge 1 -and @($eventLine|Where-Object {$_ -notmatch ' reward=True rewardPlaced=True(?:\s|$)'}).Count -eq 0) 'Staged event not issued/placed in current server status'
}
# Index presence is NOT a marker-count, original-receipt, expiry or rendered-position oracle.
[ordered]@{phase=$Phase;result='PASS_LIMITED_PROJECTION';entityId=$p.rewardEntityId;observedTracking=$track;
 checks='save/world, exact loaded entity listing, native nav index/type/name, server event issued/placed where applicable';
 unverified=@('deduplication/count/class/position of all NavObjects','original operation/native proof and receipt identity','rewardID-to-event binding beyond staging','expiry or durable removal acknowledgement','actual unload cause versus absence','inventory/access','network transport/multiplayer/pooling/crash boundaries','rendered map/compass') }|ConvertTo-Json -Depth 5
