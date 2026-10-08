#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET
from PIL import Image

root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
checks = []

def ck(cond, msg):
    ok = bool(cond)
    checks.append((ok, msg))
    print(('PASS' if ok else 'FAIL') + ' | ' + msg)

def pos(elem):
    vals = elem.get('pos','0,0').split(',')
    return tuple(int(v.strip()) for v in vals)

def size(elem):
    return int(elem.get('width','0')), int(elem.get('height','0'))

def read(rel):
    return (root/rel).read_text(encoding='utf-8')

win_path=root/'Config/XUi_InGame/windows.xml'
met_path=root/'Config/_Metabolism/windows.xml'
layout_path=root/'Scripts/UI/XUiC_RebirthToolbeltLayout.cs'
color_path=root/'Scripts/UI/XUiC_RebirthVitalColorPicker.cs'
health_path=root/'Scripts/Survivor/Condition/RebirthHealthCapacityService.cs'
apple_path=root/'UIAtlases/RebirthHud/rb_hud_icon_food_apple.png'
bolt_path=root/'UIAtlases/RebirthHud/rb_hud_energy_bolt.png'

for p in [win_path, met_path, layout_path, color_path, health_path, apple_path, bolt_path]:
    ck(p.exists(), f'file exists: {p.relative_to(root)}')

wr=ET.parse(win_path).getroot()
mr=ET.parse(met_path).getroot()
layout=read('Scripts/UI/XUiC_RebirthToolbeltLayout.cs')
color=read('Scripts/UI/XUiC_RebirthVitalColorPicker.cs')
health=read('Scripts/Survivor/Condition/RebirthHealthCapacityService.cs')

window=None
for candidate in wr.findall(".//window[@name='windowToolbelt']"):
    if candidate.find("rect[@name='rebirthVitalsLayout']") is not None:
        window=candidate; break
ck(window is not None, 'REBIRTH windowToolbelt replacement exists')
if window is not None:
    ck(window.get('pos') == '-620,115', 'whole HUD cluster moved upward 20px')

toolbelt=wr.find(".//rect[@name='toolbelt']")
vitals=wr.find(".//rect[@name='rebirthVitalsLayout']")
ck(toolbelt is not None and vitals is not None, 'toolbelt and vital layout exist')
if toolbelt is not None and vitals is not None:
    ck(size(toolbelt)[0] == size(vitals)[0] == 310, 'authored five-slot width remains synchronized')

expected={
 'rebirthVitalHealth':('Health',0,'UIAtlas','ui_game_symbol_add'),
 'rebirthVitalStamina':('Stamina',0,'UIAtlas','ui_game_symbol_run'),
 'rebirthVitalFood':('Food',-82,'RebirthHud','rb_hud_icon_food_apple'),
 'rebirthVitalWater':('Water',-82,'UIAtlas','ui_game_symbol_water'),
}
for name,(stat,y,atlas,sprite) in expected.items():
    node=wr.find(f".//rect[@name='{name}']")
    ck(node is not None, f'{name} exists')
    if node is None: continue
    ck(node.get('stat_type') == stat, f'{name} binds {stat}')
    ck(pos(node)[1] == y, f'{name} row placement preserved')
    icon=node.find("./sprite[@name='Icon']")
    label=node.find("./label[@name='rebirthVitalText']")
    track=node.find("./sprite[@name='rebirthVitalTrack']")
    full=node.find("./filledsprite[@name='rebirthVitalFullBackground']")
    cap=node.find("./filledsprite[@name='rebirthVitalCapacity']")
    fill=node.find("./filledsprite[@name='rebirthVitalFill']")
    hit=node.find("./button[@name='rebirthVitalColorHit']")
    ck(all(x is not None for x in [icon,label,track,full,cap,fill,hit]), f'{name} has icon/value/three layers/full click surface')
    if icon is not None:
        ck(icon.get('atlas') == atlas and icon.get('sprite') == sprite, f'{name} icon is correct')
        ck(pos(icon)[0] == 12, f'{name} icon is on left edge')
    if label is not None:
        ck(pos(label)[0] == 0 and size(label)[0] == size(node)[0] and label.get('justify') == 'center', f'{name} value is centered across stat cell')
    if full is not None and cap is not None and fill is not None:
        ck(pos(full)[0] == pos(cap)[0] == pos(fill)[0] == 26, f'{name} rail leaves shaped-icon breathing room')
        ck(full.get('fill')=='1' and full.get('color')=='0,0,0,255', f'{name} black unusable layer preserved')
        ck(cap.get('fill')=='{statmodifiedmax}', f'{name} gray recoverable layer preserved')
        ck(fill.get('fill')=='{statfill}', f'{name} current-value layer preserved')
    if hit is not None:
        ck(hit.get('defaultcolor')=='0,0,0,0' and hit.get('hovercolor')=='0,0,0,0', f'{name} bar click target is visually invisible')

energy=mr.find(".//rect[@name='rebirthMetabolismHud']")
ck(energy is not None, 'Energy bar exists')
if energy is not None:
    ei=energy.find("./sprite[@name='rebirthMetabolismEnergyIcon']")
    et=energy.find("./label[@name='rebirthMetabolismEnergyText']")
    ef=energy.find("./filledsprite[@name='rebirthMetabolismEnergyFill']")
    ec=energy.find("./filledsprite[@name='rebirthMetabolismEnergyCapacity']")
    eb=energy.find("./filledsprite[@name='rebirthMetabolismEnergyFullBackground']")
    eh=energy.find("./button[@name='rebirthVitalColorHit']")
    ck(ei is not None and ei.get('sprite')=='rb_hud_energy_bolt' and pos(ei)[0]==12, 'Energy uses new left-side lightning glyph')
    ck(et is not None and pos(et)[0]==0 and size(et)[0]==size(energy)[0] and et.get('justify')=='center', 'Energy value centered across its third')
    ck(all(x is not None for x in [ef,ec,eb]) and pos(ef)[0]==pos(ec)[0]==pos(eb)[0]==26, 'Energy rail starts after icon cap')
    ck(eh is not None and eh.get('defaultcolor')=='0,0,0,0', 'Energy bar itself is invisible debug color hit target')

# Dynamic width contract: 3 equal top allocations and 2 equal lower allocations follow live toolbelt width.
for token,desc in [
 ('int totalWidth = activeSlots * SlotPitch;', 'live toolbelt width derives from active slots'),
 ('int healthWidth = totalWidth / 3;', 'Health receives first third'),
 ('int staminaWidth = totalWidth / 3;', 'Stamina receives second third'),
 ('int energyWidth = totalWidth - healthWidth - staminaWidth;', 'Energy receives remaining third'),
 ('int foodWidth = totalWidth / 2;', 'Food receives first half'),
 ('int waterWidth = totalWidth - foodWidth;', 'Water receives second half'),
 ('int railWidth = Math.Max(1, width - VitalRailStartX - VitalRailRightInset);', 'rail dynamically extends with each stat allocation'),
 ('SetPosition(icon, VitalIconCenterX, -10);', 'all runtime stat icons remain at left cap'),
 ('SetPosition(text, 0, -4);', 'runtime values begin at stat-cell origin'),
 ('SetSize(text, width, VitalFillHeight);', 'runtime values center over full dynamic stat width'),
]: ck(token in layout, desc)

for slots in [5,10,11,15,20]:
    total=slots*62
    h=total//3; s=total//3; e=total-h-s
    f=total//2; w=total-f
    ck(h+s+e==total, f'{slots}-slot top row exactly spans {total}px')
    ck(f+w==total, f'{slots}-slot bottom row exactly spans {total}px')
    ck(min(h,s,e)>26 and min(f,w)>26, f'{slots}-slot allocations leave room for icon cap')

# No separate external color boxes remain; each bar owns its full-area click target.
win_text=read('Config/XUi_InGame/windows.xml')
for legacy in ['btnRebirthVitalHealthColor','btnRebirthVitalStaminaColor','btnRebirthVitalEnergyColor','btnRebirthVitalFoodColor','btnRebirthVitalWaterColor']:
    ck(legacy not in win_text and legacy not in layout, f'legacy external color box removed: {legacy}')
ck('health.GetChildById("rebirthVitalColorHit")' in layout and 'energyHud.GetChildById("rebirthVitalColorHit")' in layout, 'controller hooks clicks directly to bars')

picker=wr.find(".//window[@name='rebirthVitalColorPickerWindow']")
ck(picker is not None, 'color picker window exists')
if picker is not None:
    ck(size(picker)==(440,330), 'color picker is compact 440x330 instead of oversized 460x410')
    ck(picker.get('pos')=='-220,165', 'compact color picker remains screen-centered')
    cp=picker.find("./color_picker[@name='rebirthVitalColorPicker']")
    prev=picker.find(".//sprite[@name='rebirthVitalPickerPreview']")
    apply=picker.find("./labeledbutton[@name='btnApplyRebirthVitalColor']")
    cancel=picker.find("./labeledbutton[@name='btnCancelRebirthVitalColor']")
    ck(all(x is not None for x in [cp,prev,apply,cancel]), 'picker has selector, preview, Apply and Cancel')
    if apply is not None and cancel is not None:
        ck(size(apply)==(170,38) and size(cancel)==(170,38), 'picker buttons are compact and balanced')
ck(' + " COLOR"' in color and 'BAR FILL' not in color, 'picker title simplified to STAT COLOR')

# Icon assets are nonempty RGBA glyphs.
for p,name in [(apple_path,'food apple'),(bolt_path,'energy lightning')]:
    if p.exists():
        im=Image.open(p).convert('RGBA')
        ck(im.size==(64,64), f'{name} icon is 64x64')
        ck(im.getchannel('A').getbbox() is not None, f'{name} icon has visible alpha content')

# PC041 compile fix remains preserved.
ck('private static void ApplyGodModeHealth(EntityStats stats)' in health, 'PC041 EntityStats God-mode compile fix preserved')

for p in [layout_path,color_path,health_path]:
    src=p.read_text(encoding='utf-8')
    ck(src.count('{')==src.count('}'), f'C# braces balanced: {p.name}')

xml_count=0; errors=[]
for p in root.rglob('*.xml'):
    xml_count+=1
    try: ET.parse(p)
    except Exception as e: errors.append((p,e))
ck(not errors, f'all project XML parses | XML={xml_count}')
for p,e in errors[:10]: print('XML_ERROR',p.relative_to(root),e)

passed=sum(1 for ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC042_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={xml_count}')
sys.exit(1 if failed else 0)
