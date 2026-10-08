from pathlib import Path
import xml.etree.ElementTree as E

p=Path('Config/XUi_InGame/windows.xml')
s=p.read_text(encoding='utf-8-sig')
def edit_window(text,name,edit):
    a=text.index('<window name="'+name+'"'); b=text.index('</window>',a)+9
    r=E.fromstring(text[a:b]); edit(r); E.indent(r,space='  ')
    return text[:a]+E.tostring(r,encoding='unicode')+text[b:]
def sub(parent,tag,**attrs): return E.SubElement(parent,tag,{k:str(v) for k,v in attrs.items()})
def character(r):
    n={e.get('name'):e for e in r.iter() if e.get('name')}
    bag=n['survivorOverviewBackpackPanel']
    n['survivorOverviewBackpackHeading'].set('text','SELECTED ITEM')
    n['survivorOverviewBackpackHeading'].attrib.pop('text_key',None)
    n['characterBagSelectedIcon'].set('pos','14,-52')
    name=n['characterBagSelected']; name.set('pos','88,-50'); name.set('depth','6'); name.set('globalopacitymod','0')
    name.set('height','50')
    n['characterBagContextBg'].set('pos','8,-42'); n['characterBagContextBg'].set('width','420'); n['characterBagContextBg'].set('height','292')
    n['characterBagContextRule'].set('pos','0,-342'); n['characterBagContextRule'].set('width','436')
    n['characterBagActions'].set('pos','14,-254')
    # All allowed Character equipment actions fit two rows. Keep the authored pool but
    # let the presenter compact only populated entries; its native actions remain authoritative.
    n['characterBagCapacity'].set('pos','14,-382'); n['characterBagCapacity'].set('depth','6'); n['characterBagCapacity'].set('globalopacitymod','0')
    for k,x in [('Filter',306),('Sort',346),('Lock',386)]:
        n['characterBag'+k].set('pos',f'{x},-348')
    lock=n['characterBagLock'];lock.set('atlas','UIAtlas');lock.set('sprite','ui_game_symbol_lock')
    lock.set('selected','{userlockmode}')
    sub(bag,'label',name='characterBagInventoryHeading',pos='14,-347',width=230,height=28,font_size=22,
        color='196,158,255,255',text='BACKPACK',depth=6,globalopacitymod=0)
    sub(bag,'label',name='characterBagSummary',pos='88,-101',width=334,height=28,font_size=19,
        color='220,214,165,255',depth=6,globalopacitymod=0)
    sub(bag,'label',name='characterBagDescription',pos='14,-140',width=408,height=106,font_size=20,
        color='240,240,244,255',depth=6,globalopacitymod=0)
    n['characterBackpackScroll'].set('pos','14,-416');n['characterBackpackScroll'].set('height','380')
    # Only search this grid: the Character also owns other listViewport names.
    for e in n['characterBackpackScroll'].iter():
        if e.get('name')=='listViewport':
            e.set('height','380');e.set('clippingsize','384,380');e.set('clippingcenter','192,-190')
        if e.get('name') in ('listTrack','listThumb'):e.set('height','380')
        if e.tag=='item_stack':
            e.set('repeat_i',e.get('name').removeprefix('characterBagSlot'))
            e.set('cell_size','75')

def crafting(r):
    n={e.get('name'):e for e in r.iter() if e.get('name')}
    context=n['rebirthCraftingItemActionList']
    parent=next(e for e in r.iter() if context in list(e))
    if any(e.get('name')=='rebirthScrapPreview' for e in parent):return
    pop=sub(parent,'panel',name='rebirthScrapPreview',pos='14,-242',width=420,height=178,depth=90,
        controller='RebirthScrapPreview, RebirthUtils',visible='false')
    sub(pop,'sprite',name='previewBg',width=420,height=178,sprite='menu_empty',color='17,17,21,255',depth=1)
    sub(pop,'sprite',width=420,height=178,sprite='menu_empty2px',type='sliced',fillcenter='false',color='135,111,165,255',depth=2)
    sub(pop,'label',pos='12,-8',width=396,height=26,text='SCRAP RESULT',font_size=20,color='196,158,255,255',depth=3)
    for i in range(2):
        row=sub(pop,'rect',name=f'result{i}',pos=f'12,{-40-i*52}',width=396,height=48)
        sub(row,'sprite',name=f'resultIcon{i}',width=44,height=44,atlas='ItemIconAtlas',depth=3,globalopacitymod=0)
        sub(row,'label',name=f'resultName{i}',pos='56,-4',width=272,height=40,font_size=20,color='245,245,248,255',depth=3)
        sub(row,'label',name=f'resultCount{i}',pos='332,-10',width=58,height=28,font_size=22,justify='right',color='220,214,165,255',depth=3)
    sub(pop,'label',name='previewStatus',pos='12,-146',width=396,height=26,font_size=18,color='220,214,165,255',depth=3)

s=edit_window(s,'rebirthSurvivorCharacterWindow',character)
s=edit_window(s,'rebirthPersonalCraftingRoot',crafting)
p.write_text(s,encoding='utf-8')
print('PC142: item details above backpack; native template parameters; native padlock; Crafting scrap popup.')
