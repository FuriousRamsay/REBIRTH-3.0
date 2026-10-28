param([switch]$BeforeFix)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source=Get-Content (Join-Path $root 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyDeviceAdapter.cs') -Raw
$runtime=Get-Content (Join-Path $root 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyRuntime.cs') -Raw
$coordinator=Get-Content (Join-Path $root 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergySourceCoordinator.cs') -Raw
function Member([string]$text,[string]$marker){
 $start=$text.IndexOf($marker);if($start-lt0){throw "Missing $marker"}
 $brace=$text.IndexOf('{',$start);$arrow=$text.IndexOf('=>',$start)
 if($arrow-ge0-and$arrow-lt$brace){return $text.Substring($start,$text.IndexOf(';',$arrow)-$start+1)}
 $depth=0
 for($i=$brace;$i-lt$text.Length;$i++){if($text[$i]-eq'{'){$depth++}elseif($text[$i]-eq'}'){$depth--;if(!$depth){return $text.Substring($start,$i-$start+1)}}}
 throw "Unclosed $marker"
}
$markers=@('private static bool MatchesPlain(','private static bool MatchesExpected(','private static void RestorePlain(','private static bool KeepPoiOff(','private static bool CanReclaimRestoredPoi(','private static void Restore(','public static void Suspend(','public static void Release(','internal static void ClearAcceptedBankPoiRequest(','private sealed class Consumer :','private sealed class PlainConsumer :')
$members=($markers|ForEach-Object {Member $source $_}) -join [Environment]::NewLine
$access=Get-Content (Join-Path $root 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyAccess.cs') -Raw
$branchStart=$access.IndexOf('if(accepted&&command==RebirthCamperEnergyCommand.Enabled&&!on)')
$branch=$access.Substring($branchStart,$access.IndexOf(';',$branchStart)-$branchStart+1)
$prefixStart=$source.IndexOf('var s=For(vehicle);if(!Current(s))return Array.Empty<IRebirthCamperEnergyConsumer>();')
$prefixEnd=$source.IndexOf('var energy=',$prefixStart)
$prefix=$source.Substring($prefixStart,$prefixEnd-$prefixStart)
$decision=[regex]::Match($coordinator,'bool suspendConsumers=[^;]+;').Value
if(!$decision){throw 'Bank-OFF hold decision missing'}
if($BeforeFix){$decision='bool suspendConsumers=true;'}
$fixture=Get-Content (Join-Path $root 'Tools/BankPoiHoldRegression/BoundaryAndChecks.cs.txt') -Raw
$extra=Get-Content (Join-Path $PSScriptRoot 'Checks.cs.txt') -Raw
$gate=Member $runtime 'if(RebirthCamperStationFuelOriginal.BlocksVehicle(vehicle)||CustodyReady?.Invoke(vehicle)!=true)'
$extra=$extra.Replace('/*HOLD*/',(Member $runtime 'public bool Hold(')).Replace('/*RELEASE*/',(Member $runtime 'public void Release(')).Replace('/*DECISION*/',$decision).Replace('/*PREFIX*/',$prefix).Replace('/*GATE*/',$gate)
$fixture=$fixture.Replace('/*ACTUAL_MEMBERS*/',$members).Replace('/*ACTUAL_ACCESS_BRANCH*/',$branch)+[Environment]::NewLine+$extra
Add-Type -TypeDefinition $fixture -CompilerOptions '/nowarn:0649,0414'
[RebirthCamperEnergyDeviceAdapter]::RunApply()


