param([string]$ExpectedSourceHash)
$ErrorActionPreference='Stop'
Get-Content -LiteralPath 'E:\_Haven\shared\coordination\HAV3N_to_codex.txt'
$activeJobs=@(Get-CimInstance Win32_Process | Where-Object {$_.Name -match '^(dotnet|MSBuild|csc|ilspycmd|inspect)\.exe$' -and $_.CommandLine -notmatch 'VBCSCompiler\.dll'})
if($activeJobs.Count -ne 0){$activeJobs | Select-Object ProcessId,Name,CommandLine | Format-List;throw 'CPU gate refused; no compiler started.'}
$sourcePath=Join-Path $PSScriptRoot '..\AdvancedFarmingPlantOriginService.cs'
$beforeHash=(Get-FileHash -LiteralPath $sourcePath).Hash
if($ExpectedSourceHash -and $ExpectedSourceHash -ne $beforeHash){throw 'Source snapshot mismatch; no compiler started.'}
Write-Output "Linked source SHA256 $beforeHash"
& dotnet run --project (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release
if($LASTEXITCODE -ne 0){throw "Fixture failed with exit $LASTEXITCODE"}
if((Get-FileHash -LiteralPath $sourcePath).Hash -ne $beforeHash){throw 'Source changed during run; result not snapshot qualified.'}