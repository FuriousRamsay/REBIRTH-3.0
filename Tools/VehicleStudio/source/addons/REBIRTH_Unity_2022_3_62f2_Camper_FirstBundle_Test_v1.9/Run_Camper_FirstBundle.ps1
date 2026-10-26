# REBIRTH Vehicle Studio - actual camper AssetBundle attempt, NOT 7DTD mod installation.
# Dedicated Unity 2022.3.62f2 test on preserved v1.5/v1.6/v1.8 working assets.
[CmdletBinding()]
param(
    [string]$ProjectPath = 'C:\REBIRTH\VehicleStudio_QuickTest',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$runId = [Guid]::NewGuid().ToString('N')
function Stage([int]$index,[string]$message) { Write-Host "[$index/6] $message" -ForegroundColor Cyan }
function Fail([string]$message) { Write-Host "STATUS: FAIL - $message" -ForegroundColor Red; throw $message }
function Quoted([string]$inputText) {
    if ($inputText.Contains('"')) { throw 'Path contains unsupported quote.' }
    return '"' + $inputText + '"'
}
try {
    Stage 1 'Checking installed Unity, game XML and real previously tested camper artifacts'
    if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { Fail "Unity.exe not found: $UnityExe" }
    $versionFile = Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf) -or
        (Get-Content -LiteralPath $versionFile -Raw) -notmatch '(?m)^m_EditorVersion:\s*2022\.3\.62f2\s*$') {
        Fail "Unity project version mismatch at $ProjectPath"
    }
    $gameXml = Join-Path $GamePath 'Data\Config\vehicles.xml'
    if (-not (Test-Path -LiteralPath $gameXml -PathType Leaf)) { Fail "Game vehicles.xml missing: $gameXml" }
    $donorHash = '4abdce695fc9d65ca14063dce67b9fe84ebe6736b2ecf5ac9e397be3c6c3d145'
    if ((Get-FileHash -LiteralPath $gameXml -Algorithm SHA256).Hash.ToLowerInvariant() -cne $donorHash) {
        Fail 'Installed vehicles.xml differs from the verified 4x4 donor. Re-capture before building.'
    }
    $srcFile = Join-Path $ProjectPath 'Assets\Perfectvertex\80sCamperTruck\Prefabs\assembled_vehicle.prefab'
    $sourceHash = '75357bec0ab35826b9205011811f49ca743ea42270a30fb2baa0b82e358b916d'
    $metaHash = '83ebb10b3afb151b40ba4fee9901740c0b0a5396d850c66e4c156e6d5e66c69a'
    if (-not (Test-Path -LiteralPath $srcFile -PathType Leaf) -or
        -not (Test-Path -LiteralPath ($srcFile + '.meta') -PathType Leaf)) {
        Fail 'Imported original camper prefab or its .meta is missing.'
    }
    if ((Get-FileHash -LiteralPath $srcFile -Algorithm SHA256).Hash.ToLowerInvariant() -cne $sourceHash -or
        (Get-FileHash -LiteralPath ($srcFile + '.meta') -Algorithm SHA256).Hash.ToLowerInvariant() -cne $metaHash) {
        Fail 'Original camper or import metadata changed from passed v1.4 audit.'
    }
    $cop = 'Assets/REBIRTHCamperCockpitGenerated/06678d094a7c44d3a09edd5c90792442/UNAPPROVED_Camper_WheelsAndSteering.prefab'
    $pose = 'Assets/REBIRTHCamperDonorPoseGenerated/4c31a760c8fd442b9e2d882e7e16712c/UNAPPROVED_Camper_DonorPose_Profile.asset'
    foreach ($asset in @($cop,$pose)) {
        $path = Join-Path $ProjectPath ($asset.Replace('/', '\'))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail "Successful earlier camper Unity asset missing: $path" }
    }
    $poseReport = Join-Path $ProjectPath 'REBIRTH_CamperDonorPose_Reports\4c31a760c8fd442b9e2d882e7e16712c\result.json'
    if (-not (Test-Path -LiteralPath $poseReport -PathType Leaf)) { Fail 'Earlier v1.8 pose result.json missing.' }
    $poseObj = Get-Content -LiteralPath $poseReport -Raw | ConvertFrom-Json
    if ($poseObj.runId -cne '4c31a760c8fd442b9e2d882e7e16712c' -or
        $poseObj.status -cne 'PASS_DONOR_XML_POSE_REVIEW_NOT_GAME_VERIFIED') {
        Fail 'Earlier successful donor pose report was not recognized.'
    }
    Write-Host 'PASS: actual Unity camper, donor XML and pose-40/41 inputs verified' -ForegroundColor Green

    Stage 2 'Checking v2.5 verified player-to-seat evidence and additive harness'
    $reference = Join-Path $PSScriptRoot 'Reference\VERIFIED_4X4_DRIVER_REFERENCE.json'
    $script = Join-Path $PSScriptRoot 'UnitySources\RebirthUnityCamperFirstBundleTest.cs'
    foreach($p in @($reference,$script)) {
        if(-not (Test-Path -LiteralPath $p -PathType Leaf)) { Fail "Archive is missing $p" }
    }
    $ref=Get-Content -LiteralPath $reference -Raw | ConvertFrom-Json
    if ($ref.kind -cne 'REBIRTH_VERIFIED_VANILLA_4X4_DRIVER_REFERENCE' -or
        $ref.driverSeatIndex -ne 0 -or $ref.driverPoseId -ne 40 -or
        $ref.requiredTransformMissing -ne 0 -or
        $ref.driverSeatVerification -notmatch 'GetAttached\(0\)' -or
        $null -eq $ref.driverBonesVehicleLocal.LeftFoot -or
        $null -eq $ref.driverBonesVehicleLocal.RightFoot) {
        Fail 'v2.5 donor reference is missing direct seat evidence or both feet.'
    }
    $core = Join-Path $ProjectPath 'Assets\REBIRTHVehicleTools\Editor\RebirthVehicleRigBuilder.cs'
    if(-not (Test-Path -LiteralPath $core -PathType Leaf)) { Fail 'Working Unity rig core not found.' }
    Write-Host 'PASS: seat 0 verified, pose 40, both legs, installed 4x4 references' -ForegroundColor Green

    Stage 3 'Installing ONE additive Unity Editor test harness; preserving previously tested source'
    $destination = Join-Path $ProjectPath 'Assets\REBIRTHQuickSmoke\Editor\RebirthUnityCamperFirstBundleTest.cs'
    if(Test-Path -LiteralPath $destination -PathType Leaf) {
        if((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -cne
            (Get-FileHash -LiteralPath $script -Algorithm SHA256).Hash) {
            Fail "Existing different Unity harness. No overwrite: $destination"
        }
        Write-Host 'PASS: identical harness already installed'
    } else {
        Copy-Item -LiteralPath $script -Destination $destination -ErrorAction Stop
        Write-Host "Installed additive harness: $destination"
    }

    Stage 4 'Running REAL camper prefab/bundle build with live elapsed-time heartbeat'
    $folder = Join-Path $ProjectPath "REBIRTH_CamperFirstBundle_Reports\$runId"
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $log=Join-Path $folder 'UnityEditor.log'
    $result=Join-Path $folder 'result.json'
    Write-Host "Run ID: $runId"
    Write-Host "Unity log: $log"
    Write-Host 'Close any Unity Editor already using this project.' -ForegroundColor Yellow
    $arguments = @(
       '-batchmode','-nographics','-quit','-accept-apiupdate',
       '-projectPath',(Quoted $ProjectPath),
       '-executeMethod','RebirthUnityCamperFirstBundleTest.RunBatch',
       '-rebirthCamperRunId',$runId,
       '-logFile',(Quoted $log)
    )
    $proc = $null
    try {
        $proc=Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -ErrorAction Stop
        $watch=[System.Diagnostics.Stopwatch]::StartNew()
        do {
            $hasExited = $proc.WaitForExit(10000)
            $proc.Refresh()
            if (-not $hasExited) {
                Write-Host ("  Unity still running - {0:n0}s elapsed. Log: {1}" -f $watch.Elapsed.TotalSeconds,$log)
            }
        } while(-not $hasExited)
    } finally {
        if($null -ne $proc -and -not $proc.HasExited) {
            Write-Warning 'Stopping Unity batch process because launcher was interrupted.'
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
    $exit=[int]$proc.ExitCode
    Write-Host "Unity exited: $exit; elapsed: $([math]::Round($watch.Elapsed.TotalSeconds,1)) seconds"

    Stage 5 'Validating FRESH Unity result and actual bundle/receipt files'
    if(-not (Test-Path -LiteralPath $result -PathType Leaf)) {
        if(Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 80 | Out-Host }
        Fail "Missing new Unity report: $result"
    }
    $r=Get-Content -LiteralPath $result -Raw | ConvertFrom-Json
    foreach($line in @($r.steps)) {Write-Host "  $line"}
    $resultValid=($exit -eq 0 -and $r.runId -ceq $runId -and
        $r.status -ceq 'PASS_FIRST_CAMPER_BUNDLE_NEEDS_GAME_XML_AND_TEST')
    if($resultValid) {
        $resultValid=([string]::IsNullOrWhiteSpace($r.bundlePath) -eq $false -and
            [string]::IsNullOrWhiteSpace($r.bundleReceipt) -eq $false -and
            (Test-Path -LiteralPath $r.bundlePath -PathType Leaf) -and
            (Test-Path -LiteralPath $r.bundleReceipt -PathType Leaf))
    }
    if(-not $resultValid) {
        Write-Host "Unity status: $($r.status); failure: $($r.error)" -ForegroundColor Red
        if(Test-Path -LiteralPath $log) { Get-Content -LiteralPath $log -Tail 80 | Out-Host }
        Fail 'First camper bundle test did NOT pass. No 7DTD installation performed.'
    }
    Write-Host "PASS: compiled Unity camper AssetBundle: $($r.bundlePath)" -ForegroundColor Green
    Write-Host "Receipt: $($r.bundleReceipt)"
    Write-Host "Prefab: $($r.prefabAsset)"

    Stage 6 'Summarizing safe stopping point (NOT deployed into 7DTD)'
    Write-Host 'STATUS: FIRST_CAMPER_BUNDLE_PASS_NOT_GAME_TESTED' -ForegroundColor Green
    Write-Host "Report: $result"
    Write-Host 'No XML, localization, REBIRTH or 7DTD files changed.' -ForegroundColor Yellow
    Write-Host 'Next: use installed game donors to generate separate test modlet XML/localization before attempting spawn.'
} catch {
    Write-Host "STATUS: FIRST_CAMPER_BUNDLE_FAILED: $($_.Exception.Message)" -ForegroundColor Red
    throw
}
