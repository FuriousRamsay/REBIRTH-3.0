"""Read installed prefab collider metadata; does not write the bundle."""
import sys, json, hashlib
from pathlib import Path
root = Path(r'C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die\Mods\zzz_REBIRTH__3_0')
sys.path.insert(0, str(Path(__file__).parent / '.deps'))
import UnityPy
bundle = root / 'Resources/rebirth_camper80s.unity3d'
env = UnityPy.load(str(bundle))
objects = {o.path_id:o for o in env.objects}
gos = {i:o.read_typetree() for i,o in objects.items() if o.type.name == 'GameObject'}
trs = {i:o.read_typetree() for i,o in objects.items() if o.type.name == 'Transform'}
bygo = {t['m_GameObject']['m_PathID']:i for i,t in trs.items()}
def chain(go):
    result=[]
    while go in bygo:
        result.append(go)
        father=trs[bygo[go]]['m_Father']['m_PathID']
        if father not in trs: break
        go=trs[father]['m_GameObject']['m_PathID']
    return result[::-1]
def rotate(v,q):
    u=(q['x'],q['y'],q['z']);w=q['w']
    cross=lambda a,b:(a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0])
    c=cross(u,v);d=cross(u,c)
    return tuple(v[i]+2*w*c[i]+2*d[i] for i in range(3))
def point(v,ancestors):
    for go in reversed(ancestors):
        t=trs[bygo[go]];s=t['m_LocalScale'];p=t['m_LocalPosition']
        v=rotate(tuple(v[i]*s[k] for i,k in enumerate('xyz')),t['m_LocalRotation'])
        v=tuple(v[i]+p[k] for i,k in enumerate('xyz'))
    return v
rows=[]
for i,o in objects.items():
    if o.type.name not in ('BoxCollider','MeshCollider','CapsuleCollider','WheelCollider'):continue
    d=o.read_typetree(); go=d['m_GameObject']['m_PathID']; ancestors=chain(go)
    row=dict(id=i,type=o.type.name,path='/'.join(gos[g]['m_Name'] for g in ancestors),
                     enabled=d.get('m_Enabled'),trigger=d.get('m_IsTrigger'),
                     active=all(gos[g]['m_IsActive'] for g in ancestors),
                     local=trs.get(bygo.get(go)),collider=d)
    if o.type.name=='CapsuleCollider':
        r=d['m_Radius'];half=max(0,d['m_Height']/2-r);axis=d['m_Direction']
        c=tuple(d['m_Center'][k] for k in 'xyz')
        ends=[point(tuple(c[j]+(sign*half if j==axis else 0) for j in range(3)),ancestors) for sign in (-1,1)]
        scale=1
        for g in ancestors:scale*=max(abs(x) for x in trs[bygo[g]]['m_LocalScale'].values())
        row['capsuleWorldBounds']={'min':[min(e[j] for e in ends)-r*scale for j in range(3)],'max':[max(e[j] for e in ends)+r*scale for j in range(3)]}
    rows.append(row)
out=Path(sys.argv[1]);out.parent.mkdir(parents=True,exist_ok=True)
out.write_text(json.dumps(dict(bundle=str(bundle),sha256=hashlib.sha256(bundle.read_bytes()).hexdigest(),colliders=rows),indent=2))
from collections import Counter
print(json.dumps(dict(count=len(rows),types=Counter(r['type'] for r in rows),output=str(out))))
for r in rows:
    if r['active'] and r['enabled'] and not r['trigger']:
        print(r['path'],r['type'])
