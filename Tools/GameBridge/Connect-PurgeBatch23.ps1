param([Parameter(Mandatory=$true)][string]$Stage,[ValidateRange(23,99)][int]$Batch=23)
$ErrorActionPreference='Stop';$deadline=[datetime]::UtcNow.AddMinutes(15)
$owner=$null;$session=$null
while([datetime]::UtcNow -lt $deadline){
 if(Test-Path Tools/GameBridge/out/launch.json){
  $candidate=Get-Content Tools/GameBridge/out/launch.json -Raw|ConvertFrom-Json
  $process=Get-Process -Id $candidate.pid -ErrorAction SilentlyContinue
  if($process -and $process.StartTime.ToUniversalTime().Ticks -eq ([datetime]$candidate.processStartUtc).Ticks){
   if($candidate.save -ne 'CodexTestPurgeFresh20261102'){throw 'Unexpected live save'}
   $owner=$candidate
   $latest=Get-Content Tools/GameBridge/out/session.json -Raw -ErrorAction SilentlyContinue|ConvertFrom-Json
   if($latest -and $latest.pid -eq $owner.pid){$session=$latest;break}
  }
 }
 Start-Sleep -Seconds 5
}
if(-not $session){throw 'No matching live Bridge session within startup bound; inspect same process, do not relaunch'}
$private=Join-Path $Stage 'control.private.json'
if(Test-Path $private){throw 'Existing private control must be resumed, not reacquired'}
$lease=Invoke-RestMethod -Method Post -Uri ('http://127.0.0.1:'+$session.port+'/batch?action=acquire&run=purgebatch'+$Batch) -Headers @{'X-Bridge-Token'=$session.token} -TimeoutSec 30
if(-not $lease.runToken){throw 'No exclusive lease'}
@{pid=$owner.pid;session=(Join-Path $session.outputDir 'session.json');runToken=$lease.runToken}|ConvertTo-Json|Set-Content $private
while([datetime]::UtcNow -lt $deadline){
 $result=& (Join-Path $PSScriptRoot 'Invoke-PurgeRegression.ps1') -Stage $Stage -Label 'startup-status' -Direct -ArgumentsJson '["status"]'
 $status=($result -join "`n")|ConvertFrom-Json
 if($status.state -eq 'ingame'){Write-Output ('Ready owned game '+$owner.pid);exit 0}
 Start-Sleep -Seconds 5
}
throw 'Existing owned game has not reached ingame; retain lease and inspect, never duplicate launch'
