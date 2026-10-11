$ErrorActionPreference='Stop'
[xml]$base=Get-Content '../../Data/Config/XUi_InGame/windows.xml' -Raw
[xml]$mod=Get-Content 'Config/XUi_InGame/windows.xml' -Raw
[xml]$cook=Get-Content 'Config/_Cooking/workspace_windows.xml' -Raw
$doc=New-Object xml
$doc.LoadXml('<configs><append xpath="/windows"/></configs>')
$out=$doc.configs.append
function CloneNode($node){$doc.ImportNode($node,$true)}
function AddXml($parent,$xml){$f=$doc.CreateDocumentFragment();$f.InnerXml=$xml;[void]$parent.AppendChild($f)}
function Rect($name,$x,$y,$w,$h){$n=$doc.CreateElement('rect');$n.SetAttribute('name',$name);$n.SetAttribute('pos',"$x,$y");$n.SetAttribute('width',"$w");$n.SetAttribute('height',"$h");return $n}
function Skin($n){foreach($s in @($n.SelectNodes('.//sprite[@color="[mediumGrey]" or @color="[darkGrey]"]'))){$s.SetAttribute('color','16,16,20,220')};foreach($bg in @($n.SelectNodes('.//headerbg'))){[void]$bg.ParentNode.RemoveChild($bg)};foreach($label in @($n.SelectNodes('.//label[starts-with(@style,"header.name")]'))){$label.SetAttribute('font_size','24');$label.SetAttribute('color','181,140,255,255')};$w=$n.GetAttribute('width');$h=$n.GetAttribute('height');if(!$w){$w='1'};if(!$h){$h='1'};AddXml $n ('<sprite depth="0" pos="0,0" width="'+$w+'" height="'+$h+'" sprite="menu_empty" type="sliced" color="16,16,20,220"/><sprite depth="2" pos="0,-38" width="'+$w+'" height="2" sprite="menu_empty" color="181,140,255,220"/>')}
function Native($name,$x,$y){$n=CloneNode $base.SelectSingleNode("/windows/window[@name='$name']");$r=$doc.CreateElement('rect');foreach($a in $n.Attributes){$r.SetAttribute($a.Name,$a.Value)};$r.InnerXml=$n.InnerXml;$n=$r;$n.RemoveAttribute('panel');$n.RemoveAttribute('anchor');$n.RemoveAttribute('style');$n.SetAttribute('pos',"$x,$y");Skin $n;return $n}
# One authored workspace for all native station types. The two variants only add real station controls.
foreach($variant in 'Basic','Fuel','Forge'){
 $root=$doc.CreateElement('window');$root.SetAttribute('name',"rebirthStationRoot$variant");$root.SetAttribute('width','1872');$root.SetAttribute('height','813');$root.SetAttribute('pos','-936,435');$root.SetAttribute('anchor','Center');$root.SetAttribute('controller','RebirthStationWorkspaceLayout, RebirthUtils');$root.SetAttribute('cursor_area','true')
 $recipes=Rect 'windowCraftingList' 0 0 430 805;$recipes.SetAttribute('controller','CraftingListInfo')
 foreach($n in $mod.SelectSingleNode('//append[@xpath="/windows/window[@name=''windowCraftingList'']"]' ).ChildNodes){if($n.NodeType -eq 'Element'){[void]$recipes.AppendChild((CloneNode $n))}}
 $recipes.SetAttribute('pos','0,0');$recipes.SetAttribute('width','394');$recipes.SetAttribute('height','813');$recipes.SetAttribute('scale','0.91')
 $pager=$recipes.SelectSingleNode('.//pager');$pager.SetAttribute('visible','false');$pager.SetAttribute('hotkeys_enabled','false')
 $scroll=CloneNode $mod.SelectSingleNode('//rect[@name="rebirthCraftingPagerScroll"]');[void]$recipes.SelectSingleNode('rect[@name="content"]').AppendChild($scroll)
 [void]$root.AppendChild($recipes)
 $info=Rect 'craftingInfoPanel' 790 0 650 658;$info.SetAttribute('controller','RebirthCraftingInfoWindow, RebirthUtils')
 foreach($n in $mod.SelectSingleNode('//append[@xpath="/windows/window[@name=''craftingInfoPanel'']" and rect[@name="contentCraftingInfo"]]').ChildNodes){if($n.NodeType -eq 'Element'){[void]$info.AppendChild((CloneNode $n))}}
 $info.SetAttribute('scale','0.88');$content=$info.SelectSingleNode('rect[@name="contentCraftingInfo"]');$content.SetAttribute('height','610')
 # Ingredients occupy their own column, while selection/count/actions retain native ownership.
 $ingredients=$content.SelectSingleNode('rect[@name="ingredients"]');$ingredients.SetAttribute('pos','-434,0');$ingredients.SetAttribute('width','420');$ingredients.SetAttribute('height','608');$ingredients.RemoveAttribute('visible')
 $ig=$ingredients.SelectSingleNode('grid');$ig.SetAttribute('rows','6');$ig.SetAttribute('cols','1');$ig.SetAttribute('width','410');$ig.SetAttribute('height','420');$ig.SetAttribute('cell_width','410');$ig.SetAttribute('cell_height','70');$ig.SetAttribute('arrangement','vertical')
 foreach($s in $ingredients.SelectNodes('sprite')){$s.SetAttribute('width','420');$s.SetAttribute('height','608');$s.SetAttribute('color','16,16,20,220')}
 $summary=$content.SelectSingleNode('rect[@name="recipeSummary"]');$summary.SetAttribute('height','310')
 $description=$summary.SelectSingleNode('rect[@name="summaryDescription"]');$description.SetAttribute('height','288');$old=$description.SelectSingleNode('label[@name="summaryDescriptionLabel"]');[void]$description.RemoveChild($old)
 AddXml $description '<rect name="stationRecipeDescription" pos="0,-24" width="270" height="252" controller="RebirthReadableText, RebirthUtils" on_scroll="true"><defaultscrollbar pos="250,0" barheight="252"/><scrollview name="readableTextViewport" width="246" height="252"><label name="summaryDescriptionLabel" text="{itemdescription}" font_size="24" width="240" height="252" color="235,235,240,255" overflow="resizeheight" parse_actions="true"/></scrollview></rect>'
 $content.SelectSingleNode('rect[@name="actionBand"]').SetAttribute('pos','8,-330')
 foreach($n in $info.SelectNodes('sprite')){$n.SetAttribute('height','658')}
 [void]$root.AppendChild($info)
 $details=Native 'itemInfoPanel' 790 0;$details.SetAttribute('name','stationItemInfo');$details.SetAttribute('scale','0.96');$details.SetAttribute('visible','false');[void]$root.AppendChild($details)
 $empty=Rect 'stationEmpty' 790 0 570 580;$empty.SetAttribute('controller','InfoWindow');AddXml $empty '<sprite width="570" height="580" sprite="menu_empty" color="16,16,20,220"/><label pos="14,-8" width="542" height="30" font_size="24" color="181,140,255,255" text_key="xuiRebirthSelectedRecipe"/><sprite pos="0,-38" width="570" height="2" sprite="menu_empty" color="181,140,255,220"/>';[void]$root.AppendChild($empty)
 $station=Rect 'station' 1364 0 500 218
 AddXml $station '<sprite width="500" height="218" sprite="menu_empty" color="16,16,20,220"/><sprite pos="0,-38" width="500" height="2" sprite="menu_empty" color="181,140,255,220"/><label name="stationName" pos="14,-8" width="472" height="30" font_size="24" color="235,235,240,255" text=""/>'
 if($variant -ne 'Basic'){$fuel=Native 'windowFuel' 252 -42;[void]$station.AppendChild($fuel)}
 if($variant -eq 'Forge'){$tools=Native 'windowToolsForge' 12 -42;[void]$station.AppendChild($tools)}
 [void]$root.AppendChild($station)
 $output=Native 'windowOutput' 1364 -230;$output.SetAttribute('width','500');[void]$root.AppendChild($output)
 if($variant -eq 'Forge'){$input=Native 'windowForgeInput' 1614 -230;[void]$root.AppendChild($input)}
 $queue=Rect 'rebirthCraftingQueueRegion' 1364 -438 500 158
 AddXml $queue '<sprite width="500" height="158" sprite="menu_empty" color="16,16,20,220"/><label pos="14,-8" width="472" height="28" font_size="22" color="181,140,255,255" text_key="xuiCraftingQueue"/><sprite pos="0,-38" width="500" height="2" sprite="menu_empty" color="181,140,255,220"/><rect name="queueOwner" controller="CraftingQueue" pos="10,-50" always_update="true"><grid name="queue" rows="1" cols="4" cell_width="75" cell_height="75" repeat_content="true" always_update="true"><recipe_stack name="0"/></grid></rect>'
 [void]$root.AppendChild($queue)
 $bag=CloneNode $cook.SelectSingleNode('//window[@name="rebirthCookingRoot"]//rect[@name="rebirthCraftingInventoryRegion"]');$bag.SetAttribute('pos','398,-610');$bag.SetAttribute('height','203')
 foreach($b in $bag.SelectNodes('button')){$x=[int]$b.GetAttribute('pos').Split(',')[0]-398;$b.SetAttribute('pos',"$x,-20")}
 $scroll=$bag.SelectSingleNode('rect[@name="rebirthCraftingInventoryScroll"]');$scroll.SetAttribute('height','150');[void]$root.AppendChild($bag)
 $header=Native 'windowNonPagingHeader' 0 0;$header.SetAttribute('visible','false');[void]$root.AppendChild($header)
 [void]$out.AppendChild($root)
}
$doc.Save((Join-Path (Get-Location) 'Config/XUi_InGame/station_workspace.xml'))
