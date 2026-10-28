"""Construct numeric sweep sections for review; no Blender or legacy mesh use."""
import json,math
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
r=Path(__file__).parent
camera=json.loads((r/'frame-orientation-candidate.json').read_text())
spec=json.loads((r/'nozzle-connection-revision.json').read_text())
a,e=map(math.radians,[camera['cameraAzimuthDegrees'],camera['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
scale=760.
# Depth is an explicitly inferred progression; its absolute offset is still free.
depths=[0,.022,.049,.075]
centers=[]
for part,depth in zip(spec['plannedParts'],depths):
 x,y=part['projectedCenter'];centers.append(R*((x-891)/scale)+U*((935-y)/scale)+D*depth)
sections=[]
for i,(center,part) in enumerate(zip(centers,spec['plannedParts'])):
 tangent=centers[min(i+1,3)]-centers[max(0,i-1)];tangent/=np.linalg.norm(tangent)
 side=np.cross(tangent,D);side/=np.linalg.norm(side);other=np.cross(tangent,side)
 radius=part['projectedDiameter']/2/scale
 ring=[center+radius*(math.cos(t)*side+math.sin(t)*other) for t in np.linspace(0,math.tau,48,endpoint=False)]
 sections.append({'id':part['id'],'center':center.tolist(),'tangent':tangent.tolist(),'radius':radius,'ringVertices':[v.tolist() for v in ring]})
def project(v):v=np.array(v);return [891+scale*float(R@v),935-scale*float(U@v)]
im=Image.new('RGB',(900,700),'#f4f4ef');draw=ImageDraw.Draw(im)
def display(v):x,y=project(v);return ((x-495)*5+40,(y-480)*5+50)
for section in sections:
 ring=section['ringVertices'];draw.line([display(v) for v in ring+[ring[0]]],fill='#186d9b',width=2)
for i in range(3):
 for j in range(0,48,6):draw.line([display(sections[i]['ringVertices'][j]),display(sections[i+1]['ringVertices'][j])],fill='#b7692b',width=2)
draw.text((15,15),'Nozzle cross-section plan: projected 3D rings and connections',fill='black')
draw.text((15,665),'Depth inferred. Not a clay render or fidelity pass.',fill='black')
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview');out.mkdir(parents=True,exist_ok=True);im.save(out/'nozzle-sweep-plan.png')
report={'status':'NUMERIC_SWEEP_CANDIDATE_REQUIRES_SILHOUETTE_REVIEW','camera':camera,'sections':sections,'depthOffsets':depths,'depthAuthority':'Inferred, not recovered from image; absolute attachment depth unresolved','remaining':['Fit terminal tangent to corrected flange face','Check silhouette against original neck','Anchor throat and flange in full assembly'],'blenderAllowed':False}
(r/'nozzle-sweep-plan.json').write_text(json.dumps(report,indent=2))
print('Wrote numeric sweep and projection diagram; no Blender invoked.')
