param([string]$SourceDirectory=(Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/Energy'))
$ErrorActionPreference='Stop'
function Extract([string]$source,[string]$signature){
 $start=$source.IndexOf($signature);if($start-lt0){throw "missing method $signature"}
 $brace=$source.IndexOf('{',$start);$arrow=$source.IndexOf('=>',$start)
 if($arrow-ge0-and$arrow-lt$brace){$semi=$source.IndexOf(';',$arrow);return $source.Substring($start,$semi-$start+1)}
 $depth=0;for($i=$brace;$i-lt$source.Length;$i++){if($source[$i]-eq'{'){$depth++};if($source[$i]-eq'}'){$depth--;if($depth-eq0){return $source.Substring($start,$i-$start+1)}}};throw 'unterminated actual method'
}
$w=Get-Content "$SourceDirectory/RebirthCamperWorldLighting.cs" -Raw
$p=Get-Content "$SourceDirectory/RebirthCamperEnergyLightPlacement.cs" -Raw
$methods=@('internal static int DiscoveryGeneration','private static string Key','private static bool Trader','private static byte[] Image','private static bool Inside','private static void MaterializePlacedLightTiles','internal static void RecordPlacedPoiLight','internal static void AdmitPlacedPoiTarget','internal static void AdmitPlacedPoiLights','internal static void Capture')|ForEach-Object {Extract $w $_}
$fixture=Get-Content "$PSScriptRoot/BoundariesAndChecks.cs.txt" -Raw
$fixture=$fixture.Replace('/*ACTUAL_METHODS*/',($methods-join[Environment]::NewLine)).Replace('/*ACTUAL_POSTFIX*/',(Extract $p 'private static void Postfix')).Replace('/*ACTUAL_TRACE*/',(Extract $p 'private static void Trace'))
Add-Type -TypeDefinition $fixture -CompilerOptions '/nowarn:0649'
[LifecycleChecks]::Run()
