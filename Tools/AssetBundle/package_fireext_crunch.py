"""Package and hash-verify the native Crunch delivery without installing it."""
import hashlib, json, zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
DOC=ROOT/'_Documentation/AssetBundleOptimization/FireEXT'
OUT=ROOT/'_Deliverables/FireEXT_2048_1024_256_Crunch'
def digest(path):
    with path.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def main():
    r=json.loads((DOC/'crunch-inventory.json').read_text())
    first=json.loads((DOC/'optimized-inventory.json').read_text())
    original=ROOT/'Resources/RB_TOOL_FireEXT.unity3d'
    candidate=Path(r['candidate'])
    assert digest(original)==first['sourceSha256']
    assert digest(Path(r['source']))==r['sourceSha256']
    assert digest(candidate)==r['candidateSha256']
    text=f'''# FireEXT native Crunch delivery

Changed-files delivery from the current shared REBIRTH 3.0 checkout, targeting
7 Days to Die 3.0 b259. Original serialization version: Unity 2022.3.29f1.
Native Crunch encoder: Unity {r['editor']}, quality 100.

Original bundle: {original.stat().st_size:,} bytes ({original.stat().st_size/1048576:.2f} MiB).
Resized candidate before Crunch: {r['sourceBytes']:,} bytes ({r['sourceBytes']/1048576:.2f} MiB).
Final bundle: {r['candidateBytes']:,} bytes ({r['candidateBytes']/1048576:.2f} MiB).
Reduction from original: {100*(1-r['candidateBytes']/original.stat().st_size):.2f}%.

All 102 DXT-compatible textures now use Crunch: 39 DXT1Crunched and 63
DXT5Crunched. 81 textures were converted with the installed Unity editor;
21 already-Crunched textures were preserved. One existing 64-square RGBA32
texture remains unchanged (21,844 bytes). Outer bundle compression is LZ4HC.
Albedo/color maximum 2048, normals maximum 1024, other maps maximum 256;
66 textures were resized in the preceding stage. Smaller textures are not enlarged.
Actual material bindings determine roles, including particle color atlases and
the shared color/AO map documented in the preceding inventory.

Native encoding imported the already-resized pixel data with existing color-space
and alpha channels, without treating packed normals as a new normal-map import.
Mip chains were regenerated for converted textures. Crunch encoding is lossy;
quality 100 prioritizes fidelity. Existing texture readability, sampling and
material bindings in the bundle remain preserved. No assets were removed.

## Further optimization findings

- The original player, gear and Female_Enf texture group contributes 234.99 MiB
  of stored texture payload, about 72.5% of all original texture payload. Review
  the full prefab dependency graph for removable character/weapon variants.
  Names alone do not establish that these assets are unused.
- Two pairs have identical pre-Crunch texture payloads: Ranger body IDs 98/100
  and Miner hands IDs 113/120. Each duplicate was 2.67 MiB before Crunch.
  Deduplication requires matching full texture metadata and redirecting every
  object reference. Outer compression may already absorb repeated bytes, so
  this is not a guaranteed equivalent disk saving. Nothing was deduplicated.
- There are 233 meshes and 247,561 total vertices. Some meshes retain CPU
  readability. Disable that only after checking runtime consumers, skinning
  and any CPU mesh access. Geometry and LOD work is secondary to texture cleanup.
- All 103 textures already have Read/Write disabled. Audio is only 253,856 bytes
  and shaders about 497,560 bytes: neither is a major size contributor.
- Mask packing or DXT1 conversion of opaque DXT5 maps needs shader/channel
  verification. Packed normals, smoothness and particle alpha must be preserved.

Crunch saves disk/download space, not GPU memory. Resolution reductions provide
the GPU memory savings. See Unity's texture compression documentation:
https://docs.unity.cn/2020.2/Documentation/Manual/texture-compression-formats.html

## Requirement evidence

| Requirement | Evidence | Status |
| --- | --- | --- |
| Identify texture roles and space use | original and optimized inventories, further-optimization-audit.json | PASS |
| Apply 2048/1024/256 limits | preceding all-texture size validation; dimensions preserved in Crunch stage | PASS |
| Use native Crunch | Unity encoder receipt, 102 Crunch formats, independent decode of all 103 textures | PASS |
| Preserve asset references and nontexture content | all object IDs/types, unchanged object hashes, 307 stream ranges, mesh/audio hashes | PASS |
| Repackage and preserve original | final SHA256 and verified archive; original and preceding candidate hashes rechecked | PASS |
| Documentation and changed-files package | this report, inventories, reproducible scripts, SHA256SUMS | PASS |

All 103 textures independently decode with UnityPy after repacking. Modified
typetrees roundtrip exactly. Original bundle and preceding delivery remain intact.
The new bundle is not installed. Unity native encoding was performed; loading
the complete repacked bundle in Unity and in-game rendering were NOT RUN.
No multiplayer build or fixtures were run. Production code/XML unchanged.

COMPLETION VERDICT: COMPLETE for the requested texture resizing, Crunch encoding,
inspection and changed-files repackaging. Game rendering remains unvalidated.
This delivery does not certify completion of the separate multiplayer audit.

Original SHA256: {first['sourceSha256']}
Final SHA256: {r['candidateSha256']}
'''
    (DOC/'CRUNCH_DELIVERY.md').write_text(text,encoding='utf-8')
    names=['CRUNCH_DELIVERY.md','original-inventory.json','optimized-inventory.json','crunch-inventory.json','further-optimization-audit.json']
    files={'Resources/RB_TOOL_FireEXT.unity3d':candidate}
    files.update({f'_Documentation/AssetBundleOptimization/FireEXT/{n}':DOC/n for n in names})
    for n in ['inspect_fireext.py','optimize_fireext.py','prepare_fireext_crunch.py','repack_fireext_crunch.py','package_fireext_crunch.py','requirements.txt','CrunchProject/Assets/Editor/RebirthFireExtCrunch.cs','CrunchProject/ProjectSettings/ProjectVersion.txt','CrunchProject/Packages/manifest.json','CrunchProject/plan.json','CrunchProject/CrunchOutput/receipt.json']:
        files[f'Tools/AssetBundle/{n}']=ROOT/'Tools/AssetBundle'/n
    sums=''.join(f'{digest(p)}  {n}\n' for n,p in sorted(files.items()))
    (OUT/'SHA256SUMS.txt').write_text(sums,encoding='utf-8',newline='\n')
    archive=OUT.parent/'FireEXT_2048_1024_256_Crunch_changed_files.zip'
    with zipfile.ZipFile(archive,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for n,p in files.items():z.write(p,n)
        z.write(OUT/'SHA256SUMS.txt','SHA256SUMS.txt')
    with zipfile.ZipFile(archive) as z:
        assert set(z.namelist())==set(files)|{'SHA256SUMS.txt'}
        assert z.testzip() is None
        for n,p in files.items():assert hashlib.sha256(z.read(n)).hexdigest()==digest(p)
        assert z.read('SHA256SUMS.txt').decode()==sums
    receipt=dict(archive=str(archive),bytes=archive.stat().st_size,sha256=digest(archive),allMemberHashesVerified=True,originalUnchanged=digest(original)==first['sourceSha256'])
    (OUT/'package-receipt.json').write_text(json.dumps(receipt,indent=2))
    print(json.dumps(receipt,indent=2))
if __name__=='__main__':main()
