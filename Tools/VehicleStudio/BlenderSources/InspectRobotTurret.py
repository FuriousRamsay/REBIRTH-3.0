import bpy,json,collections,math
from pathlib import Path
from mathutils import Vector
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(next((P/'Input').glob('*.fbx'))))
report={'objects':[]}
for o in list(bpy.context.scene.objects):
 if o.type!='MESH':continue
 bpy.context.view_layer.objects.active=o;o.select_set(True)
 bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 m=o.data;m.calc_loop_triangles();parent=list(range(len(m.vertices)))
 def find(a):
  while parent[a]!=a:parent[a]=parent[parent[a]];a=parent[a]
  return a
 for e in m.edges:
  a,b=map(find,e.vertices);parent[a]=b
 groups=collections.defaultdict(list)
 for v in m.vertices:groups[find(v.index)].append(v.index)
 components=[]
 for inds in groups.values():
  pts=[m.vertices[i].co for i in inds]
  components.append({'n':len(inds),'min':[min(p[a] for p in pts) for a in range(3)],'max':[max(p[a] for p in pts) for a in range(3)]})
 report['objects'].append({'name':o.name,'triangles':len(m.loop_triangles),'vertices':len(m.vertices),'bounds':[list(o.dimensions)],'components':sorted(components,key=lambda c:-c['n'])[:100]})
(P/'Output/source-survey.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(P/'Output/source.blend'))
