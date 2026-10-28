#requires -Version 7.0
param([switch]$Build)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$camperRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$camperRun=[Guid]::NewGuid().ToString('N')
$camperReportFolder=Join-Path $PSScriptRoot "reports/offline_checks/$camperRun"
$camperLogFolder=Join-Path $env:TEMP "camper_offline_checks/$camperRun"
New-Item -ItemType Directory -Path $camperReportFolder,$camperLogFolder -Force | Out-Null
$camperReport=[ordered]@{status='RUNNING';gameTested=$false;gameLaunched=$false;checks=@();logs=$camperLogFolder}
$camperTests=@(
 'Test-MobileStationAdmission.ps1',
 'Test-StationMaterialPayment.ps1',
 'Test-CamperMoltenSnapshot.ps1',
 'Test-CamperRelocationService.ps1',
 'Test-CamperProcessingRecovery.ps1',
 'Test-MobileQueuePublication.ps1',
 'Test-MobilePublicationWatch.ps1',
 'Test-MobileSaveRequestRelease.ps1',
 'Test-MobileRecoveryLease.ps1',
 'Test-MobileQueueProcessing.ps1',
 'Test-MobileRestoredQueueMatch.ps1',
 'Test-MobileQueueRecoveryWatch.ps1',
 'Test-MobileQueueOwnerBinding.ps1',
 'Test-MobileOfflineBinding.ps1',
 'Test-MobileOfflineCompletionWatch.ps1',
 'Test-MobileProcessingRegistration.ps1',
 'Test-MobileQueueArchiveRelease.ps1',
 'Test-CamperVehicleIdentity.ps1',
 'Test-CamperCompletionNativeBody.ps1',
 'Test-MobileOpenQueueHold.ps1',
 'Test-MobileOpenQueueProcessing.ps1',
 'Test-MobileNativeBatchRefund.ps1',
 'Test-MobileCounterRefund.ps1',
 'Test-MobileCounterRefundRecord.ps1',
 'Test-MobileMaterialQueue.ps1',
 'Test-MobilePartialCancellationRewards.ps1',
 'Test-MobileQueueProjection.ps1',
 'Test-MobileQueuePolicy.ps1',
 'Test-MobileCancellationQueuePlan.ps1',
 'Test-MobileCancellationCheckpoint.ps1',
 'Test-MobileTerminalQueueGate.ps1',
 'Test-MobileCancellationSave.ps1',
 'Test-MobileCancellationRemoval.ps1',
 'Test-MobileCancellationRecord.ps1',
 'Test-MobileCancellationWatch.ps1',
 'Test-MobileRefundBackpackPlan.ps1',
 'Test-MobileRefundDeliveryRecord.ps1',
 'Test-MobileRefundDelivery.ps1',
 'Test-MobileRemoteRefundPayload.ps1',
 'Test-MobileRemoteRefundDelivery.ps1',
 'Test-MobileRemoteCancellationWatch.ps1',
 'Test-MobileRefundRecovery.ps1',
 'Test-MobileRefundRecoveryWatch.ps1',
 'Test-MobileRefundArchive.ps1',
 'Test-MobileRefundArchiveService.ps1',
 'Test-MobileRefundPlayerFileWitness.ps1',
 'Test-MobileStationClock.ps1',
 'Test-MobileTrainingService.ps1',
 'Test-MobileTrainingRecoveryWatch.ps1',
 'Test-MobileRecoveryChoice.ps1',
 'Test-MobileNativeRewardReceipt.ps1',
 'Test-MobileNativeRewardDelivery.ps1',
 'Test-MobileRemoteRewardPayload.ps1',
 'Test-MobileReceiptRetirement.ps1',
 'Test-MobileCompletionArchive.ps1',
 'Test-MobileColdNativeReward.ps1',
 'Test-MobileColdRetirement.ps1',
 'Test-MobileCompletionHold.ps1',
 'Test-MobileBatchLimit.ps1',
 'Test-MobileAwardRetention.ps1',
 'Test-CamperTransferJournal.ps1',
 'Test-CamperStorageCapacity.ps1',
 'Test-CamperStorageContainer.ps1',
 'Test-CamperRemoteAdmission.ps1',
 'Test-CamperRemoteRegistration.ps1',
 'Test-CamperRemoteCursor.ps1',
 'Test-CamperRemoteProtocol.ps1',
 'Test-CamperRemoteOwnerApplication.ps1',
 'Test-CamperRemoteStationGrid.ps1',
 'Test-CamperRemoteStationClient.ps1',
 'Test-CamperRemoteStationLease.ps1',
 'Test-CamperStationPicker.ps1',
 'Test-CamperStationRetention.ps1',
 'Test-CamperStorageSafety.ps1',
 'Test-CamperCargoSlots.ps1',
 'Test-CamperPortablePlacement.ps1',
 'Test-CamperPortableCorrelation.ps1',
 'Test-CamperModuleInteraction.ps1',
 'Test-CamperLampState.ps1',
 'Test-VehicleSequenceBounds.ps1'
)
Push-Location $camperRoot
try {
 foreach($camperTest in $camperTests){
  $camperPath=Join-Path $PSScriptRoot $camperTest
  $camperLog=Join-Path $camperLogFolder ($camperTest+'.log')
  $camperTimer=[Diagnostics.Stopwatch]::StartNew()
  & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File $camperPath *> $camperLog
  $camperExit=$LASTEXITCODE
  $camperTimer.Stop()
  $camperReport.checks+=@{name=$camperTest;exitCode=$camperExit;seconds=$camperTimer.Elapsed.TotalSeconds;log=$camperLog}
  Write-Host "$camperTest : $(if($camperExit -eq 0){'PASS'}else{'FAIL'})"
 }
 if($Build){
  if(Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue){throw 'Game is running; build refused.'}
  $camperFrameworkRoot=(Resolve-Path '.nuget/packages/microsoft.netframework.referenceassemblies.net481/1.0.3/build').Path
  $camperLog=Join-Path $camperLogFolder 'build.log'
  & dotnet build RebirthUtils.csproj -c Debug --no-restore -nologo -v quiet "-p:TargetFrameworkRootPath=$camperFrameworkRoot" *> $camperLog
  $camperExit=$LASTEXITCODE
  $camperReport.checks+=@{name='Debug build';exitCode=$camperExit;log=$camperLog}
  Write-Host "Debug build : $(if($camperExit -eq 0){'PASS'}else{'FAIL'})"
 }
 $camperReport.status=if(@($camperReport.checks | Where-Object exitCode -ne 0).Count){'FAIL_OFFLINE_CHECKS'}else{'PASS_OFFLINE_CHECKS_NOT_GAMEPLAY_ACCEPTANCE'}
} catch {
 $camperReport.status='FAIL_OFFLINE_CHECKS'
 $camperReport.error=$_.Exception.Message
} finally {
 Pop-Location
 $camperReportPath=Join-Path $camperReportFolder 'result.json'
 $camperReport | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $camperReportPath -Encoding utf8
 Write-Host $camperReportPath
}
if($camperReport.status -like 'FAIL*'){exit 1}
