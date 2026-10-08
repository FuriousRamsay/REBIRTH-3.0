from pathlib import Path
from copy import deepcopy
from lxml import etree as E
exec(Path('BuildScripts/validate_pc181_ui.py').read_text().split('w = resolve')[0])
w=resolve('Config/XUi_InGame/windows.xml','../../Data/Config/XUi_InGame/windows.xml')
p=Path('Config/XUi_InGame/windows.xml')
cond=E.Element('conditional'); block=E.SubElement(cond,'if',cond="character_progression('Rebirth')")
popup=w.xpath('//*[@name="characterStatsPopup"]')[0]
for name in ('rebirthPersonalCraftingRoot','rebirthModifyEditor','rebirthCosmeticsEditor'):
    op=E.SubElement(block,'append',xpath=f"/windows/window[@name='{name}']")
    op.append(deepcopy(popup))
# Native grid retains its layout and event handling, with Exit last.
buttons=deepcopy(w.xpath('/windows/window[@name="ingameMenu"]//*[@name="buttons"]')[0])
exit=buttons.xpath('./*[@name="btnExit"]')[0]; buttons.remove(exit); buttons.append(exit)
E.SubElement(block,'remove',xpath="/windows/window[@name='ingameMenu']//*[@name='buttons']")
E.SubElement(block,'append',xpath="/windows/window[@name='ingameMenu']").append(buttons)
debug=deepcopy(E.parse('../../Data/Config/XUi_InGame/windows.xml').xpath('/windows/window[@name="ingameDebugMenu"]')[0])
debug.set('controller','RebirthDebugMenu, RebirthUtils'); debug.set('height','960')
content=debug.find('rect'); content.set('pos','0,-42'); content.set('width','300')
for bg in debug.xpath('.//boxbg'):
    bg.set('backcolor','18,18,24,245'); bg.set('bordercolor','65,65,74,255'); bg.set('bordersprite','menu_empty2px')
for label in debug.xpath('.//label'):
    label.set('font_size','20'); label.set('color','196,158,255,255')
for btn in debug.xpath('.//simplebutton'):
    for key,val in dict(font_size='20',defaultcolor='12,12,16,255',bordercolor='95,95,108,255',hovercolor='110,100,65,255').items():btn.set(key,val)
for btn in debug.xpath('.//togglebutton'):
    btn.tag='rebirth_debug_toggle'; btn.set('font_size','20')
for cb in debug.xpath('.//combobox'):cb.set('font_size','20')
# Tighten the clock and reserve distinct section headers.
clock=debug.xpath('.//rect[@controller="InGameTimeControls"]')[0]; clock.set('height','240')
for label in clock.xpath('./table/label'):label.set('height','20')
outer=content.find('table')
suicide=outer.xpath('./simplebutton[@name="btnSuicide"]')[0];outer.remove(suicide);outer.append(suicide)
E.SubElement(debug,'sprite',name='rebirthDebugHeader',pos='0,0',width='300',height='42',sprite='menu_empty',color='12,12,16,255',depth='3')
E.SubElement(debug,'label',name='rebirthDebugTitle',pos='12,-8',width='276',height='28',text='WORLD CONTROLS',font_size='22',color='196,158,255,255',depth='4')
E.SubElement(debug,'sprite',pos='0,-40',width='300',height='2',sprite='menu_empty',color='174,22,27,255',depth='4')
E.SubElement(block,'remove',xpath="/windows/window[@name='ingameDebugMenu']")
E.SubElement(block,'append',xpath='/windows').append(debug)
s=p.read_text(encoding='utf-8-sig');p.write_text(s.replace('</configs>',E.tostring(cond,encoding='unicode')+'</configs>'),encoding='utf-8')
# A dedicated toggle template prevents changes leaking into vanilla progression.
p=Path('Config/XUi_InGame/templates.xml'); s=p.read_text(encoding='utf-8-sig')
toggle=deepcopy(E.parse('../../Data/Config/XUi_Common/templates.xml').xpath('/templates/togglebutton')[0]);toggle.tag='rebirth_debug_toggle'
for btn in toggle.xpath('.//button'):
    btn.set('defaultcolor','12,12,16,255');btn.set('hovercolor','110,100,65,255')
for border in toggle.xpath('.//sprite[@name="border"]'):border.set('color','65,65,74,255')
cond=E.Element('conditional'); block=E.SubElement(cond,'if',cond="character_progression('Rebirth')"); E.SubElement(block,'append',xpath='/templates').append(toggle)
for name in ('durability','durabilityBackground'):
    for attr,val in [('pos','3,-51'),('width','56'),('height','10')]:
        E.SubElement(block,'setattribute',xpath=f"/templates/rebirth_item_stack_toolbar/rect/*[@name='{name}']",name=attr).text=val
for attr,val in [('pos','2,-40'),('width','56'),('height','19'),('font_size','15'),('overflow','shrinkcontent')]:
    E.SubElement(block,'setattribute',xpath="/templates/rebirth_item_stack_toolbar/rect/label[@name='stackValue']",name=attr).text=val
p.write_text(s.replace('</configs>',E.tostring(cond,encoding='unicode')+'</configs>'),encoding='utf-8')
