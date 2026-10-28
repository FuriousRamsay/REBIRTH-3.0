param([switch]$OriginalRestore)
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$source=Get-Content (Join-Path $root 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyDeviceAdapter.cs') -Raw
function Member([string]$text,[string]$marker){
 $start=$text.IndexOf($marker);if($start-lt0){throw "Missing source member $marker"}
 $brace=$text.IndexOf('{',$start);$arrow=$text.IndexOf('=>',$start)
 if($arrow-ge0-and$arrow-lt$brace){$end=$text.IndexOf(';',$arrow);return $text.Substring($start,$end-$start+1)}
 $depth=0
 for($i=$brace;$i-lt$text.Length;$i++){if($text[$i]-eq'{'){$depth++}elseif($text[$i]-eq'}'){$depth--;if($depth-eq0){return $text.Substring($start,$i-$start+1)}}}
 throw "Unclosed source member $marker"
}
$markers=@('private static bool MatchesPlain(','private static bool MatchesExpected(','private static void RestorePlain(','private static bool KeepPoiOff(','private static void Restore(','public static void Suspend(','public static void Release(','internal static void ClearAcceptedBankPoiRequest(')
$original=if($OriginalRestore){Get-Content (Join-Path $PSScriptRoot 'OriginalRestoreMembers.cs.txt') -Raw}else{''}
$members=($markers | ForEach-Object {
 if($OriginalRestore-and$_-in@('private static void RestorePlain(','private static void Restore(','public static void Suspend(')){
  # The ignored optional argument lets the same diagnostic test call original
  # method bodies; their restoration behavior is not changed.
  (Member $original $_).Replace('RestorePlain(Session s,TileEntityLight light)','RestorePlain(Session s,TileEntityLight light,bool retainPoiOff=false)').Replace('Restore(Session s,PowerItem node)','Restore(Session s,PowerItem node,bool retainPoiOff=false)')
 }else{Member $source $_}
}) -join [Environment]::NewLine
$access=Get-Content (Join-Path $root 'Scripts/Vehicles/Camper/Energy/RebirthCamperEnergyAccess.cs') -Raw
$branch=[regex]::Match($access,'if\(accepted&&command==RebirthCamperEnergyCommand.Enabled&&!on\)\s*RebirthCamperEnergyDeviceAdapter\.\w+\([^;]+;').Value
if(!$branch){throw 'Accepted bank OFF branch missing'}
$fixture=Get-Content (Join-Path $PSScriptRoot 'BoundaryAndChecks.cs.txt') -Raw
$fixture=$fixture.Replace('/*ACTUAL_MEMBERS*/',$members).Replace('/*ACTUAL_ACCESS_BRANCH*/',$branch)
Add-Type -TypeDefinition $fixture -CompilerOptions '/nowarn:0649'
[RebirthCamperEnergyDeviceAdapter]::Run()
