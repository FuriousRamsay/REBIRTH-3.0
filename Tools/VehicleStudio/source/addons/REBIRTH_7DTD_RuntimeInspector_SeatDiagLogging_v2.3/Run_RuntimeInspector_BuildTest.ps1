# REBIRTH Vehicle Studio v1.9 - read-only environment/compile test.
# DOES NOT edit 7DTD, REBIRTH, Unity, mods or save games. No DLL installation.
[CmdletBinding()]
param(
    [string]$GamePath = 'C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die',
    [ValidateSet('Preflight','Build')][string]$Mode = 'Build',
    [string]$ReportsRoot = (Join-Path $env:USERPROFILE 'Downloads\REBIRTH_RuntimeInspector_BuildReports')
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$runId=[guid]::NewGuid().ToString('N')
$runPath=Join-Path $ReportsRoot $runId
$null=New-Item -ItemType Directory -Path $runPath -Force
$reportZip=Join-Path $ReportsRoot ("REBIRTH_RuntimeInspector_BuildReport_{0}.zip" -f $runId)
$logOut=Join-Path $runPath 'dotnet_stdout.log'
$logErr=Join-Path $runPath 'dotnet_stderr.log'
$project=Join-Path $PSScriptRoot 'src\VehicleInspector\VehicleInspector.csproj'
$gameDll=Join-Path $GamePath '7DaysToDie_Data\Managed\Assembly-CSharp.dll'
$firstPass=Join-Path $GamePath '7DaysToDie_Data\Managed\Assembly-CSharp-firstpass.dll'
$vehicleXml=Join-Path $GamePath 'Data\Config\vehicles.xml'
$modInfo=Join-Path $PSScriptRoot 'Mod\ModInfo.xml'
$versionSource=Join-Path $PSScriptRoot 'RELEASE_VERSION.txt'
$steps=New-Object 'System.Collections.Generic.List[object]'
function Add-Step([string]$name,[bool]$ok,[string]$detail) {
    $script:steps.Add([pscustomobject]@{check=$name;ok=$ok;detail=$detail})
    Write-Host ('{0}: {1} - {2}' -f $(if ($ok) {'PASS'} else {'FAIL'}),$name,$detail)
}
function ShaOrNull([string]$path) {if(Test-Path -LiteralPath $path -PathType Leaf){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()}; return $null}
$stage='PREFLIGHT'
$buildExitCode=$null
$summary='NOT_RUN'
$dotnetCommand=$null
$compilerErrors=@()
$builtDllSha256=$null
try {
    Add-Step 'game_root' (Test-Path -LiteralPath $GamePath -PathType Container) $GamePath
    Add-Step 'managed_assembly' (Test-Path -LiteralPath $gameDll -PathType Leaf) $gameDll
    Add-Step 'game_vehicles_xml' (Test-Path -LiteralPath $vehicleXml -PathType Leaf) $vehicleXml
    Add-Step 'runtime_project' (Test-Path -LiteralPath $project -PathType Leaf) $project
    Add-Step 'mod_manifest' (Test-Path -LiteralPath $modInfo -PathType Leaf) $modInfo
    $sdk=Get-Command 'dotnet.exe' -ErrorAction SilentlyContinue
    if(-not $sdk) {$sdk=Get-Command 'dotnet' -ErrorAction SilentlyContinue}
    $dotnetCommand=if($sdk){$sdk.Source}else{$null}
    Add-Step 'dotnet_sdk_available' ($null -ne $sdk) $(if($sdk){$sdk.Source}else{'dotnet not on PATH; install current .NET SDK (not just runtime)'})
    if ($sdk) {
        try {
            $sdkList=(& $dotnetCommand --list-sdks 2>&1 | Out-String).Trim()
            Add-Step 'sdk_list' ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($sdkList)) $sdkList
            $sdkList | Out-File -LiteralPath (Join-Path $runPath 'installed_sdks.txt') -Encoding utf8
        } catch { Add-Step 'sdk_list' $false $_.Exception.Message }
    }
    $exists=@($steps | Where-Object { -not $_.ok })
    if ($exists.Count -gt 0) {
        $summary='PREFLIGHT_BLOCKED'
    } elseif ($Mode -eq 'Preflight') {
        $summary='PREFLIGHT_PASS'
    } else {
        $stage='BUILD'
        $arguments='build "{0}" -c Release -p:Game7D2D="{1}" -nologo -v:minimal' -f $project,$GamePath
        $commandText='dotnet '+$arguments
        $commandText | Out-File -LiteralPath (Join-Path $runPath 'build_command.txt') -Encoding utf8
        Write-Host 'Building original read-only inspector against local game assemblies; no mods are installed.'
        # Start-Process / Wait / ExitCode fixes the missing-exit-code mistake in the earlier Unity launcher.
        # Do not block silently: report a heartbeat every 15 seconds during restoration/compilation.
        $proc=Start-Process -FilePath $dotnetCommand -ArgumentList $arguments -WorkingDirectory $PSScriptRoot -PassThru -NoNewWindow -RedirectStandardOutput $logOut -RedirectStandardError $logErr
        $startedAt=[DateTime]::UtcNow
        $ticks=0
        while(-not $proc.WaitForExit(3000)) {
            $ticks++
            if(($ticks % 5) -eq 0) {
                $elapsed=[math]::Round(([DateTime]::UtcNow-$startedAt).TotalSeconds)
                Write-Host "Build still running ($elapsed seconds). Logs: $runPath"
            }
        }
        $proc.WaitForExit() # ensures stdout/stderr redirection is drained
        $proc.Refresh()
        $buildExitCode=[int]$proc.ExitCode
        Write-Host "dotnet build exit code: $buildExitCode"
        $buildTail=if(Test-Path $logOut){@(Get-Content -LiteralPath $logOut -Tail 80)}else{@()}
        if($buildTail.Count -gt 0){Write-Host (($buildTail | Select-Object -Last 22) -join [Environment]::NewLine)}
        $allLogLines=@($buildTail)
        if (Test-Path -LiteralPath $logErr) {$allLogLines+=@(Get-Content -LiteralPath $logErr -Tail 60)}
        $compilerErrors=@($allLogLines | Where-Object {$_ -match '(: error |error (CS|MSB|NU)\d+|fatal error|Exception|not found|NETSDK\d+)' } | Select-Object -Last 40)
        $builtDll=Join-Path $PSScriptRoot 'src\VehicleInspector\bin\Release\RebirthVehicleInspector.dll'
        if($buildExitCode -eq 0 -and (Test-Path -LiteralPath $builtDll -PathType Leaf) -and (Get-Item -LiteralPath $builtDll).Length -gt 4096) {
            $null=New-Item -ItemType Directory -Force -Path (Join-Path $runPath 'compiled')
            Copy-Item -LiteralPath $builtDll -Destination (Join-Path $runPath 'compiled\RebirthVehicleInspector.dll')
            $builtDllSha256=ShaOrNull (Join-Path $runPath 'compiled\RebirthVehicleInspector.dll')
            $summary='BUILD_PASS_NOT_INSTALLED'
        } else {
            $summary='BUILD_FAIL_NOT_INSTALLED'
            Add-Step 'compiled_dll' $false ('Compiler exit code: '+$buildExitCode+'; inspect dotnet_stdout.log and dotnet_stderr.log.')
        }
    }
} catch {
    $summary='EXCEPTION_NOT_INSTALLED'
    $_ | Out-String | Out-File -LiteralPath (Join-Path $runPath 'exception.txt') -Encoding utf8
    Write-Warning ('Build-test exception: '+$_.Exception.Message)
} finally {
    $report=[ordered]@{
        schemaVersion=1; tool='REBIRTH 7DTD Runtime Inspector build test'; version='2.0';
        runId=$runId; status=$summary; stage=$stage; gamePath=$GamePath;
        gameAssemblySha256=(ShaOrNull $gameDll); firstpassSha256=(ShaOrNull $firstPass);
        vanillaVehiclesXmlSha256=(ShaOrNull $vehicleXml); inspectorSourceProjectSha256=(ShaOrNull $project);
        modInfoSha256=(ShaOrNull $modInfo); requestedMode=$Mode;
        dotnetCommand=$dotnetCommand; buildExitCode=$buildExitCode; compiledDllSha256=$builtDllSha256;
        tests=@($steps.ToArray()); compilerErrors=@($compilerErrors);
        installedDll=$false; modifiedGameFiles=$false;
        capturedUtc=([DateTime]::UtcNow.ToString('o'))
    }
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $runPath 'result.json') -Encoding utf8
    if (Test-Path $reportZip) {throw 'Unexpected duplicate report ZIP; refusing overwrite.'}
    Compress-Archive -Path (Join-Path $runPath '*') -DestinationPath $reportZip
    Write-Host "STATUS: $summary"
    Write-Host "Report JSON: $(Join-Path $runPath 'result.json')"
    Write-Host "Report ZIP: $reportZip"
    Write-Host 'No mod, installed game, Unity asset, or REBIRTH file was changed.'
}
if ($summary -notin @('PREFLIGHT_PASS','BUILD_PASS_NOT_INSTALLED')) {exit 1}
