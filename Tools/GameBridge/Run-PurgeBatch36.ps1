param([int]$Batch=36)
$ErrorActionPreference='Stop';$base="Tools/Profiler/out/purge-b$Batch";$results=@()
function Mark($phase,$passed,$message){$script:results+=@{phase=$phase;passed=$passed;message=$message;time=[datetime]::UtcNow.ToString('o')};$script:results|ConvertTo-Json -Depth 4|Set-Content "$base/batch-results.json";Write-Output "$phase : $message"}
try{
 & "$PSScriptRoot/Run-PurgeRemoteBatch35.ps1" -Phase Connect -Batch $Batch;Mark Connect $true 'Native join ready'
 & "$PSScriptRoot/Run-PurgeRemoteBatch35.ps1" -Phase Baseline -Batch $Batch;Mark Baseline $true 'Native topology and fixture baseline'
 foreach($phase in @('Docks','Ranger')){
  try{& "$PSScriptRoot/Run-PurgeRemainingBatch36.ps1" -Phase $phase -Batch $Batch;Mark $phase $true 'Native retained clearance and client synchronization'}
  catch{
   Mark $phase $false $_.Exception.Message
   $raw=& "$PSScriptRoot/Invoke-PurgeRegression.ps1" -Stage "$base/client" -Label ("b$Batch-$phase-terminal-status") -Direct -ArgumentsJson '["status"]';$status=($raw -join "`n")|ConvertFrom-Json
   if($status.batch.uncertain -or $status.batch.queued -ne 0){throw 'Bridge request custody uncertain; no next test'}
  }
 }
 try{& "$PSScriptRoot/Run-PurgeRemoteBatch35.ps1" -Phase Disconnect -Batch $Batch;Mark Disconnect $true 'Native menu/lifecycle observed'}catch{Mark Disconnect $false $_.Exception.Message}
}finally{
 & "$PSScriptRoot/../Profiler/MpHost/Close-Peer.ps1" -Role client -Fixture purge;Mark ClientQuit $true 'Exact owned client terminal'
 & "$PSScriptRoot/../Profiler/MpHost/Close-Peer.ps1" -Role server -Fixture purge;Mark ServerQuit $true 'Exact owned server terminal'
}
