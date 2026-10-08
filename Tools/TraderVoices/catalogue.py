"""Generate runtime catalogue and localized subtitles from verified authoritative mappings."""
from pathlib import Path
import json,csv,io,xml.etree.ElementTree as ET
root=Path(__file__).resolve().parents[2]
rows=json.loads((Path(__file__).parent/'Source/Trader_Voiceset_Implementation/Production_Manifest.json').read_text())
doc=ET.Element('traderVoices',version='1',inactiveTarget='Poppy')
texts={}
for r in rows:
    asset='#@modfolder(zzz_REBIRTH__3_0):Resources/rebirth_trader_voices.unity3d?'+r['id']
    ET.SubElement(doc,'line',id=r['id'],trader=r['target_trader'],preset=r['preset_id'],category=r['category'],phase=r['phase'],asset=asset,source=r['actual_audio_path'],export=r['requested_game_export_path']).text=r['spoken_text']
    texts[r['id']]=r['spoken_text']
dest=root/'Config/_TraderVoices/catalogue.xml';dest.parent.mkdir(exist_ok=True)
ET.indent(doc);ET.ElementTree(doc).write(dest,encoding='utf-8',xml_declaration=True)
for label in ['Original','Hostile','Gruff','Warm','Smooth','Practical','Sardonic']:texts['rebirthTraderVoice'+label]=label
for trader in ['Rekt','Hugh','Jen','Joel','Bob']:texts['rebirthTraderVoice'+trader+'Title']=trader+' Dialogue Style'
texts['rebirthTraderVoiceSection']='Trader Dialogue'
texts['rebirthTraderVoiceDescription']="Changes this trader's dialogue personality while keeping their own voice. Saved separately for each player."
loc=root/'Config/Localization.csv'
existing=loc.read_text(encoding='utf-8-sig')
keys={r[0] for r in csv.reader(io.StringIO(existing)) if r}
new=io.StringIO();writer=csv.writer(new,lineterminator='\n')
for k,v in texts.items():
    if k not in keys:writer.writerow([k,v])
with loc.open('a',encoding='utf-8',newline='') as f:f.write('\n'+new.getvalue())
print('Catalogue:',len(rows),'active:',sum(r['target_trader']!='Poppy' for r in rows))
