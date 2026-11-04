"""Install the four comparison copies; preserve the approved collection."""
import json, shutil, subprocess, sys, xml.etree.ElementTree as ET
from pathlib import Path
from install_approved import sha, copy_with_retry, ROOT
BASE=Path('F:/REBIRTH_Wrap_Optimization/2026-11-04/Comparison')
DOC=ROOT/'_Documentation/WrapComparison20261104'

def main():
    data=json.loads((BASE/'comparison.json').read_text());rows=data['variants'];assert len(rows)==4
    assert 'all 52 textures GPU rendered' in (BASE/'gpu-comparison-PASS.txt').read_text()
    game=subprocess.run(['powershell','-NoProfile','-Command',"@(Get-Process -ErrorAction SilentlyContinue | Where-Object {$_.ProcessName -like '7DaysToDie*'}).Count"],capture_output=True,text=True,check=True)
    assert game.stdout.strip()=='0','Game running; leave comparison items staged and do not close it automatically'
    production=json.loads((BASE.parent/'installation.json').read_text())['bundles']
    expected={name:row['candidateSha256'] for name,row in production.items()}
    backup=Path('F:/REBIRTH_Wrap_Backups/2026-11-04_PreOptimization')
    primary=json.loads((backup/'VERIFIED_MANIFEST.json').read_text())
    for row in primary['files']:
        if row['relative'].startswith('CurrentBundles/'):
            name=row['relative'].split('/')[-1]
            if name not in expected:expected[name]=row['sha256']
    for row in json.loads((backup/'LEGACY_BUNDLE_SUPPLEMENT.json').read_text())['files']:expected[row['name']]=row['sha256']
    for name,digest in expected.items():assert sha(ROOT/'Resources'/name)==digest,('Production changed',name)
    for row in rows:
        assert sha(BASE/'Runtime/Resources'/row['bundle'])==row['sha256']
        assert not (ROOT/'Resources'/row['bundle']).exists(),'Comparison already installed'
        for mode in ('Camper','Pickup'):
            for view in ('quarter','side','right','rear','hood','rear-door-open'):
                assert (BASE/'NativeReview'/mode/(row['id']+'-'+view+'.png')).exists()
    # Keep the previous cumulative DLL and configuration for exact recovery.
    previous=BASE/'BeforeInstallation';assert not previous.exists();previous.mkdir()
    files=['RebirthUtils.dll','Config/_Vehicles/camper_pattern_item_modifiers.xml','Config/Localization.csv','Config/Localization.txt']
    old={}
    for file in files:
        dest=previous/file;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/file,dest)
        old[file]=sha(ROOT/file);assert sha(dest)==old[file]
    (previous/'manifest.json').write_text(json.dumps(old,indent=2))
    new_dll=DOC/'build/RebirthUtils.dll';assert new_dll.exists()
    installed=[]
    try:
        for row in rows:
            copy_with_retry(BASE/'Runtime/Resources'/row['bundle'],ROOT/'Resources'/row['bundle']);installed.append(row['bundle'])
        copy_with_retry(new_dll,ROOT/'RebirthUtils.dll');assert sha(ROOT/'RebirthUtils.dll')==sha(new_dll)
        subprocess.run([sys.executable,str(Path(__file__).parent/'prepare_comparison_items.py'),'--activate'],cwd=ROOT,check=True)
        xml=ET.parse(ROOT/'Config/_Vehicles/camper_wrap_comparison_item_modifiers.xml')
        assert {n.attrib['name'] for n in xml.iter('item_modifier')}=={row['item'] for row in rows}
        for row in rows:assert sha(ROOT/'Resources'/row['bundle'])==row['sha256']
        for name,digest in expected.items():assert sha(ROOT/'Resources'/name)==digest,('Production changed',name)
    except BaseException:
        for file in files:copy_with_retry(previous/file,ROOT/file);assert sha(ROOT/file)==old[file]
        for name in installed:(ROOT/'Resources'/name).unlink()
        path=ROOT/'Config/_Vehicles/camper_wrap_comparison_item_modifiers.xml'
        if path.exists():path.unlink()
        raise
    result=dict(status='PASS',dllSha256=sha(ROOT/'RebirthUtils.dll'),variants=rows,productionBundlesUnchanged=len(expected),
        gameLaunched=False,gameplayTest='Owner manual comparison pending',beforeInstallation=str(previous))
    (BASE/'installation.json').write_text(json.dumps(result,indent=2));(DOC/'installation.json').write_text(json.dumps(result,indent=2))
    print('PASS: four bundles + four creative modifiers installed; all',len(expected),'previous wrap bundles unchanged; DLL',result['dllSha256'])
if __name__=='__main__':main()
