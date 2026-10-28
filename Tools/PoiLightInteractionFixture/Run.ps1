$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$actual=Get-Content (Join-Path $root 'Scripts/Vehicles/Camper/Energy/RebirthCamperPoiLightInteraction.cs') -Raw
$actual=$actual.Substring($actual.IndexOf('internal static class RebirthCamperPoiLightInteraction'))
$fixture=Get-Content (Join-Path $PSScriptRoot 'BoundariesAndChecks.cs.txt') -Raw
Add-Type -TypeDefinition ($fixture.Replace('/*ACTUAL_INTERACTION_FILE*/',$actual)) -CompilerOptions '/nowarn:0649'
[PoiInteractionChecks]::Run()
