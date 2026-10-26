import sys,collections
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
e=UnityPy.load(sys.argv[1]);objects={o.path_id:o for o in e.objects}
for label,ids in [('lens',(-6571958702597769235,209635268596600669,-7204612321527722442)),('surround',(6130890988526752843,-9148037036518840170,-4796251074057068951))]:
 for mid in ids:
    h=MeshHandler(objects[mid].read());h.process();v=h.m_Vertices
    groups=collections.defaultdict(list)
    for sub in h.get_triangles():
     for f in sub:
      if len(set(f))!=3:continue
      pts=[v[i] for i in f]
      if not all(.65<abs(x)<.91 and .50<y<.72 and -3.53<z<-3.38 for x,y,z in pts):continue
      center=sum(x for x,y,z in pts)/3
      key=('+outer' if center>0 else '-outer') if abs(center)>.783 else ('+inner' if center>0 else '-inner')
      groups[key].append(f)
    print(label,mid)
    for key,faces in sorted(groups.items()):
      inds=[i for f in faces for i in f];unique=set(inds)
      print(key,'faces',len(faces),'unique',len(unique),'reused',len(inds)-len(unique))
      print(' bounds',[(round(min(v[i][a] for i in unique),4),round(max(v[i][a] for i in unique),4)) for a in range(3)])
      if label=='lens':
       uv=[h.m_UV0[i] for i in unique]
       print(' UV',[(round(min(q[a] for q in uv),4),round(max(q[a] for q in uv),4)) for a in range(2)])
       if key=='+inner':
        for axis in (0,1):
         lo=[v[i][2] for i in unique if v[i][axis] < (.738 if axis==0 else .6123)]
         hi=[v[i][2] for i in unique if v[i][axis] > (.738 if axis==0 else .6123)]
         print(' z mean axis',axis,round(sum(lo)/len(lo),5),round(sum(hi)/len(hi),5))
