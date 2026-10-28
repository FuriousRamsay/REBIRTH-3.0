"""Minimal audited optional-DISO import for the upstream hierarchical decoder."""
from pathlib import Path
import hashlib,json
root=Path('E:/_Haven/shared/REBIRTHTemp/ForgeImage3DTrial/TripoSG')
path=root/'triposg/inference_utils.py';original=path.read_text()
backup=path.with_name('inference_utils.upstream.py')
if not backup.exists():backup.write_text(original)
old='from diso import DiffDMC'
new='try:\n    from diso import DiffDMC\nexcept ImportError:\n    DiffDMC = None  # Hierarchical decoder does not require DISO.'
if new not in original:
    assert original.count(old)==1
    path.write_text(original.replace(old,new))
receipt={'upstreamCommit':'fc5c40990181e2a756c4e0b1c2f4d6b5202faf8c','upstreamHash':hashlib.sha256(backup.read_bytes()).hexdigest(),'modifiedHash':hashlib.sha256(path.read_bytes()).hexdigest(),'change':'Optional DISO import only; inference must explicitly use_flash_decoder=False','officialAlternative':'hierarchical_extract_geometry uses skimage marching_cubes already in upstream','modelWeightsChanged':False,'validatedInference':False}
(root.parent/'windows-adapter-receipt.json').write_text(json.dumps(receipt,indent=2));print(json.dumps(receipt))
