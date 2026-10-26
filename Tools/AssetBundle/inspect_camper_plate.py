import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy

bundle = Path(__file__).resolve().parents[2] / 'Resources' / 'rebirth_camper80s.unity3d'
env = UnityPy.load(str(bundle))
names = {}
gameobjects = {}
for obj in env.objects:
    if obj.type.name == 'GameObject':
        data = obj.read()
        names[obj.path_id] = data.m_Name
        gameobjects[obj.path_id] = data
for obj in env.objects:
    if obj.type.name != 'MeshFilter':
        continue
    data = obj.read()
    name = names.get(data.m_GameObject.path_id)
    if name and name in ('licence_plate', 'Original rear license plate', 'Camper original plate 0'):
        mesh = data.m_Mesh.read()
        print(name, 'meshfilter', obj.path_id, 'mesh', data.m_Mesh.path_id,
              mesh.m_Name, 'vertices', mesh.m_VertexData.m_VertexCount,
              'channels', [(c.stream,c.offset,c.format,c.dimension) for c in mesh.m_VertexData.m_Channels])
        print(' attrs', [x for x in dir(mesh) if 'vert' in x.lower() or 'data' in x.lower()][:25],
              'dataLen', len(mesh.m_VertexData.m_DataSize),
              'stream', mesh.m_VertexData.m_Streams,
              'first', bytes(mesh.m_VertexData.m_DataSize[:24]).hex())
        import struct
        raw = mesh.m_VertexData.m_DataSize
        points = [struct.unpack_from('<fff', raw, i * 48) for i in range(mesh.m_VertexData.m_VertexCount)]
        print(' rawBounds', [(min(p[a] for p in points), max(p[a] for p in points)) for a in range(3)])
        print(' halves', [(k, [(min(p[a] for p in points[k:k+64]), max(p[a] for p in points[k:k+64])) for a in range(3)]) for k in (0,64)] if len(points)==128 else '')
        go = gameobjects[data.m_GameObject.path_id]
        print(' active', go.m_IsActive, 'components', [(type(c).__name__, getattr(c, 'path_id', None), getattr(getattr(c, 'component', None), 'path_id', None)) for c in go.m_Component[:4]])
        for component in go.m_Component:
            cp = component.component if hasattr(component, 'component') else component
            comp = cp.read()
            if comp.__class__.__name__ == 'Transform':
                print(' transform', cp.path_id, 'pos', comp.m_LocalPosition, 'rot', comp.m_LocalRotation, 'scale', comp.m_LocalScale)
                ancestor = comp
                for _ in range(6):
                    if not ancestor.m_Father or ancestor.m_Father.path_id == 0:
                        break
                    ancestor = ancestor.m_Father.read()
                    print('  parent', names.get(ancestor.m_GameObject.path_id), ancestor.m_LocalPosition)
            if comp.__class__.__name__ == 'MeshRenderer':
                print(' renderer', cp.path_id, 'enabled', comp.m_Enabled)
