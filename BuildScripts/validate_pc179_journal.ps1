$ErrorActionPreference = 'Stop'
$journalSource = Get-Content Scripts/UI/XUiC_RebirthJournal.cs -Raw
$storeSource = $journalSource.Substring(0,$journalSource.IndexOf('[Preserve]')).Replace('using UnityEngine;','').Replace('using UnityEngine.Scripting;','')
$atomicSource = Get-Content Scripts/Survivor/Persistence/RebirthAtomicXmlFile.cs -Raw
$atomicSource = $atomicSource.Substring($atomicSource.IndexOf('public static class RebirthAtomicXmlFile'))
$testStubs = @"
public class RebirthHudTrackingPreferenceContext { public string WorldKey; public string PlayerStorageKey; }
public static class RebirthHudTrackingPreferenceStore { public static bool TryResolveCurrentContext(out RebirthHudTrackingPreferenceContext c,out string e){c=null;e="Test stub";return false;} }
public static class GameIO { public static string GetUserGameDataDir(){return "";} }
"@
Add-Type -TypeDefinition ("using System.Text;`nusing System.Xml;`n" + $storeSource + $atomicSource + $testStubs)
$testPath = Join-Path (Get-Location) ('BuildScripts/PC179Build/journal-test-' + [guid]::NewGuid().ToString('N') + '.xml')
$entry = [RebirthJournalEntry]::new()
$entry.Id = [guid]::NewGuid().ToString('N'); $entry.Title = '<Title> & text'; $entry.Type = 'Plans'; $entry.Body = "Line one`n[FF0000]literal markup[-] & <image>text only</image>"; $entry.Created = [DateTime]::UtcNow.ToString('o')
$entries = [System.Collections.Generic.List[RebirthJournalEntry]]::new(); $entries.Add($entry)
$authored = [RebirthJournalEntry]::new(); $authored.Authored = $true; $authored.Image = 'campfire'; $entries.Add($authored)
$errorText = ''
if(-not [RebirthJournalStore]::Save($testPath,$entries,[ref]$errorText)){throw $errorText}
$loaded = [RebirthJournalStore]::Read($testPath)
if($loaded.Count -ne 1 -or $loaded[0].Body -cne $entry.Body -or $loaded[0].Title -cne $entry.Title){throw 'Text round trip failed'}
if((Get-Content $testPath -Raw).Contains('campfire')){throw 'Authored image leaked into personal storage'}
$entry.Body = 'Edited entry'
if(-not [RebirthJournalStore]::Save($testPath,$entries,[ref]$errorText)){throw $errorText}
if([RebirthJournalStore]::Read($testPath)[0].Body -ne 'Edited entry'){throw 'Update failed'}
[IO.File]::WriteAllText($testPath,'<broken')
if([RebirthJournalStore]::Read($testPath)[0].Body -cne $loaded[0].Body){throw 'Backup recovery failed'}
$entries.Clear()
if(-not [RebirthJournalStore]::Save($testPath,$entries,[ref]$errorText)){throw $errorText}
if([RebirthJournalStore]::Read($testPath).Count -ne 0){throw 'Delete persistence failed'}
[IO.File]::WriteAllText($testPath,'<journal><entry id="invalid" type="Notes"><title>T</title><body>B</body></entry></journal>')
$rejected = $false
try{[void][RebirthJournalStore]::Read($testPath)}catch{$rejected = $true}
if(-not $rejected){throw 'Invalid identity accepted'}
Remove-Item -LiteralPath $testPath
if(Test-Path -LiteralPath ($testPath+'.bak')){Remove-Item -LiteralPath ($testPath+'.bak')}
'Journal text round trip, authored exclusion, update, delete, backup recovery, and invalid identity checks passed.'
