"""Export only the resized candidate's non-Crunch DXT textures for native Unity Crunch."""
import sys,json,hashlib
from pathlib import Path
from collections import Counter,defaultdict
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.enums import TextureFormat as TF
ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'_Deliverables/FireEXT_2048_1024_256/Resources/RB_TOOL_FireEXT.unity3d'
PROJECT=ROOT/'Tools/AssetBundle/CrunchProject'
DOC=ROOT/'_Documentation/AssetBundleOptimization/FireEXT'

def main():
    env=UnityPy.load(str(SOURCE));entries=[];duplicates=defaultdict(list);meshes=[];counts=Counter();readable=Counter()
    folder=PROJECT/'Assets/Textures';folder.mkdir(parents=True,exist_ok=True)
    for obj in env.objects:
        if obj.type.name=='Texture2D':
            t=obj.read();raw=t.get_image_data();fmt=TF(t.m_TextureFormat)
            counts[fmt.name]+=1;readable[str(t.m_IsReadable)]+=1
            signature=(t.m_Width,t.m_Height,int(fmt),hashlib.sha256(raw).hexdigest())
            duplicates[signature].append(dict(id=obj.path_id,name=t.m_Name,bytes=len(raw)))
            if fmt in (TF.DXT1,TF.DXT5):
                t.image.save(folder/f'T_{obj.path_id}.png')
                entries.append(dict(id=obj.path_id,width=t.m_Width,height=t.m_Height,format=28 if fmt==TF.DXT1 else 29,mips=t.m_MipCount,srgb=t.m_ColorSpace==1))
        elif obj.type.name=='Mesh':
            tree=obj.read_typetree();meshes.append(dict(id=obj.path_id,name=tree['m_Name'],vertices=tree['m_VertexData']['m_VertexCount'],readable=tree.get('m_IsReadable'),
                serializedBytes=obj.byte_size,streamBytes=tree.get('m_StreamData',{}).get('size',0)))
    plan=dict(source=str(SOURCE),sourceSha256=hashlib.sha256(SOURCE.read_bytes()).hexdigest(),textures=entries)
    (PROJECT/'plan.json').write_text(json.dumps(plan,indent=2),encoding='utf-8')
    audit=dict(textureFormats=dict(counts),textureReadable=dict(readable),duplicatePayloadGroups=[rows for rows in duplicates.values() if len(rows)>1],
        largestMeshes=sorted(meshes,key=lambda m:-(m['serializedBytes']+m['streamBytes']))[:15],meshCount=len(meshes),totalVertices=sum(m['vertices'] for m in meshes))
    (DOC/'further-optimization-audit.json').write_text(json.dumps(audit,indent=2),encoding='utf-8')
    print(json.dumps(dict(texturesToCrunch=len(entries),**audit),indent=2))

if __name__=='__main__':main()
