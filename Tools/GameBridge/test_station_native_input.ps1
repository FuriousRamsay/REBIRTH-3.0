#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Crafting/UI/RebirthStationNativeInputCodec.cs'))
$doubles=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'test_station_native_input_fixture.cs'))
Add-Type -TypeDefinition ($source+[Environment]::NewLine+$doubles)
[NativeInputFixture]::Run()