param([switch]$VerifyOldFails)
$ErrorActionPreference='Stop'
$source=Join-Path $PSScriptRoot '../../Scripts/UI/XUiC_RebirthVersionLabel.cs'
if($VerifyOldFails){$source=Join-Path $PSScriptRoot '../../_Documentation/Purge/FullRegression20261102/batch12-version-before/XUiC_RebirthVersionLabel.cs'}
[IO.File]::WriteAllBytes((Join-Path $PSScriptRoot 'Target.cs'),[IO.File]::ReadAllBytes($source))
& dotnet build (Join-Path $PSScriptRoot 'Fixture.csproj') -c Release --nologo -v quiet --no-incremental
if($LASTEXITCODE){throw 'Actual version-label class did not compile; no test result'}
$output=@(& dotnet (Join-Path $PSScriptRoot 'bin/Release/net9.0/Fixture.dll'))
$result=$LASTEXITCODE
$output
if($VerifyOldFails){if($result-ne 1-or!($output-match'FAIL cached HUD reopen repeated global discovery')){throw 'Old class did not reproduce expected global scan regression'};'EXPECTED: old class repeats global discovery on HUD reopen';exit 0}
exit $result
