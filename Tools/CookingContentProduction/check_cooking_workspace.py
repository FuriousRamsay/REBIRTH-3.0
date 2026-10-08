"""Static integration checks for the generated, Rebirth-only cooking workspace."""
from pathlib import Path
import xml.etree.ElementTree as E
from content_model import ROOT

windows=E.parse(ROOT/'Config/_Cooking/workspace_windows.xml').getroot().findall('append/window')
assert len(windows)==3
required=['pullIngredients','prepare','prepProgress','prepStatus','cook','substitutionPopup','stationName','stationIcon']
for window in windows:
    names={e.get('name') for e in window.iter()}
    assert set(required)<=names,window.get('name')
    assert all('cookingSlot'+str(i) in names for i in range(12))
    assert all('btnRebirthCraftingTab'+name in names for name in ['Crafting','Character','Map','Quests','Challenges','Players','Journal'])
    assert 'rebirthCraftingWorldStatus' in names
    assert 'rebirthCraftingTabSkills' not in names and 'rebirthCraftingTabInventory' not in names
    for name in ['known','ingredients','result','station']:
        bg=window.find(".//sprite[@name='%sBg']"%name)
        assert bg.get('sprite')=='menu_empty' and bg.get('globalopacitymod')=='0'
    assert window.find(".//rect[@name='station']").get('pos')=='1364,-70'
    assert window.find(".//rect[@name='rebirthCraftingInventoryRegion']").get('pos')=='398,-670'
    assert all('choice'+str(i) in names for i in range(6))
    assert 'outcomeStatus' in names and 'cookShortcut' in names
    assert 'outcomeIcon' not in names and 'cookingSkillValue' in names
    assert window.find(".//label[@name='burnTimeLeft']").get('justify')=='right'
    for action in ['cook','prepare','pullIngredients','clearRecipe']:
        a=window.find(".//rect[@name='%s']"%action)
        assert a.get('controller')=='RebirthCookingAction, RebirthUtils'
        assert a.find("sprite[@name='actionFill']").get('color')=='17,17,21,255'
        assert a.find("label[@name='label']").get('pivot')=='center'

    assert all('stat'+stat in names for stat in ['Nutrition','Water','Comfort','Time','Energy'])
    assert window.find(".//panel[@controller='RebirthCharacterStatsPopup, RebirthUtils']") is not None
    assert all('foodStatIcon'+str(i) in names for i in range(16))
    food_popup=window.find(".//rect[@name='foodPopup']")
    assert food_popup is not None
    assert len(food_popup.findall("label[@name='foodPopupQuality']"))==1
    assert not any(e.get('text') in ('HOVERED','SELECTED','ITEM STATS','Quality') for e in food_popup.iter())
    assert all('foodPopupRow'+str(i) in names for i in range(4))
    assert window.find(".//button[@name='filter0']").get('sprite')=='rb_skill_cooking'
    guide=window.find(".//label[@name='cookingGuideBody']")
    assert int(guide.get('font_size'))>=19 and int(guide.get('height'))>=200
    assert int(window.find(".//label[@name='statNutrition']").get('pos').split(',')[1]) == -83
    for element in window.iter():
        for attribute,target in element.attrib.items():
            if attribute.startswith('nav_'):assert target in names,(window.get('name'),element.get('name'),attribute,target)
    # Top-level content panels must not overlap, including the reserved left outcome column.
    boxes=[]
    for name in ['known','outcome','ingredients','result','station','rebirthCraftingQueueRegion','rebirthCraftingInventoryRegion']:
        e=window.find(".//*[@name='%s']"%name);x,y=map(int,e.get('pos').split(','));y=-y
        box=(x,y,x+int(e.get('width')),y+int(e.get('height')))
        assert box[0]>=0 and box[1]>=70 and box[2]<=1872 and box[3]<=900,(name,box)
        for other,b in boxes:assert box[2]<=b[0] or box[0]>=b[2] or box[3]<=b[1] or box[1]>=b[3],(name,other)
        boxes.append((name,box))
    assert not any(e.get('controller')=='RebirthCookingScroll, RebirthUtils' for e in window.iter())
    bag=window.find(".//grid[@controller='RebirthCraftingInventory, RebirthUtils']")
    assert (bag.get('rows'),bag.get('cols'),bag.get('repeat_content'))==('4','26','true')
    assert 'btnRebirthCraftingInventorySort' in names and 'btnRebirthCraftingInventoryLock' in names
    assert 'rebirthCraftingInventoryScrollThumb' in names
    queue=window.find(".//rect[@name='rebirthCraftingQueueRowsHost']")
    assert len(queue.findall('rebirth_personal_crafting_queue_entry'))==4
    assert 'output' not in names and 'takeOutput' not in names
    assert window.find(".//rect[@name='cookingOutput']").get('visible')=='false'
    assert window.find(".//sprite[@name='statComfortIcon']").get('sprite')=='rb_condition_mood_good'
    assert all(window.find(".//label[@name='%s']"%n).get('font_size')=='20' for n in ['toolsTitle','fuelTitle'])
    output=window.find(".//grid[@controller='WorkstationOutputGrid']")
    assert (output.get('rows'),output.get('cols'))==('2','3')
    for i in range(12):
        cell=window.find(".//rect[@name='ingredient%s']"%i)
        ghost=cell.find("sprite[@name='ghost%s']"%i)
        assert ghost.get('pivot')=='center' and ghost.get('color').endswith(',255')
        assert cell.find('item_stack').get('pos')=='0,0'
        assert cell.find('item_stack').get('repeat_i')==str(i)
        assert cell.find('item_stack').get('on_press')=='true'
        label=cell.find('label')
        assert abs(int(label.get('pos').split(',')[1]))+int(label.get('height'))<=int(cell.get('height'))

# Catch misspelled output, literature, or substitution IDs before the game loads.
items=set()
for path in [ROOT.parents[1]/'Data/Config/items.xml', *ROOT.glob('Config/**/*.xml')]:
    root=E.parse(path).getroot()
    items.update(e.get('name') for e in root.iter('item') if e.get('name'))
runtime=E.parse(ROOT/'Config/_Cooking/runtime.xml').getroot()
techniques={e.get('item'):e for e in runtime.findall('technique')}
assert len(techniques)==44
for technique in techniques.values():
    assert technique.get('item') in items,technique.attrib
    assert technique.get('effect') in {'T','Q','H'},technique.attrib
roles=0
for dish in runtime.findall('dish'):
    assert dish.get('item') in items,dish.get('item')
    for e in dish:
        if e.tag=='substitute':
            roles+=1
            assert e.get('ingredient') in items,e.attrib
            assert all(name in items for name in e.get('choices').split(',')),e.attrib
            assert len(e.get('choices').split(','))<=5
        else: assert e.get('item') in items,e.attrib
for file in ['windows','xui']:
    main=E.parse(ROOT/f'Config/XUi_InGame/{file}.xml').getroot()
    conditionals=main.findall(".//if[@cond=\"character_progression('Rebirth')\"]")
    assert any(e.find(f"include[@filename='../_Cooking/workspace_{file}.xml']") is not None for e in conditionals)
print(f'Cooking workspace XML checks passed: {len(windows)} station layouts, {len(runtime.findall('dish'))} dishes, {roles} substitution roles, conditional registration.')

# Hidden native output must not consume Reload and close the station before Take runs.
for window in windows:
    assert window.find(".//*[@name='cookingOutput']").get('controller') != 'WorkstationOutputWindow'
# Newly generated water is full and qualifies for the full-container ingredient rule.
balance=E.parse(ROOT/'Config/_Cooking/balance_items.xml')
water=next((p for a in balance.findall('append') if a.get('xpath')=="/items/item[@name='drinkJarBoiledWater']" for p in a.findall('property') if p.get('name')=='RebirthInitialVolumeMl'),None)
assert water is not None and float(water.get('value'))==500

# Recipe scrolling translates a stable clipped list; it must not replace eight rows per notch.
for window in windows:
    viewport=window.find(".//panel[@name='recipeViewport']")
    assert viewport is not None and viewport.get('clipping')=='softclip'
    rows=viewport.find("rect[@name='recipeRows']")
    assert len(rows)==160 and len(runtime.findall('dish'))<=len(rows)
    assert window.find(".//*[@name='recipeThumb']").get('on_press')=='true'
    for name in ['bookReferenceIcon','magazineReferenceIcon']:
        icon=window.find(".//*[@name='%s']"%name)
        assert int(icon.get('width'))>=64 and icon.get('width')==icon.get('height')
    for i in range(12):
        ghost=window.find(".//*[@name='ghost%s']"%i)
        assert ghost.get('width')==ghost.get('height')
        button=window.find(".//*[@name='sub%s']"%i)
        x,y=map(int,button.get('pos').split(','))
        assert x>=4 and -y>=4 and x+int(button.get('width'))<=82 and -y+int(button.get('height'))<=82
cards=E.parse(ROOT/'Config/_Cooking/items.xml')
for item in cards.findall('.//item'):
    kind=item.find("property[@name='RebirthCookingLiteratureKind']")
    if kind is not None:
        assert item.find("property[@class='Action0']/property[@name='UseAnimation']").get('value')=='false'
        assert item.find("property[@class='Action0']/property[@name='Consume']").get('value')=='false'
print('Review regressions passed: stable recipe rows, draggable thumb, square icons, inset substitution controls, non-consuming literature actions.')

# Every recipe card has a unique read marker, separate from broad background knowledge.
literature={e.get('id'):e for e in E.parse(ROOT/'Config/_Survivor/literature.xml').getroot().findall('item')}
markers=set()
for dish in runtime.findall('dish'):
    for card in dish.findall('card'):
        item=card.get('item');definition=literature[item]
        marker=definition.get('knowledge')
        assert marker=='literature.read.'+item
        assert marker not in markers
        markers.add(marker)
print('Card definition checks passed: unique per-title read markers.')
