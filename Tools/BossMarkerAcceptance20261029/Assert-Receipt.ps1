param([Parameter(Mandatory)][string]$CapturePath,[Parameter(Mandatory)][string]$BaselinePath,[string]$PreviousCapturePath,
 [Parameter(Mandatory)][ValidateSet('loaded','unloaded','returned','removalInvoking','nativeRemoved','expired','rejoined')][string]$Phase)
$ErrorActionPreference='Stop';Set-StrictMode -Version Latest
function Need($condition,$message){if(!$condition){throw $message}}
function Yes($value){return $value -is [bool] -and $value}
function No($value){return $value -is [bool] -and !$value}
function Eq($a,$b,$label){Need ($null -ne $a -and $null -ne $b -and [string]$a -ceq [string]$b) "Identity differs: $label"}
function Pos($a,$b){Need (@($a).Count -eq 3 -and @($b).Count -eq 3) 'Position unavailable';for($i=0;$i -lt 3;$i++){Need ([double]::IsFinite([double]$a[$i]) -and [double]::IsFinite([double]$b[$i]) -and [Math]::Abs([double]$a[$i]-[double]$b[$i]) -le 0.01) 'Position differs'}}
function Qualified($c){
 Need ($c.captureSchema -eq 1) 'Unsupported capture envelope'
 $o=$c.observation
 Need ($o.schema -eq 1 -and (Yes $o.observationOnly) -and (Yes $o.accepted) -and (Yes $o.complete)) 'Rejected/incomplete observation'
 Eq $o.scope 'local-authoritative-host' 'authority scope';Eq $o.eventLookup 'available' 'event lookup'
 Need ((No $o.remotePresentationObserved) -and (No $o.diskDurabilityProved)) 'Unsupported remote/disk claim'
 Eq $o.save.gameName 'CodexTest' 'disposable save'
 Need ((Yes $o.save.registryLookupAvailable) -and (Yes $o.save.boundWorldMatches) -and (Yes $o.save.cachedPathMatchesBound) -and (No $o.save.loadFailed) -and (No $o.save.freshIsReadyGetterInvoked)) 'Unqualified raw save binding'
 Need ((No $o.save.boundPathTruncated) -and (No $o.save.cachedDirectoryTruncated)) 'Truncated save path'
 Need ((No $o.originalScan.truncated) -and $o.originalScan.total -eq $o.originalScan.examined -and $o.originalScan.matchingOriginalsExamined -eq 1) 'Incomplete/ambiguous original scan'
 foreach($field in @('eventOriginalLookup','nativeMarkerCounts','helperCurrentScope')){Need (Yes $o.completeness.$field) "Incomplete $field"}
 foreach($field in @('scopeWorldMatches','scopeNativeWorldMatches','scopeSaveMatchesObserved','scopeManagerMatches')){Need (Yes $o.helper.$field) "Unqualified helper $field"}
 Need ((No $o.helper.stoppedForWorld) -and (No $o.helper.scopeSaveDirectoryTruncated)) 'Stopped/truncated helper scope'
 Need ((Yes $o.markers.available) -and (Yes $o.markers.countsComplete) -and (No $o.markers.scanTruncated) -and (No $o.markers.rowsTruncated)) 'Incomplete native marker scan/rows'
 Need ($o.markers.totalIndexCount -eq $o.markers.examined -and @($o.markers.rows).Count -eq $o.markers.matchingIdAndClassCountExamined) 'Count/row inconsistency'
 Need ($o.markers.coincidentForeignIdSameClassCountExamined -eq 0) 'Coincident foreign marker: collision requires separate qualification'
 Need ((Yes $o.loaded.lookupAvailable) -and (No $o.loaded.aliveTestUsed)) 'Loaded lookup unavailable or unsupported alive filter'
 Need ($o.original.role -eq 3 -and $o.original.phase -eq 3 -and (No $o.original.abandoned) -and (Yes $o.original.nativeProofShapeValid) -and $o.original.nativeProof -cmatch '^[a-fA-F0-9]{64}$' -and (Yes $o.original.proofsAreInMemoryFieldsOnly)) 'Unqualified original publication'
 Need ($o.requestedEvent -cmatch '^[a-fA-F0-9]{32}$' -and $o.original.operation -cmatch '^[a-fA-F0-9]{32}$' -and $o.requestedRewardId -gt 0) 'Malformed exact identity'
 Need ([guid]$o.requestedEvent -ne [guid]::Empty -and [guid]$o.original.operation -ne [guid]::Empty -and ![string]::IsNullOrWhiteSpace($o.nativeWorld)) 'Empty original/event/world identity'
 Eq $o.requestedEvent $o.event.id 'requested event';Eq $o.requestedRewardId $o.original.id 'requested reward'
 Need ($o.event.currentRewardId -eq $o.original.id -or $o.event.currentRewardId -eq -1) 'Different current reward ID'
 foreach($r in @($o.markers.rows)){Eq $r.id $o.original.id 'nav ID';Eq $r.class 'FuriousRamsayDroppedLoot' 'nav class';Need (No $r.nameTruncated) 'Truncated marker name'}
 Need ($c.session.pid -gt 0 -and ![string]::IsNullOrWhiteSpace($c.session.processStartUtc)) 'Missing actual Bridge process session identity'
 return $o
}
function Loaded($o){
 Need ((Yes $o.loaded.present) -and (Yes $o.loaded.idClassBirthWorldMatch) -and (Yes $o.loaded.addedToChunk)) 'Original not loaded/identity differs'
 Eq $o.loaded.id $o.original.id 'loaded ID';Eq $o.loaded.class $o.original.class 'loaded class';Eq $o.loaded.born $o.original.born 'loaded birth'
 foreach($field in @('available','present','bytesBounded','comparisonAvailable','expectedBytesMatch','existingMatchesAvailable','existingMatches')){Need (Yes $o.loadedStamp.$field) "Unqualified loaded stamp $field"}
 Need ((No $o.loadedStamp.savedToNetwork) -and (No $o.loadedStamp.writeCallbackPresent) -and [string]$o.loadedStamp.expires -ceq '18446744073709551615' -and $o.loadedStamp.sha256 -cmatch '^[a-fA-F0-9]{64}$') 'Loaded stamp flags/hash differ'
 Need ($o.markers.matchingIdAndClassCountExamined -eq 1) 'Loaded original must have exactly one matching native marker'
 $r=@($o.markers.rows)[0];Eq $r.tracking 'Entity' 'native tracking'
 Need ((Yes $r.tracksLoadedReference) -and (No $r.helperOwnedReference) -and (Yes $r.positionAvailable) -and (No $o.helper.fallbackReferencePresent)) 'Native marker/ref or fallback dedup differs'
 Pos $r.position $o.loaded.position
}
function Available($o){
 Need ((Yes $o.event.rewardIssued) -and (Yes $o.event.rewardPlaced) -and (No $o.event.expiredAtSample) -and [decimal]$o.sampleWorldTime -lt [decimal]$o.event.rewardExpiresWorldTime -and (No $o.original.removalInvoking) -and (No $o.original.nativeRemoved)) 'Reward not available'
 Eq $o.event.currentRewardId $o.original.id 'available reward ID'
 Need ((Yes $o.helper.projectionPresent) -and (No $o.helper.projectionNameTruncated)) 'Original projection missing/truncated'
 Eq $o.helper.projectionOperation $o.original.operation 'projection operation'
}
function Withdrawn($o){
 Need ((No $o.helper.projectionPresent) -and (No $o.helper.fallbackReferencePresent)) 'Availability projection remains'
 if(Yes $o.loaded.present){Loaded $o}else{Need ((No $o.loaded.present) -and $o.markers.matchingIdAndClassCountExamined -eq 0) 'Withdrawn unloaded marker remains'}
}
$c=Get-Content -LiteralPath $CapturePath -Raw|ConvertFrom-Json
$b=Get-Content -LiteralPath $BaselinePath -Raw|ConvertFrom-Json
$o=Qualified $c;$base=Qualified $b;Available $base;Loaded $base
if($Phase -in @('returned','rejoined')){
 Need (![string]::IsNullOrWhiteSpace($PreviousCapturePath)) 'Return/rejoin requires prior qualified unloaded capture'
 $null=& $PSCommandPath -CapturePath $PreviousCapturePath -BaselinePath $BaselinePath -Phase unloaded
 $prior=Get-Content -LiteralPath $PreviousCapturePath -Raw|ConvertFrom-Json
 Need ([decimal]$o.sampleWorldTime -ge [decimal]$prior.observation.sampleWorldTime) 'World time predates prior unloaded capture'
 if($Phase -eq 'returned'){Need ([double]$o.sampleRealtime -gt [double]$prior.observation.sampleRealtime) 'Return must follow prior unloaded sample'}
}
foreach($field in @('nativeWorld','requestedEvent','requestedRewardId')){Eq $o.$field $base.$field $field}
foreach($field in @('operation','id','class','born','nativeProof')){Eq $o.original.$field $base.original.$field "original.$field"}
foreach($field in @('ownerStableId','rewardExpiresWorldTime')){Eq $o.event.$field $base.event.$field "event.$field"}
foreach($field in @('gameName','gameWorld','storage','directory','boundPath','cachedDirectory')){Eq $o.save.$field $base.save.$field "save.$field"}
Eq $o.actor.stableId $base.actor.stableId 'observing stable actor'
Need (![string]::IsNullOrWhiteSpace($o.actor.stableId) -and ![string]::IsNullOrWhiteSpace($o.event.ownerStableId)) 'Stable identity unavailable'
Need ([decimal]$o.sampleWorldTime -ge [decimal]$base.sampleWorldTime) 'World time predates baseline'
if($Phase -eq 'rejoined'){
 Need ($c.session.pid -ne $b.session.pid -or $c.session.processStartUtc -cne $b.session.processStartUtc) 'Rejoin requires a new actual Bridge process session'
}else{
 Eq $c.session.pid $b.session.pid 'Bridge process';Eq $c.session.processStartUtc $b.session.processStartUtc 'Bridge process start'
 Need ([double]$o.sampleRealtime -ge [double]$base.sampleRealtime) 'Sample predates baseline session'
}
switch($Phase){
 {$_ -in @('loaded','returned')} {Available $o;Loaded $o;Eq $o.loadedStamp.sha256 $base.loadedStamp.sha256 'original stamp bytes';break}
 {$_ -in @('unloaded','rejoined')} {
  Available $o;Need ((No $o.loaded.present) -and $o.markers.matchingIdAndClassCountExamined -eq 1) 'Unloaded original must have exactly one fallback'
  $r=@($o.markers.rows)[0];Eq $r.tracking 'Position' 'fallback tracking'
  Need ((Yes $r.helperOwnedReference) -and (No $r.tracksLoadedReference) -and (Yes $r.positionAvailable) -and (Yes $o.helper.fallbackReferencePresent) -and (Yes $o.helper.fallbackReferenceShapeMatches) -and (Yes $o.markers.helperOwnedReferenceSeenInExaminedIndex)) 'Fallback ownership/index differs'
  Need ((No $r.usingLocalizationId) -and (Yes $r.displayNameAvailable)) 'Fallback label unavailable'
  Eq $r.name $o.helper.projectionName 'fallback label';Eq $o.helper.projectionName $base.helper.projectionName 'original owner label'
  Pos $r.position $o.event.rewardPosition;Pos $o.helper.projectionPosition $o.event.rewardPosition;break
 }
 'removalInvoking' {Need ((Yes $o.original.removalInvoking) -and (No $o.original.nativeRemoved)) 'Not pending-only removal';Withdrawn $o}
 'nativeRemoved' {Need ((Yes $o.original.removalInvoking) -and (Yes $o.original.nativeRemoved) -and (Yes $o.original.removalProofShapeValid) -and $o.original.removalProof -cmatch '^[a-fA-F0-9]{64}$' -and (No $o.loaded.present)) 'Positive in-memory removal receipt/absence unavailable';Withdrawn $o}
 'expired' {Need ((Yes $o.event.expiredAtSample) -and [decimal]$o.sampleWorldTime -ge [decimal]$o.event.rewardExpiresWorldTime) 'Actual expiry not reached';Withdrawn $o}
}
[ordered]@{phase=$Phase;result='PASS_HOST_RECEIPT_AND_PROJECTION';event=$o.event.id;originalOperation=$o.original.operation;originalId=$o.original.id;
 markerCount=$o.markers.matchingIdAndClassCountExamined;nativeRemovedInMemory=$o.original.nativeRemoved;
 limits=@('in-memory receipt is not disk durability','absence is not a recorded unload cause','remote transport/rendering/multiplayer unverified','native pooling/crash boundaries unverified','collision cases excluded','separate phase samples do not prove every intervening frame')}|ConvertTo-Json -Depth 5
