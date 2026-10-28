param([string]$Python = (Join-Path $env:USERPROFILE 'AppData/Local/Programs/Python/Python314/python.exe'))
$ErrorActionPreference='Stop'
$fixture=Join-Path $PSScriptRoot 'Fixture/Fixture.csproj'
foreach($mode in @('added','model')){
 $negative=Join-Path $PSScriptRoot "negative-$mode.cs.txt"
 dotnet run --project $fixture -c Release -p:UseSharedCompilation=false "-p:CompatibilitySource=$negative"
 if($LASTEXITCODE -ne 1){throw "Expected $mode negative exit1"}
}
dotnet run --project $fixture -c Release -p:UseSharedCompilation=false
if($LASTEXITCODE -ne 0){throw 'Positive lifecycle fixture failed'}
$native=Join-Path $PSScriptRoot 'Abi/Abi.csproj'
dotnet run --project $native -c Release -p:UseSharedCompilation=false -- --negative-onload
if($LASTEXITCODE -ne 1){throw 'Expected native null-module negative exit1'}
dotnet run --project $native -c Release -p:UseSharedCompilation=false --no-build
if($LASTEXITCODE -ne 0){throw 'Native roundtrip fixture failed'}
& $Python (Join-Path $PSScriptRoot 'verify_xml.py') --negative
if($LASTEXITCODE -ne 1){throw 'Expected removed-schema XML negative exit1'}
& $Python (Join-Path $PSScriptRoot 'verify_xml.py')
if($LASTEXITCODE -ne 0){throw 'XML inheritance validation failed'}
& (Join-Path $PSScriptRoot 'verify_guard.ps1')
'PASS offline regression suite; no game, real save or production source modification'
