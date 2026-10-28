"""Apply the existing plough material family to approved plate samples.
Review render only: preserve geometry and installed resources.
"""
import bpy,ast,math,json,sys,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.geometry import tessellate_polygon
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017');O=P/'PlateSamples/TexturedReview';O.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'PlateSamples/reference-plate-samples.blend'))
detail_image=bpy.data.images.load(str(Path(__file__).parent/'ReferenceSurfaceDetail_20261017/dark-steel-detail.png'),check_existing=True)
# Same layer construction, steel detail, pitting, metallic response and paint
# as the plough. Paint remnants use panel-local Z/Y placement on the truck.
paint_shapes=[
    [(-1.39,1.10),(-1.53,1.10),(-1.87,.91),(-1.72,.91)],
    [(-.25,.92),(-.13,.92),(.10,.65),(-.02,.62)],
    [(1.09,1.10),(1.31,1.10),(1.53,.98),(1.41,.94)],
    [(-2.65,1.11),(-2.53,1.11),(-2.53,.96),(-2.62,.98)]]
source=(Path(__file__).parent/'apply_reference_finish.py').read_text();tree=ast.parse(source)
fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='build_material')
code=ast.unparse(fn)
code=code.replace("position = geo.outputs['Position']", "raw = node('ShaderNodeSeparateXYZ'); l.new(geo.outputs['Position'],raw.inputs[0]); adapted = node('ShaderNodeCombineXYZ'); l.new(raw.outputs['Z'],adapted.inputs['X']); l.new(raw.outputs['Y'],adapted.inputs['Y']); l.new(raw.outputs['X'],adapted.inputs['Z']); position = adapted.outputs[0]")
code=code.replace('l.new(position, domain.inputs[0])',"l.new(geo.outputs['Position'], domain.inputs[0])")
exec(compile(code,'shared_plough_material','exec'))
steel=build_material('Plough family - armor worn steel')
paint=build_material('Plough family - armor ochre remnants',painted=True)
hardware=build_material('Plough family - armor fasteners',hardware=True)
painted={'Rear crown','Front crown','Cab door lower rear','Bed rear end upper field'}
for o in bpy.data.collections['Reference traced plating samples'].objects:
    if o.type!='MESH':continue
    m=paint if o.name in painted else steel if 'panel_outline_zy' in o else hardware
    o.data.materials.clear();o.data.materials.append(m)
scene=bpy.context.scene;scene.cycles.samples=48;cam=scene.camera
selected=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
for name,eye,target,scale in [('rear-armor-textured',(4,1.7,-2.8),(.85,.87,-1.62),2.1),('front-armor-textured',(4,1.7,2.7),(.8,.86,1.3),2.0),('door-armor-textured',(4,1.6,.6),(.8,.94,.10),1.5),('front-door-coverage',(5,1.7,1.4),(.8,.95,.65),2.8)]:
    if selected and name not in selected:continue
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(O/'textured-armor-review.blend'))
(O/'review-notes.json').write_text(json.dumps({'materialSource':'apply_reference_finish.py build_material, same as plough','geometry':'approved plate sample scene, unchanged','status':'TEXTURE_REVIEW_NOT_INSTALLED'},indent=2))
