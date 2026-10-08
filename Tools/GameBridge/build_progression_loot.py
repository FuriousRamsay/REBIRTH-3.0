"""Generate the Rebirth-only distribution layer from the authoritative literature registry.

Run from the mod root. No base-game files are modified. The audit reads the last
merged test-save dump to validate concrete item/block names before generating.
"""
from pathlib import Path
from lxml import etree as E
import json

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT.parent.parent / 'Data/Config'
DUMP = Path.home() / 'AppData/Roaming/7DaysToDie/Saves/West Xuyofu Territory/CodexTest/ConfigsDump'
OUT = ROOT / 'Config/_Rebirth'
DOC = ROOT / '_Documentation/Progression_Loot'
DOC.mkdir(parents=True, exist_ok=True)
registry = E.parse(str(ROOT / 'Config/_Survivor/literature.xml'))
items = E.parse(str(DUMP / 'items.xml'))
blocks = E.parse(str(DUMP / 'blocks.xml'))
known = {x.get('name') for x in items.xpath('/items/item')} | {x.get('name') for x in blocks.xpath('/blocks/block')}
native_loot = E.parse(str(BASE / 'loot.xml'))
native_traders = E.parse(str(BASE / 'traders.xml'))
loot = E.Element('configs')
traders = E.Element('configs')
groups = E.SubElement(loot, 'insertBefore', xpath='/lootcontainers/lootgroup[1]')
trade_groups = E.SubElement(traders, 'insertBefore', xpath='/traders/trader_item_groups/trader_item_group[1]')
manifest = {}

def group(name, ids, parent=groups, tag='lootgroup'):
    ids = sorted(set(ids))
    assert ids, name
    assert set(ids) <= known, (name, set(ids)-known)
    g = E.SubElement(parent, tag, name=name, count='1')
    for item in ids:
        E.SubElement(g, 'item', name=item, count='1')
    manifest[name] = ids
    return g

def setatt(root, xpath, name, value):
    E.SubElement(root, 'setattribute', xpath=xpath, name=name).text = value

# Native magazine subject -> relevant theory families and selected practical titles.
subjects = {
 'bows': ('Archery', 'PatternBowmaking SchematicCrossbowsCompound SchematicArcherySpecialtyAmmo SchematicBowMods'),
 'spears': ('Spears', 'PatternMeleeWeaponForging'),
 'clubs': ('Clubs', 'PatternMeleeWeaponForging SchematicClubMods'),
 'sledgehammers': ('Hammers', 'PatternMeleeWeaponForging'),
 'harvestingTools': ('Mining Logging', 'PatternIronHandTools SchematicPoweredTools'),
 'knuckles': ('Knuckles Unarmed', 'PatternMeleeWeaponForging'),
 'blades': ('Knives Swords Scythes Axes', 'PatternMeleeWeaponForging ManualSharpeningEdgeService'),
 'repairTools': ('Maintenance Construction', 'ManualRepairKit ManualPreventiveMaintenance ManualServiceToolFabrication'),
 'salvageTools': ('Salvage', 'ManualServiceToolFabrication'),
 'handguns': ('Pistols Revolvers HeavyHandguns Gunsmithing', 'ManualGunRepairKit ManualFirearmFieldService ManualFirearmServiceMods'),
 'shotguns': ('Shotguns Gunsmithing', 'ManualSpecialtyAmmo SchematicStandardAmmunition'),
 'rifles': ('LongRangeRifles Gunsmithing', 'ManualSpecialtyAmmo ManualFirearmFieldService'),
 'machineGuns': ('AssaultRifles TacticalRifles Gunsmithing', 'ManualFirearmServiceMods SchematicStandardAmmunition'),
 'explosives': ('Explosives Chemistry', 'FormulaImprovisedExplosives FormulaGrenadesCharges FormulaImprovisedMines FormulaRocketPayloads'),
 'armor': ('ArmorProficiency Tailoring', 'ManualArmorFitting ManualGarmentRepair ManualLeatherRepair ManualBackpackExpansion'),
 'robotics': ('DeployableTurrets DroneOperations', 'ManualTurretDeviceSchematics ManualDronePlatformSchematics ManualTurretCalibration'),
 'workstation': ('Metalworking Construction', 'ManualForgeTools SchematicForgeCrucible SchematicChemistryStation'),
 'medical': ('Medicine Chemistry', 'ManualFieldTriage RecipeFirstAidBandage RecipeFirstAidKit ManualInfectionCare'),
 'food': ('Cooking', 'CookbookCampfireBasics ManualFoodSafety ManualFoodPreservation GuidePreparedBeverages'),
 'seed': ('Farming', 'GuideSeedSaving GuideProtectedGrowing GuideFarmPlot GuideWaterHarvesting'),
 'vehicles': ('Mechanics', 'ManualBicycleRepairKit ManualTireService SchematicMinibike SchematicMotorcycle SchematicVehicleWheels'),
 'electrician': ('Electrical', 'ManualElectricalServiceTools SchematicGeneratorBank SchematicBatteryBank ManualControlDevices'),
 'traps': ('Construction Electrical', 'ManualConcealedTraps SchematicElectricFencePost SchematicTripWirePost'),
}
for subject, (skills, titles) in subjects.items():
    ids = [name for s in skills.split() for name in ('rebirthTheory'+s+'Primer','rebirthFieldNotes'+s)]
    ids += ['rebirth'+t for t in titles.split()]
    name = 'rebirthLearning_'+subject
    group(name, ids)
    group(name, ids, trade_groups, 'trader_item_group')
    old = subject+'SkillMagazine'
    for root, prefix in ((loot,'/lootcontainers'), (traders,'/traders')):
        xpath = prefix+f"//item[@name='{old}']"
        setatt(root,xpath,'group',name)
        setatt(root,xpath,'count','1')
        E.SubElement(root,'removeattribute',xpath=xpath+'/@name')

theory = [x.get('id') for x in registry.xpath('//item[@kind="theory"]')]
discovery = [x.get('id') for x in registry.xpath('//item[@kind="discovery"]')]
group('rebirthLearningReward', theory + [x for x in discovery if x.startswith('rebirthManual')])
group('rebirthRecipeReward', discovery)
xpath = "/lootcontainers//item[@name='questRewardT1SkillMagazineBundle']"
setatt(loot,xpath,'group','rebirthLearningReward')
setatt(loot,xpath,'count','3')
E.SubElement(loot,'removeattribute',xpath=xpath+'/@name')
# Existing empty legacy recipe-cassette group is intentionally removed, never refilled.
E.SubElement(loot,'remove',xpath="/lootcontainers//item[@group='rebirthAudiobookDiscoveryCassettes']")
E.SubElement(loot,'remove',xpath="/lootcontainers/lootgroup[@name='rebirthAudiobookDiscoveryCassettes']")

# Concrete seed stages only: never give abstract crop masters or growing/harvest blocks.
seed_ids=[]
for path in sorted((ROOT/'Config/_Farming/_Crops/Blocks').glob('*.xml')):
    tree=E.parse(str(path))
    seed_ids += [x.get('name') for x in tree.xpath('//block[property[@name="Extends" and @value="plantedCrop"]]')
                 if x.get('name','').endswith('1')]
group('rebirthCropSeeds',seed_ids)
components=['rebirthElectricalFuse','rebirthElectricalTerminalBlock','rebirthElectricalWiringHarness']
group('rebirthElectricalComponents',components)
group('rebirthWorkshopComponents',['rebirthStructuralBracketSet','rebirthRivetFastenerSet','rebirthSheetMetalBrackets'])
group('rebirthTextileComponents',['rebirthReinforcedStraps','rebirthPaddedLining'])
group('rebirthVehicleServiceComponents',['rebirthVehicleServicePartsKit'])
group('rebirthFirearmServiceComponents',['rebirthFirearmCleaningKit'])

routes = {
 'rebirthRecipeReward': ['groupBookcase01','groupBookPile01','groupBuriedTreasure','groupBuriedSuppliesT1','groupIntroBuriedSupplies'],
 'rebirthCookingCardPool': ['groupShamwayCrate01','groupShamwayShelves01','groupBuriedSuppliesT1','groupBookcase01'],
 'rebirthCropSeeds': ['groupCupboard01','groupShamwayCrate01','groupShamwayShelves01'],
 'rebirthElectricalComponents': ['groupMoPower01','groupElectricalDevices'],
 'rebirthAudiobookCassettes': ['groupBackpacks01','groupNightstand'],
 'rebirthAudioEquipment': ['groupBackpacks01'],
 'rebirthWorkshopComponents': ['groupWorkingStiffs01'],
 'rebirthTextileComponents': ['groupSavageCountryCrate01'],
 'rebirthVehicleServiceComponents': ['groupPassNGas01'],
 'rebirthFirearmServiceComponents': ['groupShotgunMessiahCrate01'],
}
native_names={x.get('name') for x in native_loot.xpath('/lootcontainers/lootgroup')}
for pool, targets in routes.items():
    for target in targets:
        assert target in native_names, target
        a=E.SubElement(loot,'append',xpath=f"/lootcontainers/lootgroup[@name='{target}']")
        E.SubElement(a,'item',group=pool,count='1',prob='0.04' if pool=='rebirthAudioEquipment' else '0.4')

# Remove primitive wooden/stone weapons at every loot leaf (including reward pools).
# Retain native item tiers and quality templates; low-tier archery now gives iron arrows,
# not an early crossbow that bypasses weapon balance.
replacements={
 'meleeWpnSpearT0StoneSpear':'meleeWpnBatonT0PipeBaton',
 'meleeWpnClubT0WoodenClub':'ItemsWeaponsCrowbar001_FR',
 'meleeWpnSledgeT0StoneSledgehammer':'ItemsWeaponsJunkBaton001_FR',
 'meleeWpnClubT1BaseballBat':'meleeWpnBatonT0PipeBaton',
 'gunBowT0PrimitiveBow':'ammoArrowIron',
 'gunBowT1WoodenBow':'gunBowT1IronCrossbow',
}
for old,new in replacements.items():
    assert new in known,new
    # Do not double an existing weapon's selection weight in the same pool.
    duplicate_path=f"/lootcontainers/lootgroup[item[@name='{new}']]/item[@name='{old}']"
    if native_loot.xpath(duplicate_path):
        E.SubElement(loot,'remove',xpath=duplicate_path)
    if old == 'gunBowT0PrimitiveBow':
        setatt(loot,f"/lootcontainers//item[@name='{old}']",'count','10,20')
    target = loot
    if old == 'meleeWpnSpearT0StoneSpear':
        # The duplicate-removal operation above can remove every stone spear.
        conditional = E.SubElement(loot, 'conditional')
        target = E.SubElement(conditional, 'if', cond=f'''xpath('/lootcontainers//item[@name="{old}"]') != null''')
    setatt(target,f"/lootcontainers//item[@name='{old}']",'name',new)

# Cleaver and scythe use the hunting knife's damage/effect tier. Share its existing
# selection weight and enclosing quality/rarity rules rather than adding tier-one
# damage to primitive pools or increasing the chance of a blade-family drop.
group('rebirthTier1Blades', ['meleeWpnBladeT1HuntingKnife',
                           'ItemsWeaponsCleaver001_FR', 'ItemsWeaponsScythe004_FR'])
# Scope outside the newly inserted variant pool to avoid a self-reference.
blade_path="/lootcontainers/lootgroup[@name!='rebirthTier1Blades']/item[@name='meleeWpnBladeT1HuntingKnife']"
setatt(loot,blade_path,'group','rebirthTier1Blades')
E.SubElement(loot,'removeattribute',xpath=blade_path+'/@name')
# Earlier starter distribution placed the scythe outside the native tier roll.
E.SubElement(loot,'remove',xpath="/lootcontainers/lootgroup[@name='groupToolsTiered']/item[@name='ItemsWeaponsScythe004_FR']")

for name,root in [('progression_loot.xml',loot),('progression_traders.xml',traders)]:
    E.indent(root,space='  ')
    (OUT/name).write_bytes(E.tostring(root,encoding='utf-8',xml_declaration=True,pretty_print=True))
(DOC/'distribution_manifest.json').write_text(json.dumps({'pools':manifest,'routes':routes,'weapon_replacements':replacements},indent=2)+'\n',encoding='utf-8')
print(f'Generated {len(manifest)} pools; {len(seed_ids)} custom seed types; 23 magazine subjects.')
