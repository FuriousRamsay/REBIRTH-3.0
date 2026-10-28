"""Install a reviewed seating candidate with a guarded backup on E:."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import uuid
from prepare_cabin_seating import MOD, validate_stage


def install(report_path, bundle_report=None):
    evidence = json.loads(Path(bundle_report).read_text()) if bundle_report else None
    report, baseline, candidate = validate_stage(report_path, bed_pose_report=evidence)
    target = MOD / 'Config/_Vehicles/camper_vehicles.xml'
    if Path(report['source']).resolve() != target.resolve() or target.read_bytes() != baseline:
        raise RuntimeError('Installed configuration changed; regenerate the candidate.')
    if report['capacity'] == 6:
        from camper_integration import BUNDLE, validate_candidate_report
        validate_candidate_report(evidence)
        if hashlib.sha256((MOD / 'Resources' / BUNDLE).read_bytes()).hexdigest() != evidence['bundleSha256']:
            raise RuntimeError('Install verified authored bed-pose bundle before six-seat XML.')
    probe = subprocess.run(['powershell', '-NoProfile', '-Command',
        'if(Get-Process -Name 7DaysToDie -ErrorAction SilentlyContinue){exit 1}'], check=False)
    if probe.returncode:
        raise RuntimeError('Game is running; seating installation refused.')
    folder = Path('E:/_Haven/shared/REBIRTHBackups/VehicleStudio') / uuid.uuid4().hex
    folder.mkdir(parents=True)
    backup = folder / 'camper_vehicles.xml'
    backup.write_bytes(baseline)
    receipt = {'status': 'PREPARED', 'candidateReport': str(Path(report_path).resolve()),
               'path': str(target), 'backup': str(backup), 'capacity': report['capacity'],
               'before': report['sourceSha256'], 'after': report['candidateSha256'],
               'gameTested': False, 'visualAcceptance': False}
    receipt_path = folder / 'installation.json'
    receipt_path.write_text(json.dumps(receipt, indent=2))
    temporary = target.with_name(target.name + '.seating-' + folder.name + '.tmp')
    try:
        temporary.write_bytes(candidate)
        if target.read_bytes() != baseline:
            raise RuntimeError('Concurrent configuration change; installation refused.')
        temporary.replace(target)
        if hashlib.sha256(target.read_bytes()).hexdigest() != receipt['after']:
            raise RuntimeError('Installed seating checksum mismatch; inspect preserved backup.')
        receipt['status'] = 'INSTALLED_CANDIDATE_REQUIRES_GAMEPLAY_FIT'
    except Exception as error:
        receipt.update(status='FAILED', error=str(error))
        raise
    finally:
        if temporary.exists():
            temporary.unlink()
        receipt_path.write_text(json.dumps(receipt, indent=2))
    return receipt_path


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--report', required=True, type=Path)
    parser.add_argument('--bundle-report', type=Path)
    args = parser.parse_args()
    print(install(args.report, args.bundle_report))
