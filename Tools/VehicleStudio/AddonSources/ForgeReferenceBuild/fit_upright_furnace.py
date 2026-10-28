"""Fit an upright volumetric envelope. Not a finished furnace or fidelity pass."""
import json,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
r=Path(__file__).parent
camera=json.loads((r/'bellows-frame-plan.json').read_text())
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);H=np.array([math.sin(a),math.cos(a),0.])
U=H*math.sin(e)+np.array([0.,0.,math.cos(e)]);D=np.cross(R,U)
trace=json.loads((r/'furnace-silhouette-recheck.json').read_text())['contourPixels']
def hull(points):
 pts=sorted(set(map(tuple,points)))
 def cross(o,a,b):return (a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
 low=[];high=[]
 for p in pts:
  while len(low)>1 and cross(low[-2],low[-1],p)<=0:low.pop()
  low.append(p)
 for p in reversed(pts):
  while len(high)>1 and cross(high[-2],high[-1],p)<=0:high.pop()
  high.append(p)
 return low[:-1]+high[:-1]
def geometry(p):
 px,py,rx,ry,wall,cap,power=p
 center=R*((px-891)/760)+H*((935-py)/(760*math.sin(e)))
 rings=[(0.,1.),(wall*.5,1.),(wall,1.)]
 rings.extend((wall+cap*math.sin(t),math.cos(t)**power) for t in np.linspace(0,math.pi/2,19)[1:-1])
 n=64;vertices=[]
 for z,scale in rings:
  for angle in np.linspace(0,math.tau,n,endpoint=False):
   vertices.append(center+np.array([rx*scale*math.cos(angle),ry*scale*math.sin(angle),z]))
 vertices.append(center+np.array([0,0,wall+cap]));faces=[]
 for i in range(len(rings)-1):
  for j in range(n):faces.append([i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j])
 for j in range(n):faces.append([(len(rings)-1)*n+j,(len(rings)-1)*n+(j+1)%n,len(vertices)-1])
 faces.append(list(range(n-1,-1,-1)))
 return np.array(vertices),faces,center
def project(v):return np.column_stack([891+760*(v@R),935-760*(v@U)])
def mask(contour):
 im=Image.new('1',(484,362));ImageDraw.Draw(im).polygon([(x/3,y/3) for x,y in contour],fill=1)
 return np.asarray(im,dtype=bool)
target=mask(trace)
yy,xx=np.mgrid[:362,:484]
# Lower-left body is hidden by the bellows/crucible: don't fit invented edges.
visible=~((xx*3<760)&(yy*3>400))
constraints=[]
for record in json.loads((r/'harness-reference-recheck.json').read_text())['records']:
 constraints.extend(record.get('pathPixels',[]))
for record in json.loads((r/'outer-stone-contours.json').read_text())['stones']:
 constraints.append(np.array(record['contour']).mean(0).tolist())
constraints=np.array(constraints)/3;constraint_indices=np.rint(constraints).astype(int)
def loss(p):
 if not (.40<p[2]<.70 and .40<p[3]<.70 and .14<p[4]<.55 and .30<p[5]<.80):return 100.
 if not .8<p[2]/p[3]<1.25:return 100.
 if not .6<p[6]<1.5:return 100.
 v,_,_=geometry(p);m=mask(hull(project(v)))
 mismatch=1-float(np.sum(m&target&visible)/np.sum((m|target)&visible))
 unsupported=1-float(m[constraint_indices[:,1],constraint_indices[:,0]].mean())
 return mismatch+.20*unsupported
p=np.array([890.,790.,.55,.51,.34,.57,1.]);initial=p.copy();steps=np.array([24.,24.,.04,.04,.05,.05,.12]);score=loss(p)
for iteration in range(14):
 improved=False
 for j in range(7):
  options=[]
  for sign in [-1,1]:
   q=p.copy();q[j]+=steps[j]*sign;options.append((loss(q),q))
  value,q=min(options,key=lambda item:item[0])
  if value<score:p=q;score=value;improved=True
 if not improved:steps*=.5
v,faces,center=geometry(p)
fitted_mask=mask(hull(project(v)))
visible_iou=float(np.sum(fitted_mask&target&visible)/np.sum((fitted_mask|target)&visible))
report={'status':'UPRIGHT_ENVELOPE_CANDIDATE_NOT_FIDELITY_PASS','initialParameters':initial.tolist(),
 'parameterOrder':['footprintCenterPixelX','footprintCenterPixelY','worldRadiusX','worldRadiusY','wallHeight','capHeight','capRadialExponent'],
 'parameters':p.tolist(),'center':center.tolist(),'vertices':v.tolist(),'faces':faces,
 'silhouetteIoUAgainstDraftContour':float(np.sum(fitted_mask&target)/np.sum(fitted_mask|target)),
 'visibleRegionIoU':visible_iou,'fitObjective':score,'occludedRegionExcluded':'pixel x<760 and y>400; approximate bellows/crucible coverage',
 'height':float(p[4]+p[5]),'footprintDimensions':[float(2*p[2]),float(2*p[3])],
 'axis':[0,0,1],'groundZ':0,'limitations':['Draft silhouette includes inferred occluded edges','Single view does not determine depth uniquely','No hearth, stones, collar or attachments','Envelope fit is not asset fidelity'],'blenderAllowed':False}
(r/'upright-furnace-envelope.json').write_text(json.dumps(report))
im=Image.new('RGB',(1800,800),'#30343a');draw=ImageDraw.Draw(im)
for panel,angle in enumerate([a,a+math.pi/2,a]):
 elev=e if panel<2 else math.radians(89.9)
 rr=np.array([math.cos(angle),-math.sin(angle),0.]);uu=np.array([math.sin(angle)*math.sin(elev),math.cos(angle)*math.sin(elev),math.cos(elev)]);dd=np.cross(rr,uu)
 screen=np.column_stack([v@rr,-v@uu]);mid=(screen.min(0)+screen.max(0))/2
 scale=460/max(np.ptp(screen,axis=0));xy=(screen-mid)*scale+np.array([300+600*panel,405])
 light=dd+uu-rr;light/=np.linalg.norm(light)
 for face in sorted(faces,key=lambda f:float(v[f].mean(0)@dd)):
  pts=v[face];normal=np.cross(pts[1]-pts[0],pts[2]-pts[0]);length=np.linalg.norm(normal)
  if length<1e-10:continue
  normal/=length
  if normal@dd<0:normal=-normal
  shade=int(95+130*max(0,normal@light));draw.polygon([tuple(t) for t in xy[face]],fill=(shade,)*3)
 if panel==0:
  # Overlay authored silhouette in matching shared camera coordinates, not reference pixels.
  t=(np.array(trace)-np.array([891,935]))/760;t[:,1]*=-1
  t=np.column_stack([t[:,0],-t[:,1]])
  contour=(t-mid)*scale+np.array([300,405]);draw.line([tuple(q) for q in np.vstack([contour,contour[0]])],fill='#ffae54',width=3)
 draw.text((panel*600+30,45),['REFERENCE ANGLE / draft outline in orange','SIDE / same volume','TOP / same volume'][panel],fill='white')
draw.text((30,745),'NUMERICAL ENVELOPE ONLY — no hearth or masonry; not Blender proof and not fidelity approval',fill='white')
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview/upright-envelope-three-views.png');im.save(out)
print(json.dumps({k:report[k] for k in ['parameters','silhouetteIoUAgainstDraftContour','height','footprintDimensions']},indent=2));print(out)
