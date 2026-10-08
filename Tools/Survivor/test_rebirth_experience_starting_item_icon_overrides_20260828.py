#!/usr/bin/env python3
from pathlib import Path
import sys
from lxml import etree

root=Path('.')
if '--root' in sys.argv:
    i=sys.argv.index('--root'); root=Path(sys.argv[i+1])
errors=[]
creator=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8')
profile=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
bg=etree.parse(str(root/'Config/_Survivor/backgrounds.xml'))

# All custom starter art must use the original 2.6/base-game ItemIconAtlas sprite name.
required_map={
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
    'FuriousRamsayWaterTank':'cntBarrelPlasticSingle00',
    'qt_claude':'treasureQuestMaster',
    'modArmorHelmetLight':'modArmorHelmetLight',
}
for alias in (root/'UIAtlases/RebirthSurvivorIcons').glob('rb_starting_item_*.png'):
    errors.append('duplicate/renamed starter icon must not exist: '+alias.name)
for code_name,code in [('creator',creator),('profile',profile)]:
    if 'TryGetStartingItemIconOverride' not in code:
        errors.append(code_name+': explicit starter icon resolver missing')
    for iid,icon in required_map.items():
        if f'case "{iid}"' not in code:
            errors.append(code_name+': missing icon override case '+iid)
        if f'icon = "{icon}"' not in code:
            errors.append(code_name+': missing original ItemIconAtlas sprite '+iid+' -> '+icon)
        # Every explicit custom mapping uses ItemIconAtlas, never the Survivor atlas.
        case_pos=code.find(f'case "{iid}"')
        if case_pos >= 0:
            frag=code[case_pos:case_pos+220]
            if 'atlas = "RebirthSurvivorIcons"' in frag:
                errors.append(code_name+': '+iid+' still points at RebirthSurvivorIcons')

# Original 2.6 custom art files that are not base-game sprites must actually be packaged under their original names.
for fn in [
    'FR_Cleaver_icon.png','ItemsWeaponsJunkBaton001_FR.png','ItemsWeaponsScythe004_FR.png',
    'FR_FountainPen_icon.png','FR_HammerPliers_icon.png','FR_Screwdriver_icon.png','FR_Pliers_icon.png',
    'FR_SM_Propane_icon.png'
]:
    p=root/'UIAtlases/ItemIconAtlas'/fn
    if not p.is_file() or p.stat().st_size==0:
        errors.append('missing original ItemIconAtlas file: '+fn)

# Fire extinguisher/base-game sprites can already be provided by the project/base atlas; no renamed aliases permitted.
if not bg.xpath("//starting_items/item[@id='FuriousRamsayPropaneTank']"):
    errors.append('Scavenger actual 2.6 Propane Tank starter missing')
if bg.xpath("//starting_items/item[@id='tankPropaneWhite']"):
    errors.append('Scavenger still references decorative tankPropaneWhite block')

print('Experience starter original-icon audit errors='+str(len(errors)))
for e in errors: print('ERROR:',e)
sys.exit(1 if errors else 0)
