import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
e=UnityPy.load(sys.argv[1]);o={x.path_id:x for x in e.objects}
t=o[-2274880113814208478].read();im=t.image.convert('RGB')
print(t.m_Name,im.size)
for uv in [(.4785,.8837),(.545,.2606),(.5,.88),(.2,.2),(.9,.9)]:
    p=(int(uv[0]*im.width),int((1-uv[1])*im.height))
    print(uv,p,im.getpixel(p))
im.thumbnail((1024,1024));im.save(sys.argv[2])
