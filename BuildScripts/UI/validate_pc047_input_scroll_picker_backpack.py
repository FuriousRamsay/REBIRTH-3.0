from pathlib import Path
import os, re, sys, xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
checks=[]
def check(cond,msg):
    checks.append((bool(cond),msg))
    print(('PASS' if cond else 'FAIL')+' | '+msg)

windows=(ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
templates=(ROOT/'Config/XUi_InGame/templates.xml').read_text(encoding='utf-8')
pager=(ROOT/'Scripts/UI/XUiC_RebirthPagerScrollbar.cs').read_text(encoding='utf-8')
backpack=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthExpandableBackpackScroll.cs').read_text(encoding='utf-8')
console=(ROOT/'Scripts/UI/Console/RebirthConsolePopupSuppression.cs').read_text(encoding='utf-8')
controls=(ROOT/'Scripts/Input/RebirthNativeControls.cs').read_text(encoding='utf-8')

# Recipe list remains native/direct-child and now fills the gutter up to the external scrollbar.
check('controller="RebirthRecipeSmoothScroll, RebirthUtils"' not in windows,'unsafe PC045 smooth-scroll wrapper remains absent')
check('<grid name="recipes" depth="2" rows="8" cols="1" pos="3,-98" width="402" height="368" cell_width="402"' in windows,'native RecipeList grid widened to scrollbar edge')
check('name="rebirthCraftingPagerScroll" controller="RebirthPagerScrollbar, RebirthUtils" pos="405,-98"' in windows,'crafting scrollbar remains at dedicated right edge')
check('name="width">402</setattribute>' in templates,'recipe row template width follows 402px grid')
check('name="width">286</setattribute>' in templates,'recipe label gains the reclaimed row width')
check('name="pos">371,-10</setattribute>' in templates,'recipe unlock icon tracks widened row')

# Initial scrollbar must refresh when page range appears even if current page is still zero.
check('private int lastMaxPage = -1;' in pager,'pager tracks page-range changes independently')
check('bool rangeChanged = maxPage != lastMaxPage;' in pager,'pager detects delayed native page-count population')
check('ViewComponent.IsVisible = IsCraftingBridge || needed;' in pager,'crafting bridge stays update-active during delayed population')
check('!rangeChanged && !sizeChanged && !pageChanged' in pager,'range changes force native scrollbar refresh')
check('WireRecipeWheelSurface();' in pager,'recipe rows are wired as wheel surfaces without reparenting')
check('WireScrollRecursive(recipeList);' in pager,'recipe subtree forwards mouse wheel to pager bridge')
check('Time.frameCount == lastWheelFrame' in pager,'recipe wheel events are de-duplicated per frame')

# Backpack: wheel works over slots, and native slot click/drag ownership is not replaced.
check('WireScrollRecursive(inventory);' not in backpack,'backpack no longer injects OnScroll handlers through native item-stack descendants')
check('IsMouseOverBackpackArea()' in backpack,'backpack wheel fallback is scoped to the backpack area')
check('IsMouseInsideColliderTree' in backpack,'backpack wheel fallback includes occupied and empty slot collider trees')
check('slot.OnPress+=Slot_OnPressDiagnostic;' in backpack,'slot press diagnostics observe native interaction path')
check('slot.OnDrag+=Slot_OnDragDiagnostic;' in backpack,'slot drag diagnostics observe native interaction path')
check('INTERACTION_MISS' in backpack,'diagnostics identify mouse-downs that never reach XUiC_ItemStack.OnPress')
check('BACKPACK_MOUSE_DOWN' in backpack,'diagnostics record raw backpack mouse-down target')
check('RAW_WHEEL' in backpack,'backpack raw wheel diagnostic retained')

# Color picker visible color field itself is moved down, not only buttons/window.
check('name="rebirthVitalColorPickerWindow" anchor="Center" depth="35" pos="-220,220" width="440" height="440"' in windows,'color picker window expanded for lower content')
check('<color_picker name="rebirthVitalColorPicker" pos="24,-170"/>' in windows,'actual color square/gradient root moved 50px farther down')
check('name="rebirthVitalPickerPreviewRect" pos="302,-180"' in windows,'preview follows lowered picker')
check('name="btnApplyRebirthVitalColor" depth="6" pos="20,-390"' in windows,'picker actions remain below lowered color field')

# Debug console typing must not dispatch gameplay movement/hotkeys.
check('class RebirthConsoleInputGuardRuntime' in console,'legacy debug-console input guard exists')
check('class RebirthConsolePlayerMovementGuardPatch' in console,'PlayerMoveController console guard patch exists')
check('player.ClearMovementInputs();' in console and 'return false;' in console,'console guard clears movement and skips gameplay movement update')
check('PatchConsoleTransitions(harmony);' in console,'console open/close transitions are patched')
check('RebirthConsoleInputGuardRuntime.MarkOpenedPostfix' in console,'console open transition marks keyboard ownership')
check('RebirthConsoleInputGuardRuntime.IsConsoleOpen()' in controls,'REBIRTH custom gameplay hotkeys also respect console ownership')

# Basic source structural checks.
def braces(text): return text.count('{')==text.count('}')
for rel,text in [
 ('Scripts/UI/XUiC_RebirthPagerScrollbar.cs',pager),
 ('Scripts/Survivor/UI/XUiC_RebirthExpandableBackpackScroll.cs',backpack),
 ('Scripts/UI/Console/RebirthConsolePopupSuppression.cs',console),
 ('Scripts/Input/RebirthNativeControls.cs',controls),
]: check(braces(text),'balanced C# braces: '+rel)

xml_count=0; xml_bad=[]
for path in (ROOT/'Config').rglob('*.xml'):
    xml_count += 1
    try: ET.parse(path)
    except Exception as e: xml_bad.append((path,e))
check(not xml_bad,f'all Config XML parses ({xml_count} files)')
if xml_bad:
    for p,e in xml_bad[:10]: print('XML ERROR',p,e)

failed=[m for ok,m in checks if not ok]
print(f'PC047_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)} XML={xml_count}')
sys.exit(1 if failed else 0)
