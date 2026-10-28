$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
Add-Type -Path (Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperTransferJournal.cs')
$journalRun=[Guid]::NewGuid().ToString('N')
$journalFolder=Join-Path $env:TEMP "CamperJournalTests/$journalRun"
$journal=[RebirthCamperTransferJournal]::new($journalFolder)
$operation=[Guid]::NewGuid()
$vehicle=[Guid]::NewGuid()
$before=[byte[]]@(1,2,3)
$after=[byte[]]@(4,5,6)
$prepared=$journal.Prepare($operation,$vehicle,'test-owner',7,$before,$after)
$before[0]=99
if($prepared.Before[0] -ne 1){throw 'Journal aliases supplied snapshots.'}
$copy=$prepared.After
$copy[0]=99
if($prepared.After[0] -ne 4){throw 'Journal exposes mutable stored snapshots.'}
$reopened=[RebirthCamperTransferJournal]::new($journalFolder)
if($reopened.Read($operation).State -ne 'Prepared'){throw 'Prepared receipt did not survive reopening.'}
function MustReject([scriptblock]$operationToReject){
  $refused=$false
  try{& $operationToReject | Out-Null}catch{$refused=$true}
  if(-not $refused){throw 'Unsafe journal operation was accepted.'}
}
MustReject {$journal.Prepare($operation,$vehicle,'different-owner',7,[byte[]]@(1,2,3),$after)}
MustReject {$journal.Advance($operation,'Prepared','Applied')}
$null=$journal.Advance($operation,'Prepared','Uncertain')
if($reopened.Read($operation).State -ne 'Uncertain'){throw 'Uncertain receipt did not survive reopening.'}
MustReject {$reopened.AssertAvailable($vehicle,[Guid]::NewGuid())}
$reopened.AssertAvailable([Guid]::NewGuid(),[Guid]::NewGuid())
MustReject {$journal.Advance($operation,'Prepared','Uncertain')}
$null=$journal.Advance($operation,'Uncertain','Applied')
if($reopened.Read($operation).State -ne 'Applied'){throw 'Applied receipt did not survive reopening.'}
$reopened.AssertAvailable($vehicle,[Guid]::NewGuid())
MustReject {$journal.Advance($operation,'Applied','Uncertain')}
$other=[Guid]::NewGuid()
$null=$journal.Prepare($other,$vehicle,'test-owner',8,[byte[]]@(7),[byte[]]@(8))
$null=$journal.Advance($other,'Prepared','Rejected')
if($reopened.Read($other).State -ne 'Rejected'){throw 'Rejected receipt did not survive reopening.'}
$appliedPath=Join-Path $journalFolder ($operation.ToString('N')+'.2.rcj')
$findApplied=$journal.GetType().GetMethod('FindApplied',[Reflection.BindingFlags]'Instance,NonPublic')
$matched=@($findApplied.Invoke($reopened,[object[]]@($vehicle,'test-owner')))
if($matched.Count -ne 1 -or $matched[0].Operation -ne $operation){throw 'Applied source correlation did not select the exact retained receipt.'}
if(@($findApplied.Invoke($reopened,[object[]]@($vehicle,'other-owner'))).Count -ne 0){throw 'Applied source correlation crossed owner scope.'}
if(@($findApplied.Invoke($reopened,[object[]]@([Guid]::NewGuid(),'test-owner'))).Count -ne 0){throw 'Applied source correlation crossed vehicle scope.'}
$bytes=[IO.File]::ReadAllBytes($appliedPath)
$bytes[4]=$bytes[4] -bxor 1
[IO.File]::WriteAllBytes($appliedPath,$bytes)
MustReject {$reopened.Read($operation)}
$result=[ordered]@{status='PASS_OFFLINE_JOURNAL';snapshotsIsolated=$true;reopenChecked=$true;phaseTransitionsChecked=$true;receiptReuseRejected=$true;corruptionRejected=$true;gameplayAcceptance=$false;temporaryEvidence=$journalFolder}
$reportDirectory=Join-Path $PSScriptRoot "reports/transfer_journal/$journalRun"
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $reportDirectory 'result.json') -Encoding UTF8
$result | ConvertTo-Json
