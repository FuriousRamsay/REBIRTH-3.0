"""Shaded numerical-plan projection, not Blender proof or concept artwork."""
import json,math,sys
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
r=Path(__file__).parent
frame=json.loads((r/'bellows-frame-plan.json').read_text());body=json.loads((r/'bellows-frame-aligned-plan.json').read_text());neck=json.loads((r/'nozzle-smooth-plan.json').read_text())
a,e=map(math.radians,[frame['cameraAzimuthDegrees'],frame['cameraElevationDegrees']])
R=np.array([math.cos(a),-math.sin(a),0.]);U=np.array([math.sin(a)*math.sin(e),math.cos(a)*math.sin(e),math.cos(e)]);D=np.cross(R,U)
polygons=[]
mesh_records=[]
attachment=json.loads((r/'attachment-depth-plan.json').read_text()) if '--upright' in sys.argv else None
bellows_shift=np.array(attachment['bellows']['translation']) if attachment else np.zeros(3)
cup_shift=np.array(attachment['crucible']['translation']) if attachment else np.zeros(3)
def mesh(v,faces,shift=None,label=None):
 v=np.array(v)+(np.zeros(3) if shift is None else shift)
 mesh_records.append({'vertices':v.tolist(),'faces':faces,'label':label})
 for f in faces:
  points=v[f]
  candidates=np.cross(points[1:-1]-points[0],points[2:]-points[0])
  lengths=np.linalg.norm(candidates,axis=1);normal=candidates[np.argmax(lengths)];size=max(lengths)
  if size<1e-12:continue
  normal/=size
  if normal@D<0:normal=-normal
  light=.25+.65*max(0,float(normal@((D+U-R)/np.linalg.norm(D+U-R))))
  shade=int(70+150*light)
  polygons.append((float(points.mean(0)@D),points,(shade,shade,shade)))
mesh(body['vertices'],body['faces'],bellows_shift)
full='--full' in sys.argv
upright='--upright' in sys.argv
if full:
 mass=json.loads((r/('upright-hearth-plan.json' if upright else 'furnace-mass-plan.json')).read_text());mesh(mass['vertices'],mass['faces'],label='furnace_core')
 round_parts=json.loads((r/'round-components-plan.json').read_text())
 for part in round_parts['parts']:mesh(part['vertices'],part['faces'],cup_shift if part['id']=='crucible' else None)
 crucible_hardware=json.loads((r/'crucible-hardware-plan.json').read_text())
 for part in crucible_hardware['parts']:mesh(part['vertices'],part['faces'],cup_shift)
 stones=json.loads((r/('bulk-masonry-plan.json' if '--bulk-stones' in sys.argv else ('upright-masonry-plan.json' if upright else 'arch-stones-plan.json'))).read_text())
 for part in stones['parts']:mesh(part['vertices'],part['faces'],label='stone_'+part['id'])
 if upright:
  feet=json.loads((r/'mounting-feet-plan.json').read_text())
  for part in feet['parts']:mesh(part['vertices'],part['faces'])
  bands=json.loads((r/'upright-harness-plan.json').read_text())
  for part in bands['parts']:mesh(part['vertices'],part['faces'],label='harness_'+part['id'])
 if not upright:
  outer=json.loads((r/'outer-stones-plan.json').read_text())
  for part in outer['parts']:mesh(part['vertices'],part['faces'])
  bands=json.loads((r/'iron-bands-plan.json').read_text())
  for part in bands['parts']:mesh(part['vertices'],part['faces'])
boards=json.loads((r/'bellows-board-plan.json').read_text())
for board in boards['parts']:mesh(board['vertices'],board['faces'],bellows_shift)
hardware=json.loads((r/'frame-hardware-plan.json').read_text())
for part in hardware['parts']:mesh(part['vertices'],part['faces'],bellows_shift)
connection=json.loads((r/'nozzle-hardware-plan.json').read_text())
for part in connection['parts']:mesh(part['vertices'],part['faces'],bellows_shift)
for p in frame['records']:
 if 'axisEndpoints' not in p:continue
 start,end=np.array(p['axisEndpoints']);axis=end-start;axis/=np.linalg.norm(axis)
 helper=np.array([0.,1,0]) if abs(axis[1])<.8 else np.array([1.,0,0])
 side=np.cross(axis,helper);side/=np.linalg.norm(side);other=np.cross(axis,side)
 if 'crossSection' in p:
  w,h=p['crossSection'];cross=[side*w*x/2+other*h*y/2 for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]]
 else:cross=[p['diameter']/2*(side*math.cos(t)+other*math.sin(t)) for t in np.linspace(0,math.tau,24,endpoint=False)]
 n=len(cross);v=[(center+c).tolist() for center in [start,end] for c in cross]
 faces=[[j,(j+1)%n,(j+1)%n+n,j+n] for j in range(n)]+[list(range(n-1,-1,-1)),list(range(n,n*2))];mesh(v,faces,bellows_shift)
v=[v for s in neck['sections'] for v in s['ringVertices']];faces=[]
for i in range(len(neck['sections'])-1):
 for j in range(48):faces.append([i*48+j,i*48+(j+1)%48,(i+1)*48+(j+1)%48,(i+1)*48+j])
mesh(v,faces,bellows_shift)
if '--masonry' in sys.argv:
 polygons=[]
 masonry=json.loads((r/'upright-masonry-plan.json').read_text())
 for part in masonry['parts']:mesh(part['vertices'],part['faces'])
if '--export-clay' in sys.argv:
 out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview/clay-input.json')
 out.write_text(json.dumps({'status':'DIAGNOSTIC_ONLY_FIDELITY_FAILED','meshes':mesh_records,'R':R.tolist(),'U':U.tolist(),'D':D.tolist()}))
 print(out);sys.exit(0)
pixels=np.zeros((1100,1200,3),dtype=np.uint8);pixels[:]=[48,52,58]
if '--hearth' in sys.argv:
 polygons=[]
 hearth=json.loads((r/'upright-hearth-plan.json').read_text());mesh(hearth['vertices'],hearth['faces'])
depth=np.full((1100,1200),-np.inf)
side_view='--side' in sys.argv
if side_view:
 angle=a+math.pi/2
 R=np.array([math.cos(angle),-math.sin(angle),0.])
 U=np.array([math.sin(angle)*math.sin(e),math.cos(angle)*math.sin(e),math.cos(e)])
 D=np.cross(R,U)
 all_points=np.concatenate([p for _,p,_ in polygons])
 screen_extent=np.column_stack([all_points@R,-all_points@U])
 lo=screen_extent.min(0);hi=screen_extent.max(0)
 view_scale=min(1050/(hi[0]-lo[0]),950/(hi[1]-lo[1]))
 view_center=(lo+hi)/2
def project(v):
 if side_view:
  xy=(np.array([R@v,-U@v])-view_center)*view_scale+np.array([600,570])
  return tuple(xy)
 x,y=891+760*float(R@v),935-760*float(U@v)
 return (x*.8+20,y*.8+80) if full else ((x-140)*2+40,(y-210)*2+60)
for _,points,color in polygons:
 if side_view:
  normals=np.cross(points[1:-1]-points[0],points[2:]-points[0]);normal=normals[np.argmax(np.linalg.norm(normals,axis=1))];normal/=np.linalg.norm(normal)
  if normal@D<0:normal=-normal
  shade=int(70+150*(.25+.65*max(0,float(normal@((D+U-R)/np.linalg.norm(D+U-R))))));color=(shade,shade,shade)
 for i in range(1,len(points)-1):
  tri=points[[0,i,i+1]];screen=np.array([project(v) for v in tri]);z=tri@D
  xmin=max(0,int(np.floor(screen[:,0].min())));xmax=min(1199,int(np.ceil(screen[:,0].max())))
  ymin=max(0,int(np.floor(screen[:,1].min())));ymax=min(1099,int(np.ceil(screen[:,1].max())))
  if xmin>xmax or ymin>ymax:continue
  x0,y0=screen[0];x1,y1=screen[1];x2,y2=screen[2]
  denominator=(y1-y2)*(x0-x2)+(x2-x1)*(y0-y2)
  if abs(denominator)<1e-9:continue
  yy,xx=np.mgrid[ymin:ymax+1,xmin:xmax+1];xx=xx+.5;yy=yy+.5
  w0=((y1-y2)*(xx-x2)+(x2-x1)*(yy-y2))/denominator
  w1=((y2-y0)*(xx-x2)+(x0-x2)*(yy-y2))/denominator;w2=1-w0-w1
  newdepth=w0*z[0]+w1*z[1]+w2*z[2];olddepth=depth[ymin:ymax+1,xmin:xmax+1]
  mask=(w0>=-1e-8)&(w1>=-1e-8)&(w2>=-1e-8)&(newdepth>olddepth)
  olddepth[mask]=newdepth[mask];pixels[ymin:ymax+1,xmin:xmax+1][mask]=color
im=Image.fromarray(pixels);draw=ImageDraw.Draw(im)
draw.text((20,20),'NUMERICAL PLAN PREVIEW — partial assembly; no Blender, textures or fidelity approval',fill='white')
out=Path('E:/_Haven/shared/REBIRTHTemp/ForgeReferenceBuild/PlanReview');filename='assembly-side-plan.png' if side_view else ('assembly-bulk-plan.png' if full else 'bellows-shaded-plan.png')
if '--masonry' in sys.argv:filename='upright-masonry-side.png' if side_view else 'upright-masonry-reference.png'
if '--hearth' in sys.argv:filename='upright-hearth-side.png' if side_view else 'upright-hearth-reference.png'
if upright:filename='upright-assembly-side.png' if side_view else 'upright-assembly-reference.png'
im.save(out/filename)
print(str(out/filename))
if '--masonry' in sys.argv:
 diagnostic=out/('upright-masonry-side.png' if side_view else 'upright-masonry-reference.png')
 im.save(diagnostic);print(str(diagnostic))


