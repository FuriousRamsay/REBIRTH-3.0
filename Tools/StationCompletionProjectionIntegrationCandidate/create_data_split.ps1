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
function Method($text,$sig){$a=$text.IndexOf($sig);if($a-lt 0){throw 'missing'};$b=$text.IndexOf('{',$a);$d=1;$i=$b+1;while($d){if($text[$i]-eq '{'){$d++};if($text[$i]-eq '}'){$d--};$i++};$text.Substring($a,$i-$a)}
$path=Join-Path $root 'Scripts/Survivor/Persistence/RebirthStationCompletionExpectationProjection.cs';$s=[IO.File]::ReadAllText($path).Replace("`r`n","`n")
$read=Method $s '    internal static bool TryRead('
$stored=$read.Replace('internal static bool TryRead(','internal static bool TryReadStored(').Replace('completed,IList<Recipe> definitions,','completed,').Replace('            if(!Semantic(bytes,admission,intent,definitions))return false;'+"`n",'')
$all=Method $s '    internal static bool ReadAll('
$allStored=$all.Replace('ReadAll(','ReadAllStored(').Replace('completed,IList<Recipe> definitions,','completed,').Replace('!TryRead(node,a,i,q,c,definitions,out var projection)','!TryReadStored(node,a,i,q,c,out var projection)')
$write=Method $s '    internal static XElement WriteAll('
$writeStored=$write.Replace('WriteAll(','WriteAllStored(').Replace('completed,IList<Recipe> definitions)','completed)').Replace('!ReadAll(wrapper,admissions,intents,queued,completed,definitions,out _)','!ReadAllStored(wrapper,admissions,intents,queued,completed,out _)')
$s=$s.Replace($read,$read+"`n"+$stored).Replace($all,$all+"`n"+$allStored).Replace($write,$write+"`n"+$writeStored)
$dest=Join-Path $PSScriptRoot 'Projection.candidate.txt';[IO.File]::WriteAllText($dest,$s);$diff=& git diff --no-index -- $path $dest;[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'Projection.unapplied.diff'),($diff-join "`n"));Write-Output ('PROJECTION CANDIDATE '+(Get-FileHash $dest).Hash)
$repo=Join-Path $PSScriptRoot 'Repository.candidate.txt';$text=[IO.File]::ReadAllText($repo)
$text=$text.Replace('RebirthStationCompletionExpectationProjection.WriteAll(state.StationCompletionExpectationProjections,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications,XUiM_Recipes.GetRecipes())','RebirthStationCompletionExpectationProjection.WriteAllStored(state.StationCompletionExpectationProjections,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications)')
$text=$text.Replace('RebirthStationCompletionExpectationProjection.ReadAll(node,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications,node.Elements("stationCompletionExpectationProjections").Any()?XUiM_Recipes.GetRecipes():null,out var completionProjections)','RebirthStationCompletionExpectationProjection.ReadAllStored(node,state.StationPreparations,state.StationTerminalIntents,state.StationPublications,state.StationCompletionPublications,out var completionProjections)')
[IO.File]::WriteAllText($repo,$text);$diff=& git diff --no-index -- (Join-Path $root 'Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs') $repo;[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'Repository.unapplied.diff'),($diff-join "`n"))
Write-Output ('REPO CANDIDATE '+(Get-FileHash $repo).Hash)
