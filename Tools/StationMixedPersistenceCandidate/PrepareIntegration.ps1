$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dest=Join-Path $PSScriptRoot 'production_candidates'
New-Item -ItemType Directory $dest -Force | Out-Null
$model=[IO.File]::ReadAllText("$root/Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs")
$repo=[IO.File]::ReadAllText("$root/Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs")
function InsertAfter([string]$text,[string]$anchor,[string]$add){if(($text.Split(@($anchor),[StringSplitOptions]::None).Count)-ne2){throw "anchor not unique $anchor"};return $text.Replace($anchor,$anchor+"`r`n"+$add)}
$field='    internal readonly Dictionary<string,MixedCompletionRecord> StationMixedCompletionRecords = new Dictionary<string,MixedCompletionRecord>(StringComparer.Ordinal);'
$clone='        foreach(var pair in StationMixedCompletionRecords) copy.StationMixedCompletionRecords.Add(pair.Key,pair.Value.Clone());'
$m=InsertAfter $model '    internal readonly Dictionary<string,RebirthStationCompletionExpectationProjection> StationCompletionExpectationProjections = new Dictionary<string,RebirthStationCompletionExpectationProjection>(StringComparer.Ordinal);' $field
$m=InsertAfter $m '        foreach(var pair in StationCompletionExpectationProjections) copy.StationCompletionExpectationProjections.Add(pair.Key,pair.Value.Clone());' $clone
$save=@'
        if(state.StationMixedCompletionRecords.Count!=0)
        {
            if(!MixedCompletionRecord.TryAppendData(node,state.StationMixedCompletionRecords.Values,out var mixedNode))throw new InvalidDataException("Invalid mixed completion DATA section");
            node=mixedNode;
        }
'@
$r=InsertAfter $repo '        if(state.StationCompletionExpectationProjections.Count!=0)node.Add(RebirthStationCompletionExpectationProjection.WriteAllStored(state.StationCompletionExpectationProjections,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications));' $save
$read=@'
        if(!MixedCompletionRecord.TryReadAllData(node,out var mixedCompletionRecords)){error="mixed completion DATA invalid";return false;}
        foreach(var pair in mixedCompletionRecords)state.StationMixedCompletionRecords.Add(pair.Key,pair.Value);
'@
$r=InsertAfter $r '        foreach(var pair in completionProjections)state.StationCompletionExpectationProjections.Add(pair.Key,pair.Value);' $read
[IO.File]::WriteAllText("$dest/RebirthSurvivorRuntimeModels.candidate.cs.txt",$m)
[IO.File]::WriteAllText("$dest/RebirthWorldCharacterRepository.candidate.cs.txt",$r)
[IO.File]::WriteAllText("$dest/model.baseline.cs.txt",$model)
[IO.File]::WriteAllText("$dest/repository.baseline.cs.txt",$repo)
$slices=@"
using System;using System.Linq;using System.IO;using System.Collections.Generic;using System.Xml.Linq;
internal sealed class MixedProgressionCandidate {
$field
internal MixedProgressionCandidate Clone(){var copy=new MixedProgressionCandidate();
$clone
return copy;}}
internal static class ActualMixedRepositorySeams {
internal static XElement SerializeSeam(XElement node,MixedProgressionCandidate state){
$save
return node;}
internal static bool DeserializeSeam(XElement node,out MixedProgressionCandidate state,out string error){state=new MixedProgressionCandidate();error="";
$read
return true;}}
"@
[IO.File]::WriteAllText("$PSScriptRoot/ActualMixedRepositorySeams.cs",$slices)
Write-Output 'Candidate model/save/load insertion anchors matched exactly; extracted narrow seams generated.'
