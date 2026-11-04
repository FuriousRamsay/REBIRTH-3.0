"""Distinct owner-requested runtime comparison copies; production art untouched."""
import copy, hashlib, json, math
from collections import defaultdict
from pathlib import Path
from PIL import Image
from compress_candidate import ROOT, streams, resize, encode, UnityPy, TF, EndianBinaryReader, etcpak, sha

BASE=Path('F:/REBIRTH_Wrap_Optimization/2026-11-04/Comparison')
ORIGINAL=Path('F:/REBIRTH_Wrap_Backups/2026-11-04_PreOptimization/CurrentBundles/rebirth_wrap_navezganeexplorer.unity3d')
CURRENT=ROOT/'Resources/rebirth_wrap_navezganeexplorer.unity3d'
PREFIX='NavezganeExplorer'

def build(percent, production_id=None, output_base=None, production_name=None):
    prefix=production_id or PREFIX
    id=production_id or PREFIX+'Compare'+str(percent);name=production_name or 'rebirth_wrap_'+id.lower()+'.unity3d'
    source=(ORIGINAL.parent/name) if production_id else (CURRENT if percent==100 else ORIGINAL)
    source_hash=sha(source.read_bytes());e=UnityPy.load(str(source));bundle=next(iter(e.files.values()))
    objects={o.path_id:o for o in e.objects};trees={k:o.read_typetree() for k,o in objects.items()}
    old_objects={k:sha(o.get_raw_data()) for k,o in objects.items()}
    replacements={};modified=set();rows=[]
    for k,o in objects.items():
        tree=trees[k]
        if o.type.name=='AssetBundle' and not production_id:
            tree['m_Name']=tree['m_AssetBundleName']=name
            tree['m_Container']=[(path.replace(prefix.lower(),id.lower()),value) for path,value in tree['m_Container']]
            modified.add(k)
        if o.type.name!='Texture2D':continue
        t=o.read();w,h=t.m_Width,t.m_Height
        if percent==100:
            assert TF(t.m_TextureFormat)==TF.BC7
            data=t.get_image_data();tw,th,mips=w,h,t.m_MipCount
        else:
            assert TF(t.m_TextureFormat)==TF.RGBA32
            tw=max(4,math.ceil(w*percent/400)*4);th=max(4,math.ceil(h*percent/400)*4)
            mips=int(math.log2(max(tw,th)))+1
            raw=t.get_image_data();parts=[];params=etcpak.BC7CompressBlockParams();params.m_uber_level=2
            # Resize from the retained uncompressed master, never from BC7.
            master=Image.frombytes('RGBA',(w,h),raw[:w*h*4]).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            for level in range(mips):
                im=resize(master,(max(1,tw>>level),max(1,th>>level)))
                parts.append(encode(im,params))
            data=b''.join(parts)
        ref=tree['m_StreamData']
        if ref['path']:
            assert not tree['image data']
            replacements[(ref['path'].rsplit('/',1)[-1],ref['offset'],ref['size'])]=data
        else:
            tree['image data']=data
        tree.update(m_Name=tree['m_Name'].replace(prefix,id),m_Width=tw,m_Height=th,m_MipCount=mips,
            m_TextureFormat=int(TF.BC7),m_CompleteImageSize=len(data))
        modified.add(k);rows.append(dict(name=tree['m_Name'],width=tw,height=th,mips=mips,bytes=len(data),sha256=sha(data)))
    refs=defaultdict(list)
    for key,tree in trees.items():
        for node in streams(tree):refs[node['path'].rsplit('/',1)[-1]].append((key,node))
    for path,uses in refs.items():
        stream=bundle.files[path];raw=stream.bytes;groups=defaultdict(list)
        for key,node in uses:groups[(node['offset'],node['size'])].append((key,node))
        out=bytearray();cursor=0
        for (offset,size),owners in sorted(groups.items()):
            assert cursor<=offset and offset+size<=len(raw)
            out.extend(raw[cursor:offset]);out.extend(b'\0'*((-len(out))%16));start=len(out)
            data=replacements.get((path,offset,size),raw[offset:offset+size]);out.extend(data)
            for key,node in owners:node.update(offset=start,size=len(data));modified.add(key)
            cursor=offset+size
        out.extend(raw[cursor:]);new=EndianBinaryReader(bytes(out));new.flags=stream.flags;bundle.files[path]=new
    # Independent internal CAB identities allow original and all four variants
    # to coexist, including the runtime's load-new-before-release-old switch.
    names={}
    for path in ([] if production_id else list(bundle.files)):
        stem=path.split('.')[0]
        names[stem]='CAB-'+hashlib.md5((stem+id).encode()).hexdigest()
    for tree in trees.values():
        for node in streams(tree):
            for old,new in names.items():node['path']=node['path'].replace(old,new)
    for key in modified:objects[key].save_typetree(trees[key])
    for path in list(bundle.files):
        value=bundle.files.pop(path);newpath=path
        for old,new in names.items():newpath=newpath.replace(old,new)
        if hasattr(value,'name'):value.name=newpath
        bundle.files[newpath]=value
    dest=(output_base or BASE)/'Runtime/Resources'/name;dest.parent.mkdir(parents=True,exist_ok=True)
    assert not dest.exists(),'Do not overwrite a comparison candidate'
    dest.write_bytes(bundle.save(packer='lz4hc'))
    checked=UnityPy.load(str(dest));current={o.path_id:o for o in checked.objects};assert set(current)==set(objects)
    for key,o in current.items():
        if key in modified:assert o.read_typetree()==trees[key]
        else:assert sha(o.get_raw_data())==old_objects[key],('Shader/layout changed',key)
    for o in checked.objects:
        if o.type.name=='Texture2D':assert o.read().image.size==(o.read().m_Width,o.read().m_Height)
    assert sha(source.read_bytes())==source_hash
    result=dict(id=id,item='modVehicleWrap'+id,bundle=name,percent=percent,bytes=dest.stat().st_size,
        sha256=sha(dest.read_bytes()),source=str(source),sourceSha256=source_hash,format='BC7 HQ2 / LZ4HC',textures=rows)
    dest.with_suffix('.report.json').write_text(json.dumps(result,indent=2))
    print(id,result['bytes'],flush=True);return result

if __name__=='__main__':
    assert sha(CURRENT.read_bytes())=='249c09e0708eb7ebc70217d9ebdf12a7f95feff713efcfdbe86c54dc7e535088'
    assert sha(ORIGINAL.read_bytes())=='2c891c49e6fc69c8587a323c6088440fcaeffc09d8b2489b449a0bebb85a7fac'
    rows=[build(p) for p in (100,75,50,35)]
    current=rows[0]['bytes']
    for row in rows:
        row['reductionPercent']=round(100*(1-row['bytes']/current),2)
        row['estimatedCollectionBytes']=round(1430060681*row['bytes']/current)
    (BASE/'comparison.json').write_text(json.dumps(dict(status='BUILT',variants=rows),indent=2))

