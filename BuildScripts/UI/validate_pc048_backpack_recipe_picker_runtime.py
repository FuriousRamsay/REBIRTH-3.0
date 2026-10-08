from pathlib import Path
import sys, xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
checks=[]
def check(cond,msg):
    checks.append((bool(cond),msg))
    print(('PASS' if cond else 'FAIL')+' | '+msg)

windows=(ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
templates=(ROOT/'Config/XUi_InGame/templates.xml').read_text(encoding='utf-8')
pager=(ROOT/'Scripts/UI/XUiC_RebirthPagerScrollbar.cs').read_text(encoding='utf-8')
backpack=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthExpandableBackpackScroll.cs').read_text(encoding='utf-8')

# Crafting list must stay in the V3.2-safe native hierarchy while using a real mouse-owning track.
check('controller="RebirthRecipeSmoothScroll, RebirthUtils"' not in windows,'unsafe RecipeList reparenting remains absent')
check('<grid name="recipes" depth="2" rows="8" cols="1" pos="3,-98" width="411" height="368" cell_width="411"' in windows,'recipe grid extends under the scrollbar gutter to the visible scroll bar')
check('name="rebirthCraftingPagerScroll" controller="RebirthPagerScrollbar, RebirthUtils" pos="405,-98"' in windows,'crafting pager scrollbar remains at right edge')
check('name="rebirthCraftingPagerScrollTrackInput"' in windows,'crafting scrollbar has transparent full-track input surface')
check('on_press="true" on_drag="true" on_scroll="true"' in windows,'crafting track surface accepts press drag and wheel')
check('name="width">411</setattribute>' in templates,'recipe entry chrome follows 411px grid width')
check('name="width">295</setattribute>' in templates,'recipe name uses reclaimed gutter width')
check('name="pos">380,-10</setattribute>' in templates,'recipe unlock icon follows expanded row')
check('private XUiController craftingTrackInput;' in pager,'pager controller resolves crafting track input')
check('CraftingTrack_OnPress' in pager and 'CraftingTrack_OnDrag' in pager,'track and thumb-area mouse input is handled explicitly')
check('TryGetPointerNormalizedY' in pager,'track pointer position maps directly to pager position')
check('pager.SetPage(requested);' in pager,'track interaction drives native pager without reparenting RecipeList')
check('WireRecipeWheelSurface();' in pager,'recipe rows retain mouse-wheel paging support')

# Backpack native ItemStack input forwarding is explicitly repaired and observable.
check('slot.ViewComponent.EventOnPress=true;' in backpack,'visible backpack item stacks explicitly forward press events')
check('slot.ViewComponent.EventOnDrag=true;' in backpack,'visible backpack item stacks explicitly forward drag events')
check('if(interactionDiagnosticWiredSlots.Contains(slot))continue;' in backpack,'diagnostic delegates remain single-wired')
check('automaticInteractionMissBudget=12' in backpack,'automatic interaction-miss trace budget is present')
check('INPUT_READY' in backpack and 'BuildSlotInputForwardingReport' in backpack,'backpack logs input-forwarding readiness on open')
check('INTERACTION_MISS' in backpack and 'pressForwarding=' in backpack,'failed slot presses produce actionable automatic diagnostics')
check('IsMouseOverBackpackArea()' in backpack,'working backpack wheel targeting remains scoped to backpack area')

# Picker: move the actual native color picker through a parent rect because direct color_picker Y was ineffective.
check('name="rebirthVitalColorPickerWindow" anchor="Center" depth="35" pos="-220,235" width="440" height="470"' in windows,'picker window has room for lowered color field')
check('name="rebirthVitalPickerColorBody" pos="24,-112"' in windows,'ordinary rect physically moves the entire color square+hue section down')
check('<color_picker name="rebirthVitalColorPicker" pos="0,0"/>' in windows,'native color picker is nested inside the translated body')
check('name="rebirthVitalPickerPreviewRect" pos="302,-150"' in windows,'preview remains aligned with the lowered picker')
check('name="btnApplyRebirthVitalColor" depth="6" pos="20,-420"' in windows,'picker actions remain below the lowered field')

# Basic source and XML integrity.
def braces(text): return text.count('{')==text.count('}')
check(braces(pager),'balanced C# braces: pager scrollbar')
check(braces(backpack),'balanced C# braces: expandable backpack')
xml_count=0; bad=[]
for path in (ROOT/'Config').rglob('*.xml'):
    xml_count+=1
    try: ET.parse(path)
    except Exception as e: bad.append((path,e))
check(not bad,f'all Config XML parses ({xml_count} files)')
if bad:
    for p,e in bad[:10]: print('XML ERROR',p,e)

failed=[m for ok,m in checks if not ok]
print(f'PC048_STATIC_CHECKS={len(checks)} PASSED={len(checks)-len(failed)} FAILED={len(failed)} XML={xml_count}')
sys.exit(1 if failed else 0)
