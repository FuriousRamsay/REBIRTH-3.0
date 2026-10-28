[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$FirstBundleResult,
    [Parameter(Mandatory=$true)][string]$CabinPlan,
    [string]$RearLampSelection='',
    [string]$ReportRoot='',
    [ValidateSet('FastLZ4','NativeHC')][string]$BundleCompression='FastLZ4',
    [string]$ProjectPath='E:\_Haven\shared\REBIRTHVehicleStudio_Camper',
    [string]$UnityExe='C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
Set-StrictMode -Version Latest
if(-not $RearLampSelection){$RearLampSelection=Join-Path $PSScriptRoot 'reports/rear_lens_review/ac1c07fbfa594894b6dc5ab1ff8d0e25/result.json'}
$runId=[Guid]::NewGuid().ToString('N')
if(-not $ReportRoot){$ReportRoot=Join-Path $PSScriptRoot 'reports/playable_build'}
$runFolder=Join-Path $ReportRoot $runId
New-Item -ItemType Directory -Path $runFolder -Force | Out-Null
$executionPath=Join-Path $runFolder 'execution.json'
$receipt=[ordered]@{kind='REBIRTH_CAMPER_PLAYABLE_BUILD_EXECUTION';runId=$runId;status='FAIL';startedUtc=[DateTime]::UtcNow.ToString('o');gameLaunched=$false;unityPid=$null;unityExitCode=$null;resultPath=(Join-Path $runFolder 'result.json');error=$null}
$proc=$null
$code=1
Start-Transcript -LiteralPath (Join-Path $runFolder 'launcher.log') | Out-Null
try {
    $cabin=Get-Content -LiteralPath $CabinPlan -Raw | ConvertFrom-Json
    if($cabin.capacity -ne 6 -or $cabin.status -ne 'STAGED_SIX_OCCUPANT_CANDIDATE_REQUIRES_AVATAR_FIT') { throw 'Measured six-place cabin source candidate required.' }
    Copy-Item -LiteralPath $CabinPlan -Destination (Join-Path $runFolder 'cabin_plan.json')
    Write-Host '[1 PREFLIGHT] Checking coordination and preserved first-bundle evidence'
    $coordPath='E:\_Haven\shared\coordination\HAV3N_to_codex.txt'
    if(Test-Path -LiteralPath $coordPath){
        $coord=Get-Content -LiteralPath $coordPath -Raw
        if($coord -match '(?m)^STATUS:\s*(QUIET|CLOSE_GAME)\s*$' -and $coord -match '(?m)^UNTIL:\s*(\d{4}-\d{2}-\d{2} \d{2}:\d{2})\s*$'){
            $until=[DateTime]::ParseExact($Matches[1],'yyyy-MM-dd HH:mm',[Globalization.CultureInfo]::InvariantCulture)
            $now=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'Eastern Standard Time')
            if($until -gt $now){throw "HAV3N coordination hold active until $until"}
        }
    }
    if(@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count -gt 0){throw 'An existing Unity process is running; no process will be stopped.'}
    if((Get-Content (Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt') -Raw) -notmatch '(?m)^m_EditorVersion:\s*2022\.3\.62f2\s*$'){throw 'Wrong Unity version.'}
    $prior=Get-Content -LiteralPath $FirstBundleResult -Raw | ConvertFrom-Json
    $rearSelection=Get-Content -LiteralPath $RearLampSelection -Raw | ConvertFrom-Json
    if($rearSelection.status -cne 'PASS_GEOMETRY_PROPOSAL_NOT_LIGHTING_ACCEPTANCE' -or $rearSelection.rearTriangles -le 0){throw 'Qualified rear geometry receipt required.'}
    Copy-Item -LiteralPath $RearLampSelection -Destination (Join-Path $runFolder 'rear_selection.json')
    if($prior.status -cne 'PASS_FIRST_CAMPER_BUNDLE_NEEDS_GAME_XML_AND_TEST' -or -not $prior.originalAssetsUnchanged){throw 'First bundle did not pass preservation/build verification.'}
    $original=Join-Path $ProjectPath 'Assets\Perfectvertex\80sCamperTruck\Prefabs\assembled_vehicle.prefab'
    if((Get-FileHash -LiteralPath $original).Hash.ToLowerInvariant() -cne '75357bec0ab35826b9205011811f49ca743ea42270a30fb2baa0b82e358b916d'){throw 'Original camper changed.'}
    Write-Host '[2 HARNESS] Installing this owned additive playable authoring harness and recording rollback inputs'
    $shaderSource=Join-Path $PSScriptRoot 'UnitySources\CamperBodyDye.shader'
    $shaderFolder=Join-Path $ProjectPath 'Assets\REBIRTHCamperShaders'
    $shaderTarget=Join-Path $shaderFolder 'CamperBodyDye.shader'
    if(Test-Path -LiteralPath $shaderTarget){
        $actualShaderHash=(Get-FileHash -LiteralPath $shaderTarget).Hash
        if($actualShaderHash -ne (Get-FileHash -LiteralPath $shaderSource).Hash){
            $knownShader=@(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'reports/playable_build') -Recurse -Filter 'dye_shader_ownership.json' | ForEach-Object {
                $record=Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json
                if($record.target -ieq $shaderTarget -and $record.sha256 -ieq $actualShaderHash){$record}
            })
            if($knownShader.Count -eq 0){throw 'Existing dye shader differs and has no matching prior ownership receipt; preserve it.'}
            Copy-Item -LiteralPath $shaderTarget -Destination (Join-Path $runFolder 'previous_CamperBodyDye.shader.txt')
            Copy-Item -LiteralPath $shaderSource -Destination $shaderTarget -Force
        }
    }else{
        New-Item -ItemType Directory -Path $shaderFolder -Force | Out-Null
        Copy-Item -LiteralPath $shaderSource -Destination $shaderTarget
    }
    @{target=$shaderTarget;sha256=(Get-FileHash -LiteralPath $shaderTarget).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runFolder 'dye_shader_ownership.json') -Encoding utf8
    $harness=Join-Path $PSScriptRoot 'UnitySources\RebirthCamperPlayableBuild.cs'
    $auxOwned=@()
    foreach($auxName in @('RebirthCamperRearLensReview','RebirthCamperRearLampAuthoring','RebirthCamperBedPoseAuthoring','RebirthCamperDirectionalLampAuthoring')){
        $auxSource=Join-Path $PSScriptRoot "UnitySources/$auxName.cs"
        $auxTarget=Join-Path $ProjectPath "Assets/REBIRTHQuickSmoke/Editor/$auxName.cs"
        $auxHash=(Get-FileHash -LiteralPath $auxSource).Hash
        if(Test-Path -LiteralPath $auxTarget){
            $actualHash=(Get-FileHash -LiteralPath $auxTarget).Hash
            if($actualHash -ne $auxHash){
                $receiptName=if($auxName -eq 'RebirthCamperRearLensReview'){'rear_lens_review_ownership.json'}elseif($auxName -eq 'RebirthCamperRearLampAuthoring'){'rear_lamp_authoring_ownership.json'}elseif($auxName -eq 'RebirthCamperDirectionalLampAuthoring'){'directional_lamp_authoring_ownership.json'}else{'bed_pose_authoring_ownership.json'}
                $known=Get-Content -LiteralPath (Join-Path $PSScriptRoot $receiptName) -Raw | ConvertFrom-Json
                if($known.harness -ine $auxTarget -or $known.harnessSha -ne $actualHash){throw "Existing $auxName harness changed externally."}
                Copy-Item -LiteralPath $auxTarget -Destination (Join-Path $runFolder "previous_$auxName.cs.txt")
            }
        }
        Copy-Item -LiteralPath $auxSource -Destination $auxTarget -Force
        if($auxName -eq 'RebirthCamperBedPoseAuthoring'){
            @{harness=$auxTarget;harnessSha=$auxHash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'bed_pose_authoring_ownership.json') -Encoding utf8
        }
        if($auxName -eq 'RebirthCamperDirectionalLampAuthoring'){
            @{harness=$auxTarget;harnessSha=$auxHash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'directional_lamp_authoring_ownership.json') -Encoding utf8
        }
        $auxOwned+=@{target=$auxTarget;sha256=$auxHash}
    }
    $auxOwned | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runFolder 'aux_harness_ownership.json') -Encoding utf8
    $target=Join-Path $ProjectPath 'Assets\REBIRTHQuickSmoke\Editor\RebirthCamperPlayableBuild.cs'
    $ownership=Join-Path $PSScriptRoot 'playable_harness_ownership.json'
    if(Test-Path -LiteralPath $target){
        $actual=(Get-FileHash -LiteralPath $target).Hash
        if($actual -ne (Get-FileHash -LiteralPath $harness).Hash){
            if(-not(Test-Path -LiteralPath $ownership)){throw 'Existing harness is not recorded as owned by VehicleStudio.'}
            $owned=Get-Content -LiteralPath $ownership -Raw | ConvertFrom-Json
            if($owned.target -cne $target -or $owned.sha256 -cne $actual){throw 'Existing harness changed outside this runner; refusing overwrite.'}
            Copy-Item -LiteralPath $target -Destination (Join-Path $runFolder 'previous_harness.cs.txt')
        }
    }
    Copy-Item -LiteralPath (Join-Path $ProjectPath 'ProjectSettings\TagManager.asset') -Destination (Join-Path $runFolder 'TagManager.before.asset')
    Copy-Item -LiteralPath $harness -Destination $target -Force
    @{target=$target;sha256=(Get-FileHash -LiteralPath $target).Hash} | ConvertTo-Json | Set-Content -LiteralPath $ownership -Encoding utf8
    $bundleOutput=Join-Path $camperTemporaryDirectory ('CamperBundles\'+$runId)
    New-Item -ItemType Directory -Path $bundleOutput -Force | Out-Null
    $unityLog=Join-Path $runFolder 'UnityEditor.log'
    $args=@('-batchmode','-nographics','-quit','-accept-apiupdate','-projectPath',('"'+$ProjectPath+'"'),'-executeMethod','RebirthCamperPlayableBuild.RunBatch','-rebirthCamperRunId',$runId,'-rebirthCamperReport',('"'+$runFolder+'"'),'-rebirthCamperSourcePrefab',('"'+$prior.prefabAsset+'"'),'-rebirthCamperRearSelection',('"'+(Join-Path $runFolder 'rear_selection.json')+'"'),'-logFile',('"'+$unityLog+'"'))
    $args+=@('-rebirthCamperBundleOutput',('"'+$bundleOutput+'"'))
    $args+=@('-rebirthCamperCompression',$BundleCompression)
    $args+=@('-rebirthCamperCabinPlan',('"'+(Join-Path $runFolder 'cabin_plan.json')+'"'))
    Write-Host "[3 UNITY] Compiling geometry, native physics, lights, six-seat candidate and humanoid bed clip; log=$unityLog"
    Initialize-CamperUnityTemporaryFiles $ProjectPath
    $proc=Start-Process -FilePath $UnityExe -ArgumentList $args -WindowStyle Hidden -PassThru
    $receipt.unityPid=$proc.Id
    $receipt.status='RUNNING'
    $receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $executionPath -Encoding utf8
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(-not $proc.WaitForExit(10000)){
        $proc.Refresh()
        $bytes=if(Test-Path -LiteralPath $unityLog){(Get-Item -LiteralPath $unityLog).Length}else{0}
        Write-Host "[HEARTBEAT] Unity PID=$($proc.Id); elapsed=$([Math]::Round($watch.Elapsed.TotalSeconds))s; logBytes=$bytes"
    }
    $proc.Refresh(); $receipt.unityExitCode=$proc.ExitCode
    Write-Host "[4 VERIFY] Unity exit=$($proc.ExitCode); validating fresh report and bundle checksum"
    if(-not(Test-Path -LiteralPath $receipt.resultPath)){throw "Missing result: inspect $unityLog"}
    $result=Get-Content -LiteralPath $receipt.resultPath -Raw | ConvertFrom-Json
    foreach($step in $result.steps){Write-Host $step}
    if($proc.ExitCode -ne 0 -or $result.runId -cne $runId -or $result.status -cne 'PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED'){throw "Playable prefab build failed: $($result.error)"}
    if((Get-FileHash -LiteralPath $result.bundlePath).Hash.ToLowerInvariant() -cne $result.bundleSha256 -or -not $result.prefabVerified -or -not $result.originalsUnchanged){throw 'Bundle receipt/preservation check failed.'}
    if((Get-FileHash -LiteralPath $original).Hash.ToLowerInvariant() -cne '75357bec0ab35826b9205011811f49ca743ea42270a30fb2baa0b82e358b916d'){throw 'Original camper preservation check failed.'}
    $receipt.status='PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED';$code=0
    Write-Host "[5 PASS] Bundle=$($result.bundlePath); prefab=$($result.prefabName); result=$($receipt.resultPath)"
}catch{
    $receipt.status='FAIL';$receipt.error=$_.Exception.ToString()
    Write-Host "[FAIL] $($_.Exception.Message)"
}finally{
    if($null -ne $proc -and -not $proc.HasExited){Write-Host "[CANCEL] Stopping this owned Unity batch PID=$($proc.Id)";Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue}
    $receipt.finishedUtc=[DateTime]::UtcNow.ToString('o')
    $receipt | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $executionPath -Encoding utf8
    Write-Host "[RESULT] $($receipt.status); report=$executionPath"
    Stop-Transcript | Out-Null
}
exit $code



