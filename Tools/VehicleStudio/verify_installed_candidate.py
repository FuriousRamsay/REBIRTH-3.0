"""Verify installed candidate artifacts; does not emulate the game's patch loader."""
import csv
import argparse
import io
import json
from pathlib import Path
import uuid
import xml.etree.ElementTree as ET
from camper_integration import STUDIO, VEHICLE, ITEM, BUNDLE, LOCALIZATION, file_hash, safe_xml_bytes, validate_candidate_report


def verify(mod, report, seating_report=None):
    validate_candidate_report(report)
    checks = []
    def require(condition, label):
        if not condition:
            raise ValueError(label)
        checks.append(label)
    require(file_hash(mod / 'Resources' / BUNDLE) == report['bundleSha256'], 'Installed bundle matches Unity reopen evidence')
    definitions = {}
    for category, tag, identifier in [('vehicles', 'vehicle', VEHICLE), ('entityclasses', 'entity_class', VEHICLE), ('items', 'item', ITEM)]:
        filename = f'_Vehicles/camper_{category}.xml'
        root = safe_xml_bytes((mod / 'Config' / (category + '.xml')).read_bytes())
        require(len(root.findall(f"include[@filename='{filename}']")) == 1, f'{category}: exactly one camper include')
        patch = safe_xml_bytes((mod / 'Config' / filename).read_bytes())
        matches = patch.findall(f"append/{tag}[@name='{identifier}']")
        require(len(matches) == 1, f'{category}: exactly one matching camper definition')
        definitions[category] = matches[0]
    vehicle = definitions['vehicles']
    seats = [p for p in vehicle.findall('property[@class]') if p.get('class').startswith('seat')]
    authored = report['seats']
    if seating_report is not None:
        from prepare_cabin_seating import validate_stage
        layout, _, candidate = validate_stage(seating_report, bed_pose_report=report)
        require((mod / 'Config/_Vehicles/camper_vehicles.xml').read_bytes() == candidate,
                'Installed seating matches separately fingerprinted cabin candidate')
        authored = [{'index': index, 'pose': 40 if index == 0 else 41} for index in range(layout['capacity'])]
    require(len(authored) >= 2 and [s['index'] for s in authored] == list(range(len(authored))), 'Candidate authored seats are contiguous')
    require({p.get('class') for p in seats} == {f"seat{i}" for i in range(len(authored))}, 'Installed capacity matches authored cabin layout; comfortable fit remains unverified')
    seats = [vehicle.find(f"property[@class='seat{i}']") for i in range(len(authored))]
    for index, seat in enumerate(seats):
        pose = 40 if index == 0 else 41
        require(authored[index]['pose'] == pose and seat.find("property[@name='pose']").get('value') == str(pose), f'Seat {index}: native pose {pose}')
        require(bool(seat.find("property[@name='exit']").get('value')), f'Seat {index}: exit candidates present')
    paths = set(report['transformPaths'])
    if report.get('rearLampAuthored'):
        require(report.get('rendererCount') == 17 and report.get('spotLightCount') == 4,
                'Rear-lamp candidate has measured renderer/emitter counts')
        require('M/VisualBody/80sCamperTruck/lights/RearRedLenses' in paths
                and all('M/RearLights/'+side in paths for side in ('LeftRed','RightRed')),
                'Separate rear red-lens and emitter targets present; runtime hook and lighting remain unverified')
    for part in vehicle.findall('property[@class]'):
        for prop in part.findall('property'):
            if prop.get('name') in ('transform', 'steerTransform', 'tireTransform', 'matT', 'paint'):
                require(prop.get('value') in paths, f"Prefab target exists: {part.get('class')}/{prop.get('name')}")
    require(all(f'Physics/Wheel{i}' in paths for i in range(4)), 'Four native physics wheel targets present')
    expected = f'#@modfolder:Resources/{BUNDLE}?{report["prefabName"]}'
    entity = definitions['entityclasses']
    item = definitions['items']
    if report.get('bodyDyeMaterial'):
        require(vehicle.find("property[@class='chassis']/property[@name='paint']").get('value') == report['dyePaintPath'], 'Dye paint targets isolated truck-body renderer')
        require(item.find("property[@name='RebirthCamperRandomInitialDye']").get('value') == 'true', 'Initial native dye opt-in configured; runtime patch acceptance remains unverified')
    require(entity.find("property[@name='Prefab']").get('value') == expected, 'Entity prefab resolves to installed candidate reference')
    require(entity.find("property[@name='Class']").get('value') == 'EntityVJeep', 'Native jeep runtime class selected')
    require(item.find("property[@name='Meshfile']").get('value') == expected, 'Placeable mesh uses same candidate reference')
    require(item.find("property[@class='Action1']/property[@name='Vehicle']").get('value') == VEHICLE, 'Placeable action links exact vehicle ID')
    tags = set(item.find("property[@name='Tags']").get('value').split(','))
    require(not tags.intersection({'vseat', 'vplow'}), 'Unsupported extra-seat/plow capability tags omitted')
    require('canHaveCosmetic' in tags, 'Native cosmetic-slot creation tag explicitly present')
    rows = list(csv.DictReader(io.StringIO((mod / 'Config/Localization.csv').read_text(encoding='utf-8-sig'))))
    for key, value in LOCALIZATION.items():
        matches = [r for r in rows if r.get('Key') == key]
        require(len(matches) == 1 and matches[0].get('english') == value, f'Unique correct localization: {key}')
    return checks


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--report', required=True, type=Path)
    parser.add_argument('--seating-report', type=Path)
    args = parser.parse_args()
    folder = STUDIO / 'reports' / 'installed_verification' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    result = {'kind': 'REBIRTH_CAMPER_INSTALLED_ARTIFACT_VERIFICATION', 'status': 'FAIL', 'gameTested': False,
              'scope': 'Installed candidate bytes, native target references and explicit definition structure. Does not emulate cross-mod patch ordering, inherited item resolution or certify runtime behavior.'}
    print('[1 VERIFY] Checking installed candidate against authoritative Unity build report', flush=True)
    try:
        report = json.loads(args.report.read_text(encoding='utf-8-sig'))
        result['checks'] = verify(STUDIO.parents[1], report, args.seating_report)
        result['status'] = 'PASS_INSTALLED_ARTIFACTS_NOT_GAME_TESTED'
    except Exception as exc:
        result['error'] = str(exc)
    (folder / 'result.json').write_text(json.dumps(result, indent=2) + '\n')
    print(f'[2 {result["status"]}] Report: {folder / "result.json"}', flush=True)
    raise SystemExit(0 if result['status'].startswith('PASS') else 1)
