from pathlib import Path
import csv, sys, xml.etree.ElementTree as ET
R=Path(__file__).resolve().parents[2]
source=R/'Config/_Survivor/background_bonuses.xml'
out=Path(sys.argv[1]) if len(sys.argv)>1 else R/'_Documentation/ProjectChanges/REBIRTH_3_0_BACKGROUND_SIGNATURE_BONUSES_PC015_CHUNK_B_CATALOGUE_20260902.csv'
root=ET.parse(source).getroot()
rows=[]
for e in root.findall('bonus'):
    tuning=';'.join(f"{t.get('key')}={t.get('value')}|locked={t.get('locked')}" for t in e.findall('tuning'))
    rows.append({
        'bonus_id':e.get('id',''),'background_id':e.get('background_id',''),'name_key':e.get('name_key',''),
        'description_key':e.get('description_key',''),'icon_key':e.get('icon_key',''),'icon_state':e.get('icon_state',''),
        'category':e.get('category',''),'handler':e.get('handler',''),'profile':e.get('profile',''),'tuning':tuning})
out.parent.mkdir(parents=True,exist_ok=True)
with out.open('w',encoding='utf-8',newline='') as f:
    w=csv.DictWriter(f,fieldnames=list(rows[0].keys()) if rows else ['bonus_id'])
    w.writeheader(); w.writerows(rows)
print(f'EXPORTED_BACKGROUND_SIGNATURE_BONUSES={len(rows)} path={out}')
