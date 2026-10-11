$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$base=Join-Path (Split-Path (Split-Path $root -Parent) -Parent) 'Data/Config/loot.xml'
function Apply-Nodes($doc,$nodes,$file,$timed) {
 foreach($n in $nodes) {
  if($n.NodeType -ne 'Element'){continue}
  switch($n.LocalName) {
   include { $p=Join-Path (Split-Path $file -Parent) $n.GetAttribute('filename'); [xml]$patch=[IO.File]::ReadAllText($p); Apply-Nodes $doc $patch.DocumentElement.ChildNodes $p $timed }
   conditional {
    $chosen=$false
    foreach($b in $n.ChildNodes) {
     if($b.NodeType -ne 'Element'){continue}
     $yes=$false
     if($b.LocalName -eq 'else'){$yes=!$chosen}
     elseif($b.GetAttribute('cond') -eq "character_progression('Rebirth')"){$yes=$true}
     elseif($b.GetAttribute('cond') -eq "isoption('RequireTimedReading', 'bool', 'false')"){$yes=!$timed}
     elseif($b.GetAttribute('cond') -match "^xpath\('(.+)'\) != null$"){$yes=$null -ne $doc.SelectSingleNode($Matches[1])}
     else {throw "Unmodelled condition: $($b.OuterXml)"}
     if($yes -and !$chosen){Apply-Nodes $doc $b.ChildNodes $file $timed;$chosen=$true}
    }
   }
   default {
    $targets=@($doc.SelectNodes($n.GetAttribute('xpath')))
    foreach($t in $targets) {
     switch($n.LocalName) {
      remove {if($t -is [Xml.XmlAttribute]){$null=$t.OwnerElement.RemoveAttributeNode($t)}else{$null=$t.ParentNode.RemoveChild($t)}}
      set {$t.InnerText=$n.InnerText}
      removeattribute {if($t -is [Xml.XmlAttribute]){$null=$t.OwnerElement.RemoveAttributeNode($t)}else{$t.RemoveAttribute($n.GetAttribute('name'))}}
      setattribute {$t.SetAttribute($n.GetAttribute('name'),$n.InnerText)}
      append {foreach($c in $n.ChildNodes){if($c.NodeType -eq 'Element'){$null=$t.AppendChild($doc.ImportNode($c,$true))}}}
      insertBefore {foreach($c in $n.ChildNodes){if($c.NodeType -eq 'Element'){$null=$t.ParentNode.InsertBefore($doc.ImportNode($c,$true),$t)}}}
      insertAfter {foreach($c in @($n.ChildNodes) | Sort-Object {0} -Descending){if($c.NodeType -eq 'Element'){$null=$t.ParentNode.InsertAfter($doc.ImportNode($c,$true),$t)}}}
      default {throw "Unsupported operation $($n.LocalName) in $file"}
     }
    }
   }
  }
 }
}
foreach($timed in @($true,$false)) {
 [xml]$doc=[IO.File]::ReadAllText($base)
 $file=Join-Path $root 'Config/loot.xml';[xml]$patch=[IO.File]::ReadAllText($file)
 Apply-Nodes $doc $patch.DocumentElement.ChildNodes $file $timed
 $groups=@{};foreach($g in $doc.SelectNodes('/lootcontainers/lootgroup')){$groups[$g.GetAttribute('name')]=$g}
 $seen=@{};$queue=New-Object 'Collections.Generic.Queue[Xml.XmlElement]'
 $crates=@($doc.SelectNodes("/lootcontainers/lootcontainer[starts-with(@name,'airDropPurge_')]"))
 if($crates.Count -ne 5){throw "Expected five reward containers"}
 foreach($c in $crates){if($c.GetAttribute('size') -ne '8,4'){throw 'Crate size changed'};$queue.Enqueue($c)}
 $items=New-Object 'Collections.Generic.HashSet[string]'
 while($queue.Count){$node=$queue.Dequeue();foreach($i in $node.SelectNodes('item')){
  if($i.HasAttribute('group')){$name=$i.GetAttribute('group');if(!$groups.ContainsKey($name)){throw "Missing reachable group $name"};if(!$seen.ContainsKey($name)){$seen[$name]=$true;$queue.Enqueue($groups[$name])}}
  elseif($i.HasAttribute('name')){$null=$items.Add($i.GetAttribute('name'))}
 }}
 foreach($ex in @('loot_food_exclusions.xml','vanilla_progression_exclusions.xml')) {
  [xml]$exclude=[IO.File]::ReadAllText((Join-Path $root "Config/_Survivor/$ex"))
  foreach($remove in $exclude.SelectNodes('/configs/remove')){if($doc.SelectNodes($remove.GetAttribute('xpath')).Count){throw "Excluded items survived $ex"}}
 }
 Write-Output "PASS Rebirth timedReading=$timed : 5 containers, $($seen.Count) reachable groups, $($items.Count) reachable item names; no missing groups; final food/literature exclusions hold."
}
Write-Output 'Offline XPath patch model, not the native game patcher or random loot generation. Both timed-reading branches checked; unrelated progression modes not qualified.'
