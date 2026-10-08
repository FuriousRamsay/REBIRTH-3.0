#!/usr/bin/env python3
from pathlib import Path
import re, sys, xml.etree.ElementTree as ET

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('.')
checks = []

def ck(name, cond):
    cond = bool(cond)
    checks.append((name, cond))
    print(('PASS' if cond else 'FAIL') + ': ' + name)

q_path = root / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueue.cs'
e_path = root / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingQueueEntry.cs'
i_path = root / 'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingInventoryScroll.cs'
w_path = root / 'Config/XUi_InGame/windows.xml'
t_path = root / 'Config/XUi_InGame/templates.xml'

q = q_path.read_text(encoding='utf-8')
e = e_path.read_text(encoding='utf-8')
i = i_path.read_text(encoding='utf-8')
w = w_path.read_text(encoding='utf-8')
t = t_path.read_text(encoding='utf-8')

ET.parse(w_path)
ET.parse(t_path)
ck('windows XML parses', True)
ck('templates XML parses', True)

# Native hierarchy / capacity safety.
ck('native queue host remains lightweight rect', '<rect name="rebirthCraftingQueueRowsHost"' in w and '<panel name="rebirthCraftingQueueRowsHost"' not in w)
ck('all 50 native queue entries retained', w.count('<rebirth_personal_crafting_queue_entry name="rebirthCraftingQueueSlot') == 50)
ck('queue maximum capacity remains 50', 'MaximumQueueEntries = 50' in q)
ck('native base queue update retained', 'base.Update(dt);' in q)
ck('native base recipe stack update retained', 'base.Update(dt);' in e)

# Partial-card scrolling behavior.
ck('partial cards use viewport intersection instead of full-card cutoff', 'bottom > 0.01f && top < viewportHeight - 0.01f' in q and 'top >= -0.01f && bottom <= viewportHeight + 0.01f' not in q)
ck('top and bottom visual clip masks authored', 'name="rebirthCraftingQueueTopClipMask"' in w and 'name="rebirthCraftingQueueBottomClipMask"' in w)
ck('clip masks updated from live viewport geometry', 'UpdateClipMasks(rowsTop, hostWidth);' in q and 'CardHeight + BottomPad + 4' in q)
ck('queue card foreground layers removed so masks cover complete cards', 'foregroundlayer="true"' not in t[t.find('<rebirth_personal_crafting_queue_entry>'):t.find('</rebirth_personal_crafting_queue_entry>')])

# Single frame presentation pass while wheel interpolation is active.
m = re.search(r'private void AnimateScroll\(float dt\)(.*?)\n    private void ApplyScrollPosition', q, re.S)
ck('AnimateScroll no longer repaints queue inside interpolation', m is not None and 'ApplyScrollPosition();' not in m.group(1) and 'MaintainEntryVisibilityAndPosition();' not in m.group(1) and 'UpdateScrollbar();' not in m.group(1))
ck('Update performs one normal geometry pass after interpolation', 'AnimateScroll(dt);\n        MaintainEntryVisibilityAndPosition();\n        UpdateScrollbar();' in q)

# Flash reduction: no periodic full binding refresh from progress.
ck('old 50ms binding-refresh loop removed', 'BindingRefreshSeconds' not in e and 'nextBindingRefresh' not in e and 'lastProgressBucket' not in e)
ck('progress fill updated directly each frame', 'progressFill.Fill = Mathf.Clamp01(progress);' in e)
ck('full custom binding refresh limited to discrete recipe/crafting state', 'recipeIdentity == lastRecipeIdentity && craftingNow == lastCrafting' in e)

# Cancel presentation / interaction.
queue_template = t[t.find('<rebirth_personal_crafting_queue_entry>'):t.find('</rebirth_personal_crafting_queue_entry>')]
ck('native cancel child names retained', 'name="background"' in queue_template and 'name="cancel"' in queue_template and 'XUiC_RebirthCraftingQueueEntry : XUiC_RecipeStack' in e)
ck('cancel hit target is transparent instead of a red square', 'name="background" pos="187,-31" width="26" height="26"' in queue_template and 'color="0,0,0,0"' in queue_template and 'new Color32(0, 0, 0, 0)' in e)
ck('cancel X remains visibly rendered', 'name="cancel" pos="191,-35" width="18" height="18"' in queue_template and 'ui_game_symbol_x' in queue_template)
ck('cancel X grows smoothly on hover', 'CancelHoverScale = 1.28f' in e and 'cancelHit.OnHover += CancelHit_OnHover;' in e and 'Vector3.one * cancelScale' in e)
ck('clipped cancel hit target cannot steal input', 'SetCancelHitEnabled' in q and 'cancelHit.ViewComponent.Enabled = HasRecipeForPresentation && cancelInputAllowed;' in e)
ck('native cancel hit target remains press-enabled', 'on_press="true"' in queue_template and 'on_hover="true"' in queue_template)

# Exact scrollbar parity: Recipes is the authority requested by the user.
def xml_width(name):
    pos = w.find(f'name="{name}"')
    if pos < 0:
        return None
    chunk = w[pos:pos+360]
    m = re.search(r'width="(\d+)"', chunk)
    return int(m.group(1)) if m else None

recipe_track = xml_width('rebirthCraftingRecipeScrollTrack')
recipe_thumb = xml_width('rebirthCraftingRecipeScrollThumb')
inv_track = xml_width('rebirthCraftingInventoryScrollTrack')
inv_thumb = xml_width('rebirthCraftingInventoryScrollThumb')
queue_track = xml_width('rebirthCraftingQueueScrollTrack')
queue_thumb = xml_width('rebirthCraftingQueueScrollThumb')
ck('recipe scrollbar authority is 16px track / 12px thumb', recipe_track == 16 and recipe_thumb == 12)
ck('inventory XML matches recipe scrollbar exactly', (inv_track, inv_thumb) == (recipe_track, recipe_thumb))
ck('queue XML matches recipe scrollbar exactly', (queue_track, queue_thumb) == (recipe_track, recipe_thumb))
ck('inventory runtime reasserts recipe scrollbar widths', 'ScrollbarTrackWidth = 16' in i and 'ScrollbarThumbWidth = 12' in i and 'track.ViewComponent.Size = new Vector2i(ScrollbarTrackWidth, viewportHeight);' in i and 'ScrollbarThumbWidth, thumbHeight' in i)
ck('queue runtime reasserts recipe scrollbar widths', 'ScrollbarTrackWidth = 16' in q and 'ScrollbarThumbWidth = 12' in q and 'SetRect(scrollTrack, trackX, trackY, ScrollbarTrackWidth, viewportHeight)' in q and 'ScrollbarThumbWidth, thumbHeight' in q)
ck('queue trace now reports runtime track/thumbnail widths', 'scrollTrack.ViewComponent.Size.x + "x"' in q and 'thumbW=' in q)

ck('queue braces balanced', q.count('{') == q.count('}'))
ck('entry braces balanced', e.count('{') == e.count('}'))
ck('inventory-scroll braces balanced', i.count('{') == i.count('}'))

passed = sum(v for _, v in checks)
print(f'RESULT: {passed}/{len(checks)} PASS')
sys.exit(0 if passed == len(checks) else 1)
