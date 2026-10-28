"""Refit observed stones to upright surface; preserve relief attempt separately."""
import json,sys
from pathlib import Path
import numpy as np
from audit_upright_surface import hit,R,U,D
r=Path(__file__).parent
parts=[];unresolved=[];extensions=[]
profiles=json.loads((r/'arch-face-profiles.json').read_text())['profiles']
bulk='--bulk-stones' in sys.argv
hearth=json.loads((r/'upright-hearth-plan.json').read_text());arch_normal=np.array(hearth['outwardAxis'])
for file,key,contour in [('outer-stone-contours.json','stones','contour'),('masonry-gap-records.json','stones','contour'),('arch-stone-records.json','records','contourPixels')]:
 for record in json.loads((r/file).read_text())[key]:
  outline=np.array(record[contour],float);center=outline.mean(0);anchor=hit(center)
  if anchor is None:
   unresolved.append(record['id']);continue
  n=len(outline);vertices=[]
  def surface(p):
   if file=='arch-stone-records.json':
    # Arch voussoirs face the chamber entrance, not the dome's radial surface.
    ray=R*((p[0]-891)/760)+U*((935-p[1])/760)
    position=np.array(anchor['position'])
    distance=(arch_normal@(position-ray))/(arch_normal@D)
    return ray+D*distance,arch_normal
   h=hit(p)
   if h:return np.array(h['position']),np.array(h['normal'])
   # Limited extension past fitted silhouette along its local screen tangent.
   lo,hi=0.,1.
   for _ in range(16):
    mid=(lo+hi)/2
    if hit(center+(p-center)*mid):lo=mid
    else:hi=mid
   q=center+(p-center)*lo;h=hit(q)
   delta=R*((p[0]-q[0])/760)-U*((p[1]-q[1])/760)
   extensions.append({'id':record['id'],'pixel':p.tolist(),'distance':float(np.linalg.norm(delta))})
   return np.array(h['position'])+delta,np.array(h['normal'])
  profile=profiles.get(record['id'])
  if profile:
   assert len(profile['inset'])==n and len(profile['relief'])==n
  # Thickness follows the local shell normal, never the comparison camera ray.
  rings=[(.85,-.14),(1.,-.015),(.94,.018),(.82,.025)]
  if bulk and not profile:rings=[(.85,-.14),(1.,-.008),(.93,.048),(.64,.067)]
  for shrink,offset in rings:
   for j,p in enumerate(outline):
    local_shrink,local_offset=shrink,offset
    if profile and shrink==.94:
     local_shrink=profile['inset'][j];local_offset=profile['relief'][j]
    elif profile and shrink==.82:
     local_shrink=profile['inset'][j]*.48;local_offset=(profile['relief'][j]+profile['centerRelief'])/2
    face_center=np.array(profile['faceCenterPixel']) if profile else center
    q=face_center+(p-face_center)*local_shrink;position,normal=surface(q)
    vertices.append((position+normal*local_offset).tolist())
  position,normal=surface(np.array(profile['faceCenterPixel']) if profile else center)
  vertices.append((position+normal*(profile['centerRelief'] if profile else (.070 if bulk else .026))).tolist());faces=[]
  for row in range(3):
   for j in range(n):
    k=(j+1)%n;faces.extend([[row*n+j,row*n+k,(row+1)*n+k],[row*n+j,(row+1)*n+k,(row+1)*n+j]])
  for j in range(n):faces.append([3*n+j,3*n+(j+1)%n,4*n])
  faces.append(list(range(n-1,-1,-1)))
  parts.append({'id':record['id'],'vertices':vertices,'faces':faces,'surfaceAnchor':anchor,'nominalProfileDepth':.14+(profile['centerRelief'] if profile else (.070 if bulk else .026)),'sourceContour':record[contour]})
report={'status':'UPRIGHT_MASONRY_PARTIAL_NOT_FIDELITY_PASS','parts':parts,'unresolvedStoneCenters':unresolved,'silhouetteExtensions':extensions,
 'limitations':['Only visible traced stones; rear/side masonry incomplete','No physical hearth wall or lining yet','Neighbor gaps/overlaps unvalidated','Surface extensions inferred at silhouette','Uniform depth/profile still require individual stone refinement'],
 'blenderAllowed':False}
(r/('bulk-masonry-plan.json' if bulk else 'upright-masonry-plan.json')).write_text(json.dumps(report))
print('Refitted',len(parts),'stones; unresolved:',unresolved,'; extrapolated samples:',len(extensions))
