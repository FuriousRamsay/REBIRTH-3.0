#!/usr/bin/env python3
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]
BG=ROOT/'Config/_Survivor/backgrounds.xml'
LOC=ROOT/'Config/Localization.csv'
LEGACY=ROOT/'Config/_Survivor/starter_legacy_items.xml'
ITEMS=ROOT/'Config/items.xml'
REBIRTH_ITEMS=ROOT/'Config/_Rebirth/items_starter.xml'
REBIRTH_BUFFS=ROOT/'Config/_Rebirth/buffs.xml'

root=ET.parse(BG).getroot()
bgs={b.get('id'): b for b in root.findall('.//background')}
def ids(bgid): return [x.get('id') for x in bgs[bgid].find('starting_items').findall('item')]
def item(bgid,iid):
    for x in bgs[bgid].find('starting_items').findall('item'):
        if x.get('id')==iid:return x
    return None

def require(cond,msg):
    if not cond: raise AssertionError(msg)

# User-requested removals/replacements.
require('meleeWpnBladeT1HuntingKnife' not in ids('background.butcher'),'Butcher still has hunting knife')
require('meleeWpnBladeT1HuntingKnife' in ids('background.chef') and 'ItemsWeaponsCleaver001_FR' not in ids('background.chef'),'Chef knife/cleaver replacement wrong')
require(all(x in ids('background.electrician') for x in ['generatorbank','smallEngine']),'Electrician generator/engine missing')
require('carBattery' not in ids('background.electrician') and 'medicalFirstAidBandage' not in ids('background.electrician'),'Electrician old battery/bandage remains')
require(all(x not in ids('background.engineer') for x in ['meleeToolWireTool','toolBeaker']),'Engineer old wire tool/beaker remains')
require(all(x in ids('background.engineer') for x in ['FuriousRamsayHammerPliers','FuriousRamsayFountainPen']),'Engineer replacements missing')
require('resourceMechanicalParts' not in ids('background.engineer'),'Engineer mechanical parts must be replaced by Fountain Pen')
require('ItemsWeaponsScythe004_FR' in ids('background.farmer') and 'ItemsWeaponsScythe001_FR' not in ids('background.farmer'),'Farmer must use real Rusty Sickle id')
require('drinkJarRedTea' not in ids('background.farmer') and 'resourceDuctTape' not in ids('background.farmer'),'Farmer tea/duct tape remains')
require('gunHandgunT1Pistol' in ids('background.gunsmith') and item('background.gunsmith','ammo9mmBulletBall').get('count')=='50','Gunsmith pistol/50 9mm missing')
require('meleeToolSalvageT1Wrench' not in ids('background.gunsmith') and 'resourceOil' not in ids('background.gunsmith'),'Gunsmith wrench/oil remains')
require('gunBowT1WoodenBow' in ids('background.hunter') and item('background.hunter','ammoArrowIron').get('count')=='50','Hunter bow/50 iron arrows missing')
require('drinkJarRedTea' not in ids('background.hunter'),'Hunter still has Red Tea')
require('drinkJarYuccaJuice' in ids('background.logger') and 'drinkJarRedTea' not in ids('background.logger'),'Logger Yucca Juice replacement wrong')
require('carBattery' in ids('background.mechanic') and 'resourceOil' not in ids('background.mechanic'),'Mechanic battery replacement wrong')
require('drinkJarGoldenRodTea' in ids('background.miner') and 'drinkJarCoffee' not in ids('background.miner'),'Miner Goldenrod Tea replacement wrong')
require('qt_claude' in ids('background.park_ranger_outdoor_guide') and 'drinkJarRedTea' not in ids('background.park_ranger_outdoor_guide'),'Park Ranger treasure map replacement wrong')
require('vehicleBicyclePlaceable' in ids('background.personal_trainer') and 'drinkCanMegaCrush' not in ids('background.personal_trainer'),'Personal Trainer bicycle replacement wrong')
require('ItemsWeaponsJunkBaton001_FR' in ids('background.police_officer') and 'ItemsWeaponsBaton001_FR' not in ids('background.police_officer'),'Police Blade Baton replacement wrong')
require('resourceSewingKit' not in ids('background.tailor'),'Tailor still has Sewing Kit')
require('meleeToolFlashlight02' in ids('background.teacher') and 'FuriousRamsayFountainPen' not in ids('background.teacher'),'Teacher flashlight replacement wrong')

# Current starter-loadout revision.
require(item('background.bartender','casinoCoin').get('count')=='500','Bartender must receive 500 casino coins')
require('foodSteakAndPotato' not in ids('background.butcher'),'Butcher steak-and-potato meal must be removed')
require(item('background.butcher','drinkJarBoiledWater').get('count')=='2','Butcher must receive 2 Boiled Waters')
require(item('background.butcher','foodGrilledMeat').get('count')=='3','Butcher must receive 3 Grilled Meat')
require('medicalBandage' not in ids('background.hunter') and 'medicalFirstAidBandage' not in ids('background.hunter'),'Hunter must not receive bandages')
require('meleeToolSalvageT2Ratchet' in ids('background.mechanic') and 'meleeToolSalvageT1Wrench' not in ids('background.mechanic'),'Mechanic must receive Ratchet instead of Wrench')
require(item('background.mechanic','resourceRepairKit').get('count')=='2' and 'resourceDuctTape' not in ids('background.mechanic'),'Mechanic must receive 2 Repair Kits instead of Duct Tape')
require('modArmorHelmetLight' in ids('background.miner') and 'meleeToolTorch' not in ids('background.miner'),'Miner must receive Helmet Light Mod instead of Torch')
require(item('background.salesperson','drinkJarRedTea').get('count')=='4' and 'FuriousRamsayFountainPen' not in ids('background.salesperson'),'Salesperson must receive 4 Red Tea instead of Fountain Pen')
require(item('background.scavenger','resourceLockPick').get('count')=='20','Scavenger must receive 20 Lockpicks')
require('ItemsWeaponsCrowbar001_FR' not in ids('background.scavenger'),'Scavenger Crowbar must be removed')
require('FuriousRamsayPropaneTank' in ids('background.scavenger'),'Scavenger must receive the actual 2.6 Propane Tank item')
require('tankPropaneWhite' not in ids('background.scavenger'),'Scavenger must not use the decorative tankPropaneWhite block')

# All backgrounds retain one Torch except Miner, whose Helmet Light Mod explicitly replaces it.

require(len(bgs)==28,'Expected authoritative 28-background roster')
for bgid,b in bgs.items():
    torches=[x for x in b.find('starting_items').findall('item') if x.get('id')=='meleeToolTorch']
    expected=0 if bgid=='background.miner' else 1
    require(len(torches)==expected,f'{bgid} expected {expected} torch starter(s), got {len(torches)}')

# Localized starter names must be exact 2.6 names (not generic invented labels).
loc={}
for line in LOC.read_text(encoding='utf-8-sig').splitlines():
    if ',' in line:
        k,v=line.split(',',1); loc[k]=v
expected_loc={
'xuiRebirthStarterItemName_ItemsWeaponsJunkBaton001_FR':'Blade Baton',
'xuiRebirthStarterItemName_ItemsWeaponsScythe004_FR':'Rusty Sickle',
'xuiRebirthStarterItemName_generatorbank':'Generator Bank',
'xuiRebirthStarterItemName_smallEngine':'Small Engine',
'xuiRebirthStarterItemName_resourceMechanicalParts':'Mechanical Parts',
'xuiRebirthStarterItemName_gunHandgunT1Pistol':'Pistol',
'xuiRebirthStarterItemName_ammo9mmBulletBall':'9mm Ammo',
'xuiRebirthStarterItemName_gunBowT1WoodenBow':'Wooden Bow',
'xuiRebirthStarterItemName_ammoArrowIron':'Iron Arrow (Ammo)',
'xuiRebirthStarterItemName_drinkJarYuccaJuice':'Yucca Juice',
'xuiRebirthStarterItemName_carBattery':'Lead Car Battery',
'xuiRebirthStarterItemName_drinkJarGoldenRodTea':'Goldenrod Tea',
'xuiRebirthStarterItemName_qt_claude':"Claude's Treasure Map",
'xuiRebirthStarterItemName_vehicleBicyclePlaceable':'Bicycle',
'xuiRebirthStarterItemName_meleeToolFlashlight02':'Flashlight',
'xuiRebirthStarterItemName_armorRogueHelmet':'Rogue Hood',
'xuiRebirthStarterItemName_armorRogueOutfit':'Rogue Outfit',
'xuiRebirthStarterItemName_armorRogueGloves':'Rogue Gloves',
'xuiRebirthStarterItemName_armorRogueBoots':'Rogue Boots',
'xuiRebirthStarterItemName_FuriousRamsaySeedBundle':'Farming Seeds',
'xuiRebirthStarterItemName_foodGrilledMeat':'Grilled Meat',
'xuiRebirthStarterItemName_meleeToolSalvageT2Ratchet':'Ratchet',
'xuiRebirthStarterItemName_resourceRepairKit':'Repair Kit',
'xuiRebirthStarterItemName_modArmorHelmetLight':'Helmet Light Mod',
'xuiRebirthStarterItemName_FuriousRamsayPropaneTank':'Propane Tank',
}
for k,v in expected_loc.items(): require(loc.get(k)==v,f'{k} localization mismatch: {loc.get(k)!r}')

# Real custom item definitions must exist in 3.0, and main items entrypoint must include them.
require('_Rebirth/items_starter.xml' in ITEMS.read_text(encoding='utf-8'),'root items config not routed through Rebirth starter layer')
require('../_Survivor/starter_legacy_items.xml' in REBIRTH_ITEMS.read_text(encoding='utf-8'),'Rebirth starter legacy item config not included')
lroot=ET.parse(LEGACY).getroot()
defined={x.get('name') for x in lroot.findall('.//item')}
required_custom={'FuriousRamsayHammerPliers','FuriousRamsayPliers','FuriousRamsayScrewdriver','FuriousRamsayFountainPen','FuriousRamsaySeedBundle','ItemsWeaponsCleaver001_FR','ItemsWeaponsCrowbar001_FR','ItemsWeaponsJunkBaton001_FR','ItemsWeaponsScythe004_FR','FuriousRamsayPropaneTank'}
require(required_custom <= defined,f'missing custom item definitions: {sorted(required_custom-defined)}')

# Custom starter art is packaged ONLY under the original ItemIconAtlas names.
for p in [
    ROOT/'UIAtlases/ItemIconAtlas/ItemsWeaponsJunkBaton001_FR.png',
    ROOT/'UIAtlases/ItemIconAtlas/ItemsWeaponsScythe004_FR.png',
    ROOT/'UIAtlases/ItemIconAtlas/FR_SM_Propane_icon.png',
]:
    require(p.exists() and p.stat().st_size>0,f'missing original icon {p}')
require(not list((ROOT/'UIAtlases/RebirthSurvivorIcons').glob('rb_starting_item_*.png')), 'renamed starter-item icon aliases must not exist')


# Every REBIRTH/custom starter reference must resolve to an actual definition in this 3.0 project.
config_text='\n'.join(p.read_text(encoding='utf-8',errors='ignore') for p in (ROOT/'Config').rglob('*.xml'))
custom_starters=set()
for b in bgs.values():
    for x in b.find('starting_items').findall('item'):
        iid=x.get('id','')
        if iid.startswith('FuriousRamsay') or iid.startswith('ItemsWeapons') or iid.startswith('guppy'):
            custom_starters.add(iid)
for iid in sorted(custom_starters):
    require((f'<item name="{iid}"' in config_text) or (f'<block name="{iid}"' in config_text),f'custom starter has no 3.0 item/block definition: {iid}')


# Original 2.6 held-model bundle references are preserved; bundles may be supplied externally.
legacy_text=LEGACY.read_text(encoding='utf-8')
expected_bundles={
'ItemsWeaponsCleaver001_FR':'#@modfolder:Resources/FR_Knives.unity3d?ItemsWeaponsCleaver001_FR',
'ItemsWeaponsCrowbar001_FR':'#@modfolder:Resources/FR_Simple.unity3d?ItemsWeaponsCrowbar001_FR',
'ItemsWeaponsJunkBaton001_FR':'#@modfolder:Resources/FR_Batons.unity3d?ItemsWeaponsJunkBaton001_FR',
'ItemsWeaponsScythe004_FR':'#@modfolder:Resources/FR_Scythes.unity3d?ItemsWeaponsScythes004_FR',
}
for iid,mesh in expected_bundles.items():
    require(mesh in legacy_text,f'{iid} must preserve its exact 2.6 asset-bundle Meshfile')

# Fountain Pen retains its actual 2.6 functional buff support, not only its icon/name.
BUFFS=ROOT/'Config/buffs.xml'
LEGACY_BUFFS=ROOT/'Config/_Survivor/starter_legacy_buffs.xml'
require(LEGACY_BUFFS.exists(),'legacy starter buff config missing')
require('_Rebirth/buffs.xml' in BUFFS.read_text(encoding='utf-8'),'root buffs config not routed through Rebirth layer')
require('../_Survivor/starter_legacy_buffs.xml' in REBIRTH_BUFFS.read_text(encoding='utf-8'),'legacy starter buff config not included by Rebirth router')
require('FuriousRamsayUnlockRecipesFountainPen' in legacy_text,'Fountain Pen buff hooks missing')
require('FuriousRamsayUnlockRecipesFountainPen' in LEGACY_BUFFS.read_text(encoding='utf-8'),'Fountain Pen buff definition missing')

print('PASS: Survivor starter loadout source-authority revision')
