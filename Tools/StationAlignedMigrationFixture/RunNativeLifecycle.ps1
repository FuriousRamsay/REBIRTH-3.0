$ErrorActionPreference='Stop'
# A process snapshot is a safety gate, not a substitute for the parent CPU lease.
Get-Content -LiteralPath 'E:\_Haven\shared\coordination\HAV3N_to_codex.txt'
$activeCpuJobs=@(Get-CimInstance Win32_Process | Where-Object {
 $_.Name -match '^(dotnet|MSBuild|csc|ilspycmd|inspect)\.exe$' -and
 $_.CommandLine -notmatch 'VBCSCompiler\.dll'
})
if($activeCpuJobs.Count -ne 0){$activeCpuJobs | Select-Object ProcessId,Name,CommandLine | Format-List;throw 'CPU gate refused: active compiler/build/fixture/decompiler; no compiler started.'}
& (Join-Path $PSScriptRoot 'PrepareNativeLifecycle.ps1')
if(!$?){throw 'Preparation failed; no compiler started.'}
& dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') --no-restore
if($LASTEXITCODE -ne 0){throw "Fixture failed with exit $LASTEXITCODE"}