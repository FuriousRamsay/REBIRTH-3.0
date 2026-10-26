import sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
e=UnityPy.load(sys.argv[1]);objs={o.path_id:o for o in e.objects}
target=-6753923948166799871
for o in e.objects:
    if o.type.name!='MeshFilter':continue
    t=o.read_typetree();mesh=t.get('m_Mesh',{}).get('m_PathID')
    if mesh!=target:continue
    go=t['m_GameObject']['m_PathID'];print('meshfilter',o.path_id,'go',go)
    gt=objs[go].read_typetree();print('name',gt['m_Name'])
    for c in gt['m_Component']:
        cid=c['component']['m_PathID'];co=objs[cid]
        print('component',co.type.name,cid)
        if co.type.name=='MeshRenderer':
            rt=co.read_typetree()
            for ref in rt.get('m_Materials',[]):
                mid=ref['m_PathID'];mat=objs[mid];mt=mat.read_typetree();print(' material',mid,mt.get('m_Name'))
                for entry in mt.get('m_SavedProperties',{}).get('m_TexEnvs',[]):
                    print(' texenv',entry)
