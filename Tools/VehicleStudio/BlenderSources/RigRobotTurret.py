"""Split the supplied Tripo mesh into stationary, yaw and whole-body pitch parts.

Works on a staged copy. Source UVs are interpolated by bmesh at separation cuts.
The review output must be inspected before this candidate is integrated in Unity.
"""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector, Matrix

P = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RobotTurret_20261019')
OUT = P / 'Output'
bpy.ops.wm.open_mainfile(filepath=str(OUT / 'source-review.blend'))
source = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
source.name = 'SOURCE_283376'
material = source.data.materials[0]
cap_material = bpy.data.materials.new('Concealed bearing steel')
cap_material.use_nodes=True
cap_bsdf=cap_material.node_tree.nodes.get('Principled BSDF')
cap_bsdf.inputs['Base Color'].default_value=(.09,.07,.05,1)
cap_bsdf.inputs['Metallic'].default_value=.8
cap_bsdf.inputs['Roughness'].default_value=.5
CENTER = Vector((.000328, .106773, 0))
PIVOT = Vector((.000328, .1397, .529))

def from_faces(bm, faces, name):
    indices = sorted({v.index for f in faces for v in f.verts})
    mapping = {old: new for new, old in enumerate(indices)}
    bm.verts.ensure_lookup_table()
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([bm.verts[i].co[:] for i in indices], [],
                     [[mapping[v.index] for v in f.verts] for f in faces])
    uv_source = bm.loops.layers.uv.active
    uv = mesh.uv_layers.new(name='UVMap')
    for dst, src in zip(mesh.polygons, faces):
        dst.use_smooth = src.smooth
        for li, loop in zip(dst.loop_indices, src.loops):
            uv.data[li].uv = loop[uv_source].uv if uv_source else (0, 0)
    mesh.materials.append(material)
    mesh.update()
    return mesh

def bisect(mesh, point, normal, prefix):
    bm = bmesh.new(); bm.from_mesh(mesh)
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                          plane_co=point, plane_no=normal, dist=1e-7)
    bm.verts.index_update(); bm.verts.ensure_lookup_table()
    n, p = Vector(normal), Vector(point)
    neg, pos = [], []
    for f in bm.faces:
        (neg if (f.calc_center_median()-p).dot(n) < 0 else pos).append(f)
    result = (from_faces(bm, neg, prefix+'Negative'), from_faces(bm, pos, prefix+'Positive'))
    bm.free(); return result

def combine(meshes, name):
    bm = bmesh.new()
    for mesh in meshes: bm.from_mesh(mesh)
    # Rejoin temporary partition planes; only the real component boundaries remain.
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    # Cap only newly cut bearing cross-sections. The original model has open
    # boundaries in the central ring; filling those would invent a solid disk.
    edges = [e for e in bm.edges if e.is_boundary and
             all(abs(abs(v.co.x)-.286)<2e-6 and v.co.z>.32 for v in e.verts)]
    caps = bmesh.ops.holes_fill(bm, edges=edges, sides=0).get('faces', [])
    for face in caps: face.smooth = False; face.material_index=1
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mesh = bpy.data.meshes.new(name); bm.to_mesh(mesh); bm.free()
    mesh.materials.clear(); mesh.materials.append(material); mesh.materials.append(cap_material)
    return mesh, len(caps)

# The supplied pedestal is already a distinct connected component.
bm = bmesh.new(); bm.from_mesh(source.data)
bm.verts.ensure_lookup_table(); bm.verts.index_update()
seen = set(); islands = []
for v in bm.verts:
    if v.index in seen: continue
    stack = [v]; group = []; seen.add(v.index)
    while stack:
        cur = stack.pop(); group.append(cur)
        for e in cur.link_edges:
            other = e.other_vert(cur)
            if other.index not in seen: seen.add(other.index); stack.append(other)
    islands.append(group)
pedestal_ids = {v.index for g in islands if max(v.co.z for v in g) < .16 for v in g}
base = from_faces(bm, [f for f in bm.faces if f.verts[0].index in pedestal_ids], 'Pedestal')
main = from_faces(bm, [f for f in bm.faces if f.verts[0].index not in pedestal_ids], 'Main')
bm.free()

lower, upper = bisect(main, (0,0,.32), (0,0,1), 'VerticalPartition')
left, middle_right = bisect(upper, (-.286,0,0), (1,0,0), 'LeftBearing')
middle, right = bisect(middle_right, (.286,0,0), (1,0,0), 'RightBearing')

# Split the stationary inner bearing from the rotating outer C-ring at the
# radial gap. 64 planes interpolate UVs instead of producing a jagged face cut.
bm = bmesh.new(); bm.from_mesh(lower)
radius = .287
for i in range(64):
    a = math.tau*i/64; n = Vector((math.cos(a),math.sin(a),0))
    bmesh.ops.bisect_plane(bm, geom=list(bm.verts)+list(bm.edges)+list(bm.faces),
                          plane_co=CENTER+n*radius, plane_no=n, dist=1e-7)
bm.verts.index_update(); bm.verts.ensure_lookup_table()
inside, outside = [], []
for f in bm.faces:
    p = f.calc_center_median()-CENTER
    (inside if max(p.x*math.cos(math.tau*i/64)+p.y*math.sin(math.tau*i/64) for i in range(64)) <= radius+1e-7 else outside).append(f)
inner = from_faces(bm, inside, 'InnerBearing'); outer = from_faces(bm, outside, 'OuterRing')
bm.free()

def empty(name, position, parent=None):
    o = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(o)
    if parent: o.parent = parent
    o.matrix_world = Matrix.Translation(position)
    bpy.context.view_layer.update()
    return o

root = empty('RustyRobotTurret', CENTER)
bpy.context.view_layer.update()
yaw = empty('TurretYaw', CENTER, root)
bpy.context.view_layer.update()
pitch = empty('TurretPitch', PIVOT, yaw)
bpy.context.view_layer.update()
parts = []
report = {'source_triangles':283376, 'coordinates':'Blender Z up; barrel forward -Y',
          'yaw_axis':list(CENTER), 'pitch_axis':list(PIVOT), 'parts':[],
          'status':'segmentation candidate; not deployed; clearance not yet measured'}
for name, meshes, parent in [('FixedSupport',[base,inner],root), ('YawCradle',[left,right,outer],yaw), ('PitchBody',[middle],pitch)]:
    mesh, caps = combine(meshes, name)
    obj = bpy.data.objects.new(name,mesh); bpy.context.scene.collection.objects.link(obj)
    obj.parent = parent; obj.matrix_parent_inverse = parent.matrix_world.inverted()
    mesh.calc_loop_triangles(); parts.append(obj)
    report['parts'].append({'name':name,'triangles':len(mesh.loop_triangles),'cap_faces':caps})
source.hide_render = True; source.hide_set(True)
bpy.context.view_layer.update()
OUT.joinpath('rig-separation.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'RobotTurret_Separated.blend'))

sc = bpy.context.scene; sc.cycles.samples=24
cam = sc.camera; target=Vector((0,0,.42))
for label, eye, angle in [('rig-level',(-2,-3,1.8),0), ('rig-pitch-up-side',(-3,0,.42),-45), ('rig-pitch-down-side',(-3,0,.42),30)]:
    pitch.rotation_euler.x=math.radians(angle); bpy.context.view_layer.update()
    cam.location=eye; cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler()
    sc.render.filepath=str(OUT/(label+'.png')); bpy.ops.render.render(write_still=True)
pitch.rotation_euler.x=0
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'RobotTurret_Separated.blend'))
