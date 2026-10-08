from pathlib import Path
from lxml import etree as E
r=Path.cwd()
def inject(f,mark,text):
 p=r/f;s=p.read_text(encoding='utf-8-sig');assert mark in s;p.write_text(s.replace(mark,text+'\n'+mark,1),encoding='utf-8')
def repl(f,a,b):
 p=r/f;s=p.read_text(encoding='utf-8-sig');assert a in s;p.write_text(s.replace(a,b),encoding='utf-8')
belts=[('UtilityBelt',2,0),('FieldBelt',4,40),('TacticalBelt',6,100)]
for name,bonus,stage in belts:
 item='rebirthGear'+name
 inject('Config/_Survivor/items.xml','  </append>',f'''    <item name="{item}">
      <property name="Extends" value="rebirthSupportBackSupportBelt"/>
      <property name="CustomIcon" value="rebirthSupportBackSupportBelt"/>
      <property name="CustomIconTint" value="{['185,160,110','110,140,85','95,110,125'][bonus//2-1]}"/>
      <property name="EconomicValue" value="{bonus*100}"/>
      <property name="DescriptionKey" value="{item}Desc"/>
    </item>''')
 inject('Config/_Survivor/support_profiles.xml','</survivor_support_profiles>',f'''  <support_profile id="gear.belt.{name.lower()}" name_key="{item}" kind="survivor_gear" effect_summary="Adds {bonus} toolbelt slots up to the 18-slot maximum." timing_state="survivor_gear" repeat_mode="equipment" gear_slot_id="belt" gear_slot_name_key="xuiRebirthGearSlotBelt" gear_item_id="{item}" toolbelt_slot_bonus="{bonus}" />''')
# Daypack must be usable by every starting character, including negative attributes.
p=r/'Config/_Survivor/support_profiles.xml';s=p.read_text();line=next(x for x in s.splitlines() if 'id="gear.backpack.daypack"' in x);s=s.replace(line,line.replace('min_strength="50"','min_strength="0"').replace('min_constitution="50"','min_constitution="0"'));p.write_text(s)
recipes=[('Daypack',{'resourceCloth':10,'resourceYuccaFibers':20},False),('UtilityBelt',{'resourceCloth':6,'resourceYuccaFibers':12},False),('FieldPack',{'rebirthGearDaypack':1,'resourceLeather':15,'resourceDuctTape':3},True),('HikingPack',{'rebirthGearFieldPack':1,'resourceLeather':25,'resourceDuctTape':5,'resourceMechanicalParts':3},True),('ExpeditionPack',{'rebirthGearHikingPack':1,'resourceLeather':35,'resourceDuctTape':8,'armorParts':2},True),('FieldBelt',{'rebirthGearUtilityBelt':1,'resourceLeather':12,'resourceDuctTape':3},True),('TacticalBelt',{'rebirthGearFieldBelt':1,'resourceLeather':20,'resourceDuctTape':5,'armorParts':2},True)]
for name,ingredients,advanced in recipes:
 item='rebirthGear'+name;area=' craft_area="workbench"' if advanced else ''
 body='\n'.join(f'      <ingredient name="{n}" count="{c}"/>' for n,c in ingredients.items())
 inject('Config/_Survivor/recipes.xml','  </append>',f'    <recipe name="{item}" count="1" always_unlocked="true" craft_time="{30 if advanced else 10}"{area}>\n{body}\n    </recipe>')
 if advanced:inject('Config/_Survivor/recipe_knowledge.xml','</survivor_recipe_knowledge>',f'  <recipe name="{item}" knowledge="procedure.tailoring.backpack_expansion" category="tailoring" literature_item="rebirthManualBackpackExpansion"/>')
# Remove all original unscaled pack entries. Supply a dedicated, loot-stage-gated roll.
f='Config/_Survivor/loot.xml';p=r/f;s=p.read_text();
for n in ['Daypack','FieldPack','HikingPack','ExpeditionPack']:
 s=s.replace(f'    <item name="rebirthGear{n}" loot_prob_template="veryLow" />\n','')
p.write_text(s)
entries=[('ExpandedDaypack',20,69),('FieldPack',30,99),('ExpandedFieldPack',60,129),('HikingPack',90,159),('ExpandedHikingPack',120,199),('ExpeditionPack',150,999999),('ExpandedExpeditionPack',200,999999),('FieldBelt',40,119),('TacticalBelt',100,999999)]
xml=['<configs>','  <append xpath="/lootcontainers/lootprobtemplates">']
for n,low,high in entries:
 xml.append(f'    <lootprobtemplate name="rbStorage{n}"><loot level="0,{low-1}" prob="0"/><loot level="{low},{high}" prob="1"/>'+ (f'<loot level="{high+1},999999" prob="0"/>' if high<999999 else '')+'</lootprobtemplate>')
xml+=['  </append>','  <insertBefore xpath="/lootcontainers/lootgroup[1]">','    <lootgroup name="rebirthStorageGear" count="1">']
for n,low,high in entries:xml.append(f'      <item name="rebirthGear{n}" count="1" loot_prob_template="rbStorage{n}"/>')
xml+=['    </lootgroup>','  </insertBefore>']
for group,chance in [('groupAllClothing','.08'),('groupBackpacks01','.12'),('groupArmyTruck','.12')]:
 if not E.parse(str(r/'../../Data/Config/loot.xml')).xpath('/lootcontainers/lootgroup[@name=$n]',n=group):continue
 xml +=[f'  <append xpath="/lootcontainers/lootgroup[@name=\'{group}\']"><item group="rebirthStorageGear" count="1" prob="{chance}" force_prob="true"/></append>']
xml+=['</configs>'];(r/'Config/_Rebirth/storage_loot.xml').write_text('\n'.join(xml)+'\n')
inject('Config/_Rebirth/loot.xml','</configs>','  <include filename="storage_loot.xml"/>')
inject('Config/entityclasses.xml','</configs>','''  <append xpath="/entity_classes">
    <entity_class name="rebirthGearRecoveryBackpack" extends="DroppedLootContainer">
      <property name="TimeStayAfterDeath" value="3600"/>
    </entity_class>
  </append>''')
# Native Craft objectives: distinct inventory icons, no duplicate generated challenge glyph.
for name,item,icon in [('StoragePack','rebirthGearDaypack','rebirthGearDaypack'),('StorageBelt','rebirthGearUtilityBelt','rebirthSupportBackSupportBelt')]:
 inject('Config/_Rebirth/challenges.xml','</configs>',f'''  <append xpath="/challenges">
    <challenge name="rebirthLesson{name}" title_key="rebirthLesson{name}" short_description_key="rebirthLesson{name}" description_key="rebirthLesson{name}Desc" group="rebirthLearningSurvival" icon="{icon}">
      <objective type="Craft" item="{item}" count="1"/>
    </challenge>
  </append>''')
with (r/'Config/Localization.csv').open('a',encoding='utf-8') as out:
 out.write('\nrebirthGearRecoveryFailed,Could not safely release stored items. Your gear is still equipped.\nrebirthGearRecoveryBackpack,Dropped Backpack Contents\n')
 for n,b,st in belts:
  label={'UtilityBelt':'Utility Toolbelt','FieldBelt':'Field Toolbelt','TacticalBelt':'Tactical Toolbelt'}[n]
  out.write(f'rebirthGear{n},{label}\nrebirthGear{n}Desc,"Equip in your Survivor Belt slot to add {b} toolbelt slots (maximum 18). Removing it drops items from lost slots for 30 minutes."\n')
 out.write('rebirthLessonStoragePack,Make Room to Carry\nrebirthLessonStoragePackDesc,"Craft a Daypack from cloth and plant fibers. Use it from your inventory to equip it in the Backpack slot and gain five slots. Removing a pack puts items from lost slots in a lootable backpack for one hour."\nrebirthLessonStorageBelt,Keep Tools Within Reach\nrebirthLessonStorageBeltDesc,"Craft a Utility Toolbelt from cloth and plant fibers. Use it to equip your Belt slot and gain two toolbelt slots. Removing a belt drops items from lost slots for 30 minutes."\n')
