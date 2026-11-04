param([ValidateRange(23,99)][int]$Batch=23)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent | Split-Path -Parent
Set-Location -LiteralPath $root
if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){throw 'A game is already running; no launch permitted'}
$config=Get-Content Tools/GameBridge/gamebridge.config.json -Raw|ConvertFrom-Json
if($config.save -ne 'CodexTestPurgeFresh20261102' -or $config.world -ne 'West Xuyofu Territory'){throw 'Unexpected disposable save configuration'}
$stage=Join-Path $PSScriptRoot ('out/purge-b'+$Batch);New-Item -ItemType Directory -Force $stage|Out-Null
if(Test-Path (Join-Path $stage 'host.owner.json')){throw 'Existing batch owner must be inspected; no duplicate launch'}
$env:PROFILER_SAMPLE_HZ='25';$env:PROFILER_TIMELINE='1'
$hostProcess=Start-Process -FilePath (Join-Path $PSScriptRoot 'bin/RebirthProfiler.exe') -ArgumentList @('--headless-launch','--wait') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $stage 'host.stdout.log') -RedirectStandardError (Join-Path $stage 'host.stderr.log')
$handle=$hostProcess.Handle
@{hostPid=$hostProcess.Id;startUtc=[datetime]::UtcNow.ToString('o');dll=(Get-FileHash RebirthUtils.dll).Hash;sampler=(Get-FileHash Tools/Profiler/native/mono-profiler-rebirthprof.dll).Hash;requestedHz=25}|ConvertTo-Json|Set-Content (Join-Path $stage 'host.owner.json')
Write-Output ('Owned headless profiler PID '+$hostProcess.Id)
$hostProcess.WaitForExit();$hostProcess.Refresh()
@{hostPid=$hostProcess.Id;terminal=$true;exitCode=$hostProcess.ExitCode}|ConvertTo-Json|Set-Content (Join-Path $stage 'host.terminal.json')
Get-Content (Join-Path $stage 'host.stdout.log')
