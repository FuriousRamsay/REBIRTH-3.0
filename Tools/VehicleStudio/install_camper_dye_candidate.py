"""Guarded update of the three owned camper files; no shared root XML or DLL writes."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import uuid
import xml.etree.ElementTree as ET

def digest(data):
    return hashlib.sha256(data).hexdigest()

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--report', required=True, type=Path)
    parser.add_argument('--expected-bundle-sha256', required=True)
    args = parser.parse_args()
    studio = Path(__file__).resolve().parent
    mod = studio.parent.parent
    probe = subprocess.run(['powershell', '-NoProfile', '-Command',
        'if(Get-Process 7DaysToDie -ErrorAction SilentlyContinue){exit 1}'], check=False)
    if probe.returncode:
        raise RuntimeError('Game must be closed; leave the running session untouched.')
    report = json.loads(args.report.read_text(encoding='utf-8-sig'))
    if report['status'] != 'PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED' or not report['originalsUnchanged'] or not report['prefabVerified']:
        raise ValueError('Candidate receipt does not qualify for installation.')
    paint = 'M/VisualBody/80sCamperTruck/body'
    if report.get('dyePaintPath') != paint or report.get('bodyDyeMaterial') is not True:
        raise ValueError('Candidate does not contain the isolated body dye material.')
    bundle = Path(report['bundlePath']).read_bytes()
    if digest(bundle) != report['bundleSha256']:
        raise ValueError('Candidate bundle checksum differs from receipt.')
    paths = [mod / 'Resources/rebirth_camper80s.unity3d',
             mod / 'Config/_Vehicles/camper_vehicles.xml', mod / 'Config/_Vehicles/camper_items.xml']
    before = [p.read_bytes() for p in paths]
    if digest(before[0]) != args.expected_bundle_sha256.lower():
        raise ValueError('Installed bundle differs from the explicit expected checksum.')
    vehicle = ET.fromstring(before[1]).find("append/vehicle[@name='rebirthCamper80s']")
    chassis = vehicle.find("property[@class='chassis']/property[@name='paint']")
    if chassis is None or chassis.get('value') != 'M':
        raise ValueError('Installed paint configuration differs from the expected original.')
    old_paint = b'<property name="paint" value="M" />'
    if before[1].count(old_paint) != 1:
        raise ValueError('Paint source is ambiguous.')
    item = ET.fromstring(before[2]).find("append/item[@name='rebirthCamper80sPlaceable']")
    if item is None or item.find("property[@name='RebirthCamperRandomInitialDye']") is not None:
        raise ValueError('Item definition missing or initial dye already configured.')
    marker = b'<item name="rebirthCamper80sPlaceable">'
    if before[2].count(marker) != 1:
        raise ValueError('Item source is ambiguous.')
    after = [bundle, before[1].replace(old_paint, f'<property name="paint" value="{paint}" />'.encode()),
        before[2].replace(marker, marker + b'\n      <property name="RebirthCamperRandomInitialDye" value="true" />')]
    for xml in after[1:]:
        ET.fromstring(xml)
    folder = studio / 'reports/dye_candidate_install' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    receipt = {'status': 'PREPARED', 'candidateRunId': report['runId'], 'gameTested': False,
               'sharedDllChanged': False, 'files': []}
    for path, old, new in zip(paths, before, after):
        backup = folder / path.name
        backup.write_bytes(old)
        receipt['files'].append({'path': str(path), 'backup': str(backup), 'before': digest(old), 'after': digest(new)})
    receipt_path = folder / 'result.json'
    receipt_path.write_text(json.dumps(receipt, indent=2))
    changed = []
    try:
        for path, old, new in zip(paths, before, after):
            if path.read_bytes() != old:
                raise RuntimeError(f'Concurrent change detected: {path}')
            temporary = path.with_name(path.name + '.camper-' + folder.name + '.tmp')
            temporary.write_bytes(new)
            temporary.replace(path)
            changed.append((path, old, new))
            if digest(path.read_bytes()) != digest(new):
                raise RuntimeError(f'Installed checksum mismatch: {path}')
        receipt['status'] = 'PASS_INSTALLED_NOT_GAME_TESTED'
    except Exception as error:
        receipt['status'] = 'FAIL'
        receipt['error'] = str(error)
        for path, old, new in reversed(changed):
            if path.read_bytes() == new:
                path.write_bytes(old)
            else:
                receipt.setdefault('rollbackConflicts', []).append(str(path))
        raise
    finally:
        receipt_path.write_text(json.dumps(receipt, indent=2))
        print(receipt_path)

if __name__ == '__main__':
    main()
