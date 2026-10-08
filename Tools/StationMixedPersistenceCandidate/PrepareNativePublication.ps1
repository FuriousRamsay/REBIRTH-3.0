$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$s=[IO.File]::ReadAllText("$root/Scripts/Survivor/Persistence/RebirthStationCompletionPublication.cs")
$start=$s.IndexOf('internal sealed class RebirthStationCompletionPublication')
$s=$s.Substring($start)
$s="using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;`r`n"+$s
$s=$s.Replace('RebirthStationCompletionPublication','MixedNativePublicationCandidate').Replace('RebirthStationCompletionExpectation expectation','TypedMixedWatchExpectation expectation')
$s=$s.Replace('expectation.IsBound(admission,intent,queuedPublication)','Bound(expectation,admission,intent,queuedPublication)')
$s=$s.Replace('!expectation.MatchesStation(bytes[0],bytes[1])||!expectation.MatchesTerminal(bytes[2],bytes[3])','!expectation.NativeProjection.MatchesCurrentAndNative(bytes)||!Bound(expectation,admission,intent,queuedPublication)')
$s=$s.Replace('return expectation.MatchesStation(bytes[0],bytes[1])&&expectation.MatchesTerminal(bytes[2],bytes[3]);','return expectation.NativeProjection.MatchesCurrentAndNative(bytes)&&Bound(expectation,admission,intent,queuedPublication);')
$s=$s.Replace('StringComparison.OrdinalIgnoreCase','RebirthStationCompletionOriginalScope.PathComparison')
$bound=@'
    private static bool Bound(TypedMixedWatchExpectation expected,RebirthStationGridAdmission admission,RebirthStationTerminalIntent intent,RebirthStationPublicationRecord queued)
    {
        try {
            var original=expected?.OriginalEvent?.Original;
            return original!=null&&expected.NativeExpectation!=null&&expected.NativeProjection!=null&&ValidOriginals(admission,intent,queued)&&
                XNode.DeepEquals(admission.Write(),original.Admission.Write())&&XNode.DeepEquals(queued.Write(),original.Queued.Write())&&
                original.Progression.StationTerminalIntents.TryGetValue(admission.JobId,out var retained)&&ReferenceEquals(retained,intent)&&
                (int)intent.Write().Attribute("actor")==original.Actor&&expected.MatchesLive()&&original.IsCurrent();
        }catch{return false;}
    }
'@
$s=$s.Replace('    private readonly XElement image;', $bound+"`r`n    private readonly XElement image;")
# Require exact native original save root, not caller-provided neighboring save.
$s=$s.Replace('if(proof==null||expectation==null||!Bound(expectation,admission,intent,queuedPublication))return false;','if(proof==null||expectation==null||!Bound(expectation,admission,intent,queuedPublication)||!string.Equals(Path.GetFullPath(saveRoot),expectation.OriginalEvent.Original.SaveRoot,RebirthStationCompletionOriginalScope.PathComparison))return false;')
[IO.File]::WriteAllText("$PSScriptRoot/MixedNativePublicationCandidate.cs",$s)
Write-Output 'Mixed publication factory generated from actual strict originals/native region reader; no ordinary completion changes.'
