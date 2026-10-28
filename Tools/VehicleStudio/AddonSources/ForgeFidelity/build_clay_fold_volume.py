"""Rounded continuous leather lobes, geometry only, retaining prior evidence."""
import bpy,math,json
from pathlib import Path
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/ClayBellows_03')
if out.exists():raise RuntimeError('Preserve existing review')
out.mkdir(parents=True)
bpy.ops.wm.open_mainfile(filepath='E:/_Haven/shared/REBIRTHTemp/ForgeFidelity/ClayBellows_02/forge-clay.blend')
clay=bpy.data.materials['Geometry review neutral clay']
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith(('Pleated fan','Raised leather','Individual leather')):
  bpy.data.objects.remove(obj,do_unlink=True)
# Observed three broad inflated leather folds narrow into the nozzle.
# Physical shape dimensions are authored estimates, not recovered measurements.
profile=[(0,.026),(.222,.050),(.519,.083),(.815,.100),(1,.085)]
def width(u):
 for (a,wa),(b,wb) in zip(profile,profile[1:]):
  if a<=u<=b:return wa+(wb-wa)*(u-a)/(b-a)
 return .085
for side in [-1,1]:
 verts=[];faces=[];nu=48;nv=72
 for i in range(nu+1):
  u=i/nu;x=-.28-.2106*u;h=.12528*u
  for j in range(nv+1):
   t=j/nv
   # Broad rounded lobes separated by recessed creases; meet wooden boards.
   lobe=math.sin(3*math.pi*t)**2
   bulge=.023*math.sin(math.pi*u/2)*lobe
   y=side*(width(u)+bulge)
   verts.append((x,y,.31-h+2*h*t))
 for i in range(nu):
  for j in range(nv):
   a=i*(nv+1)+j;face=(a,a+1,a+nv+2,a+nv+1)
   faces.append(face if side>0 else tuple(reversed(face)))
 mesh=bpy.data.meshes.new('Rounded three-lobe bellows surface');mesh.from_pydata(verts,[],faces);mesh.update()
 obj=bpy.data.objects.new('Rounded leather fold envelope',mesh);bpy.context.collection.objects.link(obj)
 mesh.materials.append(clay)
 for p in mesh.polygons:p.use_smooth=True
 solid=obj.modifiers.new('Leather shell thickness','SOLIDIFY');solid.thickness=.0015
scene=bpy.context.scene;scene.render.filepath=str(out/'clay-fold-volume.png')
bpy.ops.wm.save_as_mainfile(filepath=str(out/'forge-clay.blend'))
bpy.ops.render.render(write_still=True)
(out/'receipt.json').write_text(json.dumps({'status':'UNACCEPTED_GEOMETRY_EXPERIMENT','change':'Three rounded leather lobes per side replacing flat pleat surfaces','remaining':'Reference contour, wooden frame, nozzle, seams and all forge components still require comparison','textures':False},indent=2))
