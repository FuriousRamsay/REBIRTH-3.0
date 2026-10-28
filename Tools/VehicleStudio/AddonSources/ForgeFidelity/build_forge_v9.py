"""Smooth fitted bands preserve strip width instead of independently projecting edges."""
from pathlib import Path
driver=Path(__file__).with_name('build_forge_v8.py').read_text()
driver=driver.rsplit("exec(compile(source,__file__,'exec'))",1)[0]+'pass\n'
exec(compile(driver,__file__,'exec'))
source=source.replace('ForgeFidelity/v8','ForgeFidelity/v9')
start=source.index('for obj in list(bpy.context.scene.objects):\n if obj.name.startswith((\'Curved horizontal')
end=source.index('# Controlled inspection scene',start)
source=source[:start]+'''
# Smooth ellipse fitted outside each course. Cross braces must remain manufactured iron strips.
bands=[o for o in bpy.context.scene.objects if o.name.startswith('Curved horizontal iron cross brace')]
for obj in bands:
 z=sum(v.co.z for v in obj.data.vertices)/len(obj.data.vertices)
 rx,ry=(.279,.252) if z>.35 else (.318,.273)
 for j in range(41):
  t=math.pi+math.pi*j/40
  for k,h in enumerate([-.016,.016]):obj.data.vertices[2*j+k].co=(rx*math.cos(t),ry*math.sin(t),z+h)
 for poly in obj.data.polygons:poly.use_smooth=True
 bevel=obj.modifiers.new('Rounded forged strip edges','BEVEL');bevel.width=.002;bevel.segments=3
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith('Domed brace rivet'):
  x,y,z=obj.location;rx,ry=(.279,.252) if z>.35 else (.318,.273)
  t=math.atan2(y/ry,x/rx);obj.location=(rx*math.cos(t),ry*math.sin(t)-.007,z)
# Smooth front ribs retain the stepped reference profile while eliminating hard folds.
for obj in list(bpy.context.scene.objects):
 if obj.name.startswith('Profile fitted front strap'):
  bevel=obj.modifiers.new('Rib softened edges','BEVEL');bevel.width=.002;bevel.segments=3
  for poly in obj.data.polygons:poly.use_smooth=True
''' +source[end:]
exec(compile(source,__file__,'exec'))
