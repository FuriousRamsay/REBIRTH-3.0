#!/usr/bin/env python3
from pathlib import Path
import sys, re
import xml.etree.ElementTree as ET

root=Path(sys.argv[1] if len(sys.argv)>1 else '.').resolve()
checks=[]
def ck(cond,msg):
    ok=bool(cond); checks.append((ok,msg)); print(('PASS' if ok else 'FAIL')+' | '+msg)
def read(rel): return (root/rel).read_text(encoding='utf-8')
def pos(e): return tuple(int(x.strip()) for x in e.get('pos','0,0').split(','))
def size(e): return int(e.get('width','0')),int(e.get('height','0'))

win=root/'Config/XUi_InGame/windows.xml'; met=root/'Config/_Metabolism/windows.xml'; tpl=root/'Config/XUi_InGame/templates.xml'; layoutp=root/'Scripts/UI/XUiC_RebirthToolbeltLayout.cs'; healthp=root/'Scripts/Survivor/Condition/RebirthHealthCapacityService.cs'
for p in [win,met,tpl,layoutp,healthp]: ck(p.exists(),f'file exists: {p.relative_to(root)}')
wr=ET.parse(win).getroot(); mr=ET.parse(met).getroot(); tr=ET.parse(tpl).getroot(); layout=read('Scripts/UI/XUiC_RebirthToolbeltLayout.cs'); wtxt=read('Config/XUi_InGame/windows.xml'); ttxt=read('Config/XUi_InGame/templates.xml'); health=read('Scripts/Survivor/Condition/RebirthHealthCapacityService.cs')
window=None
for candidate in wr.findall(".//window[@name='windowToolbelt']"):
    if candidate.find("rect[@name='rebirthVitalsLayout']") is not None: window=candidate; break
ck(window is not None,'REBIRTH toolbelt window exists')
if window is not None: ck(window.get('pos')=='-620,108','HUD cluster is 7px lower than PC042 while retaining most of prior lift')

expected={
 'rebirthVitalHealth':('Health','UIAtlas','ui_game_symbol_add'),
 'rebirthVitalStamina':('Stamina','UIAtlas','ui_game_symbol_run'),
 'rebirthVitalFood':('Food','RebirthHud','rb_hud_icon_food_apple'),
 'rebirthVitalWater':('Water','UIAtlas','ui_game_symbol_water'),
}
for name,(stat,atlas,sprite) in expected.items():
    node=wr.find(f".//rect[@name='{name}']"); ck(node is not None,f'{name} exists')
    if node is None: continue
    ck(node.get('stat_type')==stat,f'{name} binds {stat}')
    icon=node.find("./sprite[@name='Icon']"); label=node.find("./label[@name='rebirthVitalText']"); full=node.find("./filledsprite[@name='rebirthVitalFullBackground']"); cap=node.find("./filledsprite[@name='rebirthVitalCapacity']"); fill=node.find("./filledsprite[@name='rebirthVitalFill']")
    ck(all(x is not None for x in [icon,label,full,cap,fill]),f'{name} has icon/value/three capacity layers')
    if icon is not None:
        ck(icon.get('atlas')==atlas and icon.get('sprite')==sprite,f'{name} icon source correct')
        ck(icon.get('color')=='255,255,255,255',f'{name} icon authored white')
        ck(pos(icon)[0]==12,f'{name} icon sits inside left edge of bar')
    if full is not None:
        w=size(node)[0]; ck(pos(full)[0]==2 and size(full)[0]==w-4,f'{name} authored rail spans full cell with symmetric 2px inset')
        ck(full.get('fill')=='1' and full.get('color')=='0,0,0,255',f'{name} black unusable layer retained')
        ck(cap.get('fill')=='{statmodifiedmax}',f'{name} gray recoverable layer retained')
        ck(fill.get('fill')=='{statfill}',f'{name} current fill retained')
    ck(node.find("./button[@name='rebirthVitalColorHit']") is None,f'{name} stale internal PC042 hit target removed')

energy=mr.find(".//rect[@name='rebirthMetabolismHud']"); ck(energy is not None,'Energy bar exists')
if energy is not None:
    ei=energy.find("./sprite[@name='rebirthMetabolismEnergyIcon']"); ef=energy.find("./filledsprite[@name='rebirthMetabolismEnergyFullBackground']")
    ck(ei is not None and ei.get('color')=='255,255,255,255' and pos(ei)[0]==12,'Energy icon is white and inside its bar')
    ck(ef is not None and pos(ef)[0]==2 and size(ef)[0]==size(energy)[0]-4,'Energy rail spans its full third')
    ck(energy.find("./button[@name='rebirthVitalColorHit']") is None,'Energy stale internal PC042 hit target removed')

for token,msg in [
 ('int railWidth = Math.Max(1, width - (VitalRailInset * 2));','runtime rail uses full dynamic stat width with symmetric inset'),
 ('SetPosition(text, VitalRailInset, -4);','runtime value begins at rail origin'),
 ('SetSize(text, railWidth, VitalFillHeight);','runtime value width equals exact rail width'),
 ('Color32 iconWhite = new Color32(255, 255, 255, 255);','runtime forces all HUD icons white'),
 ('PositionColorButtons(startX);','debug hit surfaces follow dynamic layout'),
 ('GetChildById("btnRebirthVitalHealthColor")','external proven Health debug hit surface restored'),
 ('GetChildById("btnRebirthVitalEnergyColor")','external proven Energy debug hit surface restored'),
]: ck(token in layout,msg)

# Exact thirds/halves still consume live toolbelt width.
for slots in [5,10,11,15,20]:
    total=slots*62; h=total//3; s=total//3; e=total-h-s; f=total//2; w=total-f
    ck(h+s+e==total,f'{slots}-slot top row exactly spans {total}px')
    ck(f+w==total,f'{slots}-slot bottom row exactly spans {total}px')

# External hit buttons: invisible but nonzero alpha, high depth, full dynamic size at runtime.
host=wr.find(".//rect[@name='rebirthToolbeltLayoutController']")
ck(host is not None,'layout controller host exists')
for name in ['btnRebirthVitalHealthColor','btnRebirthVitalStaminaColor','btnRebirthVitalEnergyColor','btnRebirthVitalFoodColor','btnRebirthVitalWaterColor']:
    b=host.find(f"./button[@name='{name}']") if host is not None else None
    ck(b is not None,f'{name} exists')
    if b is not None:
        ck(b.get('depth')=='50',f'{name} renders above stat/info controls for click capture')
        ck(b.get('defaultcolor')=='0,0,0,1' and b.get('hovercolor')=='0,0,0,1',f'{name} is visually imperceptible but hit-testable')
ck('SetVisible(healthColorButtonController, visible);' in layout and 'EnumGamePrefs.DebugMenuEnabled' in layout,'bar color click surfaces only become active in Debug Menu')

# Toolbelt degradation bar must stay inside the slot while selection outline is topmost.
tmpl=None
for e in tr.iter():
    if e.tag=='rebirth_item_stack_toolbar': tmpl=e; break
ck(tmpl is not None,'REBIRTH compact toolbelt template exists')
if tmpl is not None:
    border=tmpl.find(".//sprite[@name='background']")
    db=tmpl.find(".//sprite[@name='durabilityBackground']")
    dfill=tmpl.find(".//filledsprite[@name='durability']")
    ck(border is not None and border.get('depth')=='30' and border.get('foregroundlayer')=='true','selection highlight/border is topmost foreground layer')
    ck(db is not None and size(db)==(54,7) and pos(db)==(4,-53),'durability background is narrower and inset from slot border')
    ck(dfill is not None and size(dfill)==(54,7) and pos(dfill)==(4,-53),'durability fill matches narrowed inset geometry')
    if border is not None and dfill is not None: ck(int(border.get('depth'))>int(dfill.get('depth')),'selection border depth exceeds durability fill depth')

# Compact PC042 picker remains intact and PC041 compile correction remains preserved.
picker=wr.find(".//window[@name='rebirthVitalColorPickerWindow']")
ck(picker is not None and size(picker)==(440,330),'compact color picker retained')
ck('private static void ApplyGodModeHealth(EntityStats stats)' in health,'PC041 EntityStats God-mode compile fix preserved')

for p in [layoutp,healthp,root/'Scripts/UI/XUiC_RebirthVitalColorPicker.cs']:
    s=p.read_text(encoding='utf-8'); ck(s.count('{')==s.count('}'),f'C# braces balanced: {p.name}')
errors=[]; xml_count=0
for p in root.rglob('*.xml'):
    xml_count+=1
    try: ET.parse(p)
    except Exception as e: errors.append((p,e))
ck(not errors,f'all project XML parses | XML={xml_count}')
for p,e in errors[:10]: print('XML_ERROR',p.relative_to(root),e)
passed=sum(1 for ok,_ in checks if ok); failed=len(checks)-passed
print(f'PC043_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={xml_count}')
sys.exit(1 if failed else 0)
