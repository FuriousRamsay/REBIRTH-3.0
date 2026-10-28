[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$runId=[Guid]::NewGuid().ToString('N')
$folder=Join-Path $camperTemporaryDirectory "CamperDiagnosticsBuilds\$runId"
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$result=[ordered]@{kind='REBIRTH_CAMPER_DIAGNOSTICS_BUILD';runId=$runId;status='FAIL';gameLaunched=$false;sharedGameDllBuilt=$false;error=$null}
$exitCode=1
Start-Transcript -LiteralPath (Join-Path $folder 'launcher.log') | Out-Null
try {
  Write-Host '[1 PREFLIGHT] Building only the standalone developer diagnostic/calibration helper'
  if(Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue){throw 'Game is running; leave it untouched and build later.'}
  $project=Join-Path $PSScriptRoot 'RuntimeDiagnostics\CamperDiagnostics.csproj'
  $production=Join-Path $PSScriptRoot '..\..\RebirthUtils.dll'
  if(-not(Test-Path -LiteralPath $production)){throw 'Current production RebirthUtils.dll must be built first.'}
  [xml]$projectXml=Get-Content -LiteralPath $project -Raw
  if(@($projectXml.Project.ItemGroup.Compile | Where-Object { $_.Include }).Count){throw 'Diagnostic project must not compile linked production clones.'}
  $productionHash=(Get-FileHash -LiteralPath $production).Hash.ToLowerInvariant()
  $result.productionDll=[IO.Path]::GetFullPath($production)
  $result.productionSha256=$productionHash
  $stdout=Join-Path $folder 'build.stdout.log'
  $stderr=Join-Path $folder 'build.stderr.log'
  Write-Host '[2 BUILD] Using installed framework and native game references'
  $framework=(Resolve-Path (Join-Path $PSScriptRoot '..\..\.nuget\packages\microsoft.netframework.referenceassemblies.net481\1.0.3\build')).Path
  $output=Join-Path $folder 'bin'
  $intermediate=Join-Path $folder 'obj'
  # Forward slashes avoid Windows command-line escaping a quote after a trailing backslash.
  $outputArgument=$output.Replace('\','/')+'/'
  $intermediateArgument=$intermediate.Replace('\','/')+'/'
  $proc=Start-Process -FilePath 'dotnet' -ArgumentList @('build',('"'+$project+'"'),'-c','Debug','--nologo',('"-p:TargetFrameworkRootPath='+$framework+'"'),('-p:OutputPath='+$outputArgument),('-p:IntermediateOutputPath='+$intermediateArgument),('-p:MSBuildProjectExtensionsPath='+$intermediateArgument)) -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
  $null=$proc.Handle
  while(-not $proc.WaitForExit(5000)) { Write-Host "[HEARTBEAT] Diagnostic compiler PID=$($proc.Id)" }
  $proc.WaitForExit()
  $proc.Refresh()
  $result.compilerExitCode=$proc.ExitCode
  if($proc.ExitCode -ne 0){throw "Diagnostic compilation failed; inspect $stdout and $stderr"}
  if((Get-FileHash -LiteralPath $production).Hash.ToLowerInvariant() -cne $productionHash){throw 'Production DLL changed during diagnostic compilation; rebuild against the settled DLL.'}
  Write-Host '[3 VERIFY] Recording the produced developer DLL'
  $dll=Join-Path $output 'RebirthCamperDiagnostics.dll'
  $result.dll=$dll
  $result.sha256=(Get-FileHash -LiteralPath $dll).Hash.ToLowerInvariant()
  $result.status='PASS_BUILD_NOT_RUNTIME_TESTED'
  $exitCode=0
} catch { $result.error=$_.Exception.Message; Write-Host "[FAIL] $($result.error)" }
finally {
  $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $folder 'result.json') -Encoding UTF8
  Write-Host "[RESULT] $($result.status); report=$folder"
  Stop-Transcript | Out-Null
}
exit $exitCode
