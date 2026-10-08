param([string]$Root=(Resolve-Path "$PSScriptRoot/../..").Path)
$ErrorActionPreference='Stop'
$checked=0
foreach($file in @('Config/XUi_InGame/windows.xml','Config/XUi_InGame/trader_workspace.xml','Config/XUi_InGame/quest_turnin_workspace.xml','Config/_Cooking/workspace_windows.xml')){
 [xml]$doc=[IO.File]::ReadAllText((Join-Path $Root $file))
 foreach($theory in $doc.SelectNodes("//button[@name='rbBackpackTheorySection']")){
  $parent=$theory.ParentNode;$sell=$parent.SelectSingleNode("button[@name='rbBackpackSellSection']")
  if(!$sell){throw "Missing paired Sell icon: $file"}
  if($theory.controller -ne 'RebirthBackpackSectionButton, RebirthUtils' -or $sell.controller -ne $theory.controller){throw 'Unscoped section controller'}
  $sort=$parent.SelectSingleNode("button[contains(@name,'Sort') or @name='btnSort']")
  if($sort){
   $p=$sort.pos.Split(',');$cx=[double]$p[0];$cy=[double]$p[1]
   if($sort.pivot -ne 'center'){$cx+=[double]$sort.width/2;$cy-=[double]$sort.height/2}
   $t=$theory.pos.Split(',');$s=$sell.pos.Split(',')
   if([double]$t[1] -ne $cy -or [double]$s[1] -ne $cy){throw "Toolbar center misalignment: $file $($sort.name)"}
   if([double]$t[0] -ge [double]$s[0] -or [double]$s[0] -ge $cx){throw 'Wrong section/Sort ordering'}
  }
  $checked++
 }
}
"PASS $checked paired icon definitions; authored Sort center alignment/order and shared controller. Native patch execution/rendering remains unverified."
# Apply only the three new inherited toolbar append directives and their coordinate
# overrides to the exact installed native XML. Other feature patches are outside this check.
[xml]$native=[IO.File]::ReadAllText((Join-Path $Root '../../Data/Config/XUi_InGame/windows.xml'))
[xml]$mod=[IO.File]::ReadAllText((Join-Path $Root 'Config/XUi_InGame/windows.xml'))
foreach($id in @('windowLooting','windowBackpack','windowBagStorage')){
 foreach($a in $mod.SelectNodes("//setattribute[@name='pos']")){
  if($a.xpath.Contains("window[@name='$id']")){
   foreach($n in $native.SelectNodes($a.xpath)){$n.SetAttribute('pos',$a.InnerText)}
  }
 }
 foreach($append in $mod.SelectNodes("//append[button[@name='rbBackpackTheorySection']]")){
  if(!$append.xpath.Contains("window[@name='$id']")){continue}
  $targets=$native.SelectNodes($append.xpath);if($targets.Count -ne 1){throw "Inherited toolbar target missing/ambiguous: $id"}
  foreach($button in $append.SelectNodes("button[@controller='RebirthBackpackSectionButton, RebirthUtils']")){[void]$targets[0].AppendChild($native.ImportNode($button,$true))}
 }
 $toolbar=$native.SelectSingleNode("/windows/window[@name='$id']//rect[@controller='ContainerStandardControls']")
 $theory=$toolbar.SelectSingleNode("button[@name='rbBackpackTheorySection']");$sell=$toolbar.SelectSingleNode("button[@name='rbBackpackSellSection']");$sort=$toolbar.SelectSingleNode("button[@name='btnSort']");$lock=$toolbar.SelectSingleNode("button[@name='btnToggleLockMode']")
 if(!$theory -or !$sell){throw "Inherited pair absent: $id"}
 $t=$theory.pos.Split(',');$s=$sell.pos.Split(',');$r=$sort.pos.Split(',');$l=$lock.pos.Split(',')
 if([double]$t[1] -ne [double]$r[1] -or [double]$s[1] -ne [double]$r[1]){throw "Inherited center mismatch: $id"}
 if([double]$l[0]+16 -gt [double]$t[0]-14 -or [double]$t[0]+14 -gt [double]$s[0]-14 -or [double]$s[0]+14 -gt [double]$r[0]-16){throw "Inherited lock/section/Sort hitboxes overlap: $id"}
}
'PASS three inherited toolbar directives resolve and their local lock/section/Sort hitboxes do not overlap. This limited XML application does not simulate all game/mod patches or native rendering.'

# Start from lock controls too: counting existing section buttons alone cannot detect
# a newly introduced inventory toolbar that forgot both buttons.
$lockCount=0
foreach($file in Get-ChildItem (Join-Path $Root 'Config') -Recurse -Filter '*.xml'){
 [xml]$doc=[IO.File]::ReadAllText($file.FullName)
 foreach($lock in $doc.SelectNodes("//button[@sprite='rb_backpack_lock' or @sprite='ui_game_symbol_lock' or @name='btnToggleLockMode']")){
  $parent=$lock.ParentNode
  $theory=$parent.SelectNodes("button[@name='rbBackpackTheorySection']")
  $sell=$parent.SelectNodes("button[@name='rbBackpackSellSection']")
  if($theory.Count -ne 1 -or $sell.Count -ne 1){throw "Lock toolbar missing or duplicating section buttons: $($file.FullName) $($lock.name)"}
  if($theory[0].section_window -ne 'rebirthBackpackLibrary' -or $sell[0].section_window -ne 'rebirthBackpackSellStash'){throw "Wrong section destination beside lock: $($lock.name)"}
  $lockCount++
 }
}
if($lockCount -eq 0){throw 'No authored lock toolbars found; coverage check is invalid'}
"PASS $lockCount authored lock controls independently checked across all Config XML for exactly one Theory Library and Sell Stash pair."