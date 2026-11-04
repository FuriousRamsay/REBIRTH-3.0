import os,xml.etree.ElementTree as E,json
from pathlib import Path
r=Path.cwd();game=r.parents[1];idx={p.stem:p for p in (game/'Data/Prefabs').rglob('*.xml')};rows=[]
for d in E.parse(Path(os.environ['APPDATA'])/'7DaysToDie/GeneratedWorlds/West Xuyofu Territory/prefabs.xml').getroot().findall('decoration'):
 p=idx.get(d.get('name'))
 if not p:continue
 props={v.get('name'):v.get('value','') for v in E.parse(p).getroot().findall('property')}
 scripts={k:v for k,v in props.items() if k and k.startswith('SVS') and v.strip()}
 if not scripts:continue
 groups=props.get('SleeperVolumeGroup','').split(',');n=len(groups)//3
 if n>8:continue
 x,y,z=map(int,d.get('position').split(','));rows.append({'name':d.get('name'),'origin':[x,y,z],'size':props.get('PrefabSize'),'rotation':d.get('rotation'),'tier':props.get('DifficultyTier'),'rooms':n,'scripts':scripts,'groups':groups,'distance':((x-47)**2+(z+254)**2)**.5})
rows.sort(key=lambda x:x['distance']);out=r/'_Documentation/Purge/FullRegression20261102/BATCH31_SMALL_SCRIPTS.json';out.write_text(json.dumps(rows,indent=2));print(json.dumps(rows[:8],indent=2))
