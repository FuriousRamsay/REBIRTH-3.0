"""Build cooking UI and runtime reference coverage from the approved catalogue."""
from pathlib import Path
import copy
import xml.etree.ElementTree as E
from content_model import ROOT, CATALOGUE, ASSETS, recipe, station
from cooking_substitutions import substitutions

def write(path, root):
    E.indent(root, space='  ')
    path.write_text(E.tostring(root, encoding='unicode')+'\n', encoding='utf-8')

runtime = E.Element('cooking', preparation_seconds='10', prepared_seconds='300')
for magazine in CATALOGUE['magazines']:
    if magazine['id'] in ASSETS:
        E.SubElement(runtime, 'technique', item=ASSETS[magazine['id']]['icon'], effect=magazine['effect'], herbs={'M23':'foodCropBasil,foodCropDill','M24':'foodCropSage'}.get(magazine['id'],''))
for key, row in CATALOGUE['recipes'].items():
    r = recipe(key)
    n = E.SubElement(runtime, 'dish', item=r['game_id'], substitutions=row.get('substitutions',''), herbs=row.get('herbs',''))
    # Station-specific preparation applies to existing dishes as well as new ones.
    area, tool = station(key)
    if r.get('source')=='Current Cooking inventory' or r.get('source','').startswith('Native 3.2'): area,tool=r.get('station') or None,r.get('tool') or None
    oven_keys={'berrypie','pumpkinpie','cheesecake','cornbread','pumpkinbread','crumble','potpie','shepherd'}
    if key in oven_keys: area,tool='WorkbenchIronOven001_FR','FuriousRamsayBakingPan'
    if key in {'grilled','corncob','steak'}: area,tool='campfire','toolCookingGrill'
    if key=='charred': area,tool='campfire',None
    if key in {'bacon','skillet','tunatoast'}: area,tool='WorkbenchGasStove001_FR','rebirthCookingFryingPan'
    n.set('station',area or 'cold');n.set('tool',tool or '')
    for kind, prefix in [('books','book'),('magazines','magazine')]:
        for identity in row.get(kind,[]):
            asset = ASSETS.get(identity)
            if asset: E.SubElement(n, prefix, item=asset['icon'])
    for card in CATALOGUE['cards']:
        if card['recipe'] == key:
            asset = ASSETS.get(card['id'])
            if asset: E.SubElement(n,'card',item=asset['icon'])
    for source, choices in substitutions(key).items():
        assert source in {i['id'] for i in r['ingredients']}, (key,source)
        E.SubElement(n,'substitute',ingredient=source,choices=','.join(choices))

# Explicitly authored ingredient roles. No global vegetable swap rule.
write(ROOT/'Config/_Cooking/runtime.xml',runtime)

# Reuse the established Rebirth screen components instead of a parallel UI skin.
existing=E.parse(ROOT/'Config/XUi_InGame/windows.xml').getroot().find(".//window[@name='rebirthPersonalCraftingRoot']")
base=E.parse(ROOT.parents[1]/'Data/Config/XUi_InGame/windows.xml').getroot()
root=E.Element('configs'); app=E.SubElement(root,'append',xpath='/windows')
w=E.SubElement(app,'window',name='rebirthCookingRoot',width='1872',height='900',controller='RebirthCookingWorkspace, RebirthUtils',cursor_area='true',anchor='Center')
def rect(p,name,x,y,width,height): return E.SubElement(p,'rect',name=name,pos=f'{x},{-y}',width=str(width),height=str(height))
def sprite(p,name,x,y,width,height,**kw): return E.SubElement(p,'sprite',name=name,pos=f'{x},{-y}',width=str(width),height=str(height),sprite=kw.pop('sprite','menu_empty'),depth=kw.pop('depth','1'),**kw)
def label(p,name,text,x,y,width=250,height=26,size=20,color='235,235,240,255'):
    return E.SubElement(p,'label',name=name,text=text,pos=f'{x},{-y}',width=str(width),height=str(height),font_size=str(size),color=color,depth='15',overflow='clampcontent')
def button(p,name,text,x,y,width,height=34):
    return E.SubElement(p,'labeledbutton',name=name,caption=text,pos=f'{x},{-y}',width=str(width),height=str(height),font_size='18',depth='16')
def panel(name,title,x,y,width,height):
    p=rect(w,name,x,y,width,height)
    sprite(p,name+'Bg',0,0,width,height,sprite='menu_empty',type='sliced',color='16,16,20,250',globalopacitymod='0')
    sprite(p,name+'Frame',0,0,width,height,sprite='menu_empty2px',type='sliced',fillcenter='false',color='64,64,72,255')
    sprite(p,name+'Line',0,38,width,2,color='228,18,21,220')
    label(p,name+'Title',title,12,8,width-24,color='181,140,255,255')
    return p
def clone(name,parent,x=None,y=None,width=None,height=None):
    e=copy.deepcopy(existing.find(".//*[@name='%s']"%name));parent.append(e)
    if x is not None:e.set('pos',f'{x},{-y}')
    if width is not None:e.set('width',str(width))
    if height is not None:e.set('height',str(height))
    return e
def scrollbar(p,track,thumb,x,y,height):
    for name,xx,ww,col in [(track,x,16,'42,42,48,255'),(thumb,x+2,12,'150,24,27,255')]:
        E.SubElement(p,'button',name=name,pos=f'{xx},{-y}',width=str(ww),height=str(height if name==track else 50),depth='49',sprite='menu_empty',hoversprite='menu_empty',disabledsprite='menu_empty',defaultcolor=col,hovercolor=col,disabledcolor=col,hoverscale='1',on_scroll='true',on_drag='true')
clone('rebirthCraftingRootBackground',w,width=1872,height=900)
clone('rebirthCraftingRootFrame',w,width=1872,height=900)
top=clone('rebirthCraftingTopZone',w)
# The source XML still contains retired tabs which runtime Crafting hides elsewhere.
tabs=top.find("rect[@name='rebirthCraftingTopTabs']")
for e in list(tabs):
    if e.get('name') in ['rebirthCraftingTabInventory','rebirthCraftingTabSkills']:tabs.remove(e)
for e in top.iter():
    if e.get('name')=='rebirthCraftingTabCraftingLabel':e.attrib.pop('text_key',None);e.set('text','COOKING')
    if e.get('name')=='rebirthCraftingTabCraftingIcon':e.set('sprite','ui_game_symbol_fork')
p=panel('known','RECIPES',8,70,380,475)
for i,(icon,tip) in enumerate([('ui_game_symbol_hammer','All cooking'),('ui_game_symbol_fork','Food'),('ui_game_symbol_water','Drinks')]):
    E.SubElement(p,'button',name='filter'+str(i),pos=f'{14+i*46},-46',width='38',height='34',sprite=icon,tooltip=tip,style='press, hover',depth='16')
E.SubElement(p,'textfield',name='cookingSearch',pos='14,-86',width='330',height='30',search_field='true',clear_button='true')
for i in range(8):
    row=rect(p,'dish'+str(i),12,124+i*38,330,36);row.set('on_press','true');row.set('on_scroll','true')
    sprite(row,'dishBg'+str(i),0,0,330,36,sprite='menu_empty2px',color='20,20,24,225')
    sprite(row,'dishIcon'+str(i),4,2,32,32,atlas='ItemIconAtlas',sprite='',depth='12',foregroundlayer='true')
    label(row,'label','',44,7,282,26,18)
scrollbar(p,'recipeTrack','recipeThumb',350,124,304)
button(p,'clearRecipe','CLEAR SELECTION',12,435,332,30)
p=panel('ingredients','INGREDIENTS',398,70,280,480)
label(p,'batchLabel','BATCH',12,49,70);button(p,'batchMinus','−',90,44,30);label(p,'batchCount','1',133,49,40);button(p,'batchPlus','+',174,44,30)
button(p,'pullIngredients','PULL INGREDIENTS',12,86,256,30)
# Explicit native cells; ghost and counts share the same parent coordinates.
for i in range(12):
    x=12+(i%3)*86; y=126+(i//3)*86 if i<9 else 390
    if i>=9:y=410
    s=rect(p,'ingredient'+str(i),x,y,82,82)
    E.SubElement(s,'item_stack',name='cookingSlot'+str(i),controller='RebirthCookingSlot, RebirthUtils',pos='0,0',cell_size='82')
    sprite(s,'ghost'+str(i),41,33,64,56,pivot='center',atlas='ItemIconAtlasGreyscale',sprite='',color='255,255,255,255',depth='13',foregroundlayer='true')
    label(s,'need'+str(i),'',3,61,76,20,16).set('justify','center')
    button(s,'sub'+str(i),'↔',59,3,21,20)
p.set('height','500')
for n in ['ingredientsBg','ingredientsFrame']:p.find("*[@name='%s']"%n).set('height','500')
label(p,'herbsLabel','OPTIONAL HERBS',12,381,256,25,18,'181,140,255,255')
p=panel('result','RESULT',688,70,676,500)
sprite(p,'resultIcon',16,53,112,112,atlas='ItemIconAtlas',sprite='',depth='12',foregroundlayer='true')
label(p,'resultName','Add ingredients',144,53,450,55,24)
label(p,'resultStats','',144,118,450,80,19)
label(p,'referenceTitle','PREPARATION',16,190,620,28,22,'181,140,255,255')
label(p,'referenceStatus','',16,228,638,65,19)
label(p,'prepStatus','',16,303,638,42,20)
sprite(p,'prepTrack',16,355,638,12,color='42,42,48,255')
sprite(p,'prepProgress',16,355,638,12,color='181,140,255,255',type='filled',fill='0')
button(p,'prepare','PREPARE',16,381,638,36)
button(p,'cook','COOK',16,429,638,36)
label(p,'cookStatus','',16,469,638,28,17)
# Inventory retains the standard header, actions, slot renderer, clipping and draggable scrollbar.
b=clone('rebirthCraftingInventoryRegion',w,398,580,966,312)
for e in b:
    if e.get('name') in ['rebirthCraftingInventoryRegionBg','rebirthCraftingInventoryHeaderRule']:e.set('width','966')
    if e.tag=='button' or e.get('name')=='rebirthCraftingInventoryLockActive':
        x,y=map(int,e.get('pos').split(','));e.set('pos',f'{x+236},{y}')
sc=b.find("rect[@name='rebirthCraftingInventoryScroll']");sc.set('width','942');sc.set('height','256')
# Four native workstation queue entries, presented as the same populated cards as Crafting.
q=clone('rebirthCraftingQueueRegion',w,1374,70,490,548)
for e in q:
    if e.get('width')=='414':e.set('width','490')
    if e.get('height')=='682':e.set('height','548')
qc=q.find("rect[@name='rebirthCraftingQueueController']")
host=qc.find("rect[@name='rebirthCraftingQueueRowsHost']")
for e in list(host)[4:]:host.remove(e)
for e in q.iter('label'):
    if e.get('text_key') in ['xuiCraftingQueue','xuiRebirthCraftingQueue'] or 'CRAFTING QUEUE' in e.get('text',''):e.attrib.pop('text_key',None);e.set('text','COOKING QUEUE')
p=panel('station','',8,555,380,337)
sprite(p,'stationIcon',14,6,28,28,sprite='campfire',atlas='ItemIconAtlas',depth='12',foregroundlayer='true')
label(p,'stationName','Campfire',50,8,315,30,24)
label(p,'toolsTitle','TOOLS',14,49,300,25,18,'181,140,255,255')
label(p,'fuelTitle','FUEL',14,169,150,25,18,'181,140,255,255')
label(p,'burnTimeLeft','00:00',200,169,150,25,20)
output=panel('output','OUTPUT',1374,628,490,264)
button(output,'takeOutput','TAKE ALL',310,5,165,28)
for original,name,parent,x,y in [('windowToolsCampfire','cookingTools',p,14,82),('windowFuel','cookingFuel',p,14,203),('windowOutput','cookingOutput',output,14,52)]:
    source=copy.deepcopy(base.find(f"window[@name='{original}']"));source.tag='rect';source.set('name',name);source.set('pos',f'{x},{-y}')
    for attr in ['panel','anchor']:source.attrib.pop(attr,None)
    header=source.find('rect');header.set('visible','false');header.set('pos','-10000,0')
    content=source.find("rect[@name='content']")
    if content is not None:content.set('pos','0,0')
    if original=='windowFuel':
        for e in source.iter():
            if e.get('name')=='buttonContent':e.set('pos','0,-80')
    parent.append(source)
# Approved concept geometry: recipes | ingredients | result/prep | station/queue/output;
# the backpack spans the bottom. Reuse the existing skin and controllers throughout.
def node(name):return w.find(".//*[@name='%s']"%name)
def bounds(name,x,y,width,height):
    e=node(name);e.set('pos',f'{x},{-y}');e.set('width',str(width));e.set('height',str(height));return e
def panel_bounds(name,x,y,width,height):
    bounds(name,x,y,width,height)
    for suffix in ['Bg','Frame']:bounds(name+suffix,0,0,width,height)
    bounds(name+'Line',0,38,width,2)
panel_bounds('known',8,70,380,626)
bounds('clearRecipe',12,578,332,34)
# More visible recipe rows, with the same spacing and standard scrollbar.
known=node('known')
for i in range(8,11):
    row=copy.deepcopy(node('dish0'));row.set('name','dish'+str(i));row.set('pos',f'12,{-124-i*38}')
    for e in row:
        if e.get('name') in ['dishIcon0','dishBg0']:e.set('name',e.get('name')[:-1]+str(i))
    known.append(row)
bounds('recipeTrack',350,124,16,418);bounds('recipeThumb',352,124,12,60)
panel_bounds('ingredients',398,70,446,626)
for i in range(12):
    x=48+(i%3)*116;y=54+(i//3)*116 if i<9 else 436
    bounds('ingredient'+str(i),x,y,112,112 if i<9 else 96)
    cell=node('cookingSlot'+str(i));cell.set('cell_size','112' if i<9 else '96')
    ghost=node('ghost'+str(i));ghost.set('pos','56,-62' if i<9 else '48,-52');ghost.set('width','86' if i<9 else '72');ghost.set('height','76' if i<9 else '62')
    bounds('need'+str(i),4,5,104 if i<9 else 88,24)
    node('need'+str(i)).set('font_size','19')
    bounds('sub'+str(i),86 if i<9 else 70,84 if i<9 else 68,23,22)
bounds('herbsLabel',18,405,410,25)
bounds('batchLabel',20,550,92,26);node('batchLabel').set('text','SERVINGS')
bounds('batchMinus',160,546,34,34);bounds('batchCount',224,551,60,26);bounds('batchPlus',316,546,34,34)
bounds('pullIngredients',18,588,410,30)
panel_bounds('result',854,70,500,626)
bounds('resultIcon',158,48,180,155)
bounds('resultName',16,206,466,32);node('resultName').set('justify','center')
bounds('resultStats',24,248,450,84)
bounds('referenceTitle',16,355,466,28);node('referenceTitle').set('text','PREPARATION · OPTIONAL')
bounds('referenceStatus',72,400,412,58);node('referenceStatus').set('font_size','17')
bounds('prepStatus',16,460,466,42);node('prepStatus').set('font_size','18')
bounds('prepare',16,508,466,32)
bounds('prepTrack',16,550,466,12);bounds('prepProgress',16,550,466,12)
bounds('cook',16,577,466,32);bounds('cookStatus',16,610,466,20)
for kind,y in [('book',398),('magazine',431)]:
    sprite(node('result'),kind+'ReferenceIcon',18,y,32,30,atlas='ItemIconAtlas',sprite='',depth='16',foregroundlayer='true')
panel_bounds('station',1364,70,500,220)
bounds('toolsTitle',14,46,300,25);bounds('cookingTools',14,72,228,80)
node('cookingTools').set('scale','0.65')
bounds('fuelTitle',14,124,120,25);bounds('burnTimeLeft',268,152,205,30)
bounds('cookingFuel',14,151,228,120);node('cookingFuel').set('scale','0.55')
# Explicit native view scaling is applied by the workspace as well; XML scale is not assumed.
bounds('rebirthCraftingQueueRegion',1364,300,500,154)
for name in ['rebirthCraftingQueueRegionBg','rebirthCraftingQueueController']:bounds(name,0,0,500,154)
qbg=node('rebirthCraftingQueueRegionBg');qbg.set('sprite','menu_empty');qbg.set('color','16,16,20,250');qbg.set('globalopacitymod','0')
bounds('rebirthCraftingQueueHeaderRule',0,38,500,2)
panel_bounds('output',1364,464,500,232)
bounds('takeOutput',310,5,175,28);bounds('cookingOutput',120,50,228,150)
node('cookingOutput').set('scale','1.05')
# A wide, two-row backpack uses the same standard scrolling implementation.
bounds('rebirthCraftingInventoryRegion',8,706,1856,186)
bg=node('rebirthCraftingInventoryRegionBg');bg.set('sprite','menu_empty');bg.set('color','16,16,20,250');bg.set('globalopacitymod','0');bounds(bg.get('name'),0,0,1856,186)
bounds('rebirthCraftingInventoryHeaderRule',0,38,1856,2)
for i,name in enumerate(['Sort','Lock','QuickStack','Companions']):bounds('btnRebirthCraftingInventory'+name,1708+i*38,20,28,28)
bounds('rebirthCraftingInventoryLockActive',1731,36,30,3)
bounds('rebirthCraftingInventoryScroll',12,46,1832,136)
inventory=node('rebirthCraftingInventoryRegion').find(".//grid[@name='inventory']")
inventory.set('cols','26');inventory.set('rows','4')
pop=rect(w,'substitutionPopup',550,220,580,400);pop.set('visible','false')
sprite(pop,'popupBg',0,0,580,400,color='12,12,15,255',depth='70')
label(pop,'substitutionTitle','SUBSTITUTIONS',14,12,550,32,22,'181,140,255,255')
label(pop,'substitutionHelp','',14,50,550,65,18)
for i in range(6):button(pop,'choice'+str(i),'',14,120+i*36,550,32)
button(pop,'closeSub','CLOSE',14,354,550,32)
for e in pop:
    if e.get('name')!='popupBg':e.set('depth','73')
# September 16 review: compact workspace, full-size adjacent tools/fuel, and left outcome.
# Individual native slots are outside a repeat grid and must supply repeat_i explicitly.
for i in range(12):node('cookingSlot'+str(i)).set('repeat_i',str(i))
# Keep all copied controller navigation references inside this generated window.
names={e.get('name') for e in w.iter()}
for e in w.iter():
    for key in list(e.attrib):
        if key.startswith('nav_') and e.get(key) not in names:e.attrib.pop(key)
panel_bounds('known',8,70,380,532)
bounds('clearRecipe',12,490,332,30)
for i in range(8,11):node('known').remove(node('dish'+str(i)))
for i in range(8):
    bounds('dish'+str(i),12,124+i*44,330,42)
    bounds('dishBg'+str(i),0,0,330,42)
    bounds('dishIcon'+str(i),4,5,32,32)
    row=node('dish'+str(i));title=row.find("label[@name='label']");title.set('pos','44,-2');title.set('width','195');title.set('font_size','18')
    label(row,'dishSkill'+str(i),'COOKING',44,23,180,18,15,'181,140,255,255')
    label(row,'dishStatus'+str(i),'',232,6,94,25,14,'201,198,105,255').set('justify','right')
bounds('recipeTrack',350,124,16,352)
for i in range(3):
    e=node('filter'+str(i));x=14+i*46
    sprite(node('known'),'filterFrame'+str(i),x-2,44,42,38,sprite='menu_empty2px',fillcenter='false',type='sliced',color='70,70,78,255',depth='10')
    e.set('width','30');e.set('height','30');e.set('pos',f'{x+4},-48')
sprite(node('known'),'searchIcon',14,88,28,28,sprite='ui_game_symbol_search',depth='15',foregroundlayer='true')
bounds('cookingSearch',52,86,292,30)
outcome=panel('outcome','EXPECTED OUTCOME',8,612,380,280)
node('outcomeTitle').set('color','201,198,105,255')
label(outcome,'outcomeStatusTitle','COOK STATUS',14,50,170,24,17,'181,140,255,255')
label(outcome,'outcomeStatus','Add ingredients',14,78,215,28,18)
label(outcome,'outcomeXpTitle','BASE SKILL XP',224,50,144,24,17,'181,140,255,255')
label(outcome,'outcomeXp','—',224,78,144,28,18)
sprite(outcome,'outcomeDivider',12,114,356,1,color='64,64,72,255')
sprite(outcome,'outcomeIcon',14,142,64,64,atlas='ItemIconAtlas',sprite='',depth='15',foregroundlayer='true')
label(outcome,'outcomeName','No meal selected',90,139,270,40,19)
label(outcome,'outcomeHint','',90,185,270,72,17)
panel_bounds('ingredients',398,70,380,590)
for i in range(12):
    bounds('ingredient'+str(i),51+(i%3)*90,54+(i//3)*90 if i<9 else 356,86,86)
    node('cookingSlot'+str(i)).set('cell_size','86')
    bounds('ghost'+str(i),43,47,66,60)
    bounds('need'+str(i),2,3,82,21);node('need'+str(i)).set('font_size','17')
    bounds('sub'+str(i),62,62,22,22)
bounds('herbsLabel',18,329,344,25)
bounds('batchLabel',18,455,100,28);bounds('batchMinus',156,450,32,32);bounds('batchCount',217,455,60,26);bounds('batchPlus',304,450,32,32)
bounds('pullIngredients',18,494,344,34)
label(node('ingredients'),'ingredientGuide','',18,543,344,40,17,'180,180,188,255')
panel_bounds('result',788,70,566,590)
# Hero image on a framed inset, with separate stat rows and iconography.
sprite(node('result'),'resultHeroBg',14,48,180,165,sprite='menu_empty',color='10,10,13,210',globalopacitymod='0',depth='2')
bounds('resultIcon',24,55,160,145)
bounds('resultName',210,52,342,52);node('resultName').set('justify','left')
node('resultStats').set('visible','false')
for i,(key,caption,icon) in enumerate([('Nutrition','Nutrition','ui_game_symbol_fork'),('Water','Water','ui_game_symbol_water'),('Comfort','Base comfort','ui_game_symbol_heart'),('Time','Cook time','ui_game_symbol_clock')]):
    y=117+i*32
    sprite(node('result'),'stat'+key+'Icon',210,y,22,22,sprite=icon,depth='15',foregroundlayer='true')
    label(node('result'),'stat'+key+'Label',caption,241,y,185,26,18)
    label(node('result'),'stat'+key,'—',414,y,134,26,19,'201,198,105,255').set('justify','right')
sprite(node('result'),'resultDivider',14,251,538,1,color='64,64,72,255')
bounds('resultDivider',14,267,538,1)
bounds('referenceTitle',16,274,534,28)
bounds('referenceStatus',70,312,480,64)
bounds('bookReferenceIcon',18,309,32,30);bounds('magazineReferenceIcon',18,343,32,30)
bounds('prepStatus',16,391,534,44)
bounds('prepare',16,447,534,34)
bounds('prepTrack',16,493,534,12);bounds('prepProgress',16,493,534,12)
bounds('cook',16,521,534,36);bounds('cookStatus',16,562,534,24)
# Station equipment remains native size; tool and fuel grids sit beside each other.
panel_bounds('station',1364,70,500,220)
bounds('toolsTitle',14,47,218,25);bounds('fuelTitle',260,47,80,25);bounds('burnTimeLeft',340,47,145,25)
bounds('cookingTools',14,82,228,80);node('cookingTools').set('scale','1')
bounds('cookingFuel',260,82,228,120);node('cookingFuel').set('scale','1')
panel_bounds('output',1364,464,500,196)
bounds('cookingOutput',145,46,228,150);node('cookingOutput').set('scale','0.9')
# Backpack starts at Ingredients, leaving the full left column for recipes/outcome.
bounds('rebirthCraftingInventoryRegion',398,670,1466,222)
bounds('rebirthCraftingInventoryRegionBg',0,0,1466,222)
bounds('rebirthCraftingInventoryHeaderRule',0,38,1466,2)
bounds('rebirthCraftingInventoryScroll',12,46,1442,168)
node('rebirthCraftingInventoryTitle').attrib.pop('text_key',None);node('rebirthCraftingInventoryTitle').set('text','BACKPACK')
# Compact, dynamically sized substitution choices, with individual icons.
bounds('substitutionPopup',490,210,340,244);node('substitutionPopup').set('disablefallthrough','true')
bounds('popupBg',0,0,340,244);node('popupBg').set('globalopacitymod','0')
bounds('substitutionTitle',12,10,316,26)
bounds('substitutionHelp',12,42,316,36);node('substitutionHelp').set('font_size','16')
for i in range(6):
    bounds('choice'+str(i),12,78+i*42,316,36)
    sprite(node('substitutionPopup'),'choiceIcon'+str(i),20,81+i*42,30,30,atlas='ItemIconAtlas',sprite='',depth='76',foregroundlayer='true')
bounds('closeSub',12,202,316,30)
# Action content follows the standard icon / caption / shortcut pattern.
for name,icon in [('cook','ui_game_symbol_fork'),('prepare','ui_game_symbol_book'),('pullIngredients','ui_game_symbol_loot_sack'),('clearRecipe','ui_game_symbol_x'),('takeOutput','ui_game_symbol_store_all_up')]:
    old=node(name);parent=next(p for p in w.iter() if old in list(p));index=list(parent).index(old)
    action=E.Element('rect',name=name,pos=old.get('pos'),width=old.get('width'),height=old.get('height'),on_press='true',on_hover='true',depth='16')
    width=int(old.get('width'));height=int(old.get('height'))
    sprite(action,name+'Frame',0,0,width,height,sprite='menu_empty2px',type='sliced',fillcenter='false',color='225,225,230,255',depth='16')
    sprite(action,name+'ActionIcon',10,6,22,22,sprite=icon,depth='17',foregroundlayer='true')
    label(action,'label',old.get('caption'),42,7,width-87,height-8,18).set('depth','18')
    if name=='cook':label(action,'cookShortcut','',width-42,7,32,height-8,18).set('depth','18')
    parent.remove(old);parent.insert(index,action)
label(node('result'),'mealQuality','',24,211,175,24,18,'181,140,255,255')
label(node('result'),'actualComfort','',16,239,534,28,17)
bounds('resultDivider',14,267,538,1)
bounds('referenceTitle',16,274,534,28)
bounds('referenceStatus',70,308,480,60);node('referenceStatus').set('font_size','16')
label(node('result'),'referenceBenefits','',16,368,534,22,17,'112,196,126,255')
node('magazineReferenceIcon').set('tooltip','Click to choose another studied, accessible technique magazine.')
# Native-style numeric field, single-step and end arrows; no separate queueing control.
old=node('batchCount'); next(p for p in w.iter() if old in list(p)).remove(old)
bounds('batchLabel',18,455,90,28)
bounds('batchMinus',164,450,28,32);node('batchMinus').set('caption','◀')
bounds('batchPlus',276,450,28,32);node('batchPlus').set('caption','▶')
button(node('ingredients'),'batchMin','|◀',128,450,30,32)
button(node('ingredients'),'batchMax','▶|',312,450,30,32)
E.SubElement(node('ingredients'),'textfield',name='batchInput',pos='200,-450',width='68',height='32',text='1',font_size='22',justify='center',validation='Integer',character_limit='4')
node('batchInput').set('validation','integer')
for name,x,icon,flip in [('batchMin',143,'ui_game_symbol_arrow_max',True),('batchMinus',178,'ui_game_symbol_arrow_left',False),('batchPlus',291,'ui_game_symbol_arrow_right',False),('batchMax',328,'ui_game_symbol_arrow_max',False)]:
    old=node(name);node('ingredients').remove(old)
    arrow=E.SubElement(node('ingredients'),'button',name=name,pos=f'{x},-466',width='28',height='30',depth='16',style='icon30px, press',sprite=icon,pivot='center',sound='[paging_click]')
    if flip:arrow.set('flip','Horizontally')
button(node('ingredients'),'cookingMethod','COOKING POT',18,405,320,30)
for e in q.iter('label'):
    if e.get('text')=='COOKING QUEUE':e.set('text','COOKING AREA')
# Manual collection is presented exclusively by the cooking-area card.
# Keep the native output controller offscreen for workstation save/sync compatibility.
output=node('output')
backend=node('cookingOutput')
output.remove(backend)
w.remove(output)
backend.set('pos','-10000,-10000');backend.set('visible','false')
w.append(backend)
for name in ('toolsTitle','fuelTitle'):
    node(name).set('font_size','20');node(name).set('height','26')
node('statComfortIcon').set('atlas','RebirthSurvivorIcons')
node('statComfortIcon').set('sprite','rb_condition_mood_good')
bounds('rebirthCraftingQueueRegion',1364,300,500,360)
bounds('rebirthCraftingQueueRegionBg',0,0,500,360)
bounds('rebirthCraftingQueueController',0,0,500,360)
from cooking_ui_polish import workspace as polish_workspace, hud as polish_hud
polish_workspace(w)
for suffix,tools in [('Stove','toolCookingPot,rebirthCookingFryingPan,toolCookingGrill'),('Oven','FuriousRamsayBakingPan')]:
    c=copy.deepcopy(w);c.set('name','rebirthCookingRoot'+suffix)
    for e in c.iter():
        if e.get('controller')=='WorkstationToolGrid':e.set('required_tools',tools)
    app.append(c)
write(ROOT/'Config/_Cooking/workspace_windows.xml',root)

registration=E.Element('configs')
for name,suffix in [('campfire',''),('cntWoodBurningStove',''),('WorkbenchGasStove001_FR','Stove'),('WorkbenchIronOven001_FR','Oven')]:
    xpath=f"/xui/window_group[@name='workstation_{name}']"
    E.SubElement(registration,'remove',xpath=xpath+'/window')
    for attr,value in [('controller','RebirthCookingStation, RebirthUtils'),('open_backpack_on_open','false')]:E.SubElement(registration,'setattribute',xpath=xpath,name=attr).text=value
    E.SubElement(E.SubElement(registration,'append',xpath=xpath),'window',name='rebirthCookingRoot'+suffix,anchor='Center')
write(ROOT/'Config/_Cooking/workspace_xui.xml',registration)

# Cooking jobs occupy their own HUD cards above the minimap, only in Rebirth progression.
hud=E.Element('configs');parent=E.SubElement(hud,'append',xpath='/windows')
h=E.SubElement(parent,'window',name='rebirthCookingHud',controller='RebirthCookingHud, RebirthUtils',anchor='BottomRight',width='1',height='1',cursor_area='false')
for i in range(64):
    card=rect(h,'cookingHud'+str(i),-300,-230,294,60);card.set('visible','false')
    sprite(card,'background',0,0,294,60,sprite='menu_empty',color='16,16,20,235',globalopacitymod='0')
    sprite(card,'line',0,0,294,2,color='180,25,28,255')
    sprite(card,'icon',5,5,48,48,atlas='ItemIconAtlas',sprite='',depth='8',foregroundlayer='true')
    label(card,'name','',59,3,228,20,17)
    label(card,'status','',59,23,228,17,15,'181,140,255,255')
    label(card,'time','',59,41,228,17,15,'220,198,87,255')
polish_hud(h)
write(ROOT/'Config/_Cooking/hud_windows.xml',hud)
