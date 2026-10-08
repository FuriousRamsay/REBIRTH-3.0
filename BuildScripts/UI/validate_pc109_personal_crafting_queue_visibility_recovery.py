#!/usr/bin/env python3
from pathlib import Path
import sys, xml.etree.ElementTree as ET
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path('.')
checks=[]
def ck(name, cond):
    checks.append((name,bool(cond)))
    print(('PASS' if cond else 'FAIL')+': '+name)

w=(root/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
q=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs').read_text(encoding='utf-8')
e=(root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs').read_text(encoding='utf-8')
ET.parse(root/'Config/XUi_InGame/windows.xml')
ck('queue rows host restored to rect', '<rect name="rebirthCraftingQueueRowsHost"' in w)
ck('queue rows host is not clipping panel', '<panel name="rebirthCraftingQueueRowsHost"' not in w)
ck('50 queue entry presenters remain authored', w.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot') == 50)
ck('custom queue scroll track authored', 'name="rebirthCraftingQueueScrollTrack"' in w)
ck('custom queue scroll thumb authored', 'name="rebirthCraftingQueueScrollThumb"' in w)
ck('obsolete queue native scroll host removed', 'rebirthCraftingQueueScrollHost' not in w)
ck('obsolete queue native scroll view removed', 'rebirthCraftingQueueScrollView' not in w)
ck('queue keeps max capacity 50', 'MaximumQueueEntries = 50' in q)
ck('queue uses continuous pixel target', 'scrollTargetPixels' in q and 'WheelPixels' in q)
ck('queue custom thumb drag implemented', 'Thumb_OnDrag' in q and 'dragStartOffset' in q)
ck('queue custom track click implemented', 'Track_OnPress' in q)
ck('queue scrollbar height uses viewport height', ('SetRect(scrollTrack, scrollX, -rowsTop, 16, viewportHeight)' in q) or ('SetRect(scrollTrack, scrollX, -rowsTop, ScrollbarTrackWidth, viewportHeight)' in q))
ck('queue cards laid out from runtime entries', 'GetRuntimeEntries(this)' in q and 'denseDisplayOrder' in q)
ck('queue entry local refresh only', 'RefreshPresentationNow' in q and 'entry.RefreshPresentationNow()' in q)
ck('queue runtime trace added', '[REBIRTH Crafting QueueTrace]' in q)
ck('queue does not recursively RefreshBindings on controller', 'IsDirty = true;\n        RefreshBindings();' not in q)
ck('entry exposes local refresh helper', 'public void RefreshPresentationNow()' in e)
ck('entry native base update retained', 'base.Update(dt);' in e)
# rough brace sanity
for name,text in [('queue',q),('entry',e)]:
    ck(name+' braces balanced', text.count('{')==text.count('}'))
passed=sum(v for _,v in checks)
print(f'RESULT: {passed}/{len(checks)} PASS')
sys.exit(0 if passed==len(checks) else 1)
