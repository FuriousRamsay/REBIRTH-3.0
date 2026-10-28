"""Stage and integrate the camper into the existing REBIRTH mod using native XML.

Uses the preserved Vehicle Studio XML parser, hash and atomic-write helpers.
This adapter replaces historical standalone-modlet deployment assumptions.
"""
from __future__ import annotations
import argparse
import copy
import csv
from datetime import datetime, timezone
import hashlib
import io
import json
import math
from pathlib import Path
import sys
import uuid
import xml.etree.ElementTree as ET

STUDIO = Path(__file__).resolve().parent
sys.path.insert(0, str(STUDIO))
from calibrate_hand_frames import donor_to_camper
sys.path.insert(0, str(STUDIO / 'source' / 'tools'))
from rebirth_vehicle_mod import safe_xml_bytes, file_hash, atomic_write, write_json

VEHICLE = 'rebirthCamper80s'
ITEM = VEHICLE + 'Placeable'
BUNDLE = 'rebirth_camper80s.unity3d'
MARKER = 'REBIRTH_VEHICLE_STUDIO_CAMPER'
LOCALIZATION = {
    VEHICLE: '80s Camper Truck',
    VEHICLE + 'Desc': 'A camper truck with passenger seating and room for supplies.',
    ITEM: '80s Camper Truck',
    ITEM + 'Desc': 'Place the camper truck on clear ground. Passenger capacity follows its fitted cabin seats.',
}

def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()

def vector(value: dict) -> str:
    if any(isinstance(value[k], bool) or not isinstance(value[k], (int, float)) or not math.isfinite(value[k]) for k in ('x', 'y', 'z')):
        raise ValueError('Finite authored transform coordinates required')
    return ', '.join(format(float(value[k]), '.8g') for k in ('x', 'y', 'z'))

def placement_envelope(report: dict) -> dict:
    """Native clearance is root-centred in X/Z, but starts root+.05 in Y.

    CalcSpawnPosition initially lifts the root .14 above its ground hit. Do not
    hide below-ground authored geometry by enlarging a box upward.
    """
    center, size = report.get('boundsCenter'), report.get('boundsSize')
    if not isinstance(center, dict) or not isinstance(size, dict) or any(k not in center or k not in size for k in ('x', 'y', 'z')):
        raise ValueError('Malformed authored bounds: complete centre and size required')
    if any(type(row[k]) not in (int, float) or not math.isfinite(row[k]) for row in (center, size) for k in ('x', 'y', 'z')):
        raise ValueError('Nonfinite authored bounds refused')
    if any(size[k] <= 0 for k in ('x', 'y', 'z')):
        raise ValueError('Malformed authored bounds: positive sizes required')
    minimum = {k: center[k] - size[k]/2 for k in ('x', 'y', 'z')}
    maximum = {k: center[k] + size[k]/2 for k in ('x', 'y', 'z')}
    if minimum['y'] < -.14 or maximum['y'] <= .05:
        raise ValueError('Authored bounds violate native initial ground clearance or upward box height')
    return {'x': 2*max(abs(minimum['x']), abs(maximum['x'])) + .2,
            'y': maximum['y'] - .05 + .1,
            'z': 2*max(abs(minimum['z']), abs(maximum['z'])) + .2}

def set_prop(parent: ET.Element, name: str, value: str):
    found = parent.findall(f"property[@name='{name}']")
    if len(found) > 1:
        raise ValueError(f'Duplicate property {name}')
    if found:
        found[0].set('value', value)
    else:
        ET.SubElement(parent, 'property', name=name, value=value)

def patch(root_name: str, definition: ET.Element) -> bytes:
    root = ET.Element('configs')
    ET.SubElement(root, 'append', xpath='/' + root_name).append(definition)
    ET.indent(root, space='  ')
    return (ET.tostring(root, encoding='unicode') + '\n').encode()

def definitions(game: Path, report: dict) -> dict[str, bytes]:
    raw = {name: safe_xml_bytes((game / 'Data' / 'Config' / name).read_bytes())
           for name in ('vehicles.xml', 'entityclasses.xml', 'items.xml')}
    donor = copy.deepcopy(raw['vehicles.xml'].find("vehicle[@name='vehicleTruck4x4']"))
    entity = copy.deepcopy(raw['entityclasses.xml'].find("entity_class[@name='vehicleTruck4x4']"))
    item = copy.deepcopy(raw['items.xml'].find("item[@name='vehicleTruck4x4Placeable']"))
    if donor is None or entity is None or item is None:
        raise ValueError('Exact installed native 4x4 donors are missing')
    donor.set('name', VEHICLE)
    seats = report['seats']
    if (len(seats) < 2 or any(type(s.get('index')) is not int for s in seats)
            or [s['index'] for s in seats] != list(range(len(seats)))
            or [s.get('pose') for s in seats] != [40] + [41] * (len(seats)-1)):
        raise ValueError('Contiguous authored seats required: driver pose40 and passenger pose41')
    passenger_template = copy.deepcopy(donor.find("property[@class='seat1']"))
    if passenger_template is None:
        raise ValueError('Native front passenger seat donor missing')
    for part in list(donor.findall('property[@class]')):
        cls = part.get('class', '')
        if (cls.startswith('seat') and cls not in {f"seat{s['index']}" for s in seats}) or cls == 'plow':
            donor.remove(part)
            continue
        for prop in list(part.findall('property')):
            name = prop.get('name', '')
            # Preserve scalar upgrade effects; remove unsupported donor visual upgrade references.
            if name in ('modT', 'modHideT', 'modOnT', 'modRBT', 'modRot', 'particleOn', 'particleDamaged', 'particleBroken'):
                part.remove(prop)
    set_prop(donor, 'recipeName', ITEM)
    set_prop(donor, 'cameraDistance', '5.5, 7')
    if report.get('bodyDyeMaterial'):
        if report.get('dyePaintPath') != 'M/VisualBody/80sCamperTruck/body':
            raise ValueError('Dye must target the exact truck body renderer')
        set_prop(donor.find("property[@class='chassis']"), 'paint', report['dyePaintPath'])
    passenger_template = copy.deepcopy(donor.find("property[@class='seat1']"))
    for seat in seats:
        part = donor.find(f"property[@class='seat{seat['index']}']")
        if part is None:
            part = copy.deepcopy(passenger_template)
            part.set('class', f"seat{seat['index']}")
            donor.append(part)
        for key in ('position', 'rotation'):
            set_prop(part, key, vector(seat[key]))
        set_prop(part, 'pose', str(seat['pose']))
        if not seat['exits']:
            raise ValueError('Camper exits required')
        set_prop(part, 'exit', ' ~ '.join(vector(v) for v in seat['exits']))
    steering = donor.find("property[@class='handlebars']")
    # Camper mesh is authored in the local XZ plane: Y follows the tilted shaft.
    # The donor's local-Z rotation tilts this wheel instead of spinning its rim.
    set_prop(steering, 'steerAngle', '0, -60, 0')
    for name, key in (('IKHandLPosition', 'handLeftPosition'), ('IKHandRPosition', 'handRightPosition'),
                      ('IKHandLRotation', 'handLeftRotation'), ('IKHandRRotation', 'handRightRotation')):
        value = report[key]
        if name.endswith('Rotation') and report.get('handRotationFrame', 'donor-shaft') == 'donor-shaft':
            value = donor_to_camper(value)
        set_prop(steering, name, vector(value))
    headlight = donor.find("property[@class='headlight']")
    headlight_mesh = next((p for p in report['transformPaths'] if p.endswith('/headlights_glass')), None)
    if headlight_mesh is None:
        raise ValueError('Actual camper headlight mesh path missing')
    set_prop(headlight, 'matT', headlight_mesh)
    # Only explicit authored seats define capacity; donor upgrade tags cannot add seats.
    reference = f'#@modfolder:Resources/{BUNDLE}?{report["prefabName"]}'
    entity.set('name', VEHICLE)
    set_prop(entity, 'Class', 'EntityVJeep')
    set_prop(entity, 'Prefab', reference)
    item.set('name', ITEM)
    if report.get('bodyDyeMaterial'):
        set_prop(item, 'RebirthCamperRandomInitialDye', 'true')
    set_prop(item, 'Meshfile', reference)
    set_prop(item, 'CustomIcon', 'vehicleTruck4x4Placeable')
    set_prop(item, 'DescriptionKey', ITEM + 'Desc')
    set_prop(item, 'Tags', 'T3,vehicle,vengine,vfuel,vlight,vstorage,canHaveCosmetic')
    action = item.find("property[@class='Action1']")
    set_prop(action, 'Vehicle', VEHICLE)
    # Placement dimensions cover actual camper bounds, rather than the oversized donor dimensions.
    set_prop(action, 'VehicleSize', vector(placement_envelope(report)))
    return {
        'Config/_Vehicles/camper_vehicles.xml': patch('vehicles', donor),
        'Config/_Vehicles/camper_entityclasses.xml': patch('entity_classes', entity),
        'Config/_Vehicles/camper_items.xml': patch('items', item),
    }

def include_block(filename: str, newline: bytes = b'\n') -> bytes:
    return newline.join((f'  <!-- {MARKER}_BEGIN -->'.encode(),
                         f'  <include filename="{filename}"/>'.encode(),
                         f'  <!-- {MARKER}_END -->'.encode())) + newline

def add_include(existing: bytes | None, filename: str) -> tuple[bytes, bytes]:
    if existing is None:
        existing = b'<configs>\n</configs>\n'
    root = safe_xml_bytes(existing)
    if root.tag != 'configs':
        raise ValueError('Expected existing configs patch root')
    if any(p.get('filename') == filename for p in root.findall('include')):
        raise ValueError(f'Include already exists: {filename}')
    if existing.count(b'</configs>') != 1 or MARKER.encode() in existing:
        raise ValueError('Ambiguous closing root or existing camper integration marker')
    block = include_block(filename, b'\r\n' if b'\r\n' in existing else b'\n')
    combined = existing.replace(b'</configs>', block + b'</configs>', 1)
    safe_xml_bytes(combined)
    return combined, block

def localization_append(existing: bytes) -> tuple[bytes, bytes]:
    rows = list(csv.reader(io.StringIO(existing.decode('utf-8-sig'))))
    if not rows or rows[0] != ['Key', 'english']:
        raise ValueError('Expected existing REBIRTH Key,english localization format')
    keys = [r[0] for r in rows[1:] if r]
    if set(keys) & LOCALIZATION.keys():
        raise ValueError('Camper localization key already exists')
    newline = '\r\n' if b'\r\n' in existing else '\n'
    stream = io.StringIO(newline='')
    writer = csv.writer(stream, lineterminator=newline)
    writer.writerows(LOCALIZATION.items())
    addition = stream.getvalue().encode()
    if existing and not existing.endswith((b'\n', b'\r')):
        addition = newline.encode() + addition
    return existing + addition, addition

def validate_candidate_report(report):
    if report.get('status') != 'PASS_PLAYABLE_CANDIDATE_NOT_GAME_TESTED' or report.get('prefabVerified') is not True or report.get('originalsUnchanged') is not True:
        raise ValueError('Verified Unity playable candidate report required')
    rear = report.get('rearLampAuthored') is True
    if report.get('bedPoseAuthored') is True:
        if (report.get('bedPoseClipName') != 'rebirthCamperBedLying'
                or 'M/BedPoseAsset' not in report.get('transformPaths', [])
                or len(report.get('seats', [])) != 6
                or [s.get('index') for s in report['seats']] != list(range(6))):
            raise ValueError('Six contiguous cabin seats and verified native bed-pose dependency required')
    markers = report.get('directionalLamps')
    if markers is not None:
        if not rear or not isinstance(markers, dict):
            raise ValueError('Directional marker lamps require authored rear lamps')
        for field in ('lightCount', 'roofTriangles', 'redBodyTriangles', 'preservedAmberTriangles', 'triangles'):
            if type(markers.get(field)) is not int or markers[field] <= 0:
                raise ValueError('Measured directional marker fields required')
        if (markers['lightCount'] < 2 or markers['triangles'] != markers['roofTriangles'] + markers['redBodyTriangles']
                or len(markers.get('markerPositions', [])) != markers['lightCount']
                or len(markers.get('markerDirections', [])) != markers['lightCount']
                or not {'M/BodyMarkerLights', 'M/VisualBody/80sCamperTruck/lights/BodyMarkerLenses'}.issubset(report.get('transformPaths', []))):
            raise ValueError('Measured directional marker geometry and emitter layout required')
    expected_counts = {'rendererCount': 17 if rear else 16, 'wheelColliderCount': 4,
                       'bodyColliderCount': 2, 'spotLightCount': 4 if rear else 2}
    if markers is not None:
        expected_counts.update(rendererCount=18, spotLightCount=4 + markers['lightCount'])
    for field, expected in expected_counts.items():
        if type(report.get(field)) is not int or report[field] != expected:
            raise ValueError(f'Candidate {field} invalid')
    if rear:
        paths = set(report.get('transformPaths', []))
        required = {'M/VisualBody/80sCamperTruck/lights/RearRedLenses', 'M/RearLights/LeftRed', 'M/RearLights/RightRed'}
        if not required.issubset(paths) or type(report.get('rearLampTriangles')) is not int or report['rearLampTriangles'] <= 0:
            raise ValueError('Verified separate rear lens geometry and red emitters required')


def stage(args) -> dict:
    mod = args.mod.resolve(strict=True)
    if mod.name != 'zzz_REBIRTH__3_0' or not (mod / 'ModInfo.xml').is_file():
        raise ValueError('Target must be the authoritative existing REBIRTH mod')
    report = json.loads(args.result.read_text(encoding='utf-8-sig'))
    validate_candidate_report(report)
    bundle = Path(report['bundlePath'])
    if file_hash(bundle) != report['bundleSha256']:
        raise ValueError('Bundle changed since Unity verification')
    # Scan both installation locations for conflicting IDs. Historical Tools source is not game configuration.
    roots = [args.game.resolve() / 'Mods', Path.home() / 'AppData/Roaming/7DaysToDie/Mods']
    for mods in roots:
        if not mods.is_dir():
            continue
        for config in mods.glob('*/Config/**/*.xml'):
            xml = safe_xml_bytes(config.read_bytes())
            for node in xml.iter():
                if node.tag in ('vehicle', 'entity_class', 'item') and node.get('name') in (VEHICLE, ITEM):
                    raise ValueError(f'Conflicting camper definition in {config}')
    files = definitions(args.game, report)
    includes = {'Config/vehicles.xml': '_Vehicles/camper_vehicles.xml',
                'Config/entityclasses.xml': '_Vehicles/camper_entityclasses.xml',
                'Config/items.xml': '_Vehicles/camper_items.xml'}
    operations = []
    for rel, name in includes.items():
        path = mod / rel
        before = path.read_bytes() if path.exists() else None
        files[rel], addition = add_include(before, name)
        operations.append({'path': rel, 'mode': 'include', 'addition': addition.decode(), 'beforeSha256': sha(before) if before is not None else None})
    rel = 'Config/Localization.csv'
    before = (mod / rel).read_bytes()
    files[rel], addition = localization_append(before)
    operations.append({'path': rel, 'mode': 'localization', 'addition': addition.decode(), 'beforeSha256': sha(before)})
    for rel in list(files):
        if rel not in includes and rel != 'Config/Localization.csv':
            if (mod / rel).exists():
                raise ValueError(f'Generated destination already exists: {rel}')
            operations.append({'path': rel, 'mode': 'new', 'beforeSha256': None})
    if (mod / 'Resources' / BUNDLE).exists():
        raise ValueError('Camper bundle already exists; require a versioned update receipt')
    operations.append({'path': 'Resources/' + BUNDLE, 'mode': 'new', 'beforeSha256': None})
    folder = STUDIO / 'reports' / 'integration' / uuid.uuid4().hex
    for rel, content in files.items():
        dest = folder / 'staged' / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_bytes(content)
    destination = folder / 'staged' / 'Resources' / BUNDLE
    destination.parent.mkdir(parents=True, exist_ok=True)
    import shutil
    shutil.copyfile(bundle, destination)
    for op in operations:
        op['afterSha256'] = file_hash(folder / 'staged' / op['path'])
    plan = {'kind': 'REBIRTH_CAMPER_INTEGRATION_PLAN', 'modRoot': str(mod), 'gameRoot': str(args.game.resolve()),
            'unityResult': str(args.result.resolve()), 'gameTested': False, 'status': 'PASS_STAGED_NOT_DEPLOYED',
            'operations': operations, 'localizationKeys': list(LOCALIZATION),
            'rollback': 'Remove only recorded owned includes, localization rows and unchanged generated files; preserve concurrent unrelated edits.'}
    write_json(folder / 'plan.json', plan)
    return {'status': plan['status'], 'plan': str(folder / 'plan.json'), 'files': list(files) + ['Resources/' + BUNDLE]}

def apply(plan_file: Path) -> dict:
    plan = json.loads(plan_file.read_text(encoding='utf-8-sig'))
    folder = plan_file.parent
    mod = Path(plan['modRoot'])
    if (folder / 'deployment.json').exists():
        raise ValueError('Plan already deployed')
    for op in plan['operations']:
        dest = mod / op['path']
        actual = file_hash(dest) if dest.exists() else None
        if actual != op['beforeSha256']:
            raise ValueError(f"Concurrent change: {op['path']}; regenerate stage from current files")
        if file_hash(folder / 'staged' / op['path']) != op['afterSha256']:
            raise ValueError('Staged contents changed')
    write_json(folder / 'deployment_intent.json', plan)
    completed = []
    try:
        for op in plan['operations']:
            dest = mod / op['path']
            before = dest.read_bytes() if dest.exists() else None
            if (sha(before) if before is not None else None) != op['beforeSha256']:
                raise ValueError(f"Concurrent change immediately before write: {op['path']}")
            if before is not None:
                backup = folder / 'backups' / op['path']
                backup.parent.mkdir(parents=True, exist_ok=True)
                backup.write_bytes(before)
            atomic_write(dest, (folder / 'staged' / op['path']).read_bytes())
            completed.append(op)
        receipt = dict(plan, status='PASS_DEPLOYED_NOT_GAME_TESTED', deployedUtc=datetime.now(timezone.utc).isoformat())
        write_json(folder / 'deployment.json', receipt)
        return {'status': receipt['status'], 'receipt': str(folder / 'deployment.json')}
    except Exception:
        rollback_ops(mod, completed)
        raise

def rollback_ops(mod: Path, operations: list):
    for op in reversed(operations):
        dest = mod / op['path']
        if op['mode'] == 'new':
            if dest.exists() and file_hash(dest) != op['afterSha256']:
                raise ValueError(f'Owned file modified after deployment: {dest}')
            dest.unlink(missing_ok=True)
        else:
            current = dest.read_bytes()
            addition = op['addition'].encode()
            if current.count(addition) != 1:
                raise ValueError(f'Owned addition missing/modified: {dest}')
            restored = current.replace(addition, b'', 1)
            if op['beforeSha256'] is None and restored == b'<configs>\n</configs>\n':
                dest.unlink()
            else:
                atomic_write(dest, restored)

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest='command', required=True)
    p = sub.add_parser('stage'); p.add_argument('--result', type=Path, required=True)
    p.add_argument('--mod', type=Path, default=STUDIO.parents[1]); p.add_argument('--game', type=Path, default=STUDIO.parents[3])
    p = sub.add_parser('apply'); p.add_argument('plan', type=Path)
    p = sub.add_parser('rollback'); p.add_argument('receipt', type=Path)
    args = parser.parse_args()
    print(f'[1 {args.command.upper()}] Using actual installed configuration and recorded Unity evidence', flush=True)
    try:
        if args.command == 'stage': result = stage(args)
        elif args.command == 'apply': result = apply(args.plan)
        else:
            receipt = json.loads(args.receipt.read_text(encoding='utf-8-sig'))
            rollback_ops(Path(receipt['modRoot']), receipt['operations'])
            result = {'status': 'PASS_ROLLED_BACK'}
            write_json(args.receipt.parent / 'rollback.json', result)
        print('[2 PASS] ' + json.dumps(result), flush=True)
        return 0
    except Exception as exc:
        print('[FAIL] ' + str(exc), file=sys.stderr, flush=True)
        return 1

if __name__ == '__main__':
    raise SystemExit(main())
