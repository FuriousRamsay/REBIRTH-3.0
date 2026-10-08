from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
backpack = (root / 'Scripts/Survivor/UI/XUiC_RebirthExpandableBackpackScroll.cs').read_text(encoding='utf-8')
pager = (root / 'Scripts/UI/XUiC_RebirthPagerScrollbar.cs').read_text(encoding='utf-8')
util = (root / 'Scripts/UI/RebirthNativeScrollbarUtil.cs').read_text(encoding='utf-8')
windows = (root / 'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
templates = (root / 'Config/XUi_InGame/templates.xml').read_text(encoding='utf-8')

checks=[]
def check(name, cond): checks.append((name, bool(cond)))

# PC049 visual/layout state is intentionally retained.
check('recipe grid remains 402px wide', 'name="recipes" depth="2" rows="8" cols="1" pos="3,-98" width="402" height="368" cell_width="402"' in windows)
check('crafting scrollbar remains at x405', 'name="rebirthCraftingPagerScroll" controller="RebirthPagerScrollbar, RebirthUtils" pos="405,-98" width="20" height="368"' in windows)
check('recipe template remains 402px wide', '/templates/recipe_entry/rect" name="width">402</setattribute>' in templates)
check('no unsafe smooth-scroll RecipeList reparenting', 'RebirthRecipeSmoothScroll' not in windows)

# Backpack: the PC049 log proved backgroundMain is the live NGUI hover target above ItemStacks.
check('blocking backpack background collider is disabled', 'DisableBlockingBackpackBackgroundInput' in backpack and 'content.Find("backgroundMain")' in backpack and 'collider.enabled=false' in backpack)
check('background collider state is restored on close', 'RestoreBlockingBackpackBackgroundInput();' in backpack and 'public override void OnClose()' in backpack)
check('visible item stacks explicitly forward press', 'slot.ViewComponent.EventOnPress=true;' in backpack)
check('visible item stacks explicitly forward drag', 'slot.ViewComponent.EventOnDrag=true;' in backpack)
check('blocker diagnostic is emitted', '[REBIRTH BackpackScroll] INPUT_BLOCKER_DISABLED' in backpack)
check('interaction miss diagnostics retained', 'INTERACTION_MISS' in backpack and 'DescribeHoveredObjectDetailed()' in backpack)
check('backpack wheel fallback stays scoped', 'if(!IsMouseOverBackpackArea())return;' in backpack)

# Recipe scrollbar: raw pointer was already reaching the bridge; use the native paging callbacks.
check('crafting direct input uses native pager navigation', 'NavigateCraftingPagerNative' in pager)
check('native navigation uses PageDown', 'pager.PageDown();' in pager)
check('native navigation uses PageUp', 'pager.PageUp();' in pager)
check('native direction is detected at runtime', 'ResolveCraftingPagingDirection' in pager and 'craftingPageDownDirection' in pager)
check('raw track input logs final pager page', '+ " after=" + after' in pager)
check('native navigation diagnostic retained', '[REBIRTH RecipeScroll] NATIVE_NAV' in pager)
check('raw pointer track polling retained', 'PollCraftingTrackRawInput' in pager and 'Input.GetMouseButtonDown(0)' in pager)

# Native thumb size: invalidate UIScrollView cached bounds immediately on category/range changes.
check('native scrollbar refresh invalidates bounds', 'InvalidateBounds' in util)
check('native scrollbar barSize can be inspected', 'TryGetBarSize' in util and 'barSize' in util)
check('range-applied diagnostics include native bar size', '[REBIRTH RecipeScroll] RANGE_APPLIED' in pager and 'nativeBarSize' in pager)
check('periodic category range sync retained', 'nextCraftingForcedRefreshTime = Time.realtimeSinceStartup + 0.15f' in pager)

# Structural sanity.
for name, src in [('backpack',backpack),('pager',pager),('scrollbar util',util)]:
    check(f'{name} braces balanced', src.count('{') == src.count('}'))
    check(f'{name} parens balanced', src.count('(') == src.count(')'))
check('pager does not reintroduce CultureInfo dependency', 'CultureInfo' not in pager)

xml_count=0
xml_fail=[]
for path in (root/'Config').rglob('*.xml'):
    xml_count += 1
    try: ET.parse(path)
    except Exception as exc: xml_fail.append((path, str(exc)))
check(f'all Config XML parses ({xml_count} files)', not xml_fail)

failed=[n for n,ok in checks if not ok]
for n,ok in checks:
    print(('PASS' if ok else 'FAIL')+' | '+n)
print(f'PC050_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)} XML={xml_count}')
if xml_fail:
    for p,e in xml_fail[:10]: print('XML_FAIL',p,e)
if failed: sys.exit(1)
