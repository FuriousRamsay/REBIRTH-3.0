"""Continuous bands on upright shell, independent of individual stone noise."""
import json,math
from pathlib import Path
import numpy as np
from audit_upright_surface import hit,R,U,D
r=Path(__file__).parent;traces=json.loads((r/'harness-reference-recheck.json').read_text())['records']
parts=[];unsupported=[];edge_overhang=[]
shell=json.loads((r/'upright-furnace-envelope.json').read_text());shell_vertices=np.array(shell['vertices'])
shell_pixels=np.column_stack([891+760*(shell_vertices@R),935-760*(shell_vertices@U)])
shell_center=np.array(shell['parameters'][:2])
def sample(p):
 h=hit(p)
 if h:return np.array(h['position']),np.array(h['normal'])
 # A strap edge can overhang the projected shell. Bound the extension explicitly.
 distances=np.linalg.norm(shell_pixels-p,axis=1);i=int(np.argmin(distances))
 if distances[i]<=16:
  q=shell_pixels[i]*.999+shell_center*.001;h=hit(q)
  if h:
   extension=R*((p[0]-q[0])/760)-U*((p[1]-q[1])/760)
   edge_overhang.append({'pixel':p.tolist(),'extension':float(np.linalg.norm(extension))})
   return np.array(h['position'])+extension,np.array(h['normal'])
 # Larger misses require revision, not unconstrained continuation of the mesh.
 return None
for trace in traces:
 if 'pathPixels' not in trace:continue
 path=np.array(trace['pathPixels'],float);samples=[]
 for i in range(len(path)-1):
  p0=path[max(0,i-1)];p1=path[i];p2=path[i+1];p3=path[min(len(path)-1,i+2)]
  for t in np.linspace(0,1,12,endpoint=False):
   samples.append(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t))
 samples.append(path[-1]);vertices=[];bad=[]
 clearance=.039+(.018 if trace['id']=='left_front_strap' else 0)
 # Rounded rectangular section with explicit edge chamfers, not paper strips.
 section=[(-1,0),(-1,.007),(-.94,.012),(.94,.012),(1,.007),(1,0),(.94,-.003),(-.94,-.003)]
 for i,p in enumerate(samples):
  tangent=samples[min(i+1,len(samples)-1)]-samples[max(0,i-1)]
  cross=np.array([-tangent[1],tangent[0]]);cross/=np.linalg.norm(cross)
  for width,z in section:
   q=p+cross*width*trace['widthPixels']/2;s=sample(q)
   if s is None:bad.append(q.tolist());vertices.append([0.,0.,0.])
   else:
    position,normal=s;vertices.append((position+normal*(clearance+z)).tolist())
 if bad:
  unsupported.append({'id':trace['id'],'samples':bad});continue
 faces=[];n=len(section)
 for i in range(len(samples)-1):
  for j in range(n):faces.append([i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j])
 faces.extend([list(range(n-1,-1,-1)),list(range(len(vertices)-n,len(vertices)))])
 parts.append({'id':trace['id'],'vertices':vertices,'faces':faces,'widthPixels':trace['widthPixels'],'clearance':clearance,'sourcePath':trace['pathPixels']})
# Prominent rivets use source-image locations; unsupported positions are explicit.
heads=json.loads((r/'hardware-reference-records.json').read_text())['fasteners']
for head in heads:
 if head['parent'] not in [p['id'] for p in parts]:continue
 h=sample(np.array(head['center'],float))
 if h is None:unsupported.append({'id':head['id'],'samples':[head['center']]});continue
 position,normal=h;position+=normal*(.069 if head['parent']=='left_front_strap' else .051)
 side=np.cross(normal,D)
 if np.linalg.norm(side)<1e-6:side=np.cross(normal,U)
 side/=np.linalg.norm(side);other=np.cross(normal,side)
 radius=head['diameter']/1520;n=48;vertices=[]
 profile=[(0,radius*.88),(.004,radius),(.010,radius*.90),(.018,radius*.60),(.021,.001)]
 for z,rad in profile:
  for t in np.linspace(0,math.tau,n,endpoint=False):vertices.append((position+normal*z+rad*(math.cos(t)*side+math.sin(t)*other)).tolist())
 faces=[]
 for i in range(len(profile)-1):
  for j in range(n):faces.append([i*n+j,i*n+(j+1)%n,(i+1)*n+(j+1)%n,(i+1)*n+j])
 faces.extend([list(range(n-1,-1,-1)),list(range(len(vertices)-n,len(vertices)))])
 parts.append({'id':head['id'],'vertices':vertices,'faces':faces,'parent':head['parent'],'referenceCenter':head['center']})
report={'status':'UPRIGHT_HARNESS_PARTIAL_NOT_FIDELITY_PASS','parts':parts,'unsupported':unsupported,'inferredEdgeOverhang':edge_overhang,
 'limitations':['Unsupported bands omitted explicitly; must resolve','Stone clearance not collision-tested','Rivet count includes only supported prominent inventory','Foot/lap plates and small fasteners missing','Section offsets and camera measurements provisional'],
 'blenderAllowed':False}
(r/'upright-harness-plan.json').write_text(json.dumps(report))
print('Generated',len(parts),'parts; unsupported:',[(p['id'],len(p['samples'])) for p in unsupported])
