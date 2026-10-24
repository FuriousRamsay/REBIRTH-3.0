"""Replace warped camper rear caps with two clean bumper-mounted lamp panels.

This consumes the existing cap triangles in each camper-body LOD as triangle
slots, making a beveled bumper insert per side. The bulky original metal
surround faces are removed. The circular red lenses are flattened and seated
almost flush on each insert; their two beam emitters follow them inward.
"""
import argparse
import collections
import json
import math
import struct
import sys
from pathlib import Path

sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
from UnityPy.streams import EndianBinaryReader

BODY={-6753923948166799871:640,-6045061701533401843:496,8398484439755328957:76}
SURROUNDS=(6130890988526752843,-9148037036518840170,-4796251074057068951)
LENSES=(-6571958702597769235,209635268596600669,-7204612321527722442)
INWARD=.0255
FRONT=-3.4305
BACK=-3.398
UV=(.545,.2606)

def cap(v):
    x,y,z=v
    return .77<abs(x)<.89 and .56<y<.66 and -3.47<z<-3.40

def lamp(v):
    x,y,z=v
    return .65<abs(x)<.91 and .50<y<.72 and -3.53<z<-3.38

def normalize(v):
    length=math.sqrt(sum(a*a for a in v))
    return tuple(a/length for a in v)

def normal(a,b,c):
    u=tuple(b[i]-a[i] for i in range(3));v=tuple(c[i]-a[i] for i in range(3))
    return normalize((u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]))

def panel_triangles(side):
    # Same physical dimensions on both ends, mirrored through the vehicle axis.
    xy=[(.675,.572),(.683,.563),(.892,.563),(.900,.572),
        (.900,.653),(.892,.662),(.683,.662),(.675,.653)]
    front=[(side*x,y,FRONT) for x,y in xy]
    back=[(side*x,y,BACK) for x,y in xy]
    triangles=[]
    for i in range(1,7):
        triangles.append((front[0],front[i+1],front[i]))
        triangles.append((back[0],back[i],back[i+1]))
    for i in range(8):
        j=(i+1)%8
        triangles.append((front[i],front[j],back[j]))
        triangles.append((front[i],back[j],back[i]))
    # Mirroring swaps winding; restore outward-facing normals on the left.
    if side<0:triangles=[(a,c,b) for a,b,c in triangles]
    assert len(triangles)==28
    return triangles

def write_vertex(data,index,stride,offset,point,norm,uv=UV):
    at=offset+index*stride
    struct.pack_into('<fff',data,at,*point)
    struct.pack_into('<fff',data,at+12,*norm)
    struct.pack_into('<ffff',data,at+24,1.0,0.0,0.0,1.0)
    struct.pack_into('<ff',data,at+40,*uv)

def main():
    p=argparse.ArgumentParser();p.add_argument('--source',type=Path,required=True);p.add_argument('--out',type=Path,required=True)
    args=p.parse_args();args.out.mkdir(parents=True,exist_ok=True)
    env=UnityPy.load(str(args.source));objects={o.path_id:o for o in env.objects}
    bundle=next(iter(env.files.values()));resname=next(n for n in bundle.files if n.endswith('.resS'))
    raw=bytes(bundle.files[resname].bytes);resource=bytearray(raw)
    report=[]
    for mid,expected in BODY.items():
        obj=objects[mid];tree=obj.read_typetree();h=MeshHandler(obj.read());h.process()
        tris=[f for sub in h.get_triangles() for f in sub if len(set(f))==3 and all(cap(h.m_Vertices[i]) for i in f)]
        if len(tris)!=expected:raise RuntimeError(f'body face selection {mid}: {len(tris)}/{expected}')
        indices=[i for f in tris for i in f]
        if len(indices)!=len(set(indices)):raise RuntimeError(f'cap vertices are shared {mid}')
        count=tree['m_VertexData']['m_VertexCount'];data=bytearray(tree['m_VertexData']['m_DataSize']);stride=len(data)//count
        if stride!=48:raise RuntimeError((mid,stride))
        ib=bytearray(tree['m_IndexBuffer']);isize=2 if h.m_Use16BitIndices else 4
        fmt='<H' if isize==2 else '<I'
        used=0;removed=0
        for side in (-1,1):
            source_faces=[f for f in tris if (h.m_Vertices[f[0]][0]<0)==(side<0)]
            generated=panel_triangles(side)
            if len(source_faces)<len(generated):raise RuntimeError(f'not enough faces {mid}/{side}')
            for f,points in zip(source_faces,generated):
                n=normal(*points)
                for idx,pt in zip(f,points):write_vertex(data,idx,stride,0,pt,n)
                used+=1
            # All unused cap faces are removed by making their indices degenerate.
            unused={tuple(f) for f in source_faces[len(generated):]}
            for sub in obj.read().m_SubMeshes:
                first=sub.firstByte//isize
                for pos in range(first,first+sub.indexCount,3):
                    f=tuple(h.m_IndexBuffer[pos:pos+3])
                    if f in unused:
                        struct.pack_into(fmt,ib,(pos+1)*isize,f[0]);struct.pack_into(fmt,ib,(pos+2)*isize,f[0]);removed+=1
            if removed< len(unused):raise RuntimeError(f'cap removal incomplete {mid}/{side}')
        if used!=56 or removed!=expected-56:raise RuntimeError(f'panel triangle count {mid}: {used}, {removed}')
        tree['m_VertexData']['m_DataSize']=bytes(data);tree['m_IndexBuffer']=bytes(ib);obj.save_typetree(tree)
        report.append(dict(mesh=mid,panels=2,visible_panel_triangles=used,old_cap_triangles_removed=removed))
    for mid in SURROUNDS:
        obj=objects[mid];tree=obj.read_typetree();h=MeshHandler(obj.read());h.process()
        ib=bytearray(tree['m_IndexBuffer']);isize=2 if h.m_Use16BitIndices else 4
        fmt='<H' if isize==2 else '<I';removed=0
        for sub in obj.read().m_SubMeshes:
            first=sub.firstByte//isize
            for pos in range(first,first+sub.indexCount,3):
                f=h.m_IndexBuffer[pos:pos+3]
                if len(set(f))==3 and all(lamp(h.m_Vertices[i]) for i in f):
                    struct.pack_into(fmt,ib,(pos+1)*isize,f[0]);struct.pack_into(fmt,ib,(pos+2)*isize,f[0]);removed+=1
        if removed<80:raise RuntimeError(f'no rear metal surrounds selected {mid}')
        tree['m_IndexBuffer']=bytes(ib);obj.save_typetree(tree)
        report.append(dict(mesh=mid,rear_surround_triangles_removed=removed))
    for mid in LENSES:
        obj=objects[mid];tree=obj.read_typetree();h=MeshHandler(obj.read());h.process()
        indices={i for sub in h.get_triangles() for f in sub if len(set(f))==3 and all(lamp(h.m_Vertices[j]) for j in f) for i in f}
        if not indices:raise RuntimeError(f'no lamp vertices {mid}')
        count=tree['m_VertexData']['m_VertexCount'];streamed=tree['m_StreamData']['size']>0
        stride=(tree['m_StreamData']['size'] if streamed else len(tree['m_VertexData']['m_DataSize']))//count
        offset=tree['m_StreamData']['offset'] if streamed else 0
        data=resource if streamed else bytearray(tree['m_VertexData']['m_DataSize'])
        for index in indices:
            at=offset+index*stride;x,y,z=struct.unpack_from('<fff',data,at)
            if max(abs(a-b) for a,b in zip((x,y,z),h.m_Vertices[index]))>1e-4:raise RuntimeError(f'vertex mismatch {mid}/{index}')
            # The original lens is a deep, vertically tilted dome. Preserve
            # its circular x/y outline and red UVs; flatten its z profile so
            # the red face is parallel to and nearly flush with the insert.
            nz=-3.432+.1*(z+3.451)+.02*(y-.6123)
            struct.pack_into('<fff',data,at,x,y,nz)
        if not streamed:tree['m_VertexData']['m_DataSize']=bytes(data);obj.save_typetree(tree)
        report.append(dict(mesh=mid,lamp_vertices_moved=len(indices),geometry='flattened circular lens seated on bumper insert'))
    if resource!=raw:
        updated=EndianBinaryReader(bytes(resource));updated.flags=bundle.files[resname].flags
        bundle.files[resname]=updated;bundle.mark_changed()
    emitters=[]
    for obj in objects.values():
        if obj.type.name!='Transform':continue
        t=obj.read_typetree();pos=t.get('m_LocalPosition')
        if not pos:continue
        if abs(abs(pos['x'])-.783243)<.0001 and abs(pos['y']-.6131673)<.0001 and abs(pos['z']+3.519111)<.0001:
            t['m_LocalPosition']['z']+=INWARD;obj.save_typetree(t);emitters.append(obj.path_id)
    if len(emitters)!=2:raise RuntimeError(f'rear light emitter selection {emitters}')
    env.save(pack='lz4',out_path=str(args.out));result=args.out/args.source.name
    saved={o.path_id:o for o in UnityPy.load(str(result)).objects}
    for row in report:
        mid=row['mesh'];before=MeshHandler(objects[mid].read());before.process();after=MeshHandler(saved[mid].read());after.process()
        changed=sum(a!=b for a,b in zip(before.m_Vertices,after.m_Vertices))
        if mid in BODY and changed<56*3:raise RuntimeError(f'body positions unchanged {mid}')
        if mid in LENSES and changed!=row['lamp_vertices_moved']:raise RuntimeError(f'lamp change count {mid}: {changed}')
        if mid in SURROUNDS:
            count=sum(len([f for f in sub if len(set(f))==3 and all(lamp(after.m_Vertices[i]) for i in f)]) for sub in after.get_triangles())
            if count:raise RuntimeError(f'metal surround remains {mid}: {count}')
        row['verified_changed_vertices']=changed
    for mid in emitters:
        before=objects[mid].read_typetree()['m_LocalPosition'];after=saved[mid].read_typetree()['m_LocalPosition']
        if abs(after['z']-before['z']-INWARD)>1e-4:raise RuntimeError('emitter move not saved')
    print(json.dumps(dict(output=str(result),report=report,emitters_moved=emitters),indent=2))

if __name__=='__main__':main()
