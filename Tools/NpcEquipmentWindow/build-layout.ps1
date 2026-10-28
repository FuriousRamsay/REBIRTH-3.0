$xml = New-Object System.Text.StringBuilder
[void]$xml.AppendLine('<configs>')
[void]$xml.AppendLine('<remove xpath="/windows/window[@name=''rebirthCompanionsWindow'']/labeledglyphbutton[@name=''btnClose'']" />')
[void]$xml.AppendLine('<setattribute xpath="/windows/window[@name=''rebirthCompanionsWindow'']/rect[label[@name=''selectedName'']]" name="name">companionSelectedCard</setattribute>')
[void]$xml.AppendLine('<setattribute xpath="/windows/window[@name=''rebirthCompanionsWindow'']/rect[rect[@name=''commandViewport'']]" name="name">companionCommandCard</setattribute>')
[void]$xml.AppendLine('<setattribute xpath="/windows/window[@name=''rebirthCompanionsWindow'']/headerbox" name="textcolor">181,140,255,255</setattribute><setattribute xpath="/windows/window[@name=''rebirthCompanionsWindow'']/headerbox" name="bordercolor">181,140,255,255</setattribute>')
foreach($row in @(@('Overview',368),@('Inventory',520),@('Stats',824),@('Behavior',976))){[void]$xml.AppendLine('<setattribute xpath="/windows/window[@name=''rebirthCompanionsWindow'']/button[@name=''btnTab'+$row[0]+''']" name="pos">'+$row[1]+',-7</setattribute>')}
[void]$xml.AppendLine('<setattribute xpath="/windows/window[@name=''rebirthCompanionsWindow'']/rect[rect[@name=''companionViewport'']]" name="name">companionRosterCard</setattribute>')
[void]$xml.AppendLine('<append xpath="/windows/window[@name=''rebirthCompanionsWindow'']">')
[void]$xml.AppendLine('<button name="btnTabEquipment" depth="6" pos="672,-7" width="145" height="38" sprite="menu_empty" hoversprite="menu_empty" selectedsprite="menu_empty" type="sliced" defaultcolor="28,28,28,255" hovercolor="48,48,48,255" selectedcolor="110,18,20,255"><label width="145" height="30" justify="center" font_size="20" text_key="xuiRbNpcEquipmentTab" pivot="center" pos="72,-19" /></button>')
[void]$xml.AppendLine('<rect name="panelEquipment" controller="RebirthHumanEquipment, RebirthUtils" pos="368,-68" width="1470" height="850" depth="12" visible="false"><label name="npcEquipName" pos="0,0" width="1400" height="38" font_size="26" />')
function Panel($name,$pos,$width,$height,$key){[void]$xml.AppendLine('<rect name="'+$name+'" pos="'+$pos+'" width="'+$width+'" height="'+$height+'"><boxbg backcolor="16,16,20,255" bordersprite="menu_empty2px" bordercolor="65,65,74,255" /><label pos="12,-8" width="'+($width-24)+'" height="32" font_size="23" text_key="'+$key+'" color="181,140,255,255" /><sprite pos="12,-44" width="'+($width-24)+'" height="2" sprite="menu_empty" color="181,140,255,255" />')}
function Gear($index,$x,$y,$size){[void]$xml.AppendLine('<rect name="npcEquipSlot'+$index+'" pos="'+$x+','+$y+'" width="'+$size+'" height="'+$size+'" on_press="true" on_hover="true" on_drag="true"><sprite width="'+$size+'" height="'+$size+'" sprite="menu_empty2px" type="sliced" color="65,65,74,255" /><sprite name="itemIcon" pos="3,-3" width="'+($size-6)+'" height="'+($size-6)+'" atlas="ItemIconAtlas" sprite="" depth="2" /><sprite name="condition" pos="3,-'+($size-8)+'" width="'+($size-6)+'" height="5" sprite="menu_empty" color="145,198,100,255" depth="4" /><label name="count" pos="3,-'+($size-26)+'" width="'+($size-8)+'" height="22" justify="right" font_size="19" depth="3" /></rect>')}
Panel 'npcEquipCharacter' '0,-48' 430 744 'xuiRbNpcEquipmentAppearance'
[void]$xml.AppendLine('<texture name="npcEquipModel" pos="112,-58" width="302" height="676" depth="3" keepsourceaspectratio="true" />')
for($i=0;$i -lt 4;$i++){[void]$xml.AppendLine('<label pos="12,-'+(56+$i*168)+'" width="96" height="24" font_size="20" text_key="xuiRbNpcEquipment'+@('Head','Chest','Hands','Feet')[$i]+'" />');Gear $i 12 (-86-$i*168) 96}
[void]$xml.AppendLine('</rect>')
Panel 'npcEquipLoadout' '442,-48' 298 744 'xuiRbNpcEquipmentLoadout'
[void]$xml.AppendLine('<label pos="12,-56" width="270" height="24" text_key="xuiRbNpcEquipmentMelee" font_size="20" />');Gear 4 12 -88 96
[void]$xml.AppendLine('<label name="npcEquipWeaponName4" pos="120,-100" width="166" height="90" font_size="21" overflow="clampcontent" />')
[void]$xml.AppendLine('<label pos="12,-208" width="270" height="24" text_key="xuiRbNpcEquipmentRanged" font_size="20" />');Gear 5 12 -240 96
[void]$xml.AppendLine('<label name="npcEquipWeaponName5" pos="120,-244" width="166" height="70" font_size="21" overflow="clampcontent" /><label name="npcEquipWeaponStatus" pos="120,-320" width="166" height="44" font_size="18" color="181,140,255,255" />')
[void]$xml.AppendLine('<label pos="12,-376" width="270" height="24" text_key="xuiRbNpcEquipmentAmmo" font_size="20" />')
for($i=0;$i -lt 3;$i++){Gear (6+$i) (12+$i*92) -412 86}
[void]$xml.AppendLine('<label pos="12,-540" width="270" height="24" text_key="xuiRbNpcEquipmentBackpack" font_size="20" />');Gear 9 12 -576 110
[void]$xml.AppendLine('<label pos="134,-580" width="150" height="100" text_key="xuiRbNpcEquipmentBackpackHint" font_size="19" overflow="resizeheight" /></rect>')
Panel 'npcEquipStorage' '0,-48' 740 744 'xuiRbNpcEquipmentCompanionStorage'
[void]$xml.AppendLine('<rect name="npcEquipStorageGrid" pos="14,-58" width="710" height="630" on_scroll="true">')
for($i=0;$i -lt 52;$i++){[void]$xml.AppendLine('<rect name="npcEquipCargo'+$i+'" pos="'+(($i%13)*54)+','+(-[int][Math]::Floor($i/13)*64)+'" width="51" height="61" on_press="true" on_hover="true" on_drag="true"><sprite width="51" height="51" sprite="menu_empty2px" type="sliced" color="65,65,74,255" /><sprite name="itemIcon" pos="2,-2" width="47" height="47" atlas="ItemIconAtlas" sprite="" depth="2" /><label name="count" pos="2,-28" width="46" height="22" font_size="17" justify="right" depth="3" /></rect>')}
[void]$xml.AppendLine('</rect><label name="npcEquipNpcStorageCapacity" pos="14,-700" width="710" height="28" font_size="20" /></rect>')
Panel 'npcEquipSelected' '752,-48' 718 344 'xuiRbNpcEquipmentSelected'
[void]$xml.AppendLine('<sprite name="npcEquipInspectIcon" pos="14,-62" width="96" height="96" atlas="ItemIconAtlas" sprite="" /><label name="npcEquipInspectName" pos="124,-60" width="575" height="38" font_size="25" overflow="shrinkcontent" /><label name="npcEquipInspectDescription" pos="124,-106" width="575" height="94" font_size="20" overflow="clampcontent" /><label name="npcEquipInspectStats" pos="14,-204" width="684" height="70" font_size="20" overflow="clampcontent" /><labeledglyphbutton name="npcEquipApply" pos="14,-292" width="220" height="38" caption_key="xuiRbNpcEquipmentEquip" font_size="19" /><labeledglyphbutton name="npcEquipRemove" pos="246,-292" width="220" height="38" caption_key="xuiRbNpcEquipmentUnequip" font_size="19" /><labeledglyphbutton name="npcEquipModify" pos="478,-292" width="220" height="38" caption_key="lblContextActionModify" font_size="19" /></rect>')
Panel 'npcEquipInventory' '752,-404' 718 388 'xuiRbNpcEquipmentYourInventory'
[void]$xml.AppendLine('<button name="npcEquipSort" pos="668,-8" width="30" height="30" atlas="RebirthUiIcons" sprite="rb_backpack_sort" style="press, hover" tooltip_key="lblSortContainer" /><rect name="npcEquipPlayerGrid" pos="14,-58" width="690" height="280" on_scroll="true">')
for($i=0;$i -lt 52;$i++){[void]$xml.AppendLine('<rect pos="'+(($i%13)*53)+','+(-[int][Math]::Floor($i/13)*63)+'" width="51" height="61"><item_stack name="npcEquipPlayer'+$i+'" repeat_i="'+$i+'" controller="RebirthHumanEquipmentPlayerSlot, RebirthUtils" cell_size="51" on_scroll="true" on_press="true" on_hover="true" on_drag="true" background_color="58,58,65,255" /></rect>')}
[void]$xml.AppendLine('</rect><label name="npcEquipCapacity" pos="14,-350" width="690" height="25" font_size="19" /></rect><label name="npcEquipFeedback" pos="0,-806" width="1470" height="38" font_size="19" color="205,205,215,255" /></rect></append></configs>')
$xml.ToString() | Set-Content 'Config/_NPC/equipment_windows.xml'








