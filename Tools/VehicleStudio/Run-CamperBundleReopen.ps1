[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$ExistingResult,
    [string]$ProjectPath='E:\_Haven\shared\REBIRTHVehicleStudio_Camper',
    [string]$UnityExe='C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe'
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$runFolder=Join-Path $PSScriptRoot ('reports\bundle_reopen\'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $runFolder -Force | Out-Null
$execution=[ordered]@{status='FAIL';gameLaunched=$false;pid=$null;originalReport=$ExistingResult;resultPath=(Join-Path $runFolder 'result.json');error=$null}
$proc=$null
try {
    $coordPath='E:\_Haven\shared\coordination\HAV3N_to_codex.txt'
    if(Test-Path -LiteralPath $coordPath){
        $coord=Get-Content -LiteralPath $coordPath -Raw
        if($coord -match '(?m)^STATUS:\s*(QUIET|CLOSE_GAME)\s*$' -and $coord -match '(?m)^UNTIL:\s*(\d{4}-\d{2}-\d{2} \d{2}:\d{2})\s*$'){
            $until=[DateTime]::ParseExact($Matches[1],'yyyy-MM-dd HH:mm',[Globalization.CultureInfo]::InvariantCulture)
            $now=[TimeZoneInfo]::ConvertTimeBySystemTimeZoneId([DateTime]::UtcNow,'Eastern Standard Time')
            if($until -gt $now){throw 'HAV3N coordination hold active.'}
        }
    }
    if(@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count){throw 'Existing Unity process must finish first.'}
    $ExistingResult=(Resolve-Path -LiteralPath $ExistingResult).Path
    $prior=Get-Content -LiteralPath $ExistingResult -Raw | ConvertFrom-Json
    $before=(Get-FileHash -LiteralPath $prior.bundlePath).Hash.ToLowerInvariant()
    if($before -cne $prior.bundleSha256){throw 'Existing bundle receipt/hash mismatch.'}
    $ownershipPath=Join-Path $PSScriptRoot 'playable_harness_ownership.json'
    $owned=Get-Content -LiteralPath $ownershipPath -Raw | ConvertFrom-Json
    $target=Join-Path $ProjectPath 'Assets\REBIRTHQuickSmoke\Editor\RebirthCamperPlayableBuild.cs'
    if($owned.target -ine $target -or (Get-FileHash -LiteralPath $target).Hash -ne $owned.sha256){throw 'Existing harness ownership mismatch.'}
    Copy-Item -LiteralPath $target -Destination (Join-Path $runFolder 'previous_harness.cs.txt')
    Copy-Item -LiteralPath $ownershipPath -Destination (Join-Path $runFolder 'previous_ownership.json')
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnitySources\RebirthCamperPlayableBuild.cs') -Destination $target
    @{target=$target;sha256=(Get-FileHash -LiteralPath $target).Hash} | ConvertTo-Json | Set-Content -LiteralPath $ownershipPath -Encoding utf8
    $unityLog=Join-Path $runFolder 'UnityEditor.log'
    $args=@('-batchmode','-nographics','-quit','-projectPath',('"'+$ProjectPath+'"'),'-executeMethod','RebirthCamperPlayableBuild.VerifyExistingBatch','-rebirthCamperExistingReport',('"'+$ExistingResult+'"'),'-rebirthCamperVerifyReport',('"'+$runFolder+'"'),'-logFile',('"'+$unityLog+'"'))
    Initialize-CamperUnityTemporaryFiles $ProjectPath
    $proc=Start-Process -FilePath $UnityExe -ArgumentList $args -WindowStyle Hidden -PassThru
    $execution.pid=$proc.Id;$execution.status='RUNNING'
    $execution | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runFolder 'execution.json') -Encoding utf8
    while(-not $proc.WaitForExit(10000)){Write-Host "[HEARTBEAT] Bundle reopen Unity PID=$($proc.Id)"}
    $proc.Refresh()
    $result=Get-Content -LiteralPath $execution.resultPath -Raw | ConvertFrom-Json
    if($proc.ExitCode -ne 0 -or $result.status -cne 'PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED' -or
        -not $result.prefabVerified -or -not $result.originalsUnchanged -or
        $result.bundleSha256 -cne $before -or (Get-FileHash -LiteralPath $prior.bundlePath).Hash.ToLowerInvariant() -cne $before){
        throw "Immutable bundle verification failed: $($result.error)"
    }
    $execution.status='PASS_IMMUTABLE_BUNDLE_REOPEN'
    Write-Host "[PASS] Exact physics and preservation verified; report=$($execution.resultPath)"
}catch{$execution.status='FAIL';$execution.error=$_.Exception.ToString();Write-Host "[FAIL] $($_.Exception.Message)"}
finally{
    if($null -ne $proc -and -not $proc.HasExited){Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue}
    $execution | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runFolder 'execution.json') -Encoding utf8
}
if($execution.status -ne 'PASS_IMMUTABLE_BUNDLE_REOPEN'){exit 1}
