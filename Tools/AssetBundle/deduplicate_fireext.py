"""Share duplicate native texture payloads; unify identical Miner references."""
import sys,json,hashlib,copy,zipfile
from pathlib import Path
from collections import defaultdict
sys.path.insert(0,str(Path(__file__).parent/'.python'))
import UnityPy
from UnityPy.streams import EndianBinaryReader
from optimize_fireext import stream_nodes
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'_Documentation/AssetBundleOptimization/FireEXT'
OUT=ROOT/'_Deliverables/FireEXT_2048_1024_256_Crunch_Deduplicated'
def sha(data):return hashlib.sha256(data).hexdigest()
def digest(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def pointers(tree):
    if isinstance(tree,dict):
        if set(tree)=={'m_FileID','m_PathID'}:yield tree
        for value in tree.values():yield from pointers(value)
    elif isinstance(tree,(list,tuple)):
        for value in tree:yield from pointers(value)
def main():
    prior=json.loads((DOC/'crunch-inventory.json').read_text())
    source=Path(prior['candidate']);assert digest(source)==prior['candidateSha256']
    original=ROOT/'Resources/RB_TOOL_FireEXT.unity3d'
    assert digest(original)=='15cdc0526ba661abf308e0c93661f73504ae8cc10847189cd7fb7060e412b7e3'
    env=UnityPy.load(str(source));bundle=next(iter(env.files.values()))
    objects={o.path_id:o for o in env.objects};trees={k:o.read_typetree() for k,o in objects.items()}
    before=copy.deepcopy(trees);rawhash={k:sha(o.get_raw_data()) for k,o in objects.items()}
    textures={k:sha(o.read().get_image_data()) for k,o in objects.items() if o.type.name=='Texture2D'}
    aliases={100:98,120:113};shared={};differences={}
    for duplicate,canonical in aliases.items():
        assert textures[duplicate]==textures[canonical]
        a,b=trees[duplicate],trees[canonical]
        differences[str(duplicate)]=[key for key in a if key!='m_StreamData' and a[key]!=b[key]]
        if duplicate==120:assert not differences[str(duplicate)]
        stream=a['m_StreamData'];base=b['m_StreamData']
        shared[(stream['path'].rsplit('/',1)[-1],stream['offset'],stream['size'])]=(base['path'].rsplit('/',1)[-1],base['offset'],base['size'])
    redirected=[]
    for key,tree in trees.items():
        for pointer in pointers(tree):
            if pointer['m_FileID']==0 and pointer['m_PathID']==120:
                pointer['m_PathID']=113;redirected.append(key)
    assert redirected,'Expected actual references to duplicate Miner texture'
    refs=defaultdict(list)
    for key,tree in trees.items():
        for node in stream_nodes(tree):refs[node['path'].rsplit('/',1)[-1]].append((key,node))
    expected_ranges=[];saved=0
    for name,rows in refs.items():
        previous=bundle.files[name];raw=previous.bytes;segments=defaultdict(list)
        for key,node in rows:segments[(node['offset'],node['size'])].append((key,node))
        output=bytearray();cursor=0;locations={}
        for (offset,size),owners in sorted(segments.items()):
            assert offset>=cursor and offset+size<=len(raw)
            output.extend(raw[cursor:offset]);identity=(name,offset,size)
            if identity in shared:
                assert shared[identity] in locations
                new_offset=locations[shared[identity]];saved+=size
                assert raw[offset:offset+size]==bytes(output[new_offset:new_offset+size])
            else:
                output.extend(b'\0'*((-len(output))%16));new_offset=len(output);output.extend(raw[offset:offset+size])
            locations[identity]=new_offset
            for key,node in owners:
                node['offset']=new_offset
                expected_ranges.append((key,name,new_offset,size,sha(raw[offset:offset+size])))
            cursor=offset+size
        output.extend(raw[cursor:]);replacement=EndianBinaryReader(bytes(output));replacement.flags=previous.flags;bundle.files[name]=replacement
    changed={k for k in trees if trees[k]!=before[k]}
    for key in changed:objects[key].save_typetree(trees[key])
    candidate=OUT/'Resources/RB_TOOL_FireEXT.unity3d';candidate.parent.mkdir(parents=True,exist_ok=True)
    assert not candidate.exists(),'Never overwrite a prior delivery'
    candidate.write_bytes(bundle.save(packer='lz4hc'))
    check=UnityPy.load(str(candidate));checked={o.path_id:o for o in check.objects};checked_bundle=next(iter(check.files.values()))
    assert set(checked)==set(objects)
    for key,obj in checked.items():
        assert obj.type.name==objects[key].type.name
        assert obj.read_typetree()==trees[key],key
        if key not in changed:assert sha(obj.get_raw_data())==rawhash[key],key
        if key in textures:
            t=obj.read();assert sha(t.get_image_data())==textures[key]
            assert t.image.size==(t.m_Width,t.m_Height)
        assert all(p['m_FileID']!=0 or p['m_PathID']!=120 for p in pointers(obj.read_typetree()))
    for key,name,offset,size,d in expected_ranges:assert sha(checked_bundle.files[name].bytes[offset:offset+size])==d,key
    for name,file in bundle.files.items():
        if name not in refs and not hasattr(file,'objects'):assert sha(checked_bundle.files[name].bytes)==sha(file.bytes)
    assert digest(source)==prior['candidateSha256']
    assert digest(original)=='15cdc0526ba661abf308e0c93661f73504ae8cc10847189cd7fb7060e412b7e3'
    result=dict(source=str(source),sourceSha256=digest(source),candidate=str(candidate),candidateSha256=digest(candidate),
        sourceBytes=source.stat().st_size,candidateBytes=candidate.stat().st_size,duplicateStreamBytesRemoved=saved,
        payloadAliases=aliases,metadataDifferences=differences,minerReferenceOwners=redirected,
        validation=dict(all103TexturesDecoded=True,allTexturePayloadsIdentical=True,allObjectIdsPreserved=True,
            unrelatedObjectBytesPreserved=True,streamRanges=len(expected_ranges),allMeshAndAudioPayloadsPreserved=True,
            noLocalReferencesToMinerAlias=True,originalAndPriorCandidateUnchanged=True,unityBundleLoad='NOT_RUN',game='NOT_RUN'))
    (DOC/'deduplicated-inventory.json').write_text(json.dumps(result,indent=2))
    report=f'''# FireEXT duplicate texture correction

Changed-files delivery for current REBIRTH 3.0 / 7 Days to Die 3.0 b259.
No character or gear variant has been established as unused or removed.

Starting Crunch bundle: {result['sourceBytes']:,} bytes.
Deduplicated bundle: {result['candidateBytes']:,} bytes.
Duplicate texture stream bytes removed: {saved:,}.

Ranger body IDs 98/100 have identical Crunch payloads but different filtering,
anisotropy and mip streaming. They now share one payload range; both texture
objects and their distinct settings remain. Their material references are unchanged.
Miner hands IDs 113/120 have identical full texture metadata and payloads.
All {len(redirected)} local pointer occurrences in bundle objects {sorted(set(redirected))}
now use ID 113. ID 120 remains as an unreferenced compatibility alias with shared
payload, preserving any external asset ID users. No object ID was removed.
This unifies this bundle's Miner texture usage; loading the alias explicitly can
still instantiate a separate texture. Ranger GPU objects intentionally remain
distinct to preserve their actual sampler and streaming behavior.

All 103 textures independently decode and remain byte-identical to the preceding
Crunch candidate. Texture dimensions/formats and all nonpointer/nonoffset metadata
remain unchanged. {len(expected_ranges)} resource ranges retain their exact payload
hashes, including all streamed meshes. Audio unchanged. All object IDs/types and
unchanged raw serialized objects verified. Complete modified typetrees roundtrip.
Original and prior candidate SHA256 rechecked unchanged. Not installed.
Unity loading and in-game rendering: NOT RUN.

| Requirement | Evidence | Status |
| --- | --- | --- |
| Verify duplicate compatibility | full metadata comparison and payload hashes | PASS |
| Remove duplicate stored texture data | shared ranges and compacted stream | PASS |
| Unify compatible texture references | all local Miner references point to 113 | PASS |
| Preserve distinct Ranger settings | exact typetree comparison | PASS |
| Preserve requested texture sizes/Crunch | identical native payloads/dimensions/formats | PASS |
| Preserve original and references | object IDs, pointer checks, resource hashes | PASS |
| Changed-files delivery | SHA256SUMS and member-verified archive | PASS after packaging |

COMPLETION VERDICT: COMPLETE for this duplicate texture correction and packaging.
Does not certify native rendering or the separate multiplayer audit goal.
Final bundle SHA256: {result['candidateSha256']}
'''
    (DOC/'DEDUPLICATED_DELIVERY.md').write_text(report,encoding='utf-8')
    files={'Resources/RB_TOOL_FireEXT.unity3d':candidate,
        '_Documentation/AssetBundleOptimization/FireEXT/DEDUPLICATED_DELIVERY.md':DOC/'DEDUPLICATED_DELIVERY.md',
        '_Documentation/AssetBundleOptimization/FireEXT/deduplicated-inventory.json':DOC/'deduplicated-inventory.json',
        'Tools/AssetBundle/deduplicate_fireext.py':Path(__file__),
        'Tools/AssetBundle/optimize_fireext.py':Path(__file__).parent/'optimize_fireext.py',
        'Tools/AssetBundle/requirements.txt':Path(__file__).parent/'requirements.txt'}
    sums=''.join(f'{digest(p)}  {n}\n' for n,p in sorted(files.items()));(OUT/'SHA256SUMS.txt').write_text(sums,newline='\n')
    archive=OUT.parent/(OUT.name+'_changed_files.zip')
    with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for n,p in files.items():z.write(p,n)
        z.write(OUT/'SHA256SUMS.txt','SHA256SUMS.txt')
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None and set(z.namelist())==set(files)|{'SHA256SUMS.txt'}
        for n,p in files.items():assert sha(z.read(n))==digest(p)
        assert z.read('SHA256SUMS.txt').decode()==sums
    result['archive']=str(archive);result['archiveSha256']=digest(archive)
    (OUT/'package-receipt.json').write_text(json.dumps(result,indent=2))
    print(json.dumps(result,indent=2))
if __name__=='__main__':main()
