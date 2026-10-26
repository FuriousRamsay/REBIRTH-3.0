"""Produce and independently reopen a texture-only FireEXT bundle candidate.

Requires UnityPy 1.25.4 and Pillow in .python beside this script. Never overwrites
the source. Preserves streamed mesh bytes while compacting the shared .resS.
"""
import sys, json, hashlib, math, copy
from pathlib import Path
from collections import Counter, defaultdict
sys.path.insert(0, str(Path(__file__).parent / '.python'))
import UnityPy
from UnityPy.enums import TextureFormat as TF
from UnityPy.export.Texture2DConverter import image_to_texture2d
from UnityPy.streams import EndianBinaryReader
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'Resources/RB_TOOL_FireEXT.unity3d'
OUT = ROOT / '_Deliverables/FireEXT_2048_1024_256'
DOC = ROOT / '_Documentation/AssetBundleOptimization/FireEXT'
EXPECTED_SOURCE = '15cdc0526ba661abf308e0c93661f73504ae8cc10847189cd7fb7060e412b7e3'

def sha(data):
    return hashlib.sha256(data).hexdigest()

def stream_nodes(tree):
    if isinstance(tree, dict):
        if all(k in tree for k in ('offset', 'size', 'path')) and tree['path']:
            yield tree
        for value in tree.values():
            yield from stream_nodes(value)
    elif isinstance(tree, (list, tuple)):
        for value in tree:
            yield from stream_nodes(value)

def block_size(fmt, width, height):
    return max(1, (width+3)//4) * max(1, (height+3)//4) * (8 if fmt == TF.DXT1 else 16)

def channel_resize(image, size):
    # DXT5 normals can pack X in alpha and Y in green. Pillow's normal RGBA
    # resize premultiplies colors by alpha; process channels independently.
    return Image.merge('RGBA', [band.resize(size, Image.Resampling.LANCZOS) for band in image.convert('RGBA').split()])

def resize_texture(texture, raw, maximum):
    width, height = texture.m_Width, texture.m_Height
    fmt = TF(texture.m_TextureFormat)
    skip = 0
    while max(width, height) > maximum:
        width, height = max(1,width//2), max(1,height//2)
        skip += 1
    if not skip:
        return raw, width, height, fmt, texture.m_MipCount, 'unchanged'
    if fmt in (TF.DXT1, TF.DXT5) and texture.m_MipCount > skip:
        offset = sum(block_size(fmt,max(1,texture.m_Width>>i),max(1,texture.m_Height>>i)) for i in range(skip))
        result = raw[offset:]
        mips = texture.m_MipCount-skip
        expected = sum(block_size(fmt,max(1,width>>i),max(1,height>>i)) for i in range(mips))
        assert len(result) == expected, (texture.m_Name,'native mip-chain length')
        return result,width,height,fmt,mips,'existing native mip chain (no recompression)'
    assert fmt in (TF.DXT1Crunched,TF.DXT5Crunched,TF.DXT1,TF.DXT5,TF.RGBA32), fmt
    target = {TF.DXT1Crunched:TF.DXT1,TF.DXT5Crunched:TF.DXT5}.get(fmt,fmt)
    # UnityPy does not encode Crunch. Changed Crunch textures use the same
    # underlying DXT block format, with LZ4HC compression at bundle level.
    mip_count = int(math.log2(max(width,height)))+1 if texture.m_MipCount>1 else 1
    image = channel_resize(texture.image,(width,height))
    pieces = []
    for level in range(mip_count):
        current = image if level==0 else channel_resize(image,(max(1,width>>level),max(1,height>>level)))
        data, actual_fmt = image_to_texture2d(current,target,texture.object_reader.platform,texture.m_PlatformBlob)
        assert actual_fmt == target
        pieces.append(data)
    return b''.join(pieces),width,height,target,mip_count,'channel-wise Lanczos; DXT encode; full mip chain'

def main():
    DOC.mkdir(parents=True,exist_ok=True)
    (OUT/'Resources').mkdir(parents=True,exist_ok=True)
    original_bytes=SOURCE.read_bytes()
    assert sha(original_bytes)==EXPECTED_SOURCE, 'Source changed since inspection; inspect it again first'
    env=UnityPy.load(str(SOURCE)); bundle=next(iter(env.files.values()))
    objects={obj.path_id:obj for obj in env.objects}
    trees={key:obj.read_typetree() for key,obj in objects.items()}
    raw_objects={key:sha(obj.get_raw_data()) for key,obj in objects.items()}
    expected_trees={}
    uses=defaultdict(list)
    for key,obj in objects.items():
        if obj.type.name=='Material':
            for prop,value in trees[key]['m_SavedProperties']['m_TexEnvs']:
                ref=value['m_Texture']
                if ref['m_PathID'] and not ref['m_FileID']:
                    uses[ref['m_PathID']].append(dict(material=trees[key]['m_Name'],property=prop))
    references=defaultdict(list)
    for key,tree in trees.items():
        for node in stream_nodes(tree):
            name=node['path'].rsplit('/',1)[-1]
            assert name in bundle.files, ('External stream requires separate handling',name)
            references[name].append((key,node))
    texture_changes={}; entries=[]; resized=set()
    for key,obj in objects.items():
        if obj.type.name!='Texture2D':continue
        texture=obj.read(); raw=texture.get_image_data(); props={u['property'] for u in uses[key]}
        assert props, ('Texture role unavailable',texture.m_Name)
        normal=bool(props & {'_BumpMap','_Normal','_DetailNormalMap'})
        albedo=bool(props & {'_MainTex','_BaseMap','_BaseColorMap','_DetailAlbedoMap'})
        assert not(normal and albedo), ('Mixed normal/color roles need separate textures',texture.m_Name)
        role,maximum=('normal',1024) if normal else ('albedo',2048) if albedo else ('other',256)
        data,width,height,fmt,mips,method=resize_texture(texture,raw,maximum)
        row=dict(id=key,name=texture.m_Name,role=role,maximum=maximum,uses=uses[key],
            before=dict(width=texture.m_Width,height=texture.m_Height,format=TF(texture.m_TextureFormat).name,mips=texture.m_MipCount,bytes=len(raw)),
            after=dict(width=width,height=height,format=fmt.name,mips=mips,bytes=len(data)),method=method,payloadSha256=sha(data))
        entries.append(row)
        if method!='unchanged':
            resized.add(key)
            node=trees[key]
            node.update(m_Width=width,m_Height=height,m_TextureFormat=int(fmt),m_MipCount=mips,m_CompleteImageSize=len(data))
            if 'm_MipMap' in node:node['m_MipMap']=mips>1
            assert not node['image data'], 'Expected original streamed texture'
            ref=node['m_StreamData']; texture_changes[(ref['path'].rsplit('/',1)[-1],ref['offset'],ref['size'])]=data
        print(f"{texture.m_Name}: {row['before']['width']} -> {width} ({role})",flush=True)
    stream_checks=[]; modified=set(resized)
    for name,refs in references.items():
        source=bundle.files[name]; original=source.bytes
        segments=defaultdict(list)
        for key,node in refs:segments[(node['offset'],node['size'])].append((key,node))
        output=bytearray();cursor=0
        for (offset,size),owners in sorted(segments.items()):
            assert offset>=cursor and offset+size<=len(original), 'Overlapping/out-of-range resource references'
            output.extend(original[cursor:offset]) # preserve unknown inter-segment bytes
            output.extend(b'\0'*((-len(output))%16))
            new_offset=len(output)
            replacement=texture_changes.get((name,offset,size))
            payload=original[offset:offset+size] if replacement is None else replacement
            output.extend(payload)
            for key,node in owners:
                if node['offset']!=new_offset or node['size']!=len(payload):modified.add(key)
                node['offset']=new_offset;node['size']=len(payload)
                stream_checks.append(dict(id=key,path=node['path'],offset=new_offset,size=len(payload),sha256=sha(payload)))
            cursor=offset+size
        output.extend(original[cursor:])
        replacement=EndianBinaryReader(bytes(output));replacement.flags=source.flags;bundle.files[name]=replacement
    for key in modified:
        objects[key].save_typetree(trees[key]);expected_trees[key]=copy.deepcopy(trees[key])
    destination=OUT/'Resources'/SOURCE.name
    assert not destination.exists(), 'Candidate already exists; do not overwrite a reviewed artifact'
    destination.write_bytes(bundle.save(packer='lz4hc'))
    print('Reopening and validating candidate...',flush=True)
    check=UnityPy.load(str(destination));check_bundle=next(iter(check.files.values()))
    current={obj.path_id:obj for obj in check.objects}
    assert set(current)==set(objects), 'Object identities changed'
    assert Counter(o.type.name for o in current.values())==Counter(o.type.name for o in objects.values())
    assert check_bundle.version_engine==bundle.version_engine
    assert set(check_bundle.files)==set(bundle.files)
    for key,obj in current.items():
        assert obj.type.name==objects[key].type.name
        if key in modified:assert obj.read_typetree()==expected_trees[key], ('Modified object roundtrip mismatch',key)
        else:assert sha(obj.get_raw_data())==raw_objects[key], ('Unrelated object changed',key)
    for row in stream_checks:
        name=row['path'].rsplit('/',1)[-1];data=check_bundle.files[name].bytes[row['offset']:row['offset']+row['size']]
        assert sha(data)==row['sha256'], ('Resource postimage mismatch',row['id'])
    for name,file in bundle.files.items():
        if name not in references and not hasattr(file,'objects'):
            assert sha(check_bundle.files[name].bytes)==sha(file.bytes), ('Unrelated resource changed',name)
    for entry in entries:
        texture=current[entry['id']].read();after=entry['after']
        assert (texture.m_Width,texture.m_Height,TF(texture.m_TextureFormat).name,texture.m_MipCount)==(after['width'],after['height'],after['format'],after['mips'])
        assert max(texture.m_Width,texture.m_Height)<=entry['maximum']
        assert sha(texture.get_image_data())==entry['payloadSha256']
        assert texture.image.size==(after['width'],after['height']), ('Texture decode failed',entry['name'])
    assert sha(SOURCE.read_bytes())==EXPECTED_SOURCE, 'Original unexpectedly changed'
    result=dict(source=str(SOURCE),candidate=str(destination),sourceSha256=EXPECTED_SOURCE,candidateSha256=sha(destination.read_bytes()),
        sourceBytes=len(original_bytes),candidateBytes=destination.stat().st_size,resizedTextures=len(resized),
        texturePayloadBefore=sum(e['before']['bytes'] for e in entries),texturePayloadAfter=sum(e['after']['bytes'] for e in entries),
        validation=dict(reopened=True,allTexturesDecoded=True,objectIdsAndTypesPreserved=True,unchangedObjectsByteIdentical=True,
            meshPayloadsByteIdentical=True,audioResourceByteIdentical=True,streamRangesChecked=len(stream_checks),sourceUnchanged=True,
            unityEditorLoad='NOT_RUN',game='NOT_RUN'),textures=entries)
    (DOC/'optimized-inventory.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
    print(json.dumps({k:v for k,v in result.items() if k!='textures'},indent=2),flush=True)

if __name__=='__main__':main()
