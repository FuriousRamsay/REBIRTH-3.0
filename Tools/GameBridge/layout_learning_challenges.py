"""Install compact, larger challenge tiles with variable-height chapter containers."""
from pathlib import Path
from lxml import etree as E
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'Config/XUi_InGame/windows.xml'
text=p.read_text(encoding='utf-8-sig')
old='<grid name="newList" rows="24" cols="1" pos="0,0" width="764" height="2664" cell_width="764" cell_height="111" repeat_content="true" arrangement="vertical" controller="ChallengeGroupList"><challenge_list_entry name="0"/></grid>'
new='<rect name="newList" pos="0,0" width="764" height="3408" controller="RebirthChallengeGroupList, RebirthUtils">'+''.join(f'<challenge_list_entry name="{i}"/>' for i in range(24))+'</rect>'
assert old in text or new in text
text=text.replace(old,new);p.write_text(text,encoding='utf-8-sig')
marker='<!-- Water supply controls used by the introductory learning challenge. -->'
if marker not in text:
    patch='''
<!-- Water supply controls used by the introductory learning challenge. -->
<setattribute xpath="//rect[@name='survivorMetabolismPanel']/sprite[@sprite='rb_metabolism_digestive_tract']" name="height">350</setattribute>
<setattribute xpath="//rect[@name='survivorMetabolismPanel']/sprite[@sprite='rb_metabolism_digestive_tract']" name="pos">695,-365</setattribute>
<append xpath="//rect[@name='survivorMetabolismPanel']">
  <rect name="rebirthWaterSupply" pos="680,-66" width="310" height="260">
    <sprite pos="-10,10" width="330" height="280" sprite="menu_empty2px" type="sliced" fillcenter="false" color="65,65,74,255" depth="1"/>
    <label pos="0,0" width="310" height="28" text_key="rebirthWaterSupplyTitle" font_size="22" color="181,140,255,255"/>
    <label pos="0,-30" width="310" height="26" text_key="rebirthWaterSupplyHint" font_size="16" overflow="shrinkcontent"/>
    <label pos="0,-58" width="310" height="28" text="{rbmet_slot_name}" font_size="18" overflow="shrinkcontent"/>
    <label pos="0,-84" width="310" height="26" text="{rbmet_slot_volume}" font_size="18"/>
    <labeledbutton name="btnRebirthMetabolismAutoSip" pos="0,-152" width="310" height="32" caption="{rbmet_autosip}" font_size="18"/>
    <rect name="rebirthWaterFeedbackReader" controller="RebirthReadableText, RebirthUtils" pos="0,-190" width="310" height="62" on_scroll="true" gamepad_selectable="true" snap="false" use_selection_box="true">
      <defaultscrollbar/>
      <scrollview name="readableTextViewport" depth="10" width="288" height="62" clippingsoftness="0,4">
        <label name="rebirthWaterFeedback" pos="0,0" width="282" height="62" text="{rbmet_feedback}" font_size="20" overflow="resizeheight" support_bb_code="true"/>
      </scrollview>
    </rect>
  </rect>
</append>
'''
    text=text.replace('</configs>',patch+'</configs>');p.write_text(text,encoding='utf-8-sig')
p=ROOT/'Config/XUi_InGame/templates.xml';text=p.read_text(encoding='utf-8-sig')
begin='<!-- REBIRTH learning tiles: 84px artwork and 6px between frames. -->'
if begin in text:text=text[:text.index(begin)]+'</configs>\n'
block=E.Element('conditional');body=E.SubElement(block,'if',cond="character_progression('Rebirth')")
def set_(path,name,value):E.SubElement(body,'setattribute',xpath=path,name=name).text=str(value)
base='/templates/challenge_entry/rect'
for path,size in [(base,86),(base+"/sprite[@name='backgroundMain']",88),(base+"/sprite[@name='highlightOverlay']",82),(base+"/sprite[@name='background']",82),(base+"/sprite[@name='itemIcon']",80)]:
    set_(path,'width',size);set_(path,'height',size)
set_(base+"/sprite[@name='itemIcon']",'pos','43,-43')
set_(base+'/rect','width',88);set_(base+'/rect','pos','-1,-86')
grid="/templates/challenge_list_entry/rect/grid[@name='newList']"
for name,value in [('rows',2),('cols',8),('pos','10,-44'),('width',744),('height',188),('cell_width',94),('cell_height',94)]:set_(grid,name,value)
set_("/templates/challenge_list_entry/rect/label[@text='{groupreward}']",'font_size',18)
text=text.replace('</configs>',begin+'\n'+E.tostring(block,encoding='unicode',pretty_print=True)+'</configs>')
p.write_text(text,encoding='utf-8-sig')
print('Larger tiles installed; chapter height follows occupied rows.')
