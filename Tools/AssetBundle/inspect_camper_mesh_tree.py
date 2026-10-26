import json,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
p=Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d'
env=UnityPy.load(str(p));o=next(o for o in env.objects if o.path_id==6130890988526752843)
t=o.read_typetree()
print('root keys',list(t.keys()))
print('vertex keys',list(t['m_VertexData'].keys()))
for k,v in t['m_VertexData'].items():print(k,type(v).__name__,len(v) if hasattr(v,'__len__') else v)
print('stream',t['m_StreamData'])
print('meshcompression',t['m_MeshCompression'])
