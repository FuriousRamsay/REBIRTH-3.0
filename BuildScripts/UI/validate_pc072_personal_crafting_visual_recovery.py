#!/usr/bin/env python3
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
windows = root / 'Config' / 'XUi_InGame' / 'windows.xml'
templates = root / 'Config' / 'XUi_InGame' / 'templates.xml'
xui = root / 'Config' / 'XUi_InGame' / 'xui.xml'
loc = root / 'Config' / 'Localization.csv'
errors = []
checks = []

def ok(name, condition, detail=''):
    if condition:
        checks.append('PASS - ' + name + ((' — ' + detail) if detail else ''))
    else:
        errors.append('FAIL - ' + name + ((' — ' + detail) if detail else ''))

for path in (windows, templates, xui):
    try:
        ET.parse(path)
        checks.append('PASS - XML parse: ' + str(path.relative_to(root)))
    except Exception as e:
        errors.append('FAIL - XML parse: ' + str(path.relative_to(root)) + ' — ' + str(e))

wt = windows.read_text(encoding='utf-8', errors='replace')
tt = templates.read_text(encoding='utf-8', errors='replace')
xt = xui.read_text(encoding='utf-8', errors='replace')
lt = loc.read_text(encoding='utf-8', errors='replace')

# Scope only the Chunk H authored section so legacy/localized names elsewhere do not confuse checks.
start = wt.find('REBIRTH UI REDESIGN - CHUNK H: STATION + NON-STATION CRAFTING')
end = wt.find('REBIRTH UI REDESIGN - CHUNK I:', start)
h = wt[start:end] if start >= 0 and end > start else ''

ok('PC072 recovery marker present', 'PC072 visual recovery' in h)
ok('Recipes header restored', 'text_key="xuiRebirthRecipes" text="RECIPES"' in h)
ok('Eight visible RecipeList rows retained', 'name="recipes" depth="2" rows="8" cols="1"' in h)
ok('Expected Outcome controller retained', 'name="rebirthExpectedOutcome" controller="RebirthCraftOutcomePanel, RebirthUtils"' in h)
ok('Expected Outcome authoritative Success binding retained', 'text="{rebirthoutcomesuccess}"' in h)
ok('Expected Outcome authoritative Quality binding retained', 'text="{rebirthoutcomequality}"' in h)
ok('Expected Outcome authoritative Craft XP binding retained', 'text="{rebirthoutcomexp}"' in h)
ok('Expected Outcome authoritative Result binding retained', 'text="{rebirthoutcomeresults}"' in h)
ok('Unsupported probability rows removed from active PC072 Outcome layout', all(x not in h for x in ('text="{rebirthoutcomehigh}"','text="{rebirthoutcomenormal}"','text="{rebirthoutcomelow}"')))
ok('Deterministic explanation retained', 'xuiRebirthOutcomeDeterministicNote' in h)

# Center hierarchy and native ownership.
pos_summary = h.find('name="recipeSummary"')
pos_actions = h.find('name="actionBand"')
pos_requirements = h.find('name="ingredients"')
ok('Recipe -> Actions -> Requirements hierarchy ordered', 0 <= pos_summary < pos_actions < pos_requirements)
ok('Knowledge binding retained', 'text="{rebirthknowledge}"' in h)
ok('Knowledge Explorer button retained', 'name="btnRebirthCraftKnowledge"' in h)
ok('Cook/Craft time binding retained', 'name="craftingTime"' in h and 'text="{craftingtime}"' in h)
ok('Native RecipeCraftCount retained', 'controller="RecipeCraftCount"' in h)
ok('Native ItemActionList retained', 'controller="ItemActionList"' in h)
ok('Six real Requirements positions retained', 'rows="3" cols="2" pos="0,-28" width="626" height="165"' in h and 'controller="IngredientList"' in h)
ok('Redundant detail tabs kept only as hidden controller children', 'name="detailTabs" pos="-5000,-5000"' in h and 'visible="false"' in h)

# Styling authority from current Survivor Profiles language.
ok('Charcoal main panels used', 'color="12,12,15,252"' in h)
ok('Red primary frames used', 'color="228,18,21,190"' in h)
ok('Purple section headers used', 'color="196,158,255,255"' in h)
ok('Yellow semantic subheads used', 'color="201,198,105,255"' in h)

# Queue: real capacity, compact presentation, visible cancel.
ok('Non-station queue remains real four-entry capacity', 'name="queue" rows="4" cols="1"' in h)
m = re.search(r'name="queue" rows="4" cols="1"[^>]*cell_height="(\d+)"', h)
ok('Queue rows compact rather than full-height cards', bool(m) and int(m.group(1)) <= 110, ('cell_height=' + m.group(1)) if m else 'cell_height missing')
qtpl_start = tt.find('<rebirth_crafting_queue_card>')
qtpl_end = tt.find('</rebirth_crafting_queue_card>', qtpl_start)
qtpl = tt[qtpl_start:qtpl_end] if qtpl_start >= 0 and qtpl_end > qtpl_start else ''
ok('Compact queue template height', 'height="98"' in qtpl)
ok('Queue template keeps native RebirthRecipeStack subclass', 'controller="RebirthRecipeStack, RebirthUtils"' in qtpl)
ok('Queue cancel child visible and localized to a compact control', 'name="cancel"' in qtpl and 'width="26" height="26"' in qtpl and 'color="235,235,240,255"' in qtpl)

# Personal-crafting center must not be replaced by inventory ItemInfo, but only in Rebirth mode.
ok('Rebirth crafting group removes itemInfoPanel overlay', '/xui/window_group[@name=\'crafting\']/window[@name=\'itemInfoPanel\']' in xt)
ok('Rebirth crafting group removes emptyInfoPanel overlay', '/xui/window_group[@name=\'crafting\']/window[@name=\'emptyInfoPanel\']' in xt)
chunk = xt[xt.find("<conditional><if cond=\"character_progression('Rebirth')\">"):]
ok('Crafting group recovery remains inside Rebirth conditional', 'PC072: keep the personal-crafting center recipe-owned' in chunk)
ok('Base Game mode not directly patched by a separate personal-crafting group', 'character_progression(\'Base Game\')' not in xt)

# Scrollbar bridge and non-invention gates.
ok('Recipe stock scrollbar bridge retained', 'name="rebirthCraftingPagerScroll" controller="RebirthPagerScrollbar, RebirthUtils"' in wt)
ok('Hidden native pager retained behind scrollbar bridge', 'windowCraftingList' in wt and 'pager[@name=\'pager\']" name="pos">-5000,-5000' in wt)
active_h = re.sub(r'<!--.*?-->', '', h, flags=re.S)
active_qtpl = re.sub(r'<!--.*?-->', '', qtpl, flags=re.S)
ok('No fabricated 24-slot runtime queue', 'rows="24"' not in active_h and 'cols="24"' not in active_h and '8 / 24' not in active_h)
ok('No crafting weight UI introduced', not re.search(r'text(?:_key)?="[^"]*(?:weight|kg)[^"]*"', active_h, re.I))
ok('No random craft-roll implementation introduced in XUi', not re.search(r'text(?:_key)?="[^"]*(?:random|roll chance|pause queue|clear queue)[^"]*"', active_h, re.I))
ok('Localization key xuiRebirthRecipes present', 'xuiRebirthRecipes,Recipes' in lt)
ok('Localization key xuiRebirthSelectedRecipe present', 'xuiRebirthSelectedRecipe,Selected Recipe' in lt)

print('REBIRTH 3.0 PC072 PERSONAL CRAFTING VISUAL RECOVERY STATIC VALIDATION')
for line in checks:
    print(line)
for line in errors:
    print(line)
print(f'RESULT: {len(checks)} PASS / {len(errors)} FAIL')
if errors:
    sys.exit(1)
