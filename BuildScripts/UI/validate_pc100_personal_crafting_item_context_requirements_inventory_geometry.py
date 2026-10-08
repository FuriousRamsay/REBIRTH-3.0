from pathlib import Path
import sys, xml.etree.ElementTree as ET

root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('.')
xml=(root/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
item=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs').read_text(encoding='utf-8')
req=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRequirementEntry.cs').read_text(encoding='utf-8')
bridge=(root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs').read_text(encoding='utf-8')
scroll=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs').read_text(encoding='utf-8')
slot=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs').read_text(encoding='utf-8')

checks=[]
def ck(name, cond): checks.append((name, bool(cond)))

ET.parse(root/'Config/XUi_InGame/windows.xml')

ck('craft time title lowered to visual baseline',
   'name="rebirthCraftingSelectedRecipeTimeTitle"' in xml and 'pos="446,-153"' in xml)
ck('batch title lowered to selector centerline',
   'name="rebirthCraftingBatchTitle"' in xml and 'pos="446,-194"' in xml)

ck('item summary matches recipe category typography',
   'name="rebirthCraftingItemContextSummary"' in xml and 'font_size="22"' in xml)
ck('item description matches recipe description typography',
   'name="rebirthCraftingItemContextDescription"' in xml and 'font_size="18"' in xml)
ck('item action row shares recipe baseline',
   'name="rebirthCraftingItemActionList" pos="14,-198" width="702" height="34"' in xml)
ck('item action labels use recipe action font size',
   xml.count('text="{actionname}" font_size="17"') >= 5)
ck('item action count hidden',
   'name="rebirthCraftingItemContextActionCount"' in xml and 'visible="false"' in xml and
   'actionCount.Text = string.Empty;' in item)
ck('item context hides requirements',
   'SetRequirementsVisible(false);' in item and 'SetRequirementsVisible(true);' in item and
   'requirementsRegion = owner != null ? owner.GetChildById("rebirthCraftingRequirementsRegion") : null;' in item)

ck('requirement HAVE title/value split',
   xml.count('name="rebirthCraftingRequirementHaveLabel"') >= 6 and
   xml.count('name="rebirthCraftingRequirementHaveValue"') >= 6)
ck('requirement NEED title/value split',
   xml.count('name="rebirthCraftingRequirementNeedLabel"') >= 6 and
   xml.count('name="rebirthCraftingRequirementNeedValue"') >= 6)
ck('requirement titles are white',
   'haveTitleLabel.Color = new Color32(240, 240, 242, 255);' in req and
   'needTitleLabel.Color = new Color32(240, 240, 242, 255);' in req)
ck('HAVE value is red/green by sufficiency',
   'haveValueLabel.Color = value.HasEnough' in req and
   'new Color32(112, 196, 126, 255)' in req and
   'new Color32(228, 92, 92, 255)' in req)
ck('NEED value is white',
   'needValueLabel.Color = new Color32(240, 240, 242, 255);' in req)

ck('inventory uses thirteen proportional columns after PC101 supersession',
   'public const int Columns = 13;' in bridge and
   'public const int AuthoredRows = 8;' in bridge and
   'name="inventory" rows="8" cols="13"' in xml)
ck('inventory retains four visible rows',
   'public const int VisibleRows = 4;' in bridge)
ck('inventory uniformly scales complete native ItemStack inside horizontal-fit pitch',
   'private const int FallbackNativeCell = 75;' in scroll and 'private const int CellGap = 2;' in scroll and
   'cell_width="60" cell_height="60"' in xml and 'Vector3.one * uniformScale' in scroll and 'slot.ViewComponent.Size.x' in scroll and 'grid.CellWidth = effectivePitch;' in scroll)
ck('slot palette is reasserted after native refreshes',
   'nextPaletteRefresh' in slot and 'ApplyRebirthSlotPalette();' in slot and
   'new Color32(30, 30, 36, 255)' in slot and 'Never tint the ItemStack root' in slot)

failed=[name for name,ok in checks if not ok]
for name,ok in checks:
    print(('PASS ' if ok else 'FAIL ')+name)
print(f'PC100 personal crafting item context/requirements/inventory geometry: {len(checks)-len(failed)} PASS / {len(failed)} FAIL')
sys.exit(1 if failed else 0)
