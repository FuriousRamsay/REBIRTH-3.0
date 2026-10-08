from pathlib import Path
from lxml import etree as E
import re

r=E.parse('Config/XUi_InGame/windows.xml')
native=E.parse('../../Data/Config/XUi_InGame/windows.xml')
theme=r.xpath('//conditional/if')[ -1 ]
for patch in theme:
    if patch.tag not in ('set','setattribute','remove','append'): continue
    target=patch.get('xpath')
    assert native.xpath(target), f'Editor patch has no native target: {target}'

for file in ('windows.xml','xui.xml','templates.xml'):
    E.parse('Config/XUi_InGame/'+file)
for w in r.xpath('//window[@name="rebirthSurvivorCharacterWindow" or @name="rebirthPersonalCraftingRoot"]'):
    assert len(w.xpath('.//rect[starts-with(@name,"rebirthCraftingTab") and not(contains(@name,"Active"))]')) == 7
    assert not w.xpath('.//*[@name="rebirthCraftingTabInventory"]')
assert len(r.xpath('//rect[@controller="RebirthCraftingItemActionEntry, RebirthUtils" and starts-with(@name,"rebirthCraftingItemAction")]'))==6
for id in ('characterBagDurability','rebirthCraftingItemDurability','rebirthRecipeQuality'):
    assert len(r.xpath('//*[@name=$id]',id=id))==1
nativeGroup=Path('../../Data/Config/XUi_InGame/xui.xml').read_text()
assert 'name="assemble"' in nativeGroup and 'name="cosmetics"' in nativeGroup
code=Path('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs').read_text()
assert 'public bool IsInventoryOnlyMode => false;' in code
assert 'surfaceMode = SurfaceMode.Inventory' not in code
life=Path('Scripts/Survivor/UI/RebirthItemWindowLifecycle.cs').read_text()
assert 'nameof(XUiC_AssembleWindowGroup.OnClose)' in life
assert 'instruction.Calls(native)' in life and 'returnCaptured' in Path('Scripts/Survivor/UI/XUiC_RebirthItemEditorHeader.cs').read_text()
assert 'ParentActionList = actions' in life
quality=Path('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeDetails.cs').read_text()
assert 'XUiM_Recipes.CraftingMaxTier' in quality and 'ChangeQuality(-1)' in quality
for file in ('XUiC_RebirthCraftingRequirements.cs','XUiC_RebirthCraftingActions.cs','RebirthCraftingOutcomeViewModel.cs','RebirthCraftingQualityCountPatch.cs'):
    assert 'SelectedCraftingTier' in Path('Scripts/Crafting/UI/PersonalCrafting',file).read_text()
print('PASS: native editor XPath targets; XML parses; seven-tab navigation; six action slots; durability/quality controls; native return call interception and shared selected-tier consumers.')
