from pathlib import Path
import re, sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
xml=(root/'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8')
cs=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
bg=ET.parse(root/'Config/_Survivor/backgrounds.xml').getroot()
errors=[]
for i in range(8):
    for marker in [f'name="profileStartingItemRow{i}"',f'name="profileStartingItemIcon{i}"',f'name="profileStartingItemName{i}"',f'name="profileStartingItemMeta{i}"']:
        if marker not in xml: errors.append('missing '+marker)
if 'name="selectedProfileStartingItems" pos="-5000,-5000"' not in xml: errors.append('legacy selectedProfileStartingItems label must be hidden')
if 'StartingItemVisibleRows = 8' not in cs: errors.append('profile manager starter row capacity is not 8')
for marker in ['RenderStartingItems(startingItems);','SetStartingItemIcon(startingItemIcons[i], item);','StartingItemMeta(item)','TryGetStartingItemIconOverride']:
    if marker not in cs: errors.append('missing controller marker '+marker)
for item_id, icon in {
    'ItemsWeaponsCleaver001_FR':'FR_Cleaver_icon',
    'ItemsWeaponsJunkBaton001_FR':'ItemsWeaponsJunkBaton001_FR',
    'ItemsWeaponsScythe004_FR':'ItemsWeaponsScythe004_FR',
    'FuriousRamsayFountainPen':'FR_FountainPen_icon',
    'FuriousRamsayHammerPliers':'FR_HammerPliers_icon',
    'FuriousRamsayScrewdriver':'FR_Screwdriver_icon',
    'FuriousRamsayPliers':'FR_Pliers_icon',
    'guppyFireExtinguisherItem':'guppyFireExtinguisher',
    'FuriousRamsayPropaneTank':'FR_SM_Propane_icon',
    'FuriousRamsaySeedBundle':'bundleFarm',
    'FuriousRamsayWaterTank':'cntBarrelPlasticSingle00'
}.items():
    if item_id not in cs or icon not in cs: errors.append('missing starter icon mapping '+item_id+' -> '+icon)
aliases=list((root/'UIAtlases/RebirthSurvivorIcons').glob('rb_starting_item_*.png'))
if aliases: errors.append('renamed starter-item icon aliases still exist: '+','.join(p.name for p in aliases))
max_items=0
for b in bg.findall('.//background'):
    max_items=max(max_items,len(b.findall('./starting_items/item')))
if max_items>8: errors.append(f'authored background has {max_items} starting items, exceeds 8 rows')
if errors:
    print('FAIL')
    for e in errors: print('-',e)
    sys.exit(1)
print('PASS profile starting items rendered as icon rows; authored max=',max_items)
