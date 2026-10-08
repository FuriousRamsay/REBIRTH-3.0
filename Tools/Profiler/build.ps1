# Builds the profiler: the native sampling module (needs Visual Studio C++ tools) and the viewer app.
# Output: native\mono-profiler-rebirthprof.dll and bin\RebirthProfiler.exe
param([switch]$AppOnly)   # -AppOnly: skip the native module (it is locked while the game runs)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio with the C++ x64 tools was not found.' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'

if (-not $AppOnly) {
Push-Location (Join-Path $root 'native')
try {
    cmd /c "`"$vcvars`" >nul 2>&1 && cl /nologo /O2 /LD /EHa mono-profiler.cpp /Fe:mono-profiler-rebirthprof.dll"
    if ($LASTEXITCODE -ne 0) { throw 'native module build failed' }
    Remove-Item *.obj, *.exp, *.lib -ErrorAction SilentlyContinue
} finally { Pop-Location }
}

dotnet build (Join-Path $root 'App\RebirthProfiler.csproj') -c Release -o (Join-Path $root 'bin')
if ($LASTEXITCODE -ne 0) { throw 'app build failed' }
Write-Host "Built. Run: $root\bin\RebirthProfiler.exe"
