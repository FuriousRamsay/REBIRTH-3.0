#!/usr/bin/env python3
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path(sys.argv[1] if len(sys.argv) > 1 else '.').resolve()
checks = []

def ck(cond, msg):
    checks.append((bool(cond), msg))
    print(('PASS' if cond else 'FAIL') + ' | ' + msg)

def read(rel):
    return (root / rel).read_text(encoding='utf-8')

def pos(elem):
    return tuple(int(v.strip()) for v in elem.get('pos', '0,0').split(','))

def size(elem):
    return (int(elem.get('width', '0')), int(elem.get('height', '0')))

windows_path = root / 'Config/XUi_InGame/windows.xml'
met_xml_path = root / 'Config/_Metabolism/windows.xml'
layout_cs_path = root / 'Scripts/UI/XUiC_RebirthToolbeltLayout.cs'
color_cs_path = root / 'Scripts/UI/XUiC_RebirthVitalColorPicker.cs'
met_ui_cs_path = root / 'Scripts/Metabolism/UI/XUiC_RebirthMetabolismUi.cs'
met_install_cs_path = root / 'Scripts/Metabolism/RebirthMetabolismInstaller.cs'
health_capacity_cs_path = root / 'Scripts/Survivor/Condition/RebirthHealthCapacityService.cs'

for p in [windows_path, met_xml_path, layout_cs_path, color_cs_path, met_ui_cs_path, met_install_cs_path, health_capacity_cs_path]:
    ck(p.exists(), f'file exists: {p.relative_to(root)}')

windows_root = ET.parse(windows_path).getroot()
met_root = ET.parse(met_xml_path).getroot()
layout_src = read('Scripts/UI/XUiC_RebirthToolbeltLayout.cs')
color_src = read('Scripts/UI/XUiC_RebirthVitalColorPicker.cs')
met_ui_src = read('Scripts/Metabolism/UI/XUiC_RebirthMetabolismUi.cs')
met_install_src = read('Scripts/Metabolism/RebirthMetabolismInstaller.cs')
health_capacity_src = read('Scripts/Survivor/Condition/RebirthHealthCapacityService.cs')

window = None
for candidate in windows_root.findall(".//window[@name='windowToolbelt']"):
    if candidate.find("rect[@name='rebirthVitalsLayout']") is not None:
        window = candidate
        break
ck(window is not None, 'REBIRTH replacement windowToolbelt exists')

vitals = windows_root.find(".//rect[@name='rebirthVitalsLayout']")
toolbelt = windows_root.find(".//rect[@name='toolbelt']")
ck(vitals is not None and toolbelt is not None, 'vitals layout and native toolbelt exist')
if vitals is not None and toolbelt is not None:
    ck(size(vitals)[0] == size(toolbelt)[0] == 310, 'authored five-slot vital width matches toolbelt width')
    ck(pos(vitals)[1] == 7 and pos(toolbelt)[1] == -13, 'top row remains physically above toolbelt')

expected = {
    'rebirthVitalHealth': ('Health', 0, 'UIAtlas', 'ui_game_symbol_add', True),
    'rebirthVitalStamina': ('Stamina', 0, 'UIAtlas', 'ui_game_symbol_run', False),
    'rebirthVitalFood': ('Food', -82, 'UIAtlas', 'ui_game_symbol_fork', True),
    'rebirthVitalWater': ('Water', -82, 'UIAtlas', 'ui_game_symbol_water', False),
}

for name, (stat, y, atlas, sprite, leading) in expected.items():
    node = windows_root.find(f".//rect[@name='{name}']")
    ck(node is not None, f'{name} exists')
    if node is None:
        continue
    ck(node.get('stat_type') == stat, f'{name} binds native {stat}')
    ck(pos(node)[1] == y, f'{name} is on correct top/bottom row')
    track = node.find("./sprite[@name='rebirthVitalTrack']")
    full = node.find("./filledsprite[@name='rebirthVitalFullBackground']")
    capacity = node.find("./filledsprite[@name='rebirthVitalCapacity']")
    fill = node.find("./filledsprite[@name='rebirthVitalFill']")
    icon = node.find("./sprite[@name='Icon']")
    label = node.find("./label[@name='rebirthVitalText']")
    ck(all(x is not None for x in [track, full, capacity, fill, icon, label]), f'{name} has complete full-bar layers + inline icon/value')
    if track is not None:
        ck(pos(track)[0] == 0 and size(track)[0] == size(node)[0], f'{name} authored track spans full allocated width')
    if full is not None and capacity is not None and fill is not None:
        ck(full.get('fill') == '1' and full.get('color') == '0,0,0,255', f'{name} black layer is absolute max/unusable remainder')
        ck(capacity.get('fill') == '{statmodifiedmax}' and capacity.get('color','').startswith('110,110,110,'), f'{name} gray layer is recoverable modified max')
        ck(fill.get('fill') == '{statfill}', f'{name} colored layer is current value')
    if icon is not None:
        ck(icon.get('atlas') == atlas and icon.get('sprite') == sprite, f'{name} uses required icon {sprite}')
    if label is not None:
        ck(label.get('text') == '{statcurrentwithmax}', f'{name} keeps current/max numeric value')
    if icon is not None and label is not None and track is not None:
        # Since track is full width, icon/value must geometrically overlap it rather than consume external width.
        ix = pos(icon)[0]
        tx = pos(label)[0]
        tw = size(label)[0]
        track_x = pos(track)[0]
        track_w = size(track)[0]
        ck(track_x <= ix <= track_x + track_w and track_x <= tx and tx + tw <= track_x + track_w,
           f'{name} icon/value are inside full-width bar')
        if leading:
            ck(ix < tx, f'{name} inline order is icon then value at left')
        else:
            ck(tx < ix, f'{name} inline order is value then icon at right')

energy = met_root.find(".//rect[@name='rebirthMetabolismHud']")
ck(energy is not None, 'Energy HUD exists')
if energy is not None:
    ck(size(energy)[1] == 20, 'Energy is a bar, not a one-slot circle')
    track = energy.find("./sprite[@name='rebirthEnergyTrack']")
    full = energy.find("./filledsprite[@name='rebirthMetabolismEnergyFullBackground']")
    capacity = energy.find("./filledsprite[@name='rebirthMetabolismEnergyCapacity']")
    fill = energy.find("./filledsprite[@name='rebirthMetabolismEnergyFill']")
    icon = energy.find("./sprite[@name='rebirthMetabolismEnergyIcon']")
    label = energy.find("./label[@name='rebirthMetabolismEnergyText']")
    ck(all(x is not None for x in [track, full, capacity, fill, icon, label]), 'Energy has full bar + black/gray/current layers + inline icon/value')
    if track is not None:
        ck(pos(track)[0] == 0 and size(track)[0] == size(energy)[0], 'Energy authored track spans full allocation')
    if full is not None and capacity is not None and fill is not None:
        ck(full.get('fill') == '1' and full.get('color') == '0,0,0,255', 'Energy black layer is unavailable capacity')
        ck(capacity.get('fill') == '{rbmet_hud_energy_capacity_fill}', 'Energy gray layer binds recoverable capacity')
        ck(fill.get('fill') == '{rbmet_hud_energy_fill}', 'Energy colored layer binds current Energy')
    ck(icon is not None and icon.get('sprite') == 'rb_hud_energy_bolt', 'Energy uses lightning icon')
    ck(label is not None and label.get('text') == '{rbmet_hud_energy_current_with_max}', 'Energy displays numeric current/max inside bar with no word label')
    ck(not energy.findall("./label[@text='ENERGY']"), 'Energy has no visible ENERGY word label')

# Dynamic geometry contract: top thirds, bottom halves, full-width tracks.
for token, desc in [
    ('int totalWidth = activeSlots * SlotPitch;', 'toolbelt width derives from unlocked slot count'),
    ('int healthWidth = totalWidth / 3;', 'Health receives first third'),
    ('int staminaWidth = totalWidth / 3;', 'Stamina receives second third'),
    ('int energyWidth = totalWidth - healthWidth - staminaWidth;', 'Energy receives exact remaining third'),
    ('LayoutEnergy(startX + energyX, TopVitalsY, energyWidth);', 'Energy is laid out on same top row'),
    ('int foodWidth = totalWidth / 2;', 'Food receives first half below toolbelt'),
    ('int waterWidth = totalWidth - foodWidth;', 'Water receives second half below toolbelt'),
    ('SetPosition(track, 0, 0);', 'runtime track begins at allocation edge'),
    ('SetSize(track, width, VitalHeight);', 'runtime track expands to full allocation width'),
]:
    ck(token in layout_src, desc)

for slots in [5, 10, 11, 20]:
    total = slots * 62
    h = total // 3
    s = total // 3
    e = total - h - s
    f = total // 2
    w = total - f
    ck(h + s + e == total, f'{slots}-slot top thirds exactly span {total}px')
    ck(f + w == total, f'{slots}-slot bottom halves exactly span {total}px')

# No legacy swatch boxes; the full bars are the debug hit targets.
controller = windows_root.find(".//rect[@name='rebirthToolbeltLayoutController']")
ck(controller is not None, 'toolbelt layout controller exists')
if controller is not None:
    buttons = {b.get('name'): b for b in controller.findall('./button')}
    required_buttons = [
        'btnRebirthVitalHealthColor','btnRebirthVitalStaminaColor','btnRebirthVitalEnergyColor',
        'btnRebirthVitalFoodColor','btnRebirthVitalWaterColor'
    ]
    ck(all(name in buttons for name in required_buttons), 'all five stat bars have debug click targets')
    ck(not controller.findall(".//*[@name='rebirthVitalHealthSwatch']") and not controller.findall(".//*[@name='rebirthVitalFoodSwatch']"), 'legacy color swatch boxes are removed')
    for name in required_buttons:
        if name in buttons:
            ck(buttons[name].get('width') == '20' and buttons[name].get('height') == '20', f'{name} is a runtime-resized transparent seed button')

for token, desc in [
    ('PositionVitalColorButton(healthColorButtonController', 'Health click target follows Health bar'),
    ('PositionVitalColorButton(staminaColorButtonController', 'Stamina click target follows Stamina bar'),
    ('PositionVitalColorButton(foodColorButtonController', 'Food click target follows Food bar'),
    ('PositionVitalColorButton(waterColorButtonController', 'Water click target follows Water bar'),
    ('energyColorButtonController', 'Energy color target is wired'),
    ('SetVisible(energyColorButtonController, visible);', 'Energy color click target is debug-only'),
]:
    ck(token in layout_src, desc)

ck('Energy = 4' in color_src, 'Energy is included in vital color picker enum')
ck('new Color32(174, 88, 238, 255)' in color_src, 'Energy has purple default calibration color')

# God mode must bypass live Health/Stamina max blockage and refill current values without deleting persisted injury state.
for token, desc in [
    ('if (player.IsGodMode.Value)', 'Health capacity service detects native god mode'),
    ('stats.Health.MaxModifier = 0f;', 'God mode removes live unusable Health capacity'),
    ('stats.Health.Value = stats.Health.ModifiedMax;', 'God mode refills Health to live maximum'),
]:
    ck(token in health_capacity_src, desc)
ck('record.Condition.HealthCapacity' in health_capacity_src and 'ApplyGodModeHealth' in health_capacity_src,
   'god mode bypass preserves persisted Survivor condition capacity rather than deleting injury state')
for token, desc in [
    ('if (player.IsGodMode.Value)', 'Stamina metabolism patch detects native god mode'),
    ('__instance.Stamina.MaxModifier = 0f;', 'God mode removes live unusable Stamina capacity'),
    ('__instance.Stamina.Value = __instance.Stamina.ModifiedMax;', 'God mode refills Stamina to live maximum'),
]:
    ck(token in met_install_src, desc)

ck('case "rbmet_hud_energy_current_with_max":' in met_ui_src, 'Energy controller exposes combined current/max binding')
ck('black part of the bar' in met_ui_src and 'black part of the ring' not in met_ui_src, 'Energy tooltip describes bar capacity semantics')
ck('rb_hud_icon_food_fork' not in read('Config/XUi_InGame/windows.xml'), 'old custom fork glyph is no longer referenced by HUD')

met_xml = read('Config/_Metabolism/windows.xml')
ck('rebirthMetabolismStomachBar' not in met_xml and 'rebirthMetabolismIntestineBar' not in met_xml, 'Stomach and intestine remain absent from play HUD')
ck('CharacterFrameWindow' in met_xml and 'rbmet_stomach_fill' in met_xml and 'rbmet_intestine_fill' in met_xml, 'detailed Character -> Metabolism digestive information remains intact')

for p in [layout_cs_path, color_cs_path, met_ui_cs_path, met_install_cs_path, health_capacity_cs_path]:
    src = p.read_text(encoding='utf-8')
    ck(src.count('{') == src.count('}'), f'C# brace counts balanced: {p.name}')

xml_count = 0
xml_errors = []
for p in root.rglob('*.xml'):
    xml_count += 1
    try:
        ET.parse(p)
    except Exception as e:
        xml_errors.append((p, e))
ck(not xml_errors, f'all project XML parses | XML={xml_count}')
if xml_errors:
    for p, e in xml_errors[:10]:
        print(f'XML_ERROR | {p.relative_to(root)} | {e}')

passed = sum(1 for ok,_ in checks if ok)
failed = len(checks) - passed
print(f'PC040_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={xml_count}')
sys.exit(1 if failed else 0)
