"""Thickness review only; preserve accepted outlines, body fit and mount endpoints."""
import bpy,json,sys
from pathlib import Path
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
source=Path(__file__).with_name('render_armor_plough_closeup.py').read_text()
exec(source.split('scene=bpy.context.scene;scene.cycles.samples=48')[0])
O=P/('Thickness40Review_20261018' if '--40' in sys.argv else 'ThicknessReview_20261018');O.mkdir(exist_ok=True)
platecols=[bpy.data.collections[n] for n in ['Reference traced plating samples','Armor opposite side review']]
changed={};old_washers=[]
for col in platecols:
 for obj in col.objects:
  if 'panel_outline_zy' not in obj:continue
  for mod in obj.modifiers:
   if mod.type=='SOLIDIFY':mod.thickness=.040 if '--40' in sys.argv else .020;mod.name='40 mm protective plate' if '--40' in sys.argv else '20 mm protective plate'
  bevel=obj.modifiers.new('Visible fabricated edge bevel','BEVEL');bevel.width=.0015;bevel.segments=3;bevel.limit_method='ANGLE';bevel.angle_limit=.55
  changed[obj.name]=obj
plough=bpy.data.collections['Reference plough clay']
def front_z(y):
 t=(y-.35)/.87
 return 2.39+.30*(1-t)**2+.06*t**8
for obj in plough.objects:
 if obj.name.startswith('Flat curved bolted strap'):
  cx=sum(v.co.x for v in obj.data.vertices)/len(obj.data.vertices)
  for v in obj.data.vertices:
   v.co.x=cx+(v.co.x-cx)*1.4
   if v.co.z>front_z(v.co.y)+.004:v.co.z+=.007
  changed[obj.name]=obj
 if obj.name.startswith('Rolled upper edge') and obj.type=='CURVE':obj.data.bevel_depth=.025
 if obj.name.startswith('Short overlapping cutting tooth'):
  count=len(obj.data.vertices)//2
  for v in list(obj.data.vertices)[:count]:v.co.z+=.010
  changed[obj.name]=obj
# Refit complete hardware to the new plate/strap surfaces; no floating fixings.
allhardware=[o for col in platecols+[plough] for o in col.objects if o.type=='MESH' and ('washer' in o.name.lower() or 'fastener' in o.name.lower())]
for obj in allhardware:
 if 'washer' not in obj.name.lower() or len(obj.data.vertices)!=48:continue
 base=sum((v.co for v in list(obj.data.vertices)[:24]),Vector())/24;top=sum((v.co for v in list(obj.data.vertices)[24:48]),Vector())/24
 support=obj.get('seat_support')
 if support not in changed:
  if obj in plough.objects.values() and abs(base.y-.408)<.015 and base.z>2.6:
   teeth=[o for o in changed.values() if o.name.startswith('Short overlapping cutting tooth')]
   support=min(teeth,key=lambda o:abs(sum(v.co.x for v in o.data.vertices)/len(o.data.vertices)-base.x)).name
  else:continue
 old_washers.append((obj,base.copy(),(top-base).normalized(),support))
bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get();surfaces={n:BVHTree.FromObject(o,dg) for n,o in changed.items()};updates=[]
for washer,old,n,support in old_washers:
 target=surfaces[support];hit,normal,_,_=target.ray_cast(old+n*.07,-n,.14);assert hit is not None,support
 updates.append((old,hit,n.rotation_difference(normal)))
 for i in range(24):
  v=washer.data.vertices[i];p,_,_,_=target.ray_cast(v.co+n*.07,-n,.14);assert p is not None,'Washer leaves plate '+support
  v.co=p;washer.data.vertices[i+24].co=p+normal*.0039
for obj in allhardware:
 if 'fastener' not in obj.name.lower():continue
 center=sum((v.co for v in obj.data.vertices),Vector())/len(obj.data.vertices)
 old,new,rotation=min(updates,key=lambda row:(center-row[0]).length)
 if (center-old).length>.025:continue
 for v in obj.data.vertices:v.co=new+rotation@(v.co-old)
scene=bpy.context.scene;scene.cycles.samples=48
bpy.ops.wm.save_as_mainfile(filepath=str(O/'protective-thickness-review.blend'))
for name,eye,target,scale in ([] if '--no-render' in sys.argv else [('thicker-plates-and-plough',(4,1.65,5),(.4,.9,1.25),3.1),('plate-edge-closeup',(5,1.35,1.5),(.85,.95,.65),1.7)]):
 cam=scene.camera;cam.location=eye;f=(Vector(target)-cam.location).normalized();r=f.cross(Vector((0,1,0))).normalized();u=r.cross(f);cam.rotation_euler=Matrix((r,u,-f)).transposed().to_euler();cam.data.ortho_scale=scale
 scene.render.filepath=str(O/(name+'.png'));bpy.ops.render.render(write_still=True)
(O/'review.json').write_text(json.dumps({'armorThicknessM':.040 if '--40' in sys.argv else .020,'plateBevelM':.0015,'ploughFaceStrapWidthM':.070,'ploughFaceStrapDepthM':.016,'rolledTopDiameterM':.050,'toothThicknessM':.0284,'moldboardUnchangedM':.022,'status':'REVIEW_ONLY_NOT_INSTALLED','preserved':'Accepted outline boundaries, body clearance, grille placement and mount endpoints'},indent=2))

