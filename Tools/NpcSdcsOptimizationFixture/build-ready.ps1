# Compile/install only. This script never launches a game or executes tests.
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$doc = Join-Path $root '_Documentation/NPC SDCS Optimization/20261020'
if (Get-Process 7DaysToDie -ErrorAction SilentlyContinue) { throw 'Close the game before building/installing the DLL.' }
$project = Join-Path $root 'RebirthUtils.csproj'
function Get-CompiledSources {
    $evaluation = (& dotnet msbuild $project -nologo -getItem:Compile) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw 'Could not evaluate actual compiled sources.' }
    ($evaluation | ConvertFrom-Json).Items.Compile | ForEach-Object { $_.FullPath } |
        Where-Object { $_.StartsWith((Join-Path $root 'Scripts') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) } | Sort-Object -Unique
}
$sources = @(Get-CompiledSources)
if ($sources.Count -eq 0) { throw 'No production source files found.' }
function Snapshot-Sources {
    foreach ($source in @($sources) + @($project)) {
        [pscustomobject]@{ path=$source.Substring($root.Length+1).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $source).Hash }
    }
}
$before = @(Snapshot-Sources)
$backup = Join-Path $root 'Backups/NPC_SDCS_Ready_20261020'
New-Item -ItemType Directory -Force -Path $backup | Out-Null
foreach ($name in @('RebirthUtils.dll','RebirthUtils.pdb')) {
    $existing = Join-Path $root $name
    if ((Test-Path -LiteralPath $existing) -and -not (Test-Path -LiteralPath (Join-Path $backup $name))) {
        Copy-Item -LiteralPath $existing -Destination (Join-Path $backup $name)
    }
}
$targeting = Join-Path $env:USERPROFILE '.nuget/packages/microsoft.netframework.referenceassemblies.net481/1.0.3/build'
if (-not (Test-Path -LiteralPath $targeting)) { throw 'Cached .NET 4.8.1 targeting pack is unavailable.' }
$log = Join-Path $doc 'native_ready_build.log'
Push-Location $root
try {
    & dotnet build $project -c Debug --no-restore -nologo -v quiet "-p:TargetFrameworkRootPath=$targeting" '-p:IntermediateOutputPath=obj/NpcSdcsReady/RootDebug/' *> $log
    $buildExit = $LASTEXITCODE
} finally { Pop-Location }
if ($buildExit -ne 0) { Get-Content -LiteralPath $log -Tail 22; throw 'Native mod build failed; output is not ready.' }
$after = @(Snapshot-Sources)
$currentSources = @(Get-CompiledSources)
if (($before | ConvertTo-Json -Compress) -ne ($after | ConvertTo-Json -Compress) -or
    ($sources -join "`n") -ne ($currentSources -join "`n")) {
    throw 'Shared production sources changed during the build; coordinate and rebuild before packaging.'
}
$after | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $doc 'compiled_sources.json')
$managed = [IO.Path]::GetFullPath((Join-Path $root '../../7DaysToDie_Data/Managed/Assembly-CSharp.dll'))
$dll = Join-Path $root 'RebirthUtils.dll'
$record = [pscustomobject]@{
    configuration='Debug'; installedDll=$dll; dllSha256=(Get-FileHash -LiteralPath $dll).Hash;
    dllBytes=(Get-Item -LiteralPath $dll).Length; compiledProductionFiles=$sources.Count;
    sourceChangedDuringBuild=$false; nativeAssembly=$managed; nativeAssemblySha256=(Get-FileHash -LiteralPath $managed).Hash;
    previousDllBackup=(Join-Path $backup 'RebirthUtils.dll'); gameLaunched=$false; gameplayTestsRun=$false
}
$record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $doc 'installed_build.json')
$record | ConvertTo-Json
