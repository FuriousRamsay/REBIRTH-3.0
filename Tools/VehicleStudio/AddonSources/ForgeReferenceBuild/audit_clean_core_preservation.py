import bpy,json,hashlib,struct
from pathlib import Path
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/ApprovedTextureBaseline');records=[]
for source in ('SmallCoreLoopRepair_v53/small-loop-repair-clay.blend','CleanMortarCore_v55/clean-core-clay.blend'):
    bpy.ops.wm.open_mainfile(filepath=str(root/source));deps=bpy.context.evaluated_depsgraph_get();objects={}
    for obj in bpy.context.scene.objects:
        if obj.hide_render or obj.type not in ('MESH','CURVE') or obj.name=='furnace_core':continue
        ev=obj.evaluated_get(deps);mesh=ev.to_mesh();h=hashlib.sha256()
        for vertex in mesh.vertices:h.update(struct.pack('<3f',*(obj.matrix_world@vertex.co)))
        for polygon in mesh.polygons:h.update(struct.pack('<I',len(polygon.vertices)));h.update(struct.pack('<'+'I'*len(polygon.vertices),*polygon.vertices))
        objects[obj.name]={'hash':h.hexdigest(),'vertices':len(mesh.vertices),'polygons':len(mesh.polygons)};ev.to_mesh_clear()
    records.append({'source':source,'objects':objects})
before,after=[r['objects'] for r in records];changed=[name for name in before if name not in after or before[name]!=after[name]];added=[name for name in after if name not in before]
report={'sources':records,'changedNonCoreObjects':changed,'addedVisibleNonCoreObjects':added,'scope':'Evaluated world geometry hash, including curves; excludes furnace_core and render-hidden backups. No appearance fidelity assertion.'}
path=root/'CleanMortarCore_v55/preservation-audit.json';assert not path.exists();path.write_text(json.dumps(report,indent=2));print(json.dumps({'verifiedNonCoreObjects':len(before),'changed':changed,'added':added}));assert not changed and not added
