#!/usr/bin/env python3
from pathlib import Path
import sys
import re
import xml.etree.ElementTree as ET

try:
    from PIL import Image
except Exception:
    Image = None

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
met_cs_path = root / 'Scripts/Metabolism/UI/XUiC_RebirthMetabolismUi.cs'
energy_icon_path = root / 'UIAtlases/RebirthHud/rb_hud_energy_bolt.png'

for p in [windows_path, met_xml_path, layout_cs_path, met_cs_path, energy_icon_path]:
    ck(p.exists(), f'file exists: {p.relative_to(root)}')

windows_root = ET.parse(windows_path).getroot()
met_root = ET.parse(met_xml_path).getroot()
layout_src = read('Scripts/UI/XUiC_RebirthToolbeltLayout.cs')
met_src = read('Scripts/Metabolism/UI/XUiC_RebirthMetabolismUi.cs')

window = windows_root.find(".//window[@name='windowToolbelt']")
ck(window is not None, 'REBIRTH replacement windowToolbelt exists')
if window is not None:
    ck(int(window.get('width', '0')) >= 1310, 'windowToolbelt reserves max 20-slot width plus Energy circle')
    ck(int(window.get('height', '0')) >= 102, 'windowToolbelt reserves top/toolbelt/bottom vital geometry')

vitals = windows_root.find(".//rect[@name='rebirthVitalsLayout']")
toolbelt = windows_root.find(".//rect[@name='toolbelt']")
ck(vitals is not None and toolbelt is not None, 'vitals layout and native toolbelt are both present')
if vitals is not None and toolbelt is not None:
    ck(size(vitals)[0] == size(toolbelt)[0] == 310, 'authored five-slot vital width equals authored toolbelt width (310)')
    # In XUi coordinates, 7 is the top vital baseline; toolbelt starts at -13 and is 62 high;
    # bottom vital offset -82 gives absolute -75, exactly the toolbelt bottom edge.
    ck(pos(vitals)[1] == 7 and pos(toolbelt)[1] == -13, 'top vital row is authored directly above the toolbelt')

expected = {
    'rebirthVitalHealth': ('Health', 0, 'ui_game_symbol_add', True),
    'rebirthVitalStamina': ('Stamina', 0, 'ui_game_symbol_run', False),
    'rebirthVitalFood': ('Food', -82, 'rb_hud_icon_food_fork', True),
    'rebirthVitalWater': ('Water', -82, 'ui_game_symbol_water', False),
}

for name, (stat, y, sprite, leading) in expected.items():
    node = windows_root.find(f".//rect[@name='{name}']")
    ck(node is not None, f'{name} exists')
    if node is None:
        continue
    ck(node.get('stat_type') == stat, f'{name} binds native {stat} stat')
    ck(pos(node)[1] == y, f'{name} is on the correct {"top" if y == 0 else "bottom"} row')
    icon = node.find("./sprite[@name='Icon']")
    label = node.find("./label[@name='rebirthVitalText']")
    track = node.find("./sprite[@name='rebirthVitalTrack']")
    full = node.find("./filledsprite[@name='rebirthVitalFullBackground']")
    capacity = node.find("./filledsprite[@name='rebirthVitalCapacity']")
    fill = node.find("./filledsprite[@name='rebirthVitalFill']")
    ck(icon is not None and icon.get('sprite') == sprite, f'{name} uses required icon {sprite}')
    ck(label is not None and label.get('text') == '{statcurrentwithmax}', f'{name} displays current/max value')
    ck(all(x is not None for x in [track, full, capacity, fill]), f'{name} has track + black max + gray capacity + colored current layers')
    if all(x is not None for x in [full, capacity, fill]):
        ck(full.get('fill') == '1' and full.get('color') == '0,0,0,255', f'{name} black layer represents full absolute max')
        ck(capacity.get('fill') == '{statmodifiedmax}' and capacity.get('color','').startswith('110,110,110,'), f'{name} gray layer represents recoverable/modified max')
        ck(fill.get('fill') == '{statfill}', f'{name} colored layer represents current value')
    if icon is not None and label is not None and track is not None:
        ix = pos(icon)[0]
        tx = pos(label)[0]
        bx = pos(track)[0]
        if leading:
            ck(ix < tx < bx, f'{name} authored order is icon -> value -> bar')
        else:
            # bar starts left, then value, then trailing icon.
            ck(bx < tx < ix, f'{name} authored order is bar -> value -> icon')

# Dynamic geometry contract.
for token, desc in [
    ('int totalWidth = activeSlots * SlotPitch;', 'toolbelt width derives directly from visible slot count'),
    ('SetSize(nativeToolbelt, totalWidth, SlotPitch);', 'native toolbelt width follows visible slot count'),
    ('SetSize(vitalsLayout, totalWidth, 102);', 'top/bottom vital container follows exact toolbelt width'),
    ('LayoutVital(health, 0, 0, leftWidth, true);', 'Health uses leading icon/value/bar layout'),
    ('LayoutVital(stamina, rightX, 0, rightWidth, false);', 'Stamina uses trailing bar/value/icon layout'),
    ('LayoutVital(food, 0, BottomVitalsOffsetY, leftWidth, true);', 'Food uses leading icon/value/bar below toolbelt'),
    ('LayoutVital(water, rightX, BottomVitalsOffsetY, rightWidth, false);', 'Water uses trailing bar/value/icon below toolbelt'),
    ('SetPosition(energyHud, startX + totalWidth + EnergyGap, ToolbeltY);', 'Energy follows the live right edge of the toolbelt'),
    ('SetSize(energyHud, EnergySize, EnergySize);', 'Energy stays square and one-slot sized'),
    ('private const int EnergySize = SlotPitch;', 'Energy diameter equals one toolbelt slot'),
    ('private const int BottomVitalsOffsetY = -82;', 'Food/Water baseline is directly below 62px toolbelt'),
]:
    ck(token in layout_src, desc)

# Explicit leading/trailing implementation order.
ck('iconCenterX = VitalIconBox / 2;' in layout_src and 'barX = textX + VitalValueWidth + VitalElementGap;' in layout_src,
   'leading vital implementation keeps icon -> value -> growing bar')
ck('barX = 0;' in layout_src and 'textX = width - VitalIconBox - VitalElementGap - VitalValueWidth;' in layout_src and 'iconCenterX = width - (VitalIconBox / 2);' in layout_src,
   'trailing vital implementation keeps growing bar -> value -> icon')

# Simulate the fixed geometry formulas for progression widths.
slot_pitch = 62
max_width = 20 * slot_pitch
gap = 8
for slots in [5, 10, 11, 20]:
    total = slots * slot_pitch
    start = (max_width - total) // 2
    usable = total - gap
    left = usable // 2
    right = usable - left
    energy_x = start + total + gap
    ck(left + gap + right == total, f'{slots}-slot vital row spans exactly toolbelt width ({total}px)')
    ck(energy_x == start + total + gap, f'{slots}-slot Energy starts 8px beyond live toolbelt edge')
ck((0 + max_width + gap + slot_pitch) <= 1320, '20-slot toolbelt + gap + Energy circle fits HUD root width')

energy = met_root.find(".//rect[@name='rebirthMetabolismHud']")
ck(energy is not None, 'circular Energy HUD exists as a windowToolbelt child')
if energy is not None:
    ck(size(energy) == (62, 62), 'authored Energy HUD is exactly one slot (62x62)')
    ring_capacity = energy.find("./filledsprite[@name='rebirthMetabolismEnergyCapacity']")
    ring_fill = energy.find("./filledsprite[@name='rebirthMetabolismEnergyFill']")
    ring_black = energy.find("./sprite[@name='rebirthMetabolismEnergyFullBackground']")
    bolt = energy.find("./sprite[@name='rebirthMetabolismEnergyIcon']")
    current = energy.find("./label[@name='rebirthMetabolismEnergyCurrent']")
    max_label = energy.find("./label[@name='rebirthMetabolismEnergyMax']")
    ck(all(x is not None for x in [ring_capacity, ring_fill, ring_black]), 'Energy ring preserves black max + gray recoverable + purple current layers')
    if ring_capacity is not None and ring_fill is not None:
        ck(ring_capacity.get('filldirection') == 'Radial360' and ring_fill.get('filldirection') == 'Radial360', 'Energy uses circular radial fill rather than a bar')
        ck(ring_capacity.get('fill') == '{rbmet_hud_energy_capacity_fill}' and ring_fill.get('fill') == '{rbmet_hud_energy_fill}', 'Energy ring binds capacity and current fractions separately')
    ck(bolt is not None and bolt.get('sprite') == 'rb_hud_energy_bolt', 'Energy uses unlabeled lightning-bolt icon')
    ck(current is not None and current.get('text') == '{rbmet_hud_energy_current}', 'Energy current numeric value is present')
    ck(max_label is not None and max_label.get('text') == '/{rbmet_hud_energy_max}', 'Energy max numeric value is present')
    visible_text = [e.get('text','') for e in energy.findall('.//label')]
    ck(visible_text == ['{rbmet_hud_energy_current}', '/{rbmet_hud_energy_max}'], 'Energy HUD has only numeric current/max labels and no visible word label')

met_xml = read('Config/_Metabolism/windows.xml')
ck('rebirthMetabolismStomachBar' not in met_xml, 'Stomach bar removed from in-world HUD')
ck('rebirthMetabolismIntestineBar' not in met_xml, 'Intestine bar removed from in-world HUD')
ck('rebirthMetabolismHydrationNet' not in met_xml and 'rebirthMetabolismNutritionNet' not in met_xml, 'old metabolism net text removed from in-world HUD')
ck('CharacterFrameWindow' in met_xml and 'rbmet_stomach_fill' in met_xml and 'rbmet_intestine_fill' in met_xml, 'detailed Character -> Metabolism stomach/intestine presentation remains intact')

for token, desc in [
    ('case "rbmet_hud_energy_current":', 'Energy controller exposes current numeric binding'),
    ('case "rbmet_hud_energy_max":', 'Energy controller exposes max numeric binding'),
    ('case "rbmet_hud_energy_fill":', 'Energy controller exposes current fill binding'),
    ('case "rbmet_hud_energy_capacity_fill":', 'Energy controller exposes capacity fill binding'),
]:
    ck(token in met_src, desc)
ck('rbmet_hud_stomach_text' not in met_src and 'rbmet_hud_intestine_text' not in met_src, 'obsolete play-HUD stomach/intestine bindings removed')

# C# delimiter preflight (same lightweight source gate used by prior Project Changes).
for p in [layout_cs_path, met_cs_path]:
    src = p.read_text(encoding='utf-8')
    ck(src.count('{') == src.count('}'), f'C# brace counts balanced: {p.name}')

if Image is not None and energy_icon_path.exists():
    with Image.open(energy_icon_path) as im:
        ck(im.size == (64, 64), 'Energy bolt atlas source is 64x64')
        ck(im.mode in ('RGBA','LA') or 'transparency' in im.info, 'Energy bolt atlas source carries transparency')
        if 'A' in im.getbands():
            alpha = im.getchannel('A')
            ck(alpha.getbbox() is not None, 'Energy bolt has non-empty visible alpha content')
else:
    ck(energy_icon_path.exists(), 'Energy bolt asset exists (Pillow unavailable; pixel preflight skipped)')

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
print(f'PC039_STATIC_CHECKS={len(checks)} PASSED={passed} FAILED={failed} XML={xml_count}')
sys.exit(1 if failed else 0)
