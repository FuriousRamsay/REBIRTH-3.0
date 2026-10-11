from pathlib import Path
import re,csv,io,xml.etree.ElementTree as ET
root=Path.cwd()
source=(root/'Scripts/Purge/Runtime/RebirthPurgeInformationTopics.cs').read_text()
topics=re.findall(r'new Topic\("([^"]+)","([^"]+)","([^"]+)","([^"]+)","([^"]+)"\)',source)
assert len(topics)==9
backup=root/'_Documentation/PurgeReaudit_20261010/before/Config/items.xml'
backup.parent.mkdir(parents=True,exist_ok=True)
if not backup.exists():backup.write_bytes((root/'Config/items.xml').read_bytes())
patch=ET.Element('configs');append=ET.SubElement(patch,'append',xpath='/items')
journal_path=root/'Resources/Journal/entries.xml'
journal=ET.parse(journal_path);existing={e.get('id') for e in journal.getroot()}
localization_path=root/'Config/Localization.csv'
rows=list(csv.reader(localization_path.read_text(encoding='utf-8-sig').splitlines()))
existing_keys={r[0] for r in rows if r}
messages={
'xuiRebirthPurgeIntroTitle':'The Purge',
'xuiRebirthPurgeIntroBody':"The streets have been cleared of roaming zombies, but occupied buildings remain dangerous. Your mission is to clear eligible locations across the Pine Forest, Desert, Snow, Wasteland and Burnt Forest.\\n\\nProgress is shared, while each biome has its own clearing objective. You may enter and clear them in any order.\\n\\nDiscovery milestones reveal more locations on your map. Credited kills from cleared locations earn supply drops. Watch the Purge display for your current objective and supply progress.",
'xuiRebirthPurgeScannerTitle':'Threat Scanner',
'xuiRebirthPurgeScannerBody':"The threat scanner helps locate remaining hostiles inside occupied locations. It is part of your communication system and does not require an equipped tool.\\n\\nEnemy counts are estimates while rooms remain unactivated. Clearing more rooms allows the scanner to identify remaining active threats. A quiet room or absent marker does not by itself prove the entire location is clear.\\n\\nUse the room and enemy display together with the shared map markers to track your progress.",
'xuiRebirthPurgeSupplyUpdateTitle':'Supply Drop Update',
'xuiRebirthPurgeSupplyUpdateBody':"Your first supply drop has been called. Follow the airdrop marker to collect your reserved supplies.\\n\\nEach earned supply reward raises the cost of the next reward by 5 credited kills. The added cost stops increasing at 150. With the base cost of 75, the maximum unmodified requirement is 225.\\n\\nSupply credits come from eligible kills in cleared locations. Keep clearing occupied locations and check the Purge display for your remaining supply requirement.\\n\\nThis briefing is saved in your Journal.",
'xuiRebirthPurgeDesertTitle':'Purge Field Briefing: Desert',
'xuiRebirthPurgeSnowTitle':'Purge Field Briefing: Snow',
'xuiRebirthPurgeWastelandTitle':'Purge Field Briefing: Wasteland',
'xuiRebirthPurgeBurntForestTitle':'Purge Field Briefing: Burnt Forest',
'xuiRebirthPurgeBiomeHelpBody':"Your progress has earned another field briefing.\\n\\nEvery biome has an independent clearing objective. Its discovery milestones reveal eligible locations of the earned difficulty tier, helping you plan further clearing runs.\\n\\nYou may visit and clear any biome in any order. This briefing does not unlock entry or require a stabilizer. Check the map and Purge display for the current shared progress."
}
for ident,item,sprite,title,body in topics:
    assert (root/'UIAtlases/UIAtlas'/f'{sprite}.png').exists()
    if ident not in existing:
        e=ET.SubElement(journal.getroot(),'entry',id=ident,trigger='purge-information')
        ET.SubElement(e,'title').text=title;ET.SubElement(e,'body').text=body
    node=ET.SubElement(append,'item',name=item)
    for k,v in [('Extends','blankNoteMaster'),('CreativeMode','Dev'),('DescriptionKey',body),
                ('CustomIcon','challengeQuestMaster'),('ItemTypeIcon','book'),('Stacknumber','1'),
                ('SellableToTrader','false'),('NoScrapping','true')]:
        ET.SubElement(node,'property',name=k,value=v)
    action=ET.SubElement(node,'property',{'class':'Action0'})
    for k,v in [('Class','Eat'),('Delay','1'),('UseAnimation','false'),('Sound_start','read_mod'),('Sound_in_head','true')]:
        ET.SubElement(action,'property',name=k,value=v)
    group=ET.SubElement(node,'effect_group',tiered='false')
    ET.SubElement(group,'triggered_effect',trigger='onSelfPrimaryActionEnd',action='RebirthPurgeInformation, RebirthUtils',topic=ident)
    if item not in existing_keys:
        messages[item]=messages.get(title,next((r[1] for r in rows if len(r)>1 and r[0]==title),title))
ET.indent(patch,space='  ')
(root/'Config/_Purge/information_items.xml').write_text(ET.tostring(patch,encoding='unicode'))
ET.indent(journal,space='  ');journal.write(journal_path,encoding='utf-8',xml_declaration=True)
items=root/'Config/items.xml';text=items.read_text()
include='<include filename="_Purge/information_items.xml"/>'
if include not in text:items.write_text(text.replace('<configs>','<configs>\n  '+include,1))
out=io.StringIO(newline='');writer=csv.writer(out,lineterminator='\n')
for key,value in messages.items():
    if key not in existing_keys:writer.writerow((key,value))
with localization_path.open('a',encoding='utf-8',newline='') as f:f.write('\n'+out.getvalue())
print('9 stable reading items and Journal topic definitions validated against available artwork.')