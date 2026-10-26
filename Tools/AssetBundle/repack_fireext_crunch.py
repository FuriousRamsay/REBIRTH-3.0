"""Splice native Unity Crunch payloads into the verified resized bundle."""
import sys,json,hashlib,copy
from pathlib import Path
from collections import Counter,defaultdict
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.enums import TextureFormat as TF
from UnityPy.streams import EndianBinaryReader
from optimize_fireext import stream_nodes
ROOT=Path(__file__).resolve().parents[2]
PROJECT=ROOT/'Tools/AssetBundle/CrunchProject'
DOC=ROOT/'_Documentation/AssetBundleOptimization/FireEXT'
OUT=ROOT/'_Deliverables/FireEXT_2048_1024_256_Crunch'

def sha(data):return hashlib.sha256(data).hexdigest()

def main():
    plan=json.loads((PROJECT/'plan.json').read_text(encoding='utf-8'))
    receipt=json.loads((PROJECT/'CrunchOutput/receipt.json').read_text(encoding='utf-8-sig'))
    assert receipt['textures']==plan['textures']
    source=Path(plan['source']);assert sha(source.read_bytes())==plan['sourceSha256']
    env=UnityPy.load(str(source));bundle=next(iter(env.files.values()))
    objects={o.path_id:o for o in env.objects};trees={key:o.read_typetree() for key,o in objects.items()}
    original_hashes={key:sha(o.get_raw_data()) for key,o in objects.items()}
    textures={};changes={};modified=set();refs=defaultdict(list)
    for entry in plan['textures']:
        key=entry['id'];tree=trees[key];texture=objects[key].read()
        assert (texture.m_Width,texture.m_Height,texture.m_MipCount)==(entry['width'],entry['height'],entry['mips'])
        data=(PROJECT/'CrunchOutput'/f'{key}.bin').read_bytes();assert data
        stream=tree['m_StreamData'];name=stream['path'].rsplit('/',1)[-1]
        changes[(name,stream['offset'],stream['size'])]=data
        tree['m_TextureFormat']=entry['format'];tree['m_CompleteImageSize']=len(data)
        modified.add(key)
    for key,tree in trees.items():
        for node in stream_nodes(tree):refs[node['path'].rsplit('/',1)[-1]].append((key,node))
    expected_payloads=[]
    for name,rows in refs.items():
        previous=bundle.files[name];raw=previous.bytes;segments=defaultdict(list)
        for key,node in rows:segments[(node['offset'],node['size'])].append((key,node))
        data=bytearray();cursor=0
        for (offset,size),owners in sorted(segments.items()):
            assert offset>=cursor and offset+size<=len(raw)
            data.extend(raw[cursor:offset]);data.extend(b'\0'*((-len(data))%16));new_offset=len(data)
            payload=changes.get((name,offset,size),raw[offset:offset+size]);data.extend(payload)
            for key,node in owners:
                if node['offset']!=new_offset or node['size']!=len(payload):modified.add(key)
                node['offset']=new_offset;node['size']=len(payload)
                expected_payloads.append((key,name,new_offset,len(payload),sha(payload)))
            cursor=offset+size
        data.extend(raw[cursor:]);replacement=EndianBinaryReader(bytes(data));replacement.flags=previous.flags;bundle.files[name]=replacement
    expected={}
    for key in modified:objects[key].save_typetree(trees[key]);expected[key]=copy.deepcopy(trees[key])
    destination=OUT/'Resources/RB_TOOL_FireEXT.unity3d';destination.parent.mkdir(parents=True,exist_ok=True)
    assert not destination.exists(), 'Do not overwrite a reviewed candidate'
    destination.write_bytes(bundle.save(packer='lz4hc'))
    check=UnityPy.load(str(destination));checked={o.path_id:o for o in check.objects};checked_bundle=next(iter(check.files.values()))
    assert set(objects)==set(checked) and bundle.version_engine==checked_bundle.version_engine
    for key,obj in checked.items():
        assert obj.type.name==objects[key].type.name
        if key in expected:assert obj.read_typetree()==expected[key],key
        else:assert sha(obj.get_raw_data())==original_hashes[key],key
        if obj.type.name=='Texture2D':
            t=obj.read();assert t.image.size==(t.m_Width,t.m_Height)
            textures[key]=dict(id=key,name=t.m_Name,width=t.m_Width,height=t.m_Height,mips=t.m_MipCount,format=TF(t.m_TextureFormat).name,
                bytes=len(t.get_image_data()),sha256=sha(t.get_image_data()))
    for key,name,offset,size,digest in expected_payloads:
        assert sha(checked_bundle.files[name].bytes[offset:offset+size])==digest,key
    for name,file in bundle.files.items():
        if name not in refs and not hasattr(file,'objects'):assert sha(checked_bundle.files[name].bytes)==sha(file.bytes)
    assert len(textures)==103 and all(t['format'] in ('DXT1Crunched','DXT5Crunched','RGBA32') for t in textures.values())
    assert sum('Crunched' in t['format'] for t in textures.values())==102
    result=dict(source=str(source),sourceSha256=plan['sourceSha256'],candidate=str(destination),candidateSha256=sha(destination.read_bytes()),
        editor=receipt['editor'],sourceBytes=source.stat().st_size,candidateBytes=destination.stat().st_size,
        convertedTextures=len(plan['textures']),texturePayloadBytes=sum(t['bytes'] for t in textures.values()),
        textureFormats=dict(Counter(t['format'] for t in textures.values())),textures=list(textures.values()),
        validation=dict(allTexturesDecoded=True,objectIdsAndTypesPreserved=True,unrelatedObjectBytesPreserved=True,meshPayloadsPreserved=True,audioPreserved=True,
            checkedResourceRanges=len(expected_payloads),originalBundleUntouched=True,game='NOT_RUN'))
    (DOC/'crunch-inventory.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='textures'},indent=2))

if __name__=='__main__':main()
