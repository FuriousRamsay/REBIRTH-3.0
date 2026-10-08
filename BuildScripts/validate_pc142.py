from pathlib import Path
import xml.etree.ElementTree as E
import re

r=E.parse('Config/XUi_InGame/windows.xml').getroot()
character=next(e for e in r.iter('window') if e.get('name')=='rebirthSurvivorCharacterWindow')
n={e.get('name'):e for e in character.iter() if e.get('name')}
bag=n['survivorOverviewBackpackPanel']
slots=list(bag.iter('item_stack'))
assert len(slots)==100
# Verify the specific native template dependency reported 100 times in the supplied game log.
native=E.parse('../../Data/Config/XUi_InGame/templates.xml').getroot()
template=next(e for e in native if e.tag=='item_stack')
assert '${repeat_i}' in E.tostring(template,encoding='unicode')
assert [int(e.get('repeat_i')) for e in slots]==list(range(100))
assert all(e.get('cell_size')=='75' for e in slots)
assert 'userlockmode' in E.tostring(template,encoding='unicode')
source=Path('Scripts/Survivor/UI/XUiC_RebirthCharacterBackpack.cs').read_text()
assert 'name == "userlockmode"' in source and 'lockMode ? "true" : "false"' in source
assert n['characterBagLock'].get('sprite')=='ui_game_symbol_lock'
assert n['characterBagLock'].get('atlas')=='UIAtlas'
assert 'ItemActionEntryScrap ||' in source and 'is ItemActionEntryDrop' in source
for name in ('characterBagSelected','characterBagSummary','characterBagDescription'):
    e=n[name]
    assert int(e.get('depth'))>int(n['characterBagContextBg'].get('depth'))
    assert abs(int(e.get('pos').split(',')[1])) < 254
assert n['characterBagActions'].get('pos')=='14,-254'
assert n['characterBackpackScroll'].get('pos')=='14,-416'
assert 416+int(n['characterBackpackScroll'].get('height'))<=813
for f in ('XUiC_RebirthCraftingInventory.cs','XUiC_RebirthCraftingItemContext.cs'):
    text=Path('Scripts/Crafting/UI/PersonalCrafting',f).read_text()
    assert 'InfoWindow = null' not in text, f
lifecycle=Path('Scripts/Survivor/UI/RebirthItemWindowLifecycle.cs').read_text()
assert 'character.IsCharacterWindowOpen' in lifecycle and 'crafting.State.IsOpen' in lifecycle
assert '__instance.InMenu = true' in lifecycle
assert 'showCraftingLater' in lifecycle and 'RestoreItem' in lifecycle and 'RestoreOffset' in lifecycle
installer=Path('Scripts/Survivor/UI/RebirthSurvivorUiInstaller.cs').read_text()
for patch in ('RebirthCharacterDragMenuPatch','RebirthItemModifyCapturePatch','RebirthItemModifyReturnPatch'):
    assert 'typeof('+patch+')' in installer
preview=next(e for e in r.iter() if e.get('name')=='rebirthScrapPreview')
assert preview.tag=='panel' and int(preview.get('depth'))>=90
for field in ('resultIcon0','resultName0','resultCount0','resultIcon1','resultName1','resultCount1'):
    assert any(e.get('name')==field for e in preview.iter())
previewcode=Path('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthScrapPreview.cs').read_text()
assert 'GetScrapableRecipe' in previewcode and 'ScrappingOutputModifier' in previewcode
assert not any(x in previewcode for x in ('AddItem(', 'DropItem(', 'HandleRemoveAmmo(', 'OnActivated('))
print('PASS: native repeat/template and lock-binding contracts, detail/grid order and text depth, native Modify references, scoped drag ownership and return patches, read-only graphical scrap preview.')
