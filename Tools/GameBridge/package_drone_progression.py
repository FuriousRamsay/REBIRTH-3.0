from pathlib import Path
import hashlib,json,zipfile
root=Path(__file__).resolve().parents[2]
names=['Scripts/Survivor/Progression/RebirthDroneTravelTrainingService.cs',
'Scripts/Survivor/Progression/RebirthComplexSkillSystemService.cs',
'Config/_Rebirth/drone_loot.xml','Config/_Rebirth/loot.xml','Config/Localization.csv',
'Config/_Rebirth/challenges.xml','Tools/GameBridge/build_learning_challenges.py',
'Tools/GameBridge/audit_drone_loot.py','Tools/GameBridge/audit_progression_distribution.py',
'Tools/GameBridge/tests/drone_travel_practice.json','Tools/GameBridge/package_drone_progression.py',
'_Documentation/Codex_Changes/CUMULATIVE_LOG.txt',
'_Documentation/Progression_Loot/merged_distribution_audit.json','RebirthUtils.dll','RebirthUtils.pdb']
names += [p.relative_to(root).as_posix() for p in (root/'_Documentation/DroneProgression').glob('*') if p.name!='package_manifest.json']
manifest={n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in sorted(names)}
mp='_Documentation/DroneProgression/package_manifest.json'
(root/mp).write_text(json.dumps(manifest,indent=2)+'\n')
dest=root/'Tools/GameBridge/out/delivery/REBIRTH_20261002_drone_progression.zip'
dest.parent.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(dest,'w',zipfile.ZIP_DEFLATED) as z:
    for n in sorted(names+[mp]):z.write(root/n,n)
with zipfile.ZipFile(dest) as z:
    assert z.testzip() is None
    for n,h in manifest.items():assert hashlib.sha256(z.read(n)).hexdigest()==h
h=hashlib.sha256(dest.read_bytes()).hexdigest()
dest.with_suffix('.zip.sha256').write_text(h+'  '+dest.name+'\n')
print(json.dumps({'archive':str(dest),'files':len(names)+1,'sha256':h}))
