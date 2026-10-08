"""Resolve UI patches in load order and check the repaired Rebirth workspace."""
from copy import deepcopy
from pathlib import Path
from lxml import etree as E

def resolve(file, native):
    patch = E.parse(file)
    result = E.parse(native)
    latest = patch.xpath('./conditional')[-1]
    assert latest.find('if').get('cond') == "character_progression('Rebirth')"
    for op in patch.getroot().iter():
        if not isinstance(op.tag, str) or op.tag not in ('append', 'setattribute', 'remove', 'removeattribute') or not op.get('xpath'):
            continue
        targets = result.xpath(op.get('xpath'))
        if op.tag == 'setattribute':
            assert op.text is not None, ('Native XML loader requires a text node', op.get('xpath'))
        if latest in op.iterancestors() and op.tag != 'remove':
            assert targets, ('Unmatched final patch', op.get('xpath'))
        for target in targets:
            if op.tag == 'append':
                for child in op:
                    target.append(deepcopy(child))
            elif op.tag == 'setattribute':
                target.set(op.get('name'), op.text or '')
            else:
                if isinstance(target, E._ElementUnicodeResult) and target.is_attribute:
                    assert op.tag == 'removeattribute', 'Native remove rejects attributes'
                    del target.getparent().attrib[target.attrname]
                else:
                    assert op.tag == 'remove', 'Native removeattribute requires an attribute'
                    target.getparent().remove(target)
    return result

w = resolve('Config/XUi_InGame/windows.xml', '../../Data/Config/XUi_InGame/windows.xml')
x = resolve('Config/XUi_InGame/xui.xml', '../../Data/Config/XUi_InGame/xui.xml')
bars = w.xpath('//rect[@name="rebirthCraftingTopTabs"]')
assert bars
for bar in bars:
    names = bar.xpath('./rect/@name')
    assert 'rebirthCraftingTabSkills' not in names
    for tab in ('Crafting', 'Character', 'Map', 'Quests', 'Challenges', 'Players', 'Journal'):
        assert 'rebirthCraftingTab' + tab in names
    window = bar.xpath('ancestor::window')[0]
    available = set(window.xpath('.//@name'))
    for button in bar.xpath('.//button'):
        for direction in ('nav_left', 'nav_right', 'nav_up', 'nav_down'):
            target = button.get(direction)
            if target:
                assert target in available, (window.get('name'), button.get('name'), direction, target)
creative = w.xpath('/windows/window[@name="rebirthCreativeRoot"]')[0]
assert x.xpath('/xui/window_group[@name="creative"]/window/@name') == ['rebirthCreativeRoot']
assert not creative.xpath('.//*[@name="rebirthCraftingTopTabs"]')
for name in ('btnClearInventory', 'btnToggleLockMode', 'btnSort', 'btnRebirthQuickStack', 'btnRebirthCompanions', 'characterStatsPopup'):
    assert len(creative.xpath('.//*[@name="'+name+'"]')) == 1, name
assert creative.xpath('.//item_stack[@controller="RebirthCreativeStatsStack, RebirthUtils"]')
grid = creative.xpath('.//grid[@controller="Creative2StackGrid" or @controller="RebirthCreativeCatalogueGrid, RebirthUtils"]')[0]
assert int(grid.get('cols')) * 75 + 101 < 1412
assert int(grid.get('rows')) * 75 + 97 < 889
journal = w.xpath('/windows/window[@name="rebirthJournalRoot"]')[0]
for i in range(12):
    for name in ('journalRow'+str(i)+'Hit', 'journalRowTitle'+str(i), 'journalRowType'+str(i)):
        assert len(journal.xpath('.//*[@name="'+name+'"]')) == 1, ('Missing/duplicate Journal control', name)
assert not journal.xpath('.//*[@name="journalPrevious" or @name="journalNext" or @name="journalTextPrevious" or @name="journalTextNext" or @name="journalType"]')
assert journal.xpath('.//rect[@name="journalTypeDropdown" and @controller="RebirthJournalTypeDropdown, RebirthUtils"]')
for i in range(3): assert len(journal.xpath('.//button[@name="journalTypeChoice'+str(i)+'"]')) == 1
assert journal.xpath('.//*[@name="journalBodyScroll"]/defaultscrollbar')
assert journal.xpath('.//*[@name="journalListScroll"]//*[@name="listNativeScrollbar"]/defaultscrollbar')
assert len(journal.xpath('.//*[@name="journalListScroll"]//*[@name="listContent"]/rect')) == 12
for label in journal.xpath('.//label[@pos]'):
    assert '.0' not in label.get('pos')
for button in journal.xpath('.//button'):
    if button.get('name', '').endswith('Hit'):
        assert button.get('hoverscale') == '1'
map_area = w.xpath('/windows/window[@name="mapArea"]')[0]
native = E.parse('../../Data/Config/XUi_InGame/windows.xml').xpath('/windows/window[@name="mapArea"]')[0]
assert map_area.get('controller') == 'RebirthMapArea, RebirthUtils'
for name in ('mapView', 'mapViewTexture'):
    actual = map_area.xpath('.//*[@name="'+name+'"]')[0]
    original = native.xpath('.//*[@name="'+name+'"]')[0]
    assert actual.get('width') == '1406'
    for attr in ('height', 'pos', 'material', 'clipping'):
        assert actual.get(attr) == original.get(attr), (name, attr)
assert map_area.xpath('.//*[@name="clippingPanel" and @clippingsize="1406,712" and @clippingcenter="703,-356"]')
assert w.xpath('//*[@name="btnRebirthGroupColorPicker"]')
assert not w.xpath('//*[starts-with(@name,"btnRebirthGroupColor") and @name!="btnRebirthGroupColorPicker"]')
print('PASS: shared seven-tab navigation; Creative controls/layout; Journal dropdown, scroll controls and button bounds; native map geometry/material; player color states.')


# The overlay must not obscure the independent caption/icon artwork.
for button in journal.xpath('.//*[@name="rebirthCraftingTopTabs"]//button'):
    for color in ('defaultcolor', 'hovercolor', 'selectedcolor'):
        assert int(button.get(color).split(',')[-1]) <= 1
assert int(journal.xpath('.//scrollview[@name="journalBodyViewport"]/@depth')[0]) > int(journal.get('depth'))
assert int(w.xpath('/windows/window[@name="windowChallengeList"]//scrollview/@depth')[0]) > 0
q=w.xpath('/windows/window[@name="windowQuestList"]')[0]
assert not q.xpath('.//pager')
assert q.xpath('.//*[@name="listNativeScrollbar"]/defaultscrollbar')
assert len(q.xpath('.//*[@name="listContent"]/rect')) == 22
assert len(q.xpath('.//*[@name="questSelectionReference"]')) == 1
# Image UV projection and marker/pointer transforms agree after pan and zoom.
for zoom in (.5,1,2,3):
    scale=336*zoom/2048
    for center in ((0,0),(231,-460)):
        for offset in ((0,0),(-90,32),(130,-80)):
            width,height=1406,712
            local=(width/2+offset[0]*712/(336*zoom),-height/2+offset[1]*712/(336*zoom))
            recovered=((local[0]-width/2)*336/712*zoom,(local[1]+height/2)*336/712*zoom)
            assert all(abs(a-b)<1e-6 for a,b in zip(offset,recovered))
            native_start=((2048-336*zoom)/2+center[0])/2048
            wide_scale=scale*width/712
            u=native_start-(wide_scale-scale)/2+local[0]/width*wide_scale
            assert abs(u-(.5+(center[0]+offset[0])/2048))<1e-6
print('PASS: foreground panel order, transparent navigation overlays, three-choice dropdown, unpaged quests, rectangular map UV/marker/pointer agreement.')
