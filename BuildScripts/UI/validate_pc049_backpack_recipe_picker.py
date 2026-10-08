from pathlib import Path
import xml.etree.ElementTree as ET
import sys

root = Path(__file__).resolve().parents[2]
windows = (root / 'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
templates = (root / 'Config/XUi_InGame/templates.xml').read_text(encoding='utf-8')
backpack = (root / 'Scripts/Survivor/UI/XUiC_RebirthExpandableBackpackScroll.cs').read_text(encoding='utf-8')
pager = (root / 'Scripts/UI/XUiC_RebirthPagerScrollbar.cs').read_text(encoding='utf-8')

checks = []
def check(name, cond):
    checks.append((name, bool(cond)))

for path in [root / 'Config/XUi_InGame/windows.xml', root / 'Config/XUi_InGame/templates.xml']:
    try:
        ET.parse(path)
        check(f'XML parses: {path.name}', True)
    except Exception:
        check(f'XML parses: {path.name}', False)

# Color picker tightening.
check('picker window 440x365', 'name="rebirthVitalColorPickerWindow" anchor="Center" depth="35" pos="-220,183" width="440" height="365"' in windows)
check('picker body raised to -60', 'name="rebirthVitalPickerColorBody" pos="24,-60"' in windows)
check('preview aligned to picker top', 'name="rebirthVitalPickerPreviewRect" pos="302,-60"' in windows)
check('picker divider tightened', 'pos="12,-305" width="416" height="2"' in windows)
check('picker buttons tightened', 'name="btnApplyRebirthVitalColor" depth="6" pos="20,-317"' in windows and 'name="btnCancelRebirthVitalColor" depth="6" pos="250,-317"' in windows)

# Recipe geometry and native hierarchy.
check('recipe grid restored to 402 width', 'name="recipes" depth="2" rows="8" cols="1" pos="3,-98" width="402" height="368" cell_width="402"' in windows)
check('recipe template 402 width', '/templates/recipe_entry/rect" name="width">402</setattribute>' in templates)
check('recipe label width restored', '/templates/recipe_entry/rect/label[@name=\'Name\']" name="width">286</setattribute>' in templates)
check('recipe unlock icon restored', '/templates/recipe_entry/rect/sprite[@name=\'Unlocked\']" name="pos">371,-10</setattribute>' in templates)
check('crafting scrollbar stays at x405', 'name="rebirthCraftingPagerScroll" controller="RebirthPagerScrollbar, RebirthUtils" pos="405,-98" width="20" height="368"' in windows)
check('PC048 transparent track interceptor removed', 'rebirthCraftingPagerScrollTrackInput' not in windows)
check('no smooth-scroll reparenting', 'RebirthRecipeSmoothScroll' not in windows)

# Recipe runtime behavior + diagnostics.
check('raw crafting pointer polling', 'PollCraftingTrackRawInput' in pager and 'Input.GetMouseButtonDown(0)' in pager and 'Input.GetMouseButton(0)' in pager)
check('full root rect fallback', 'transform.TransformPoint(new Vector3(size.x, -size.y, 0f))' in pager)
check('does not use child collider as track bounds', 'GetComponentsInChildren<Collider>(true)' not in pager[pager.find('private static bool TryGetControllerScreenRect'):])
check('periodic category/range refresh', 'nextCraftingForcedRefreshTime = Time.realtimeSinceStartup + 0.15f' in pager and 'SyncFromPager(true);' in pager)
check('range debug logging', '[REBIRTH RecipeScroll] RANGE' in pager)
check('track ready debug logging', '[REBIRTH RecipeScroll] TRACK_READY' in pager)
check('track input debug logging', '[REBIRTH RecipeScroll] TRACK_INPUT' in pager)

# Backpack: restore known-good pre-PC045 input ownership while keeping wheel-area isolation.
check('backpack wrapper input contract restored', 'name="rebirthExpandableBackpackScroll" pos="18,-2" width="568" height="301" on_scroll="true" gamepad_selectable="true" snap="false" use_selection_box="true"' in windows)
check('recursive native controller wiring restored', 'WireScrollRecursive(inventory);' in backpack and 'WireScrollRecursive(backpack.itemControllers[i]);' in backpack)
check('wheel fallback stays backpack-scoped', 'if(!IsMouseOverBackpackArea())return;' in backpack)
check('late press flag mutation removed', 'slot.ViewComponent.EventOnPress=true' not in backpack)
check('late drag flag mutation removed', 'slot.ViewComponent.EventOnDrag=true' not in backpack)
check('automatic interaction miss logging retained', 'INTERACTION_MISS' in backpack)
check('interaction miss now has detailed hover path', 'DescribeHoveredObjectDetailed()' in backpack and 'path='+'' in backpack)

# Coarse C# structural sanity.
for name, src in [('backpack', backpack), ('pager', pager)]:
    check(f'{name} braces balanced', src.count('{') == src.count('}'))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS' if ok else 'FAIL') + ' - ' + name)
print(f'\n{len(checks)-len(failed)}/{len(checks)} checks passed')
if failed:
    sys.exit(1)
