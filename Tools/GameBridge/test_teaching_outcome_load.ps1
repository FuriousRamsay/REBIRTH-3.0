$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$store=Get-Content -Raw (Join-Path $root 'Scripts/Survivor/Progression/RebirthTeachingOutcomeStore.cs')
$atomic=Get-Content -Raw (Join-Path $root 'Scripts/Survivor/Persistence/RebirthAtomicXmlFile.cs')
$code='using System;using System.IO;using System.Text;using System.Xml;using System.Xml.Linq;using System.Globalization;using System.Collections.Generic;'+$store.Substring($store.IndexOf('public sealed class RebirthTeachingDurableOutcome'))+$atomic.Substring($atomic.IndexOf('public static class RebirthAtomicXmlFile'))+@"
public static class GameIO {public static string DirectoryPath;public static string GetSaveGameDir(){return DirectoryPath;}}
public static class Log {public static void Warning(string s){}}
"@
$scope=Get-Content -Raw (Join-Path $root "Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs")
$code += $scope.Substring($scope.IndexOf("public static class RebirthSurvivorRequestScope"))
Add-Type -TypeDefinition $code
$folder=Join-Path ([IO.Path]::GetTempPath()) ('rebirth-teaching-'+[Guid]::NewGuid().ToString('N'))
[GameIO]::DirectoryPath=$folder
$journal=Join-Path $folder 'RebirthData/Survivor/TeachingOutcomes.xml'
try {
 $outcome=[RebirthTeachingDurableOutcome]::new();$outcome.OutcomeId='test';$outcome.InstructorStorageKey='teacher';$outcome.StudentStorageKey='student';$outcome.SkillId='skill';$outcome.HistoryKey='student:skill';$outcome.InstructorCreationId=[Guid]::NewGuid().ToString('N');$outcome.StudentCreationId='legacy-'+('a'*64)
 $reason='';if(-not [RebirthTeachingOutcomeStore]::TryReserve($outcome,[ref]$reason)){throw $reason}
 $good=[IO.File]::ReadAllText($journal)
 [RebirthTeachingOutcomeStore]::Reset();if([RebirthTeachingOutcomeStore]::Snapshot().Length -ne 1){throw 'valid outcome failed reload'}
 if(-not [RebirthTeachingOutcomeStore]::TryReserve($outcome.Clone(),[ref]$reason)){throw 'exact retry refused'}
 foreach($field in @('InstructorCreationId','StudentCreationId','InstructorStorageKey','StudentStorageKey','SkillId','HistoryKey','StudentKnowledgeTarget','HistoryTargetCount','CompletedUtcTicks','HasLastingLesson','LastingLessonSeconds','LastingLessonMultiplier','TeacherAward')){
  $changed=$outcome.Clone();$info=$changed.GetType().GetField($field)
  if($info.FieldType -eq [string]){$info.SetValue($changed,'different')}
  elseif($info.FieldType -eq [bool]){$info.SetValue($changed,$true)}
  else{$info.SetValue($changed,[Convert]::ChangeType(2,$info.FieldType))}
  if([RebirthTeachingOutcomeStore]::TryReserve($changed,[ref]$reason)){throw ('changed intent accepted: '+$field)}
  if([IO.File]::ReadAllText($journal) -ne $good){throw 'conflicting retry rewrote journal'}
 }
 foreach($stage in @('StudentApplied','InstructorApplied','RewardApplied')){
  $preacked=$outcome.Clone();$preacked.OutcomeId='new-preacked-'+$stage;$preacked.$stage=$true
  if([RebirthTeachingOutcomeStore]::TryReserve($preacked,[ref]$reason)){throw ('new preacknowledged stage accepted: '+$stage)}
  if([IO.File]::ReadAllText($journal) -ne $good){throw 'preacknowledged reservation rewrote journal'}
 }
 $legacy=[xml]$good;$legacy.DocumentElement.SetAttribute('version','1')
 foreach($a in @('binding','instructorCreation','studentCreation')){$legacy.DocumentElement.FirstChild.RemoveAttribute($a)}
 [IO.File]::WriteAllText($journal,$legacy.OuterXml);[RebirthTeachingOutcomeStore]::Reset()
 $old=[RebirthTeachingOutcomeStore]::Snapshot()
 if($old.Length -ne 1 -or [RebirthTeachingOutcomeStore]::HasCharacterBinding($old[0])){throw 'legacy outcome lost or guessed'}
 if([RebirthTeachingOutcomeStore]::AcknowledgeStudent('test')){throw 'legacy outcome acknowledged without binding'}
 $new=$outcome.Clone();$new.OutcomeId='new-bound'
 if(-not [RebirthTeachingOutcomeStore]::TryReserve($new,[ref]$reason)){throw 'legacy blocks new lesson'}
 [RebirthTeachingOutcomeStore]::Reset();$mixed=[RebirthTeachingOutcomeStore]::Snapshot()
 if($mixed.Length -ne 2){throw 'mixed journal lost outcomes'}
 if(@($mixed | Where-Object {[RebirthTeachingOutcomeStore]::HasCharacterBinding($_)}).Count -ne 1){throw 'mixed binding changed'}
 $receipts=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($i in 1..300){[void]$receipts.Add(('z-ordinary-'+$i))}
 foreach($id in @('test','new-bound')){foreach($stage in @('student','instructor','reward')){[void]$receipts.Add('teaching:'+$id+':'+$stage)}}
 [void]$receipts.Add('teaching:finished:reward')
 [RebirthTeachingOutcomeStore]::PruneAwardReceipts($receipts)
 if($receipts.Count -ne 262){throw ('pending receipt retention count: '+$receipts.Count+' teaching='+([string]::Join(',',@($receipts | Where-Object {$_ -like 'teaching:*'}))))}
 foreach($id in @('test','new-bound')){foreach($stage in @('student','instructor','reward')){if(-not $receipts.Contains('teaching:'+$id+':'+$stage)){throw 'pending receipt pruned'}}}
 if($receipts.Contains('teaching:finished:reward')){throw 'ordinary historical receipt not bounded'}
 if(-not [RebirthTeachingOutcomeStore]::AcknowledgeStudent('new-bound') -or
    -not [RebirthTeachingOutcomeStore]::AcknowledgeInstructor('new-bound') -or
    -not [RebirthTeachingOutcomeStore]::AcknowledgeReward('new-bound')){throw 'bound completion failed'}
 [RebirthTeachingOutcomeStore]::Reset()
 foreach($i in 1..300){[void]$receipts.Add('z-more-'+$i)}
 [RebirthTeachingOutcomeStore]::PruneAwardReceipts($receipts)
 foreach($stage in @('student','instructor','reward')){if(-not $receipts.Contains('teaching:new-bound:'+$stage)){throw 'backup pending receipt pruned after final completion/reload'}}
 $later=$outcome.Clone();$later.OutcomeId='later'
 if(-not [RebirthTeachingOutcomeStore]::TryReserve($later,[ref]$reason)){throw 'backup rotation reserve failed'}
 [RebirthTeachingOutcomeStore]::Reset()
 foreach($i in 1..300){[void]$receipts.Add('z-rotation-'+$i)}
 [RebirthTeachingOutcomeStore]::PruneAwardReceipts($receipts)
 foreach($stage in @('student','instructor','reward')){if($receipts.Contains('teaching:new-bound:'+$stage)){throw 'obsolete backup protection never released'}}
 [IO.File]::WriteAllText($journal+'.bak','<broken')
 [RebirthTeachingOutcomeStore]::Reset()
 [void]$receipts.Add('teaching:unknown-backup:reward')
 foreach($i in 1..300){[void]$receipts.Add('z-pressure-'+$i)}
 [RebirthTeachingOutcomeStore]::PruneAwardReceipts($receipts)
 if(-not $receipts.Contains('teaching:unknown-backup:reward')){throw 'unreadable backup discarded receipt'}
 $cases=[Collections.Generic.List[string]]::new()
 foreach($bad in @('<broken','<wrong version="1"/>','<rebirthTeachingOutcomes version="999"/>')){$cases.Add($bad)}
 foreach($pair in @(@('instructorCreation','invalid'),@('studentCreation','legacy-ABC'),@('binding','unknown'),@('studentTarget','NaN'),@('teacherAward','Infinity'),@('historyTarget','oops'),@('completedUtcTicks','9223372036854775807'),@('lastingMultiplier','-1'),@('studentApplied','yes'),@('id',''),@('historyKey',''))){$doc=[xml]$good;$doc.DocumentElement.FirstChild.SetAttribute($pair[0],$pair[1]);$cases.Add($doc.OuterXml)}
 $doc=[xml]$good;[void]$doc.DocumentElement.AppendChild($doc.DocumentElement.FirstChild.CloneNode($true));$cases.Add($doc.OuterXml)
 $doc=[xml]$good;[void]$doc.DocumentElement.AppendChild($doc.CreateElement('unexpected'));$cases.Add($doc.OuterXml)
 if([IO.File]::Exists($journal+".bak")){[IO.File]::Delete($journal+".bak")}
 foreach($bad in $cases){
  [IO.File]::WriteAllText($journal,$bad);[RebirthTeachingOutcomeStore]::Reset()
  if([RebirthTeachingOutcomeStore]::TryReserve($outcome,[ref]$reason)){throw 'bad history admitted reservation'}
  if([RebirthTeachingOutcomeStore]::AcknowledgeStudent('test')){throw 'bad history acknowledged'}
  if([RebirthTeachingOutcomeStore]::Snapshot().Length -ne 0){throw 'bad history exposed outcomes'}
  if([IO.File]::ReadAllText($journal) -ne $bad){throw 'bad history overwritten'}
  $unavailable=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
  foreach($i in 1..300){[void]$unavailable.Add('z-'+$i)}
  [void]$unavailable.Add('teaching:unknown:reward')
  [RebirthTeachingOutcomeStore]::PruneAwardReceipts($unavailable)
  if(-not $unavailable.Contains('teaching:unknown:reward') -or $unavailable.Count -ne 257){throw 'unavailable journal discarded teaching evidence'}
 }
 'PASS actual teaching store/atomic writer: new store reserves; malformed XML/root/version block writes and acknowledgement; original bytes preserved'
} finally {
 [RebirthTeachingOutcomeStore]::Reset()
 foreach($suffix in @('','.bak','.tmp')){if([IO.File]::Exists($journal+$suffix)){[IO.File]::Delete($journal+$suffix)}}
 foreach($dir in @((Split-Path $journal), (Join-Path $folder 'RebirthData'), $folder)){if([IO.Directory]::Exists($dir)){[IO.Directory]::Delete($dir)}}
}
