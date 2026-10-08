"""One-time material rewards for the learning curriculum; never player XP."""
from lxml import etree as E
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
REWARDS={
 'Food':('drinkJarBoiledWater',1,'1 Boiled Water'),
 'Carry':('resourceCloth',6,'6 Cloth Fragments'),
 'Learn':('casinoCoin',50,'50 Dukes'),
 'Survive':('medicalBandage',1,'1 Bandage'),
 'Melee':('resourceScrapIron',10,'10 Scrap Iron'),
 'Gather':('resourceDuctTape',1,'1 Duct Tape'),
 'Farm':('resourceYuccaFibers',10,'10 Plant Fibers'),
 'Build':('resourceGlue',2,'2 Glue'),
 'Trade':('casinoCoin',75,'75 Dukes'),
 'Ranged':('resourceGunPowder',10,'10 Gun Powder'),
 'Workshop':('resourceMechanicalParts',2,'2 Mechanical Parts'),
 'Devices':('resourceElectricParts',2,'2 Electrical Parts'),
 'Special':('medicalBandage',2,'2 Bandages'),
}
# Chapter rewards support application after the practice lessons; quantities are
# deliberately independent of per-lesson payouts (one manual is not three books).
SECTION_REWARDS={
 'Food':('rebirthRecipeVegetableStew',1,'Vegetable Stew recipe'),
 'Carry':('rebirthManualSecureStorage',1,'Secure Storage manual'),
 'Learn':('rebirthManualRepairKit',1,'Repair Kit manual'),
 'Survive':('rebirthRecipeFirstAidKit',1,'First Aid Kit recipe'),
 'Melee':('rebirthManualRepairKit',1,'Repair Kit manual'),
 'Gather':('rebirthManualForgeTools',1,'Forge Tools manual'),
 'Farm':('rebirthRecipePickledVegetables',1,'Pickled Vegetables recipe'),
 'Build':('rebirthManualConstructionFraming',1,'Construction Framing manual'),
 'Trade':('rebirthTheoryBarteringPrimer',1,'Bartering Principles book'),
 'Ranged':('rebirthManualGunRepairKit',1,'Gun Repair Kit manual'),
 'Workshop':('rebirthManualElectricalServiceTools',1,'Electrical Service Tools manual'),
 'Devices':('rebirthManualDroneFieldService',1,'Drone Field Service manual'),
 'Special':('medicalBandage',6,'6 Bandages'),
}
LESSON_REWARDS={
 'StudyTheory':('StudyTheory','rebirthTheoryMaintenancePrimer',1,'Maintenance Principles book'),
 'ReadRecipe':('ReadRecipe','medicalBandage',3,'3 Bandages'),
 'StudyManual':('StudyManual','resourceRepairKit',1,'1 Repair Kit'),
 'Practice':('Practice','rebirthTheoryCookingPrimer',1,'Cooking Principles book'),
 'Theory':('Theory','rebirthRecipeFirstAidKit',1,'First Aid Kit recipe'),
 'Knowledge':('Knowledge','medicalBandage',3,'3 bandages'),
 'Audio':('Audio','rebirthTheoryMedicinePrimer',1,'Medicine Principles book'),
 'RouteStudyThenPractice':('StudyPractice','rebirthTheoryMaintenancePrimer',1,'Maintenance Principles book'),
 'RouteKnowledgeThenWork':('KnowledgeWork','rebirthTheoryConstructionPrimer',1,'Construction Principles book'),
 'FindTheory':('FindTheory','resourceWood',20,'20 Wood'),
 'FindRecipe':('FindRecipe','medicalBandage',1,'1 Bandage'),
 'FindManual':('FindManual','rebirthTheoryMaintenancePrimer',1,'Maintenance Principles book'),
}
# Practice earns a matching study reference: Theory remains a separate, deliberate action.
# Explicit IDs keep missing/renamed literature from silently generating invalid rewards.
SKILL_REFERENCES = {
 'spears':'Spears', 'clubs':'Clubs', 'swords':'Swords', 'axes':'Axes',
 'batons':'Batons', 'hammers':'Hammers', 'knives':'Knives', 'scythes':'Scythes',
 'knuckles':'Knuckles', 'unarmed':'Unarmed', 'archery':'Archery',
 'pistols':'Pistols', 'revolvers':'Revolvers', 'heavy_handguns':'HeavyHandguns',
 'shotguns':'Shotguns', 'assault_rifles':'AssaultRifles',
 'tactical_rifles':'TacticalRifles', 'long_range_rifles':'LongRangeRifles',
 'explosives':'Explosives', 'deployable_turrets':'DeployableTurrets',
 'drone_operations':'DroneOperations', 'mining':'Mining', 'logging':'Logging',
 'salvage':'Salvage', 'farming':'Farming', 'animal_processing':'AnimalProcessing',
 'tracking':'Tracking', 'mechanics':'Mechanics', 'electrical':'Electrical',
 'metalworking':'Metalworking', 'gunsmithing':'Gunsmithing', 'chemistry':'Chemistry',
 'lockpicking':'Lockpicking', 'stealth':'Stealth', 'athletics':'Athletics',
 'armor_proficiency':'ArmorProficiency', 'bartering':'Bartering',
 'tailoring':'Tailoring', 'construction':'Construction',
}
for skill, edition in SKILL_REFERENCES.items():
    LESSON_REWARDS['Skill_'+skill] = (
        'Skill_'+skill, 'rebirthTheory'+edition+'Primer', 1,
        skill.replace('_',' ').title()+' Principles book')
def apply_rewards(root,texts):
    path=ROOT/'Config/gameevents.xml'
    events=E.parse(str(path)).getroot()
    destinations=events.xpath("conditional/if[@cond=\"character_progression('Rebirth')\"]/append[@xpath='/gameevents']")
    if len(destinations)!=1:
        raise ValueError('Expected one REBIRTH reward event destination')
    dest=destinations[0]
    owned={'rebirthLessonReward'+key+suffix for key in REWARDS for suffix in ('','Section')}
    owned.update('rebirthLessonReward'+v[0] for v in LESSON_REWARDS.values())
    owned.add('rebirthLessonRewardLegacy')
    for sequence in list(dest.findall('action_sequence')):
        if sequence.get('name') in owned:
            dest.remove(sequence)
    for key,(item,count,label) in REWARDS.items():
        event='rebirthLessonReward'+key
        texts[event]=label
        seq=E.SubElement(dest,'action_sequence',name=event)
        E.SubElement(seq,'property',name='allow_user_trigger',value='false')
        E.SubElement(seq,'property',name='action_type',value='Game')
        action=E.SubElement(seq,'action',{'class':'AddItems'})
        E.SubElement(action,'property',name='added_items',value=item)
        E.SubElement(action,'property',name='added_item_counts',value=str(count))
        for c in root.xpath(f"append/challenge[@group='rebirthLearn{key}']"):
            c.set('reward_event',event);c.set('reward_text_key',event)
        bonus=event+'Section'
        bonus_item,bonus_count,bonus_label=SECTION_REWARDS[key]
        texts[bonus]='Chapter reward: '+bonus_label
        seq=E.SubElement(dest,'action_sequence',name=bonus)
        E.SubElement(seq,'property',name='allow_user_trigger',value='false')
        E.SubElement(seq,'property',name='action_type',value='Game')
        action=E.SubElement(seq,'action',{'class':'AddItems'})
        E.SubElement(action,'property',name='added_items',value=bonus_item)
        E.SubElement(action,'property',name='added_item_counts',value=str(bonus_count))
        for g in root.xpath(f"append/challenge_group[@name='rebirthLearn{key}']"):
            g.set('reward_event',bonus);g.set('reward_text_key',bonus)
    for lesson,(suffix,item,count,label) in LESSON_REWARDS.items():
        event='rebirthLessonReward'+suffix
        texts[event]=label
        seq=E.SubElement(dest,'action_sequence',name=event)
        E.SubElement(seq,'property',name='allow_user_trigger',value='false')
        E.SubElement(seq,'property',name='action_type',value='Game')
        action=E.SubElement(seq,'action',{'class':'AddItems'})
        E.SubElement(action,'property',name='added_items',value=item)
        E.SubElement(action,'property',name='added_item_counts',value=str(count))
        for challenge in root.xpath(f"append/challenge[@name='rebirthLesson{lesson}']"):
            challenge.set('reward_event',event);challenge.set('reward_text_key',event)
    # Saved duplicate lessons remain valid but cannot silently award vanilla XP.
    empty=E.SubElement(dest,'action_sequence',name='rebirthLessonRewardLegacy')
    E.SubElement(empty,'property',name='allow_user_trigger',value='false')
    E.SubElement(empty,'property',name='action_type',value='Game')
    texts['rebirthLessonRewardLegacy']='Covered by the introductory lesson'
    for c in root.xpath("append/challenge[@name='rebirthLessonSkill_cooking' or @name='rebirthLessonSkill_drink_preparation' or @name='rebirthLessonSkill_medicine' or @name='rebirthLessonSkill_maintenance']"):
        c.set('reward_event','rebirthLessonRewardLegacy');c.set('reward_text_key','rebirthLessonRewardLegacy')
    # Also replace rewards on the two native lessons repurposed for Rebirth learning.
    for name in ('spendSkillPoint','readMagazines'):
        for attr in ('reward_event','reward_text_key'):
            existing=root.xpath(f"setattribute[@xpath=\"/challenges/challenge[@name='{name}']\"][@name='{attr}']")
            node=existing[0] if existing else E.SubElement(root,'setattribute',xpath=f"/challenges/challenge[@name='{name}']",name=attr)
            node.text='rebirthLessonRewardLearn'
    E.indent(events,space='  ')
    (ROOT/'Config/gameevents.xml').write_bytes(E.tostring(events,encoding='utf-8',xml_declaration=True,pretty_print=True))
