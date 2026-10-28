"""Crucible retention assembly; dimensions provisional, no fidelity approval."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent
cup=next(p for p in json.loads((r/'round-components-plan.json').read_text())['parts'] if p['id']=='crucible')
center=np.array(cup['center']);camera=json.loads((r/'bellows-frame-plan.json').read_text())
a=math.radians(camera['cameraAzimuthDegrees']);right=np.array([math.cos(a),-math.sin(a),0.]);front=np.array([-math.sin(a),-math.cos(a),0.]);up=np.array([0.,0.,1.])
parts=[]
def ring(name,origin,axis,profile,n=96):
 helper=up if abs(axis@up)<.9 else right
 s=np.cross(axis,helper);s/=np.linalg.norm(s);t=np.cross(axis,s)
 vertices=[(origin+axis*z+radius*(s*math.cos(angle)+t*math.sin(angle))).tolist() for z,radius in profile for angle in np.linspace(0,math.tau,n,endpoint=False)]
 faces=[]
 for i in range(len(profile)):
  for j in range(n):faces.append([i*n+j,i*n+(j+1)%n,((i+1)%len(profile))*n+(j+1)%n,((i+1)%len(profile))*n+j])
 parts.append({'id':name,'vertices':vertices,'faces':faces,'origin':origin.tolist(),'axis':axis.tolist(),'profile':profile})
for name,z,radius in [('upper_hoop',-.091,.141),('lower_hoop',-.292,.136)]:
 ring(name,center,up,[(z-.016,radius),(z-.017,radius+.010),(z+.017,radius+.010),(z+.016,radius)])
directions=[('left_retainer',-.93*right+.3676*front),('right_retainer',.93*right+.3676*front)]
profile=[(-.352,.148),(-.333,.158),(-.075,.161),(-.040,.174),(.004,.174),(.011,.151)]
for name,direction in directions:
 direction/=np.linalg.norm(direction);side=np.cross(up,direction)
 vertices=[]
 for z,radius in profile:
  for radial in [0,.010]:
   for sign in [-1,1]:vertices.append((center+z*up+(radius+radial)*direction+side*sign*.014).tolist())
 faces=[]
 for i in range(len(profile)-1):
  for j,k in [(0,1),(1,3),(3,2),(2,0)]:faces.append([4*i+j,4*i+k,4*(i+1)+k,4*(i+1)+j])
 faces.extend([[0,2,3,1],[len(vertices)-4,len(vertices)-3,len(vertices)-1,len(vertices)-2]])
 parts.append({'id':name,'vertices':vertices,'faces':faces,'width':.028,'thickness':.010,'radialDirection':direction.tolist(),'heightRadiusProfile':profile})
 for i,z in enumerate([-.091,-.292]):
  ring(name+'_rivet_'+str(i),center+z*up+.173*direction,direction,[(0,.012),(.004,.012),(.011,.009),(.015,.001)],32)
# Visible small rivets on the exposed front arcs of both hoops.
for name,z,radius in [('upper',-.091,.151),('lower',-.292,.146)]:
 for i,angle in enumerate([-.65,.05,.72]):
  direction=front*math.cos(angle)+right*math.sin(angle)
  ring(name+'_hoop_rivet_'+str(i),center+up*z+radius*direction,direction,[(0,.009),(.003,.009),(.007,.006),(.009,.001)],32)
direction=directions[1][1];direction/=np.linalg.norm(direction)
origin=center+up*(-.091)+direction*.174
ring('side_link_pivot',origin,direction,[(0,.022),(.008,.024),(.020,.020),(.025,.012),(.029,.001)],48)
ring('side_link_shaft',origin+direction*.004,right,[(0,.014),(.083,.014),(.083,.010),(0,.010)],48)
report={'status':'CRUCIBLE_RETENTION_PLAN_NOT_FIDELITY_PASS','parts':parts,
 'observed':['Two hoops','Upright retainers extending above rim and below vessel','Riveted side pivot and short link'],
 'inferred':['Rear circumference and hidden fixation','Retainer radial positions from approximate screen silhouette','Link endpoint not yet fitted to furnace bracket'],
 'remaining':['Reference silhouette review','Contact/collision check at rim and hoop crossings','Fastener pole welding','Bottom support geometry'],
 'blenderAllowed':False}
(r/'crucible-hardware-plan.json').write_text(json.dumps(report))
print('Planned',len(parts),'crucible hardware parts')
