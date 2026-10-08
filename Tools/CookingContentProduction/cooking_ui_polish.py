"""Shared review refinements for generated cooking layouts (also applies to existing XML)."""
import xml.etree.ElementTree as E

def node(root,name):
    return next(e for e in root.iter() if e.get('name')==name)

def bounds(e,x,y,w,h):
    e.attrib.update(pos=f'{x},{-y}',width=str(w),height=str(h))

def label(parent,name,text,x,y,w,h=26,size=18,color='235,235,240,255'):
    return E.SubElement(parent,'label',name=name,text=text,pos=f'{x},{-y}',width=str(w),height=str(h),font_size=str(size),color=color,depth='18',justify='left',overflow='shrinkcontent')

def action(e,icon=None,caption=None,shortcut=None):
    w=int(e.get('width'));h=int(e.get('height'));depth=int(e.get('depth','16'))
    if caption is None:
        caption=e.get('caption') or next((n.get('text','') for n in e if n.get('name')=='label'),'')
    if icon is None:
        icon=next((n.get('sprite') for n in e if n.get('name','').endswith('ActionIcon')),'ui_game_symbol_book')
    for child in list(e):e.remove(child)
    e.tag='rect'
    for attr in ('caption','font_size'):e.attrib.pop(attr,None)
    e.attrib.update(controller='RebirthCookingAction, RebirthUtils',on_press='true',on_hover='true')
    E.SubElement(e,'sprite',name='actionFill',width=str(w),height=str(h),sprite='menu_empty',color='17,17,21,255',depth=str(depth),type='sliced')
    E.SubElement(e,'sprite',name='actionFrame',width=str(w),height=str(h),sprite='menu_empty2px',color='240,240,244,255',depth=str(depth+1),type='sliced',fillcenter='false')
    E.SubElement(e,'sprite',name='actionIcon',pos=f'10,{-int((h-20)/2)}',width='20',height='20',sprite=icon,depth=str(depth+2),foregroundlayer='true')
    text=label(e,'label',caption,34,h/2,w-68,24,17)
    text.set('pos',f'{w//2},{-h//2}');text.set('pivot','center');text.set('justify','center');text.set('depth',str(depth+3))
    if shortcut:
        text=label(e,shortcut,'',w-32,h//2,24,24,17);text.set('pivot','left');text.set('justify','right');text.set('depth',str(depth+3))

def workspace(w):
    n=lambda name:node(w,name)
    # Hidden output storage must not handle Reload/Take and close the cooking window.
    n('cookingOutput').attrib.pop('controller',None)
    n('filter0').set('sprite','rb_skill_cooking');n('filter0').set('atlas','RebirthSurvivorIcons')
    for i in range(8):
        bounds(n('dishStatus'+str(i)),116,23,210,18);n('dishStatus'+str(i)).set('font_size','15')
        n('dishSkill'+str(i)).set('width','72')
    for i in range(12):n('cookingSlot'+str(i)).set('on_press','true');n('cookingSlot'+str(i)).set('on_drag','true')
    for name in ('cook','prepare','pullIngredients','clearRecipe'):
        action(n(name),shortcut='cookShortcut' if name=='cook' else None)
    for name in ('herbsLabel','toolsTitle','fuelTitle'):n(name).set('font_size','20')
    n('burnTimeLeft').set('justify','right');n('burnTimeLeft').set('font_size','18')
    bounds(n('burnTimeLeft'),330,47,156,26)
    bounds(n('resultIcon'),24,62,160,132)
    bounds(n('mealQuality'),16,215,176,24);n('mealQuality').set('justify','left')
    bounds(n('referenceStatus'),96,313,176,88);n('referenceStatus').set('justify','left');n('referenceStatus').set('font_size','17')
    label(n('result'),'magazineReferenceText','',366,313,182,88,17)
    bounds(n('bookReferenceIcon'),16,314,72,72)
    bounds(n('magazineReferenceIcon'),286,314,72,72)
    n('referenceBenefits').set('visible','false')
    bounds(n('prepStatus'),16,415,534,28);n('prepStatus').set('font_size','16')
    for i in range(12):
        bounds(n('ghost'+str(i)),43,49,64,64)
        bounds(n('sub'+str(i)),57,5,24,22)
        bounds(n('need'+str(i)),3,5,52,21)
        n('sub'+str(i)).set('tooltip','Choose substitute')
    for cell in n('cookingTools').iter('item_stack'):cell.set('controller','RebirthCookingToolSlot, RebirthUtils')
    for cell in n('cookingFuel').iter('item_stack'):cell.set('controller','RebirthCookingFuelSlot, RebirthUtils')
    # Keep the native fuel button/controller contract; only change its visual treatment.
    fuel=n('cookingFuel');button=node(fuel,'button');button.attrib.update(defaultcolor='17,17,21,255',disabledcolor='17,17,21,255',height='34',depth='2')
    parent=next(p for p in fuel.iter() if button in list(p))
    E.SubElement(parent,'sprite',name='fuelActionFrame',width='222',height='34',sprite='menu_empty2px',type='sliced',fillcenter='false',color='240,240,244,255',depth='3')
    flame=node(fuel,'flameIcon');flame.attrib.pop('style',None);bounds(flame,10,7,20,20);flame.set('depth','4')
    text=node(fuel,'onoff');text.attrib.update(pos='111,-17',pivot='center',width='154',height='24',font_size='17',justify='center',depth='4')
    outcome=n('outcome');outcome.remove(n('outcomeIcon'))
    bounds(n('outcomeName'),14,134,352,28);n('outcomeName').set('justify','left')
    bounds(n('outcomeHint'),14,170,352,42);n('outcomeHint').set('justify','left')
    E.SubElement(outcome,'sprite',name='cookingSkillIcon',pos='14,-229',width='30',height='30',sprite='rb_skill_cooking',atlas='RebirthSurvivorIcons',depth='15')
    label(outcome,'cookingSkillValue','',54,233,304,26,20,'181,140,255,255')
    # This workspace accepts one held batch; reserve a distinct guidance panel below it.
    for name in ('rebirthCraftingQueueRegion','rebirthCraftingQueueRegionBg','rebirthCraftingQueueController'):n(name).set('height','360')
    guide=E.SubElement(n('rebirthCraftingQueueRegion'),'rect',name='cookingGuide',pos='10,-68',width='480',height='278')
    E.SubElement(guide,'sprite',name='cookingGuideBg',width='480',height='278',sprite='menu_empty',color='17,17,21,210',depth='1')
    title=label(guide,'cookingGuideTitle','',14,12,452,26,20,'181,140,255,255');title.set('text_key','rbCookingGuideTitle')
    body=label(guide,'cookingGuideBody','',14,48,452,210,19);body.set('text_key','rbCookingGuideBody')
    for key in ('Nutrition','Water','Comfort','Time'):
        for suffix in ('Icon','Label',''):
            element=n('stat'+key+suffix);x,y=map(int,element.get('pos').split(','));element.set('pos',f'{x},{y+34}')
    # Energy is a real consumable attribute; expose it only when the selected dish supplies it.
    for suffix in ('Icon','Label',''):
        element=E.fromstring(E.tostring(n('statTime'+suffix),encoding='unicode'));element.set('name','statEnergy'+suffix)
        element.set('pos',element.get('pos').split(',')[0]+',-179')
        if suffix=='Icon':element.set('sprite','ui_game_symbol_electric_power')
        elif suffix=='Label':element.set('text','Energy')
        else:element.set('text','')
        n('result').append(element)
    import copy
    known=n('known')
    viewport=E.SubElement(known,'panel',name='recipeViewport',pos='12,-124',width='330',height='352',clipping='softclip',clippingsize='330,352',clippingcenter='165,-176',clippingsoftness='0,0',depth='10')
    host=E.SubElement(viewport,'rect',name='recipeRows',pos='0,0',width='330',height='7040')
    template=copy.deepcopy(n('dish0'))
    for i in range(8):known.remove(n('dish'+str(i)))
    for i in range(160):
        row=copy.deepcopy(template);row.set('name','dish'+str(i));row.set('pos',f'0,{-i*44}');row.set('visible','false')
        for child in row:
            if child.get('name','').endswith('0'):child.set('name',child.get('name')[:-1]+str(i))
        host.append(row)
    for id in ('recipeTrack','recipeThumb'):n(id).set('on_press','true');n(id).set('on_drag','true')
    # Reuse the very same popup markup as Character / personal Crafting.
    from pathlib import Path
    import copy
    root=Path(__file__).resolve().parents[2]
    shared=E.parse(root/'Config/XUi_InGame/windows.xml')
    popup=next(e for e in shared.iter() if e.get('controller')=='RebirthCharacterStatsPopup, RebirthUtils')
    w.append(copy.deepcopy(popup))
    button=node(n('cookingFuel'),'button');button.set('hoversprite','menu_empty');button.set('hovercolor','75,75,80,255');button.set('selectedsprite','menu_empty');button.set('selectedcolor','75,75,80,255')

def hud(h):
    for card in h:
        if not card.get('name','').startswith('cookingHud'):continue
        bounds(card,-255,-247,245,60)
        for name in ('background','line'):node(card,name).set('width','245')
        bounds(node(card,'icon'),5,8,42,42)
        for name in ('name','status','time'):
            e=node(card,name);e.set('width','189');e.set('pos','51,'+e.get('pos').split(',')[1]);e.set('overflow','shrinkcontent')

if __name__=='__main__':
    from pathlib import Path
    root=Path(__file__).resolve().parents[2]
    for file,fn in [('workspace_windows.xml',workspace),('hud_windows.xml',hud)]:
        path=root/'Config/_Cooking'/file;t=E.parse(path)
        for w in t.getroot().findall('append/window'):fn(w)
        E.indent(t,space='  ');path.write_text(E.tostring(t.getroot(),encoding='unicode')+'\n',encoding='utf-8')
