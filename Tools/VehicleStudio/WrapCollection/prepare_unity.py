"""Stage an isolated shader/art-only Unity project; never edits shared prefab project."""
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HERE = Path(__file__).resolve().parent
PROJECT = HERE / "_UnityProject"


def main():
    for name in ['Packages', 'ProjectSettings', 'Assets/Editor', 'Assets/REBIRTHCamperWrapCollection/Shaders', 'Assets/REBIRTHCamperWrapCollection/Artwork']:
        (PROJECT / name).mkdir(parents=True, exist_ok=True)
    (PROJECT / 'Packages/manifest.json').write_text(json.dumps({'dependencies': {'com.unity.modules.imageconversion': '1.0.0', 'com.unity.modules.assetbundle': '1.0.0', 'com.unity.modules.physics': '1.0.0'}}), encoding='utf-8')
    (PROJECT / 'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 2022.3.62f2\nm_EditorVersionWithRevision: 2022.3.62f2 (7670c08855a9)\n', encoding='utf-8')
    for path in (HERE / 'Unity').glob('*.shader'):
        shutil.copyfile(path, PROJECT / 'Assets/REBIRTHCamperWrapCollection/Shaders' / path.name)
    for path in (HERE / 'Unity').glob('*.cs'):
        shutil.copyfile(path, PROJECT / 'Assets/Editor' / path.name)
    shutil.copyfile(ROOT / 'Scripts/Vehicles/Camper/RebirthCamperWrapCatalog.cs', PROJECT / 'Assets/Editor/RebirthCamperWrapCatalog.cs')
    shutil.copyfile(ROOT / 'Scripts/Vehicles/Camper/RebirthCamperWrapAppearance.cs', PROJECT / 'Assets/Editor/RebirthCamperWrapAppearance.cs')
    shutil.copyfile(ROOT / 'Scripts/Vehicles/Camper/RebirthCamperPatternAppearance.cs', PROJECT / 'Assets/Editor/RebirthCamperPatternAppearance.cs')
    for path in (ROOT / 'Resources/CamperWraps/Artwork').glob('*.png'):
        shutil.copyfile(path, PROJECT / 'Assets/REBIRTHCamperWrapCollection/Artwork' / path.name)
    print(PROJECT)


if __name__ == '__main__':
    main()
