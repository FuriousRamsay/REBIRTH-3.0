"""Stage an isolated shader/art-only Unity project; never edits shared prefab project."""
import json
import shutil
import filecmp
import hashlib
from check_panel_quality import check
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
PROJECT = HERE / "_UnityProject"


def copy_if_changed(source,target):
    if not target.exists() or not filecmp.cmp(source,target,shallow=False):shutil.copyfile(source,target)

def main():
    quality=PROJECT/'Assets/REBIRTHCamperWrapCollection/Quality'
    quality.mkdir(parents=True,exist_ok=True)
    for contract in sorted((HERE/'Templates').glob('*panel-layout.json')):
        if not contract.exists():continue
        receipt=check(contract)
        (quality/(receipt['id']+'.json')).write_text(json.dumps(receipt,indent=2),encoding='utf-8')
    for name in ['Packages', 'ProjectSettings', 'Assets/Editor', 'Assets/REBIRTHCamperWrapCollection/Shaders', 'Assets/REBIRTHCamperWrapCollection/Artwork']:
        (PROJECT / name).mkdir(parents=True, exist_ok=True)
    (PROJECT / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {'com.unity.modules.imageconversion': '1.0.0', 'com.unity.modules.assetbundle': '1.0.0', 'com.unity.modules.physics': '1.0.0'}}), encoding='utf-8')
    (PROJECT / 'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.62f2\nm_EditorVersionWithRevision: 2022.3.62f2 (7670c08855a9)\n', encoding='utf-8')
    for path in (HERE / 'Unity').glob('*.shader'):
        copy_if_changed(path, PROJECT / 'Assets/REBIRTHCamperWrapCollection/Shaders' / path.name)
    for path in (HERE / 'Unity').glob('*.cs'):
        copy_if_changed(path, PROJECT / 'Assets/Editor' / path.name)
    copy_if_changed(ROOT / 'Tools/AssetBundle/Unity/RebirthAssetBundlePackaging.cs', PROJECT / 'Assets/Editor/RebirthAssetBundlePackaging.cs')
    copy_if_changed(ROOT / 'Scripts/Vehicles/Camper/RebirthCamperWrapCatalog.cs', PROJECT / 'Assets/Editor/RebirthCamperWrapCatalog.cs')
    copy_if_changed(ROOT / 'Scripts/Vehicles/Camper/RebirthCamperWrapAppearance.cs', PROJECT / 'Assets/Editor/RebirthCamperWrapAppearance.cs')
    copy_if_changed(ROOT / 'Scripts/Vehicles/Camper/RebirthCamperPatternAppearance.cs', PROJECT / 'Assets/Editor/RebirthCamperPatternAppearance.cs')
    for path in (ROOT / 'Resources/CamperWraps/Artwork').glob('*.png'):
        copy_if_changed(path, PROJECT / 'Assets/REBIRTHCamperWrapCollection/Artwork' / path.name)
    # All preview layers must survive a clean project stage, not rely on manual copies.
    manifest=json.loads((HERE / 'ConceptProjection/native-layer-manifest.json').read_text(encoding='utf-8'))
    for layer in manifest['layers']:
        source=HERE / 'ConceptProjection' / layer['source']
        if hashlib.sha256(source.read_bytes()).hexdigest()!=layer['sha256']:
            raise ValueError('Native concept layer changed without reviewed manifest update: '+layer['source'])
        copy_if_changed(source,PROJECT / 'Assets/REBIRTHCamperWrapCollection/Artwork' / layer['target'])
    print(PROJECT)


if __name__ == '__main__':
    main()
