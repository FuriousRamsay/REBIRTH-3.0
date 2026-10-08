from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
errors=[]
checks=0

def ok(cond,msg):
    global checks
    checks += 1
    if not cond:
        errors.append(msg)

win=(ROOT/'Config/XUi_InGame/windows.xml').read_text(encoding='utf-8')
met=(ROOT/'Config/_Metabolism/windows.xml').read_text(encoding='utf-8')
cs=(ROOT/'Scripts/UI/XUiC_RebirthToolbeltLayout.cs').read_text(encoding='utf-8')

for rel in ['Config/XUi_InGame/windows.xml','Config/_Metabolism/windows.xml']:
    try:
        ET.parse(ROOT/rel)
        ok(True, rel+' parses')
    except Exception as exc:
        ok(False, rel+' XML parse failed: '+str(exc))

for name in ['rebirthVitalHealth','rebirthVitalStamina','rebirthVitalFood','rebirthVitalWater']:
    m=re.search(r'<rect name="'+name+r'"[^>]*height="24"[^>]*>(.*?)</rect>',win,re.S)
    ok(bool(m), name+' is 24px tall')
    if not m: continue
    body=m.group(1)
    ok('name="rebirthVitalTrack"' in body and 'height="24"' in body, name+' track is 24px')
    ok(body.count('height="20" type="filled"') >= 3, name+' three capacity layers are 20px')
    ok('width="14" height="14" color="255,255,255,255"' in body, name+' icon is 14px white')
    ok('name="rebirthVitalText"' in body and 'height="20"' in body and 'font_size="13"' in body and 'justify="center"' in body, name+' centered text is 13px')

ok('name="Icon" atlas="UIAtlas" sprite="ui_game_symbol_fork"' in win, 'Food uses native fork icon')
ok('rb_hud_icon_food_apple' not in win, 'Gameplay HUD no longer references apple icon')

for token in [
    'private const int TopVitalsY = 11;',
    'private const int BottomVitalsOffsetY = -86;',
    'private const int VitalHeight = 24;',
    'private const int VitalIconSize = 14;',
    'private const int VitalFillHeight = 20;',
    'int healthWidth = totalWidth / 3;',
    'int staminaWidth = totalWidth / 3;',
    'int foodWidth = totalWidth / 2;',
    'PositionColorButtons(startX);',
    'SetVisible(healthColorButtonController, visible);',
]:
    ok(token in cs, 'controller token missing: '+token)

ok('name="rebirthMetabolismHud" pos="671,11" width="104" height="24"' in met, 'Energy root defaults to 24px')
ok(met.count('height="20" type="filled"') >= 3, 'Energy capacity layers are 20px')
ok('rebirthMetabolismEnergyIcon' in met and 'width="14" height="14"' in met, 'Energy icon is 14px')
ok('rebirthMetabolismEnergyText' in met and 'font_size="13"' in met and 'height="20"' in met, 'Energy text is larger and centered')

for token in [
    'name="rebirthVitalColorPickerWindow" anchor="Center" depth="35" pos="-220,175" width="440" height="350"',
    '<color_picker name="rebirthVitalColorPicker" pos="24,-82"/>',
    'name="rebirthVitalPickerPreviewRect" pos="302,-92"',
    'pos="12,-288" width="416" height="2"',
    'name="btnApplyRebirthVitalColor" depth="6" pos="20,-300"',
    'name="btnCancelRebirthVitalColor" depth="6" pos="250,-300"',
]:
    ok(token in win, 'picker layout token missing: '+token)

for name in ['btnRebirthVitalHealthColor','btnRebirthVitalStaminaColor','btnRebirthVitalEnergyColor','btnRebirthVitalFoodColor','btnRebirthVitalWaterColor']:
    ok(name in win, name+' debug click surface remains present')

if errors:
    print('PC044 validation FAILED: %d/%d checks failed' % (len(errors), checks))
    for e in errors:
        print(' -',e)
    sys.exit(1)
print('PC044 validation PASSED: %d/%d checks' % (checks,checks))
