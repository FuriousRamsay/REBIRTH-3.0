#!/usr/bin/env python3
from pathlib import Path
from lxml import etree
import csv, sys

root=Path('.')
if '--root' in sys.argv:
    i=sys.argv.index('--root'); root=Path(sys.argv[i+1])
errors=[]

bg=etree.parse(str(root/'Config/_Survivor/backgrounds.xml'))
backgrounds=bg.xpath('//background')
if len(backgrounds)!=28: errors.append(f'expected 28 backgrounds, found {len(backgrounds)}')

with (root/'Config/Localization.csv').open(encoding='utf-8-sig',errors='ignore') as f:
    loc={r[0] for r in csv.reader(f) if r}

for b in backgrounds:
    items=b.xpath('./starting_items/item')
    if not items: errors.append(f"{b.get('id')}: no starting items")
    torches=[x for x in items if x.get('id')=='meleeToolTorch']
    expected_torches=0 if b.get('id')=='background.miner' else 1
    if len(torches)!=expected_torches: errors.append(f"{b.get('id')}: expected {expected_torches} starting torch(s), found {len(torches)}")
    seen=set()
    for item in items:
        iid=item.get('id',''); nk=item.get('name_key','')
        if not iid: errors.append(f"{b.get('id')}: empty item id")
        if iid.lower() in seen: errors.append(f"{b.get('id')}: duplicate {iid}")
        seen.add(iid.lower())
        if nk not in loc: errors.append(f"{b.get('id')}: missing localization {nk}")
        try:
            if int(item.get('count','1'))<1: errors.append(f"{b.get('id')}: bad count {iid}")
        except: errors.append(f"{b.get('id')}: noninteger count {iid}")

farmer=bg.xpath("//background[@id='background.farmer']/starting_items/item")
fmap={x.get('id'):x for x in farmer}
if fmap.get('FuriousRamsaySeedBundle') is None or fmap['FuriousRamsaySeedBundle'].get('count')!='2': errors.append('Farmer must have 2 seed bundles')
if 'FuriousRamsayWaterTank' not in fmap: errors.append('Farmer missing Water Barrel')
if 'bucketEmpty' not in fmap: errors.append('Farmer missing Empty Bucket')

for forbidden in ['toolBellows','resourceHeadlight','armorWorkingStiffsHelmet']:
    if bg.xpath(f"//starting_items/item[@id='{forbidden}']"): errors.append(f'forbidden starter item present: {forbidden}')
miner_light=bg.xpath("//background[@id='background.miner']/starting_items/item[@id='modArmorHelmetLight']")
if len(miner_light)!=1: errors.append('Miner must have one Helmet Light Mod starter')
# Ranged starter items are normally avoided, but the source-authority revision explicitly
# approves a Wooden Bow for Hunter and a Pistol for Gunsmith. Reject any unapproved extras.
approved_ranged={
    ('background.hunter','gunBowT1WoodenBow'),
    ('background.gunsmith','gunHandgunT1Pistol'),
}
for b in backgrounds:
    for item in b.xpath('./starting_items/item'):
        iid=item.get('id','')
        low=iid.lower()
        if low.startswith('gun') or 'rifle' in low or 'pistol' in low or 'shotgun' in low or 'bow' in low:
            if (b.get('id'),iid) not in approved_ranged:
                errors.append(f'unapproved ranged/firearm starter present: {b.get("id")} -> {iid}')

xui=(root/'Config/XUi_Menu/windows.xml').read_text(encoding='utf-8')
for marker in ['selectedProfileStartingItems','backgroundStartingItemRow0','reviewStartingItemsHeading','reviewStartingItemRow0']:
    if marker not in xui: errors.append(f'missing UI marker {marker}')

creator=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorCreator.cs').read_text(encoding='utf-8')
profile=(root/'Scripts/Survivor/UI/XUiC_RebirthSurvivorProfileManager.cs').read_text(encoding='utf-8')
uitext=(root/'Scripts/Survivor/UI/RebirthSurvivorUiText.cs').read_text(encoding='utf-8')
for marker in ['StartingItemsInline','StartingItemsBullets']:
    if marker not in uitext: errors.append(f'missing text helper {marker}')
if 'backgroundStartingItemRows' not in creator or 'reviewStartingItemRows' not in creator: errors.append('Creator controller not wired to starter item UI')
if 'selectedProfileStartingItems' not in profile: errors.append('Profile Manager not wired to starter item UI')

print(f'Background Starting Items UI audit errors={len(errors)} backgrounds={len(backgrounds)} entries={len(bg.xpath("//starting_items/item"))}')
for e in errors: print('ERROR:',e)
sys.exit(1 if errors else 0)
