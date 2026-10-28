param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference='Stop'
$sourcePath=Join-Path $PSScriptRoot '../../Scripts/TraderJobs/RebirthTraderJobs.cs'
$source=[IO.File]::ReadAllText($sourcePath)
$marker='public static class RebirthFetchQuestIdentity'
$start=$source.IndexOf($marker,[StringComparison]::Ordinal)
if($start -lt 0 -or $source.IndexOf($marker,$start+1,[StringComparison]::Ordinal) -ge 0){throw 'Unique current identity class required'}
$end=$source.IndexOf('    public static bool TryFindActiveFetchForPosition(',$start,[StringComparison]::Ordinal)
if($end -le $start){throw 'Identity source boundary changed; fixture extraction refused'}
$identity=$source.Substring($start,$end-$start)+"}`r`n"
foreach($method in @('public static void Bind(', 'public static bool Matches(', 'public static int Count(')){
    if(-not $identity.Contains($method)){throw "Current identity method missing: $method"}
}
$directory=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($Output))
[IO.Directory]::CreateDirectory($directory)|Out-Null
[IO.File]::WriteAllText($Output,$identity)
Write-Output 'Extracted current production Bind/Matches/Count without rewriting their bodies; routing methods excluded.'
