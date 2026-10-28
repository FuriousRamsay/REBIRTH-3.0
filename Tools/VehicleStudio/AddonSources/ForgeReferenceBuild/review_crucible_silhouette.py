"""Compare the proposed solid vessel silhouette to the independent trace."""
import json, math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

root = Path(__file__).parent
p = json.loads((root/'crucible-volume-plan.json').read_text())
t = next(r for r in json.loads((root/'left-reference-records.json').read_text())['records'] if r['id']=='crucible_body')
print('Observed fields:', list(t))
az, el = map(math.radians, [30, 24])
R = np.array([math.cos(az), -math.sin(az), 0.])
U = np.array([math.sin(az)*math.sin(el), math.cos(az)*math.sin(el), math.cos(el)])
base = np.array(p['positionBaseCenter'])
def project(v): return (891+760*float(R@v), 935-760*float(U@v))
points=[]
for height,radius in [(0,p['outerBottomRadius']),(p['height'],p['outerTopRadius'])]:
    for a in np.linspace(0,math.tau,180,endpoint=False):
        points.append(project(base+np.array([radius*math.cos(a),radius*math.sin(a),height])))
def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
ordered=sorted(set(points)); lower=[];upper=[]
for sequence, chain in [(ordered,lower),(ordered[::-1],upper)]:
    for point in sequence:
        while len(chain)>1 and cross(chain[-2],chain[-1],point)<=0:chain.pop()
        chain.append(point)
hull=lower[:-1]+upper[:-1]
observed=t.get('outerContour',t.get('contour',t.get('outline')))
if observed is None:
    observed=next(v for k,v in t.items() if 'outer' in k.lower() and isinstance(v,list))
def segment_distance(q,a,b):
    q,a,b=map(lambda x:np.array(x,float),(q,a,b));v=b-a
    return float(np.linalg.norm(q-(a+np.clip(np.dot(q-a,v)/np.dot(v,v),0,1)*v)))
errors=[min(segment_distance(q,hull[i],hull[(i+1)%len(hull)]) for i in range(len(hull))) for q in observed]
im=Image.new('RGB',(900,1000),'#f5f5f1');d=ImageDraw.Draw(im)
def display(v):return ((v[0]-390)*3+40,(v[1]-590)*2.5+80)
d.text((20,15),'Crucible planning comparison: blue = observed; red = solid volume hypothesis',fill='black')
d.line([display(v) for v in observed+[observed[0]]],fill='#176ba0',width=4)
d.line([display(v) for v in hull+[hull[0]]],fill='#c0392b',width=3)
d.text((20,950),'Not a Blender render. Camera and vessel profile remain unvalidated.',fill='black')
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview');out.mkdir(parents=True,exist_ok=True)
im.save(out/'crucible-solid-outline.png')
report={'status':'INDEPENDENT_SILHOUETTE_CHECK_NOT_ACCEPTANCE','meanDistancePx':float(np.mean(errors)),'maxDistancePx':max(errors),'perObservedPointDistancePx':errors,'projectedSolidHull':hull,'nextAction':'Inspect outline discrepancies before accepting vessel dimensions; the rim-only fit does not validate the full vessel.','blenderAllowed':False}
(root/'crucible-silhouette-audit.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if k!='projectedSolidHull'},indent=2))
