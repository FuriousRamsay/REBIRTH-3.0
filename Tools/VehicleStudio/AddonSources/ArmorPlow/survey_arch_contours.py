import bpy,json,numpy as np
from pathlib import Path
from mathutils import Vector
from mathutils.bvhtree import BVHTree
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017')
bpy.ops.wm.open_mainfile(filepath=str(P/'ConnectedGrilles/camper-connected-grilles.blend'))
b=BVHTree.FromObject(bpy.data.objects['body'],bpy.context.evaluated_depsgraph_get())
out={}
for name,cz in [('front',1.289),('rear',-1.620)]:
    points=[]
    for z in np.linspace(cz-.65,cz+.65,66):
        found=None
        for y in np.arange(.40,1.15,.005):
            p,n,_,_=b.ray_cast(Vector((2,float(y),float(z))),Vector((-1,0,0)),1.32)
            if p is not None and abs(n.x)>.5:found=float(y);break
        points.append([float(z),found])
    out[name]=points
(P/'PlateSamples/actual-arch-survey.json').write_text(json.dumps(out,indent=2));print(out)
