#requires -Version 7.0
$ErrorActionPreference='Stop'
if(Get-Process 7DaysToDie* -ErrorAction SilentlyContinue){throw 'Game must be closed'}
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=[IO.File]::ReadAllText((Join-Path $root 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyDeviceAdapter.Circuits.cs'))
$fixture=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'native_circuits_fixture.cs'))
# Compile as separate source units so both preserve their original using directives.
$temp=Join-Path ([IO.Path]::GetTempPath()) ('rebirth-circuit-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null
[IO.File]::WriteAllText((Join-Path $temp 'actual.cs'),$source)
[IO.File]::WriteAllText((Join-Path $temp 'fixture.cs'),$fixture)
Add-Type -Path (Join-Path $temp 'actual.cs'),(Join-Path $temp 'fixture.cs') -IgnoreWarnings -WarningAction SilentlyContinue
[RebirthCamperEnergyDeviceAdapter]::Run()
