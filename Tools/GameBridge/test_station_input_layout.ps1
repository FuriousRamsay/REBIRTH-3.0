#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$source=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Crafting/UI/RebirthStationInputLayout.cs'))
$doubles=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'test_station_input_layout_fixture.cs'))
Add-Type -TypeDefinition ($source+[Environment]::NewLine+$doubles)
[LayoutFixture]::Run()