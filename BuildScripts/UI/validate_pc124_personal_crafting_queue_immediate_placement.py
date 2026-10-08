from pathlib import Path
import re, sys, xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
queue = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs'
queue_entry = ROOT / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs'
windows = ROOT / 'Config/XUi_InGame/windows.xml'
templates = ROOT / 'Config/XUi_InGame/templates.xml'

checks=[]
def check(name, cond): checks.append((name, bool(cond)))

for path in (windows, templates):
    try:
        ET.parse(path)
        check('XML parses: ' + path.name, True)
    except Exception:
        check('XML parses: ' + path.name, False)

q = queue.read_text(encoding='utf-8')
qe = queue_entry.read_text(encoding='utf-8')

check('PC124 documents deferred XUi Position activation hazard', 'XUiView.Position is deferred' in q and 'IsVisible immediately activates the GameObject' in q)
check('PC124 flushes staged queue position before first reveal', 'entry.ViewComponent.TryUpdatePosition();' in q)
check('PC124 uses narrow position flush rather than full view redraw', 'TryUpdatePosition is deliberately narrow' in q and 'entry.ViewComponent.UpdateData();' not in q)
check('PC124 placement trace exists for first reveal diagnostics', '[REBIRTH Crafting QueuePlacement]' in q and 'positionDirtyFlush=True' in q)

flush_pos = q.find('entry.ViewComponent.TryUpdatePosition();')
visible_pos = q.find('SetVisible(entry, intersectsViewport);', flush_pos)
check('PC124 staged transform flush occurs before SetVisible reveal', flush_pos >= 0 and visible_pos > flush_pos)

refresh_pos = q.rfind('entry.RefreshPresentationNow();', 0, flush_pos)
check('PC124 presentation fields are staged before transform flush', refresh_pos >= 0 and refresh_pos < flush_pos)

check('PC124 keeps position assignment before reveal', 'entry.ViewComponent.Position = desiredPosition;' in q and q.find('entry.ViewComponent.Position = desiredPosition;') < flush_pos)
check('PC124 keeps card size staging before reveal', 'entry.ViewComponent.Size = new Vector2i(cardWidth, CardHeight);' in q and q.find('entry.ViewComponent.Size = new Vector2i(cardWidth, CardHeight);') < flush_pos)
check('PC124 does not add another queue redraw pass', q.count('MaintainEntryVisibilityAndPosition();') <= 4)
check('PC124 keeps diff-only visible queue presentation', 'entry.SyncPresentationAfterNativeQueuePass();' in q)
check('PC124 keeps single native active RecipeStack tick', 'bool nativeTick = HasRecipeForPresentation && IsCrafting;' in qe and 'if (nativeTick)' in qe)
check('PC124 keeps native cancel/refund path', 'ForceCancel();' in qe and 'SyncVisiblePresentationAfterNativeMutation' in qe)
check('PC124 keeps SoftClip viewport behavior', 'UIDrawCall.Clipping.SoftClip' in q)
check('PC124 queue braces balanced', q.count('{') == q.count('}'))
check('PC124 queue-entry braces balanced', qe.count('{') == qe.count('}'))

for i,(name,ok) in enumerate(checks,1):
    print(f'{i:02d}. {"PASS" if ok else "FAIL"}: {name}')
passed=sum(ok for _,ok in checks)
print(f'\nResult: {passed}/{len(checks)} checks passed')
if passed != len(checks): sys.exit(1)
