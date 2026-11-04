"""Offline native-control-preserving authoring for standalone bank windows."""
from pathlib import Path
from copy import deepcopy
from lxml import etree as E
def attributes(node, **values): node.attrib.update(values)
root=Path(__file__).resolve().parents[1]
native=E.parse(str(root.parents[1]/'Data/Config/XUi_InGame/windows.xml'))
configs=E.Element('configs')
condition=E.SubElement(E.SubElement(configs,'conditional'),'if',cond="character_progression('Rebirth')")
E.SubElement(condition,'remove',xpath="/windows/window[@name='windowPowerSourceStats' or @name='windowPowerSourceSlots']")
append=E.SubElement(condition,'append',xpath='/windows')
stats=deepcopy(native.xpath('/windows/window[@name="windowPowerSourceStats"]')[0])
attributes(stats,width='450',height='360',anchor='Center',cursor_area='true')
stats.attrib.pop('panel',None)
for node in list(stats.iter()):
    if not isinstance(node.tag,str):continue
    if node.tag=='headerbg':
        node.tag='sprite';attributes(node,sprite='menu_empty',width='450',height='43',color='12,12,16,255')
    if node.get('color')=='[darkGrey]':node.set('color','24,24,29,250')
    if node.get('width')=='228':node.set('width','450')
    if node.get('width')=='224':node.set('width','444')
    if node.get('name')=='backgroundMain':node.set('color','83,77,94,255')
    if node.tag=='label':
        node.set('color','245,245,247,255')
        if node.get('style','').startswith('header.name'):node.set('color','181,140,255,255')
    if node.get('name')=='stats':attributes(node,cell_width='420',cell_height='46')
    if node.tag=='label' and node.get('name') in ('lblSpeed','lblNoise','lblProtection'):attributes(node,width='300',font_size='24')
    if node.get('pos','').startswith(('135,','136,')):
        node.set('pos',node.get('pos').replace('135,','355,').replace('136,','356,'))
    if node.get('name')=='btnOn':attributes(node,pos='0,-210',width='450',height='44')
    if node.get('name')=='btnRefuel':attributes(node,pos='0,-258',width='450',height='44')
content=stats.find('rect[@name="content"]');attributes(content,width='450',height='314')
for node in content.findall('sprite'):
    node.set('width','450');node.set('height','314')
for node in stats.xpath('.//button[@name="clickable"]'):
    attributes(node,width='444',defaultcolor='27,25,33,255',hovercolor='83,64,109,255')
append.append(stats)
slots=deepcopy(native.xpath('/windows/window[@name="windowPowerSourceSlots"]')[0])
attributes(slots,width='450',height='437',anchor='Center');slots.attrib.pop('panel',None)
for node in slots.iter():
    if not isinstance(node.tag,str):continue
    if node.tag=='headerbg':node.tag='sprite';attributes(node,sprite='menu_empty',width='450',height='43',color='12,12,16,255')
    if node.tag=='label':node.set('color','181,140,255,255')
    if node.get('name')=='content':attributes(node,width='450',height='391')
    if node.tag=='sprite' and node.get('name') in ('background','backgroundMain'):
        attributes(node,width='450',height='391',color='24,24,29,250' if node.get('name')=='background' else '83,77,94,255')
    if node.tag=='grid':attributes(node,pos='15,-15',width='420',height='280',cell_width='140',cell_height='140',controller='RebirthPowerSourceSlots, RebirthUtils')
    if node.tag=='item_stack':attributes(node,width='128',height='128')
append.append(slots)
workspace=E.SubElement(append,'window',name='rebirthPowerSourceWorkspace',width='1323',height='883',pos='-662,442',anchor='Center',controller='RebirthPowerSourceWorkspace, RebirthUtils',depth='-10',cursor_area='true')
header=E.SubElement(workspace,'rect',name='rebirthBankHeader',width='1323',height='883')
E.SubElement(header,'sprite',name='rebirthBankBackdrop',width='1323',height='883',sprite='menu_empty',color='17,13,24,245',depth='0')
E.SubElement(header,'sprite',width='1323',height='883',sprite='menu_empty2px',type='sliced',fillcenter='false',color='83,77,94,255',depth='1')
E.SubElement(header,'sprite',pos='8,-8',width='1307',height='44',sprite='menu_empty',color='12,12,16,255',depth='2')
E.SubElement(header,'label',name='rebirthBankTitle',pos='22,-15',width='1260',height='32',font_size='28',color='181,140,255,255',depth='3')
E.SubElement(header,'sprite',pos='8,-52',width='1307',height='2',sprite='menu_empty',color='181,140,255,255',depth='3')
destination=root/'Config/XUi_InGame/standalone_bank_windows.xml'
E.ElementTree(configs).write(str(destination),pretty_print=True,encoding='utf-8',xml_declaration=True)
print('Authored standalone bank windows from native controllers:',destination)
