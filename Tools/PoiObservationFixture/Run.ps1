param([string]$SourceDirectory=(Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/Energy'))
$ErrorActionPreference='Stop'
function Extract([string]$s,[string]$sig){$a=$s.IndexOf($sig);if($a-lt0){throw "missing $sig"};$b=$s.IndexOf('{',$a);$arrow=$s.IndexOf('=>',$a);if($arrow-ge0-and$arrow-lt$b){$end=$s.IndexOf(';',$arrow);return $s.Substring($a,$end-$a+1)};$n=0;for($i=$b;$i-lt$s.Length;$i++){if($s[$i]-eq'{'){$n++};if($s[$i]-eq'}'){$n--;if($n-eq0){return $s.Substring($a,$i-$a+1)}}};throw 'method boundary'}
$a=Get-Content "$SourceDirectory/RebirthCamperEnergyDeviceAdapter.cs" -Raw
$c=Get-Content "$SourceDirectory/RebirthCamperPoiLightChoices.cs" -Raw
$p=Get-Content "$SourceDirectory/RebirthCamperEnergyDeviceAdapter.PoiChoices.cs" -Raw
$observe=Extract $a 'private static RebirthCamperEnergyPoiSnapshot ObservePoi'
$consumers=Extract $a 'public static IEnumerable<IRebirthCamperEnergyConsumer> Consumers'
if($observe.Contains('.Take(')-or$consumers.Contains('.Take(')){throw 'Divergent Take selection returned'}
if(-not$observe.Contains('SelectPoiLights(')-or-not$consumers.Contains('SelectPoiLights(')){throw 'Consumers/Observe must share selection policy'}
$f=Get-Content "$PSScriptRoot/BoundariesAndChecks.cs.txt" -Raw
$f=$f.Replace('/*ACTUAL_OBSERVE*/',$observe).Replace('/*ACTUAL_SELECTION*/',(Extract $p 'private static RebirthCamperPoiLightChoices.Plan SelectPoiLights')).Replace('/*ACTUAL_POOL_CHECK*/',(Extract $p 'private static bool CurrentPoiPoolPosition')).Replace('/*ACTUAL_PLAN*/',(Extract $c 'internal sealed class Plan')).Replace('/*ACTUAL_SELECT*/',(Extract $c 'internal static Plan Select'))
Add-Type -TypeDefinition $f -CompilerOptions '/nowarn:0649'
[RebirthCamperEnergyDeviceAdapter]::Run()
