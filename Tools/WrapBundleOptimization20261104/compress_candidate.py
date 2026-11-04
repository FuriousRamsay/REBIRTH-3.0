"""Texture-only BC7 candidate. Never writes installed bundles or original artwork."""
import argparse, copy, hashlib, json, sys, time
from collections import defaultdict
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT/'Tools/AssetBundle/.python'))
import UnityPy, etcpak
from UnityPy.enums import TextureFormat as TF
from UnityPy.streams import EndianBinaryReader
from PIL import Image

def sha(data): return hashlib.sha256(data).hexdigest()
def streams(node):
    if isinstance(node, dict):
        if all(k in node for k in ('offset','size','path')) and node['path']: yield node
        for value in node.values(): yield from streams(value)
    elif isinstance(node, (list,tuple)):
        for value in node: yield from streams(value)

def resize(im, size):
    if im.size == size: return im
    return Image.merge('RGBA',[c.resize(size,Image.Resampling.LANCZOS) for c in im.split()])

def encode(im, params):
    # Unity texture payload starts at the lower left. Extend only encoder block
    # padding of small mip levels; top level is resampled to four-pixel alignment.
    im=im.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    w,h=im.size
    pw,ph=(w+3)//4*4,(h+3)//4*4
    if (pw,ph)!=(w,h):
        padded=Image.new('RGBA',(pw,ph));padded.paste(im,(0,0))
        if pw>w: padded.paste(im.crop((w-1,0,w,h)).resize((pw-w,h)),(w,0))
        if ph>h: padded.paste(padded.crop((0,h-1,pw,h)).resize((pw,ph-h)),(0,h))
        im=padded
    return etcpak.compress_bc7(im.tobytes(),pw,ph,params)

def main():
    ap=argparse.ArgumentParser();ap.add_argument('name');ap.add_argument('--uber',type=int,default=2)
    ap.add_argument('--output-root',type=Path,default=Path(__file__).parent/'Candidates')
    args=ap.parse_args()
    plan=json.loads((Path(__file__).parent/'backup-result.json').read_text())
    assert plan['status']=='PASS'
    assert args.name in plan['approved_bundles'].values(), 'Only frozen approved bundle names are eligible'
    source=ROOT/'Resources'/args.name
    original=source.read_bytes();before_sha=sha(original)
    manifest=json.loads((Path(plan['destination'])/'VERIFIED_MANIFEST.json').read_text())
    backed=next(r for r in manifest['files'] if r['relative']=='CurrentBundles/'+args.name)
    assert before_sha==backed['sha256'], 'Installed bundle changed since backup'
    dest=args.output_root/('BC7HQ'+str(args.uber))/args.name
    assert not dest.exists(), 'Candidate exists: do not overwrite a reviewed candidate'
    dest.parent.mkdir(parents=True,exist_ok=True)
    env=UnityPy.load(str(source));bundle=next(iter(env.files.values()))
    objects={o.path_id:o for o in env.objects}
    trees={key:o.read_typetree() for key,o in objects.items()}
    raw_hashes={key:sha(o.get_raw_data()) for key,o in objects.items()}
    changes={};rows=[];modified=set();refs=defaultdict(list)
    for key,tree in trees.items():
        for node in streams(tree): refs[node['path'].rsplit('/',1)[-1]].append((key,node))
    params=etcpak.BC7CompressBlockParams();params.m_uber_level=args.uber
    for key,o in objects.items():
        if o.type.name!='Texture2D': continue
        t=o.read();assert TF(t.m_TextureFormat)==TF.RGBA32,(t.m_Name,'Unexpected format requires separate analysis')
        w,h=t.m_Width,t.m_Height;tw,th=(w+3)//4*4,(h+3)//4*4
        raw=t.get_image_data();pieces=[];cursor=0;started=time.time()
        for level in range(t.m_MipCount):
            sw,sh=max(1,w>>level),max(1,h>>level)
            nw,nh=max(1,tw>>level),max(1,th>>level)
            size=sw*sh*4
            # Reuse each authored mip, preserving its filtering rather than
            # silently generating a different mip chain from the largest image.
            im=Image.frombytes('RGBA',(sw,sh),raw[cursor:cursor+size]).transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            cursor+=size
            pieces.append(encode(resize(im,(nw,nh)),params))
        assert cursor==len(raw),(t.m_Name,cursor,len(raw))
        data=b''.join(pieces);tree=trees[key];ref=tree['m_StreamData']
        if tree['image data']:
            assert not ref['path'], 'Unexpected mixed embedded/streamed texture'
            tree['image data']=data
        else:
            assert ref['path'], 'Missing texture payload'
            changes[(ref['path'].rsplit('/',1)[-1],ref['offset'],ref['size'])]=data
        tree.update(m_Width=tw,m_Height=th,m_TextureFormat=int(TF.BC7),m_CompleteImageSize=len(data))
        modified.add(key)
        rows.append(dict(id=key,name=t.m_Name,before=[w,h,len(raw)],after=[tw,th,len(data)],mips=t.m_MipCount,payloadSha256=sha(data)))
        print(t.m_Name,w,h,'=>',tw,th,round(time.time()-started,2),'s',flush=True)
    checks=[]
    for name,uses in refs.items():
        stream=bundle.files[name];raw=stream.bytes;groups=defaultdict(list)
        for key,node in uses: groups[(node['offset'],node['size'])].append((key,node))
        out=bytearray();cursor=0
        for (offset,size),owners in sorted(groups.items()):
            assert cursor<=offset and offset+size<=len(raw)
            out.extend(raw[cursor:offset]);out.extend(b'\0'*((-len(out))%16));start=len(out)
            payload=changes.get((name,offset,size),raw[offset:offset+size]);out.extend(payload)
            for key,node in owners:
                if node['offset']!=start or node['size']!=len(payload): modified.add(key)
                node.update(offset=start,size=len(payload))
            checks.append((name,start,len(payload),sha(payload)));cursor=offset+size
        out.extend(raw[cursor:]);replacement=EndianBinaryReader(bytes(out));replacement.flags=stream.flags;bundle.files[name]=replacement
    for key in modified: objects[key].save_typetree(trees[key])
    dest.write_bytes(bundle.save(packer='lz4hc'))
    check=UnityPy.load(str(dest));cb=next(iter(check.files.values()));co={o.path_id:o for o in check.objects}
    assert set(co)==set(objects) and set(cb.files)==set(bundle.files)
    for key,o in co.items():
        assert o.type.name==objects[key].type.name
        if key in modified: assert o.read_typetree()==trees[key]
        else: assert sha(o.get_raw_data())==raw_hashes[key], ('Nontexture object changed',key)
    for name,offset,size,digest in checks: assert sha(cb.files[name].bytes[offset:offset+size])==digest
    for row in rows:
        t=co[row['id']].read();assert sha(t.get_image_data())==row['payloadSha256'];assert t.image.size==tuple(row['after'][:2])
    assert sha(source.read_bytes())==before_sha
    report=dict(source=str(source),sourceSha256=before_sha,candidate=str(dest),candidateSha256=sha(dest.read_bytes()),
        sourceBytes=len(original),candidateBytes=dest.stat().st_size,profile=dict(format='BC7',uber=args.uber,maxPartitions=64,bundle='LZ4HC',
        dimensions='Round up to multiple of four, channel-wise Lanczos; canonical source unchanged',mips='Retained original mip levels'),
        offlineValidation='PASS',unityGpuValidation='PENDING',textures=rows)
    dest.with_suffix('.report.json').write_text(json.dumps(report,indent=2))
    print(json.dumps({k:v for k,v in report.items() if k!='textures'}),flush=True)

if __name__=='__main__': main()
