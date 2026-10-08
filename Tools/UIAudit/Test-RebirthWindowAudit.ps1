$ErrorActionPreference='Stop'
$root=Resolve-Path (Join-Path $PSScriptRoot '../..')
$files=Get-ChildItem (Join-Path $root 'Config') -Recurse -Filter '*.xml' | Where-Object {$_.FullName -match 'XUi|windows|templates|workspace'}
$windows=[Collections.Generic.List[string]]::new();$remaining=[Collections.Generic.List[string]]::new()
foreach($f in $files){$d=[xml](Get-Content $f.FullName -Raw);foreach($w in $d.SelectNodes('//window[@controller]')){$windows.Add($f.Name+' | '+$w.GetAttribute('name')+' | '+$w.GetAttribute('controller'))};foreach($n in $d.SelectNodes('//*')){foreach($a in $n.Attributes){if($a.Value -match '^(\d+),\s*(\d+),\s*(\d+),\s*(\d+)$' -and [int]$Matches[1] -gt 100 -and [int]$Matches[1] -gt [int]$Matches[2]*1.6 -and [int]$Matches[1] -gt [int]$Matches[3]*1.3){if($n.GetAttribute('name') -match 'thumb|scroll|rule|border|header|accent' -or $n.GetAttribute('height') -match '^[1-4]$'){$remaining.Add($f.Name+' | '+$n.GetAttribute('name')+' | '+$a.Name+'='+$a.Value)}}};if($n.LocalName -eq 'label' -and $n.GetAttribute('text') -match '^(?i:inventory)$'){$remaining.Add($f.Name+' | Inventory caption remains')}}}
$windows | Sort-Object -Unique | Set-Content (Join-Path $root '_Documentation/PlayerReportedFixes/UI_WINDOW_AUDIT_COVERAGE_20261008.txt')
if($remaining.Count){$remaining;throw 'UI audit found remaining decorative red or Inventory captions'}
$d=[xml](Get-Content (Join-Path $root 'Config/XUi_InGame/windows.xml') -Raw)
foreach($name in @('rebirthBackpackLibraryRoot','rebirthBackpackSellStashRoot')){
 $w=$d.SelectSingleNode('//window[@name="'+$name+'"]');$mode=if($name -match 'Library'){'theory'}else{'sell'}
 $slots=$w.SelectNodes('.//item_stack[starts-with(@name,"'+$mode+'Available")]');if($slots.Count -ne 169){throw "${name}: incomplete Backpack slot pool"}
 foreach($n in $w.SelectNodes('.//item_stack[starts-with(@name,"'+$mode+'Slot")]')){$p=$n.GetAttribute('pos').Split(',');if([int]$p[0]+78 -gt 922 -or -[int]$p[1]+78 -gt 813){throw 'Section slot outside its panel'}}
 if($mode -eq 'theory'){
  $reading=$w.SelectNodes('.//item_stack[starts-with(@name,"theorySlot")]')
  if($reading.Count -ne 80){throw 'Reading section must expose 80 slots for eight ten-slot tiers'}
  for($i=0;$i -lt 80;$i++){if(!$w.SelectSingleNode('.//item_stack[@name="theorySlot'+$i+'"]')){throw "Missing reading slot $i"}}
 }
 if($w.SelectNodes('.//item_stack[@controller="RebirthBackpackSectionNativeSlot, RebirthUtils"]').Count -ne 249){throw "${name}: native slot scaffold incomplete"}
 if($w.SelectNodes('.//button[starts-with(@name,"'+$mode+'Choose") or starts-with(@name,"'+$mode+'Available")]').Count){throw 'Custom section click controls remain'}
 if(!$w.SelectSingleNode('.//*[@name="'+$mode+'NativeActions" and @controller="ItemActionList"]')){throw 'Standard section actions missing'}
 if(!$w.SelectSingleNode('.//*[@name="listThumb" and @width="12" and @defaultcolor="[rebirthScrollbarThumb]"]') -or !$w.SelectSingleNode('.//*[@name="listTrack" and @width="16"]')){throw 'Working section scrollbar scaffold missing'}
 if(!$w.SelectSingleNode('.//*[@name="rebirthSelectedHeaderSalePrice"]')){throw 'Missing section quote'}
}
$w=$d.SelectSingleNode('//window[@name="rebirthModifyEditor"]');if($w.GetAttribute('width') -ne '1840'){throw 'Modify footprint incorrect'}
foreach($id in @('editorEquipmentPreview','editorSelectionPreview')){
 $n=$w.SelectSingleNode('.//item_stack[@name="'+$id+'"]')
 if(!$n -or $n.GetAttribute('controller') -ne 'RebirthEditorPreviewSlot, RebirthUtils'){throw 'Modify must reuse native preview slot'}
 if($n.ParentNode.ParentNode.GetAttribute("name") -ne "editorBody"){throw "Preview must not enter native Backpack owned-slot pool"}
}
foreach($n in $w.SelectNodes('.//item_stack[starts-with(@name,"editorSlot")]')){if($n.GetAttribute('cell_size') -ne '76'){throw 'Modify slot size incorrect'}}
"$($files.Count) UI XML files parsed; $($windows.Count) controller window definitions inventoried; no remaining decorative red or literal Inventory captions; section and Modify geometry checks passed."

$trader=[xml](Get-Content (Join-Path $root 'Config/XUi_InGame/trader_workspace.xml') -Raw)
$info=$trader.SelectSingleNode("//*[@name='traderItemInfo']")
foreach($id in @('sellBackpackHeading','sellBackpackTotal','sellBackpackAll','sellStashCoin')){
 $node=$trader.SelectSingleNode("//*[@name='$id']")
 if(!$node -or $node.ParentNode.GetAttribute('name') -ne 'traderRight'){throw "$id must be independent of selected-item visibility"}
}
if($trader.SelectSingleNode("//*[@name='sellBackpackHeading']").GetAttribute('text') -ne 'SELL STASH TOTAL'){throw 'Incorrect sale-section caption'}
foreach($grid in $trader.SelectNodes("//grid[rebirth_trader_action]")){
 $action=$grid.SelectSingleNode('rebirth_trader_action')
 if([int]$action.GetAttribute('action_width') -gt [int]$grid.GetAttribute('cell_width')){throw 'Trader action exceeds its layout cell'}
}
$input=[IO.File]::ReadAllText((Join-Path $root 'Scripts/UI/RebirthWindowInventoryScope.cs'))
foreach($type in @('XUiC_RebirthTraderWorkspace','XUiC_RebirthQuestTurnInWorkspace','XUiC_RebirthCreativeWorkspace')){
 if(!$input.Contains($type)){throw "$type is missing shared cursor ownership"}
}
'PASS: stash controls independent of selection; SELL STASH TOTAL/cash icon exist; trader actions fit their cells; inventory window cursor coverage includes trader, rewards and Creative.'