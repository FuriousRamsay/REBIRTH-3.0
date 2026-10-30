[CmdletBinding()]
param(
 [string]$PolicyPath='',
 [string]$UnityPath='C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Unity.exe'
)
$ErrorActionPreference='Stop'
if([string]::IsNullOrWhiteSpace($PolicyPath)){$PolicyPath=Join-Path $PSScriptRoot 'BuildProfiles/BuggyBike.json'}
$policyFile=(Resolve-Path -LiteralPath $PolicyPath).Path
$policy=Get-Content -LiteralPath $policyFile -Raw|ConvertFrom-Json
$project=[IO.Path]::GetFullPath($policy.sourceProject)
if(!(Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt'))){throw 'Unity project is missing'}
if(!(Test-Path -LiteralPath $UnityPath)){throw 'Unity editor is missing'}
# Coordination is read-only; this never edits HAV3N files or controls its processes.
$coord='E:/_Haven/shared/coordination/HAV3N_to_codex.txt'
if(Test-Path -LiteralPath $coord){$text=Get-Content -LiteralPath $coord -Raw;if($text -match '(?m)^STATUS:\s*(QUIET|CLOSE_GAME)\s*$'){$status=$Matches[1];if($text -match '(?m)^UNTIL:\s*(\d{4}-\d\d-\d\d \d\d:\d\d)\s*$'){$until=[datetime]::ParseExact($Matches[1],'yyyy-MM-dd HH:mm',[Globalization.CultureInfo]::InvariantCulture);$now=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([datetime]::UtcNow,'Eastern Standard Time');if($until -gt $now){throw ('Shared-PC restriction active: '+$status+' until '+$until)}}}}
$running=Get-CimInstance Win32_Process -Filter "name='Unity.exe'"
if($running|Where-Object {$_.CommandLine -and $_.CommandLine.Replace('\','/').IndexOf($project.Replace('\','/'),[StringComparison]::OrdinalIgnoreCase) -ge 0}){throw 'This Unity project is already owned by a running editor. Do not delete its lock or launch a second job.'}
$tool=Join-Path $PSScriptRoot 'UnitySources/RebirthVehicleDeliveryPipeline.cs'
$destination=Join-Path $project 'Assets/Editor/RebirthVehicleDeliveryPipeline.cs'
New-Item -ItemType Directory -Force -Path (Split-Path $destination)|Out-Null
Copy-Item -LiteralPath $tool -Destination $destination
& (Join-Path $PSScriptRoot '../AssetBundle/Stage-CrunchPackaging.ps1') -ProjectPath $project
New-Item -ItemType Directory -Force -Path $policy.output|Out-Null
$log=Join-Path $policy.output ('build-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'.log')
$arguments='-batchmode -nographics -quit -projectPath "'+$project+'" -executeMethod RebirthVehicleDeliveryPipeline.Run -vehiclePolicy "'+$policyFile+'" -logFile "'+$log+'"'
$startedUtc=[datetime]::UtcNow
$job=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
if($job.ExitCode -ne 0){throw ('Unity failed, exit '+$job.ExitCode+'. Inspect '+$log+'. No automatic retry or installation performed.')}
$receipt=Join-Path $policy.output 'latest-verified-run.txt'
if(!(Test-Path -LiteralPath $receipt)){throw ('Unity produced no verified receipt. Inspect '+$log)}
if((Get-Item -LiteralPath $receipt).LastWriteTimeUtc -lt $startedUtc){throw 'Verified receipt is stale'}
$run=(Get-Content -LiteralPath $receipt -Raw).Trim()
Get-Content -LiteralPath (Join-Path $run 'verified.txt')
Write-Output ('Evidence: '+$run)
Write-Output 'Build only. Installation and real game acceptance are separate gates.'

