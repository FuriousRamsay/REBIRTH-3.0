from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
path = root / 'Scripts/Survivor/UI/XUiC_RebirthExpandableBackpackScroll.cs'
s = path.read_text(encoding='utf-8')
checks=[]
def check(name, cond): checks.append((name, bool(cond)))

check('PC050 background blocker fix retained', 'DisableBlockingBackpackBackgroundInput();' in s and 'content.Find("backgroundMain")' in s)
check('full viewport scroll surface blocker method exists', 'DisableBlockingBackpackScrollSurfaceInput' in s)
check('root collider is explicitly disabled', 'Collider collider=ViewComponent.UiTransform.GetComponent<Collider>();' in s and 'collider.enabled=false;' in s)
check('root event wheel ownership is disabled', 'ViewComponent.EventOnScroll=false;' in s)
check('root controller is no longer wired as scroll event surface', 'WireScrollOnce(this);' not in s)
check('raw wheel fallback retained', 'PollMouseWheelFallback();' in s and 'if(!IsMouseOverBackpackArea())return;' in s)
check('root blocker disabled after wiring during Init', 'WireControls();\n        DisableBlockingBackpackScrollSurfaceInput();\n        RefreshGeometry(true);' in s)
check('root blocker disabled after wiring during Update', 'WireControls();\n        DisableBlockingBackpackScrollSurfaceInput();\n\n        bool visible=' in s)
check('root collider restored when backpack closes', 'RestoreBlockingBackpackScrollSurfaceInput();' in s and 'public override void OnClose()' in s)
check('root blocker diagnostic emitted', '[REBIRTH BackpackScroll] INPUT_SCROLL_SURFACE_DISABLED' in s)
check('item stack press forwarding retained', 'slot.ViewComponent.EventOnPress=true;' in s)
check('item stack drag forwarding retained', 'slot.ViewComponent.EventOnDrag=true;' in s)
check('interaction miss logging retained', 'INTERACTION_MISS' in s and 'DescribeHoveredObjectDetailed()' in s)
check('slot press diagnostics retained', 'SLOT_PRESS' in s)
check('slot drag diagnostics retained', 'SLOT_DRAG' in s)
check('source braces balanced', s.count('{') == s.count('}'))
check('source parens balanced', s.count('(') == s.count(')'))

failed=[n for n,ok in checks if not ok]
for n,ok in checks:
    print(('PASS' if ok else 'FAIL')+' | '+n)
print(f'PC051_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)}')
if failed: sys.exit(1)
