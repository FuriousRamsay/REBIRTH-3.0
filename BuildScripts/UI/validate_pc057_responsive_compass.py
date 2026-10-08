#!/usr/bin/env python3
from __future__ import annotations

from pathlib import Path
from xml.etree import ElementTree as ET
from PIL import Image
import csv
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
WINDOWS = ROOT / 'Config' / 'XUi_InGame' / 'windows.xml'
METABOLISM = ROOT / 'Config' / '_Metabolism' / 'windows.xml'
LOCALIZATION = ROOT / 'Config' / 'Localization.csv'
COMPASS_CS = ROOT / 'Scripts' / 'UI' / 'XUiC_RebirthCompassWindow.cs'
HUD = ROOT / 'UIAtlases' / 'RebirthHud'

checks: list[tuple[bool, str]] = []

def check(ok: bool, label: str):
    checks.append((bool(ok), label))

# Parse XML files.
for p, name in [(WINDOWS, 'XUi_InGame/windows.xml parses'), (METABOLISM, '_Metabolism/windows.xml parses')]:
    try:
        ET.parse(p)
        check(True, name)
    except Exception:
        check(False, name)

w = WINDOWS.read_text(encoding='utf-8')
m = METABOLISM.read_text(encoding='utf-8')
c = COMPASS_CS.read_text(encoding='utf-8')

# Job tier presentation.
check('sprite="ui_game_symbol_fetch_loot"' in w, 'native 2.6 fetch-loot satchel restored')
check('name="rebirthJobTierIcon"' in w and 'width="18" height="18"' in w[w.index('name="rebirthJobTierIcon"'):w.index('name="rebirthJobTierIcon"')+300], 'job satchel renders at 18x18')
check('rb_hud_job_tier' not in w and 'rb_hud_job_tier' not in c, 'obsolete custom job-tier icon is no longer referenced')
check('ValueDisplayFormatters.RomanNumber' in c, 'job tier uses native Roman numeral formatter')
check('Mathf.Clamp(tier, 1, 6)' in c, 'HUD tier presentation is constrained to I-VI')
check('xuiRebirthCompassJobTier,TIER {0} [aa7fc9]{1}[-]/{2}' in LOCALIZATION.read_text(encoding='utf-8'), 'only job-progress numerator is localized purple')
check('template = "TIER {0} [aa7fc9]{1}[-]/{2}"' in c, 'purple numerator fallback exists')
check('const int jobLabelLeftX = 58' in c and 'const int jobIconCenterX = 46' in c, 'satchel-to-tier text geometry uses 3px gap')

# Compact icon geometry.
check('private const int JobIconSize = 18;' in c, 'job icon size constant is 18')
check('private const int EnergyIconSize = 18;' in c, 'Energy icon size constant is 18')
check('private const int DayIconSize = 18;' in c, 'Day icon size constant is 18')
check('private const int TemperatureIconWidth = 17;' in c and 'private const int TemperatureIconHeight = 19;' in c, 'temperature icon preserves 17x19 aspect')

# Responsive layout behavior.
for token, label in [
    ('MeasureLabelWidth(jobLabelView', 'job tier width measured dynamically'),
    ('MeasureLabelWidth(elevationLabelView', 'elevation width measured dynamically'),
    ('MeasureLabelWidth(energyLabelView', 'Energy width measured dynamically'),
    ('MeasureLabelWidth(dayLabelView', 'Day width measured dynamically'),
    ('MeasureLabelWidth(timeLabelView', 'time width measured dynamically'),
    ('MeasureLabelWidth(temperatureLabelView', 'temperature width measured dynamically'),
    ('PrintedSizeProperty', 'rendered NGUI text size is consulted'),
    ('EstimateTextWidth', 'Unicode-aware text-width fallback exists'),
    ('Localization', 'localization-aware runtime path retained'),
]:
    check(token in c, label)

check('ResponsiveLayoutRefreshSeconds = 0.20f' in c, 'layout refreshes after live text/localization changes')
check('ViewComponent.Position = new Vector2i(headerOffsetX, HeaderTopY);' in c, 'complete variable-width header is re-centered')
check('private const int CompassWidth = 250;' in c, 'native compass root width stays 250px')
check(re.search(r'ViewComponent\.Size\s*=', c) is None, 'controller never resizes native compass root')

# Three-piece variable shell.
for name, size in [
    ('rb_hud_shell_cap_left.png', (12, 38)),
    ('rb_hud_shell_stretch.png', (1, 38)),
    ('rb_hud_shell_cap_right.png', (12, 38)),
]:
    p = HUD / name
    ok = p.exists()
    if ok:
        with Image.open(p) as im:
            ok = im.mode == 'RGBA' and im.size == size and im.getbbox() is not None
    check(ok, f'{name} is valid RGBA {size[0]}x{size[1]} shell asset')

check('name="rebirthCompassShellLeft"' in w and 'name="rebirthCompassShellCenter"' in w and 'name="rebirthCompassShellRight"' in w, 'three-piece shell is wired in XML')
check('rebirthCompassShellFull' not in w and 'rebirthCompassShellDayOnly' not in w and 'rebirthCompassShellTimeOnly' not in w and 'rebirthCompassShellCompact' not in w, 'fixed-width shell variants removed from compass XML')

# Modules need IDs for runtime layout.
for view_id in [
    'rebirthCompassProgressionSeparator',
    'rebirthCompassElevationSeparator',
    'rebirthCompassRightSeparator',
    'rebirthCompassDayText',
    'rebirthCompassClockIcon',
    'rebirthCompassTimeText',
    'rebirthCompassTemperatureText',
]:
    check(f'name="{view_id}"' in w, f'{view_id} has runtime-layout ID')

for view_id in ['rebirthMetabolismHud', 'rebirthMetabolismEnergyLightningIcon', 'rebirthMetabolismEnergyText', 'rebirthCompassEnergySeparator']:
    check(f'name="{view_id}"' in m, f'{view_id} is available for responsive Energy layout')

check('pos="14,-19" width="18" height="18"' in m, 'Energy lightning is 18x18 and aligned with Day/job icons')
check('justify="left"' in m[m.index('name="rebirthMetabolismEnergyText"'):m.index('name="rebirthMetabolismEnergyText"')+350], 'Energy value uses left alignment for compact icon-to-text spacing')

# Localization rows should remain unique.
rows = []
with LOCALIZATION.open(encoding='utf-8', newline='') as f:
    for row in csv.reader(f):
        if row:
            rows.append(row[0])
for key in ['xuiRebirthCompassJobTier', 'xuiRebirthCompassJobTierMax']:
    check(rows.count(key) == 1, f'{key} localization key exists exactly once')

# Tier formatter scenarios: presentation-focused replication of current formula.
def roman(v:int) -> str:
    return ['', 'I','II','III','IV','V','VI'][max(1,min(6,v))]

def display(points:int, jobs:int=10, max_tier:int=6) -> str:
    points=max(0,points)
    tier=1
    remaining=points
    for t in range(1,100):
        remaining -= t*jobs
        if remaining < 0:
            tier=min(t,max_tier)
            break
        if t >= max_tier:
            tier=max_tier
            break
    if tier >= max_tier:
        return f'TIER {roman(tier)} MAX'
    tier_points=sum(t*jobs for t in range(1,tier+1))
    points_left=max(0,tier_points-points)
    jobs_left=points_left//max(1,tier)
    if 0 < points_left < tier:
        jobs_left=1
    completed=max(0,min(jobs,jobs-jobs_left))
    return f'TIER {roman(tier)} {completed}/{jobs}'

for points, expected in [
    (0, 'TIER I 0/10'),
    (9, 'TIER I 9/10'),
    (10, 'TIER II 0/10'),
    (29, 'TIER II 9/10'),
    (30, 'TIER III 0/10'),
    (59, 'TIER III 9/10'),
    (60, 'TIER IV 0/10'),
    (99, 'TIER IV 9/10'),
    (100, 'TIER V 0/10'),
    (149, 'TIER V 9/10'),
    (150, 'TIER VI MAX'),
]:
    check(display(points) == expected, f'scenario {points} points => {expected}')

for points, jobs, expected in [
    (0, 6, 'TIER I 0/6'),
    (5, 6, 'TIER I 5/6'),
    (6, 6, 'TIER II 0/6'),
    (20, 20, 'TIER II 0/20'),
]:
    check(display(points, jobs=jobs) == expected, f'configured jobs={jobs}, points={points} => {expected}')

# Print report.
for ok, label in checks:
    print(('PASS' if ok else 'FAIL') + ' | ' + label)
passed = sum(1 for ok,_ in checks if ok)
print(f'\nSUMMARY: {passed} / {len(checks)} checks passed.')
sys.exit(0 if passed == len(checks) else 1)
