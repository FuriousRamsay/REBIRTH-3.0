"""Prepare/apply a bundle-only update while preserving the fitted cabin XML."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import uuid
from camper_integration import STUDIO, BUNDLE, validate_candidate_report, file_hash
from verify_installed_candidate import verify

MOD = STUDIO.parents[1]
STAGING = Path('E:/_Haven/shared/REBIRTHStaged/VehicleStudio')
BACKUPS = Path('E:/_Haven/shared/REBIRTHBackups/VehicleStudio')
PROTECTED = ['Config/_Vehicles/camper_vehicles.xml', 'Config/_Vehicles/camper_items.xml',
             'Config/_Vehicles/camper_entityclasses.xml']


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def prepare(args):
    report = read(args.report)
    validate_candidate_report(report)
    verify(MOD, read(args.installed_report), args.seating_report)
    if file_hash(Path(report['bundlePath'])) != report['bundleSha256']:
        raise ValueError('Verified candidate changed.')
    installed = MOD / 'Resources' / BUNDLE
    before = file_hash(installed)
    run = uuid.uuid4().hex
    folder, backup = STAGING / run, BACKUPS / run
    folder.mkdir(parents=True)
    backup.mkdir(parents=True)
    shutil.copyfile(report['bundlePath'], folder / BUNDLE)
    shutil.copyfile(installed, backup / BUNDLE)
    if file_hash(installed) != before or file_hash(backup / BUNDLE) != before:
        raise ValueError('Installed bundle changed during preparation.')
    plan = {'kind': 'REBIRTH_CAMPER_BUNDLE_ONLY_UPDATE', 'status': 'PREPARED_NOT_INSTALLED',
            'mod': str(MOD), 'before': before, 'after': report['bundleSha256'],
            'candidate': str(folder / BUNDLE), 'backup': str(backup / BUNDLE),
            'report': str(args.report.resolve()), 'seatingReport': str(args.seating_report.resolve()),
            'reportSha256': file_hash(args.report), 'seatingSha256': file_hash(args.seating_report),
            'protected': {name: file_hash(MOD / name) for name in PROTECTED}, 'gameTested': False}
    path = folder / 'plan.json'
    path.write_text(json.dumps(plan, indent=2) + '\n')
    return path


def apply(path):
    plan = read(path)
    if plan.get('kind') != 'REBIRTH_CAMPER_BUNDLE_ONLY_UPDATE' or plan.get('status') != 'PREPARED_NOT_INSTALLED' or Path(plan['mod']).resolve() != MOD.resolve():
        raise ValueError('Exact prepared update required.')
    # This does not grant shared game ownership; the caller coordinates that first.
    probe = subprocess.run(['powershell', '-NoProfile', '-Command',
        'if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){exit 1}'], check=False)
    if probe.returncode:
        raise RuntimeError('Game must be closed; installed files were not changed.')
    for name, expected in plan['protected'].items():
        if name not in PROTECTED or file_hash(MOD / name) != expected:
            raise ValueError('Fitted camper configuration changed; reprepare.')
    if set(plan['protected']) != set(PROTECTED):
        raise ValueError('All fitted configuration guards required.')
    if file_hash(Path(plan['report'])) != plan['reportSha256'] or file_hash(Path(plan['seatingReport'])) != plan['seatingSha256']:
        raise ValueError('Verification evidence changed.')
    report = read(plan['report'])
    validate_candidate_report(report)
    destination = MOD / 'Resources' / BUNDLE
    if report['bundleSha256'] != plan['after'] or file_hash(Path(plan['candidate'])) != plan['after'] or file_hash(Path(plan['backup'])) != plan['before'] or file_hash(destination) != plan['before']:
        raise ValueError('Candidate/backup/installed checksums changed.')
    # A brief sibling file is necessary for same-volume atomic replacement.
    sibling = destination.with_name(destination.name + '.' + Path(path).parent.name + '.tmp')
    if sibling.exists():
        raise ValueError('Existing transaction file must be inspected.')
    shutil.copyfile(plan['candidate'], sibling)
    if file_hash(sibling) != plan['after'] or file_hash(destination) != plan['before']:
        raise ValueError('Concurrent change before replacement.')
    sibling.replace(destination)
    if file_hash(destination) != plan['after']:
        raise RuntimeError('Installed checksum mismatch; backup retained.')
    verify(MOD, report, Path(plan['seatingReport']))
    plan['status'] = 'PASS_BUNDLE_UPDATED_NOT_GAME_TESTED'
    Path(path).with_name('result.json').write_text(json.dumps(plan, indent=2) + '\n')
    return Path(path).with_name('result.json')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='command', required=True)
    prep = commands.add_parser('prepare')
    prep.add_argument('--report', type=Path, required=True)
    prep.add_argument('--installed-report', type=Path, required=True)
    prep.add_argument('--seating-report', type=Path, required=True)
    install = commands.add_parser('apply')
    install.add_argument('plan', type=Path)
    args = parser.parse_args()
    print(prepare(args) if args.command == 'prepare' else apply(args.plan))
