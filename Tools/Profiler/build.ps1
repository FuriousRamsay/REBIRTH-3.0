# Builds the profiler: the native sampling module (needs Visual Studio C++ tools) and the viewer app.
# Output: native\mono-profiler-rebirthprof.dll and bin\RebirthProfiler.exe
param([switch]$AppOnly,[switch]$NativeOnly)   # NativeOnly retains the open viewer instance.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if($AppOnly -and $NativeOnly){throw 'Choose either AppOnly or NativeOnly.'}

if (-not $AppOnly) {
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vs) { throw 'Visual Studio with the C++ x64 tools was not found.' }
$vcvars = Join-Path $vs 'VC\Auxiliary\Build\vcvars64.bat'
# Some VS installations omit the installed UCRT from vcvars' INCLUDE/LIB.
# Locate a complete SDK pair explicitly so a clean profiler rebuild still works.
$kits=Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10'
$ucrt=Get-ChildItem (Join-Path $kits 'Include') -Directory |
    Where-Object {(Test-Path (Join-Path $_.FullName 'ucrt/ctype.h')) -and
        (Test-Path (Join-Path $kits ('Lib/'+$_.Name+'/ucrt/x64/ucrt.lib')))} |
    Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1
if(!$ucrt){throw 'A complete Windows SDK UCRT header/library pair was not found.'}
$ucrtInclude=Join-Path $ucrt.FullName 'ucrt'
$ucrtLib=Join-Path $kits ('Lib/'+$ucrt.Name+'/ucrt/x64')

Push-Location (Join-Path $root 'native')
try {
    cmd /c "`"$vcvars`" >nul 2>&1 && cl /nologo /O2 /LD /EHa /I`"$ucrtInclude`" mono-profiler.cpp /Fe:mono-profiler-rebirthprof.dll /link /LIBPATH:`"$ucrtLib`""
    if ($LASTEXITCODE -ne 0) { throw 'native module build failed' }
    Remove-Item *.obj, *.exp, *.lib -ErrorAction SilentlyContinue
} finally { Pop-Location }
}

if($NativeOnly){Write-Host 'Built native profiler; existing viewer retained.';return}
dotnet build (Join-Path $root 'App\RebirthProfiler.csproj') -c Release -o (Join-Path $root 'bin')
if ($LASTEXITCODE -ne 0) { throw 'app build failed' }
Write-Host "Built. Run: $root\bin\RebirthProfiler.exe"
