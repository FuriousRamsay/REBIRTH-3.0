"""Reuse the authored Sell Stash panels without its sale transfer controller."""
import copy
import xml.etree.ElementTree as E
from pathlib import Path

root = Path(__file__).resolve().parents[2]
source = E.parse(root / 'Config/XUi_InGame/windows.xml').getroot()
sale = next(n for n in source.iter('window') if n.get('name') == 'rebirthBackpackSellStashRoot')
sale_body = next(n for n in sale.iter('rect') if n.get('name') == 'sellBody')
target = E.parse(root / 'Config/_Vehicles/camper_modules_windows.xml')
window = target.getroot().find('.//window')
slots = [copy.deepcopy(next(n for n in window.iter('rect') if n.get('name') == f'camperModule{i}')) for i in range(10)]
window.clear()
window.attrib.update(name='rebirthCamperModulesWindow', controller='RebirthCamperModules, RebirthUtils', depth='80', width='1856', height='813', anchor='Center', pos='-928,406', cursor_area='true')
body = E.SubElement(window, 'rect', name='camperBody', width='1856', height='813')
left = E.SubElement(body, 'rect', name='camperInstallationPanel', width='922', height='813')
E.SubElement(left,'sprite',sprite='menu_empty2px',type='sliced',width='922',height='813',color='17,17,21,250',depth='0')
E.SubElement(left,'label',name='camperManagementTitle',pos='14,-10',width='894',height='32',font_size='25',color='181,140,255,255',depth='6',upper_case='true')
E.SubElement(left,'sprite',sprite='menu_empty',pos='0,-48',width='922',height='2',color='181,140,255,255',depth='3')
E.SubElement(left,'label',name='camperModuleInstructions',pos='24,-68',width='874',height='56',font_size='24',text_key='xuiRebirthCamperModuleInstructions',depth='6')
E.SubElement(left,'label',name='camperInstallationSummary',pos='24,-644',width='874',height='72',font_size='25',color='181,140,255,255',depth='6')
for i, slot in enumerate(slots):
    slot.set('pos',f'{24 + (i % 5)*176},{-160 - (i//5)*224}')
    slot.set('width','170'); slot.set('height','170')
    background=slot.find("sprite[@name='background']")
    background.set('width','170');background.set('height','170');background.set('color','58,58,65,255')
    icon=slot.find("sprite[@name='itemIcon']")
    icon.set('pos','85,-85');icon.set('width','142');icon.set('height','142')
    left.append(slot)
    E.SubElement(left,'label',pos=f'{24+(i%5)*176},{-336-(i//5)*224}',width='170',height='28',font_size='21',text=str(i+1),justify='center',color='181,140,255,255',depth='6')
selected=copy.deepcopy(next(n for n in sale_body if n.get('name')=='sellSelectedPanel'))
for node in list(selected):
    if node.get('name') in ('sellNativeActions','rebirthSelectedHeaderSalePrice'): selected.remove(node)
for node in selected.iter():
    if node.get('name','').startswith('sell'):node.set('name',node.get('name').replace('sell','camper',1))
body.append(selected)
bag=copy.deepcopy(next(n for n in sale_body if n.get('name')=='sellBackpackPanel'))
body.append(bag)
E.SubElement(body,'sprite',sprite='menu_empty2px',type='sliced',fillcenter='false',width='1856',height='813',color='181,140,255,255',depth='30',on_press='false',on_hover='false')
E.indent(target,space='  ')
target.write(root/'Config/_Vehicles/camper_modules_windows.xml',encoding='unicode')
groups=E.parse(root/'Config/_Vehicles/camper_modules_xui.xml')
for group in groups.getroot().iter('window_group'):
    if group.get('name') in ('rebirthCamperStorageManagement','rebirthCamperStationManagement'):
        group.attrib.pop('open_backpack_on_open',None)
        group.attrib.pop('defaultselected',None)
E.indent(groups,space='  ')
groups.write(root/'Config/_Vehicles/camper_modules_xui.xml',encoding='unicode')
