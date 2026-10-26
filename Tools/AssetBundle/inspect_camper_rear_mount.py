"""Summarize the exact bumper-cap and lamp surface geometry in the source bundle."""
import collections
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

source = Path(sys.argv[1])
env = UnityPy.load(str(source))
objects = {o.path_id: o for o in env.objects}
for name, mid in [('body', -6753923948166799871), ('lens', -6571958702597769235), ('surround', 6130890988526752843)]:
    h = MeshHandler(objects[mid].read()); h.process(); v = h.m_Vertices
    print('\n', name, 'verts', len(v), 'submeshes', len(h.get_triangles()))
    for si, sub in enumerate(h.get_triangles()):
        faces = [f for f in sub if len(set(f)) == 3 and all(.65 < abs(v[i][0]) < .94 and .53 < v[i][1] < .69 and -3.54 < v[i][2] < -3.36 for i in f)]
        if not faces: continue
        ids = {i for f in faces for i in f}
        z = collections.Counter(round(v[i][2], 3) for i in ids)
        x = collections.Counter(round(abs(v[i][0]), 3) for i in ids)
        print('sub', si, 'faces', len(faces), 'verts', len(ids), 'bounds',
              [(round(min(v[i][a] for i in ids),4),round(max(v[i][a] for i in ids),4)) for a in range(3)])
        print('  z', z.most_common(18))
        print('  x', x.most_common(18))
        if name == 'body':
            leftover=[f for f in faces if not all(.77<abs(v[i][0])<.89 and .56<v[i][1]<.66 and -3.47<v[i][2]<-3.40 for i in f)]
            print('  leftover',len(leftover))
            for f in leftover[:20]: print('   ',[tuple(round(q,4) for q in v[i]) for i in f])
            for lo,hi in ((.775,.83),(.83,.89)):
                ids2={i for f in faces for i in f if lo<abs(v[i][0])<hi and .56<v[i][1]<.66 and -3.47<v[i][2]<-3.40}
                perz=collections.defaultdict(list)
                for i in ids2: perz[round(v[i][2],4)].append(i)
                print('  cap region',lo,hi)
                for zkey,izz in sorted(perz.items()):
                    if len(izz)>2: print('   ',zkey,len(izz),[(round(min(v[i][a] for i in izz),4),round(max(v[i][a] for i in izz),4)) for a in (0,1)])
