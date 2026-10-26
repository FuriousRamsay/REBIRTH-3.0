"""Turn the camper's four offset bumper ovals into flush lamp mounting bosses.

Only camper_body geometry changes. Red lenses, their existing surrounds, and all
lighting behavior stay byte-for-byte unchanged. Source is the pre-cap-removal
bundle, since that contains the original body vertices and triangle indices.
"""
import argparse
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
from UnityPy.streams import EndianBinaryReader

BODY = {-6753923948166799871:640, -6045061701533401843:496, 8398484439755328957:76}
LAMPS = (6130890988526752843,-9148037036518840170,-4796251074057068951,
         -6571958702597769235,209635268596600669,-7204612321527722442)

def cap_vertex(v):
    x,y,z=v
    return .77<abs(x)<.89 and .56<y<.66 and -3.47<z<-3.40

def cap_indices(h):
    faces=[f for sub in h.get_triangles() for f in sub if len(set(f))==3 and all(cap_vertex(h.m_Vertices[i]) for i in f)]
    return len(faces),{i for f in faces for i in f}

def position(x,y,z):
    side=1 if x>0 else -1
    ax=abs(x)
    old=.8060 if ax<.831 else .8598
    new=.7380 if ax<.831 else .8277
    inner=ax<.831
    dx=ax-old
    # The shallow circular nose seats behind the red lens.  The wider rear
    # flange covers the former oval footprint and joins the bumper face.
    t=max(0,min(1,(-z-3.4407)/.0237))
    if inner:
        rear_scale=2.05 if dx<0 else 1.45
    else:
        rear_scale=1.72 if dx<0 else 2.17
    scale=rear_scale*(1-t)+1.18*t
    yscale=1.0*(1-t)+.72*t
    nx=side*(new+dx*scale)
    ny=.6123+(y-.6123)*yscale
    nz=z+.016
    return nx,ny,nz

def main():
    p=argparse.ArgumentParser()
    p.add_argument('--source',type=Path,required=True)
    p.add_argument('--out',type=Path,required=True)
    args=p.parse_args();args.out.mkdir(parents=True,exist_ok=True)
    env=UnityPy.load(str(args.source));objects={o.path_id:o for o in env.objects}
    bundle=next(iter(env.files.values()))
    resource_name=next(n for n in bundle.files if n.endswith('.resS'))
    original=bytes(bundle.files[resource_name].bytes)
    resource=bytearray(original)
    report=[]
    for mid,expected in BODY.items():
        obj=objects[mid];tree=obj.read_typetree();h=MeshHandler(obj.read());h.process()
        faces,indices=cap_indices(h)
        if faces!=expected:raise RuntimeError(f'cap selection {mid}: {faces}/{expected}')
        count=tree['m_VertexData']['m_VertexCount']
        streamed=tree['m_StreamData']['size']>0
        stride=(tree['m_StreamData']['size'] if streamed else len(tree['m_VertexData']['m_DataSize']))//count
        if stride not in (36,48,64):raise RuntimeError((mid,stride))
        offset=tree['m_StreamData']['offset'] if streamed else 0
        data=resource if streamed else bytearray(tree['m_VertexData']['m_DataSize'])
        for index in indices:
            at=offset+index*stride
            old=struct.unpack_from('<fff',data,at)
            if max(abs(a-b) for a,b in zip(old,h.m_Vertices[index]))>1e-4:
                raise RuntimeError(f'vertex mismatch {mid}/{index}')
            struct.pack_into('<fff',data,at,*position(*old))
        if not streamed:
            tree['m_VertexData']['m_DataSize']=bytes(data);obj.save_typetree(tree)
        report.append(dict(mesh=obj.read().m_Name,id=mid,streamed=streamed,cap_faces=faces,changed_vertices=len(indices)))
    if resource!=original:
        updated=EndianBinaryReader(bytes(resource));updated.flags=bundle.files[resource_name].flags
        bundle.files[resource_name]=updated;bundle.mark_changed()
    env.save(pack='lz4',out_path=str(args.out))
    result=args.out/args.source.name
    verified=UnityPy.load(str(result));saved={o.path_id:o for o in verified.objects}
    for row in report:
        before=MeshHandler(objects[row['id']].read());before.process()
        after=MeshHandler(saved[row['id']].read());after.process()
        count=sum(a!=b for a,b in zip(before.m_Vertices,after.m_Vertices))
        if count!=row['changed_vertices'] or before.get_triangles()!=after.get_triangles():
            raise RuntimeError(f'body verification failed {row["id"]}: {count}')
    for mid in LAMPS:
        before=MeshHandler(objects[mid].read());before.process()
        after=MeshHandler(saved[mid].read());after.process()
        if before.m_Vertices!=after.m_Vertices or before.get_triangles()!=after.get_triangles():
            raise RuntimeError(f'lamp changed {mid}')
    print(json.dumps(dict(output=str(result),body=report,lamp_meshes_unchanged=True),indent=2))

if __name__=='__main__':main()
