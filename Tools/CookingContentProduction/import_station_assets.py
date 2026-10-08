"""Bring in the verified 2.6 cooking station assets without overwriting other work."""
from pathlib import Path
import hashlib
import json
import shutil

ROOT = Path(__file__).resolve().parents[2]
OLD = ROOT.parents[2] / '7 Days To Die 2.6/Mods/zzz_REBIRTH__Core'
files = ['Resources/FR_Industrial.unity3d']
files += ['UIAtlases/ItemIconAtlas/' + n + '.png' for n in [
    'FR_GasStove_icon', 'FR_IronStove_icon', 'FR_BakingPan_icon', 'FR_MortarPestle_icon']]
files += ['UIAtlases/UIAtlas/' + n + '.png' for n in [
    'ui_game_symbol_FR_GasStove_icon', 'ui_game_symbol_FR_IronStove_icon', 'ui_game_symbol_FR_MortarPestle_icon']]
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()
report = []
for relative in files:
    source, target = OLD / relative, ROOT / relative
    if not source.is_file():
        raise FileNotFoundError(source)
    sha = digest(source)
    if target.exists() and digest(target) != sha:
        raise RuntimeError('Refusing to overwrite a different existing asset: ' + str(target))
    target.parent.mkdir(parents=True, exist_ok=True)
    if not target.exists():
        shutil.copy2(source, target)
    report.append({'path': relative, 'source': str(source), 'sha256': sha, 'bytes': target.stat().st_size})
out = ROOT / '_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915/Production/station_asset_imports.json'
out.write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps({'imported_or_verified': len(report), 'total_bytes': sum(r['bytes'] for r in report)}))
