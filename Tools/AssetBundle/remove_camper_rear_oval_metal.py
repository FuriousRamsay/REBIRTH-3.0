"""Remove four interfering rear metal ovals, keeping the red lamp lenses."""
import argparse
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

HOUSINGS=(6130890988526752843,-9148037036518840170,-4796251074057068951)
LENSES=(-6571958702597769235,209635268596600669,-7204612321527722442)

def in_oval(vertex):
    x,y,z=vertex
    return .65<abs(x)<.91 and .50<y<.72 and -3.53<z<-3.38

def visible_rear_faces(helper):
    return sum(1 for sub in helper.get_triangles() for face in sub
               if len(set(face))==3 and all(in_oval(helper.m_Vertices[i]) for i in face))

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--out',type=Path,required=True)
    args=parser.parse_args()
    args.out.mkdir(parents=True,exist_ok=True)
    env=UnityPy.load(str(args.source))
    objects={obj.path_id:obj for obj in env.objects}
    report=[]
    for mesh_id in HOUSINGS:
        obj=objects[mesh_id]
        tree=obj.read_typetree()
        helper=MeshHandler(obj.read());helper.process()
        vertices=helper.m_Vertices
        index_size=2 if helper.m_Use16BitIndices else 4
        pack='<H' if index_size==2 else '<I'
        raw=bytearray(tree['m_IndexBuffer'])
        removed=0
        for sub in obj.read().m_SubMeshes:
            if int(sub.topology)!=0: raise RuntimeError(f'unexpected topology {sub.topology}')
            first=sub.firstByte//index_size
            for pos in range(first,first+sub.indexCount,3):
                face=helper.m_IndexBuffer[pos:pos+3]
                if len(face)!=3: raise RuntimeError('partial triangle')
                if len(set(face))==3 and all(in_oval(vertices[i]) for i in face):
                    # A degenerate triangle has no rendered area. Keep the index
                    # buffer length and unrelated submesh offsets unchanged.
                    struct.pack_into(pack,raw,(pos+1)*index_size,face[0])
                    struct.pack_into(pack,raw,(pos+2)*index_size,face[0])
                    removed+=1
        before=visible_rear_faces(helper)
        if removed!=before or removed<40: raise RuntimeError(f'unsafe oval selection {mesh_id}: {removed}/{before}')
        tree['m_IndexBuffer']=bytes(raw)
        obj.save_typetree(tree)
        report.append(dict(mesh=helper.src.m_Name,id=mesh_id,removed_triangles=removed))
    env.save(pack='lz4',out_path=str(args.out))
    result=args.out/args.source.name
    verify=UnityPy.load(str(result));saved={obj.path_id:obj for obj in verify.objects}
    for row in report:
        helper=MeshHandler(saved[row['id']].read());helper.process()
        if visible_rear_faces(helper): raise RuntimeError(f"oval faces remain in {row['mesh']}")
    for mesh_id in LENSES:
        old=MeshHandler(objects[mesh_id].read());old.process()
        new=MeshHandler(saved[mesh_id].read());new.process()
        if old.m_Vertices!=new.m_Vertices or old.get_triangles()!=new.get_triangles():
            raise RuntimeError(f'red lens changed: {mesh_id}')
    print(json.dumps(dict(output=str(result),housing_meshes=report,lenses_unchanged=True),indent=2))

if __name__=='__main__': main()
