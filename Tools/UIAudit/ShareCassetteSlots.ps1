[xml]$d=Get-Content Config/XUi_InGame/windows.xml -Raw
$root=$d.SelectSingleNode('//window[@name="rebirthMusicLibraryRoot"]')
function Display($name,$x,$y,$size){$n=$d.CreateElement('item_stack');$n.SetAttribute('name',$name);$n.SetAttribute('controller','RebirthReadonlyItemSlot, RebirthUtils');$n.SetAttribute('pos',"$x,$y");$n.SetAttribute('display_size',"$size");$n.SetAttribute('depth','15');$n.SetAttribute('on_press','false');$n.SetAttribute('on_hover','false');$n.SetAttribute('on_drag','false');return $n}
for($i=0;$i -lt 24;$i++){
 $button=$root.SelectSingleNode(".//button[@name='library$i']");$cell=$button.ParentNode
 foreach($n in @($cell.SelectNodes("sprite[@name='libraryIcon$i']|label[@name='libraryCount$i']"))){[void]$cell.RemoveChild($n)}
 [void]$cell.AppendChild((Display "libraryDisplay$i" 2 -2 100));$button.SetAttribute('depth','25')
}
$bag=$root.SelectSingleNode('rect[@name="cassetteBackpackPanel"]')
foreach($n in @($bag.SelectNodes('rect[button[starts-with(@name,"cassetteBag")]]'))){[void]$bag.RemoveChild($n)}
for($i=0;$i -lt 60;$i++){
 $x=16+($i%20)*54;$y=-54-[math]::Floor($i/20)*54
 $cell=$d.CreateElement('rect');$cell.SetAttribute('pos',"$x,$y");$cell.SetAttribute('width','52');$cell.SetAttribute('height','52')
 $f=$d.CreateDocumentFragment();$f.InnerXml="<sprite width='52' height='52' sprite='menu_empty' color='58,58,65,255'/><sprite name='cassetteBagSelection$i' width='52' height='52' sprite='menu_empty2px' fillcenter='false' color='255,255,255,255' depth='20' visible='false'/><button name='cassetteBag$i' width='52' height='52' sprite='menu_empty' defaultcolor='0,0,0,1' hovercolor='181,140,255,35' on_drag='true' on_scroll='true' hoverscale='1' depth='25'/>";[void]$cell.AppendChild($f);[void]$cell.AppendChild((Display "cassetteBagDisplay$i" 0 0 52));[void]$bag.AppendChild($cell)
}
$panel=$root.SelectSingleNode('rect[@name="inspectionPanel"]');$n=$panel.SelectSingleNode('sprite[@name="cassettePreview"]');[void]$panel.RemoveChild($n);[void]$panel.AppendChild((Display 'cassetteSelectedDisplay' 80 -62 192))
$n=Display 'cassetteDragDisplay' 0 0 52;$n.SetAttribute('visible','false');$n.SetAttribute('depth','90');[void]$root.AppendChild($n)
$d.Save((Join-Path (Get-Location) 'Config/XUi_InGame/windows.xml'))
