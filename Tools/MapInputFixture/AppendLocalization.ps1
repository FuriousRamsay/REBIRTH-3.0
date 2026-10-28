param([Parameter(Mandatory=$true)][string]$ConfigRoot)
$ErrorActionPreference='Stop'
$config=(Resolve-Path -LiteralPath $ConfigRoot).Path
$key='xuiRebirthMapTeleportOutsideWorld'
$english='Choose a point inside the playable world to teleport.'
$rows=@{ 'Localization.csv'="$key,$english"; 'Localization.txt'="$key,UI,REBIRTH,,$english,,,,,,,,,,,,," }
# Append only one unique English row. Never overwrite the shared localization
# files or an existing different definition; repeat installation is idempotent.
foreach($name in @('Localization.csv','Localization.txt')){
 $path=Join-Path $config $name
 if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing localization file: $name"}
 $bytes=[IO.File]::ReadAllBytes($path)
 if($bytes.Length-ge2-and(($bytes[0]-eq255-and$bytes[1]-eq254)-or($bytes[0]-eq254-and$bytes[1]-eq255))){throw 'Unexpected UTF16 localization encoding'}
 $text=[IO.File]::ReadAllText($path)
 $matches=@($text-split'\r?\n'|Where-Object {$_.StartsWith($key+',',[StringComparison]::Ordinal)})
 if($matches.Count-gt1-or($matches.Count-eq1-and$matches[0]-cne$rows[$name])){throw "Conflicting localization key in $name"}
 if($matches.Count-eq0){$prefix=if($text.EndsWith("`n")){''}else{"`r`n"};[IO.File]::AppendAllText($path,$prefix+$rows[$name]+"`r`n",[Text.UTF8Encoding]::new($false))}
}
'PASS unique English key appended/preserved in CSV and native TXT'
