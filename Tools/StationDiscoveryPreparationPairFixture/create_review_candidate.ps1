$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$path=Join-Path $root 'Scripts/Crafting/UI/RebirthStationPreparationService.cs'
$src=[IO.File]::ReadAllText($path)
if((Get-FileHash $path).Hash-ne '60267DA0AD2FFB955ABC060B2814418313F59AC257EE14A6417F773162102080'){throw 'baseline changed: review required'}
function EditMethod($text,$signature,$old,$new){$start=$text.IndexOf($signature);$open=$text.IndexOf('{',$start);$depth=1;$end=$open+1;while($depth){if($text[$end]-eq '{'){$depth++};if($text[$end]-eq '}'){$depth--};$end++};$m=$text.Substring($start,$end-$start);if($m.IndexOf($old)-lt 0){throw 'exact context missing'};$m=$m.Replace($old,$new);return $text.Substring(0,$start)+$m+$text.Substring($end)}
$cr="`n"; $src=$src.Replace("`r`n","`n")
$src=EditMethod $src '    internal static bool TryBeginPublicationAttempt(' '        var definitions=XUiM_Recipes.GetRecipes();' ('        if(!CandidateNativeGuard.TryCapture(player,station,out var finalGuard))return false;'+$cr+'        var definitions=XUiM_Recipes.GetRecipes();')
$old='            !RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,prepared,source)||!CanUse(finalStation,source,player))return false;'
$new='            !RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,prepared,source)||!CanUse(finalStation,source,player)||'+$cr+'            !RebirthStationLiveAccess.TryResolve(player,position,creationId,out var checkedStation,out var checkedOwner)||'+$cr+'            !ReferenceEquals(checkedOwner,owner)||!finalGuard.Matches(player,checkedStation)||'+$cr+'            !HasNoPublication(owner,checkedStation,job)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||'+$cr+'            !savedIntent.MatchesCached(owner,prepared))return false;'
$src=EditMethod $src '    internal static bool TryBeginPublicationAttempt(' $old $new
$src=EditMethod $src '    internal static bool TryPublishPrepared(' '            !TryBeginPublicationAttempt(player,position,creationId,job,out var attempted))return false;' ('            !CandidateNativeGuard.TryCapture(player,station,out var finalGuard)||'+$cr+'            !TryBeginPublicationAttempt(player,position,creationId,job,out var attempted))return false;')
$old='        if(!savedIntent.MatchesCached(owner,attempted)||'+$cr+'            !RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,attempted,source)||!CanUse(current,source,player))return false;'
$new='        if(!RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,attempted,source)||!CanUse(current,source,player)||'+$cr+'            !RebirthStationLiveAccess.TryResolve(player,position,creationId,out var checkedStation,out var checkedOwner)||'+$cr+'            !ReferenceEquals(checkedOwner,owner)||!finalGuard.Matches(player,checkedStation)||'+$cr+'            !HasNoPublication(owner,checkedStation,job)||!RebirthWorldCharacterRepository.IsCurrentCachedRecord(owner)||'+$cr+'            !savedIntent.MatchesCached(owner,attempted))return false;'
$src=EditMethod $src '    internal static bool TryPublishPrepared(' $old $new
$helper=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'NativeTail/CandidateNativeGuard.cs'))
$helper=$helper.Substring($helper.IndexOf('internal sealed class')).Replace('internal sealed class CandidateNativeGuard','private sealed class CandidateNativeGuard').Replace('ActualStackComparison.IsSameStackSnapshot','RebirthStationGridIngredients.IsSameStackSnapshot')
$last=$src.LastIndexOf('}');$src=$src.Substring(0,$last)+$helper+$cr+$src.Substring($last)
$dest=Join-Path $PSScriptRoot 'CompletePreparationService.candidate.txt';[IO.File]::WriteAllText($dest,$src)
$patch= & git diff --no-index -- $path $dest
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'CompletePreparationService.unapplied.diff'),($patch-join "`n"))
Write-Output ('CANDIDATE SHA '+(Get-FileHash $dest).Hash)
