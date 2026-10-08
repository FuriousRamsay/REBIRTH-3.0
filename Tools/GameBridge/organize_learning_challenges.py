"""Apply the beginner-first curriculum without changing stable challenge IDs or objectives."""
from pathlib import Path
from lxml import etree as E
import csv, io, json

ROOT = Path(__file__).resolve().parents[2]
# Native links guide the tracker to the next unfinished lesson in XML order.
# All objectives remain active: prior/specialist practice is never discarded.
SECTIONS = [
 ('Food', '01 - Food, water and digestion', 'Drink WaterSlot AbsorbWater Cook RouteDigestionStages DigestMeal RouteEnergyRecovery'),
 ('Carry', '02 - Backpacks and toolbelts', 'StoragePack StorageBelt'),
 ('Learn', '03 - Practice, books and recipes', 'Practice FindTheory StudyTheory Theory FindRecipe ReadRecipe FindManual StudyManual Knowledge Audio RouteStudyThenPractice RouteKnowledgeThenWork'),
 ('Survive', '04 - Moving, protection and treatment', 'Skill_athletics Skill_armor_proficiency Medicine Skill_stealth RouteArmoredTravel'),
 ('Melee', '05 - Melee combat', 'Skill_clubs Skill_spears Skill_knives Skill_axes Skill_swords Skill_batons Skill_hammers Skill_scythes Skill_knuckles Skill_unarmed'),
 ('Gather', '06 - Gathering and hunting', 'Skill_mining Skill_logging Skill_salvage Skill_tracking Skill_animal_processing'),
 ('Farm', '07 - Growing and preparing food', 'Seeds Skill_farming RouteGardenKitchen RouteHuntKitchen RouteFieldMeal RouteWaterRoutine'),
 ('Build', '08 - Repairs, clothing and building', 'Repair Skill_construction Skill_tailoring RouteRepairLoop'),
 ('Trade', '09 - Trade and teamwork', 'Skill_trading Skill_bartering RouteTradeSurplus Skill_animal_handling Skill_teaching'),
 ('Ranged', '10 - Ranged weapons', 'Skill_archery Skill_pistols Skill_revolvers Skill_heavy_handguns Skill_shotguns Skill_assault_rifles Skill_tactical_rifles Skill_long_range_rifles'),
 ('Workshop', '11 - Workshop and technical skills', 'Skill_mechanics Skill_electrical Skill_metalworking Skill_gunsmithing Skill_chemistry Skill_lockpicking RouteSalvageCircuit'),
 ('Devices', '12 - Explosives, turrets and drones', 'Skill_explosives Skill_deployable_turrets Skill_drone_operations'),
 ('Special', '13 - Optional special disciplines', 'Skill_rage Skill_black_magic'),
]
# Keep these definitions for existing saves, but put their redundant entries in
# the canonical chapter. The presentation filters aliases, not saved progress.
ALIASES = {'Skill_cooking':'Cook', 'Skill_drink_preparation':'Drink', 'Skill_medicine':'Medicine', 'Skill_maintenance':'Repair'}

def organize(root, texts):
    lessons={n.get('name').removeprefix('rebirthLesson'): n for n in root.xpath("append/challenge[starts-with(@name,'rebirthLesson')]")}
    # Older generator versions do not own the storage lessons; retain those from the installed XML.
    existing=E.parse(str(ROOT/'Config/_Rebirth/challenges.xml'))
    for suffix in ('StoragePack','StorageBelt','FindTheory','FindRecipe','FindManual','StudyTheory','ReadRecipe','StudyManual'):
        if suffix not in lessons:
            lessons[suffix]=existing.xpath(f"//challenge[@name='rebirthLesson{suffix}']")[0]
    assigned={suffix: 'rebirthLearn'+key for key,_,names in SECTIONS for suffix in names.split()}
    assert len(assigned)==sum(len(names.split()) for _,_,names in SECTIONS), 'Duplicate curriculum entry'
    assert set(lessons)==set(assigned)|set(ALIASES), (set(lessons)-set(assigned)-set(ALIASES))
    for node in list(root.xpath('append/challenge_group'))+list(root.xpath("append/challenge[starts-with(@name,'rebirthLesson')]")):
        node.getparent().remove(node)
    destination=root.find("append[@xpath='/challenges']")
    for key,title,names in SECTIONS:
        group='rebirthLearn'+key
        texts[group]=title
        E.SubElement(destination,'challenge_group',category='RebirthLearning',name=group,title_key=group,link_challenges='true')
        for suffix in names.split():
            node=lessons[suffix];node.set('group',group);destination.append(node)
    # Native startup requires every group to reference a registered category.
    # Keep a valid category and suppress this retired group only in presentation.
    E.SubElement(destination,'challenge_group',category='RebirthLearning',name='rebirthLearningLegacy',title_key='rebirthLearningLegacy',link_challenges='false')
    texts['rebirthLearningLegacy']='Earlier learning records'
    for suffix,canonical in ALIASES.items():
        node=lessons[suffix];node.set('group','rebirthLearningLegacy');destination.append(node)
        for objective in node.findall('objective'):
            objective.set('challenge_stat','rebirth.legacy.retired')
    texts['rebirthLessonStoragePackDesc']='Open Crafting and search for Daypack. Craft one using 10 Cloth Fragments and 20 Plant Fibers. Crafting it completes this lesson. To use it, open Character and place it in the Backpack equipment slot. It adds one row of 13 slots; the number of encumbered slots stays the same.'
    texts['rebirthLessonStorageBeltDesc']='Open Crafting and search for Utility Toolbelt. Craft one using 6 Cloth Fragments and 2 Scrap Iron. Crafting it completes this lesson. To use it, open Character and place it in the Belt equipment slot. It adds two toolbelt slots.'
    texts.update({
      'rebirthWaterSupplyTitle':'CARRIED WATER',
      'rebirthWaterSupplyHint':'Equip water in Overview > Support.',
      'rebirthWaterEquipHeld':'EQUIP HELD',
      'xuiRebirthMetabolismAutoSipOn':'AUTO SIP: ON',
      'xuiRebirthMetabolismAutoSipOff':'AUTO SIP: OFF',
      'xuiRebirthMetabolismSlotEmpty':'No water container equipped',
      'rebirthLessonWaterSlotDesc':'Open Character > Overview. Move a filled water jar from your backpack into the Support slot beneath the toolbelt. Open Metabolism and turn Auto Sip ON. This completes when that supply contains liquid and Auto Sip is on. An empty jar does not count. Use the Support slot removal button to return the container to your backpack.',
      'rebirthLessonAbsorbWaterDesc':'Drink clean water. Open Character > Metabolism to see it pass through your stomach and intestine. Wait until your body absorbs some water. Drinking does not refill Hydration immediately; absorption completes this lesson.',
      'rebirthLessonDigestMealDesc':'Eat a meal. Open Character > Metabolism and allow the food to digest. This completes when your body absorbs nutrition. You do not need to eat again while the first meal is still being processed.',
      'rebirthLessonCookDesc':'Open Crafting and choose a food recipe that shows a Cooking skill gain. Gather its ingredients and use the station listed in the recipe. Finish crafting the food to earn Cooking practice and complete this lesson.',
      'rebirthLessonDrinkDesc':'Open Crafting and choose a drink recipe that shows a Drink Preparation skill gain. Use the ingredients and station listed in the recipe. Finish crafting it to earn practice and complete this lesson.',
      'rebirthLessonMedicineDesc':'Open Character > Condition to check an injury or illness. Use a medical item that treats that problem. This completes when the treatment earns Medicine practice. Using medicine while healthy does not count.',
      'rebirthLessonPracticeDesc':"Open Character > Progression and select a skill. Perform one of its listed training activities until you gain practical skill progress. Skill represents what you can do and improves through use; there are no skill points to spend. Claim Cooking Principles as your reward, then study it to compare practical Skill with Theory.",
      'rebirthLessonTheoryDesc':"Find a subject principles book or field notes, or study the Cooking Principles reward from the practice lesson. Finish its study action to gain Theory. Theory represents understanding and helps reveal useful information; it does not directly raise practical Skill or automatically unlock recipes. Cookbooks and cooking magazines are preparation references, not Theory books. Your reward is a First Aid Kit recipe to introduce separate recipe knowledge.",
      'rebirthLessonKnowledgeDesc':"Find an unfamiliar recipe card, schematic, pattern, guide or procedure manual, or use the First Aid Kit recipe rewarded by the Theory lesson. Finish its learning action. These sources teach specific knowledge rather than practical Skill. Tools, materials, stations and other requirements can still apply. This existing objective also accepts recipe knowledge learned through successful experimentation; possessing an unread item alone does not teach it.",
      'rebirthLessonAudioDesc':"Find or buy a Walkman. Collecting the player completes this introductory lesson. Equip it and use a compatible learning cassette to study its subject; owning the player alone does not grant Theory or recipe knowledge. Equivalent book and cassette editions share their one-time learning benefit. Claim the Medicine Principles book as a study reference.",
      'rebirthLessonSeedsDesc':'Find or buy Basil Seeds and put them in your inventory. Collecting the seeds completes this lesson. Growing and harvesting them is covered later.',
      'rebirthLessonRepairDesc':'Select damaged equipment in your inventory and check its repair material. Repair it to earn Maintenance practice. You can also complete a recipe that shows Maintenance skill gain. This completes on a real skill gain.',
      'rebirthLessonRouteDigestionStagesDesc':'Eat food or drink water, then open Character > Metabolism. This completes when some food or liquid moves from the stomach to the intestine. Your body absorbs it after this transfer.',
      'rebirthLessonRouteEnergyRecoveryDesc':'After using some Energy, rest in a safe place. Make sure your body has nutrition and hydration available. This completes when Energy actually rises. Energy and Stamina are different reserves; watch Energy in Character > Metabolism.',
      'rebirthLessonRouteFieldMealDesc':'Finish a food recipe that earns Cooking practice, and eat food that your body can digest. This completes after you have earned Cooking practice and absorbed nutrition. The two steps can happen in either order.',
      'rebirthLessonRouteWaterRoutineDesc':'Finish a recipe that earns Drink Preparation practice. Also place a filled water jar in Character > Overview > Support beneath the Toolbelt, then turn Auto Sip ON in Metabolism. Complete both listed objectives; either order counts.',
      'rebirthLessonRouteStudyThenPracticeDesc':'Gain Theory by studying a book or field notes, and earn practical skill progress through a training activity. Both are needed for this challenge. They may be completed in either order and do not have to be for the same skill.',
      'rebirthLessonRouteKnowledgeThenWorkDesc':'Study a recipe or manual you have not learned, and earn practical skill progress through a training activity. Complete both objectives in either order. Learning knowledge and improving practical skill are separate parts of Rebirth progression.',
      'rebirthLessonRouteRepairLoopDesc':'Mine resources to earn Mining practice. Repair damaged equipment or finish a recipe that earns Maintenance practice. Both skill gains complete this lesson; either order counts.',
      'rebirthLessonRouteSalvageCircuitDesc':'Dismantle a salvageable object with a salvage tool to earn Salvage practice. Complete an activity listed under Character > Progression > Electrical to earn Electrical practice. Both gains are required, in either order.',
      'rebirthLessonRouteGardenKitchenDesc':'Harvest a mature crop to earn Farming practice. Finish a food recipe to earn Cooking practice. Both gains complete this lesson; the recipe does not have to use that particular harvest.',
      'rebirthLessonRouteHuntKitchenDesc':'Harvest an animal carcass to earn Animal Processing practice. Finish a food recipe to earn Cooking practice. Both gains are required. Killing an animal and harvesting it train different skills.',
      'rebirthLessonRouteTradeSurplusDesc':'Sell useful surplus to a trader to earn Trading practice. Buy a useful item to earn Bartering practice. Both gains are required. Tiny repeated trades may not award practice.',
      'rebirthLessonRouteArmoredTravelDesc':'Wear armor and travel on foot. Earn both Armor Proficiency and Athletics practice to complete this lesson. Check each skill under Character > Progression for its training activities.',
    })
    # Keep each specialist lesson actionable without repeating an entire combat tutorial.
    melee='clubs spears knives axes swords batons hammers scythes knuckles unarmed'.split()
    ranged='archery pistols revolvers heavy_handguns shotguns assault_rifles tactical_rifles long_range_rifles'.split()
    for skill in melee+ranged:
        name='rebirthLessonSkill_'+skill
        weapon=skill.replace('_',' ')
        action='Fight an enemy with '+weapon+'.' if skill!='unarmed' else 'Fight an enemy with your bare fists.'
        texts[name+'Desc']=action+' Deal damage until the skill gains practice. Empty swings and missed shots do not count. Check your progress in Character > Progression.'
    actions={
      'mining':'Mine a resource deposit with a mining tool.',
      'logging':'Harvest a tree with an axe or other logging tool.',
      'salvage':'Dismantle a salvageable object with a wrench or other salvage tool.',
      'farming':'Harvest a mature crop. Planting and waiting alone do not count.',
      'animal_processing':'Harvest an animal carcass. The kill and the harvest train different skills.',
      'tracking':'Crouch near animals to search for tracks. Find a new eligible animal track; repeatedly finding the same animal does not count.',
      'animal_handling':'Move with your owned dog following you. Standing still together does not count.',
      'lockpicking':'Successfully pick a locked container. A failed or cancelled attempt does not count.',
      'stealth':'Land a qualifying sneak attack on an enemy. Sneaking without attacking does not count.',
      'athletics':'Travel on foot and use your stamina. Standing still or repeatedly jumping in place does not count.',
      'armor_proficiency':'Equip armor and move while wearing it. Your armor must add a real burden.',
      'bartering':'Buy a useful item from a trader. Very small or repeated reversible trades may not award practice.',
      'trading':'Sell useful surplus to a trader. Very small or repeated reversible trades may not award practice.',
      'teaching':'Use the teaching interaction with another player or an owned companion who can learn from you. The student must gain Theory.',
      'rage':'Unlock the Berserker discipline. Activate Rage and damage enemies with a melee weapon while it is active.',
      'black_magic':'Unlock the required Black Magic discipline. Take control of an eligible undead and use it in combat.',
      'explosives':'Damage an enemy with an explosive you use. A blast that misses does not count.',
      'deployable_turrets':'Place your own turret, load its ammunition, and let it damage an enemy.',
    }
    for skill,action in actions.items():
        texts['rebirthLessonSkill_'+skill+'Desc']=action+' This completes when the skill gains practice. See Character > Progression for its training activities.'
    for skill in ('mechanics','construction','electrical','metalworking','gunsmithing','chemistry','tailoring'):
        title=skill.replace('_',' ').title()
        texts['rebirthLessonSkill_'+skill+'Desc']='Open Character > Progression > '+title+' and choose Craft related recipes. Select an unlocked recipe that shows a skill gain. Gather its materials, use the listed station, and finish crafting it. This completes when you earn '+title+' practice.'
    from learning_rewards import apply_rewards
    apply_rewards(root,texts)
    from learning_rewards import SKILL_REFERENCES
    existing_texts=dict((row[0],row[1]) for row in csv.reader(io.StringIO((ROOT/'Config/Localization.csv').read_text(encoding='utf-8-sig'))) if len(row)>=2)
    for skill in SKILL_REFERENCES:
        key='rebirthLessonSkill_'+skill+'Desc'
        if key not in texts:
            texts[key]=existing_texts[key].split(' Claim the matching Principles book as your reward.')[0]
        texts[key] += ' Claim the matching Principles book as your reward. Finish studying it to gain Theory in this subject; collecting the book does not raise Skill or Theory.'
    report=[{'group':title,'lessons':['rebirthLesson'+s for s in names.split()]} for _,title,names in SECTIONS]
    folder=ROOT/'_Documentation/ChallengeLearning';folder.mkdir(exist_ok=True)
    (folder/'curriculum.json').write_text(json.dumps(report,indent=2)+'\n')

if __name__=='__main__':
    path=ROOT/'Config/_Rebirth/challenges.xml';root=E.parse(str(path)).getroot()
    texts={};organize(root,texts)
    E.indent(root,space='  ');path.write_bytes(E.tostring(root,encoding='utf-8',xml_declaration=True,pretty_print=True))
    loc=ROOT/'Config/Localization.csv';rows=list(csv.reader(io.StringIO(loc.read_text(encoding='utf-8-sig'))))
    rows=[r for r in rows if not r or r[0] not in texts]+list(texts.items())
    out=io.StringIO();csv.writer(out,lineterminator='\n').writerows(rows);loc.write_text(out.getvalue(),encoding='utf-8-sig')
    print('Curriculum:',sum(len(names.split()) for _,_,names in SECTIONS),'distinct lessons,',len(SECTIONS),'ordered sections; four saved aliases retained.')
