#!/usr/bin/env python3
from pathlib import Path
import re, sys, xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
errors=[]
creator=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8')
profile=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
legacy=(root/'Config/_Survivor/starter_legacy_items.xml').read_text(encoding='utf-8')
expected={
 'ItemsWeaponsCleaver001_FR':'FR_Cleaver_icon',
 'ItemsWeaponsJunkBaton001_FR':'ItemsWeaponsJunkBaton001_FR',
 'ItemsWeaponsScythe004_FR':'ItemsWeaponsScythe004_FR',
 'FuriousRamsayFountainPen':'FR_FountainPen_icon',
 'FuriousRamsayHammerPliers':'FR_HammerPliers_icon',
 'FuriousRamsayScrewdriver':'FR_Screwdriver_icon',
 'FuriousRamsayPliers':'FR_Pliers_icon',
 'FuriousRamsayPropaneTank':'FR_SM_Propane_icon',
}
for iid,icon in expected.items():
    if f'<item name="{iid}">' not in legacy: errors.append('missing real item definition '+iid)
    # Find within a moderate item block window.
    pos=legacy.find(f'<item name="{iid}">')
    frag=legacy[pos:pos+1800] if pos>=0 else ''
    if f'<property name="CustomIcon" value="{icon}"' not in frag: errors.append(iid+' items.xml CustomIcon is not '+icon)
    for name,code in [('creator',creator),('profile',profile)]:
        pos=code.find(f'case "{iid}"')
        frag=code[pos:pos+220] if pos>=0 else ''
        if 'atlas = "ItemIconAtlas"' not in frag or f'icon = "{icon}"' not in frag:
            errors.append(name+' does not map '+iid+' directly to ItemIconAtlas/'+icon)
    p=root/'UIAtlases/ItemIconAtlas'/(icon+'.png')
    if not p.exists(): errors.append('missing original icon file '+p.name)
for p in (root/'UIAtlases/RebirthSurvivorIcons').glob('rb_starting_item_*.png'):
    errors.append('duplicate alias still packaged: '+p.name)
for p in root.rglob('*'):
    if p.is_file() and p.suffix.lower() in {'.cs','.xml','.csv'}:
        try: text=p.read_text(encoding='utf-8',errors='ignore')
        except: continue
        if 'rb_starting_item_' in text: errors.append('obsolete rb_starting_item_ reference in '+str(p.relative_to(root)))
print('Original starter icon-name audit errors='+str(len(errors)))
for e in errors: print('ERROR:',e)
sys.exit(1 if errors else 0)
