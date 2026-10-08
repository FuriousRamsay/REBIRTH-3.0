#requires -Version 7.0
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -Path @(
 (Join-Path $taskRoot 'Scripts/Survivor/Network/RebirthSurvivorRequestScope.cs'),
 (Join-Path $taskRoot 'Scripts/Survivor/Progression/RebirthTheoryStudyOutcome.cs'),
 (Join-Path $taskRoot 'Scripts/Survivor/Persistence/RebirthTheoryStudyPersistence.cs'))
$script:checks=0
function Assert($condition,$message){if(!$condition){throw $message};$script:checks++}
$id=[guid]::NewGuid();$creation=[guid]::NewGuid().ToString('N')
$outcome=$null
Assert ([RebirthTheoryStudyOutcome]::TryCreate($id,$creation,'skill.cooking','npc','specialist-a',20,23,1,[datetime]::UtcNow.Ticks,20,[ref]$outcome)) 'Valid outcome rejected'
$section=[RebirthTheoryStudyPersistence]::Write($outcome)
$root=[System.Xml.Linq.XElement]::Parse('<progression/>');$root.Add($section)
$loaded=$null;$reason=$null
Assert ([RebirthTheoryStudyPersistence]::TryRead($root,[ref]$loaded,[ref]$reason)) 'Roundtrip rejected'
Assert ([System.Xml.Linq.XNode]::DeepEquals($loaded.Write(),$outcome.Write())) 'Roundtrip changed outcome'
Assert ([RebirthTheoryStudyPersistence]::MatchesOwner($loaded,$creation)) 'Matching owner rejected'
Assert (![RebirthTheoryStudyPersistence]::MatchesOwner($loaded,[guid]::NewGuid().ToString('N'))) 'Different owner accepted'
$image=$loaded.Write();$image.SetAttributeValue('target',99)
Assert ($loaded.Target-eq23) 'Mutable XML escaped immutable record'
Assert ([System.Xml.Linq.XNode]::DeepEquals($loaded.Clone().Write(),$loaded.Write())) 'Clone changed outcome'
$root.Add([System.Xml.Linq.XElement]::new($section))
Assert (![RebirthTheoryStudyPersistence]::TryRead($root,[ref]$loaded,[ref]$reason)) 'Duplicate section accepted'
$root=[System.Xml.Linq.XElement]::Parse('<progression/>')
Assert ([RebirthTheoryStudyPersistence]::TryRead($root,[ref]$loaded,[ref]$reason)) 'Old save rejected'
Assert ($null-eq$loaded) 'Old save invented outcome'
foreach($mutation in @('unknownAttribute','unknownChild','duplicateOutcome','nonWhitespace','wrongVersion')){
 $section=[RebirthTheoryStudyPersistence]::Write($outcome)
 switch($mutation){
  unknownAttribute {$section.SetAttributeValue('extra','x')}
  unknownChild {$section.Add([System.Xml.Linq.XElement]::Parse('<extra/>'))}
  duplicateOutcome {$section.Add($outcome.Write())}
  nonWhitespace {$section.Add('unexpected')}
  wrongVersion {$section.SetAttributeValue('version',2)}
 }
 $root=[System.Xml.Linq.XElement]::Parse('<progression/>');$root.Add($section)
 Assert (![RebirthTheoryStudyPersistence]::TryRead($root,[ref]$loaded,[ref]$reason)) "$mutation accepted"
}
foreach($pair in @(@('target','NaN'),@('target','20'),@('before','-1'),@('mode','music'),@('source','a|b'),@('historyCount','0'),@('seconds','0'),@('creation',[guid]::Empty.ToString('N')))){
 $image=$outcome.Write();$image.SetAttributeValue($pair[0],$pair[1])
 Assert (![RebirthTheoryStudyOutcome]::TryRead($image,[ref]$loaded)) "$($pair[0]) malformed outcome accepted"
}
$normalizedOutcome=$null
Assert ([RebirthTheoryStudyOutcome]::TryCreate([guid]::NewGuid(),([guid]$creation).ToString('D'),'skill.cooking','npc','specialist-a',20,23,1,[datetime]::UtcNow.Ticks,20,[ref]$normalizedOutcome)) 'Formatted creation rejected'
Assert ($normalizedOutcome.CreationId-eq$creation) 'Creation not normalized'
$legacy='legacy-'+('a'*64);$legacyOutcome=$null
Assert ([RebirthTheoryStudyOutcome]::TryCreate([guid]::NewGuid(),$legacy,'skill.cooking','solo','work-receipt',20,21,1,[datetime]::UtcNow.Ticks,20,[ref]$legacyOutcome)) 'Legacy identity rejected'
Assert ([RebirthTheoryStudyPersistence]::MatchesOwner($legacyOutcome,$legacy)) 'Legacy owner rejected'
"PASS $script:checks actual Theory study outcome/persistence/request-scope checks; repository disk I/O, completion effects and native game not exercised"