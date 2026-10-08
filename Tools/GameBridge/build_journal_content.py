"""Build localized player guides from the audited learning challenge catalogue."""
from pathlib import Path
import csv, io
from lxml import etree as E
R=Path(__file__).resolve().parents[2]
p=R/'Config/Localization.csv'
rows=list(csv.reader(io.StringIO(p.read_text(encoding='utf-8-sig'))))
loc={r[0]:r[1] for r in rows if len(r)>1}
new={}
root=E.Element('journal')
def entry(id,title,body,image='progression.png',challenge='',trigger='activity'):
    e=E.SubElement(root,'entry',id=id,trigger=trigger,challenges=challenge)
    for tag,value in [('title',title),('body',body)]:
        key='rbJournal_'+id+'_'+tag;new[key]=value;E.SubElement(e,tag).text=key
    E.SubElement(e,'image').text=image

entry('welcome','Welcome to REBIRTH',
'''Your character learns by living, working and studying. There are no skill points to spend. Open Character > Progression, select a skill, then open its explorer to find activities that actually train it. Your background and traits affect your starting point; another survivor may begin differently.

Food and water are a supply pipeline, not instant refills. What you swallow waits in the stomach, moves into the intestine, and is absorbed later. Open Character > Metabolism to see what is pending before taking another portion. A full stomach is not the same as high Nutrition or Hydration.

Study and practice do different jobs. Fundamentals and field notes build Theory. Recipe cards, schematics, patterns, guides and procedure manuals teach specific knowledge. A learned recipe can still require practical skills, tools, a station and ingredients. Inspect all requirements before planning a crafting trip.

The Challenges page contains REBIRTH lessons and longer routines. These are lessons about this progression system, not a requirement to pursue every discipline. New journal guides arrive as you encounter mechanics and finish routines. The Journal tab shows an unread count. Gold titles are unread; opening an entry reads it. You can mark it unread again or mark all entries read.

Click an illustration to enlarge it, then use the magnifier controls and arrows to examine details. Screenshot values are examples from one survivor, not targets for your character. Personal Notes, Places and Plans remain available for your own discoveries.''',trigger='intro')
entry('digestion','Eating is only the first stage',
'''1. INTAKE: swallowing starts processing. It does not immediately credit all of an item's Nutrition or Hydration to your body.

2. STOMACH: solids and liquids occupy space while they wait and transfer onward. Read the separate liquid and solid lines: Waiting and Moving describe different stages. Stomach fullness measures contents, not nourishment. Eating repeatedly because the body reserve has not risen yet can add more pending material than you intended.

3. INTESTINE: transferred material can have another waiting period before absorption. The absorbing rate tells you what is reaching your body now. Pending contents are not yet usable reserves.

4. BODY RESERVES: Hydration and Nutrition show the amount currently available. Their NET rates combine incoming absorption with ongoing use, so they can still fall while some food or water remains in transit. Eating and drinking ahead of a long trip is more useful than waiting until the reserve is empty.

Portion size, contents and your condition affect the process. There is no universal wait time for every meal. Use the live stages and rates rather than repeatedly consuming items on a fixed timer. Digestion advances during active play, not while you are offline. If you are already depleted, reduce exertion and get somewhere safe while appropriate food and water work through the pipeline.''','metabolism.png',trigger='ingestion')
extra={
'WaterSlot':'''Open the hydration controls and inspect the filled container, remaining liquid and automatic-sipping setting. An empty compatible container is still empty; carrying it does not hydrate you. Refill or replace your supply before a journey.

Automatic sipping delivers intake. It does not bypass the stomach or intestine, and pending liquid is not the same as Hydration already absorbed. Use Metabolism to check the difference. Keep a reserve for an unexpectedly long return trip.''',
'AbsorbWater':'''Hydration is the body reserve on the right of Metabolism. Stomach liquid is still on its way; intestinal liquid may be Waiting or Absorbing. Watch the stage labels rather than expecting the reserve to jump immediately after drinking.

A negative NET rate means use exceeds incoming absorption at that moment. Reduce exertion when necessary and allow processing time. Do not mistake an empty intake panel for proof that you have no liquid still in the intestine.''',
'DigestMeal':'''Nutrition measures the usable body reserve; the stomach measures pending contents. After a meal, inspect both the stomach and intestinal solid lines. Transfer and absorption are separate steps, so the Nutrition number can lag behind eating.

Choose food compatible with your character's diet and check its description before consuming it. Give pending portions time to work. Plan meals before travel and strenuous work rather than treating food as an instant emergency refill. The example picture shows one moment in digestion; its quantities are not universal requirements.''',
'Cook':'''A recipe being visible does not mean it is currently craftable. Read unlock requirements, ingredients, station and tools, then inspect the projected skill gain and batch size. Completed eligible work earns practice; merely queuing or cancelling it does not.

Plan the whole meal: obtain ingredients, finish cooking, consume a suitable portion, then allow digestion. Making food and absorbing its nutrition are separate milestones. Keep enough provisions to survive while building the knowledge and supplies for more complex recipes.''',
'Drink':'''Preparing a drink and drinking it are different activities. Eligible completed preparation trains Drink Preparation. Consumption puts liquid into digestion; absorption later supplies Hydration.

Check the recipe's requirements and projected gain before spending ingredients. Carry an appropriate filled supply and inspect the hydration slot before travelling. Check a drink's effects rather than assuming every drink has the same role as clean water.''',
'Medicine':'''Inspect Character > Condition and the item's treatment description. Match treatment to an actual condition: the name or appearance of a medical item alone does not tell you everything it treats. Useful treatment trains Medicine; spending supplies without a medical need is not a substitute.

Food, water, Energy, stamina and health are different quantities. Treating an injury does not automatically refill the others. After treatment, verify the condition and keep enough supplies for the return journey.''',
'Practice':'''The level and NEXT bar describe practical progress. Select a skill and inspect its listed activities to find actions available with your current equipment. The explorer shows relevant activities and expected gains where applicable.

Perform useful completed work and check the resulting progress. Empty attacks, cancelled work and unsupported actions are not training. Practical progress also trains associated attributes. Theory supports learning but is not a replacement for doing the work. There is no pool of points to allocate.''',
'Theory':'''Fundamentals and field notes support Theory through their study actions. Read the item and skill panels to see what is learned and what you already know. Finishing a study action matters; carrying the book does not complete it.

Do not confuse Theory with practical level or specific recipe knowledge. A survivor can understand a subject without having enough practical skill to craft a demanding item. An already studied copy does not grant a second first-read benefit. Use study to support useful practice.''',
'Knowledge':'''Recipe cards, schematics, patterns, guides and procedure manuals provide specific recipe or procedure knowledge. Learn the item, then inspect the target recipe: knowledge is one requirement, not a promise that every other requirement is satisfied.

Check practical skills, stations, tools and materials. The requirement panel identifies what is missing. Related activity lists help you find practical work to train a required skill. Plan supplies around the actual recipe rather than the artwork on a card.''',
'Audio':'''Learning cassettes cover fundamentals and field notes. Recipe cards are learned as recipe knowledge; they are not a second catalogue of recipe audiobooks.

Inspect the cassette and compatible player requirements before starting. A Walkman is useful equipment, but possession alone is not a completed listening lesson. Allow the session to finish and verify the learning result. An interrupted session does not count as a finished lesson. Check personal-storage loot and trader stock while exploring.''',
'Seeds':'''Plantable seeds are supplies for a continuing food and herb chain. Inspect each crop's planting and water requirements; do not assume every seed uses an identical setup. Kitchens and food-store loot are useful places to look.

Planting starts the process. Waiting alone is not Farming practice; supported farming work and mature harvesting matter. Reserve seed or planting supplies for the next crop and use the harvest in recipes that actually call for those ingredients.''',
'Repair':'''Read the equipment's repair material and requirements before durability reaches zero. Different tools, weapons and vehicles can use different supplies and service routes. A generic repair item is not automatically correct for every object.

Useful completed maintenance is the activity. Repair genuinely damaged equipment or perform eligible maintenance work and verify the gain. Plan a supply loop: gather what you need, maintain equipment, then return to productive work.'''
}
challenges=E.parse(str(R/'Config/_Rebirth/challenges.xml')).xpath('//challenge[starts-with(@name,"rebirthLesson")]')
for c in challenges:
    cid=c.get('name');suffix=cid.removeprefix('rebirthLesson')
    title=loc[c.get('title_key')];body=loc[c.get('description_key')]
    image='progression.png';trigger='completed' if suffix.startswith('Route') else 'activity'
    if suffix in extra:body+='\n\n'+extra[suffix]
    elif suffix.startswith('Skill_'):
        body+='\n\nYOUR NEXT STEP\nOpen Character > Progression and select this skill. Inspect the training activities and choose useful work you can perform now. Check your current level and NEXT bar before and after the activity. Do not assume an action trains this skill merely because the item looks related.\n\nStudy relevant fundamentals or field notes to support Theory. Specific recipes and procedures may require separate knowledge, practical skills, a station and supplies. Read every requirement before committing materials. Learning this discipline does not require abandoning the others; choose activities that support your current needs.'
    else:
        trigger='completed'
        body+='\n\nYou have completed the linked routine. Make it sustainable: inspect supplies before leaving, do useful work, verify the result, and replace what you consumed. Completing a lesson does not make future supplies or requirements disappear.\n\nUse the relevant skill explorer for current training options and the recipe panel for exact requirements. Keep personal notes about dependable supply locations. Check Metabolism before the next trip: pending food and water still need processing, and stamina recovery is not the same as replenishing all of your longer-term reserves.'
    if suffix=='RouteEnergyRecovery':
        body=loc[c.get('description_key')]+'''\n\nStamina is what you spend on immediate exertion. Energy is the longer-term reserve shown separately in Metabolism. Stopping briefly to recover stamina does not mean you have restored everything needed for another long expedition.

Read Energy's current value, CAP and MAX separately. The current effective cap can be below your maximum; recovery cannot be judged from MAX alone. Watch the live Use and Recovery rates. Nutrition supports Energy recovery, so a recovering survivor can consume food reserve while standing still. Food still in the stomach is not yet available nutrition.

Plan three stages: supply appropriate food and water ahead of time, allow absorption, then recover somewhere safe. Traits and conditions can change the rates. Use your character's live values rather than the example numbers in the illustration. If the cap or recovery is low, inspect Metabolism and Condition before assuming another immediate meal will fix it.'''
    if suffix in ['WaterSlot','AbsorbWater','DigestMeal','Medicine'] or suffix.startswith('RouteDigestion') or suffix=='RouteEnergyRecovery':image='metabolism.png'
    elif suffix in ['Cook','Drink','Knowledge','Seeds','Repair'] or suffix in ['RouteFieldMeal','RouteGardenKitchen','RouteRepairLoop','RouteKnowledgeThenWork']:image='crafting.png'
    entry(suffix,title,body,image,cid,trigger)
ui={'Nav':'JOURNAL','Read':'READ','Unread':'UNREAD','Guide':'GUIDE','MarkUnread':'MARK UNREAD','MarkRead':'MARK READ','ReadAll':'MARK ALL READ','Expand':'ENLARGE ILLUSTRATION','ZoomIn':'ZOOM +','ZoomOut':'ZOOM -','Fit':'FIT','Left':'LEFT','Right':'RIGHT','Up':'UP','Down':'DOWN','CloseImage':'CLOSE IMAGE','SaveError':'Reading status could not be saved. Your existing notes are unchanged.','ImageError':'This illustration could not be loaded.'}
new.update({'xuiRebirthJournal'+k:v for k,v in ui.items()})
# Game localization and the project's line-oriented authoring audit expect escaped newlines.
new={k:v.replace('\r','').replace('\n','\\n') for k,v in new.items()}
rows=[r for r in rows if not r or r[0] not in new];rows.extend(new.items())
out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerows(rows);p.write_text(out.getvalue(),encoding='utf-8-sig')
E.indent(root,space='  ');(R/'Resources/Journal/entries.xml').write_bytes(E.tostring(root,encoding='utf-8',pretty_print=True,xml_declaration=True))
print(f'Generated {len(root)} journal entries, including all skill lessons and completed routines.')
