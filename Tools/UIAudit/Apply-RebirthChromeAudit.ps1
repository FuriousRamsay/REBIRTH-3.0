$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$report=[Collections.Generic.List[string]]::new()
$files=Get-ChildItem (Join-Path $root 'Config') -Recurse -Filter '*.xml' | Where-Object {$_.FullName -match 'XUi|windows|templates|workspace'}
foreach($file in $files){
 $s=[IO.File]::ReadAllText($file.FullName)
 # These exact swatches are decorative rules, scrollbar thumbs and selected chrome.
 foreach($color in @('174,22,27','190,32,42','210,55,60')){
  $count=([regex]::Matches($s,[regex]::Escape($color))).Count
  if($count){$report.Add("$($file.Name): $count occurrences of $color replaced");$s=$s.Replace($color,'181,140,255')}
 }
 # Header text only: this does not rename native inventory APIs or player statistics.
 $s=[regex]::Replace($s,'(<label\b[^>]*\btext=")(?i:inventory)("[^>]*>)','$1BACKPACK$2')
 [IO.File]::WriteAllText($file.FullName,$s)
}
$report | Set-Content (Join-Path $root '_Documentation/PlayerReportedFixes/UI_ACCENT_AUDIT_20261008.txt')
