$ErrorActionPreference = 'Stop'
# Executes the actual snapshot class with bounded codec/native doubles. This is
# framing/dispatch coverage, not proof of native ItemValue wire conformance.
$source = Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperInventorySnapshot.cs')
$fixture = Get-Content -Raw (Join-Path $PSScriptRoot 'test_camper_inventory_frame_fixture.cs')
Add-Type -TypeDefinition ($source + $fixture)
[CamperInventoryFrameChecks]::Run()
