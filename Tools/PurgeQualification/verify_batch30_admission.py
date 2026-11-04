"""Read-only closed-save custody and omission proof for native batch30."""
import hashlib,json,os,sys,xml.etree.ElementTree as E
from pathlib import Path
R=Path(__file__).resolve().parents[2]
batch=sys.argv[2] if len(sys.argv)>2 else 'purge-b30'
assert batch.startswith('purge-b') and batch[7:].isdigit()
stage=R/'Tools/Profiler/out'/batch;stage.mkdir(parents=True,exist_ok=True)
base=Path(os.environ['APPDATA'])/'7DaysToDie/Saves/West Xuyofu Territory/CodexTestPurgeFresh20261102/RebirthData/Purge/Clearance'
def records():
    m=E.parse(base/'world.xml').getroot();rows={}
    for s in m.findall('shard'):
        data=(base/s.get('file')).read_bytes();assert hashlib.sha256(data).hexdigest()==s.get('sha256')
        for p in E.fromstring(data).findall('poi'):
            key=f"{len(p.get('prefab'))}:{p.get('prefab')}:"+':'.join(p.get(k) for k in ('x','y','z','rotation','sx','sy','sz'))
            actors={a.get('token'):{'class':a.get('class'),'point':a.get('point'),'death':None if a.find('death') is None else dict(a.find('death').attrib)} for a in p.findall('./observations/volume/actor')}
            rows[key]={'prefab':p.get('prefab'),'epoch':p.get('epoch'),'state':p.get('state'),'actors':actors}
    return {'revision':m.get('revision'),'records':rows}
current=records()
if sys.argv[1]=='before':
    (stage/'closed-before.json').write_text(json.dumps(current,indent=2));print(json.dumps({'records':len(current['records']),'revision':current['revision'],'mine':[(k,len(v['actors']),sum(a['death'] is not None for a in v['actors'].values())) for k,v in current['records'].items() if v['prefab']=='mine_01']}))
else:
    old=json.loads((stage/'closed-before.json').read_text());lost=[]
    for key,prior in old['records'].items():
        now=current['records'].get(key)
        if now is None:lost.append([key,'record lost']);continue
        if now['epoch']!=prior['epoch']:lost.append([key,'unexpected epoch change']);continue
        for token,actor in prior['actors'].items():
            nxt=now['actors'].get(token)
            if nxt is None or nxt['class']!=actor['class'] or nxt['point']!=actor['point'] or actor['death'] is not None and nxt['death']!=actor['death']:lost.append([key,token,'custody/death receipt lost'])
    witnesses=json.loads((stage/'untouched-witnesses.json').read_text(encoding='utf-8-sig'))
    assert isinstance(witnesses,list),'Invalid witness shape'
    violated=[w['key'] for w in witnesses if w['key'] in current['records']]
    report={'oldRecords':len(old['records']),'newRecords':len(current['records']),'idleWitnesses':len(witnesses),'idleStatus':'QUALIFIED' if witnesses else 'UNQUALIFIED_NO_POSITIVE_WITNESS','unexpectedDiscoveries':violated,'lostCustody':lost,'scope':'Exact old records, actor tokens/class/spawn point and existing death receipts compared independently. Idle shutdown absence requires positive native witnesses; zero witnesses cannot pass. Not MP or full-room traversal acceptance.'}
    (stage/'closed-after-review.json').write_text(json.dumps(report,indent=2));print(json.dumps(report));assert not lost and not violated and witnesses,'Custody/absence failure or no positive idle witness: inspect report'
