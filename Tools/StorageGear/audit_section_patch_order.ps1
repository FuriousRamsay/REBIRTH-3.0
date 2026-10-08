$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$native=Join-Path $root '../../Data/Config/XUi_InGame/windows.xml'
$reports=@()
foreach($rebirth in @($false,$true)){
 [xml]$script:merged=[IO.File]::ReadAllText($native)
 $script:misses=0; $script:missing=@()
 function ApplyNode($node,$directory){
  if($node.NodeType -ne [Xml.XmlNodeType]::Element){return}
  if($node.LocalName -in @('configs','conditional')){foreach($child in $node.ChildNodes){ApplyNode $child $directory};return}
  if($node.LocalName -eq 'if'){
   if($node.GetAttribute('cond') -ne "character_progression('Rebirth')"){throw ('Unsupported condition '+$node.GetAttribute('cond'))}
   if($rebirth){foreach($child in $node.ChildNodes){ApplyNode $child $directory}};return
  }
  if($node.LocalName -eq 'else'){if(!$rebirth){foreach($child in $node.ChildNodes){ApplyNode $child $directory}};return}
  if($node.LocalName -eq 'include'){
   $path=[IO.Path]::GetFullPath((Join-Path $directory $node.GetAttribute('filename')))
   [xml]$included=[IO.File]::ReadAllText($path);ApplyNode $included.DocumentElement ([IO.Path]::GetDirectoryName($path));return
  }
  if($node.LocalName -eq 'atlas'){return} # Asset declaration, outside window-node patch scope.
  if(!$node.HasAttribute("xpath")){throw ("Missing xpath "+$node.OuterXml.Substring(0,[Math]::Min(180,$node.OuterXml.Length)))}
  $targets=@($script:merged.SelectNodes($node.GetAttribute('xpath')))
  if(!$targets.Count){$script:misses++;$script:missing+=@{operation=$node.LocalName;xpath=$node.GetAttribute("xpath");file=$directory}}
  foreach($target in $targets){
   switch($node.LocalName){
    'append' {foreach($child in $node.ChildNodes){if($child.NodeType -eq [Xml.XmlNodeType]::Element){[void]$target.AppendChild($script:merged.ImportNode($child,$true))}}}
    'remove' {if($target -is [Xml.XmlAttribute]){[void]$target.OwnerElement.RemoveAttributeNode($target)}else{[void]$target.ParentNode.RemoveChild($target)}}
    'removeattribute' {if($target -is [Xml.XmlAttribute]){[void]$target.OwnerElement.RemoveAttributeNode($target)}else{$target.RemoveAttribute($node.GetAttribute('name'))}}
    'setattribute' {$target.SetAttribute($node.GetAttribute('name'),$node.InnerText)}
    'set' {$target.Value=$node.InnerText}
    default {throw ('Unsupported operation '+$directory+' '+$node.OuterXml.Substring(0,[Math]::Min(220,$node.OuterXml.Length)))}
   }
  }
 }
 $patch=Join-Path $root 'Config/XUi_InGame/windows.xml';[xml]$authored=[IO.File]::ReadAllText($patch)
 ApplyNode $authored.DocumentElement ([IO.Path]::GetDirectoryName($patch))
 $navigation=@()
 foreach($theory in $script:merged.SelectNodes('//button[@name="rbBackpackTheorySection"]')){
  $sell=$theory.ParentNode.SelectSingleNode('./button[@name="rbBackpackSellSection"]');$sort=$theory.ParentNode.SelectSingleNode('./button[contains(@name,"Sort")]')
  if($sort -and ($sort.GetAttribute("nav_left") -ne "rbBackpackSellSection" -or $sell.GetAttribute("nav_right") -ne $sort.GetAttribute("name"))){$navigation+=@{sort=$sort.GetAttribute("name");parent=$theory.ParentNode.GetAttribute("name")}}
 }
 if($rebirth){
  $content=$script:merged.SelectSingleNode('/windows/window[@name="windowLooting"]/rect[@name="content"]')
  if($content.GetAttribute('height') -ne '651'){throw 'Loot viewport height mismatch'}
  foreach($n in $content.SelectNodes('./scrollview | ./defaultscrollbar')){foreach($a in $n.Attributes){if($a.Value -match 'gridDimensions.*\* 75'){throw 'Loot scroll geometry still uses old cell size'}}}
  $queue=$content.SelectSingleNode('.//grid[@name="queue"]')
  if($queue.GetAttribute('cell_width') -ne '65' -or $queue.GetAttribute('cell_height') -ne '65'){throw 'Loot cell dimensions mismatch'}
 }
 $inventoryCount=$script:merged.SelectNodes('/windows/window[@name="windowBackpack"]//grid[@controller="Backpack"]').Count
 $backpack=$script:merged.SelectSingleNode('/windows/window[@name="windowBackpack"]')
 if($backpack.SelectNodes('./rect[@name="content"]/defaultscrollbar').Count -ne 0 -or $backpack.SelectNodes('.//rect[@name="rebirthBackpackNativeScrollHost"]/defaultscrollbar').Count -ne 1){throw 'Backpack scrollbar ownership mismatch'}
 if($inventoryCount -ne 1){throw "Duplicate or missing backpack grids: $inventoryCount"}
 $locks=@()
 foreach($lock in $script:merged.SelectNodes('//button')){
  if($lock.GetAttribute('name') -notmatch 'ToggleLockMode|BagLock|InventoryLock|contextContainerLock|contextBackpackLock'){continue}
  $parent=$lock.ParentNode;$window=$lock
  while($window -and $window.LocalName -ne 'window'){$window=$window.ParentNode}
  $theory=$parent.SelectSingleNode('./button[@name="rbBackpackTheorySection"]');$sale=$parent.SelectSingleNode('./button[@name="rbBackpackSellSection"]')
  $hidden=$false;$ancestor=$lock
  while($ancestor -is [Xml.XmlElement]){if($ancestor.GetAttribute('visible') -eq 'false'){$hidden=$true};$ancestor=$ancestor.ParentNode}
  if(!$hidden -and (!$theory -or !$sale)){throw ('Visible inventory lock lacks both compartment buttons: '+$lock.GetAttribute('name'))}
  $locks+=@{window=if($window){$window.GetAttribute('name')}else{'unknown'};lock=$lock.GetAttribute('name');sections=!!($theory -and $sale);theory=!!$theory;sale=!!$sale;staticallyHidden=$hidden}
 }
 $iconLocks=@()
 foreach($icon in $script:merged.SelectNodes('//button[@sprite="ui_game_symbol_lock" or @sprite="ui_game_symbol_unlock"]')){
  $window=$icon;$hidden=$false
  while($window -is [Xml.XmlElement]){
   if($window.GetAttribute('visible') -eq 'false'){$hidden=$true}
   if($window.LocalName -eq 'window'){break};$window=$window.ParentNode
  }
  if(!$window -or $window.LocalName -ne 'window'){continue}
  $both=!!($window.SelectSingleNode('.//button[@name="rbBackpackTheorySection"]') -and $window.SelectSingleNode('.//button[@name="rbBackpackSellSection"]'))
  # Native recipe-requirements navigation uses a lock glyph but is not an inventory lock control.
  $requirementsNavigation=$icon.GetAttribute('name') -eq 'showunlocksButton'
  if(!$hidden -and !$requirementsNavigation -and !$both){throw ('Visible lock-icon window lacks compartment access: '+$window.GetAttribute('name'))}
  $iconLocks+=@{window=$window.GetAttribute('name');lock=$icon.GetAttribute('name');bothSectionsInWindow=$both;staticallyHidden=$hidden;recipeRequirementsNavigation=$requirementsNavigation}
 }
 $reports+=@{rebirth=$rebirth;unmatchedOperations=$script:misses;unmatchedDetails=$script:missing;backpackGrids=$inventoryCount;sectionPairs=$script:merged.SelectNodes('//button[@name="rbBackpackTheorySection"]').Count;locks=$locks;iconLocks=$iconLocks;missingSortNavigation=$navigation}
}
$reports|ConvertTo-Json -Depth 6