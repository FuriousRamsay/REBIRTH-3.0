"""Locate actual rear bumper-end geometry in installed camper meshes."""
import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

source=Path(sys.argv[1]) if len(sys.argv)>1 else Path('Resources/rebirth_camper80s.unity3d')
env=UnityPy.load(str(source));objects={o.path_id:o for o in env.objects}
for mesh_id in (6130890988526752843,-9148037036518840170,-4796251074057068951,
                -6571958702597769235,209635268596600669,-7204612321527722442):
    helper=MeshHandler(objects[mesh_id].read());helper.process();v=helper.m_Vertices
    print('\nmesh',mesh_id,helper.src.m_Name)
    for side in (-1,1):
        for lo,hi in ((.60,.75),(.75,.85),(.85,.95),(.95,1.08)):
            faces=[f for sub in helper.get_triangles() for f in sub if len(set(f))==3 and
                   all(lo<side*v[i][0]<hi and .3<v[i][1]<.8 and -3.65<v[i][2]<-3.25 for i in f)]
            if not faces:continue
            ids={i for f in faces for i in f}
            xyz=[[v[i][a] for i in ids] for a in range(3)]
            print('side',side,'xspan',lo,hi,'faces',len(faces),'bbox',[[round(min(c),4),round(max(c),4)] for c in xyz])
