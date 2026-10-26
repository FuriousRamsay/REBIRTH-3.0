import sys,collections,math
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
e=UnityPy.load(sys.argv[1]);o={x.path_id:x for x in e.objects}
for mid in (6130890988526752843,-9148037036518840170,-4796251074057068951):
 h=MeshHandler(o[mid].read());h.process();v=h.m_Vertices;ids={i for sub in h.get_triangles() for f in sub if len(set(f))==3 and all(.69<v[j][0]<.78 and .55<v[j][1]<.68 and -3.49<v[j][2]<-3.4 for j in f) for i in f}
 print('\n',mid,'verts',len(ids))
 counts=collections.Counter((round(math.hypot(v[i][0]-.738,v[i][1]-.6123),4),round(v[i][2],4)) for i in ids)
 for k,n in counts.most_common(50):print(k,n)
