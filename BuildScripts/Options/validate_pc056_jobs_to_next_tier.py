#!/usr/bin/env python3
from pathlib import Path
import csv
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
checks = []

def ok(name, condition, detail=''):
    checks.append((name, bool(condition), detail))

def read(rel):
    return (ROOT / rel).read_text(encoding='utf-8-sig')

# XML and localization
menu = ROOT / 'Config/XUi_Menu/windows.xml'
try:
    ET.parse(menu)
    ok('XUi_Menu/windows.xml parses', True)
except Exception as exc:
    ok('XUi_Menu/windows.xml parses', False, str(exc))

loc_path = ROOT / 'Config/Localization.csv'
loc_keys = []
with loc_path.open('r', encoding='utf-8-sig', newline='') as f:
    for row in csv.reader(f):
        if row:
            loc_keys.append(row[0])
ok('Jobs-to-next-tier localization title exists once', loc_keys.count('xuiRebirthJobsToNextTier') == 1)
ok('Jobs-to-next-tier localization description exists once', loc_keys.count('xuiRebirthJobsToNextTierDesc') == 1)

manager = read('Scripts/Options/RebirthSandboxOptionManager.cs')
trader = read('Scripts/TraderJobs/RebirthTraderJobs.cs')
installer = read('Scripts/TraderJobs/RebirthTraderJobsInstaller.cs')
options = read('Scripts/UI/XUiC_RebirthSandboxOptions.cs')
randomizer = read('Scripts/UI/XUiC_RebirthSandboxOptions.Randomizer.cs')
summary = read('Scripts/UI/XUiC_RebirthSandboxSummary.cs')
compat = read('Scripts/AdvancedFarming/AdvancedFarmingRuntimePolicy.cs')
compass = read('Scripts/UI/XUiC_RebirthCompassWindow.cs')
menu_text = read('Config/XUi_Menu/windows.xml')

# Exact REBIRTH 2.6 option values and default.
ok('Exact 2.6 values are present', 'AllowedJobsToNextTier = { 6, 8, 10, 12, 14, 16, 18, 20 }' in trader)
ok('Default jobs-to-next-tier is 10', 'DefaultJobsToNextTier = 10' in trader)
ok('New option ID appended without renumbering older IDs', 'LiteratureStudyTime = 62,\n    JobsToNextTier = 63' in manager)

# State/runtime/persistence path.
for label, needle in [
    ('Sandbox state owns JobsToNextTier', 'public int JobsToNextTier = RebirthTraderJobPolicy.DefaultJobsToNextTier;'),
    ('Sandbox state clone preserves JobsToNextTier', 'JobsToNextTier = JobsToNextTier,'),
    ('Manager exposes JobsToNextTier', 'public int JobsToNextTier { get { return currentState.JobsToNextTier; } }'),
    ('Runtime applies JobsToNextTier', 'RebirthTraderJobPolicy.SetJobsToNextTier(currentState.JobsToNextTier);'),
    ('Sandbox code encodes JobsToNextTier', 'RebirthSandboxOptionId.JobsToNextTier) + IndexToAlpha(RebirthTraderJobPolicy.JobsToNextTierToIndex(state.JobsToNextTier))'),
    ('Sandbox code decodes JobsToNextTier', 'decoded.JobsToNextTier = decodedJobsToNextTier;'),
]:
    ok(label, needle in manager)

# UI bindings / randomizer / summary.
for label, needle, source in [
    ('Trader tab contains Jobs to Next Tier row', 'name="jobsToNextTierOption"', menu_text),
    ('Trader tab contains Jobs to Next Tier combo', 'name="cbxJobsToNextTier"', menu_text),
    ('Trader tab contains Jobs to Next Tier randomizer lock', 'name="btnJobsToNextTierRandomizerLock"', menu_text),
    ('Combo component is bound', '[XuiBindComponent("cbxJobsToNextTier", true)]', options),
    ('Combo change event exists', 'JobsToNextTier_OnValueChanged', options),
    ('Combo is populated from exact allowed values', 'RebirthTraderJobPolicy.AllowedJobsToNextTier.Length', options),
    ('Randomizer includes JobsToNextTier', 'RandomizeIndexedIntegerOption(RebirthSandboxOptionId.JobsToNextTier', options),
    ('Randomizer lock bindings exist', 'rebirth_jobs_to_next_tier_randomizer_lock_sprite', randomizer),
    ('Sandbox summary reports changed JobsToNextTier', 'xuiRebirthJobsToNextTier', summary),
]:
    ok(label, needle in source)

# Runtime tier calculation and Harmony install.
ok('2.6 weighted tier calculation exists', 'remaining -= tier * perTier;' in trader)
ok('GetCurrentFactionTier patch explicitly targets byte,int,bool overload',
   'nameof(QuestJournal.GetCurrentFactionTier), new Type[] { typeof(byte), typeof(int), typeof(bool) }' in trader)
ok('GetCurrentFactionTier patch is explicitly installed', 'typeof(RebirthTraderJobsToNextTierPatch)' in installer)

# Compile-error compatibility and compass source of truth.
ok('RebirthVariables compatibility field now exists', 'public static int customJobsToNextTier = 10;' in compat)
ok('Compass uses 3.0 trader policy', 'RebirthTraderJobPolicy.JobsToNextTier' in compass)
ok('Compass no longer directly reads missing compatibility field', 'RebirthVariables.customJobsToNextTier' not in compass)

# Formula sanity: with N configured jobs, exactly N jobs of each current tier unlock the next tier.
def tier_for(points, n, max_tier=6):
    remaining = max(0, points)
    for tier in range(1, 100):
        remaining -= tier * n
        if remaining < 0:
            return min(tier, max_tier)
    return 1

for n in (6, 8, 10, 12, 14, 16, 18, 20):
    cumulative = 0
    good = True
    for current_tier in range(1, 6):
        # One fewer same-tier job must still be current tier.
        points_before = cumulative + (n - 1) * current_tier
        if tier_for(points_before, n) != current_tier:
            good = False
        # Nth same-tier job reaches next tier.
        cumulative += n * current_tier
        if tier_for(cumulative, n) != current_tier + 1:
            good = False
    ok(f'Progression threshold sanity for {n} jobs/tier', good)

# Basic source delimiter sanity on modified C# files. This is intentionally not a compiler.
def strip_cs(text):
    # Remove strings/chars/comments enough for delimiter-count validation.
    text = re.sub(r'/\*.*?\*/', '', text, flags=re.S)
    text = re.sub(r'//.*', '', text)
    text = re.sub(r'@"(?:[^"]|"")*"', '""', text)
    text = re.sub(r'"(?:\\.|[^"\\])*"', '""', text)
    text = re.sub(r"'(?:\\.|[^'\\])'", "''", text)
    return text

for rel in [
    'Scripts/AdvancedFarming/AdvancedFarmingRuntimePolicy.cs',
    'Scripts/Options/RebirthSandboxOptionManager.cs',
    'Scripts/TraderJobs/RebirthTraderJobs.cs',
    'Scripts/TraderJobs/RebirthTraderJobsInstaller.cs',
    'Scripts/UI/XUiC_RebirthCompassWindow.cs',
    'Scripts/UI/XUiC_RebirthSandboxOptions.Randomizer.cs',
    'Scripts/UI/XUiC_RebirthSandboxOptions.cs',
    'Scripts/UI/XUiC_RebirthSandboxSummary.cs',
]:
    t = strip_cs(read(rel))
    ok(rel + ' braces balanced', t.count('{') == t.count('}'), f"{{={t.count('{')} }}={t.count('}')}")
    ok(rel + ' parentheses balanced', t.count('(') == t.count(')'), f"(={t.count('(')} )={t.count(')')}")

passed = sum(1 for _, result, _ in checks if result)
failed = len(checks) - passed
for name, result, detail in checks:
    suffix = (' :: ' + detail) if detail else ''
    print(('PASS' if result else 'FAIL') + ' - ' + name + suffix)
print(f'\nSUMMARY: {passed} / {len(checks)} checks passed.')
print('Static/source validation only. A Visual Studio/game build and live host/dedicated-client test remain authoritative.')
sys.exit(1 if failed else 0)
