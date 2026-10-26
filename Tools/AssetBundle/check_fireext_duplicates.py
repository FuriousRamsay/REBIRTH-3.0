import sys,json,hashlib
from pathlib import Path
sys.path.insert(0,str(Path('Tools/AssetBundle/.python').resolve()))
import UnityPy
p=Path('_Deliverables/FireEXT_2048_1024_256_Crunch/Resources/RB_TOOL_FireEXT.unity3d')
e=UnityPy.load(str(p));o={x.path_id:x for x in e.objects}
for a,b in [(98,100),(113,120)]:
 x,y=o[a].read_typetree(),o[b].read_typetree()
 print(a,b,[(k,x[k],y[k]) for k in x if x[k]!=y[k]])
 print('payload_equal',o[a].read().get_image_data()==o[b].read().get_image_data())
print('serialized',[(k,list(v.objects)[:2]) for k,v in e.files[next(iter(e.files))].files.items() if hasattr(v,'objects')])
