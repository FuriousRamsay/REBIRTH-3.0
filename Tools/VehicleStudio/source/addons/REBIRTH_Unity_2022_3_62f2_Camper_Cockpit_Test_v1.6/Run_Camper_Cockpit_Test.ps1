# REBIRTH Unity 2022.3.62f2 - v1.6 camper cockpit calibration preview.
# Read-only input assets; does not build game bundles, XML, or install to REBIRTH.
[CmdletBinding()]
param(
    [string]$ProjectPath = 'C:\REBIRTH\VehicleStudio_QuickTest',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [string]$WheelRunId = '54d390ca0f61428587a1b48346f6d831'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Quote-Arg([string]$Value) {
    if ($Value.Contains('"')) { throw "Invalid double-quote in argument: $Value" }
    if ($Value.EndsWith('\')) { $Value += '\' }
    return '"' + $Value + '"'
}
function Print-UnityLog([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Write-Host "--- Unity Editor log tail: $Path ---" -ForegroundColor Yellow
        Get-Content -LiteralPath $Path -Tail 100 | Out-Host
    }
}
if ($WheelRunId -cnotmatch '^[0-9a-f]{32}$') {
    throw 'WheelRunId must be the exact lowercase 32-character identifier from the successful v1.5 test.'
}
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd([char[]]@('\','/'))
if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw "Unity executable not found: $UnityExe" }
$assets = Join-Path $ProjectPath 'Assets'
$version = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $version -PathType Leaf) -or
    -not (Test-Path -LiteralPath $assets -PathType Container) -or
    ((Get-Content -LiteralPath $version -Raw) -notmatch '(?m)^m_EditorVersion:\s*2022\.3\.62f2\s*$')) {
    throw "Expected a complete Unity 2022.3.62f2 project at $ProjectPath"
}
$marker = Join-Path $assets 'REBIRTHQuickSmoke\REBIRTH_QUICK_TEST_MARKER.txt'
$core = Join-Path $assets 'REBIRTHVehicleTools\Editor\RebirthVehicleRigBuilder.cs'
$oldHarness = Join-Path $assets 'REBIRTHQuickSmoke\Editor\RebirthUnityCamperWheelRigSmoke.cs'
if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or
    -not (Test-Path -LiteralPath $core -PathType Leaf) -or
    -not (Test-Path -LiteralPath $oldHarness -PathType Leaf)) {
    throw 'The existing, successfully tested v1.2/v1.5 Vehicle Studio code was not found. No source changed.'
}
if (-not (Get-Content -LiteralPath $marker -Raw).StartsWith('REBIRTH Vehicle Studio smoke source')) {
    throw 'Project ownership marker does not match the installed quick-test tools.'
}
$wheelReport = Join-Path $ProjectPath "REBIRTH_CamperWheelRig_Reports\$WheelRunId\result.json"
if (-not (Test-Path -LiteralPath $wheelReport -PathType Leaf)) {
    throw "Successful v1.5 result is missing: $wheelReport"
}
$wheelResult = Get-Content -LiteralPath $wheelReport -Raw | ConvertFrom-Json
$expectedPreview = "Assets/REBIRTHCamperWheelRigGenerated/$WheelRunId/UNAPPROVED_Camper_FourWheel_Preview.prefab"
if ($wheelResult.runId -cne $WheelRunId -or
    $wheelResult.status -cne 'PASS_GEOMETRY_PREVIEW_NEEDS_REVIEW' -or
    $wheelResult.previewPrefab -cne $expectedPreview -or
    $wheelResult.wheelsAttached -ne 4 -or
    -not $wheelResult.sourceUnchanged) {
    throw 'The specified wheel-rig report is not the successful v1.5 camper result.'
}
$wheelPrefab = Join-Path $ProjectPath ($expectedPreview.Replace('/', '\'))
if (-not (Test-Path -LiteralPath $wheelPrefab -PathType Leaf)) {
    throw "Previously successful camper wheel prefab missing: $wheelPrefab"
}
$contract = Join-Path $PSScriptRoot 'evidence\camper_cockpit_contract.json'
$scriptFile = Join-Path $PSScriptRoot 'UnitySources\REBIRTHQuickSmoke\Editor\RebirthUnityCamperCockpitSmoke.cs'
$exportScript = Join-Path $PSScriptRoot 'UnitySources\REBIRTHQuickSmoke\Editor\RebirthCamperProfileExporter.cs'
foreach ($p in @($contract, $scriptFile, $exportScript)) {
    if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Incomplete archive; missing $p" }
}
$evidence = Get-Content -LiteralPath $contract -Raw | ConvertFrom-Json
if ($evidence.kind -cne 'REBIRTH_CAMPER_COCKPIT_EVIDENCE_V1' -or
    $evidence.unityVersion -cne '2022.3.62f2') {
    throw 'Unexpected source-evidence contract.'
}
$originalPrefab = Join-Path $assets 'Perfectvertex\80sCamperTruck\Prefabs\assembled_vehicle.prefab'
if (-not (Test-Path -LiteralPath $originalPrefab -PathType Leaf) -or
    -not (Test-Path -LiteralPath ($originalPrefab + '.meta') -PathType Leaf)) {
    throw 'The imported Perfectvertex camper prefab or Unity metadata is missing.'
}
if ((Get-FileHash -LiteralPath $originalPrefab -Algorithm SHA256).Hash.ToLowerInvariant() -cne $evidence.sourceSha256 -or
    (Get-FileHash -LiteralPath ($originalPrefab + '.meta') -Algorithm SHA256).Hash.ToLowerInvariant() -cne $evidence.sourceImporterMetaSha256) {
    throw 'The imported camper or its importer settings changed after the confirmed audit. Re-audit first.'
}
Write-Host 'Unity version, source hashes and successful wheel-rig evidence: PASS' -ForegroundColor Green

# Add ONLY new Editor test scripts. Never overwrite the v1.2/v1.5 core or imported assets.
$destDir = Join-Path $assets 'REBIRTHQuickSmoke\Editor'
foreach ($source in @($scriptFile, $exportScript)) {
    $destination = Join-Path $destDir ([System.IO.Path]::GetFileName($source))
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        $a = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
        $b = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
        if ($a -cne $b) { throw "Different script already exists; refusing overwrite: $destination" }
        Write-Host "Identical add-on already installed: $destination"
    } else {
        Copy-Item -LiteralPath $source -Destination $destination -ErrorAction Stop
        Write-Host "Added cockpit test script: $destination"
    }
}
$runId = [Guid]::NewGuid().ToString('N')
$report = Join-Path $ProjectPath "REBIRTH_CamperCockpit_Reports\$runId"
New-Item -ItemType Directory -Force -Path $report | Out-Null
$logFile = Join-Path $report 'UnityEditor.log'
$resultFile = Join-Path $report 'result.json'
Write-Host "Running camper cockpit preview in Unity 2022.3.62f2. Run ID: $runId" -ForegroundColor Cyan
Write-Host 'Close the Unity Editor for this project before batch execution.' -ForegroundColor Yellow
$arguments = @(
    '-batchmode','-nographics','-quit','-accept-apiupdate',
    '-projectPath',(Quote-Arg $ProjectPath),
    '-executeMethod','RebirthUnityCamperCockpitSmoke.RunBatch',
    '-rebirthCockpitRunId',$runId,
    '-rebirthWheelRunId',$WheelRunId,
    '-rebirthCockpitEvidence',(Quote-Arg $contract),
    '-logFile',(Quote-Arg $logFile)
)
try {
    $proc = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -Wait -ErrorAction Stop
} catch {
    Print-UnityLog $logFile
    throw "Unity failed to start: $($_.Exception.Message)"
}
if ($null -eq $proc -or $null -eq $proc.ExitCode) {
    Print-UnityLog $logFile
    throw 'Unity did not report an exit code.'
}
$code = [int]$proc.ExitCode
Write-Host "Unity process exit code: $code"
if (-not (Test-Path -LiteralPath $resultFile -PathType Leaf)) {
    Print-UnityLog $logFile
    throw "No report written; check compile/import errors in $logFile"
}
$result = Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json
if ($result.runId -cne $runId -or $result.wheelRunId -cne $WheelRunId) {
    throw 'Unity produced a stale or mismatched test report.'
}
$pass = ($code -eq 0 -and $result.status -ceq 'PASS_COCKPIT_PREVIEW_NEEDS_CALIBRATION')
Write-Host "Camper cockpit preview: $($result.status)" -ForegroundColor $(if ($pass) {'Green'} else {'Red'})
foreach ($step in @($result.steps)) { Write-Host "  $step" }
if ($result.error) { Write-Host "Failure: $($result.error)" -ForegroundColor Red }
Write-Host "New rig preview:   $($result.cockpitPreviewPrefab)"
Write-Host "Editable scene:    $($result.calibrationScene)"
Write-Host "Editable profile:  $($result.calibrationProfile)"
Write-Host "Result report:     $resultFile"
Write-Host "Unity Editor log:  $logFile"
if (-not $pass) {
    throw 'The cockpit-preview test failed; no bundle, XML, or REBIRTH installation was performed.'
}
Write-Host 'PASS: cockpit geometry is ready for visual review. Seats, hand IK and 7DTD gameplay remain UNVERIFIED.' -ForegroundColor Green
