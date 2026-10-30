param([string]$ScratchDirectory=(Join-Path $env:TEMP ('boss-marker-oracle-'+[guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $ScratchDirectory -Force|Out-Null
$plan=[ordered]@{schema=1;gameName='CodexTest';gameWorld='Fixture';nativeWorldGuid='world';eventId='event';rewardEntityId=7;ownerDisplayName='Owner';loadedNativeDisplayName='Owner'}
$pp=Join-Path $ScratchDirectory 'plan.json';$plan|ConvertTo-Json|Set-Content $pp
function Fixture([string]$phase='unloaded'){
 $state=@{ok=$true;world=@{gameName='CodexTest';gameWorld='Fixture';paused=$false};player=@{dead=$false}}
 $nav=@{schema=1;observationOnly=$true;entityId=7;world='world';navIndexAvailable=$true;entityNavPresent=$true;navTrackType='Position';navDisplayName='Owner'}
 return @{schema=1;phase=$phase;entityId=7;before=$state;after=$state;navBefore=$nav;navAfter=$nav;entities=@{ok=$true;count=0;entities=@()};eventStatus=@{ok=$true;hadError=$false;results=@(@{output=@();log=@(@{msg=" event state=Completed owner=Owner boss=5 supports=0 regulars=0 reward=True rewardPlaced=True"})})}}
}
$script:passed=0
function Case($name,$capture,[bool]$accept=$false){
 $cp=Join-Path $ScratchDirectory ($name+'.json');$capture|ConvertTo-Json -Depth 30|Set-Content $cp
 $failed=$false
 try{$null=& "$PSScriptRoot/Assert-Capture.ps1" -CapturePath $cp -PlanPath $pp -Phase $capture.phase}catch{$failed=$true}
 if($failed -eq $accept){throw "Oracle verdict differs: $name"};$script:passed++;"PASS $name"
}
Case 'unloaded-positive-limited-only' (Fixture) $true
$c=Fixture 'loaded';$c.entities.count=1;$c.entities.entities=@(@{id=7;class='RB_EventLootContainer'});$c.navBefore.navTrackType='Entity';Case 'loaded-positive-dead-loot-not-filtered' $c $true
$c=Fixture 'withdrawn';$c.navBefore.entityNavPresent=$false;$c.navBefore.navTrackType='';Case 'withdrawn-presence-only-no-reason-proof' $c $true
$c=Fixture;$c.navBefore.entityNavPresent=$false;Case 'lost-marker-baseline-shaped-negative' $c
$c=Fixture;$c.navBefore.navTrackType='Entity';Case 'loaded-marker-is-not-unloaded-fallback' $c
$c=Fixture;$c.entities.count=1;Case 'truncated-loaded-entity-list' $c
$c=Fixture;$c.before.world.gameName='RealSave';Case 'real-save-rejected' $c
$c=Fixture;$c.navBefore.world='other-world';Case 'stale-world-rejected' $c
$c=Fixture;$c.navBefore.navDisplayName='Foreign';Case 'foreign-owner-label-rejected' $c
$c=Fixture;$c.navBefore.navIndexAvailable=$false;Case 'unavailable-index-not-absence' $c
$c=Fixture;$c.eventStatus.hadError=$true;Case 'event-status-command-failure' $c
$c=Fixture;$c.eventStatus.results[0].log[0].msg='event state=Completed reward=True rewardPlaced=False';Case 'unplaced-event-not-positive' $c
$c=Fixture;$c.entities.entities=@(@{id=7;class='RB_EventLootContainer'});$c.entities.count=1;Case 'present-entity-not-unloaded' $c
$c=Fixture;$c.navBefore.Remove('navTrackType');Case 'missing-type-fails-closed' $c
$c=Fixture;$c.before.world.paused=$true;Case 'paused-capture-rejected' $c
"TOTAL $passed offline oracle checks PASS; synthetic captures only, no native acceptance"
