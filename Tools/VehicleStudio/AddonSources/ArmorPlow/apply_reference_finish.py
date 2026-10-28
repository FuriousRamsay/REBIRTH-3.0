"""Native layered material study: traced coating remnants, oxide and exposed steel.
Reference pixels define paint placement, not a copied photograph texture.
"""
import bpy,json,math,sys,numpy as np
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.geometry import tessellate_polygon
P=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/CamperReferenceSurvey_20261017');O=P/'ReferenceFinish';O.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(P/'ConnectedGrilles/camper-connected-grilles.blend'))
detail_image=bpy.data.images.load(str(Path(__file__).parent/'ReferenceSurfaceDetail_20261017/dark-steel-detail.png'),check_existing=True)

paint_pixels=[[(126,480),(151,480),(174,496),(184,516),(166,524),(151,506)],[(271,485),(295,481),(315,496),(320,509),(300,518),(281,508)],[(358,483),(402,482),(419,508),(432,536),(418,552),(404,522),(386,526),(378,500)],[(401,605),(423,587),(444,605),(454,631),(456,663),(436,669),(438,638),(417,649)],[(205,617),(221,600),(237,608),(231,630),(215,636)],[(103,562),(116,552),(128,562),(122,581),(105,585)],[(264,635),(282,626),(296,641),(289,650),(271,649)]]
pixels=np.array([[103,474],[415,474],[464,691],[81,633]],float);uv=np.array([[0,1],[1,1],[1,0],[0,0]],float);A=[];B=[]
for (x,y),(u,v) in zip(pixels,uv):A.extend([[x,y,1,0,0,0,-u*x,-u*y],[0,0,0,x,y,1,-v*x,-v*y]]);B.extend([u,v])
H=np.append(np.linalg.solve(A,B),1).reshape(3,3)
paint_shapes=[]
for polygon in paint_pixels:
    shape=[]
    for x,y in polygon:
        q=H@np.array([x,y,1]);u,t=q[:2]/q[2];shape.append(((u-.5)*2.3,.35+t*.87))
    paint_shapes.append(shape)

def build_material(name,painted=False,grille=False,hardware=False):
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;l=m.node_tree.links;n.clear()
    def node(kind):return n.new(kind)
    def inp(s,v):
        if isinstance(v,(float,int)):s.default_value=v
        else:l.new(v,s)
    def mathnode(op,a,b=0):
        q=node('ShaderNodeMath');q.operation=op;inp(q.inputs[0],a);inp(q.inputs[1],b);return q.outputs[0]
    def mix(f,a,b):
        q=node('ShaderNodeMixRGB');inp(q.inputs[0],f)
        for s,v in [(q.inputs[1],a),(q.inputs[2],b)]:
            if isinstance(v,tuple):s.default_value=(*v,1)
            else:l.new(v,s)
        return q.outputs[0]
    geo=node('ShaderNodeNewGeometry');position=geo.outputs['Position'];sep=node('ShaderNodeSeparateXYZ');l.new(position,sep.inputs[0]);x,y,z=[sep.outputs[k] for k in ['X','Y','Z']]
    def noise(scale,detail=3):
        q=node('ShaderNodeTexNoise');l.new(position,q.inputs['Vector']);q.inputs['Scale'].default_value=scale;q.inputs['Detail'].default_value=detail;q.inputs['Roughness'].default_value=.7;return q.outputs['Fac']
    coarse=noise(9);fine=noise(175,2);micro=noise(520,2)
    texture=node('ShaderNodeTexImage');texture.image=detail_image;texture.projection='BOX';texture.projection_blend=.20
    domain=node('ShaderNodeVectorMath');domain.operation='SCALE';l.new(position,domain.inputs[0]);domain.inputs[3].default_value=2.5;l.new(domain.outputs[0],texture.inputs['Vector'])
    # Distorted polygon domains make genuinely irregular coating boundaries,
    # retaining the actual reference layout instead of circular camouflage blobs.
    qx=mathnode('ADD',x,mathnode('MULTIPLY',mathnode('SUBTRACT',coarse,.5),.045))
    qy=mathnode('ADD',y,mathnode('MULTIPLY',mathnode('SUBTRACT',noise(15),.5),.028))
    paint=0.
    if painted:
        for shape in paint_shapes:
            vectors=[Vector((a,b,0)) for a,b in shape]
            for triangle in tessellate_polygon([vectors]):
                tri=[vectors[v] if isinstance(v,int) else v for v in triangle]
                area=(tri[1]-tri[0]).cross(tri[2]-tri[0]).z;inside=1.
                for a,b in zip(tri,tri[1:]+tri[:1]):
                    cross=mathnode('SUBTRACT',mathnode('MULTIPLY',mathnode('SUBTRACT',qx,a.x),b.y-a.y),mathnode('MULTIPLY',mathnode('SUBTRACT',qy,a.y),b.x-a.x))
                    side=mathnode('LESS_THAN' if area>0 else 'GREATER_THAN',cross,0)
                    inside=mathnode('MULTIPLY',inside,side)
                paint=mathnode('MAXIMUM',paint,inside)
        paint=mathnode('MULTIPLY',paint,mathnode('GREATER_THAN',fine,.33))
        paint=mathnode('MULTIPLY',paint,mathnode('GREATER_THAN',noise(45),.43))
    # Corrosion prefers the lower retained-moisture band and strip interfaces.
    wet=mathnode('MULTIPLY',mathnode('MAXIMUM',0,mathnode('MINIMUM',.25,mathnode('MULTIPLY',mathnode('SUBTRACT',.66,y),.65))),noise(12))
    seam=0.
    for sx in [-1.1086,-.621,-.092,.46,1.1086]:
        distance=mathnode('ABSOLUTE',mathnode('SUBTRACT',x,sx))
        seam=mathnode('MAXIMUM',seam,mathnode('LESS_THAN',distance,.022))
    rust_amount=mathnode('ADD',mathnode('MULTIPLY',seam,.25),mathnode('ADD',wet,.16 if not grille else .32))
    rust=mathnode('MULTIPLY',mathnode('GREATER_THAN',coarse,mathnode('SUBTRACT',.78,rust_amount)),mathnode('SUBTRACT',1,paint))
    steel=texture.outputs['Color']
    oxide=(.065,.021,.008);yellow=(.48,.285,.026)
    base=mix(mathnode('MULTIPLY',rust,.22),steel,oxide)
    paint_detail=node('ShaderNodeMixRGB');paint_detail.blend_type='MULTIPLY';paint_detail.inputs[0].default_value=.28;paint_detail.inputs[1].default_value=(*yellow,1);l.new(texture.outputs['Color'],paint_detail.inputs[2])
    base=mix(paint,base,paint_detail.outputs[0])
    # Sparse directional abrasion, distinct from corrosion and surviving paint.
    mapping=node('ShaderNodeVectorMath');mapping.operation='MULTIPLY';l.new(position,mapping.inputs[0]);mapping.inputs[1].default_value=(6,150,110)
    scratch_noise=node('ShaderNodeTexNoise');l.new(mapping.outputs['Vector'],scratch_noise.inputs['Vector']);scratch_noise.inputs['Scale'].default_value=1
    scratch=mathnode('MULTIPLY',mathnode('GREATER_THAN',scratch_noise.outputs['Fac'],.77),mathnode('GREATER_THAN',noise(23),.57))
    if not grille and not painted:scratch=mathnode('MAXIMUM',scratch,mathnode('MULTIPLY',mathnode('GREATER_THAN',z,2.98),.28))
    base=mix(scratch,base,(.21,.19,.155));variation=node('ShaderNodeMixRGB');variation.blend_type='MULTIPLY';variation.inputs[0].default_value=.22;l.new(base,variation.inputs[1]);l.new(noise(45),variation.inputs[2])
    bs=node('ShaderNodeBsdfPrincipled');l.new(variation.outputs[0],bs.inputs['Base Color'])
    metallic=mathnode('MULTIPLY',mathnode('SUBTRACT',1,mathnode('MAXIMUM',mathnode('MULTIPLY',rust,.7),paint)),.62 if not hardware else .82)
    l.new(metallic,bs.inputs['Metallic']);l.new(mathnode('ADD',.52 if not hardware else .43,mathnode('ADD',mathnode('MULTIPLY',rust,.34),mathnode('MULTIPLY',paint,.15))),bs.inputs['Roughness'])
    # Fine oxide relief is confined to exposed/rusted areas, not paint pebbling.
    bump=node('ShaderNodeBump');bump.inputs['Strength'].default_value=.55;bump.inputs['Distance'].default_value=.0009
    pits=mathnode('MULTIPLY',mathnode('MAXIMUM',0,mathnode('SUBTRACT',.48,fine)),rust)
    height=mathnode('ADD',mathnode('MULTIPLY',fine,.12),mathnode('ADD',mathnode('MULTIPLY',micro,.10),mathnode('SUBTRACT',mathnode('MULTIPLY',paint,.18),mathnode('MULTIPLY',pits,2.0))))
    l.new(height,bump.inputs['Height']);l.new(bump.outputs['Normal'],bs.inputs['Normal'])
    out=node('ShaderNodeOutputMaterial');l.new(bs.outputs[0],out.inputs['Surface']);return m

paint=build_material('Reference ochre remnants on dark plough steel',True)
steel=build_material('Reference worn spike and cutting steel')
joint=build_material('Reference oxidized fasteners',hardware=True)
grille=build_material('Reference dark oxidized grille tubes',grille=True)
target_names=['Reference plough clay','Camper mounted plough receivers','Reference connected window guards']
counts={}
for name in target_names:
    col=bpy.data.collections[name]
    for o in col.objects:
        if o.type not in ['MESH','CURVE']:continue
        if name=='Reference connected window guards':material=joint if any(k in o.name for k in ['fastener','washer','coupling','sleeve']) else grille
        else:material=joint if any(k in o.name for k in ['fastener','washer','bolt','pin']) else steel if any(k in o.name for k in ['spike','tooth','shank']) else paint
        o.data.materials.clear();o.data.materials.append(material);counts[material.name]=counts.get(material.name,0)+1

scene=bpy.context.scene;scene.cycles.samples=48;cam=scene.camera
selected=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
def mod_visibility(name):
    armor_only=name.startswith(('grilles','armor-front')) or name in ['grille-finish','overview']
    plough_only=name=='plough-only-front'
    for col in ['Reference plough clay','Camper mounted plough receivers']:bpy.data.collections[col].hide_render=armor_only
    bpy.data.collections['Reference connected window guards'].hide_render=plough_only
extra_views=[('grilles-front',(0,2.1,7),(0,1.8,.2),3.8),('grilles-rear-straight',(0,2,-8),(0,1.7,-2),3.8),('grilles-rear-opposite',(-4,3,-7),(0,1.7,-1.5),5.3),('grilles-raised',(5,6,4),(0,1.7,-.8),6.2),('armor-front-detail',(2,1.6,5),(0,.90,2.0),2.3),('armor-front-mount-side',(4,1.0,2.15),(.65,.90,2.04),.85),('armor-front-mount-opposite',(-4,1.0,2.15),(-.65,.90,2.04),.85),('armor-front-mount-under',(2,.35,3),(.65,.74,2.04),1.0),('both-front',(5,3,7),(0,1.5,-.2),6.2),('plough-only-front',(5,3,7),(0,1.5,-.2),6.2)]
for name,eye,target,scale in extra_views:
    if name not in selected:continue
    mod_visibility(name)
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
for name,eye,target,scale in [('plough-finish',(3,1.65,6.2),(0,.80,2.55),2.9),('grille-finish',(4,2.2,3),(.3,1.65,.2),2.7),('mounted-finish',(4,.75,2.3),(.65,.56,1.95),1.85),('overview',(6,3.5,7),(0,1.5,-.15),6.6),('grilles-left-side',(7,2.5,-1),(.1,1.8,-1),5.1),('grilles-right-side',(-7,2.5,-1),(-.1,1.8,-1),5.1),('grilles-rear',(4,3,-7),(0,1.7,-1.5),5.3)]:
    if selected and name not in selected:continue
    mod_visibility(name)
    cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale;scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
(O/'material-plan.json').write_text(json.dumps({'paintReferencePixels':paint_pixels,'assignmentCounts':counts,'source':'raised-spiked-plow.png and armor-window-grilles.png','method':'native layered materials, authored coating remnants, localized corrosion, physical-scale relief','status':'MATERIAL_STUDY_NOT_TEXTURE_BAKE_OR_INSTALLATION'},indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(O/'reference-finish-study.blend'))
