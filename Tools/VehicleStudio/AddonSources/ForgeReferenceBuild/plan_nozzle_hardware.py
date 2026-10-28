"""Separate throat, coupling and flange geometry in the existing neck frame.
Dimensions inferred from the approved view; these are not fidelity-approved.
"""
import json, math
from pathlib import Path
import numpy as np
r=Path(__file__).parent
neck=json.loads((r/'nozzle-smooth-plan.json').read_text())
controls=np.array(neck['controlPoints'])
start,end=controls[0],controls[-1]
axis=np.array(neck['terminalNormal']);axis/=np.linalg.norm(axis)
def basis(axis):
 up=np.array([0.,0.,1.]);up-=axis*(up@axis);up/=np.linalg.norm(up)
 return np.cross(up,axis),up
parts=[]
def revolve(name,center,axis,profile):
 side,up=basis(axis);vertices=[];n=64
 for depth,radius in profile:
  for a in np.linspace(0,math.tau,n,endpoint=False):
   vertices.append((center+depth*axis+radius*(side*math.cos(a)+up*math.sin(a))).tolist())
 faces=[]
 for i in range(len(profile)):
  for j in range(n):faces.append([i*n+j,i*n+(j+1)%n,((i+1)%len(profile))*n+(j+1)%n,((i+1)%len(profile))*n+j])
 parts.append({'id':name,'vertices':vertices,'faces':faces,'axis':axis.tolist(),'center':center.tolist(),'profileDepthRadius':profile})
# Ring bore slightly smaller than the existing neck radius: no open seam.
revolve('furnace_flange',end,axis,[(-.005,.029),(-.005,.078),(0,.084),(.020,.084),(.025,.077),(.025,.029)])
revolve('neck_coupling',end,axis,[(-.031,.029),(-.031,.039),(-.025,.044),(-.010,.044),(-.005,.039),(-.005,.029)])
side,up=basis(axis)
for i,a in enumerate(np.linspace(0,math.tau,8,endpoint=False)):
 center=end+.064*(side*math.cos(a)+up*math.sin(a))
 revolve('flange_bolt_%02d'%i,center,-axis,[(.003,.004),(.003,.008),(.009,.008),(.014,.006),(.016,.001)])
throat_axis=controls[1]-controls[0];throat_axis/=np.linalg.norm(throat_axis)
side,up=basis(throat_axis)
center=start-throat_axis*.021
# Rounded rectangle, distinct from the cylindrical neck and flexible envelope.
width,height,corner=.081,.154,.009
outline=[]
for cx,cy,angle in [(width/2-corner,height/2-corner,0),(-width/2+corner,height/2-corner,90),(-width/2+corner,-height/2+corner,180),(width/2-corner,-height/2+corner,270)]:
 for a in np.linspace(angle,angle+90,5):
  a=math.radians(a);outline.append((cx+corner*math.cos(a),cy+corner*math.sin(a)))
vertices=[]
for depth in [-.019,.021]:
 for x,y in outline:vertices.append((center+depth*throat_axis+side*x+up*y).tolist())
n=len(outline);faces=[[j,(j+1)%n,(j+1)%n+n,j+n] for j in range(n)]
faces.extend([list(range(n-1,-1,-1)),list(range(n,2*n))])
parts.append({'id':'rectangular_throat_block','vertices':vertices,'faces':faces,'center':center.tolist(),'axis':throat_axis.tolist(),'dimensions':[width,height,.04]})
report={'status':'CONNECTION_HARDWARE_DRAFT_NOT_FIDELITY_PASS','parts':parts,
 'observations':['Separate rectangular throat, rounded neck, coupling and broad flange visible in reference'],
 'inferred':['Flange thickness .03, throat dimensions and hidden attachments','Eight bolt positions; count and occlusion require reference review'],
 'remaining':['No flange-to-stone contact test','Throat-to-envelope overlap unvalidated','Fastener pole vertices require welding','Image dimensions and flange face normal remain provisional'],
 'blenderAllowed':False}
(r/'nozzle-hardware-plan.json').write_text(json.dumps(report))
print('Planned',len(parts),'connection parts')
