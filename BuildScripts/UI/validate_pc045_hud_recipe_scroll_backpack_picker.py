from pathlib import Path
import re, sys, xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
errors=[]; checks=0

def ok(cond,msg):
    global checks
    checks+=1
    if not cond: errors.append(msg)

win=(ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
tpl=(ROOT/'Config/XUi_InGame/templates.xml').read_text(encoding='utf-8')
met=(ROOT/'Config/_Metabolism/windows.xml').read_text(encoding='utf-8')
layout=(ROOT/'Scripts/UI/XUiC_RebirthToolbeltLayout.cs').read_text(encoding='utf-8')
picker=(ROOT/'Scripts/UI/XUiC_RebirthVitalColorPicker.cs').read_text(encoding='utf-8')
backpack=(ROOT/'Scripts/Survivor/UI/XUiC_RebirthExpandableBackpackScroll.cs').read_text(encoding='utf-8')
smooth=(ROOT/'Scripts/Crafting/UI/XUiC_RebirthRecipeSmoothScroll.cs').read_text(encoding='utf-8')

xml_count=0
for path in ROOT.rglob('*.xml'):
    xml_count+=1
    try: ET.parse(path)
    except Exception as exc: errors.append(f'XML parse failed: {path.relative_to(ROOT)}: {exc}')
checks += xml_count

# Vital text must be center-pivoted and runtime-positioned at the geometric vertical centre.
for name in ['rebirthVitalHealth','rebirthVitalStamina','rebirthVitalFood','rebirthVitalWater']:
    m=re.search(r'<rect name="'+name+r'".*?<label depth="7" name="rebirthVitalText"([^>]*)/>',win,re.S)
    ok(bool(m), name+' text exists')
    if m:
        attrs=m.group(1)
        ok('pivot="center"' in attrs, name+' value uses center pivot')
        ok('font_size="13"' in attrs, name+' value retains readable font')
ok('pivot="center"' in met.split('name="rebirthMetabolismEnergyText"',1)[1].split('/>',1)[0], 'Energy value uses center pivot')
ok('SetPosition(text, width / 2, -(VitalHeight / 2));' in layout, 'runtime centers values on both axes')

# Toolbelt durability/degradation rail is taller but still under the depth-30 selection border.
ok('name="background" sprite="menu_empty3px" pos="1,-1" width="60" height="60"' in tpl and 'depth="30"' in tpl, 'selection border remains foreground depth 30')
ok(tpl.count('width="54" height="9" pos="4,-51"') >= 3, 'toolbelt durability layers are 9px tall')

# Recipe list is now a real scrollview with room for its scrollbar rather than the pager bridge.
ok('name="rebirthRecipeScrollHost"' in win and 'controller="RebirthRecipeSmoothScroll, RebirthUtils"' in win, 'recipe smooth-scroll host exists')
ok('<defaultscrollbar/>' in win.split('name="rebirthRecipeScrollHost"',1)[1].split('</rect>',1)[0], 'recipe host uses stock scrollbar')
ok('name="rebirthRecipeScrollView" width="396" height="368"' in win, 'recipe viewport leaves right-side scrollbar gutter')
ok('name="recipes" depth="2" rows="256"' in win and 'cell_width="396"' in win, 'recipe controller pool supports continuous list')
ok('rebirthCraftingPagerScroll" controller="RebirthPagerScrollbar' not in win, 'old page-by-page recipe bridge removed')
ok('name="width">396</setattribute>' in tpl, 'recipe row template narrowed for scrollbar')
ok('class XUiC_RebirthRecipeSmoothScroll' in smooth, 'recipe smooth-scroll controller source exists')
ok('recipeList.recipeInfos != null ? recipeList.recipeInfos.Count : 0' in smooth, 'recipe scroll height follows filtered recipe count')
ok('RebirthNativeScrollbarUtil.Refresh(scrollView);' in smooth, 'recipe native scrollbar refreshes after content changes')

# Backpack: broad wrapper no longer owns an input collider, and global wheel fallback is hover-scoped.
m=re.search(r'<rect name="rebirthExpandableBackpackScroll"([^>]*)>',win)
ok(bool(m), 'backpack scroll root exists')
if m:
    attrs=m.group(1)
    ok('on_scroll=' not in attrs, 'backpack full-area root no longer captures scroll input')
    ok('use_selection_box=' not in attrs, 'backpack full-area root no longer captures slot clicks')
ok('if(!IsMouseOverBackpackHost())return;' in backpack, 'backpack raw-wheel fallback is hover scoped')

# Color picker is available without debug mode and its actual picker control moved down substantially.
ok('GamePrefs.GetBool(EnumGamePrefs.DebugMenuEnabled)' not in picker, 'color picker Open has no Debug Menu gate')
ok('SetVisible(healthColorButtonController, true);' in layout and 'SetVisible(energyColorButtonController, true);' in layout, 'bar click targets stay available')
ok('name="rebirthVitalColorPickerWindow" anchor="Center" depth="35" pos="-220,195" width="440" height="390"' in win, 'picker window expanded downward')
ok('<color_picker name="rebirthVitalColorPicker" pos="24,-120"/>' in win, 'color square/hue gradient moved down')
ok('name="rebirthVitalPickerPreviewRect" pos="302,-130"' in win, 'picker preview moved down with picker')
ok('name="btnApplyRebirthVitalColor" depth="6" pos="20,-340"' in win, 'picker Apply row moved down')
ok('Energy = 4' in picker, 'Energy color kind is defined')
ok('new Color32(174, 88, 238, 255)' in picker, 'Energy default color is part of color array')

# Simple delimiter sanity for changed C# sources.
for rel,text in [
    ('Scripts/UI/XUiC_RebirthToolbeltLayout.cs',layout),
    ('Scripts/UI/XUiC_RebirthVitalColorPicker.cs',picker),
    ('Scripts/Survivor/UI/XUiC_RebirthExpandableBackpackScroll.cs',backpack),
    ('Scripts/Crafting/UI/XUiC_RebirthRecipeSmoothScroll.cs',smooth),
]:
    ok(text.count('{')==text.count('}'), rel+' braces balanced')

if errors:
    print(f'PC045 validation FAILED: {len(errors)}/{checks} checks failed; XML={xml_count}')
    for e in errors: print(' -',e)
    sys.exit(1)
print(f'PC045 validation PASSED: {checks}/{checks} checks; XML={xml_count}')
