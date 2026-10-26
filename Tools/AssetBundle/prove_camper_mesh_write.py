"""Stage a one-vertex write and verify Unity decodes it after bundle reopen."""
import json,struct,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.streams import EndianBinaryReader
from UnityPy.helpers.MeshHelper import MeshHandler
source=Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d'
out=Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio/RearHousingWriteProof_20261023')
out.mkdir(parents=True,exist_ok=True)
env=UnityPy.load(str(source));o=next(o for o in env.objects if o.path_id==6130890988526752843)
tree=o.read_typetree();raw=bytearray(tree['m_VertexData']['m_DataSize'])
idx=next(i for i in range(tree['m_VertexData']['m_VertexCount']) if -.861<struct.unpack_from('<f',raw,i*36)[0]<-.860)
before=struct.unpack_from('<fff',raw,idx*36)
print('inline_before',before)
struct.pack_into('<f',raw,idx*36,before[0]+.001)
bundle=next(iter(env.files.values()))
resource_name=next(name for name in bundle.files if name.endswith('.resS'))
resource=bytearray(bundle.files[resource_name].bytes)
stream_offset=tree['m_StreamData']['offset']
print('stream_before',struct.unpack_from('<fff',resource,stream_offset+idx*36),'offset',stream_offset,'resource_len',len(resource))
struct.pack_into('<f',resource,stream_offset+idx*36,before[0]+.001)
updated_resource=EndianBinaryReader(bytes(resource))
updated_resource.flags=bundle.files[resource_name].flags
bundle.files[resource_name]=updated_resource
bundle.mark_changed()
env.save(pack='lz4',out_path=str(out))
check=UnityPy.load(str(out/source.name));x=next(x for x in check.objects if x.path_id==o.path_id)
saved_bundle=next(iter(check.files.values()))
saved_resource=next(child for name,child in saved_bundle.files.items() if name.endswith('.resS'))
print('stream_after',struct.unpack_from('<fff',saved_resource.bytes,stream_offset+idx*36))
saved=x.read_typetree()['m_VertexData']['m_DataSize'];after=struct.unpack_from('<fff',saved,idx*36)
m=x.read();h=MeshHandler(m);h.process()
print(json.dumps(dict(index=idx,before=before,after=after,decoded=h.m_Vertices[idx]),default=list))
if abs(h.m_Vertices[idx][0]-before[0]-.001)>1e-5:raise RuntimeError('mesh vertex change was not retained')
