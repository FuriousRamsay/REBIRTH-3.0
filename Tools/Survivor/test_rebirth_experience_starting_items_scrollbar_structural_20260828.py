#!/usr/bin/env python3
from pathlib import Path
import sys
from lxml import etree

root = Path('.')
if '--root' in sys.argv:
    i = sys.argv.index('--root')
    root = Path(sys.argv[i + 1])

errors = []
xui_path = root / 'Config/XUi_Menu/windows.xml'
creator_path = root / 'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs'
xui = xui_path.read_text(encoding='utf-8')
creator = creator_path.read_text(encoding='utf-8')
etree.parse(str(xui_path))

start = xui.find('name="backgroundStartingItemsNativeScrollHost"')
end = xui.find('name="backgroundStartingItemsScrollCapture"', start)
section = xui[start:end]
if start < 0 or end < 0:
    errors.append('Starting Items native-scroll section missing')
else:
    if 'visible="true"' not in section:
        errors.append('Starting Items native-scroll host must be authored visible=true')
    if '<defaultscrollbar/>' not in section:
        errors.append('Starting Items default scrollbar missing')
    if 'name="backgroundStartingItemsNativeScrollProxy"' not in section or 'height="215"' not in section:
        errors.append('Starting Items proxy must begin with overflow height=215')

if 'backgroundStartingItemsNativeScrollHost.ViewComponent.IsVisible = true;' not in creator:
    errors.append('controller must keep Starting Items native-scroll host active')
if 'backgroundStartingItemsNativeLayoutRefreshFrames' not in creator:
    errors.append('delayed native layout refresh safeguard missing')

required_icons = [
    'FR_FountainPen_icon.png',
    'FR_HammerTool_icon.png',
    'FR_HammerPliers_icon.png',
    'FR_Screwdriver_icon.png',
    'FR_Pliers_icon.png',
    'ItemsWeaponsBaton001_FR.png',
    'FR_Cleaver_icon.png',
    'ItemsWeaponsScythe001_FR.png',
    'ItemsWeaponsCrowbar001_FR.png',
]
for name in required_icons:
    p = root / 'UIAtlases/ItemIconAtlas' / name
    if not p.is_file() or p.stat().st_size == 0:
        errors.append('missing starter item icon ' + name)

print('Experience Starting Items structural scrollbar/icon audit errors=' + str(len(errors)))
for e in errors:
    print('ERROR:', e)
sys.exit(1 if errors else 0)
