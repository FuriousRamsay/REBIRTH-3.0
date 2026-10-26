"""Read-only inventory of the original extinguisher bundle."""
import sys, json, hashlib
from pathlib import Path
from collections import Counter, defaultdict
sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.enums import TextureFormat

root = Path(__file__).resolve().parents[2]
source = root / 'Resources/RB_TOOL_FireEXT.unity3d'
out = root / '_Documentation/AssetBundleOptimization/FireEXT'
out.mkdir(parents=True, exist_ok=True)
env = UnityPy.load(str(source))
uses = defaultdict(list)
for obj in env.objects:
    if obj.type.name == 'Material':
        mat = obj.read_typetree()
        for prop, value in mat['m_SavedProperties']['m_TexEnvs']:
            ref = value['m_Texture']
            if ref['m_PathID'] and not ref['m_FileID']:
                uses[ref['m_PathID']].append({'material':mat['m_Name'], 'property':prop})
textures, sizes = [], Counter()
for obj in env.objects:
    sizes[obj.type.name] += obj.byte_size
    if obj.type.name != 'Texture2D':
        continue
    t = obj.read()
    textures.append(dict(id=obj.path_id, name=t.m_Name, width=t.m_Width, height=t.m_Height,
        format=TextureFormat(t.m_TextureFormat).name, mipCount=t.m_MipCount,
        payloadBytes=len(t.get_image_data()), uses=uses[obj.path_id]))
report = dict(source=str(source), sourceBytes=source.stat().st_size,
    sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
    objectCounts=dict(Counter(o.type.name for o in env.objects)),
    serializedObjectBytes=dict(sizes), texturePayloadBytes=sum(t['payloadBytes'] for t in textures),
    textures=sorted(textures,key=lambda t:-t['payloadBytes']))
(out/'original-inventory.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k!='textures'},indent=2))
print('LARGEST',json.dumps(report['textures'][:5],indent=2))
