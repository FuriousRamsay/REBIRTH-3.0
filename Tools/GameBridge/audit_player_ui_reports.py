from pathlib import Path
from lxml import etree as E
root = Path(__file__).resolve().parents[2]
native = root.parents[1] / 'Data/Config'
windows=E.parse(str(root/'Config/XUi_InGame/windows.xml'))
hints=windows.xpath('//label[@name="journalEditorHint"]')
assert len(hints)==2 and all('Text only | Title:' in h.get('text') for h in hints)
assert len(windows.xpath('//label[starts-with(@text,"3 columns, 5 rows maximum.")]'))==1
items=E.parse(str(native/'items.xml'))
patch=E.parse(str(root/'Config/_Rebirth/vanilla_literature_items.xml'))
for op in patch.getroot():
    if not isinstance(op.tag,str):continue
    for target in items.xpath(op.get('xpath')):
        if op.tag=='remove':target.getparent().remove(target)
        else:
            import copy
            for child in op:target.append(copy.deepcopy(child))
excluded=[x for x in items.findall('item') if x.get('name','').startswith('book') or x.get('name','').endswith('SkillMagazine')]
assert len(excluded)==158
assert all(x.find("property[@name='CreativeMode']").get('value')=='None' for x in excluded)
loot=E.parse(str(native/'loot.xml'))
patch=E.parse(str(root/'Config/_Rebirth/vanilla_literature_loot.xml'))
removed=0;affected=set()
for op in patch.getroot():
    if not isinstance(op.tag,str):continue
    for target in loot.xpath(op.get('xpath')):
        if op.tag=='remove':affected.add(target.getparent());target.getparent().remove(target);removed+=1
        else:
            import copy
            for child in op:target.append(copy.deepcopy(child))
assert removed==783 and all(x.findall('item') for x in affected)
assert not loot.xpath(patch.find('remove').get('xpath'))
custom=E.parse(str(root/'Config/_Survivor/items.xml'))
for name in ['StructuralBracketSet','FirearmCleaningKit','RivetFastenerSet','SheetMetalBrackets','VehicleServicePartsKit','RenderedTallow']:
    icon=custom.find('.//item[@name="rebirth'+name+'"]/property[@name="CustomIcon"]')
    assert custom.find('.//item[@name="rebirth'+name+'"]/property[@name="CreativeMode"]').get('value')=='None'
    assert icon is not None and items.find('item[@name="'+icon.get('value')+'"]') is not None
print('PASS: 3 labels; 6 native icon references; 158 native literature exclusions; 783 direct native loot entries removed with no emptied affected groups. Native XML baseline simulation, not game-loaded configuration.')

# Apply the actual trader progression patch to the native baseline in document order.
import copy
traders=E.parse(str(native/'traders.xml'))
for op in E.parse(str(root/'Config/_Rebirth/progression_traders.xml')).getroot():
    if not isinstance(op.tag,str): continue
    for target in list(traders.xpath(op.get('xpath'))):
        if op.tag=='removeattribute': target.getparent().attrib.pop(target.attrname, None)
        elif op.tag=='setattribute': target.set(op.get('name'),op.text or '')
        elif op.tag=='remove': target.getparent().remove(target)
        elif op.tag=='append':
            for child in op: target.append(copy.deepcopy(child))
        elif op.tag=='insertBefore':
            for child in op: target.addprevious(copy.deepcopy(child))
        else: raise AssertionError('Unsupported trader patch operation '+op.tag)
excluded_ids={x.get('name') for x in excluded}
assert not [x for x in traders.xpath('//item[@name]') if x.get('name') in excluded_ids]
pool=traders.xpath('//trader_item_group[@name="perkBooks"]')[0]
assert len(pool.findall('item'))==23
for item in pool.findall('item'):
    linked=traders.xpath('//trader_item_group[@name="'+item.get('group')+'"]')
    assert len(linked)==1 and linked[0].findall('item')
assert len(traders.xpath('//item[@group="perkBooks"]'))==3
print('PASS: native trader progression simulation has no excluded literature; 23 nonempty learning categories and 3 existing stock references retained.')
