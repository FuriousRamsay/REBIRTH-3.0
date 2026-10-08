#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$production=[IO.File]::ReadAllText((Join-Path $taskRoot 'Scripts/Crafting/UI/RebirthStationPreparationReservation.cs'))
$fixture=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'test_station_reservation_generated_fixture.cs'))
$boundary=$fixture.IndexOf('public struct Vector3i ')
if($boundary-lt0){throw 'Explicit reservation fixture boundary missing'}
Add-Type -TypeDefinition ($production+$fixture.Substring($boundary))
[StationClaimFixture]::Run()