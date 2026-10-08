from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('.')

def read(rel):
    p=root/rel
    if not p.exists():
        print('FAIL missing '+rel); raise SystemExit(1)
    return p.read_text(encoding='utf-8',errors='ignore')

xml=read('Config/XUi_InGame/windows.xml')
bridge=read('Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs')
inv=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs')
scroll=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs')
slot=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs')
ctx=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs')
layout=read('Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs')
entry=read('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs')

checks=[]
def ck(name, cond):
    checks.append((name,bool(cond)))
    print(('PASS ' if cond else 'FAIL ')+name)

ET.parse(root/'Config/XUi_InGame/windows.xml')
ck('13 columns x 4 visible rows', 'public const int Columns = 13;' in bridge and 'public const int VisibleRows = 4;' in bridge)
ck('104 authored presenter cells cover 100 physical ceiling', 'public const int AuthoredRows = 8;' in bridge and 'AuthoredSlotCount = Columns * AuthoredRows' in bridge)
ck('XML grid is 13x8', 'name="inventory" rows="8" cols="13"' in xml)
ck('slot pitch derives from horizontal space', 'int pitchByWidth = Math.Max(1, availableW / Columns);' in scroll)
ck('whole stock slot uniformly scales from live native size', 'slot.ViewComponent.Size.x' in scroll and 'slot.ViewComponent.Size.y' in scroll and 'Vector3.one * uniformScale' in scroll)
ck('scrollbar sits immediately after fitted grid', 'viewportWidth + 2' in scroll)
ck('hidden presenter cells collapse and deactivate', 'UiTransform.localScale = UnityEngine.Vector3.zero;' in inv and 'gameObject.SetActive(authoritative)' in inv and 'slot.ViewComponent.IsVisible = authoritative;' in inv)
ck('nonphysical cells never become backend slots', 'if (bag == null || slotNumber < 0 || slotNumber >= physical)' in inv)
ck('dark palette does not tint root hover layer', 'Never tint the ItemStack root' in slot and 'IsPointerOverSlot()' in slot)
ck('hover geometry diagnostic exists', '[REBIRTH Crafting InventoryHover]' in scroll and 'colliders=' in scroll and 'native=' in scroll and 'actual=' in scroll and 'target=' in scroll)
ck('inventory geometry diagnostic reports capacity boundary', 'unencumbered=' in scroll and 'authored=' in scroll and 'columns=' in scroll)
ck('inventory toolbar order is sort lock quickstack companions', xml.find('btnRebirthCraftingInventorySort') < xml.find('btnRebirthCraftingInventoryLock') < xml.find('btnRebirthCraftingInventoryQuickStack') < xml.find('btnRebirthCraftingInventoryCompanions'))
ck('runtime toolbar order matches XML', 'int companionsX' in layout and 'int quickStackX = companionsX - 44;' in layout and 'int lockX = quickStackX - 44;' in layout and 'int sortX = lockX - 44;' in layout)
ck('selected item is not cleared when scrolled out of viewport', 'contextPreserved=true' in ctx and 'visibleInInventory=' in ctx and 'HideContext(true, true);\n            return;\n        }\n\n        int fingerprint' not in ctx)
ck('slot click no longer snaps inventory row', 'scroll.EnsureSlotVisible(SlotNumber)' not in slot)
ck('item selection no longer snaps inventory row', 'scroll?.EnsureSlotVisible(slot.SlotNumber);' not in ctx)
ck('item action list uses recipe strip baseline', 'name="rebirthCraftingItemActionList" pos="14,-198" width="702" height="34"' in xml)
ck('item action backgrounds are white outline style', xml.count('default_background_color="240,240,244,255"') >= 5 and xml.count('fillcenter="false" on_press="true" on_hover="true"') >= 5)
ck('item action labels center like recipe buttons', xml.count('text="{actionname}" font_size="17" color="{statuscolor}" justify="center"') >= 5)
ck('unused item actions are hidden instead of overflowing', 'bool active = actionName != null && !string.IsNullOrWhiteSpace(actionName.Text);' in ctx and 'SetControllerVisible(entry, active);' in ctx)
ck('item context expands through former requirements area', 'int itemContextHeight = detailsHeight + ZoneGap + requirementsHeight;' in layout and 'parentSize.y + (requirementsHeight > 0 ? 10 + requirementsHeight : 0)' in ctx)
ck('requirements remain suppressed in item context', 'SetRequirementsVisible(false);' in ctx)
ck('batch title lowered further', 'name="rebirthCraftingBatchTitle" depth="4" pos="446,-194"' in xml)
ck('day aligned to nav text baseline', 'name="rebirthCraftingStatusDay" depth="7" pos="28,-18"' in xml and 'font_size="18"' in xml)
ck('time aligned to nav text baseline', 'name="rebirthCraftingStatusTime" depth="7" pos="26,-18"' in xml and 'font_size="18"' in xml)
ck('temperature aligned to nav text baseline', 'name="rebirthCraftingStatusTemperature" depth="7" pos="29,-18"' in xml and 'font_size="18"' in xml)
ck('recipe status text exact-center aligned', '(height - statusHeight) / 2' in entry and xml.count('name="rebirthCraftingRecipeStatus" depth="5" pos="220,-7"') >= 11)

for n,t in [('bridge',bridge),('inventory',inv),('scroll',scroll),('slot',slot),('context',ctx),('layout',layout),('entry',entry)]:
    ck(n+' brace balance',t.count('{')==t.count('}'))

failed=[n for n,ok in checks if not ok]
print(f'PC101 personal crafting proportional inventory/context polish: {len(checks)-len(failed)} PASS / {len(failed)} FAIL')
if failed:
    for n in failed: print('FAIL - '+n)
raise SystemExit(1 if failed else 0)
