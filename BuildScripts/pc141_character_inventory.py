"""Scoped Character XML migration; preserve every byte outside the Character window."""
from pathlib import Path
import copy
import xml.etree.ElementTree as ET

path = Path('Config/XUi_InGame/windows.xml')
text = path.read_text(encoding='utf-8-sig')
start = text.index('<window name="rebirthSurvivorCharacterWindow"')
end = text.index('</window>', start) + len('</window>')
root = ET.fromstring(text[start:end])
def find(name):
    return next(e for e in root.iter() if e.get('name') == name)
bag = find('survivorOverviewBackpackPanel')
for child in list(bag):
    if child.get('name') not in ('survivorOverviewConditionsPanelBg', 'survivorOverviewConditionsPanelFrame', 'survivorOverviewConditionsPanelRule', 'survivorOverviewBackpackHeading'):
        bag.remove(child)
find('survivorOverviewBackpackHeading').set('width', '240')
for parent in root.iter():
    for child in list(parent):
        if child.get('name') == 'survivorOverviewEquipmentHint': parent.remove(child)

def element(parent, tag, **attrs):
    return ET.SubElement(parent, tag, {k:str(v) for k,v in attrs.items()})
for name,x,atlas,icon,tip in (
    ('Filter',306,'UIAtlas','ui_game_symbol_shirt','Filter: wearable items'),
    ('Sort',346,'RebirthUiIcons','rb_backpack_sort','Sort inventory'),
    ('Lock',386,'RebirthUiIcons','rb_backpack_lock_toggle','Lock slots')):
    element(bag,'button',name='characterBag'+name,pos=f'{x},-7',width=28,height=28,depth=5,
            atlas=atlas,sprite=icon,style='press, hover',tooltip=tip,
            defaultcolor='255,255,255,255',hovercolor='255,240,180,255',sound='[paging_click]')
element(bag,'label',name='characterBagCapacity',pos='14,-44',width=408,height=26,font_size=20,color='220,214,165,255')
scroll=element(bag,'rect',name='characterBackpackScroll',pos='14,-76',width=408,height=456,
               controller='RebirthCharacterOverviewList, RebirthUtils',on_scroll='true')
viewport=element(scroll,'panel',name='listViewport',width=384,height=456,depth=20,
                 clipping='SoftClip',clippingsize='384,456',clippingcenter='192,-228',clippingsoftness='0,0')
content=element(viewport,'rect',name='listContent',width=380,height=1520)
for row in range(20):
    r=element(content,'rect',name=f'characterBagRow{row}',pos=f'0,{-76*row}',width=380,height=75)
    for col in range(5):
        i=row*5+col
        cell=element(r,'rect',name=f'characterBagCell{i}',pos=f'{col*76},0',width=75,height=75)
        element(cell,'item_stack',name=f'characterBagSlot{i}',controller='RebirthCharacterBackpackSlot, RebirthUtils')
element(scroll,'label',name='listEmpty',pos='8,-12',width=370,height=32,font_size=20,color='220,220,225,255')
for name,x,w,depth in [('listTrack',392,16,49),('listThumb',394,12,50)]:
    element(scroll,'button',name=name,pos=f'{x},0',width=w,height=456,depth=depth,
            sprite='menu_empty2px' if name=='listTrack' else 'menu_empty',type='sliced',
            defaultcolor='65,65,72,255' if name=='listTrack' else '210,30,35,255',
            hovercolor='230,65,65,255',style='press, hover')
element(bag,'sprite',name='characterBagContextBg',pos='14,-546',width=408,height=253,sprite='menu_empty',color='17,17,21,245',depth=1)
element(bag,'sprite',name='characterBagContextRule',pos='14,-546',width=408,height=2,sprite='menu_empty',color='80,80,90,255',depth=2)
element(bag,'sprite',name='characterBagSelectedIcon',pos='14,-558',width=64,height=64,atlas='ItemIconAtlas',sprite='',depth=5)
element(bag,'label',name='characterBagSelected',pos='88,-568',width=334,height=54,font_size=23,color='245,245,248,255')
full=ET.fromstring(text)
template=next(e for e in full.iter() if e.get('name')=='rebirthCraftingItemAction0')
actions=element(bag,'rect',name='characterBagActions',pos='14,-638',width=408,height=154,controller='ItemActionList')
for i in range(8):
    entry=copy.deepcopy(template)
    entry.set('name',f'characterBagAction{i}')
    entry.set('pos',f'{(i%2)*208},{-(i//2)*40}')
    entry.set('width','200')
    for c in entry:
        if c.get('name') in ('rebirthActionOpaqueFill','background','name'): c.set('width','200')
        if c.get('name')=='keyboardButton': c.set('pos','162,-10')
        if c.get('name')=='gamepadIcon': c.set('pos','186,-6')
        if c.get('name')=='name':
            c.set('pos','30,-10'); c.set('width','132'); c.set('font_size','18')
    actions.append(entry)
ET.indent(root, space='  ')
path.write_text(text[:start]+ET.tostring(root,encoding='unicode')+text[end:],encoding='utf-8')
print('Character backpack: 100 physical slot presenters, five columns, native actions, Filter/Sort/Lock.')
