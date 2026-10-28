"""Surface-fitted ironwork and reference-side inspection camera."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v7.py').read_text()
driver=driver.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v7','ForgeFidelity/v8')
extra='''
# Close the crown around the vent without capping the vent opening.
for i in range(12):
 t=2*math.pi*i/12
 rock('Crown infill stone_%d'%i,(.10*math.cos(t),.082*math.sin(t),.505),(.033,.032,.027),stone)
bpy.context.view_layer.update()
stones=[o for o in bpy.context.scene.objects if o.name.startswith(('Masonry_','Arch wedge','Crown infill'))]
deps=bpy.context.evaluated_depsgraph_get()
def surface_y(x,z):
 best=None
 for original in stones:
  obj=original.evaluated_get(deps);inverse=obj.matrix_world.inverted()
  origin=inverse@Vector((x,-1,z));direction=(inverse.to_3x3()@Vector((0,1,0))).normalized()
  hit,point,normal,index=obj.ray_cast(origin,direction)
  if hit:
   world=obj.matrix_world@point
   if best is None or world.y<best:best=world.y
 return best
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith(('Curved horizontal iron cross brace','Profile fitted front strap')):
  for vertex in obj.data.vertices:
   world=obj.matrix_world@vertex.co
   y=surface_y(world.x,world.z)
   if y is not None:
    world.y=y-.011;vertex.co=obj.matrix_world.inverted()@world
# Rivets are placed on the same measured front surface instead of guessed coordinates.
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith(('Domed brace rivet','Dome rivet')):
  y=surface_y(obj.location.x,obj.location.z)
  if y is not None:obj.location.y=y-.024
'''
source=source.replace('# Controlled inspection scene',extra+'\n# Controlled inspection scene')
source=source.replace('location=(1.05,-1.8,1.05)','location=(-1.05,-1.8,1.05)')
exec(compile(source,__file__,'exec'))
