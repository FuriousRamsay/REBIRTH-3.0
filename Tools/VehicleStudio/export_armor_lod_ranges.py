"""Extract existing native armor ownership markers from the actual staged prefab."""
from pathlib import Path
import re,json,hashlib
OUT=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019/Output')
prefab=Path('E:/_Haven/shared/REBIRTHVehicleStudio_Camper/Assets/REBIRTHCamperTurretProduction_20261020/rebirthCamper80s.prefab')
raw=prefab.read_bytes();text=raw.decode('utf-8-sig')
blocks={int(m[2]):(int(m[1]),m[3]) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
def ref(body,name):
    match=re.search(r'^  '+name+r': \{fileID: (-?\d+)\}',body,re.M);return int(match[1]) if match else 0
names={i:re.search(r'^  m_Name: (.*)$',b,re.M)[1].strip('"') for i,(kind,b) in blocks.items() if kind==1}
transforms={i:(ref(b,'m_GameObject'),ref(b,'m_Father')) for i,(kind,b) in blocks.items() if kind==4}
by_go={go:i for i,(go,parent) in transforms.items()}
manifest=json.loads((OUT/'CamperStationLods/manifest.json').read_text())
entries=[]
for i,(kind,body) in blocks.items():
    if kind!=33:continue
    go=ref(body,'m_GameObject');name=names.get(go,'')
    if not name.startswith('REBIRTHArmorSurface_'):continue
    mesh=re.search(r'm_Mesh: \{fileID: (-?\d+), guid: ([a-f0-9]+),',body)
    assert mesh,'External immutable armor mesh reference required'
    matches=[e for e in manifest['entries'] if e['guid']==mesh[2] and e['localId']==int(mesh[1])]
    assert len(matches)==1,'Unique armor mesh LOD entry required: '+name
    entry=matches[0];ranges=[]
    for child,(child_go,parent) in transforms.items():
        if parent!=by_go[go]:continue
        fields=names[child_go].split('_')
        if len(fields)==5 and fields[0]=='ArmorRange':
            order,threshold,start,count=map(int,fields[1:]);ranges.append({'order':order,'threshold':threshold,'start':start,'count':count})
    ranges.sort(key=lambda r:r['start']);end=0
    for r in ranges:
        assert r['start']==end and r['count']%3==0,'Armor ownership gap/overlap'
        end+=r['count']
    assert ranges and end==entry['sourceTriangles']*3,'Full immutable armor range coverage required: '+name
    entries.append({'key':entry['key'],'name':name,'ranges':ranges})
assert len(entries)==4 and len({r['order'] for e in entries for r in e['ranges']})==54,'Four current surfaces (door hardware shares door ownership) / 54 original ownership IDs required'
result={'schema':1,'prefabSha256':hashlib.sha256(raw).hexdigest(),'inputSha256':manifest['inputSha256'],'entries':entries}
(OUT/'camper-armor-lod-ranges.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps({'surfaces':len(entries),'ranges':sum(len(e['ranges']) for e in entries),'entries':[{k:e[k] for k in ('key','name')} for e in entries]}))
