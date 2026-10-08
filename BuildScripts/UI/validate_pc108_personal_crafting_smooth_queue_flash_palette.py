from pathlib import Path
import sys, xml.etree.ElementTree as ET

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
checks=[]
def check(name, cond):
    checks.append((name,bool(cond)))
    print(('PASS' if cond else 'FAIL') + ' ' + name)

def text(rel): return (root/rel).read_text(encoding='utf-8')

w=text('Config/XUi_InGame/windows.xml')
t=text('Config/XUi_InGame/templates.xml')
q=text('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs')
qe=text('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs')
inv=text('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventory.cs')
scroll=text('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs')
slot=text('Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventorySlot.cs')
layout=text('Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs')

for rel in ['Config/XUi_InGame/windows.xml','Config/XUi_InGame/templates.xml']:
    try:
        ET.parse(root/rel); ok=True
    except Exception: ok=False
    check('xml parses '+rel, ok)

check('queue hard cap remains 50', 'MaximumQueueEntries = 50' in q)
check('queue scroll is pixel based', 'scrollTargetPixels' in q and 'MaxPixelOffset' in q and 'WheelPixels = 38f' in q)
check('queue no row/page firstVisible model', 'firstVisible' not in q)
check('queue smooth animation enabled', 'Mathf.Lerp(scrollPixels, scrollTargetPixels' in q)
check('queue scrollbar uses complete viewport height', 'SetRect(scrollHost, scrollX, -rowsTop, ScrollbarWidth, viewportHeight)' in q)
check('queue proxy represents full content height', 'Math.Max(viewportHeight, ContentHeight)' in q)
check('queue content is clipped for partial rows', '<panel name="rebirthCraftingQueueRowsHost"' in w and 'clipping="softclip"' in w)
check('queue cards tightened to 88px', 'CardHeight = 88' in q and 'height="88"' in t)
check('queue qty/progress/time moved upward', 'progressY = -58' in qe and 'rebirthQueueQtyCaption"), contentX, -40' in qe and 'rebirthQueueTimeCaption"), contentX, -68' in qe)
check('queue whole-tree refresh removed', 'Do not RefreshBindings() on the whole queue controller' in q)
check('queue count refresh isolated', 'capacityLabel.RefreshBindings();' in q)
check('queue renumber only updates index label', 'indexLabel.Text = (displayIndex + 1).ToString();' in qe)

check('inventory SetStacks burst coalescing enabled', 'ProjectionCoalesceSeconds = 0.035f' in inv and 'QueueProjection(stackList);' in inv and 'FlushPendingProjection(false);' in inv)
check('coalesce timer cannot starve on repeated notifications', 'if (firstRequest)' in inv and 'pendingProjectionDue = Time.realtimeSinceStartup + ProjectionCoalesceSeconds' in inv)
check('inventory still uses PC102 live native scaling', 'FallbackNativeCell = 75' in scroll and 'slot.ViewComponent.Size.x' in scroll and 'uniformScale' in scroll)
check('inventory scroll now animates', 'targetPixelOffset' in scroll and 'AnimateScroll(dt);' in scroll and 'Mathf.Lerp(pixelOffset, targetPixelOffset' in scroll)
check('inventory drag no longer snaps to row boundary', 'End on a row boundary' not in scroll and 'SetImmediatePixelOffset' in scroll)
check('normal inventory fill is darker', 'NormalFill = new Color32(58, 58, 65, 255)' in slot)
check('encumbered fill remains darker', 'EncumberedFill = new Color32(31, 31, 36, 255)' in slot)
check('native hover selection border is not recolored', 'GetChildById("background")' not in slot)
check('palette reapplied after slot binding refresh', 'rebirthSlot.ReapplyRebirthSlotPalette();' in inv)

check('toolbar ends at actual slot viewport', 'slotViewportRight = pad + pitch * 13' in layout and 'slotViewportRight - headerButton / 2' in layout)
check('toolbar order remains sort lock quickstack companions', layout.find('sortX =') < layout.find('SetRect(owner.GetChildById("btnRebirthCraftingInventorySort")') < layout.find('SetRect(owner.GetChildById("btnRebirthCraftingInventoryLock")') < layout.find('SetRect(owner.GetChildById("btnRebirthCraftingInventoryQuickStack")') < layout.find('SetRect(owner.GetChildById("btnRebirthCraftingInventoryCompanions")'))

check('expected-result icon outline hidden', 'rebirthCraftingOutcomeResultIconFrame' in w and 'rebirthCraftingOutcomeResultIconFrame" depth="3"' in w and 'visible="false"' in w[w.find('rebirthCraftingOutcomeResultIconFrame'):w.find('rebirthCraftingOutcomeResultIconFrame')+300])
check('selected-recipe icon outline hidden', 'visible="false"' in w[w.find('rebirthCraftingSelectedRecipeIconFrame'):w.find('rebirthCraftingSelectedRecipeIconFrame')+320])
check('selected-item icon outline hidden', 'visible="false"' in w[w.find('rebirthCraftingItemContextIconFrame'):w.find('rebirthCraftingItemContextIconFrame')+320])

passed=sum(1 for _,v in checks if v)
print(f'RESULT {passed}/{len(checks)} PASS')
sys.exit(0 if passed==len(checks) else 1)
