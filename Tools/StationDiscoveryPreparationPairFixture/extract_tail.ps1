$ErrorActionPreference='Stop'
$base=$PSScriptRoot
$path=Join-Path $base '../../Scripts/Crafting/UI/RebirthStationPreparationService.cs'
$src=[IO.File]::ReadAllText($path)
$start=$src.IndexOf('    private static bool CanUse(');$open=$src.IndexOf('{',$start);$depth=1;$end=$open+1
while($depth){if($src[$end]-eq '{'){$depth++};if($src[$end]-eq '}'){$depth--};$end++}
$can=$src.Substring($start,$end-$start)
$needle='        if(!savedIntent.MatchesCached(owner,attempted)||'
$s=$src.IndexOf($needle);$e=$src.IndexOf('        current.SetDisableModifiedCheck(true);',$s)
$guard=$src.Substring($s,$e-$s)
$actual=$guard.Replace('return false;','return false;')+'return true;'
$candidate='if(!RebirthStationDiscoveryPreparationIntegration.IsAllowedIntent(player,owner,attempted,source)||!CanUse(current,source,player)||!savedIntent.MatchesCached(owner,attempted))return false;return true;'
$header='using System;using System.Linq; static class ActualPublicationTail {'
$args='EntityPlayer player,RebirthWorldCharacterRecord owner,RebirthStationGridAdmission attempted,Recipe source,TileEntityWorkstation current,RebirthStationSavedPreparationIntent savedIntent'
[IO.File]::WriteAllText((Join-Path $base 'ActualPublicationTail.cs'),$header+$can+' public static bool Current('+$args+'){'+$actual+'} public static bool Candidate('+$args+'){'+$candidate+'}}')
Write-Output ('SOURCE SHA256 '+(Get-FileHash $path -Algorithm SHA256).Hash)
Write-Output ('EXTRACTED GUARD '+$guard)
