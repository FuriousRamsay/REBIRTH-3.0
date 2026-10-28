import bpy,json
from pathlib import Path
from mathutils.bvhtree import BVHTree
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');out=root/'CrownLayerAudit_v38';out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(root/'BellowsLeatherNormals_v35/leather-normals-clay.blend'))
names=['harness_left_front_strap','harness_right_front_strap','harness_crown_diagonal_left','harness_crown_cross_band'];deps=bpy.context.evaluated_depsgraph_get();trees={};points={};mods=[]
for name in names:
    obj=bpy.data.objects[name];ev=obj.evaluated_get(deps);mesh=ev.to_mesh();vertices=[obj.matrix_world@v.co for v in mesh.vertices];faces=[list(p.vertices) for p in mesh.polygons]
    trees[name]=BVHTree.FromPolygons(vertices,faces);points[name]=vertices
    mods.append({'object':name,'modifiers':[{'name':m.name,'type':m.type,'offset':getattr(m,'offset',None),'thickness':getattr(m,'thickness',None),'target':getattr(getattr(m,'target',None),'name',None)} for m in obj.modifiers]});ev.to_mesh_clear()
pairs=[]
for a in names[2:]:
    for b in names[:2]:
        samples=[]
        for point in points[a]:
            near,n,index,d=trees[b].find_nearest(point)
            if near is not None and d<.015:samples.append({'position':list(point),'distanceMeters':d,'signedNormalDistanceMeters':(point-near).dot(n)})
        pairs.append({'upperCandidate':a,'other':b,'nearSamples':len(samples),'minimumDistanceMeters':min((s['distanceMeters'] for s in samples),default=None),'negativeNormalSamples':sum(s['signedNormalDistanceMeters']<-.0005 for s in samples),'samples':samples})
report={'source':'BellowsLeatherNormals_v35/leather-normals-clay.blend','modifiers':mods,'pairs':pairs,'limits':'Nearest-surface distances and signed normals screen for contact. They do not establish full mesh intersection or source fidelity. Visual close-up required.'}
path=out/'layer-audit.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));print(json.dumps({'modifiers':mods,'pairs':[{k:v for k,v in pair.items() if k!='samples'} for pair in pairs]}))
