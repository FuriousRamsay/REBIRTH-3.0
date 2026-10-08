$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$repo=[IO.File]::ReadAllText("$root/Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs")
$model=[IO.File]::ReadAllText("$root/Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs")
function Method($s,$signature){$start=$s.IndexOf($signature);if($start-lt0){throw "missing $signature"};$open=$s.IndexOf('{',$start);$depth=0;for($i=$open;$i-lt$s.Length;$i++){if($s[$i]-eq'{'){$depth++};if($s[$i]-eq'}'){$depth--;if($depth-eq0){return $s.Substring($start,$i-$start+1)}}};throw 'unbalanced'}
$field=($model -split '\r?\n' | Where-Object {$_ -match 'internal readonly Dictionary<string,RebirthStationCompletionPublication> StationCompletionPublications'});if(@($field).Count-ne1){throw 'model field'}
$clone=($model -split '\r?\n' | Where-Object {$_ -match '^\s*foreach\(var pair in StationCompletionPublications\)'});if(@($clone).Count-ne1){throw 'clone line'}
$write=($repo -split '\r?\n' | Where-Object {$_ -match 'node.Add\(RebirthStationCompletionPublication.WriteAll'});if(@($write).Count-ne1){throw 'save hook'}
$start=$repo.IndexOf('        if(!RebirthStationPublicationRecord.ReadAll(node,state.StationPreparations,')
$end=$repo.IndexOf('        if(!RebirthStationCancellationRefund.ReadAll(', $start)
if($start-lt0-or$end-le$start){throw 'read hooks'};$read=$repo.Substring($start,$end-$start)
$witness=Method $repo '    internal static bool HasSavedStationCompletionPublication('
$loader=Method $repo '    private static bool TryLoadValidatedRecord('
$outer=Method $repo '    private static bool TryDeserialize(XDocument doc,'
$slices=@"
using System;using System.Collections.Generic;using System.Linq;using System.Xml.Linq;using System.Globalization;
public partial class RebirthWorldProgressionState {
$field
internal RebirthWorldProgressionState Clone(){var copy=new RebirthWorldProgressionState();foreach(var p in StationPreparations)copy.StationPreparations.Add(p.Key,p.Value.Clone());foreach(var p in StationPublications)copy.StationPublications.Add(p.Key,p.Value.Clone());foreach(var p in StationTerminalIntents)copy.StationTerminalIntents.Add(p.Key,p.Value.Clone());
$clone
return copy;}
}
public static partial class ExtractedRepository {
$witness
$loader
$outer
internal static XElement SaveCompletion(RebirthWorldProgressionState state){var node=new XElement("progression");node.Add(RebirthStationPublicationRecord.WriteAll(state.StationPublications,state.StationPreparations));node.Add(RebirthStationTerminalIntent.WriteAll(state.StationTerminalIntents,state.StationPreparations));
$write
return node;}
private static bool TryDeserializeProgression(XElement node,string ownerKey,out RebirthWorldProgressionState state,out string error){state=new RebirthWorldProgressionState();error="";if(node==null)return false;foreach(var p in Preparations)state.StationPreparations.Add(p.Key,p.Value.Clone());
$read
return true;}
}
"@
[IO.File]::WriteAllText("$PSScriptRoot/ActualSharedSlices.cs",$slices)
dotnet run --project "$PSScriptRoot/Fixture.csproj" -c Release
if($LASTEXITCODE-ne0){throw 'completion integration fixture failed'}

