"""Numerical vessel planning, no Blender invocation and no old geometry input."""
import math,json
from pathlib import Path
import numpy as np
root=Path(__file__).parent
az=math.radians(30);el=math.radians(24);scale=760.;cx=891.;cy=935.
R=np.array([math.cos(az),-math.sin(az),0.])
U=np.array([math.sin(az)*math.sin(el),math.cos(az)*math.sin(el),math.cos(el)])
def project(p):return [cx+scale*float(R@p),cy-scale*float(U@p)]
# Base is supported slightly above ground. Its plane depth is solved from
# the projected base center, not copied from the old arbitrary flat layout.
base_z=.045;base_pixel=np.array([525.,886.])
A=np.array([R[:2],U[:2]])
b=np.array([(base_pixel[0]-cx)/scale,(cy-base_pixel[1])/scale-U[2]*base_z])
x,y=np.linalg.solve(A,b)
rim_pixel=[520.,670.]
height=(base_pixel[1]-rim_pixel[1])/(scale*math.cos(el))
center=np.array([x,y,base_z+height])
radius=100/scale;wall=.024
outer=[];inner=[]
for i in range(64):
 t=math.tau*i/64
 outer.append(project(center+np.array([radius*math.cos(t),radius*math.sin(t),0.])))
 inner.append(project(center+np.array([(radius-wall)*math.cos(t),(radius-wall)*math.sin(t),0.])))
report={'status':'CRUCIBLE_VOLUME_HYPOTHESIS_NOT_VALIDATED',
 'positionBaseCenter':[float(x),float(y),base_z],'rotationDegrees':[0,0,0],
 'height':height,'outerTopRadius':radius,'outerBottomRadius':radius*.90,'wallThickness':wall,'floorThickness':.025,
 'profile':'Closed tapered vessel with inward offset wall and solid floor; open top. Rim chips require separate restrained contour specification.',
 'camera':{'azimuthDegrees':30,'elevationDegrees':24,'pixelsPerUnit':scale,'imageOrigin':[cx,cy]},
 'projectedRimCenter':project(center),'targetRimCenter':rim_pixel,
 'rimCenterErrorPixels':math.dist(project(center),rim_pixel),
 'projectedOuterRim':outer,'projectedInnerRim':inner,
 'authority':'Dimensions and pose are hypotheses based on observed silhouette and assumed support height. This is a solid-volume specification, not the flat contour reconstruction.',
 'remaining':['Perspective/shared camera validation','Independent outline comparison','Retaining hoop/strap and mounting attachment dimensions','Final ground relationship to furnace feet'],
 'blenderAllowed':False}
(root/'crucible-volume-plan.json').write_text(json.dumps(report,indent=2))
print(json.dumps({k:v for k,v in report.items() if not k.startswith('projected')},indent=2))
