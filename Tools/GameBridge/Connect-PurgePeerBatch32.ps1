param([ValidateSet('server','client')][string]$Role,[ValidateRange(32,99)][int]$Batch=32)
$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path;$mp=Join-Path $root 'Tools/Profiler/out/purge-remote-20261102';$stage=Join-Path $root ("Tools/Profiler/out/purge-b$Batch/"+$Role);New-Item -ItemType Directory -Force $stage|Out-Null
$private=Join-Path $stage 'control.private.json';if(Test-Path $private){throw 'Existing lease must be resumed, not reacquired'}
$owner=Get-Content (Join-Path $mp ($Role+'.owner.json')) -Raw|ConvertFrom-Json;$p=Get-Process -Id $owner.Pid
if($owner.Role -ne $Role -or $p.StartTime.ToUniversalTime() -ne [datetime]$owner.StartUtc -or $p.Path -ne $owner.Executable){throw 'Exact owned peer mismatch'}
$port=if($Role -eq 'server'){8863}else{8873};$deadline=[datetime]::UtcNow.AddMinutes(15);$session=$null;$path=$null
while([datetime]::UtcNow -lt $deadline){
 $files=@(Get-ChildItem (Join-Path $root 'Tools/GameBridge/out/sessions') -Directory -Filter ($owner.Pid.ToString()+'-*')|Sort-Object CreationTime -Descending)
 foreach($dir in $files){$path=Join-Path $dir.FullName 'session.json';if(-not(Test-Path $path)){continue};$candidate=Get-Content $path -Raw|ConvertFrom-Json;if($candidate.pid -eq $owner.Pid -and $candidate.port -eq $port){$session=$candidate;break}}
 if($session){break};Start-Sleep -Seconds 2
}
if(-not $session){throw 'No peer session; inspect same process'}
$lease=Invoke-RestMethod -Method Post -Uri ("http://127.0.0.1:$port/batch?action=acquire&run=purgebatch$Batch-$Role") -Headers @{'X-Bridge-Token'=$session.token} -TimeoutSec 30
if(-not $lease.runToken){throw 'No exclusive peer lease'}
@{pid=$owner.Pid;session=$path;runToken=$lease.runToken}|ConvertTo-Json|Set-Content $private
Copy-Item -LiteralPath $private -Destination (Join-Path $mp ($Role+'.private.json'))
while([datetime]::UtcNow -lt $deadline){
 $r=& "$PSScriptRoot/Invoke-PurgeRegression.ps1" -Stage $stage -Label ready-status -Direct -ArgumentsJson '["status"]';$s=$r|ConvertFrom-Json
 if(($Role -eq 'server' -and $s.state -eq 'server') -or ($Role -eq 'client' -and $s.state -eq 'menu')){Write-Output "Owned $Role ready, PID $($owner.Pid), state $($s.state)";return};Start-Sleep -Seconds 2
}
throw 'Readiness bound expired; retain same exact process/lease'
