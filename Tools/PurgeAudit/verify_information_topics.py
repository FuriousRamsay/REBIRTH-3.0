import re,json,xml.etree.ElementTree as ET,csv,argparse
from pathlib import Path
parser=argparse.ArgumentParser(description='Verify Purge topic mappings; optionally generate presentation scenario.')
parser.add_argument('--check',action='store_true',help='Read-only validation; preserve scenarios and evidence files.')
args=parser.parse_args()
root=Path.cwd()
topics=re.findall(r'new Topic\("([^"]+)","([^"]+)","([^"]+)","([^"]+)","([^"]+)"\)',(root/'Scripts/Purge/Runtime/RebirthPurgeInformationTopics.cs').read_text())
assert len(topics)==9
entries={e.get('id'):e for e in ET.parse(root/'Resources/Journal/entries.xml').getroot()}
items={e.get('name'):e for e in ET.parse(root/'Config/_Purge/information_items.xml').findall('.//item')}
rows=list(csv.reader((root/'Config/Localization.csv').read_text(encoding='utf-8-sig').splitlines()))
steps=[{'action':{'command':'surroundings'}},{'action':{'command':'cleararea','args':{'radius':40}}},{'action':{'command':'restore'}}]
for ident,item,sprite,title,body in topics:
    assert entries[ident].findtext('title')==title and entries[ident].findtext('body')==body
    assert items[item].find('.//triggered_effect').get('topic')==ident
    assert (root/'UIAtlases/UIAtlas'/f'{sprite}.png').exists()
    for key in (title,body,item):
        matches=[r for r in rows if r and r[0]==key]
        assert len(matches)==1 and len(matches[0])==2 and matches[0][1],(key,matches)
    steps.extend([
        {'action':{'command':'purgeinformationpreview','args':{'confirm':'CodexTest','topic':ident}}},
        {'wait':.5},{'expectWindowOpen':'rebirthPurgeInformation'},
        {'screenshot':'purge_topic_'+ident},
        {'action':{'command':'click','args':{'id':'purgeInformationClose'}}},{'wait':.3}
    ])
steps.append({'expectNoErrors':True})
if not args.check:
    (root/'Tools/GameBridge/tests/purge_information_presentation.json').write_text(json.dumps({'name':'Purge information: nine topic presentation fixtures, not reward triggers','stopOnFailure':True,'steps':steps},indent=2))
    (root/'_Documentation/PurgeReaudit_20261010/INFORMATION_TOPIC_STATIC_VALIDATION.json').write_text(json.dumps({'topics':len(topics),'items':len(items),'uniqueLocalization':True,'journalBindings':True,'artworkPresent':True,'runtimeAllTopics':'Not assessed by this static check; consult the native test matrix and retained runtime evidence.'},indent=2))
print('PASS 9 item/topic/Journal/art/localization mappings; '+('read-only check, no files generated.' if args.check else 'presentation scenario generated; runtime behavior not assessed.'))
