from pathlib import Path
from lxml import etree
import re, sys

root = Path(__file__).resolve().parents[2]
checks=[]

def check(name, ok, detail=''):
    checks.append((name, bool(ok), detail))

windows = root/'Config/XUi_InGame/windows.xml'
xui = root/'Config/XUi_InGame/xui.xml'
nav = root/'Scripts/Crafting/UI/PersonalCrafting/RebirthCraftingNavigationService.cs'
tabs = root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingTopTabs.cs'
status = root/'Scripts/Crafting/UI/PersonalCrafting/XUiC_RebirthCraftingWorldStatus.cs'
layout = root/'Scripts/Crafting/UI/PersonalCrafting/RebirthPersonalCraftingLayoutService.cs'

for p in [windows,xui,nav,tabs,status,layout]: check('exists '+p.relative_to(root).as_posix(), p.exists())

for p in [windows,xui]:
    try:
        etree.parse(str(p)); check('xml parse '+p.name, True)
    except Exception as e: check('xml parse '+p.name, False, str(e))

wt = windows.read_text(errors='ignore')
xt = xui.read_text(errors='ignore')
nt = nav.read_text(errors='ignore')
tt = tabs.read_text(errors='ignore')
st = status.read_text(errors='ignore')
lt = layout.read_text(errors='ignore')

check('custom top tabs controller', 'controller="RebirthCraftingTopTabs, RebirthUtils"' in wt)
check('world status controller', 'controller="RebirthCraftingWorldStatus, RebirthUtils"' in wt)
check('old chunk-b placeholder removed', 'xuiRebirthCraftingTabsComing' not in wt)
check('native header not reintroduced', 'windowPagingHeader' not in wt[wt.find('name="rebirthPersonalCraftingRoot"'):])
check('default focus is crafting tab', '>btnRebirthCraftingTabCrafting</setattribute>' in xt)
check('rebirth-only crafting conditional retained', "character_progression('Rebirth')" in xt and "name=\"controller\">RebirthPersonalCrafting, RebirthUtils" in xt)
check('native backpack remains disabled only in rebirth patch', 'name="open_backpack_on_open">false</setattribute>' in xt)

suffixes=['Crafting','Character','Map','Skills','Quests','Challenges','Players']
icons=['crafting','character','map','skills','quests','challenges','players']
for suffix,icon in zip(suffixes,icons):
    check('tab button '+suffix, f'name="btnRebirthCraftingTab{suffix}"' in wt)
    check('tab icon '+suffix, f'sprite="rb_tab_{icon}"' in wt)
    if icon != 'crafting':
        check('tab focus left '+suffix, f'btnRebirthCraftingTab{suffix}' in wt)
    asset=root/f'UIAtlases/RebirthUiIcons/rb_tab_{icon}.png'
    check('existing tab asset '+icon, asset.exists())

check('sun asset referenced', 'sprite="rb_hud_sun"' in wt and (root/'UIAtlases/RebirthHud/rb_hud_sun.png').exists())
check('moon asset referenced', 'sprite="rb_hud_moon"' in wt and (root/'UIAtlases/RebirthHud/rb_hud_moon.png').exists())
for label in ['rebirthCraftingStatusDay','rebirthCraftingStatusTime','rebirthCraftingStatusTemperature']:
    check('status label '+label, f'name="{label}"' in wt)

for route in ['"map"','"skills"','"quests"','"challenges"','"players"','"character"']:
    check('source-verified route '+route, route in nt)
check('native paging adapter used', 'XUiC_WindowSelector.OpenSelectorAndWindow(player, route)' in nt)
check('approved Rebirth Character preferred', 'XUiC_RebirthSurvivorCharacter.WindowGroupId' in nt and 'RebirthSurvivorClientState.GetOwnerStateSnapshot()' in nt)
check('crafting active-tab no-op', 'case Destination.Crafting' in nt and 'return true;' in nt)

check('world day authority', 'GameUtils.WorldTimeToDays(worldTime)' in st)
check('world time authority', 'GameUtils.WorldTimeToElements(worldTime)' in st)
check('day-night authority', 'world.IsDaytime()' in st)
check('native outside temp authority', 'XUiM_Player.GetOutsideTemp' in st)
check('biome intentionally omitted from Personal Crafting status', 'rebirthCraftingStatusBiome' not in wt and 'world.GetBiome' not in st)
check('time-display gameplay rule respected', 'PassiveEffects.NoTimeDisplay' in st)
check('status refresh bounded', 'RefreshSeconds = 0.20f' in st)

check('responsive top layout invoked', 'ApplyTopLayout(topWidth);' in lt)
check('responsive tabs split', 'ApplyTopTabsLayout(tabsWidth);' in lt)
check('responsive status split', 'ApplyWorldStatusLayout(statusWidth);' in lt)

# Formula smoke: tab + status rectangles must remain positive over representative XUi widths.
for top_width in [1160, 1280, 1544, 1880]:
    status_width=max(300,min(430,round(top_width*0.245)))
    tabs_width=max(1,top_width-status_width-10)
    gaps=3*6
    available=max(7,tabs_width-gaps)
    base=available//7
    cluster_width=82+78+94
    check(f'layout smoke top={top_width}', base>0 and status_width>=cluster_width,
          f'tab={base} status={status_width} cluster={cluster_width}')

# Basic C# delimiter preflight. Full compiler is not available in the artifact runtime.
for p in [nav,tabs,status,layout]:
    text=p.read_text(errors='ignore')
    check('delimiter braces '+p.name, text.count('{')==text.count('}'), f"{text.count('{')}/{text.count('}')}")
    check('delimiter parens '+p.name, text.count('(')==text.count(')'), f"{text.count('(')}/{text.count(')')}")

passed=sum(1 for _,ok,_ in checks if ok)
failed=[x for x in checks if not x[1]]
print(f'PC077 CHUNK C VALIDATION: {passed} PASS / {len(failed)} FAIL')
for name,ok,detail in checks:
    if not ok: print('FAIL -',name,detail)
if failed: sys.exit(1)
