"""Package only the requested optimized asset and its reproducible inventory."""
import json, hashlib, zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'_Documentation/AssetBundleOptimization/FireEXT'
OUT=ROOT/'_Deliverables/FireEXT_2048_1024_256'

def digest(path):
    with path.open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()

def main():
    report=json.loads((DOC/'optimized-inventory.json').read_text(encoding='utf-8'))
    candidate=OUT/'Resources/RB_TOOL_FireEXT.unity3d'
    source=ROOT/'Resources/RB_TOOL_FireEXT.unity3d'
    assert digest(candidate)==report['candidateSha256'] and digest(source)==report['sourceSha256']
    reduction=100*(1-report['candidateBytes']/report['sourceBytes'])
    counts={role:sum(t['role']==role for t in report['textures']) for role in ['albedo','normal','other']}
    summary=f'''# FireEXT optimized bundle delivery

Source cumulative project: current shared REBIRTH 3.0 checkout, targeting 7 Days
to Die 3.0 b259. Source Unity asset serialization: 2022.3.29f1.
Delivery type: changed-files package. No full cumulative project requested or included.
The running mod's original bundle has not been replaced.

## Result

- Original: {report['sourceBytes']:,} bytes ({report['sourceBytes']/1048576:.2f} MiB).
- Candidate: {report['candidateBytes']:,} bytes ({report['candidateBytes']/1048576:.2f} MiB).
- Bundle size reduction: {reduction:.2f}%.
- {report['resizedTextures']} of 103 textures resized; smaller textures never enlarged.
- Original texture payload: {report['texturePayloadBefore']/1048576:.2f} MiB;
  candidate texture payload: {report['texturePayloadAfter']/1048576:.2f} MiB.
- Classification by actual material slots: {counts['albedo']} albedo/color,
  {counts['normal']} normal, {counts['other']} other maps.

## Rules and preservation

Albedo/diffuse maps in _MainTex: maximum 2048 square. Normal maps in _BumpMap
or _Normal: maximum 1024 square. Other maps (AO, metallic/smoothness/roughness):
maximum 256 square. Shared albedo/AO textures retain the color resolution;
for example 5.56Bullet_Specular is actually used in both _MainTex and _OcclusionMap,
so it stays at its existing 512 square size. Particle diffuse atlases are also
_MainTex color textures: they stay at 2048, preserving their sprite pixel bindings.
The smoke normal atlas is reduced to 1024. No texture was established as unused
or deleted. Only texture resolutions, relevant texture serialization metadata,
and relocated streamed mesh offsets changed.

Existing uncrunched DXT mip chains were trimmed directly where available,
avoiding another lossy encode. Resized Crunch textures were decoded, channel-wise
resampled and encoded to their corresponding DXT1/DXT5 formats. Crunch itself is
not preserved for resized maps; the whole bundle uses LZ4HC. Unchanged texture
payloads remain byte-identical, including their original Crunch compression.
Alpha and packed normal channels are processed independently. Texture color-space,
filter/wrap settings and material shader bindings are preserved. Resizing reduces
fine detail by design. No new normal-channel convention or Y flip was introduced.
The .resS stream is compacted, rather than leaving obsolete full-size texture bytes
behind. Actual streamed mesh payload bytes and separate audio resource stay identical.

## Validation actually performed

Independently reopened the written candidate with UnityPy 1.25.4. All 103 textures
decode and meet their role's size limit. All object IDs/types/counts survive.
Every unchanged serialized object is byte-identical, including materials, shaders,
animation, particle systems, sprite and AssetBundle container/path references.
All {report['validation']['streamRangesChecked']} resource ranges passed payload SHA256
comparison, including 204 relocated native mesh ranges. Modified object typetrees
roundtrip exactly. Separate audio resource is byte-identical. Source SHA256 is unchanged.
Asset paths referenced by Config/_Fire/items.xml and sounds.xml retain their original
AssetBundle container object. No game XML change is needed.
Visually inspected texture-comparison.png: original and optimized extinguisher
albedo, packed normal, AO and roughness plus female body color/normal retain their
orientation and channel appearance. Smaller AO/roughness detail is visibly reduced,
as expected at 256. This contact sheet is texture inspection, not a rendered-model test.

Compilation: Not required (no production C# change for this asset task).
Unity Editor loading: Not performed.
In-game validation: Not performed. The package is not installed in the active mod.
No gameplay, process restart, production build or multiplayer fixture was run.

## Requirement traceability and completion confirmation

| Requirement | Evidence | Status |
| --- | --- | --- |
| Identify space use and texture formats | original-inventory.json; ANALYSIS.md | PASS |
| Albedo max 2048, normals max 1024, others max 256 | optimized-inventory.json, all-texture decode/size assertions | PASS |
| Preserve asset identities and nontexture content | raw-object hashes, mesh/audio resource hashes, typetree roundtrip assertions | PASS |
| Repackage optimized bundle | Resources/RB_TOOL_FireEXT.unity3d in changed-files archive | PASS |
| Preserve original | unchanged source SHA256 | PASS |
| Documentation and changed-files delivery | this report, inventories, scripts, SHA256SUMS and verified ZIP | PASS |

Accepted architecture preserved: PASS. Assets: PASS. Documentation: PASS.
Code/XML/UI/localization/networking/persistence requirements: N/A to this asset task.
Packaging: PASS after archive member/hash verification by package_fireext.py.
COMPLETION VERDICT: COMPLETE for inspection, requested resizing and repackaging.
This does not certify Unity/game rendering or the separate multiplayer audit goal.

## Maintenance

Use optimize_fireext.py only with its inspected source hash. It refuses to overwrite
an existing candidate and aborts on changed source, unknown texture roles, mixed
normal/color roles or overlapping/out-of-range streams. Dependencies are pinned
in Tools/AssetBundle/requirements.txt; install with pip --target Tools/AssetBundle/.python.
The original inspection script and optimized inventory preserve each texture's
object ID, actual material properties, format, dimensions and mip/payload sizes.
If rebuilding the original Unity source project later, set equivalent importer
max sizes there; do not unintentionally reintroduce the large original textures.

Original SHA256: {report['sourceSha256']}
Candidate SHA256: {report['candidateSha256']}
'''
    (DOC/'DELIVERY.md').write_text(summary,encoding='utf-8')
    rows=['# Texture inventory: original to optimized','',
        'Roles follow actual material slots; shared color/AO retains the color limit.','',
        '| ID | Texture | Role | Original size | Original format | New size | New format |',
        '| ---: | --- | --- | --- | --- | --- | --- |']
    for t in report['textures']:
        a,b=t['before'],t['after']
        rows.append(f"| {t['id']} | {t['name']} | {t['role']} | {a['width']} x {a['height']} | {a['format']} | {b['width']} x {b['height']} | {b['format']} |")
    (DOC/'TEXTURES.md').write_text('\n'.join(rows)+'\n',encoding='utf-8')
    files={
      'Resources/RB_TOOL_FireEXT.unity3d':candidate,
      **{f'_Documentation/AssetBundleOptimization/FireEXT/{name}':DOC/name for name in ['ANALYSIS.md','DELIVERY.md','TEXTURES.md','original-inventory.json','optimized-inventory.json','texture-comparison.png']},
      **{f'Tools/AssetBundle/{name}':ROOT/'Tools/AssetBundle'/name for name in ['inspect_fireext.py','optimize_fireext.py','package_fireext.py','preview_fireext.py','requirements.txt']}
    }
    sums=''.join(f'{digest(path)}  {name}\n' for name,path in sorted(files.items()))
    (OUT/'SHA256SUMS.txt').write_text(sums,encoding='utf-8',newline='\n')
    archive=OUT.parent/'FireEXT_2048_1024_256_changed_files.zip'
    assert not archive.exists(), 'Do not overwrite a reviewed delivery'
    with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as package:
        for name,path in files.items():package.write(path,name)
        package.write(OUT/'SHA256SUMS.txt','SHA256SUMS.txt')
    with zipfile.ZipFile(archive) as package:
        assert set(package.namelist())==set(files)|{'SHA256SUMS.txt'}
        assert package.testzip() is None
        for name,path in files.items():assert hashlib.sha256(package.read(name)).hexdigest()==digest(path)
        assert package.read('SHA256SUMS.txt').decode('utf-8')==sums
    receipt=dict(archive=str(archive),bytes=archive.stat().st_size,sha256=digest(archive),members=len(files)+1,allMemberHashesVerified=True,sourceUnchanged=digest(source)==report['sourceSha256'])
    (OUT/'package-receipt.json').write_text(json.dumps(receipt,indent=2),encoding='utf-8')
    print(json.dumps(receipt,indent=2))

if __name__=='__main__':main()
