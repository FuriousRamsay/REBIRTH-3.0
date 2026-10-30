param([string]$ScratchDirectory=(Join-Path $env:TEMP ('boss-receipt-oracle-'+[guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop';New-Item -ItemType Directory -Path $ScratchDirectory -Force|Out-Null
function Loaded {
 $op='11111111111111111111111111111111';$event='22222222222222222222222222222222';$hash='a'*64
 return @{captureSchema=1;session=@{pid=123;processStartUtc='2026-10-29T12:00:00Z'};observation=@{
 schema=1;observationOnly=$true;accepted=$true;complete=$true;scope='local-authoritative-host';eventLookup='available';nativeWorld='33333333333333333333333333333333';remotePresentationObserved=$false;diskDurabilityProved=$false;requestedEvent=$event;requestedRewardId=7;sampleRealtime=10;sampleWorldTime=100;
 actor=@{stableId='actor'};save=@{gameName='CodexTest';gameWorld='Fixture';storage=0;directory='fixture/CodexTest';boundPath='fixture/CodexTest/events';cachedDirectory='fixture/CodexTest';registryLookupAvailable=$true;boundWorldMatches=$true;cachedPathMatchesBound=$true;loadFailed=$false;freshIsReadyGetterInvoked=$false;boundPathTruncated=$false;cachedDirectoryTruncated=$false};
 originalScan=@{truncated=$false;total=1;examined=1;matchingOriginalsExamined=1};completeness=@{eventOriginalLookup=$true;nativeMarkerCounts=$true;helperCurrentScope=$true};
 event=@{id=$event;ownerStableId='actor';rewardIssued=$true;rewardPlaced=$true;currentRewardId=7;rewardExpiresWorldTime=1000;expiredAtSample=$false;rewardPosition=@(7,45,3)};
 original=@{operation=$op;id=7;class=1;born=12;role=3;phase=3;nativeProof=$hash;nativeProofShapeValid=$true;proofsAreInMemoryFieldsOnly=$true;abandoned=$false;removalInvoking=$false;nativeRemoved=$false;removalProof='';removalProofShapeValid=$false};
 loaded=@{lookupAvailable=$true;aliveTestUsed=$false;present=$true;idClassBirthWorldMatch=$true;addedToChunk=$true;id=7;class=1;born=12;position=@(7,45,3)};
 loadedStamp=@{available=$true;present=$true;bytesBounded=$true;comparisonAvailable=$true;expectedBytesMatch=$true;existingMatchesAvailable=$true;existingMatches=$true;savedToNetwork=$false;writeCallbackPresent=$false;expires='18446744073709551615';sha256=$hash};
 helper=@{scopeWorldMatches=$true;scopeNativeWorldMatches=$true;scopeSaveMatchesObserved=$true;scopeManagerMatches=$true;stoppedForWorld=$false;scopeSaveDirectoryTruncated=$false;projectionPresent=$true;projectionNameTruncated=$false;projectionOperation=$op;projectionName='Owner';projectionPosition=@(7,45,3);fallbackReferencePresent=$false;fallbackReferenceShapeMatches=$false};
 markers=@{available=$true;countsComplete=$true;scanTruncated=$false;rowsTruncated=$false;totalIndexCount=1;examined=1;matchingIdAndClassCountExamined=1;coincidentForeignIdSameClassCountExamined=0;helperOwnedReferenceSeenInExaminedIndex=$false;rows=@(@{id=7;class='FuriousRamsayDroppedLoot';tracking='Entity';name='Native';nameTruncated=$false;tracksLoadedReference=$true;helperOwnedReference=$false;positionAvailable=$true;position=@(7,45,3);usingLocalizationId=$false;displayNameAvailable=$true})}
 }}
}
function Unloaded {
 $c=Loaded;$o=$c.observation;$o.loaded.present=$false;$o.loadedStamp=@{available=$false;state='unavailable:entity-not-loaded';comparisonAvailable=$false;existingMatchesAvailable=$false};$o.helper.fallbackReferencePresent=$true;$o.helper.fallbackReferenceShapeMatches=$true;$o.markers.helperOwnedReferenceSeenInExaminedIndex=$true
 $r=$o.markers.rows[0];$r.tracking='Position';$r.tracksLoadedReference=$false;$r.helperOwnedReference=$true;$r.name='Owner';return $c
}
function Withdrawn {
 $c=Unloaded;$o=$c.observation;$o.helper.projectionPresent=$false;$o.helper.fallbackReferencePresent=$false;$o.markers.rows=@();$o.markers.matchingIdAndClassCountExamined=0;return $c
}
$base=Join-Path $ScratchDirectory 'baseline.json';Loaded|ConvertTo-Json -Depth 30|Set-Content $base
$prior=Join-Path $ScratchDirectory 'prior-unloaded.json';$u=Unloaded;$u.observation.sampleRealtime=20;$u|ConvertTo-Json -Depth 30|Set-Content $prior
$script:passed=0
function Case($name,$capture,$phase='unloaded',[bool]$accept=$false){
 if($phase -eq 'returned'){$capture.observation.sampleRealtime=30}
 $path=Join-Path $ScratchDirectory ($name+'.json');$capture|ConvertTo-Json -Depth 30|Set-Content $path
 $failed=$false;try{$null=& "$PSScriptRoot/Assert-Receipt.ps1" -CapturePath $path -BaselinePath $base -PreviousCapturePath $prior -Phase $phase}catch{$failed=$true;if($accept){Write-Output $_}}
 if($failed -eq $accept){throw "Unexpected receipt oracle result: $name"};$script:passed++;"PASS $name"
}
Case 'loaded-baseline' (Loaded) loaded $true
Case 'unloaded-owned-position' (Unloaded) unloaded $true
Case 'returned-native-dedup' (Loaded) returned $true
$c=Unloaded;$c.session.pid=456;$c.observation.sampleRealtime=1;Case 'new-process-rejoin-same-original' $c rejoined $true
$c=Withdrawn;$c.observation.original.removalInvoking=$true;Case 'pending-removal-unloaded' $c removalInvoking $true
$c=Loaded;$c.observation.original.removalInvoking=$true;$c.observation.helper.projectionPresent=$false;Case 'pending-removal-keeps-loaded-native' $c removalInvoking $true
$c=Withdrawn;$c.observation.original.removalInvoking=$true;$c.observation.original.nativeRemoved=$true;$c.observation.original.removalProof='b'*64;$c.observation.original.removalProofShapeValid=$true;$c.observation.event.currentRewardId=-1;Case 'positive-inmemory-removal-receipt' $c nativeRemoved $true
$c=Withdrawn;$c.observation.sampleWorldTime=1000;$c.observation.event.expiredAtSample=$true;Case 'expired-unloaded-withdrawal' $c expired $true
$c=Loaded;$c.observation.sampleWorldTime=1000;$c.observation.event.expiredAtSample=$true;$c.observation.helper.projectionPresent=$false;Case 'expired-loaded-native-retained' $c expired $true
$c=Unloaded;$c.observation.markers.rows=@();$c.observation.markers.matchingIdAndClassCountExamined=0;Case 'lost-marker-baseline-shaped-negative' $c
$c=Unloaded;$c.observation.markers.rows+=@($c.observation.markers.rows[0]);$c.observation.markers.matchingIdAndClassCountExamined=2;Case 'duplicate-position-negative' $c
$c=Loaded;$c.observation.markers.rows+=@($c.observation.markers.rows[0]);$c.observation.markers.matchingIdAndClassCountExamined=2;Case 'returned-duplicate-native-negative' $c returned
$c=Unloaded;$c.observation.markers.scanTruncated=$true;Case 'truncated-index' $c
$c=Unloaded;$c.observation.markers.rowsTruncated=$true;Case 'truncated-rows' $c
$c=Unloaded;$c.observation.markers.coincidentForeignIdSameClassCountExamined=1;Case 'coincident-foreign-collision' $c
$c=Unloaded;$c.observation.original.operation='44444444444444444444444444444444';Case 'replacement-operation' $c
$c=Unloaded;$c.observation.original.nativeProof='b'*64;Case 'changed-native-proof' $c
$c=Unloaded;$c.observation.nativeWorld='other';Case 'stale-world' $c
$c=Unloaded;$c.observation.complete=$false;Case 'incomplete-root' $c
$c=Unloaded;$c.observation.eventLookup='absent';Case 'missing-event-not-removal-ack' $c nativeRemoved
$c=Unloaded;$c.observation.helper.scopeManagerMatches=$false;Case 'stale-manager' $c
$c=Unloaded;$c.observation.helper.projectionOperation='wrong';Case 'stale-projection-operation' $c
$c=Unloaded;$c.observation.markers.rows[0].position=@(99,45,3);Case 'wrong-fallback-position' $c
$c=Unloaded;$c.observation.markers.rows[0].helperOwnedReference=$false;Case 'foreign-pooled-reference' $c
$c=Loaded;$c.observation.loadedStamp.expectedBytesMatch=$false;Case 'stamp-mismatch' $c returned
$c=Loaded;$c.observation.loaded.born=99;Case 'birth-reused' $c returned
$c=Unloaded;$c.observation.original.nativeRemoved=$true;Case 'already-removed-not-available' $c
$c=Withdrawn;$c.observation.original.removalInvoking=$true;Case 'pending-not-positive-removal' $c nativeRemoved
$c=Unloaded;Case 'same-session-not-rejoin-proof' $c rejoined
$c=Withdrawn;Case 'clock-not-expired' $c expired
$c=Unloaded;$c.observation.markers.Remove('countsComplete');Case 'missing-field-fails-closed' $c
$c=Unloaded;$c.observation.accepted='true';Case 'string-not-boolean-admission' $c
$u.observation.markers.rows=@();$u.observation.markers.matchingIdAndClassCountExamined=0;$u|ConvertTo-Json -Depth 30|Set-Content $prior
Case 'return-with-unqualified-prior-unloaded-negative' (Loaded) returned
$failed=$false;try{$null=& "$PSScriptRoot/Assert-Receipt.ps1" -CapturePath (Join-Path $ScratchDirectory 'returned-native-dedup.json') -BaselinePath $base -Phase returned}catch{$failed=$true}
if(!$failed){throw 'Return without prior unloaded capture was accepted'};$script:passed++;'PASS return-missing-prior-negative'
"TOTAL $passed receipt-oracle cases PASS; synthetic schema1 samples only, no native acceptance"
