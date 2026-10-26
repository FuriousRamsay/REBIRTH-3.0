"""Reshape the four camper rear lamps in their streamed Unity mesh data."""
import argparse
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
from UnityPy.streams import EndianBinaryReader

HOUSINGS = (6130890988526752843, -9148037036518840170, -4796251074057068951)
LENSES = (-6571958702597769235, 209635268596600669, -7204612321527722442)
CENTERS = (-.8277, -.7380, .7380, .8277)

def rear_indices(helper):
    vertices = helper.m_Vertices
    def inside(index):
        x,y,z = vertices[index]
        return .65 < abs(x) < .91 and .50 < y < .72 and -3.53 < z < -3.38
    faces = (face for sub in helper.get_triangles() for face in sub)
    return {index for face in faces if all(inside(index) for index in face) for index in face}

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, default=Path(__file__).resolve().parents[2]/'Resources'/'rebirth_camper80s.unity3d')
    parser.add_argument('--out', type=Path, required=True)
    args = parser.parse_args()
    args.out.mkdir(parents=True,exist_ok=True)
    env = UnityPy.load(str(args.source))
    objects={obj.path_id:obj for obj in env.objects}
    bundle=next(iter(env.files.values()))
    resource_name=next(name for name in bundle.files if name.endswith('.resS'))
    original=bytes(bundle.files[resource_name].bytes)
    resource=bytearray(original)
    report=[]
    for mesh_id in HOUSINGS+LENSES:
        obj=objects[mesh_id]
        tree=obj.read_typetree()
        mesh=obj.read()
        helper=MeshHandler(mesh); helper.process()
        indices=rear_indices(helper)
        if not indices: raise RuntimeError(f'no rear vertices in {mesh.m_Name}')
        count=tree['m_VertexData']['m_VertexCount']
        streamed=tree['m_StreamData']['size']>0
        stride=(tree['m_StreamData']['size'] if streamed else len(tree['m_VertexData']['m_DataSize']))//count
        offset=tree['m_StreamData']['offset'] if streamed else 0
        if stride not in (36,64): raise RuntimeError((mesh.m_Name,stride))
        factor=1.20 if mesh_id in HOUSINGS else 1.10
        depth=-.009 if mesh_id in HOUSINGS else 0
        data=resource if streamed else bytearray(tree['m_VertexData']['m_DataSize'])
        for index in indices:
            p=offset+index*stride
            x,y,z=struct.unpack_from('<fff',data,p)
            reference=helper.m_Vertices[index]
            if max(abs(a-b) for a,b in zip((x,y,z),reference))>1e-4:
                raise RuntimeError(f'position mismatch {mesh.m_Name} {index}')
            center=min(CENTERS,key=lambda c:abs(x-c))
            new=(center+(x-center)*factor,.6123+(y-.6123)*factor,z+depth)
            struct.pack_into('<fff',data,p,*new)
        if not streamed:
            tree['m_VertexData']['m_DataSize']=bytes(data)
            obj.save_typetree(tree)
        report.append(dict(mesh=mesh.m_Name,id=mesh_id,vertices=len(indices),factor=factor,depth=depth,streamed=streamed))
    changed=sum(a!=b for a,b in zip(original,resource))
    updated=EndianBinaryReader(bytes(resource));updated.flags=bundle.files[resource_name].flags
    bundle.files[resource_name]=updated
    bundle.mark_changed()
    env.save(pack='lz4',out_path=str(args.out))
    result=args.out/args.source.name
    verify=UnityPy.load(str(result))
    verify_objects={obj.path_id:obj for obj in verify.objects}
    for row in report:
        old=MeshHandler(objects[row['id']].read());old.process()
        new=MeshHandler(verify_objects[row['id']].read());new.process()
        actual=sum(a!=b for a,b in zip(old.m_Vertices,new.m_Vertices))
        if actual!=row['vertices']: raise RuntimeError(f"verify failed {row['mesh']}: {actual} != {row['vertices']}")
        row['verified_changed_vertices']=actual
    print(json.dumps(dict(output=str(result),changed_bytes=changed,meshes=report),indent=2))

if __name__=='__main__': main()
