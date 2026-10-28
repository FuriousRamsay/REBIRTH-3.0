[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$BuildResult, [string]$PreviousInstallResult)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$runId=[Guid]::NewGuid().ToString('N')
$folder=Join-Path $camperTemporaryDirectory "CamperDiagnosticsInstalls\$runId"
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$result=[ordered]@{kind='REBIRTH_CAMPER_DIAGNOSTICS_INSTALL';status='FAIL';gameLaunched=$false;error=$null}
$exitCode=1
Start-Transcript -LiteralPath (Join-Path $folder 'launcher.log') | Out-Null
try {
  Write-Host '[1 PREFLIGHT] Requiring closed game and verified standalone build'
  if(Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue){throw 'Game is running; leave it untouched and install later.'}
  $build=Get-Content -LiteralPath $BuildResult -Raw | ConvertFrom-Json
  if($build.status -cne 'PASS_BUILD_NOT_RUNTIME_TESTED'){throw 'Build receipt did not pass.'}
  $production=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\RebirthUtils.dll'))
  if($build.productionDll -ine $production -or
     (Get-FileHash -LiteralPath $production).Hash.ToLowerInvariant() -cne $build.productionSha256){
    throw 'Production DLL differs from the diagnostic build receipt; rebuild the helper against the current DLL.'
  }
  if((Get-FileHash -LiteralPath $build.dll).Hash.ToLowerInvariant() -cne $build.sha256){throw 'Built DLL differs from receipt.'}
  $mod=Join-Path $env:APPDATA '7DaysToDie\Mods\REBIRTH_VehicleInspector_Dev'
  if(-not(Test-Path -LiteralPath (Join-Path $mod 'ModInfo.xml'))){throw 'Existing developer inspector installation missing.'}
  $dest=Join-Path $mod 'RebirthCamperDiagnostics.dll'
  if(Test-Path -LiteralPath $dest){
    if(-not $PreviousInstallResult){throw 'Developer diagnostic already exists; require its prior ownership receipt.'}
    $previous=Get-Content -LiteralPath $PreviousInstallResult -Raw | ConvertFrom-Json
    if($previous.status -cne 'PASS_INSTALLED_NOT_RUNTIME_TESTED' -or $previous.destination -ine $dest){throw 'Prior ownership receipt does not identify this installed diagnostic.'}
    if((Get-FileHash -LiteralPath $dest).Hash.ToLowerInvariant() -cne $previous.sha256){throw 'Installed diagnostic changed since its prior receipt; refusing overwrite.'}
    $backup=Join-Path $folder 'previous_RebirthCamperDiagnostics.dll'
    Copy-Item -LiteralPath $dest -Destination $backup
    $result.previousDiagnosticBackup=$backup
    $result.previousDiagnosticSha256=$previous.sha256
  }
  Write-Host '[2 INSTALL] Adding owned diagnostic/calibration helper to the existing developer inspector'
  $original=Join-Path $mod 'RebirthVehicleInspector.dll'
  $originalHash=(Get-FileHash -LiteralPath $original).Hash
  if((Get-FileHash -LiteralPath $production).Hash.ToLowerInvariant() -cne $build.productionSha256){throw 'Production DLL changed before diagnostic installation.'}
  Copy-Item -LiteralPath $build.dll -Destination $dest
  if((Get-FileHash -LiteralPath $dest).Hash.ToLowerInvariant() -cne $build.sha256){throw 'Installed checksum mismatch.'}
  if((Get-FileHash -LiteralPath $original).Hash -cne $originalHash){throw 'Existing inspector changed concurrently.'}
  $result.destination=$dest
  $result.sha256=$build.sha256
  $result.originalInspectorSha256=$originalHash.ToLowerInvariant()
  $result.rollback='Remove only destination if its SHA-256 still equals this receipt. Preserve the original inspector DLL and its manifest.'
  $result.status='PASS_INSTALLED_NOT_RUNTIME_TESTED'
  $exitCode=0
} catch { $result.error=$_.Exception.Message; Write-Host "[FAIL] $($result.error)" }
finally {
  $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $folder 'result.json') -Encoding UTF8
  Write-Host "[RESULT] $($result.status); report=$folder"
  Stop-Transcript | Out-Null
}
exit $exitCode
