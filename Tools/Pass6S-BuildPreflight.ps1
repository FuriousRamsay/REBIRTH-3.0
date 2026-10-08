param(
    [string]$GameRoot = "",
    [ValidateSet("Debug","Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

function Assert-RebirthGameClosed {
    $runningGame = @(Get-Process -Name 7DaysToDie,7DaysToDieServer -ErrorAction SilentlyContinue)
    if ($runningGame.Count -gt 0) {
        throw "REBIRTH build deferred: 7 Days To Die is running. Close it before building; this script will not stop it."
    }
}
Assert-RebirthGameClosed
$projectRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $projectRoot "RebirthUtils.csproj"

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = Resolve-Path (Join-Path $projectRoot "..\..")
}

$managed = Join-Path $GameRoot "7DaysToDie_Data\Managed"
$required = @(
    (Join-Path $managed "Assembly-CSharp.dll"),
    (Join-Path $managed "mscorlib.dll"),
    (Join-Path $managed "UnityEngine.dll"),
    (Join-Path $managed "UnityEngine.CoreModule.dll"),
    (Join-Path $managed "UnityEngine.AudioModule.dll"),
    (Join-Path $projectRoot "..\0_TFP_Harmony\0Harmony.dll")
)

Write-Host "[REBIRTH Pass6S] Project: $project"
Write-Host "[REBIRTH Pass6S] GameRoot: $GameRoot"
Write-Host "[REBIRTH Pass6S] Managed: $managed"
Write-Host "[REBIRTH Pass6S] Configuration: $Configuration"

$missing = @()
foreach ($path in $required) {
    if (-not (Test-Path $path)) {
        $missing += $path
    }
}

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "Required compile references are missing:" -ForegroundColor Red
    foreach ($path in $missing) {
        Write-Host "  $path" -ForegroundColor Red
    }
    exit 2
}

$builder = $null
$builderArgs = @()

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) {
    $builder = $dotnet.Source
    $builderArgs = @(
        "build",
        $project,
        "-c", $Configuration,
        "-p:RebirthGameRoot=$GameRoot",
        "-p:RebirthManagedRoot=$managed",
        "--nologo"
    )
}
else {
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe |
            Select-Object -First 1
        if ($msbuild) {
            $builder = $msbuild
            $builderArgs = @(
                $project,
                "/t:Rebuild",
                "/p:Configuration=$Configuration",
                "/p:RebirthGameRoot=$GameRoot",
                "/p:RebirthManagedRoot=$managed",
                "/nologo",
                "/verbosity:minimal"
            )
        }
    }
}

if (-not $builder) {
    Write-Host "Neither dotnet nor Visual Studio MSBuild could be found." -ForegroundColor Red
    exit 3
}

Write-Host ""
Write-Host "[REBIRTH Pass6S] Building with: $builder"
Assert-RebirthGameClosed
& $builder @builderArgs
$code = $LASTEXITCODE

if ($code -ne 0) {
    Write-Host ""
    Write-Host "[REBIRTH Pass6S] BUILD FAILED ($code)" -ForegroundColor Red
    exit $code
}

Write-Host ""
Write-Host "[REBIRTH Pass6S] BUILD PASSED" -ForegroundColor Green
exit 0
