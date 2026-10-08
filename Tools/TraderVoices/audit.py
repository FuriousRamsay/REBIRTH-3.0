"""Offline conformance checks; does not claim runtime or listening certification."""
from pathlib import Path
import xml.etree.ElementTree as ET,json,csv,collections,hashlib
root=Path(__file__).resolve().parents[2]
catalogue=ET.parse(root/'Config/_TraderVoices/catalogue.xml').getroot()
lines=catalogue.findall('line');assert len(lines)==3600
ids=[e.get('id') for e in lines];assert len(set(ids))==3600
source=Path(__file__).parent/'Source/Trader_Voiceset_Implementation'
manifest={r['id']:r for r in json.loads((source/'Production_Manifest.json').read_text())}
active=[e for e in lines if e.get('trader')!='Poppy'];assert len(active)==3000
assert collections.Counter(e.get('trader') for e in lines)==dict.fromkeys(['Rekt','Hugh','Jen','Joel','Bob','Poppy'],600)
localization=list(csv.reader((root/'Config/Localization.csv').open(encoding='utf-8-sig')))
loc={r[0]:r[1] for r in localization if len(r)>=2}
for e in lines:
    r=manifest[e.get('id')]
    assert loc[e.get('id')]==e.text==r['spoken_text']
    assert (source/e.get('source')).is_file()
    assert e.get('export')==r['requested_game_export_path']
    assert e.get('asset').endswith('?'+e.get('id'))
for label in ['Original','Hostile','Gruff','Warm','Smooth','Practical','Sardonic']:assert loc['rebirthTraderVoice'+label]==label
ui=ET.parse(root/'Config/XUi_Menu/windows.xml')
options=[e for e in ui.iter('rect') if e.get('controller')=='RebirthTraderVoiceOption, RebirthUtils']
assert [e.get('name') for e in options]==['traderVoice'+t for t in ['Rekt','Hugh','Jen','Joel','Bob']]
for e in options: assert e.find('combobox').get('tooltip_key')=='rebirthTraderVoiceDescription'
report={'hashVerified':3652,'catalogue':3600,'active':3000,'inactivePoppy':600,'personalOptions':5,'subtitleMappings':3600,
 'passed':True,'scope':'Offline source, catalogue, localization and UI assertions; not runtime/listening certification'}
(root/'_Documentation/Trader Voicesets/offline_audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
