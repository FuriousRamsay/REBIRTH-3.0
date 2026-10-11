from pathlib import Path
import xml.etree.ElementTree as E
import csv
root=Path(__file__).resolve().parents[2];d=root/'_Documentation/CraftingAudit_20261010'
items=E.parse(d/'EFFECTIVE_items_rebirth.xml').getroot()
assert len(items.findall("item[@name='FuriousRamsayCircularSawBlade']"))==1
blade=items.find("item[@name='FuriousRamsayCircularSawBlade']")
assert blade.find("property[@name='CustomIcon']").get('value')=='FR_CircularSawBlade_icon'
assert blade.find("effect_group/passive_effect[@name='DegradationMax']").get('value')=='5000,10000'
blocks=E.parse(d/'EFFECTIVE_blocks_rebirth.xml').getroot()
mods=blocks.find("block[@name='WorkbenchCircularMachine001_FR']/property[@class='Workstation']/property[@name='Modules']").get('value').split(',')
assert set(mods)=={'tools','fuel','output'}
recipes=E.parse(d/'EFFECTIVE_recipes_rebirth.xml').getroot().findall("recipe[@craft_area='WorkbenchCircularMachine001_FR']")
assert recipes and all(r.get('craft_tool')=='FuriousRamsayCircularSawBlade' for r in recipes)
hand=E.parse(d/'EFFECTIVE_recipes_rebirth.xml').getroot().findall("recipe[@name='FuriousRamsayWoodPlank']")
assert any(not r.get('craft_area') and not r.get('craft_tool') and r.get('count')=='1' for r in hand)
loot=E.parse(d/'EFFECTIVE_loot_rebirth.xml').getroot()
assert len(loot.findall("lootcontainer[@name='rollingToolBox']/item[@name='FuriousRamsayCircularSawBlade']"))==1
rows=list(csv.DictReader((root/'Config/Localization.csv').open(encoding='utf-8-sig')))
for key in ('FuriousRamsayCircularSawBlade','FuriousRamsayCircularSawBladeDesc'):
 assert len([r for r in rows if r['Key']==key and r['english']])==1
legacy=root/'../../../7 Days To Die 2.6/Mods/zzz_REBIRTH__Core/UIAtlases/ItemIconAtlas/FR_CircularSawBlade_icon.png'
assert (root/'UIAtlases/ItemIconAtlas/FR_CircularSawBlade_icon.png').read_bytes()==legacy.read_bytes()
print('PASS: saw blade definition/icon/localization, tool module, recipe references and toolbox acquisition compose; wear/runtime not qualified')