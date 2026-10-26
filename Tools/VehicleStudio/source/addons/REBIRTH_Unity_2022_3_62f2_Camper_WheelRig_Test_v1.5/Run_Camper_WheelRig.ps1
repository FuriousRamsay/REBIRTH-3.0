# REBIRTH Vehicle Studio v1.5 — real 80s camper wheel-geometry preview.
# NO AssetBundle. NO REBIRTH/7DTD file modifications. Unity 2022.3.62f2 only.
[CmdletBinding()]
param(
    [string]$ProjectPath = 'C:\REBIRTH\VehicleStudio_QuickTest',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Quote-Arg([string]$Value) {
    if ($Value.Contains('"')) { throw "Double quote is unsupported in a Unity argument: $Value" }
    $s = $Value
    if ($s.EndsWith('\')) { $s += '\' }
    return '"' + $s + '"'
}
function Print-LogTail([string]$Log) {
    if (Test-Path -LiteralPath $Log -PathType Leaf) {
        Write-Host "=== Unity Editor log (last 70 lines): $Log ===" -ForegroundColor Yellow
        Get-Content -LiteralPath $Log -Tail 70 | Out-Host
    } else {
        Write-Warning "Unity log was not created: $Log"
    }
}

$ProjectPath = [IO.Path]::GetFullPath($ProjectPath).TrimEnd([char[]]@('\','/'))
if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) {
    throw "Unity Editor executable not found: $UnityExe"
}
$assets = Join-Path $ProjectPath 'Assets'
$version = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $assets -PathType Container) -or
    -not (Test-Path -LiteralPath $version -PathType Leaf) -or
    ((Get-Content -LiteralPath $version -Raw) -notmatch '(?m)^m_EditorVersion:\s*2022\.3\.62f2\s*$')) {
    throw "The Unity project is missing or does not use exactly 2022.3.62f2: $ProjectPath"
}
# Guard: previously tested v1.2 core and successful v1.4 imported-prefab harness.
$marker = Join-Path $assets 'REBIRTHQuickSmoke\REBIRTH_QUICK_TEST_MARKER.txt'
$builder = Join-Path $assets 'REBIRTHVehicleTools\Editor\RebirthVehicleRigBuilder.cs'
$intake = Join-Path $assets 'REBIRTHQuickSmoke\Editor\RebirthUnityImportedPrefabSmoke.cs'
if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or
    -not (Test-Path -LiteralPath $builder -PathType Leaf) -or
    -not (Test-Path -LiteralPath $intake -PathType Leaf)) {
    throw 'This test requires the already PASSing v1.2 and v1.4 Unity tools. No changes were installed.'
}
if (-not (Get-Content -LiteralPath $marker -Raw).StartsWith('REBIRTH Vehicle Studio smoke source')) {
    throw 'Unexpected Unity project ownership marker; refusing to install addon.'
}
$camper = Join-Path $assets 'Perfectvertex\80sCamperTruck\Prefabs\assembled_vehicle.prefab'
if (-not (Test-Path -LiteralPath $camper -PathType Leaf)) {
    throw "The real imported camper prefab is missing: $camper"
}
$contract = Join-Path $PSScriptRoot 'evidence\camper_wheel_contract.json'
$addon = Join-Path $PSScriptRoot 'UnitySources\REBIRTHQuickSmoke\Editor\RebirthUnityCamperWheelRigSmoke.cs'
if (-not (Test-Path -LiteralPath $contract -PathType Leaf) -or
    -not (Test-Path -LiteralPath $addon -PathType Leaf)) {
    throw 'Incomplete v1.5 package: Unity source or evidence contract missing.'
}
$evidence = Get-Content -LiteralPath $contract -Raw | ConvertFrom-Json
if ($evidence.kind -ne 'REBIRTH_CAMPER_WHEEL_EVIDENCE_V1' -or
    $evidence.sourceAsset -ne 'Assets/Perfectvertex/80sCamperTruck/Prefabs/assembled_vehicle.prefab') {
    throw 'Evidence contract is for a different prefab.'
}
$actualSha = (Get-FileHash -LiteralPath $camper -Algorithm SHA256).Hash.ToLowerInvariant()
$metaSha = (Get-FileHash -LiteralPath ($camper + '.meta') -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualSha -ne $evidence.sourceSha256 -or $metaSha -ne $evidence.sourceImporterMetaSha256) {
    throw 'Prefab or .meta differs from the audited asset. Re-run inspection before any geometry-based rig preview.'
}
Write-Host 'Source prefab SHA and importer metadata match the real captured audit.' -ForegroundColor Green

$destination = Join-Path $assets 'REBIRTHQuickSmoke\Editor\RebirthUnityCamperWheelRigSmoke.cs'
if (Test-Path -LiteralPath $destination -PathType Leaf) {
    if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $addon -Algorithm SHA256).Hash) {
        throw "A different camper rigging script already exists. Refusing to overwrite: $destination"
    }
    Write-Host 'The identical v1.5 Unity harness is already installed.' -ForegroundColor Green
} else {
    Copy-Item -LiteralPath $addon -Destination $destination -ErrorAction Stop
    Write-Host 'Installed additive v1.5 Unity harness. Existing core unchanged.' -ForegroundColor Green
}
$runId = [guid]::NewGuid().ToString('N')
$reports = Join-Path $ProjectPath "REBIRTH_CamperWheelRig_Reports\$runId"
New-Item -ItemType Directory -Path $reports -Force | Out-Null
$log = Join-Path $reports 'UnityEditor.log'
$resultFile = Join-Path $reports 'result.json'
Write-Host "Running Unity 2022.3.62f2 against the audited camper. Run ID: $runId" -ForegroundColor Cyan
Write-Host 'Close any Unity Editor instance using this project before batch execution.' -ForegroundColor Yellow
$arguments = @(
    '-batchmode', '-nographics', '-quit', '-accept-apiupdate',
    '-projectPath', (Quote-Arg $ProjectPath),
    '-executeMethod', 'RebirthUnityCamperWheelRigSmoke.RunBatch',
    '-rebirthCamperRunId', $runId,
    '-rebirthCamperEvidence', (Quote-Arg $contract),
    '-logFile', (Quote-Arg $log)
)
try {
    $proc = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -Wait -ErrorAction Stop
} catch {
    Print-LogTail $log
    throw "Unity could not be started: $($_.Exception.Message)"
}
if ($null -eq $proc -or $null -eq $proc.ExitCode) {
    Print-LogTail $log
    throw 'Unity did not provide a process exit code.'
}
$code = [int]$proc.ExitCode
Write-Host "Unity process exit code: $code"
if (-not (Test-Path -LiteralPath $resultFile -PathType Leaf)) {
    Print-LogTail $log
    throw "No test result written. Check Unity compile/import errors: $log"
}
$result = Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json
if ($result.runId -ne $runId) { throw 'Unity produced a stale/wrong run ID.' }
$pass = ($code -eq 0 -and $result.status -eq 'PASS_GEOMETRY_PREVIEW_NEEDS_REVIEW')
Write-Host "Camper wheel geometry test: $($result.status)" -ForegroundColor $(if ($pass) { 'Green' } else { 'Red' })
foreach ($step in @($result.steps)) { Write-Host "  $step" }
Write-Host "Matched wheel meshes: $($result.wheelMeshesFound); attached: $($result.wheelsAttached)"
Write-Host "Mislabeled rear-left wheel corrected (preview only): $($result.duplicateNamesRepaired)"
Write-Host "Original imported prefab unchanged: $($result.sourceUnchanged)"
Write-Host "Preview prefab: $($result.previewPrefab)"
Write-Host "Report: $resultFile"
Write-Host "Unity log: $log"
if (-not $pass) {
    if ($result.error) { Write-Host "Failure: $($result.error)" -ForegroundColor Red }
    throw 'Camper wheel-rig test failed. No AssetBundle, XML or REBIRTH deployment was performed.'
}
Write-Host 'PASS: geometry-only wheel-rig preview. Native 7DTD pivots/seats/IK remain UNVERIFIED.' -ForegroundColor Green
