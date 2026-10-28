[CmdletBinding()]
param(
    [string]$ProjectPath = 'E:\_Haven\shared\REBIRTHVehicleStudio_Camper',
    [string]$UnityExe = 'C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe',
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
Set-StrictMode -Version Latest
$runId = [Guid]::NewGuid().ToString('N')
$runFolder = Join-Path $PSScriptRoot "reports\first_bundle\$runId"
New-Item -ItemType Directory -Path $runFolder -Force | Out-Null
$logPath = Join-Path $runFolder 'launcher.log'
$receiptPath = Join-Path $runFolder 'execution.json'
$receipt = [ordered]@{ kind='REBIRTH_CAMPER_FIRST_BUNDLE_EXECUTION'; runId=$runId; status='FAIL'; startedUtc=[DateTime]::UtcNow.ToString('o'); project=$ProjectPath; gameLaunched=$false; stages=@(); unityExitCode=$null; unityResult=$null; error=$null }
$proc = $null
$exitCode = 1
function Stage([string]$name, [string]$message) {
    Write-Host "[$name] $message"
    $receipt.stages += [ordered]@{name=$name; message=$message; utc=[DateTime]::UtcNow.ToString('o')}
}
function RequireHash([string]$path, [string]$expected) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required input is missing: $path" }
    $actual=(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -cne $expected) { throw "Input hash mismatch: $path; expected=$expected; actual=$actual" }
}
function QuoteArg([string]$value) {
    if ($value.Contains('"')) { throw 'A path contains an unsupported quote.' }
    return '"'+$value+'"'
}
Start-Transcript -LiteralPath $logPath -Force | Out-Null
try {
    Stage '1 PREFLIGHT' 'Checking coordination, process ownership, exact Unity and source inputs'
    $coordPath='E:\_Haven\shared\coordination\HAV3N_to_codex.txt'
    if (Test-Path -LiteralPath $coordPath) {
        $coord=Get-Content -LiteralPath $coordPath -Raw
        if ($coord -match '(?m)^STATUS:\s*(QUIET|CLOSE_GAME)\s*$') {
            $coordStatus=$Matches[1]
            if ($coord -match '(?m)^UNTIL:\s*(\d{4}-\d{2}-\d{2} \d{2}:\d{2})\s*$') {
                $until=[DateTime]::ParseExact($Matches[1],'yyyy-MM-dd HH:mm',[Globalization.CultureInfo]::InvariantCulture)
                $localNow=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'Eastern Standard Time')
                if ($until -gt $localNow) { throw "HAV3N $coordStatus is active until $until; offline Unity job deferred." }
            }
        }
    }
    $unityProcesses=@(Get-Process -Name Unity -ErrorAction SilentlyContinue)
    if ($unityProcesses.Count -ne 0) { throw 'Unity is already running. No existing process will be stopped or reused.' }
    if (-not (Test-Path -LiteralPath $UnityExe -PathType Leaf)) { throw "Unity executable missing: $UnityExe" }
    $version=Get-Content -LiteralPath (Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt') -Raw
    if ($version -notmatch '(?m)^m_EditorVersion:\s*2022\.3\.62f2\s*$') { throw 'Unity project must use exactly 2022.3.62f2.' }
    $original=Join-Path $ProjectPath 'Assets\Perfectvertex\80sCamperTruck\Prefabs\assembled_vehicle.prefab'
    RequireHash $original '75357bec0ab35826b9205011811f49ca743ea42270a30fb2baa0b82e358b916d'
    RequireHash ($original+'.meta') '83ebb10b3afb151b40ba4fee9901740c0b0a5396d850c66e4c156e6d5e66c69a'
    RequireHash (Join-Path $GamePath 'Data\Config\vehicles.xml') '4abdce695fc9d65ca14063dce67b9fe84ebe6736b2ecf5ac9e397be3c6c3d145'
    $poseReport=Join-Path $ProjectPath 'REBIRTH_CamperDonorPose_Reports\4c31a760c8fd442b9e2d882e7e16712c\result.json'
    $prior=Get-Content -LiteralPath $poseReport -Raw | ConvertFrom-Json
    if ($prior.status -cne 'PASS_DONOR_XML_POSE_REVIEW_NOT_GAME_VERIFIED' -or $prior.savedSeatCount -ne 2 -or $prior.driverPoseId -ne '40' -or $prior.passengerPoseId -ne '41') { throw 'Prior two-seat pose result does not match expected evidence.' }
    Stage '2 HARNESS' 'Installing the reviewed additive v1.9 harness without overwriting existing work'
    $inputRoot=Join-Path $PSScriptRoot 'inputs\first_bundle_v1.9'
    $harness=Join-Path $inputRoot 'UnitySources\RebirthUnityCamperFirstBundleTest.cs'
    $reference=Get-Content -LiteralPath (Join-Path $inputRoot 'Reference\VERIFIED_4X4_DRIVER_REFERENCE.json') -Raw | ConvertFrom-Json
    if ($reference.driverSeatIndex -ne 0 -or $reference.driverPoseId -ne 40 -or $reference.requiredTransformMissing -ne 0) { throw 'Direct vanilla seat reference is invalid.' }
    $target=Join-Path $ProjectPath 'Assets\REBIRTHQuickSmoke\Editor\RebirthUnityCamperFirstBundleTest.cs'
    if (Test-Path -LiteralPath $target) {
        if ((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $harness).Hash) { throw "Different harness already exists; refusing overwrite: $target" }
    } else { Copy-Item -LiteralPath $harness -Destination $target }
    $unityReportFolder=Join-Path $ProjectPath "REBIRTH_CamperFirstBundle_Reports\$runId"
    New-Item -ItemType Directory -Path $unityReportFolder -Force | Out-Null
    $unityLog=Join-Path $unityReportFolder 'UnityEditor.log'
    $unityResult=Join-Path $unityReportFolder 'result.json'
    $receipt.unityResult=$unityResult
    Stage '3 UNITY' "Building actual camper bundle; Unity log: $unityLog"
    $arguments=@('-batchmode','-nographics','-quit','-accept-apiupdate','-projectPath',(QuoteArg $ProjectPath),'-executeMethod','RebirthUnityCamperFirstBundleTest.RunBatch','-rebirthCamperRunId',$runId,'-logFile',(QuoteArg $unityLog))
    $proc=Start-Process -FilePath $UnityExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $receipt.unityPid=$proc.Id
    $receipt.status='RUNNING'
    $receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding UTF8
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while (-not $proc.WaitForExit(10000)) {
        $proc.Refresh()
        $logBytes=if(Test-Path -LiteralPath $unityLog){(Get-Item -LiteralPath $unityLog).Length}else{0}
        Write-Host "[HEARTBEAT] Unity PID=$($proc.Id); elapsed=$([Math]::Round($watch.Elapsed.TotalSeconds))s; logBytes=$logBytes"
    }
    $proc.Refresh()
    $receipt.unityExitCode=$proc.ExitCode
    Stage '4 VERIFY' "Unity exited $($proc.ExitCode); checking fresh result and bundle receipt"
    if (Test-Path -LiteralPath $unityLog) { Copy-Item -LiteralPath $unityLog -Destination (Join-Path $runFolder 'UnityEditor.log') }
    if (-not (Test-Path -LiteralPath $unityResult)) { throw "Unity did not emit result.json. Inspect $unityLog" }
    Copy-Item -LiteralPath $unityResult -Destination (Join-Path $runFolder 'result.json')
    $result=Get-Content -LiteralPath $unityResult -Raw | ConvertFrom-Json
    foreach ($step in $result.steps) { Write-Host $step }
    if ($proc.ExitCode -ne 0 -or $result.runId -cne $runId -or $result.status -cne 'PASS_FIRST_CAMPER_BUNDLE_NEEDS_GAME_XML_AND_TEST') { throw "Unity camper build failed: $($result.error)" }
    $bundleReceipt=Get-Content -LiteralPath $result.bundleReceipt -Raw | ConvertFrom-Json
    RequireHash $result.bundlePath $bundleReceipt.bundleSha256
    if (-not $bundleReceipt.prefabVerified -or $bundleReceipt.unityEditorVersion -cne '2022.3.62f2' -or $result.rendererCount -ne 16 -or $result.seatCount -ne 2 -or -not $result.originalAssetsUnchanged) { throw 'Bundle or source preservation verification failed.' }
    Copy-Item -LiteralPath $result.bundleReceipt -Destination (Join-Path $runFolder 'bundle.rebirth-receipt.json')
    $receipt.bundlePath=$result.bundlePath
    $receipt.bundleSha256=$bundleReceipt.bundleSha256
    $receipt.status='PASS_BUNDLE_BUILT_AND_REOPENED_NOT_GAME_TESTED'
    Stage '5 PASS' "Bundle: $($result.bundlePath); prefab: $($bundleReceipt.prefabName)"
    $exitCode=0
} catch {
    $receipt.error=$_.Exception.ToString()
    Write-Host "[FAIL] $($_.Exception.Message)"
} finally {
    if ($null -ne $proc -and -not $proc.HasExited) {
        Write-Host "[CANCEL] Stopping only this runner's Unity batch PID=$($proc.Id)"
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }
    $receipt.finishedUtc=[DateTime]::UtcNow.ToString('o')
    $receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding UTF8
    Write-Host "[RESULT] $($receipt.status); report=$receiptPath; log=$logPath"
    Stop-Transcript | Out-Null
}
exit $exitCode
