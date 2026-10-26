# REBIRTH v1.8, Unity 2022.3.62f2. Donor-XML pose REVIEW only.
# Never changes original Perfectvertex prefab, vanilla game files, or REBIRTH mod.
[CmdletBinding()]
param(
    [string]$ProjectPath = 'C:\REBIRTH\VehicleStudio_QuickTest',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die',
    [string]$CockpitRunId = '06678d094a7c44d3a09edd5c90792442'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Quote-Arg([string]$Value) {
    if ($Value.Contains('"')) { throw "Double-quote not supported in argument: $Value" }
    if ($Value.EndsWith('\')) { $Value += '\' }
    return '"' + $Value + '"'
}
function Tail-UnityLog([string]$Path) {
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        Write-Host "--- Recent Unity log: $Path ---" -ForegroundColor Yellow
        Get-Content -LiteralPath $Path -Tail 100 | Out-Host
    }
}
if ($CockpitRunId -cnotmatch '^[0-9a-f]{32}$') { throw 'CockpitRunId must be the specific 32-character successful v1.6 test run ID.' }
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath).TrimEnd([char[]]@('\','/'))
if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw "Unity executable missing: $UnityExe" }
$versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf) -or
    ((Get-Content -LiteralPath $versionFile -Raw) -notmatch '(?m)^m_EditorVersion:\s*2022\.3\.62f2\s*$')) {
    throw "Expected the existing Unity 2022.3.62f2 project at $ProjectPath"
}
$assets = Join-Path $ProjectPath 'Assets'
$marker = Join-Path $assets 'REBIRTHQuickSmoke\REBIRTH_QUICK_TEST_MARKER.txt'
$rigBuilder = Join-Path $assets 'REBIRTHVehicleTools\Editor\RebirthVehicleRigBuilder.cs'
$priorCode = Join-Path $assets 'REBIRTHQuickSmoke\Editor\RebirthUnityCamperCockpitSmoke.cs'
foreach ($item in @($marker,$rigBuilder,$priorCode)) {
    if (-not (Test-Path -LiteralPath $item -PathType Leaf)) { throw "Existing tested Unity project code not found: $item" }
}
if (-not (Get-Content -LiteralPath $marker -Raw).StartsWith('REBIRTH Vehicle Studio smoke source')) {
    throw 'Project marker does not match installed REBIRTH quick-test tools.'
}
$priorResultPath = Join-Path $ProjectPath "REBIRTH_CamperCockpit_Reports\$CockpitRunId\result.json"
if (-not (Test-Path -LiteralPath $priorResultPath -PathType Leaf)) { throw "Successful cockpit result missing: $priorResultPath" }
$prior = Get-Content -LiteralPath $priorResultPath -Raw | ConvertFrom-Json
if ($prior.kind -cne 'REBIRTH_CAMPER_COCKPIT_PREVIEW_V1_6' -or
    $prior.runId -cne $CockpitRunId -or
    $prior.status -cne 'PASS_COCKPIT_PREVIEW_NEEDS_CALIBRATION' -or
    $prior.preservedMeshes -ne 16 -or $prior.preservedWheelJoints -ne 4 -or
    -not $prior.originalSourceUnchanged -or -not $prior.originalWheelPreviewUnchanged) {
    throw 'The referenced v1.6 cockpit test is not the previously successful one.'
}
$prefix = "Assets/REBIRTHCamperCockpitGenerated/$CockpitRunId/"
if ($prior.calibrationScene -cne ($prefix + 'OPEN_ME_Camper_Occupant_Calibration.unity') -or
    $prior.calibrationProfile -cne ($prefix + 'UNAPPROVED_Camper_OccupantProfile.asset') -or
    $prior.cockpitPreviewPrefab -cne ($prefix + 'UNAPPROVED_Camper_WheelsAndSteering.prefab')) {
    throw 'Cockpit report paths differ from the known v1.6 test.'
}
foreach ($path in @($prior.calibrationScene,$prior.calibrationProfile,$prior.cockpitPreviewPrefab)) {
    $disk = Join-Path $ProjectPath ($path.Replace('/', '\'))
    if (-not (Test-Path -LiteralPath $disk -PathType Leaf)) { throw "Missing prior asset: $disk" }
}
$gameXml = Join-Path $GamePath 'Data\Config\vehicles.xml'
if (-not (Test-Path -LiteralPath $gameXml -PathType Leaf)) { throw "Installed 7DTD vehicles.xml missing: $gameXml" }
$planPath = Join-Path $PSScriptRoot 'evidence\camper_donor_calibration_plan.json'
$sourceCs = Join-Path $PSScriptRoot 'UnitySources\REBIRTHQuickSmoke\Editor\RebirthUnityCamperDonorPoseSmoke.cs'
foreach ($p in @($planPath,$sourceCs)) { if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Incomplete downloaded test archive: $p" } }
$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
if ($plan.kind -cne 'REBIRTH_CAMPER_DONOR_POSE_CALIBRATION_V1_8' -or
    $plan.status -cne 'DONOR_XML_ONLY_NOT_GAME_VALIDATED' -or
    $plan.requiredUnityVersion -cne '2022.3.62f2' -or
    $plan.donorVehicleName -cne 'vehicleTruck4x4' -or
    $plan.driverPoseId -cne '40' -or $plan.passengerPoseId -cne '41' -or
    $plan.selectedSeatCountForPreview -ne 2 -or
    $plan.approvedSeatCount -ne 0 -or $plan.approvedExitCount -ne 0 -or
    $plan.enablesHandIk -or $plan.enablesFootIk) {
    throw 'Unexpected donor plan or unsafe preview configuration.'
}
$gameXmlHash = (Get-FileHash -LiteralPath $gameXml -Algorithm SHA256).Hash.ToLowerInvariant()
if ($gameXmlHash -cne $plan.donorXmlSha256) {
    throw 'Your installed game vehicles.xml has changed since the vanilla 4x4 XML capture. Re-capture first.'
}
$camperPrefab = Join-Path $ProjectPath ($plan.camperSourceAsset.Replace('/', '\'))
if (-not (Test-Path -LiteralPath $camperPrefab -PathType Leaf) -or
    -not (Test-Path -LiteralPath ($camperPrefab+'.meta') -PathType Leaf)) {
    throw 'The original Perfectvertex camper/import metadata is unavailable.'
}
if ((Get-FileHash -LiteralPath $camperPrefab -Algorithm SHA256).Hash.ToLowerInvariant() -cne $plan.camperSourceSha256 -or
    (Get-FileHash -LiteralPath ($camperPrefab+'.meta') -Algorithm SHA256).Hash.ToLowerInvariant() -cne $plan.camperSourceImporterMetaSha256) {
    throw 'Original camper prefab/import metadata changed. Refusing to use stale measured geometry.'
}
Write-Host 'PASS: exact Unity version, v1.6 cockpit artifacts, installed 4x4 XML, and imported camper hashes' -ForegroundColor Green
# Only install ONE new named C# Editor script; all previously tested core and imported assets stay unchanged.
$scriptDest = Join-Path $assets 'REBIRTHQuickSmoke\Editor\RebirthUnityCamperDonorPoseSmoke.cs'
if (Test-Path -LiteralPath $scriptDest -PathType Leaf) {
    $oldSha = (Get-FileHash -LiteralPath $scriptDest -Algorithm SHA256).Hash
    $newSha = (Get-FileHash -LiteralPath $sourceCs -Algorithm SHA256).Hash
    if ($oldSha -cne $newSha) { throw "A DIFFERENT C# script already exists, refusing overwrite: $scriptDest" }
    Write-Host "Existing identical v1.8 C# source: $scriptDest"
} else {
    Copy-Item -LiteralPath $sourceCs -Destination $scriptDest
    Write-Host "Installed additive Unity Editor source: $scriptDest"
}
$runId = [Guid]::NewGuid().ToString('N')
$reportDir = Join-Path $ProjectPath "REBIRTH_CamperDonorPose_Reports\$runId"
New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
$logFile = Join-Path $reportDir 'UnityEditor.log'
$resultFile = Join-Path $reportDir 'result.json'
Write-Host "Running donor-pose review with Unity 2022.3.62f2 (run ID: $runId)." -ForegroundColor Cyan
Write-Host 'Close any Unity Editor that has this project open before running.' -ForegroundColor Yellow
$argsList = @(
    '-batchmode','-nographics','-quit','-accept-apiupdate',
    '-projectPath',(Quote-Arg $ProjectPath),
    '-executeMethod','RebirthUnityCamperDonorPoseSmoke.RunBatch',
    '-rebirthDonorRunId',$runId,
    '-rebirthCockpitRunId',$CockpitRunId,
    '-rebirthDonorPlan',(Quote-Arg $planPath),
    '-rebirthGameVehiclesXml',(Quote-Arg $gameXml),
    '-logFile',(Quote-Arg $logFile)
)
try {
    $p = Start-Process -FilePath $UnityExe -ArgumentList $argsList -Wait -PassThru -ErrorAction Stop
} catch {
    Tail-UnityLog $logFile
    throw "Unity failed to start: $($_.Exception.Message)"
}
if ($null -eq $p -or $null -eq $p.ExitCode) {
    Tail-UnityLog $logFile
    throw 'Unity process did not report an exit code.'
}
$exitCode = [int]$p.ExitCode
Write-Host "Unity process exit code: $exitCode"
if (-not (Test-Path -LiteralPath $resultFile -PathType Leaf)) {
    Tail-UnityLog $logFile
    throw "No Unity test result written. Inspect: $logFile"
}
$result = Get-Content -LiteralPath $resultFile -Raw | ConvertFrom-Json
if ($result.runId -cne $runId -or $result.cockpitRunId -cne $CockpitRunId) {
    throw 'Stale or mismatched Unity batch result.'
}
$ok = $exitCode -eq 0 -and $result.status -ceq 'PASS_DONOR_XML_POSE_REVIEW_NOT_GAME_VERIFIED'
Write-Host "Donor pose review: $($result.status)" -ForegroundColor $(if ($ok) {'Green'} else {'Red'})
foreach ($s in @($result.steps)) { Write-Host "  $s" }
if ($result.error) { Write-Host "Error: $($result.error)" -ForegroundColor Red }
Write-Host "Review scene: $($result.sceneAsset)"
Write-Host "Review profile: $($result.profileAsset)"
Write-Host "JSON report: $resultFile"
Write-Host "Unity log: $logFile"
if (-not $ok) {
    Tail-UnityLog $logFile
    throw 'Donor pose test failed; no vehicle was installed or marked ready.'
}
Write-Host 'PASS: vanilla 4x4 seat poses 40/41 are now in a COPY of the camper profile. Hand IK, exits, gameplay still NOT verified.' -ForegroundColor Green
