from pathlib import Path
import sys, re
import xml.etree.ElementTree as ET

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parents[2]
checks=[]
def ck(name, ok, detail=''):
    ok=bool(ok); checks.append((name,ok,detail)); print(('PASS' if ok else 'FAIL')+' - '+name+((' :: '+detail) if detail else ''))
def txt(rel):
    p=root/rel; ck('exists '+rel,p.exists()); return p.read_text(errors='ignore') if p.exists() else ''

xml=txt('Config/XUi_InGame/windows.xml')
layout=txt('Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs')
bridge=txt('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs')
iscroll=txt('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs')
slot=txt('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs')
cmd=txt('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingCommandBridge.cs')
cat=txt('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs')

try:
    ET.parse(root/'Config/XUi_InGame/windows.xml'); ck('windows.xml parses',True)
except Exception as e: ck('windows.xml parses',False,str(e))

# Runtime-proven recipe reset: passive CanCraft must never force the selected row visible.
ck('passive CanCraft resolves recipe without ensure-visible side effect','BuildCraft(recipe, craftingTier, false)' in cmd)
ck('explicit Craft execution may reveal selected recipe','BuildCraft(recipe, craftingTier, true)' in cmd)
ck('Favorite execution remains explicit reveal','ResolveEntry(recipe, true)' in cmd and 'ExecuteFavorite' in cmd)
ck('Track execution remains explicit reveal','ResolveEntry(recipe, true)' in cmd and 'ExecuteTrack' in cmd)
ck('command bridge forwards explicit ensureVisible flag','GetBehaviorEntryForRecipe(recipe, ensureVisible)' in cmd)
ck('catalogue retains non-mutating lookup when ensureVisible false','if (match != null || !ensureVisible) return match;' in cat)

# Fixed approved 1920x1080 surface: wider resolutions must center, not stretch.
ck('fixed Crafting width is 1872','private const int FixedRootWidth = 1872;' in layout)
ck('fixed Crafting height is 935','private const int FixedRootHeight = 935;' in layout)
ck('root width capped by fixed width','Math.Min(FixedRootWidth' in layout)
ck('root height capped by fixed height','Math.Min(FixedRootHeight' in layout)
ck('fixed surface centers horizontally','(screen.x - rootWidth) / 2' in layout)

# Inventory returns to native-size ItemStack rendering while still using much more width than old 8 cols.
ck('backpack uses thirteen proportional columns after PC101 supersession','public const int Columns = 13;' in bridge)
ck('backpack keeps four visible rows','public const int VisibleRows = 4;' in bridge)
ck('authored presenter covers 100-slot ceiling after PC101','public const int AuthoredRows = 8;' in bridge and 'AuthoredSlotCount = Columns * AuthoredRows' in bridge)
ck('XML inventory is 13 x 8 after PC101 supersession','name="inventory" rows="8" cols="13"' in xml)
ck('XML inventory seeds 60px proportional pitch after PC101','cell_width="60" cell_height="60"' in xml and '<item_stack name="0" controller="RebirthCraftingInventorySlot, RebirthUtils"/>' in xml)
ck('layout reserves height for four native-size rows','const int inventoryMinHeight = 310;' in layout)
ck('slot visual scaling uses live native ItemStack size','FallbackNativeCell = 75' in iscroll and 'slot.ViewComponent.Size.x' in iscroll and 'Vector3.one * uniformScale' in iscroll)
ck('slot palette remains dark charcoal without clobbering hover','new Color32(30, 30, 36, 255)' in slot and 'Never tint the ItemStack root' in slot)

# Selected Item uses same top context but has no close-X chrome.
ck('Selected Item context remains top sibling','name="rebirthCraftingItemContext"' in xml and 'controller="RebirthCraftingItemContext, RebirthUtils"' in xml)
ck('Selected Item close X removed','btnRebirthCraftingItemContextClose' not in xml)

# Top-right status text visual centering.
ck('Day text aligned to navigation baseline','name="rebirthCraftingStatusDay"' in xml and 'pos="28,-18"' in xml)
ck('Time text aligned to navigation baseline','name="rebirthCraftingStatusTime"' in xml and 'pos="26,-18"' in xml)
ck('Temperature text aligned to navigation baseline','name="rebirthCraftingStatusTemperature"' in xml and 'pos="29,-18"' in xml)

# Craft time / batch size typography and alignment.
ck('Craft Time title enlarged and baseline-corrected','name="rebirthCraftingSelectedRecipeTimeTitle"' in xml and 'pos="446,-153" width="116" height="30" font_size="20"' in xml)
ck('Craft time value centered over selector column','name="rebirthCraftingSelectedRecipeTimeValue"' in xml and 'pos="571,-150" width="90" height="30" font_size="20"' in xml and 'justify="left"' in xml)
ck('Batch Size title enlarged and lowered to selector center','name="rebirthCraftingBatchTitle"' in xml and 'pos="446,-194" width="116" height="30" font_size="20"' in xml)
ck('Batch selector shares time-value x column','name="rebirthCraftingRecipeCraftCount" width="165" height="30" pos="551,-176"' in xml)
ck('Batch numeric input enlarged','name="count_input"' in xml and 'font_size="20"' in xml)

for name,text in [('layout',layout),('bridge',bridge),('inventory scroll',iscroll),('slot',slot),('command bridge',cmd),('catalogue',cat)]:
    ck(name+' brace balance',text.count('{')==text.count('}'),f"{text.count('{')}/{text.count('}')}")
    ck(name+' paren balance',text.count('(')==text.count(')'),f"{text.count('(')}/{text.count(')')}")

passed=sum(1 for _,ok,_ in checks if ok)
failed=[x for x in checks if not x[1]]
print(f'PC098 personal crafting scroll/fixed-size/inventory/alignment validation: {passed} PASS / {len(failed)} FAIL')
for n,_,d in failed: print('FAIL -',n,d)
sys.exit(1 if failed else 0)
