param([string]$SourcePath=(Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/Energy/RebirthCamperPoiLightChoices.cs'))
$ErrorActionPreference='Stop'
$source=Get-Content -LiteralPath $SourcePath -Raw
$boundaries=Get-Content (Join-Path $PSScriptRoot 'BoundariesAndChecks.cs.txt') -Raw
Add-Type -TypeDefinition ($source+[Environment]::NewLine+$boundaries) -CompilerOptions '/nowarn:0649'
[ChoiceChecks]::Run()
