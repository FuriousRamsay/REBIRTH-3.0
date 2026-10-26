"""Remove the four misaligned metal ovals in the installed camper body mesh."""
import argparse
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

# Body mesh detail levels, not the shared-parts lamp housings or red lenses.
CAP_MESHES={-6753923948166799871:640,-6045061701533401843:496,8398484439755328957:76}
UNCHANGED_MESHES=(6130890988526752843,-9148037036518840170,-4796251074057068951,
                  -6571958702597769235,209635268596600669,-7204612321527722442)

def cap_vertex(vertex):
    x,y,z=vertex
    return .77<abs(x)<.89 and .56<y<.66 and -3.47<z<-3.40

def visible_caps(helper):
    return sum(1 for sub in helper.get_triangles() for face in sub if len(set(face))==3
               and all(cap_vertex(helper.m_Vertices[i]) for i in face))

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--source',type=Path,required=True)
    parser.add_argument('--out',type=Path,required=True)
    args=parser.parse_args();args.out.mkdir(parents=True,exist_ok=True)
    env=UnityPy.load(str(args.source));objects={o.path_id:o for o in env.objects}
    report=[]
    for mesh_id,expected in CAP_MESHES.items():
        obj=objects[mesh_id];tree=obj.read_typetree()
        mesh=obj.read();helper=MeshHandler(mesh);helper.process()
        if visible_caps(helper)!=expected:raise RuntimeError(f'cap selection changed in {mesh_id}')
        index_size=2 if helper.m_Use16BitIndices else 4
        pack='<H' if index_size==2 else '<I'
        data=bytearray(tree['m_IndexBuffer']);removed=0
        for sub in mesh.m_SubMeshes:
            if int(sub.topology)!=0:raise RuntimeError(f'unexpected topology {sub.topology}')
            first=sub.firstByte//index_size
            for pos in range(first,first+sub.indexCount,3):
                face=helper.m_IndexBuffer[pos:pos+3]
                if len(set(face))==3 and all(cap_vertex(helper.m_Vertices[i]) for i in face):
                    struct.pack_into(pack,data,(pos+1)*index_size,face[0])
                    struct.pack_into(pack,data,(pos+2)*index_size,face[0])
                    removed+=1
        if removed!=expected:raise RuntimeError(f'unsafe cap edit: {removed}/{expected}')
        tree['m_IndexBuffer']=bytes(data);obj.save_typetree(tree)
        report.append(dict(mesh=mesh.m_Name,id=mesh_id,cap_triangles_removed=removed))
    env.save(pack='lz4',out_path=str(args.out));result=args.out/args.source.name
    verified=UnityPy.load(str(result));saved={o.path_id:o for o in verified.objects}
    for row in report:
        helper=MeshHandler(saved[row['id']].read());helper.process()
        if visible_caps(helper)!=0:raise RuntimeError(f"cap geometry remains: {row['mesh']}")
    for mesh_id in UNCHANGED_MESHES:
        before=MeshHandler(objects[mesh_id].read());before.process()
        after=MeshHandler(saved[mesh_id].read());after.process()
        if before.m_Vertices!=after.m_Vertices or before.get_triangles()!=after.get_triangles():
            raise RuntimeError(f'lamp mesh unexpectedly changed: {mesh_id}')
    print(json.dumps(dict(output=str(result),caps=report,lamp_meshes_unchanged=True),indent=2))

if __name__=='__main__':main()
