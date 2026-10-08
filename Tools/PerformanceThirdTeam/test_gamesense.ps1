$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
function StripInstall([string]$source,[string]$name){
 $start=$source.IndexOf('public static class RebirthGameSenseDedupePatch');$source=$source.Substring($start)
 $a=$source.IndexOf('    public static string Install(');$b=$source.IndexOf('{',$a);$depth=1;$end=$b+1
 while($depth -gt 0 -and $end -lt $source.Length){if($source[$end]-eq'{'){$depth++};if($source[$end]-eq'}'){$depth--};$end++}
 if($a-lt 0 -or $depth-ne 0){throw 'Install extraction failed'}
 return $source.Remove($a,$end-$a).Replace('RebirthGameSenseDedupePatch',$name)
}
$current=Get-Content -Raw (Join-Path $root 'Scripts/Performance/RebirthGameSenseDedupePatch.cs')
$baseline=(git -C $root show HEAD:Scripts/Performance/RebirthGameSenseDedupePatch.cs)-join "`n"
$fixture=Get-Content -Raw (Join-Path $PSScriptRoot 'gamesense_fixture.cs')
Add-Type -TypeDefinition $fixture.Replace('// PRODUCTION_CLASSES',((StripInstall $baseline 'Before')+(StripInstall $current 'After')))
[GameSenseChecks]::Run()
'PASS actual HEAD/current GameSense Prefix + fields:10000 random events; null/context/value/expiry/rewind/nonfinite clocks; ToString exceptions; exact decisions/counters. Unity clock doubled; HTTP/native patches not exercised.'
