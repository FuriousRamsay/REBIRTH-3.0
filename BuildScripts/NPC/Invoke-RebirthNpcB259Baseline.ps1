[CmdletBinding()]
param(
    [string]$GameRoot = $env:REBIRTH_7DTD_ROOT,
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [string]$OutputRoot = '',
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = (Resolve-Path (Join-Path $scriptDir '..\..')).Path
$projectFile = Join-Path $projectRoot 'RebirthUtils.csproj'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $projectRoot 'BuildArtifacts\NPC-b259'
}
New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null

function Resolve-GameRoot([string]$candidate) {
    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($candidate)) { $candidates += $candidate }
    $candidates += @(
        'C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die',
        'C:\Program Files\Steam\steamapps\common\7 Days To Die'
    )
    foreach ($path in $candidates) {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        $managed = Join-Path $path '7DaysToDie_Data\Managed'
        if (Test-Path (Join-Path $managed 'Assembly-CSharp.dll')) { return (Resolve-Path $path).Path }
    }
    throw 'Could not locate a 7 Days to Die installation. Pass -GameRoot or set REBIRTH_7DTD_ROOT.'
}

$GameRoot = Resolve-GameRoot $GameRoot
$managedRoot = Join-Path $GameRoot '7DaysToDie_Data\Managed'
$required = @(
    'Assembly-CSharp.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','UnityEngine.AnimationModule.dll',
    'UnityEngine.AudioModule.dll','UnityEngine.ImageConversionModule.dll','UnityEngine.IMGUIModule.dll',
    'UnityEngine.InputLegacyModule.dll','UnityEngine.InputModule.dll','InControl.dll','LogLibrary.dll',
    'NCalc.dll','NGUI.dll','Noemax.GZip.dll','Pathfinding.ClipperLib.dll','Unity.Addressables.dll',
    'Unity.Collections.dll','Unity.Postprocessing.Runtime.dll','UnityEngine.ParticleSystemModule.dll',
    'UnityEngine.PhysicsModule.dll','UnityEngine.UI.dll','UnityEngine.VehiclesModule.dll','UnityEngine.VideoModule.dll'
)

$assemblyRows = foreach ($name in $required) {
    $path = Join-Path $managedRoot $name
    if (-not (Test-Path $path)) { throw "Required b259 managed assembly is missing: $path" }
    $item = Get-Item $path
    $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($path)
    [pscustomobject]@{
        Name = $name
        Length = $item.Length
        LastWriteTimeUtc = $item.LastWriteTimeUtc.ToString('o')
        FileVersion = $version.FileVersion
        ProductVersion = $version.ProductVersion
        Sha256 = (Get-FileHash -Algorithm SHA256 -Path $path).Hash.ToLowerInvariant()
    }
}

$harmony = Join-Path (Split-Path -Parent $projectRoot) '0_TFP_Harmony\0Harmony.dll'
if (-not (Test-Path $harmony)) { throw "Required Harmony assembly is missing: $harmony" }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$manifestPath = Join-Path $OutputRoot "REBIRTH_NPC_B259_API_BASELINE_$stamp.json"
$assemblyRows | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $manifestPath

$sourceAudit = Join-Path $scriptDir 'Invoke-RebirthNpcSourceApiAudit.ps1'
& $sourceAudit -ProjectRoot $projectRoot -OutputRoot $OutputRoot

if ($NoBuild) {
    Write-Host "Validated required references and wrote baseline manifest: $manifestPath"
    exit 0
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) { throw 'dotnet SDK was not found on PATH.' }

$logPath = Join-Path $OutputRoot "REBIRTH_NPC_B259_BUILD_$stamp.log"
$args = @(
    'build', $projectFile,
    '--configuration', $Configuration,
    '--no-incremental',
    '-p:RebirthGameRoot=' + $GameRoot,
    '-p:RebirthValidateB259References=true',
    '-p:ContinuousIntegrationBuild=true',
    '-p:Deterministic=true',
    '-p:BaseOutputPath=' + (Join-Path $OutputRoot 'bin\'),
    '-p:BaseIntermediateOutputPath=' + (Join-Path $OutputRoot 'obj\')
)

& $dotnet.Source @args 2>&1 | Tee-Object -FilePath $logPath
if ($LASTEXITCODE -ne 0) {
    $npcErrors = Select-String -Path $logPath -Pattern 'Scripts[\\/]Rebirth[\\/]NPC.*error CS' -SimpleMatch:$false
    $errorPath = Join-Path $OutputRoot "REBIRTH_NPC_B259_ERRORS_$stamp.txt"
    $npcErrors | ForEach-Object { $_.Line } | Set-Content -Encoding UTF8 $errorPath
    throw "Build failed. Full log: $logPath; NPC error extract: $errorPath"
}

$dll = Get-ChildItem -Path (Join-Path $OutputRoot 'bin') -Filter RebirthUtils.dll -Recurse | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
if ($null -eq $dll) { throw 'Build reported success but RebirthUtils.dll was not found.' }

$result = [pscustomobject]@{
    Status = 'Succeeded'
    Configuration = $Configuration
    GameRoot = $GameRoot
    Project = $projectFile
    OutputAssembly = $dll.FullName
    OutputAssemblySha256 = (Get-FileHash -Algorithm SHA256 -Path $dll.FullName).Hash.ToLowerInvariant()
    ApiBaselineManifest = $manifestPath
    BuildLog = $logPath
    CompletedUtc = [DateTime]::UtcNow.ToString('o')
}
$resultPath = Join-Path $OutputRoot "REBIRTH_NPC_B259_BUILD_RESULT_$stamp.json"
$result | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 $resultPath
Write-Host "NPC b259 baseline build succeeded. Result: $resultPath"
