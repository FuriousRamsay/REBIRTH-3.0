"""Orthographic mesh check for the installed camper's rear bumper lamp area."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
from PIL import Image,ImageDraw

source=Path(sys.argv[1]);target=Path(sys.argv[2])
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
mesh_ids=[(-6753923948166799871,(73,75,80)),(6130890988526752843,(138,142,146)),(-6571958702597769235,(175,35,31))]
faces=[]
for mid,color in mesh_ids:
    h=MeshHandler(objects[mid].read());h.process();v=h.m_Vertices
    for sub in h.get_triangles():
        for f in sub:
            if len(set(f))!=3:continue
            pts=[v[i] for i in f]
            if not all(.60<abs(x)<.95 and .52<y<.71 and -3.54<z<-3.36 for x,y,z in pts):continue
            faces.append((sum(p[2] for p in pts)/3,color,pts))
im=Image.new('RGB',(900,560),(31,32,36));d=ImageDraw.Draw(im)
def xy(p):return (int((p[0]-.64)*3000),int((.71-p[1])*3000))
for depth,color,pts in sorted(faces,reverse=True):d.polygon([xy(p) for p in pts],fill=color)
for x in (.738,.8277):
    px=int((x-.64)*3000);py=int((.71-.6123)*3000)
    d.line((px-5,py,px+5,py),fill=(255,215,0),width=2);d.line((px,py-5,px,py+5),fill=(255,215,0),width=2)
im.save(target)
print(target,len(faces))
