param([Parameter(Mandatory=$true)][string]$ConfigRoot)
$ErrorActionPreference='Stop';$root=(Resolve-Path "$PSScriptRoot/../..").Path
$config=(Resolve-Path -LiteralPath $ConfigRoot).Path
$entries=Get-Content "$root/_Documentation/MapZoomSlider_20261028/localization-rows.json" -Raw|ConvertFrom-Json
foreach($entry in $entries){
 $key=$entry.key;$english=$entry.english
 if($english.Contains(',')-or$english.Contains('"')-or$english.Contains("`n")){throw 'Review CSV escaping before changing these rows'}
 $rows=@{'Localization.csv'="$key,$english";'Localization.txt'="$key,UI,REBIRTH,,$english,,,,,,,,,,,,,"}
 foreach($name in @('Localization.csv','Localization.txt')){
  $path=Join-Path $config $name;if(!(Test-Path -LiteralPath $path -PathType Leaf)){throw "Missing $name"}
  $bytes=[IO.File]::ReadAllBytes($path)
  if($bytes.Length-ge2-and(($bytes[0]-eq255-and$bytes[1]-eq254)-or($bytes[0]-eq254-and$bytes[1]-eq255))){throw 'Unexpected UTF16 localization'}
  $text=[IO.File]::ReadAllText($path);$found=@($text-split'\r?\n'|Where-Object {$_.StartsWith($key+',',[StringComparison]::Ordinal)})
  if($found.Count-gt1-or($found.Count-eq1-and$found[0]-cne$rows[$name])){throw "Conflicting key $key in $name"}
  if($found.Count-eq0){$prefix=if($text.EndsWith("`n")){''}else{"`r`n"};[IO.File]::AppendAllText($path,$prefix+$rows[$name]+"`r`n",[Text.UTF8Encoding]::new($false))}
 }
}
'PASS five unique English slider keys appended/preserved in CSV and native TXT'
