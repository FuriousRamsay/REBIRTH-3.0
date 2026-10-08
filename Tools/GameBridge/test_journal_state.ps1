param([switch]$VerifyReload)
$ErrorActionPreference='Stop'
$bridge=Join-Path $PSScriptRoot 'gamebridge.ps1'
function Call([string[]]$Arguments) {
    $raw=& powershell -ExecutionPolicy Bypass -File $bridge @Arguments
    if($LASTEXITCODE -ne 0){throw ($raw -join "`n")}
    $r=($raw -join "`n")|ConvertFrom-Json
    if(!$r.ok){throw ($raw -join "`n")};return $r
}
$out=Join-Path $PSScriptRoot 'out/journal_state_assertions.json'
$s=Call @('state','-Sections','journal,challenges')
if($s.journal.error){throw $s.journal.error}
if($VerifyReload){
    $before=Get-Content $out -Raw|ConvertFrom-Json
    if($s.journal.unread -ne 1){throw 'Unread preference did not survive reload'}
    $e=$s.journal.entries|Where-Object id -eq $before.unreadId
    if(!$e -or $e.read){throw 'Selected unread identity did not survive reload'}
    Write-Output 'PASS: unread identity and count persisted across game restart';exit 0
}
[xml]$defs=Get-Content (Join-Path $PSScriptRoot '../../Resources/Journal/entries.xml')
$linked=0;$locked=0;$completeRoutes=0
foreach($d in $defs.journal.entry){
    if(!$d.challenges){continue}
    $c=$s.challenges|Where-Object id -eq $d.challenges
    if(!$c){throw "Missing live challenge $($d.challenges)"}
    $eligible=if($d.trigger -eq 'completed'){@($c.objectives|Where-Object {!$_.complete}).Count -eq 0}else{@($c.objectives|Where-Object {$_.current -gt 0 -or $_.complete}).Count -gt 0}
    $found=@($s.journal.entries|Where-Object id -eq $d.id).Count -gt 0
    if($eligible -ne $found){throw "Unlock mismatch: $($d.id) expected=$eligible found=$found"}
    if($found){$linked++;if($d.trigger -eq 'completed'){$completeRoutes++}}else{$locked++}
}
$null=Call @('ui','open','rebirthJournal')
$null=Call @('click','id=journalReadAllHit')
$s=Call @('state','-Sections','journal')
if($s.journal.unread -ne 0){throw 'Mark all read failed'}
$null=Call @('click','id=journalFilterAllHit')
$null=Call @('click','id=journalRow0Hit')
$tree=Call @('uitree','window=rebirthJournal')
$title=($tree.nodes|Where-Object id -eq 'journalEntryTitle').text
$selected=$s.journal.entries|Where-Object title -eq $title
if(!$selected){throw 'Could not identify selected guide'}
$null=Call @('click','id=journalReadToggleHit')
$s=Call @('state','-Sections','journal')
if($s.journal.unread -ne 1 -or ($s.journal.entries|Where-Object id -eq $selected.id).read){throw 'Mark unread failed'}
$null=Call @('click','id=journalRow0Hit')
$s=Call @('state','-Sections','journal')
if($s.journal.unread -ne 0){throw 'Opening an entry did not mark it read'}
$null=Call @('click','id=journalReadToggleHit')
$null=Call @('ui','close','rebirthJournal')
$null=Call @('ui','open','rebirthJournal')
$s=Call @('state','-Sections','journal')
if($s.journal.unread -ne 1){throw 'Opening the journal consumed unread state'}
@{pass=$true;matchedUnlocked=$linked;matchedLocked=$locked;completedRoutes=$completeRoutes;unreadId=$selected.id;unread=1}|ConvertTo-Json|Set-Content $out
Get-Content $out
