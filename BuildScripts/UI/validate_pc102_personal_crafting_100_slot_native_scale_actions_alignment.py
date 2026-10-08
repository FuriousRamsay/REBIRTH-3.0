from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('.')

def read(rel):
    p=root/rel
    if not p.exists():
        print('FAIL missing '+rel); raise SystemExit(1)
    return p.read_text(encoding='utf-8',errors='ignore')

xml=read('Config/XUi_InGame/windows.xml')
scroll=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs')
bridge=read('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs')
ctx=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs')
entry=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs')
audit=read('Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutAudit.cs')
gear=read('Scripts/Survivor/Support/RebirthSurvivorGearService.cs')

checks=[]
def ck(name,cond):
    checks.append((name,bool(cond))); print(('PASS ' if cond else 'FAIL ')+name)

ET.parse(root/'Config/XUi_InGame/windows.xml')
ck('V3.2 live native slot fallback is 75px', 'FallbackNativeCell = 75' in scroll)
ck('slot scale uses actual live view width and height', 'slot.ViewComponent.Size.x' in scroll and 'slot.ViewComponent.Size.y' in scroll)
ck('uniform slot scale is computed from both axes', 'Math.Min(effectiveCell / (float)nativeW, effectiveCell / (float)nativeH)' in scroll)
ck('complete ItemStack root receives one uniform scale', 'Vector3.one * uniformScale' in scroll)
ck('diagnostic distinguishes native actual target', 'native=' in scroll and 'actual=' in scroll and 'target=' in scroll)
ck('13 columns remain', 'public const int Columns = 13;' in bridge and 'cols="13"' in xml)
ck('four visible rows remain', 'public const int VisibleRows = 4;' in bridge)
ck('100 physical-slot test override is explicit', 'ForceHundredSlotBackpackForPersonalCraftingTest = true' in gear and 'PersonalCraftingTestPhysicalBagSlots = 100' in gear)
ck('100-slot override is used by live player capacity resolver', 'if (ForceHundredSlotBackpackForPersonalCraftingTest)' in gear and 'desiredSlots = PersonalCraftingTestPhysicalBagSlots;' in gear)
ck('100-slot override is used by record snapshot resolver', 'return PersonalCraftingTestPhysicalBagSlots;' in gear)
ck('backend ceiling remains 100', 'public const int MaxPhysicalBagSlots = 100;' in gear)
ck('presentation still hides nonphysical tail cells', 'authoritative ? Vector3.one * uniformScale : Vector3.zero' in scroll)
ck('100 physical slots produce scrollable 8-row geometry', 'totalRows = Math.Max(1, (physical + Columns - 1) / Columns);' in scroll and 'MaxPixelOffset' in scroll)
ck('batch label lowered again', 'name="rebirthCraftingBatchTitle" depth="4" pos="446,-194"' in xml)
ck('recipe status XML is vertically lowered', xml.count('name="rebirthCraftingRecipeStatus" depth="5" pos="220,-7"') >= 11)
ck('recipe status runtime box exact-centers on row', '(height - statusHeight) / 2' in entry)
ck('item actions copy recipe action strip rectangle', 'owner.GetChildById("rebirthCraftingActionsStrip")' in ctx and 'recipeActionPos' in ctx and 'recipeActionSize' in ctx)
ck('item action names uppercase at runtime', 'nameLabel.Text = (nameLabel.Text ?? string.Empty).ToUpperInvariant();' in ctx)
ck('item action shortcut uppercase at runtime', 'keyboardLabel.Text = (keyboardLabel.Text ?? string.Empty).ToUpperInvariant();' in ctx)
ck('item action shortcut uses same 17px font', 'keyboardLabel.FontSize = 17;' in ctx and xml.count('name="keyboardButton"') >= 5 and xml.count('font_size="17" color="{statuscolor}" pivot="right" justify="right" upper_case="true"') >= 5)
ck('item action labels XML uppercase', xml.count('text="{actionname}" font_size="17" color="{statuscolor}" justify="center" overflow="clampcontent" upper_case="true"') >= 5)
ck('native item-action hover remains enabled', xml.count('on_press="true" on_hover="true"') >= 5)
ck('item selection remains independent of viewport focus', 'contextPreserved=true' in ctx)
ck('item context no longer triggers old false layout audit', 'Selected Recipe + Requirements context swap' in audit and 'item context overlaps Requirements' not in audit)
ck('item context must stop before Backpack', 'item context overlaps Backpack' in audit)
for n,t in [('scroll',scroll),('context',ctx),('entry',entry),('audit',audit),('gear',gear)]:
    ck(n+' brace balance', t.count('{')==t.count('}'))

failed=[n for n,ok in checks if not ok]
print(f'PC102 personal crafting 100-slot/native-scale/actions/alignment: {len(checks)-len(failed)} PASS / {len(failed)} FAIL')
if failed:
    for n in failed: print('FAIL - '+n)
raise SystemExit(1 if failed else 0)
