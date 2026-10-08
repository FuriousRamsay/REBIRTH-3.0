$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
# This historical generator targets the reviewed pre-integration baseline only.
# Refuse before touching candidate evidence if production has advanced.
$expectedBaseline=@{
'Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs'='A3079CBB4099F839747B7AB7FB5F495DF0A3FC65C9044600F1DA26B8488A067D'
'Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs'='7382F091774E36DD759E3F3FA2AF05FFE02226EFC3434B0FB363D073B7279E8F'
'Scripts/Survivor/Persistence/RebirthStationCompletionObservation.cs'='D396F1BFB1303807BC986AC00631B9B8D00D158B815130954DE2D0EBD51F51DA'
'Scripts/Survivor/Persistence/RebirthStationCompletionExpectationProjection.cs'='53C3530A29243B83CC5AC0289959580377086EECB2C13E5CFCD4EBE4B6DF537E'
}
foreach($entry in $expectedBaseline.GetEnumerator()) {
 if((Get-FileHash -LiteralPath (Join-Path $root $entry.Key)).Hash -ne $entry.Value) {
  throw ('Historical candidate generator baseline changed; preserve applied integration and candidate evidence: '+$entry.Key)
 }
}
function SaveCandidate($relative,$text,$name){$original=Join-Path $root $relative;$dest=Join-Path $PSScriptRoot ($name+'.candidate.txt');[IO.File]::WriteAllText($dest,$text);$diff=& git diff --no-index -- $original $dest;[IO.File]::WriteAllText((Join-Path $PSScriptRoot ($name+'.unapplied.diff')),($diff-join "`n"));Write-Output ($name+' ORIGINAL '+(Get-FileHash $original).Hash+' CANDIDATE '+(Get-FileHash $dest).Hash)}
function ExtractMethod($text,$sig){$start=$text.IndexOf($sig);if($start-lt 0){throw 'missingmethod'};$open=$text.IndexOf('{',$start);$d=1;$end=$open+1;while($d){if($text[$end]-eq '{'){$d++};if($text[$end]-eq '}'){$d--};$end++};return $text.Substring($start,$end-$start)}
$relative='Scripts/Survivor/Domain/RebirthSurvivorRuntimeModels.cs';$text=[IO.File]::ReadAllText((Join-Path $root $relative)).Replace("`r`n","`n")
$field='    internal readonly Dictionary<string,RebirthStationCompletionPublication> StationCompletionPublications = new Dictionary<string,RebirthStationCompletionPublication>(StringComparer.Ordinal);'
$text=$text.Replace($field,$field+"`n"+'    internal readonly Dictionary<string,RebirthStationCompletionExpectationProjection> StationCompletionExpectationProjections = new Dictionary<string,RebirthStationCompletionExpectationProjection>(StringComparer.Ordinal);')
$clone='        foreach(var pair in StationCompletionPublications) copy.StationCompletionPublications.Add(pair.Key,pair.Value.Clone());'
$text=$text.Replace($clone,$clone+"`n"+'        foreach(var pair in StationCompletionExpectationProjections) copy.StationCompletionExpectationProjections.Add(pair.Key,pair.Value.Clone());')
SaveCandidate $relative $text 'RuntimeModels'
$relative='Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs';$text=[IO.File]::ReadAllText((Join-Path $root $relative)).Replace("`r`n","`n")
$write='        node.Add(RebirthStationCompletionPublication.WriteAll(state.StationCompletionPublications,state.StationPreparations,state.StationTerminalIntents,state.StationPublications));'
$text=$text.Replace($write,$write+"`n"+'        if(state.StationCompletionExpectationProjections.Count!=0)node.Add(RebirthStationCompletionExpectationProjection.WriteAll(state.StationCompletionExpectationProjections,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications,XUiM_Recipes.GetRecipes()));')
$read='        foreach(var pair in completedPublications)state.StationCompletionPublications.Add(pair.Key,pair.Value);'
$text=$text.Replace($read,$read+"`n"+'        if(!RebirthStationCompletionExpectationProjection.ReadAll(node,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications,node.Elements("stationCompletionExpectationProjections").Any()?XUiM_Recipes.GetRecipes():null,out var completionProjections)){error="Invalid station completion expectation projections";return false;}' +"`n"+'        foreach(var pair in completionProjections)state.StationCompletionExpectationProjections.Add(pair.Key,pair.Value);')
$original=ExtractMethod $text '    internal static bool HasSavedStationCompletionPublication('
$paired=$original.Replace('HasSavedStationCompletionPublication(','HasSavedStationCompletionExpectationProjection(').Replace('RebirthStationPublicationRecord queued,RebirthStationCompletionPublication completed)','RebirthStationPublicationRecord queued,RebirthStationCompletionPublication completed,RebirthStationCompletionExpectationProjection projection)').Replace('queued==null||completed==null||','queued==null||completed==null||projection==null||').Replace('!RebirthStationCompletionPublication.TryRead(completed.Write(),admission,intent,queued,out _))','!RebirthStationCompletionPublication.TryRead(completed.Write(),admission,intent,queued,out _)||'+"`n"+'            !RebirthStationCompletionExpectationProjection.TryRead(projection.Write(),admission,intent,queued,completed,XUiM_Recipes.GetRecipes(),out _))').Replace('                    !serverAuthority||path!=GetPath(identity.StorageKey))','                    !saved.Progression.StationCompletionExpectationProjections.TryGetValue(admission.JobId,out var p)||p==null||'+"`n"+'                    !serverAuthority||path!=GetPath(identity.StorageKey))').Replace('XNode.DeepEquals(c.Write(),completed.Write());','XNode.DeepEquals(c.Write(),completed.Write())&&XNode.DeepEquals(p.Write(),projection.Write())&&'+"`n"+'                    serverAuthority&&path==GetPath(identity.StorageKey);')
$text=$text.Replace($original,$original+"`n"+$paired)
SaveCandidate $relative $text 'Repository'
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'PairedWitness.method.txt'),$paired)
$relative='Scripts/Survivor/Persistence/RebirthStationCompletionObservation.cs';$text=[IO.File]::ReadAllText((Join-Path $root $relative)).Replace("`r`n","`n");$old=ExtractMethod $text '    internal bool TrySavePublication('; $new=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Observation.method.txt'));$text=$text.Replace($old,$new);SaveCandidate $relative $text 'Observation'
