param([string]$SourceDirectory=(Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/Energy'))
$ErrorActionPreference='Stop'
function Extract([string]$s,[string]$sig){$a=$s.IndexOf($sig);if($a-lt0){throw "missing $sig"};$b=$s.IndexOf('{',$a);$n=0;for($i=$b;$i-lt$s.Length;$i++){if($s[$i]-eq'{'){$n++};if($s[$i]-eq'}'){$n--;if($n-eq0){return $s.Substring($a,$i-$a+1)}}};throw 'method boundary'}
$a=Get-Content "$SourceDirectory/RebirthCamperEnergyDeviceAdapter.PoiChoices.cs" -Raw
$c=Get-Content "$SourceDirectory/RebirthCamperPoiLightChoices.cs" -Raw
$actual=Extract $a 'internal static bool SetManualPoiLight'
$interaction=Extract $a 'internal static bool TryPoiLightInteraction'
if($interaction.IndexOf('!Eligible(node)') -gt $interaction.IndexOf('AdmitPlacedPoiTarget') -or
   $interaction.IndexOf('if(!PoiPoolLight(node))') -lt $interaction.IndexOf('AdmitPlacedPoiTarget')){throw 'Target admission must follow live electrical eligibility and precede pool stamp requirement'}
if($actual-match 'IsToggled\s*=|SetBlock|TryPrepareContents|\.isOn\s*='){throw 'manual service unexpectedly writes raw light/source state'}
$fixture=Get-Content "$PSScriptRoot/BoundariesAndChecks.cs.txt" -Raw
$fixture=$fixture.Replace('/*ACTUAL_SET*/',$actual).Replace('/*ACTUAL_PLAN*/',(Extract $c 'internal sealed class Plan')).Replace('/*ACTUAL_SELECT*/',(Extract $c 'internal static Plan Select'))
Add-Type -TypeDefinition $fixture -CompilerOptions '/nowarn:0649'
[RebirthCamperEnergyDeviceAdapter]::RunChecks()
