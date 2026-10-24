"""Depth-buffered, backface-culled orthographic QA views of camper rear lamps."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
import numpy as np
from PIL import Image
from UnityPy.helpers.MeshHelper import MeshHandler

source=Path(sys.argv[1]);out=Path(sys.argv[2]);out.mkdir(parents=True,exist_ok=True)
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
parts=[(-6753923948166799871,(55,57,61)),(6130890988526752843,(118,119,122)),(-6571958702597769235,(175,34,30))]
geometry=[]
for mid,color in parts:
 h=MeshHandler(objects[mid].read());h.process();v=h.m_Vertices
 for sub in h.get_triangles():
  for f in sub:
   if len(set(f))!=3:continue
   pts=np.asarray([v[i] for i in f],dtype=float)
   if not np.all((pts[:,0]>.64)&(pts[:,0]<.94)&(pts[:,1]>.53)&(pts[:,1]<.69)&(pts[:,2]>-3.52)&(pts[:,2]<-3.36)):continue
   geometry.append((pts,color))

def render(mode):
 width,height=(900,540) if mode=='front' else (840,540)
 rgb=np.full((height,width,3),(28,29,32),dtype=np.uint8)
 depth=np.full((height,width),np.inf)
 for pts,color in geometry:
  cross=np.cross(pts[1]-pts[0],pts[2]-pts[0])
  if mode=='front':
   if cross[2]>=-1e-10:continue
   q=np.stack(((pts[:,0]-.64)*3000,(.69-pts[:,1])*3300),axis=1)
   z=pts[:,2]
  else:
   if cross[0]<=1e-10:continue
   q=np.stack(((pts[:,2]+3.52)*5200,(.69-pts[:,1])*3300),axis=1)
   z=-pts[:,0]
  minx=max(0,int(np.floor(q[:,0].min())));maxx=min(width-1,int(np.ceil(q[:,0].max())))
  miny=max(0,int(np.floor(q[:,1].min())));maxy=min(height-1,int(np.ceil(q[:,1].max())))
  if minx>maxx or miny>maxy:continue
  xx,yy=np.meshgrid(np.arange(minx,maxx+1)+.5,np.arange(miny,maxy+1)+.5)
  x0,y0=q[0];x1,y1=q[1];x2,y2=q[2]
  det=(y1-y2)*(x0-x2)+(x2-x1)*(y0-y2)
  if abs(det)<1e-9:continue
  a=((y1-y2)*(xx-x2)+(x2-x1)*(yy-y2))/det
  b=((y2-y0)*(xx-x2)+(x0-x2)*(yy-y2))/det
  c=1-a-b
  near=a*z[0]+b*z[1]+c*z[2]
  block=depth[miny:maxy+1,minx:maxx+1]
  mask=(a>=-1e-6)&(b>=-1e-6)&(c>=-1e-6)&(near<block)
  block[mask]=near[mask]
  rgb[miny:maxy+1,minx:maxx+1][mask]=color
 path=out/f'{mode}-depth.png';Image.fromarray(rgb).save(path);print(path)

render('front');render('side')
