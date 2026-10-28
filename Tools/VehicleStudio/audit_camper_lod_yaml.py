"""Read-only Unity text-prefab LOD coverage audit; no Unity process needed."""
import argparse
import hashlib
import json
from pathlib import Path
import re

def main():
    p=argparse.ArgumentParser();p.add_argument('prefab');p.add_argument('output');args=p.parse_args()
    path=Path(args.prefab);raw=path.read_bytes();text=raw.decode('utf-8-sig')
    blocks={int(m[2]):(int(m[1]),m[3]) for m in re.finditer(r'^--- !u!(\d+) &(-?\d+)[^\n]*\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)}
    def ref(body,name):
        m=re.search(r'^  '+re.escape(name)+r': \{fileID: (-?\d+)\}',body,re.M);return int(m[1]) if m else 0
    names={i:re.search(r'^  m_Name: (.*)$',b,re.M)[1].strip('"') for i,(kind,b) in blocks.items() if kind==1}
    transform_go={i:ref(b,'m_GameObject') for i,(kind,b) in blocks.items() if kind==4}
    fathers={ref(b,'m_GameObject'):transform_go.get(ref(b,'m_Father'),0) for kind,b in blocks.values() if kind==4}
    def hierarchy(go):
        seen=set();parts=[]
        while go and go not in seen:
            seen.add(go);parts.append(names.get(go,str(go)));go=fathers.get(go,0)
        return '/'.join(reversed(parts))
    renderers={i:hierarchy(ref(b,'m_GameObject')) for i,(kind,b) in blocks.items() if kind in (23,137)}
    groups=[];covered=set()
    for i,(kind,b) in blocks.items():
        if kind!=205:continue
        levels=[]
        for match in re.finditer(r'  - screenRelativeHeight: ([^\n]+)\n(.*?)(?=  - screenRelativeHeight:|  m_Enabled:|\Z)',b,re.S):
            ids=[int(x) for x in re.findall(r'renderer: \{fileID: (-?\d+)\}',match[2])]
            covered.update(ids);levels.append({'screenHeight':float(match[1]),'renderers':[renderers.get(x,'UNRESOLVED:'+str(x)) for x in ids]})
        groups.append({'path':hierarchy(ref(b,'m_GameObject')),'levels':levels})
    categories={}
    for id,name in renderers.items():
        category='turrets' if 'REBIRTHCamperTurretSlot' in name else 'stations' if 'REBIRTHCampRoofStations' in name or 'REBIRTHCampInteriorStations' in name or 'REBIRTHCampPowerEquipment' in name else 'camper-and-other'
        row=categories.setdefault(category,{'covered':[],'uncovered':[]});row['covered' if id in covered else 'uncovered'].append(name)
    report={'schema':1,'source':str(path.resolve()),'sha256':hashlib.sha256(raw).hexdigest(),'nestedPrefabInstances':sum(k==1001 for k,b in blocks.values()),'lodGroups':groups,'categories':categories,'scope':'serialized renderer membership only; does not measure GPU residency, runtime LOD selection or mesh quality'}
    out=Path(args.output);out.parent.mkdir(parents=True,exist_ok=True);out.write_text(json.dumps(report,indent=2),encoding='utf-8')
    print(json.dumps({'groups':len(groups),'nested':report['nestedPrefabInstances'],'categories':{name:{key:len(value) for key,value in row.items()} for name,row in categories.items()}}))

if __name__=='__main__':main()
