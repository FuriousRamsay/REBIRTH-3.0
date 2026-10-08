from pathlib import Path
import re, sys
root = Path(__file__).resolve().parents[2]
files = [
    root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs',
    root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs',
    root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs',
    root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs',
    root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs',
    root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingItemContext.cs',
    root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeCatalogue.cs',
    root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRecipeEntry.cs',
    root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs',
    root/'Scripts/Input/RebirthNativeControls.cs',
]
text = '\n'.join(p.read_text(encoding='utf-8') for p in files)
checks=[]
def ck(name, cond): checks.append((name, bool(cond)))
forbidden = {
    'Bag.MaxItemCount': r'\.MaxItemCount\b',
    'GUIWindowManager.CloseIfOpen': r'\bCloseIfOpen\s*\(',
    'XUi.dragAndDrop': r'\bdragAndDrop\b',
    'writable Selected property': r'\.Selected\s*=',
    'XUiView.ForceHide': r'\.ForceHide\b',
    'controller IsOpen assignment': r'(?m)^\s*IsOpen\s*=',
    'manual InputStyleChanged': r'\bInputStyleChanged\s*\(',
    'XUiController.IsDormant': r'\.IsDormant\b',
    'XUi.currentWorkstation': r'\.currentWorkstation\b',
    'FindWindowGroupByName assigned to XUiWindowGroup': r'XUiWindowGroup\s+\w+\s*=\s*[^;]*FindWindowGroupByName',
    'AlwaysUpdate override method/property': r'override\s+bool\s+AlwaysUpdate\b',
}
for name, pattern in forbidden.items(): ck('forbidden absent: '+name, not re.search(pattern, text))
ck('native CarryCapacity drives unencumbered count', 'PassiveEffects.CarryCapacity' in (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingInventoryBridge.cs').read_text())
ck('inventory uses bridge unencumbered count', (root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs').read_text().count('GetUnencumberedSlotCount(xui)') >= 2)
ck('selection uses method calls', text.count('.Selected(') >= 5 or text.count('Selected(') >= 7)
ck('navigation uses IsWindowOpen', 'IsWindowOpen("crafting")' in (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs').read_text())
ck('navigation uses Close', 'windowManager.Close("crafting")' in (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs').read_text())
ck('paging lookup uses XUiController', 'XUiController group = xui.FindWindowGroupByName("windowpaging")' in (root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthPersonalCrafting.cs').read_text())
ck('toolbelt lookup uses XUiController', 'XUiController toolbelt = owner.xui.FindWindowGroupByName("toolbelt")' in (root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs').read_text())
ck('native controls lookup uses XUiController', 'XUiController group = player.PlayerUI.xui.FindWindowGroupByName("crafting")' in (root/'Scripts/Input/RebirthNativeControls.cs').read_text())
ck('AlwaysUpdate assignments retained', text.count('AlwaysUpdate = true;') == 2)
for name, ok in checks: print(('PASS' if ok else 'FAIL')+' - '+name)
passed=sum(ok for _,ok in checks); failed=len(checks)-passed
print(f'PC089 b259 API contract cleanup validation: {passed} PASS / {failed} FAIL')
sys.exit(1 if failed else 0)
