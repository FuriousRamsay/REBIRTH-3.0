$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$ui=Get-Content (Join-Path $root 'Scripts/Vehicles/Camper/Energy/XUiC_RebirthCamperEnergy.cs') -Raw
$first=$ui.IndexOf('    private int PoiSliderMaximum')
$last=$ui.IndexOf('    private void ToggleMode()', $first)
if($first-lt0-or$last-lt$first){throw 'Actual slider extraction failed'}
$wire=Get-Content (Join-Path $root 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergySnapshotWire.cs') -Raw
$wire=$wire.Substring($wire.IndexOf('internal static class RebirthCamperEnergySnapshotWire'))
$boundaries=Get-Content (Join-Path $PSScriptRoot 'BoundariesAndChecks.cs.txt') -Raw
$actual=$boundaries.Replace('/*ACTUAL_SLIDER*/',$ui.Substring($first,$last-$first)).Replace('/*ACTUAL_WIRE*/',$wire)
Add-Type -TypeDefinition $actual -CompilerOptions '/nowarn:0649'
[PoiManualUiChecks]::Run()
