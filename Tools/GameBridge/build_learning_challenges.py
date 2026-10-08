"""Build the REBIRTH learning challenges and their English text from audited IDs."""
from pathlib import Path
from lxml import etree as E
import csv, io, json
ROOT=Path(__file__).resolve().parents[2]
locpath=ROOT/'Config/Localization.csv'
rows=list(csv.reader(io.StringIO(locpath.read_text(encoding='utf-8-sig'))))
loc={r[0]:r[1] for r in rows if len(r)>1}
root=E.Element('configs')
# Replace the two vanilla progression lessons while preserving the surrounding group.
native=E.parse(str(ROOT.parent.parent/'Data/Config/challenges.xml'))
for c in native.xpath('/challenges/challenge[objective[@type="SpendSkillPoint"] or objective[@item_tags="csm"]]'):
    replacement='rebirthPracticeIntro' if c.find('objective').get('type')=='SpendSkillPoint' else 'rebirthTheoryIntro'
    E.SubElement(root,'remove',xpath=f"/challenges/challenge[@name='{c.get('name')}']/objective")
    node=E.SubElement(root,'append',xpath=f"/challenges/challenge[@name='{c.get('name')}']")
    # Retain native challenge IDs and groups for save compatibility.
    E.SubElement(node,'objective',type='ChallengeStatAwarded',challenge_stat='rebirth.practice' if replacement=='rebirthPracticeIntro' else 'rebirth.theory',stat_text_key=replacement,count='1')
    for attr in ('title_key','short_description_key','description_key'):
        E.SubElement(root,'setattribute',xpath=f"/challenges/challenge[@name='{c.get('name')}']",name=attr).text=replacement+('Desc' if attr=='description_key' else '')
add=E.SubElement(root,'append',xpath='/challenges')
E.SubElement(add,'challenge_category',name='RebirthLearning',title_key='rebirthLearningCategory',icon='ui_game_symbol_book')
new={'rebirthLearningCategory':'Life in Rebirth','rebirthPracticeIntro':'Learn through practice','rebirthTheoryIntro':'Study a fundamentals book',
 'rebirthPracticeIntroDesc':'Open Character > Progression and select a skill to see its training activities. Perform one until the skill gains practice. Practical work trains skills and their primary attributes; there are no skill points to spend.',
 'rebirthTheoryIntroDesc':'Find a REBIRTH fundamentals book or field notes and complete its study action. Theory supports learning but does not replace practical skill or recipe knowledge. An already studied copy does not grant a second first-read benefit.'}

def chapter(key,title):
    new[key]=title
    E.SubElement(add,'challenge_group',category='RebirthLearning',name=key,title_key=key,link_challenges='false')

icon_concepts=json.loads((Path(__file__).with_name('challenge_icon_art.json')).read_text(encoding='utf-8'))['concepts']

def lesson(key,title,description,group,objective):
    new[key]=title;new[key+'Desc']=description
    suffix=key.removeprefix('rebirthLesson')
    if suffix not in icon_concepts:
        raise ValueError('Challenge needs a dedicated art concept: '+key)
    icon='rb_challenge_'+suffix
    if not (ROOT/'UIAtlases/UIAtlas'/f'{icon}.png').is_file():
        raise FileNotFoundError('Missing dedicated challenge icon: '+icon)
    c=E.SubElement(add,'challenge',name=key,title_key=key,short_description_key=key,description_key=key+'Desc',group=group,icon=icon)
    if 'stat_text' in objective:
        new[key+'Objective']=objective.pop('stat_text')
        objective['stat_text_key']=key+'Objective'
    E.SubElement(c,'objective',**objective)

chapter('rebirthLearningSurvival','Food, water and digestion')
lessons=[
 ('WaterSlot','Carry water for automatic sipping','Place a filled compatible water container in the hydration slot and enable automatic sipping. The slot must contain liquid; an empty jar is not a water supply. Watch the remaining volume before leaving shelter.','rebirth.hydration.slot'),
 ('AbsorbWater','Let water reach your body','Drink clean water and allow digestion to absorb it. Water in the stomach is still pending hydration. This lesson completes when water absorption actually occurs; repeatedly drinking is not required.','rebirth.digest.water'),
 ('DigestMeal','Digest a meal','Eat a suitable meal and allow it to digest. Nutrition is not delivered instantly. Watch pending food and stomach fullness; give the meal time before eating more. This lesson completes during real nutrient absorption.','rebirth.digest.food'),
]
for key,title,desc,event in lessons:
    lesson('rebirthLesson'+key,title,desc,'rebirthLearningSurvival',dict(type='ChallengeStatAwarded',challenge_stat=event,stat_text_key='rebirthLesson'+key,count='1'))
lesson('rebirthLessonCook','Prepare food you can use','Complete a food recipe to earn Cooking practice. Inspect its unlock requirements and ingredients first. Choose food compatible with your diet and leave time for digestion before a long trip.','rebirthLearningSurvival',dict(type='ChallengeStatAwarded',challenge_stat='rebirth.practice.skill.cooking',stat_text='Earn Cooking practice',count='1'))
lesson('rebirthLessonDrink','Prepare a drink','Complete an eligible prepared-drink recipe. Inspect the projected practice gain before crafting. Food and drinks train different skills; merely moving a bottle does not prepare a drink.','rebirthLearningSurvival',dict(type='ChallengeStatAwarded',challenge_stat='rebirth.practice.skill.drink_preparation',stat_text='Earn Drink Preparation practice',count='1'))
lesson('rebirthLessonMedicine','Treat a real need','Use appropriate treatment for an actual injury or condition. Medicine practice comes from a useful treatment; wasting medical items while healthy is not a training method.','rebirthLearningSurvival',dict(type='ChallengeStatAwarded',challenge_stat='rebirth.practice.skill.medicine',stat_text='Earn Medicine practice',count='1'))
chapter('rebirthLearningKnowledge','Practice, theory and knowledge')
lesson('rebirthLessonPractice',new['rebirthPracticeIntro'],new['rebirthPracticeIntroDesc'],'rebirthLearningKnowledge',dict(type='ChallengeStatAwarded',challenge_stat='rebirth.practice',stat_text='Earn practical skill progress',count='1'))
lesson('rebirthLessonTheory',new['rebirthTheoryIntro'],new['rebirthTheoryIntroDesc'],'rebirthLearningKnowledge',dict(type='ChallengeStatAwarded',challenge_stat='rebirth.theory',stat_text='Gain Theory',count='1'))
lesson('rebirthLessonKnowledge','Learn a new recipe or procedure','Complete the study of a previously unknown REBIRTH recipe card, schematic, pattern, guide or procedure manual. Then inspect the recipe: knowledge may be only one requirement, alongside skill, materials and a station.','rebirthLearningKnowledge',dict(type='ChallengeStatAwarded',challenge_stat='rebirth.knowledge',stat_text='Discover new knowledge',count='1'))
lesson('rebirthLessonAudio','Find an audiobook player','Find a Walkman in personal storage or buy one. REBIRTH learning cassettes cover fundamentals and field notes, not recipe cards. Inspect the cassette and player requirements before listening; an interrupted session does not complete a lesson.','rebirthLearningKnowledge',dict(type='Gather',item='FuriousRamsayWalkman',count='1'))
lesson('rebirthLessonSeeds','Start a herb supply','Find plantable basil seeds in kitchens or food-store loot. Inspect farming and water requirements before planting. Mature harvests provide useful herbs; planting alone is not Farming practice.','rebirthLearningSurvival',dict(type='Gather',item='plantedBasil1',count='1'))
lesson('rebirthLessonRepair','Maintain your equipment','Repair genuinely damaged equipment or complete an eligible maintenance recipe. Check the correct repair material for the item. Keep supplies before setting out: a broken tool can stop a planned training activity.','rebirthLearningKnowledge',dict(type='ChallengeStatAwarded',challenge_stat='rebirth.practice.skill.maintenance',stat_text='Earn Maintenance practice',count='1'))

instructions={
 'mining':'Extract useful resources with a mining tool. Choose an actual resource deposit and finish useful work; swinging at empty air gives no practice.',
 'logging':'Harvest a tree with an appropriate tool. Tree work trains Logging; fighting enemies with the same axe trains its combat skill.',
 'salvage':'Dismantle a salvageable object with a salvage tool and recover resources. Ordinary digging is not salvaging.',
 'farming':'Harvest a mature crop or complete a supported Advanced Farming action. Planting and waiting alone do not grant practical Farming skill.',
 'animal_processing':'Recover resources from an animal carcass. Killing the animal and processing its carcass are separate activities.',
 'tracking':'Crouch to search for nearby animals. Acquire a previously uncredited eligible animal track. The first tracking tier is available to a beginner; standing still beside the same animal is not repeatable practice.',
 'animal_handling':'Exercise an owned dog by moving with it following you. Standing still together gives no exercise practice. Inspect command requirements before attempting more advanced training.',
 'mechanics':'Repair or restore a vehicle, service its parts, or complete an eligible mechanics recipe. Actual completed work grants practice.',
 'medicine':'Treat a genuine injury or condition with a suitable medical item. Using treatment without a medical need does not train this skill.',
 'cooking':'Complete an eligible food recipe. Inspect knowledge, skill, station and ingredient requirements; queued or cancelled work does not count.',
 'drink_preparation':'Finish crafting a drink that shows a Drink Preparation skill gain. Check its unlock requirements before gathering ingredients; cancelling the queue does not grant practice.',
 'maintenance':'Repair damaged equipment or complete eligible maintenance work. Inspect the repair material and predicted skill gain.',
 'construction':'Complete structural repair or upgrading, or an eligible construction recipe. Aim for useful completed work rather than repeated placement and removal.',
 'electrical':'Complete eligible electrical service, upgrades or fabrication. Acquire components from electrical loot and study the relevant manuals when required.',
 'metalworking':'Complete eligible metal fabrication or service. Find the necessary forge tools, materials and knowledge before committing resources.',
 'gunsmithing':'Repair or service a firearm, or complete eligible firearm fabrication. Check the gun-specific tools, repair materials and recipe requirements.',
 'chemistry':'Complete an eligible chemistry process. Use the station required by that recipe and inspect its knowledge and skill gates.',
 'lockpicking':'Successfully pick an eligible locked container to earn practice. Failed attempts, cancelled attempts and carrying lockpicks do not train this skill.',
 'stealth':'Damage an enemy while a stealth attack qualifies. Moving near a sleeper without attacking is not practical Stealth training.',
 'athletics':'Travel on foot with real exertion. Manage stamina, hydration and energy. Standing still or jumping repeatedly in place is not a training shortcut.',
 'armor_proficiency':'Move and exert yourself while wearing armor that adds a real burden. Inspect the armor tradeoff; empty equipment slots cannot train armor handling.',
 'bartering':'Complete a meaningful purchase from an NPC trader. Reversible buying-and-selling loops do not provide unrestricted practice.',
 'trading':'Complete a meaningful sale to an NPC trader. Repeated tiny trades and reversible transactions are limited; sell useful surplus.',
 'teaching':'Transfer Theory to another player or an owned NPC or companion student through the teaching interaction. The student must actually learn something.',
 'tailoring':'Make or repair clothing, textiles, leather goods, backpacks or wearable equipment. Complete useful eligible work to gain practice.',
 'rage':'Acquire the Berserker discipline before using Rage. Deal meaningful melee damage while Rage is active. The discipline is optional for other progression.',
 'black_magic':'Acquire the required discipline, temporarily dominate an eligible undead, and use controlled-undead combat. Inspect the ability requirements and limitations first.',
 'explosives':'Deal meaningful attributed damage with an explosive. Keep a safe distance and clear escape route; practice depends on useful damage.',
 'deployable_turrets':'Use your own deployed turret to deal meaningful damage. Supply the correct ammunition and arrange a clear line of fire.',
 'drone_operations':'Travel with your owned drone following within 30 metres. Cover 200 metres with at least 60 seconds of movement and finish at least 100 metres from your starting point. This trains Drone Operations without a module, at most once per five minutes. Eligible recovery, service and module combat offer additional practice; idle ownership does not.'}
skills=E.parse(str(ROOT/'Config/_Survivor/progression.xml')).xpath('//skills/skill')
for i,skill in enumerate(skills):
    ch='rebirthLearningSkills'+str(i//8+1)
    if i%8==0:chapter(ch,['Melee disciplines','Weapon disciplines','Combat and fieldwork','Living from your work','Technical disciplines','Life and advanced disciplines'][i//8])
    sid=skill.get('id'); short=sid.removeprefix('skill.');title=loc[skill.get('name_key')]
    text=instructions.get(short,'Deal real damage to an enemy using a weapon from the '+title+' family. Normal and power attacks are evaluated by actual damage and weapon performance. Empty swings give no practice. Manage stamina and leave room to recover.')
    lesson('rebirthLessonSkill_'+short,title+': first practice',text+' Open Character > Progression and select this skill to see its training activities and progress. Theory study is a separate lesson.',ch,dict(type='ChallengeStatAwarded',challenge_stat='rebirth.practice.'+sid,stat_text='Earn '+title+' practice',count='1'))
chapter('rebirthLearningRoutes','Build a sustainable routine')
chapter('rebirthLearningRoutesWork','Connect your skills and supplies')
routes=[
 ('DigestionStages','Follow food through digestion','Eat a suitable portion, then inspect the stomach and intestine displays. Material first leaves the stomach and only later becomes available nutrition or hydration. Observe real transfer; a full stomach is not the same as a fully nourished body.','rebirth.digest.transfer'),
 ('EnergyRecovery','Recover Energy before another trip','After useful exertion, stop somewhere safe and let Energy recover. Check hydration and available nutrition: both support recovery. Stamina is the short-term exertion reserve; Energy supports it over a longer trip. This lesson requires an actual Energy increase.','rebirth.energy.recovery'),
 ('FieldMeal','Provide your own field meal','Prepare an eligible meal and digest food. Read diet compatibility, stomach volume and pending nutrition before eating. Do not keep eating simply because the current food meter has not risen yet.','rebirth.practice.skill.cooking'),
 ('WaterRoutine','Maintain a drinking routine','Prepare a drink and use the hydration slot. Inspect the remaining liquid before leaving, and confirm water is being absorbed. Carrying an empty bottle is not a drinking plan.','rebirth.practice.skill.drink_preparation'),
 ('RepairLoop','Keep working tools in service','Mine useful material, then earn Maintenance practice through a real repair or eligible maintenance work. Inspect repair requirements before a tool breaks. Different tools can require different supplies.','rebirth.practice.skill.mining'),
 ('SalvageCircuit','Recover and reuse components','Salvage useful parts and complete eligible Electrical work. Electrical components are distributed through appropriate technical loot; find the required service knowledge and supplies instead of repeatedly placing and removing a device.','rebirth.practice.skill.salvage'),
 ('GardenKitchen','Connect harvest to the kitchen','Harvest a mature crop and prepare food. Find plantable seeds in kitchens and food-store loot, then inspect the crop requirements. Planting and waiting are preparation; mature harvesting supplies the kitchen and trains Farming.','rebirth.practice.skill.farming'),
 ('HuntKitchen','Use the whole hunting trip','Process an animal carcass and prepare food. The kill trains the weapon family; recovering meat and other materials trains Animal Processing. Cook with the recovered supplies and allow time for digestion.','rebirth.practice.skill.animal_processing'),
 ('TradeSurplus','Trade surplus for a useful supply','Make a meaningful sale and a different useful purchase. Sales train Trading and purchases train Bartering. Tiny repeated exchanges and selling back the same purchase are not a practice loop.','rebirth.practice.skill.trading'),
 ('ArmoredTravel','Plan for equipment burden','Travel with armor that adds a real burden and earn Athletics practice. Watch stamina while moving on a slope. Leave enough reserve to escape, then recover in safety rather than exhausting yourself beside an enemy.','rebirth.practice.skill.armor_proficiency'),
 ('StudyThenPractice','Turn reading into useful work','Gain Theory and practical progress. Study helps learning but does not replace doing the activity; recipe knowledge is another separate requirement. Use the skill activity list to find an action you can perform with your current supplies.','rebirth.theory'),
 ('KnowledgeThenWork','Put new knowledge to work','Learn previously unknown recipe or procedure knowledge, then earn practical progress. Before travelling for ingredients, inspect all requirements: knowledge, practical skills, station, tools and materials.','rebirth.knowledge'),
]
second={'FieldMeal':'rebirth.digest.food','WaterRoutine':'rebirth.hydration.slot','RepairLoop':'rebirth.practice.skill.maintenance','SalvageCircuit':'rebirth.practice.skill.electrical','GardenKitchen':'rebirth.practice.skill.cooking','HuntKitchen':'rebirth.practice.skill.cooking','TradeSurplus':'rebirth.practice.skill.bartering','ArmoredTravel':'rebirth.practice.skill.athletics','StudyThenPractice':'rebirth.practice','KnowledgeThenWork':'rebirth.practice'}
event_labels={'rebirth.digest.transfer':'Let food or liquid leave the stomach','rebirth.energy.recovery':'Recover Energy','rebirth.digest.food':'Absorb nutrition','rebirth.hydration.slot':'Carry liquid in the enabled hydration slot','rebirth.theory':'Gain Theory','rebirth.knowledge':'Learn new knowledge','rebirth.practice':'Earn practical skill progress'}
for skill in skills:
    event_labels['rebirth.practice.'+skill.get('id')]='Earn '+loc[skill.get('name_key')]+' practice'
for index,(key,title,desc,event) in enumerate(routes):
    name='rebirthLessonRoute'+key
    lesson(name,title,desc,'rebirthLearningRoutes' if index<6 else 'rebirthLearningRoutesWork',dict(type='ChallengeStatAwarded',challenge_stat=event,stat_text=event_labels[event],count='1'))
    if key in second:
        new[name+'Second']=event_labels[second[key]]
        E.SubElement(add.findall('challenge')[-1],'objective',type='ChallengeStatAwarded',challenge_stat=second[key],stat_text_key=name+'Second',count='1')
from organize_learning_challenges import organize
organize(root, new)
E.indent(root,space='  ')
(ROOT/'Config/_Rebirth/challenges.xml').write_bytes(E.tostring(root,encoding='utf-8',xml_declaration=True,pretty_print=True))
rows=[r for r in rows if not r or r[0] not in new]
rows.extend([k,v] for k,v in new.items())
out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerows(rows);locpath.write_text(out.getvalue(),encoding='utf-8-sig')
print('Generated',len(add.findall('challenge')),'REBIRTH lessons in',len(add.findall('challenge_group')),'chapters.')
