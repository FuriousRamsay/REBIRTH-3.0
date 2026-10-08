#!/usr/bin/env python3
from pathlib import Path
import re, sys
from lxml import etree

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
checks=[]
def ck(name, cond):
    checks.append((name, bool(cond)))
    print(('PASS' if cond else 'FAIL') + ' ' + name)

win=(root/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
tpl=(root/'Config/XUi_InGame/templates.xml').read_text(encoding='utf-8')
queue=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs').read_text(encoding='utf-8')
entry=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs').read_text(encoding='utf-8')
req=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingRequirements.cs').read_text(encoding='utf-8')
inv=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs').read_text(encoding='utf-8')

for rel in ['Config/XUi_InGame/windows.xml','Config/XUi_InGame/templates.xml']:
    try:
        etree.parse(str(root/rel)); ok=True
    except Exception: ok=False
    ck(rel+' parses', ok)

slots=re.findall(r'<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot(\d+)"/>', win)
ck('queue authors exactly 50 native slots', len(slots)==50 and set(map(int,slots))==set(range(50)))
ck('queue hard cap is 50', 'MaximumQueueEntries = 50' in queue)
ck('queue is two-column', 'private const int Columns = 2;' in queue)
ck('queue cards are compact', 'private const int CardHeight = 96;' in queue)
ck('queue capacity is dynamic by active count', 'bool needsScroll = active > visibleEntryCapacity;' in queue)
ck('queue scroll max uses active count', 'active - visibleEntryCapacity' in queue)
ck('empty native queue slots are hidden', '!entry.HasRecipeForPresentation' in queue and 'entry.ViewComponent.IsVisible = false;' in queue)
ck('queue visibility is maintained after native update', 'MaintainEntryVisibility();' in queue)
ck('entry exposes recipe presence', 'HasRecipeForPresentation => GetRecipe() != null' in entry)
ck('queue icon top-left full presentation size', 'const int iconSize = 54;' in entry and 'SetRect(GetChildById("itemIcon"), iconX, -7, iconSize, iconSize);' in entry)
ck('queue number upper-right', 'int indexX = width - pad - indexSize;' in entry)
ck('queue progress is compact near qty/time', 'int progressY = -64;' in entry and 'rebirthQueueQtyCaption' in entry and 'rebirthQueueTimeCaption' in entry)
ck('template compact height', 'width="220" height="96"' in tpl)

for ident in ['rebirthCraftingRequirementHaveLabel','rebirthCraftingRequirementHaveValue','rebirthCraftingRequirementNeedLabel','rebirthCraftingRequirementNeedValue']:
    ck('requirements runtime positions '+ident, f'SetRect(entry.GetChildById("{ident}")' in req)
ck('requirement values pinned to right edge', 'width - rightPad - valueWidth' in req)

ck('inventory native SetStacks only initializes once', inv.count('base.SetStacks(stackList);')==1 and 'if (!nativeProjectionInitialized)' in inv)
ck('inventory repeated updates are incremental', 'ApplyAuthoritativePresentation(stackList, false);' in inv)
ck('inventory uses visual fingerprints', 'projectedFingerprints' in inv and 'BuildFingerprint(ItemStack stack)' in inv)
ck('inventory refreshes only changed cells', 'if (presentationChanged)' in inv and 'slot.RefreshBindings();' in inv)
ck('inventory trace exposes changed-cell count', 'SetStacks changed=' in inv)

passed=sum(v for _,v in checks)
print(f'RESULT {passed}/{len(checks)} PASS')
sys.exit(0 if passed==len(checks) else 1)
