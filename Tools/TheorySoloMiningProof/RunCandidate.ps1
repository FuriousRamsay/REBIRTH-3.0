$ErrorActionPreference='Stop'
Add-Type -TypeDefinition ([IO.File]::ReadAllText("$PSScriptRoot/WorkCandidate.cs"))
[xml]$xml=Get-Content "$PSScriptRoot/proposed_difficulty.xml" -Raw;$count=0
function Check($ok,$label){$script:count++;if(!$ok){throw $label}}
foreach($row in $xml.proposed_not_active.work_difficulty){$e=[System.Xml.Linq.XElement]::Parse($row.OuterXml);$m=[WorkDifficultyCandidate]::Read($e);[float]$v=0;Check ($m.Try($m.Reference,[ref]$v) -and $v -eq50) 'proposed reference calibration';Check ($m.Try($m.Reference*4,[ref]$v) -and $v -eq75) 'proposed bounded ceiling';Check ($m.Try($m.Reference/4,[ref]$v) -and $v -eq25) 'proposed low physical work';foreach($bad in @(0,-1,[float]::NaN,[float]::PositiveInfinity)){Check (!$m.Try($bad,[ref]$v)) 'invalid physical work refused'}}
function Witness { $w=[WorkTransitionCandidate]::new();foreach($n in @('World','WorldState','Actor','Creation','Chunk','OriginalAction','OriginalItem','Hit','List','Generation')){$w.$n=[object]::new()};$w.BeforeType=1;$w.BeforeDamage=490;$w.MaxDamage=500;$w.Damage=10;$w.ActorId=10;return $w }
$w=Witness;Check ($w.Complete($w,$true,$true,$false,1,490,0,$true,$false,$false,$true,$true)) 'exact adapter original transition';Check (!$w.Complete($w,$true,$true,$false,1,490,0,$true,$false,$false,$true,$true)) 'same witness cannot replay'
foreach($n in @('World','WorldState','Actor','Creation','Chunk','OriginalAction','OriginalItem','Hit','List','Generation')){$w=Witness;
$c=[WorkTransitionCandidate]::new();foreach($p in @('World','WorldState','Actor','Creation','Chunk','OriginalAction','OriginalItem','Hit','List','Generation')){$c.$p=$w.$p};$c.ActorId=10;$c.$n=[object]::new();Check (!$w.Complete($c,$true,$true,$false,1,490,0,$true,$false,$false,$true,$true)) "changed $n refused"}
foreach($case in @('outerSuppressed','mutationSuppressed','nativeFault','notAir','simulated','fallKeep','foreignList','notPhysical')){
$w=Witness;$outer=$true;$mutation=$true;$fault=$false;$air=$true;$sim=$false;$keep=$false;$list=$true;$physical=$true
switch($case){outerSuppressed{$outer=$false}mutationSuppressed{$mutation=$false}nativeFault{$fault=$true}notAir{$air=$false}simulated{$sim=$true}fallKeep{$keep=$true}foreignList{$list=$false}notPhysical{$physical=$false}}
Check (!$w.Complete($w,$outer,$mutation,$fault,1,490,0,$air,$sim,$keep,$list,$physical)) $case
}
foreach($bad in @('NaN','Infinity','0','-1')){try{[WorkDifficultyCandidate]::Read([System.Xml.Linq.XElement]::Parse("<work_difficulty subject='skill.mining' native_full_stage_damage='$bad' theory_reference='50' maximum='75'/>"));throw 'accepted malformed policy'}catch [System.FormatException]{$count++}}"PASS $count proposed model / explicit original-transition adapter cases; no native observer capture or durable ledger/grant authority."