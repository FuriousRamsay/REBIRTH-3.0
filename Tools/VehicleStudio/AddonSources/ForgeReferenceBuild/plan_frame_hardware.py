"""Turn planned bearing dimensions into closed revolved geometry."""
import json,math
from pathlib import Path
import numpy as np
r=Path(__file__).parent;frame=json.loads((r/'bellows-frame-plan.json').read_text());parts=[]
for bearing in frame['records']:
 if not bearing['id'].startswith('bearing_'):continue
 index=int(bearing['id'].split('_')[-1]);sign=-1 if index in [0,2] else 1
 center=np.array(bearing['center']);center[0]+=sign*.030
 # Closed radial profile: back face, washer rim, boss shoulder, rounded cap, axis.
 profile=[(0,0),(0,.028),(.008,.028),(.008,.020),(.018,.020)]
 for angle in np.linspace(0,math.pi/2,12)[1:]:profile.append((.018+.010*math.sin(angle),.020*math.cos(angle)))
 vertices=[]
 for axial,radius in profile:
  for theta in np.linspace(0,math.tau,48,endpoint=False):vertices.append((center+np.array([sign*axial,radius*math.cos(theta),radius*math.sin(theta)])).tolist())
 faces=[]
 for i in range(len(profile)-1):
  for j in range(48):faces.append([i*48+j,i*48+(j+1)%48,(i+1)*48+(j+1)%48,(i+1)*48+j])
 # Pole rings coincide geometrically; weld them when creating the final mesh.
 parts.append({'id':bearing['id'],'vertices':vertices,'faces':faces,'axis':[sign,0,0],'profile':profile,'poleWeldRequired':True})
report={'status':'BEARING_FORM_PLAN_REQUIRES_VISUAL_CHECK','parts':parts,'limitations':['Nominal post face offset; inspect fit with beveled post','Pole vertices must be welded before final mesh validation','Reference head shape and plate outline remain to verify'],'blenderAllowed':False}
(r/'frame-hardware-plan.json').write_text(json.dumps(report));print('Created four washer/boss/dome profiles.')
