#!/usr/bin/env python3
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
WINDOWS = ROOT / 'Config' / 'XUi_InGame' / 'windows.xml'
TEMPLATES = ROOT / 'Config' / 'XUi_InGame' / 'templates.xml'
QUEUE = ROOT / 'Scripts' / 'Crafting' / 'UI' / 'PersonalCrafting' / 'XUiC_RebirthCraftingQueue.cs'
ENTRY = ROOT / 'Scripts' / 'Crafting' / 'UI' / 'PersonalCrafting' / 'XUiC_RebirthCraftingQueueEntry.cs'

checks = []
def check(name, condition, detail=''):
    checks.append((name, bool(condition), detail))

windows = WINDOWS.read_text(encoding='utf-8')
templates = TEMPLATES.read_text(encoding='utf-8')
queue = QUEUE.read_text(encoding='utf-8')
entry = ENTRY.read_text(encoding='utf-8')

for path in (WINDOWS, TEMPLATES):
    try:
        ET.parse(path)
        check(f'XML parses: {path.name}', True)
    except Exception as exc:
        check(f'XML parses: {path.name}', False, str(exc))

# The native queue hierarchy must remain the lightweight rect hierarchy recovered in PC109.
match = re.search(r'<rect name="rebirthCraftingQueueRowsHost"[^>]*>(.*?)</rect>', windows, re.S)
check('Queue rows host remains XUi rect', match is not None)
check('50 native queue entry children remain authored', windows.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot') == 50,
      str(windows.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot')))
check('No obsolete top visual mask', 'rebirthCraftingQueueTopClipMask' not in windows)
check('No obsolete bottom visual mask', 'rebirthCraftingQueueBottomClipMask' not in windows)
check('Queue track authored 16px', re.search(r'name="rebirthCraftingQueueScrollTrack"[^>]*width="16"', windows) is not None)
check('Queue thumb authored 12px', re.search(r'name="rebirthCraftingQueueScrollThumb"[^>]*width="12"', windows) is not None)
check('Inventory track remains 16px', re.search(r'name="rebirthCraftingInventoryScrollTrack"[^>]*width="16"', windows) is not None)
check('Inventory thumb remains 12px', re.search(r'name="rebirthCraftingInventoryScrollThumb"[^>]*width="12"', windows) is not None)

# Runtime clip is a real NGUI panel on the existing rows-host GameObject, not an XUi hierarchy node.
check('Queue declares runtime UIPanel', 'private UIPanel rowsClipPanel;' in queue)
check('Queue attaches UIPanel to rows host GameObject', 'hostObject.AddComponent<UIPanel>()' in queue)
check('Queue uses SoftClip', 'UIDrawCall.Clipping.SoftClip' in queue)
check('Queue configures real clip region', 'rowsClipPanel.baseClipRegion' in queue and 'new Vector4(hostWidth / 2f, -hostHeight / 2f, hostWidth, hostHeight)' in queue)
check('Queue registers existing widgets with runtime panel', 'GetComponentsInChildren<UIWidget>(true)' in queue and 'widget.enabled = false;' in queue and 'widget.enabled = true;' in queue)
check('Queue no longer uses fake clip-mask method', 'UpdateClipMasks' not in queue)
check('Active cards stay visible and rely on clipping', 'SetVisible(entry, true);' in queue and 'bool visible = bottom >' not in queue)
check('Queue trace reports clip state', ' + " clip=" +' in queue)
check('Runtime queue scrollbar still reasserts 16/12', 'ScrollbarTrackWidth = 16' in queue and 'ScrollbarThumbWidth = 12' in queue)

# Cancel keeps base XUiC_RecipeStack contract while rendering a literal label.
queue_template = re.search(r'<rebirth_personal_crafting_queue_entry>(.*?)</rebirth_personal_crafting_queue_entry>', templates, re.S)
chunk = queue_template.group(1) if queue_template else ''
check('Native background cancel hit sprite remains', re.search(r'<sprite[^>]*name="background"[^>]*on_press="true"', chunk) is not None)
check('Native child named cancel remains XUiV_Sprite-compatible', re.search(r'<sprite[^>]*name="cancel"[^>]*color="0,0,0,0"', chunk) is not None)
check('Literal X label exists', re.search(r'<label[^>]*name="rebirthQueueCancelLabel"[^>]*text="X"', chunk) is not None)
check('Queue-specific cancel no longer depends on icon sprite', 'name="cancel"' in chunk and 'ui_game_symbol_x' not in chunk)
check('Entry controller binds literal X label', 'GetChildById("rebirthQueueCancelLabel")' in entry)
check('Entry keeps native cancel sprite separately', 'nativeCancelSprite = GetChildById("cancel")' in entry)
check('Entry forces native cancel sprite transparent after base Update', 'sprite.Color = new Color32(0, 0, 0, 0);' in entry)
check('Entry forces visible label text X', 'label.Text = "X";' in entry)
check('Hover scale remains larger than 1', re.search(r'CancelHoverScale\s*=\s*1\.(?:2[0-9]|3[0-9])f', entry) is not None)
check('Cancel hit target still uses native base wiring', 'base.Init();' in entry and 'cancelHit.OnHover += CancelHit_OnHover;' in entry)

# Cheap structural C# sanity.
def balanced(source, left, right):
    count = 0
    in_string = False
    escape = False
    for ch in source:
        if in_string:
            if escape:
                escape = False
            elif ch == '\\':
                escape = True
            elif ch == '"':
                in_string = False
            continue
        if ch == '"':
            in_string = True
            continue
        if ch == left:
            count += 1
        elif ch == right:
            count -= 1
            if count < 0:
                return False
    return count == 0

check('Queue braces balanced', balanced(queue, '{', '}'))
check('Entry braces balanced', balanced(entry, '{', '}'))

failed = [x for x in checks if not x[1]]
for i, (name, ok, detail) in enumerate(checks, 1):
    suffix = f' ({detail})' if detail else ''
    print(f'{i:02d}. {"PASS" if ok else "FAIL"}: {name}{suffix}')
print(f'\nResult: {len(checks)-len(failed)}/{len(checks)} checks passed')
sys.exit(1 if failed else 0)
