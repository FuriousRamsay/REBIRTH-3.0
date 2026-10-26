"""Orthographic right-side profile check of camper bumper/lamp depth."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
from PIL import Image,ImageDraw
source=Path(sys.argv[1]);target=Path(sys.argv[2])
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
parts=[(-6753923948166799871,(90,82,98)),(6130890988526752843,(157,156,163)),(-6571958702597769235,(187,46,42))]
faces=[]
for mid,color in parts:
    h=MeshHandler(objects[mid].read());h.process();v=h.m_Vertices
    for sub in h.get_triangles():
        for f in sub:
            if len(set(f))!=3:continue
            pts=[v[i] for i in f]
            if not all(.65<x<.94 and .53<y<.69 and -3.52<z<-3.36 for x,y,z in pts):continue
            faces.append((sum(p[0] for p in pts)/3,color,pts))
im=Image.new('RGB',(900,600),(31,32,36));d=ImageDraw.Draw(im)
def pt(v):return (int((v[2]+3.53)*5000),int((.69-v[1])*3500))
for depth,color,vertices in sorted(faces):d.polygon([pt(p) for p in vertices],fill=color)
for z,label in [(-3.4591,'old lens face'),(-3.4391,'new lens face'),(-3.429,'plate face'),(-3.398,'plate back')]:
    px=int((z+3.53)*5000);d.line((px,0,px,600),fill=(90,95,100),width=1)
im.save(target);print(target,len(faces))
