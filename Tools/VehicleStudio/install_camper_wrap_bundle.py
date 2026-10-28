"""Prepare/apply the verified wrap candidate; replace only its owned bundle."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import uuid

MOD = Path(__file__).resolve().parents[2]
DEST = MOD / 'Resources/rebirth_camper80s.unity3d'
STAGE = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio')
BACKUP = Path('E:/_Haven/shared/REBIRTHBackups/VehicleStudio')
PROTECTED = ('Config/_Vehicles/camper_vehicles.xml', 'Config/_Vehicles/camper_items.xml',
             'Config/_Vehicles/camper_entityclasses.xml')

def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()

def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))

def validate(report, base):
    if report.get('status') != 'PASS_WRAP_GAME_ASSET_NOT_GAME_TESTED' or report.get('error'):
        raise ValueError('Native reopened wrap candidate required.')
    if report.get('defaultPattern') != 'none' or report.get('cosmeticModifier') != 'modVehiclePatternDisco':
        raise ValueError('Current native cosmetic modifier contract required; obsolete always-wrapped asset refused.')
    if base.get('status') != 'PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED' or not base.get('originalsUnchanged') or not base.get('prefabVerified'):
        raise ValueError('Verified preserved lighting source required.')
    if report['sourcePrefab'] != base['prefabAsset'] or not report.get('crc'):
        raise ValueError('Exact lighting source and native CRC required.')
    names = report.get('assetNames', [])
    if len(names) != 1 or not names[0].endswith('/rebirthcamper80s.prefab'):
        raise ValueError('Exact native prefab load name required.')
    if digest(report['bundle']) != report['sha256']:
        raise ValueError('Candidate checksum changed.')

def prepare(report_path, base_path):
    report, base = read(report_path), read(base_path)
    validate(report, base)
    run = uuid.uuid4().hex
    stage, backup = STAGE / run, BACKUP / run
    stage.mkdir(parents=True); backup.mkdir(parents=True)
    before = digest(DEST)
    shutil.copyfile(DEST, backup / DEST.name)
    shutil.copyfile(report['bundle'], stage / DEST.name)
    if digest(DEST) != before or digest(backup / DEST.name) != before:
        raise ValueError('Installed bundle changed during preparation.')
    plan = dict(kind='REBIRTH_CAMPER_WRAP_INSTALL', status='PREPARED_NOT_INSTALLED',
                mod=str(MOD), before=before, after=report['sha256'], candidate=str(stage / DEST.name),
                backup=str(backup / DEST.name), report=str(report_path.resolve()), base=str(base_path.resolve()),
                reportHash=digest(report_path), baseHash=digest(base_path),
                protected={name:digest(MOD/name) for name in PROTECTED}, gameTested=False)
    path=stage/'plan.json'; path.write_text(json.dumps(plan,indent=2)+'\n')
    return path

def no_game():
    result=subprocess.run(['powershell','-NoProfile','-Command',
                           'if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){exit 1}'])
    if result.returncode: raise RuntimeError('Running game preserved; installation refused.')

def apply(path):
    if path.with_name('superseded.json').exists():
        raise ValueError('Superseded candidate preserved for evidence; prepare the latest verified build.')
    plan=read(path)
    if plan.get('kind')!='REBIRTH_CAMPER_WRAP_INSTALL' or plan.get('status')!='PREPARED_NOT_INSTALLED' or Path(plan['mod']).resolve()!=MOD:
        raise ValueError('Exact prepared wrap transaction required.')
    no_game()
    if digest(plan['report'])!=plan['reportHash'] or digest(plan['base'])!=plan['baseHash']:
        raise ValueError('Build evidence changed.')
    validate(read(plan['report']),read(plan['base']))
    if set(plan['protected'])!=set(PROTECTED) or any(digest(MOD/name)!=value for name,value in plan['protected'].items()):
        raise ValueError('Camper configuration changed; reprepare.')
    if digest(DEST)!=plan['before'] or digest(plan['backup'])!=plan['before'] or digest(plan['candidate'])!=plan['after']:
        raise ValueError('Installed/candidate/backup checksum changed.')
    sibling=DEST.with_name(DEST.name+'.'+path.parent.name+'.tmp')
    if sibling.exists(): raise ValueError('Existing transaction file requires inspection.')
    shutil.copyfile(plan['candidate'],sibling)
    if digest(sibling)!=plan['after'] or digest(DEST)!=plan['before']: raise ValueError('Concurrent bundle change.')
    no_game()
    sibling.replace(DEST)
    if digest(DEST)!=plan['after']: raise RuntimeError('Installed checksum mismatch; backup preserved.')
    plan['status']='PASS_WRAP_INSTALLED_NOT_GAME_TESTED'
    receipt=path.with_name('result.json');receipt.write_text(json.dumps(plan,indent=2)+'\n')
    return receipt

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    commands=parser.add_subparsers(dest='command',required=True)
    prep=commands.add_parser('prepare');prep.add_argument('--report',type=Path,required=True);prep.add_argument('--base',type=Path,required=True)
    install=commands.add_parser('apply');install.add_argument('plan',type=Path)
    args=parser.parse_args()
    print(prepare(args.report,args.base) if args.command=='prepare' else apply(args.plan))
