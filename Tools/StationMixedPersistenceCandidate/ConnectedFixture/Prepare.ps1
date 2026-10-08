$ErrorActionPreference='Stop'
$here=$PSScriptRoot;$root=Split-Path (Split-Path (Split-Path $here -Parent) -Parent) -Parent
function RemoveType($s,$marker){$start=$s.IndexOf($marker);if($start-lt0){throw "missing $marker"};$open=$s.IndexOf('{',$start);$d=0;for($i=$open;$i-lt$s.Length;$i++){if($s[$i]-eq'{'){$d++};if($s[$i]-eq'}'){$d--;if($d-eq0){return $s.Remove($start,$i-$start+1)}}};throw 'unbalanced'}
$a=[IO.File]::ReadAllText("$here/AuthorityAdapters.cs")
$a=RemoveType $a 'class RebirthStationPublicationRecord'
$a=RemoveType $a 'class RebirthStationTerminalIntent'
$a=RemoveType $a 'static class RebirthWorldCharacterRepository'
$a=$a.Replace('class Origin{','partial class Origin{').Replace('class RebirthWorldCharacterRecord{','partial class RebirthWorldCharacterRecord{').Replace('class RebirthWorldProgressionState{','partial class RebirthWorldProgressionState{')
$a=$a.Replace('public static void MarkDirty(RebirthWorldCharacterRecord o,string why){}','public static bool ThrowDirty;public static int DirtyCalls;public static Action AfterDirty;public static void MarkDirty(RebirthWorldCharacterRecord o,string why){DirtyCalls++;if(ThrowDirty){ThrowDirty=false;throw new System.IO.IOException("before dirty");}o.Dirty=true;AfterDirty?.Invoke();}')
[IO.File]::WriteAllText("$here/AuthorityAdapters.cs",$a)
$n=[IO.File]::ReadAllText("$here/NativeAdapters.cs").Replace('class RebirthStationGridAdmission{','public class RebirthStationGridAdmission{public bool IsPublicationAttempted=true;')
[IO.File]::WriteAllText("$here/NativeAdapters.cs",$n)
# Extract ACTUAL final loader/whole outer parser; unrelated leaves supplied explicitly.
$s=[IO.File]::ReadAllText("$root/Tools/StationMixedPersistenceCandidate/Typed/ActualOuter.cs").Replace('MixedFinalFileRepository','RebirthWorldCharacterRepository')
[IO.File]::WriteAllText("$here/ActualOuter.cs",$s)
$methods=[IO.File]::ReadAllText("$root/Tools/StationMixedPersistenceCandidate/production_candidates/RepositoryMixedMethods.cs.txt")
[IO.File]::WriteAllText("$here/ActualMixedMethods.cs","using System;using System.IO;using System.Linq;using System.Xml.Linq;`r`nstatic partial class RebirthWorldCharacterRepository {`r`n"+$methods+"`r`n}")
