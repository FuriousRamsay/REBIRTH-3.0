"""Read-only native authored candidate inventory; runtime must verify every property."""
import hashlib,json,os,sys,xml.etree.ElementTree as E
from pathlib import Path
R=Path(__file__).resolve().parents[2];game=R.parents[1]
world=Path(os.environ['APPDATA'])/'7DaysToDie/GeneratedWorlds/West Xuyofu Territory/prefabs.xml'
clearance=Path(os.environ['APPDATA'])/'7DaysToDie/Saves/West Xuyofu Territory/CodexTestPurgeFresh20261102/RebirthData/Purge/Clearance'
manifest=E.parse(clearance/'world.xml').getroot();recorded=set()
for shard in manifest.findall('shard'):
    data=(clearance/shard.attrib['file']).read_bytes()
    assert hashlib.sha256(data).hexdigest()==shard.attrib['sha256']
    for poi in E.fromstring(data).findall('poi'):recorded.add((poi.get('prefab'),int(poi.get('x')),int(poi.get('z'))))
index={p.stem:p for p in (game/'Data/Prefabs').rglob('*.xml')}
cache={};rows=[]
for instance in E.parse(world).getroot().findall('decoration'):
    name=instance.get('name');source=index.get(name)
    if not source:continue
    if name not in cache:
        props={p.get('name'):p.get('value','') for p in E.parse(source).getroot().findall('property')}
        parts=props.get('SleeperVolumeGroup','').split(',');groups=[]
        for i in range(0,len(parts)-2,3):
            groups.append({'name':parts[i],'minimum':int(parts[i+1]),'maximum':int(parts[i+2]),
                           'script':props.get('SVS'+str(i//3),'')})
        cache[name]=(props,groups)
    p,groups=cache[name];x,y,z=map(int,instance.get('position').split(','));size=list(map(int,p.get('PrefabSize','0,0,0').split(',')))
    if len(size)!=3:continue
    rows.append({'name':name,'origin':[x,y,z],'rotation':int(instance.get('rotation')),'size':size,'tier':int(p.get('DifficultyTier','0')),
                 'authoredRooms':len(groups),'combatRooms':sum(g['maximum']>0 or bool(g['script'].strip()) for g in groups),'maxActors':sum(g['maximum'] for g in groups),
                 'groups':groups,'trader':p.get('TraderArea','False'),'tags':p.get('Tags',''),'hidden':p.get('SleeperVolumeExclude',''),
                 'recorded':(name,x,z) in recorded,'distance':round(((x-47)**2+(z+254)**2)**.5,1),'source':str(source),
                 'sourceHash':hashlib.sha256(source.read_bytes()).hexdigest()})
rows.sort(key=lambda r:r['distance'])
candidates={
 'threeRoomLowTier':[r for r in rows if not r['recorded'] and r['tier']<=3 and r['combatRooms']==3][:8],
 'ordinarySmall':[r for r in rows if not r['recorded'] and r['name'].startswith(('cabin_','house_old_')) and 3<=r['combatRooms']<=6][:8],
 'zeroSleepers':[r for r in rows if not r['recorded'] and r['authoredRooms']==0 and min(r['size'][0],r['size'][2])>=12 and r['trader']=='False'][:8],
 'highTiers':{str(t):[r for r in rows if not r['recorded'] and r['tier']==t and r['combatRooms']>t][:4] for t in (5,6,7)}
}
filename=sys.argv[1] if len(sys.argv)>1 else 'BATCH28_CANDIDATES.json'
assert Path(filename).name==filename and filename.endswith('.json')
dest=R/'_Documentation/Purge/FullRegression20261102'/filename
dest.write_text(json.dumps({'scope':'Offline authored metadata only; not loaded native room/terrain/traversal proof.','worldHash':hashlib.sha256(world.read_bytes()).hexdigest(),'candidates':candidates},indent=2))
for category,values in candidates.items():
    if isinstance(values,dict):values=[r for group in values.values() for r in group]
    print(category,[(r['name'],r['origin'],r['tier'],r['combatRooms'],r['distance']) for r in values])
