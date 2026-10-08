from pathlib import Path
from lxml import etree as E
import re

r=E.parse('Config/XUi_InGame/windows.xml')
x=E.parse('Config/XUi_InGame/xui.xml')
def named(root,n):
    found=root.xpath('.//*[@name=$n]',n=n)
    assert len(found)==1,(n,len(found))
    return found[0]
for name in ('rebirthModifyEditor','rebirthCosmeticsEditor'):
    w=named(r,name)
    assert (w.get('width'),w.get('height'),w.get('pos'))==('1872','935','-936,505')
    assert w.get('controller')=='RebirthItemEditorHeader, RebirthUtils'
    assert named(w,'editorBody').get('width')=='1856'
    assert named(w,'editorBody').get('height')=='813'
    assert named(w,'editorBack') is not None
    assert not w.xpath('.//*[@name="emptyInfoPanel" or @name="windowNonPagingHeader"]')
modify=named(r,'rebirthModifyEditor');cos=named(r,'rebirthCosmeticsEditor')
slots=modify.findall('.//item_stack')
assert len(slots)==100
assert [int(e.get('repeat_i')) for e in slots]==list(range(100))
assert all(e.get('cell_size')=='94' for e in slots)
assert named(modify,'editorBackpack').get('pos')=='844,0'
assert 844+int(named(modify,'editorBackpack').get('width'))==1856
assert named(modify,'listViewport').get('clipping_size')=='960,611'
assert named(modify,'parts').get('controller')=='ItemPartStackGrid'
assert named(modify,'cosmeticparts').get('controller')=='ItemCosmeticStackGrid'
assert named(cos,'inventory').get('cols')=='18'
assert named(cos,'playerPreviewSDCS').get('size')=='530,706'
for action in ('btnClearAll','btnApply','btnApplySet'):assert named(cos,action).get('pos').endswith(',-777')
classes=set()
for p in Path('Scripts').rglob('*.cs'):
    classes.update(re.findall(r'\bclass\s+(\w+)',p.read_text(encoding='utf-8-sig')))
for controller in modify.xpath('.//*[@controller]')+cos.xpath('.//*[@controller]'):
    raw=controller.get('controller')
    if 'RebirthUtils' not in raw:continue
    classname='XUiC_'+raw.split(',')[0]
    assert classname in classes,classname
quality=named(r,'rebirthRecipeQuality')
assert named(quality,'recipeQualityFill').get('type')=='filled'
for arrow in ('qualityDown','qualityUp'):
    assert named(quality,arrow).get('disabledsprite')==named(quality,arrow).get('sprite')
for name in ('characterBagDurability','rebirthCraftingItemDurability'):
    assert named(named(r,name),'qualityNumber').get('justify')=='right'
life=Path('Scripts/Survivor/UI/RebirthItemWindowLifecycle.cs').read_text()
assert life.index('Cancel.IsPressed')<life.index('windowManager.Open(destination')
layout=Path('Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs').read_text()
assert 'detailsHeight - 180' not in layout
assert '"rebirthCraftingActionsStrip"), 8, -198' not in layout
assert x.xpath('//setattribute[@xpath="/xui/window_group[@name=\'assemble\']" and @name="open_backpack_on_open" and text()="false"]')
print('PASS: separate wide editor roots, native editor controller dependencies, 100 indexed backpack cells, viewport bounds, native cosmetic controls, quality overlays and arrows, release-before-return ordering, single recipe action-row owner.')
