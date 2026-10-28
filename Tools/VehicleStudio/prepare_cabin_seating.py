"""Stage measured rear-seat XML without changing the installed configuration."""
import copy
import hashlib
import json
import math
from pathlib import Path
import uuid
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
MOD = ROOT.parents[1]


def prepare(capacity=6, geometry=None):
    if capacity == 6:
        return prepare_six_plan(geometry)
    if capacity != 4:
        raise ValueError('The owner requested six occupants; only reviewed four-seat legacy staging or the six-seat plan is supported.')
    source = MOD / 'Config/_Vehicles/camper_vehicles.xml'
    original = source.read_bytes()
    guides = json.loads((ROOT / 'scenarios/camper_cabin_candidates.json').read_text())
    surface = ROOT / guides['sourceSurfaceReport']
    measurements = json.loads(surface.read_text())
    if measurements.get('status') != 'PASS_SURFACE_MEASUREMENTS_NOT_SEATING_ACCEPTANCE':
        raise ValueError('Measured furniture surfaces required.')
    tree = ET.fromstring(original)
    definitions = tree.findall("./append/vehicle[@name='rebirthCamper80s']")
    if len(definitions) != 1:
        raise ValueError('One installed camper definition required.')
    vehicle = definitions[0]
    seats = [part for part in vehicle.findall('property') if part.get('class', '').startswith('seat')]
    if [part.get('class') for part in seats] not in ([f'seat{i}' for i in range(n)] for n in (2, 4, 6)):
        raise ValueError('Expected two front seats or the reviewed four-seat layout; inspect changed source before staging.')
    template = seats[1]
    for rear in seats[2:]:
        vehicle.remove(rear)
    candidates = []
    for name in ('front_bench_pose41', 'rear_bench_pose41'):
        matches = [row for row in guides['candidates'] if row.get('name') == name]
        if len(matches) != 1:
            raise ValueError('Unique measured pose41 cushion guide required.')
        candidate = matches[0]
        values = candidate['attachment'] + [candidate['yaw']]
        if candidate['pose'] != 41 or len(values) != 4 or any(type(v) not in (int, float) or not math.isfinite(v) for v in values):
            raise ValueError('Finite native passenger layout required.')
        candidates.append(candidate)
    if math.dist(candidates[0]['attachment'], candidates[1]['attachment']) < .7:
        raise ValueError('Rear seat roots overlap the proposed passenger space.')
    for slot, candidate in enumerate(candidates, 2):
        seat = copy.deepcopy(template)
        seat.set('class', f'seat{slot}')
        for key, value in {
            'pose': '41',
            'position': ', '.join(format(v, '.8g') for v in candidate['attachment']),
            'rotation': f"0, {candidate['yaw']}, 0",
            'exit': '1.7, 0, -2.3 ~ -1.7, 0, -2.3 ~ 0, 0, -4.1 ~ 1.7, 0, 0',
        }.items():
            properties = seat.findall(f"property[@name='{key}']")
            if len(properties) != 1:
                raise ValueError('Native seat template property missing or duplicated: ' + key)
            properties[0].set('value', value)
        vehicle.append(seat)
    ET.indent(tree, space='  ')
    output = ET.tostring(tree, encoding='utf-8') + b'\n'
    # Existing driver, front passenger, steering/IK, physics and light settings
    # are cloned verbatim; only the two additional seat elements differ.
    staged_vehicle = ET.fromstring(output).find("./append/vehicle[@name='rebirthCamper80s']")
    for part in list(staged_vehicle):
        if part.get('class') in ('seat2', 'seat3', 'seat4', 'seat5'):
            staged_vehicle.remove(part)
    def semantic(node):
        return node.tag, tuple(sorted(node.attrib.items())), tuple(semantic(child) for child in node)
    baseline_vehicle = ET.fromstring(original).find("./append/vehicle[@name='rebirthCamper80s']")
    for part in list(baseline_vehicle):
        if part.get('class') in ('seat2', 'seat3', 'seat4', 'seat5'):
            baseline_vehicle.remove(part)
    if semantic(staged_vehicle) != semantic(baseline_vehicle):
        raise ValueError('Existing camper settings changed during staging.')
    folder = ROOT / 'reports/cabin_seating_candidates' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    (folder / 'baseline_camper_vehicles.xml').write_bytes(original)
    (folder / 'camper_vehicles.xml').write_bytes(output)
    report = {
        'status': 'STAGED_FOUR_OCCUPANT_CANDIDATE_REQUIRES_AVATAR_FIT',
        'capacity': 4, 'installed': False, 'visualAcceptance': False,
        'simultaneousOccupantFitAccepted': False,
        'source': str(source), 'sourceSha256': hashlib.sha256(original).hexdigest(),
        'surfaceReport': str(surface), 'surfaceSha256': hashlib.sha256(surface.read_bytes()).hexdigest(),
        'candidateSha256': hashlib.sha256(output).hexdigest(), 'rearSeats': candidates,
        'remaining': ['native hip/feet alignment', 'table and knee clearance',
                      'simultaneous shoulder clearance', 'entry/exit', 'multiplayer occupancy'],
    }
    (folder / 'result.json').write_text(json.dumps(report, indent=2))
    if source.read_bytes() != original:
        raise RuntimeError('Installed configuration changed concurrently; candidate requires review.')
    return folder


def prepare_six_plan(geometry):
    """Stage all six intended places without inventing an unsupported bed pose.

    This plan is deliberately not installable XML. A living-player lying clip,
    pelvis/root fit and runtime restoration are required before emitting seat4.
    """
    if geometry is None:
        raise ValueError('Reference-matched full furniture geometry report required for six occupants.')
    geometry = Path(geometry)
    measurements = json.loads(geometry.read_text())
    if (measurements.get('status') != 'PASS_REFERENCE_MATCHED_FBX_HORIZONTAL_GEOMETRY_NOT_AVATAR_FIT'
            or measurements.get('referenceMaxVertexError', 1) > .00001):
        raise ValueError('Geometry must match the existing measured Unity surface vertices.')
    for key in ('source', 'reference'):
        evidence = Path(measurements[key])
        if hashlib.sha256(evidence.read_bytes()).hexdigest() != measurements[key + 'Sha256']:
            raise ValueError('Changed geometry evidence: ' + key)
    source = MOD / 'Config/_Vehicles/camper_vehicles.xml'
    original = source.read_bytes()
    vehicle = ET.fromstring(original).find("./append/vehicle[@name='rebirthCamper80s']")
    if vehicle is None:
        raise ValueError('Camper definition required.')
    seats = [p for p in vehicle if p.get('class', '').startswith('seat')]
    if [p.get('class') for p in seats] not in ([f'seat{i}' for i in range(n)] for n in (2, 4, 6)):
        raise ValueError('Unreviewed baseline seats.')
    guides = json.loads((ROOT / 'scenarios/camper_cabin_candidates.json').read_text())
    occupants = []
    for slot, seat in enumerate(seats[:2]):
        occupants.append({'slot': slot, 'role': 'driver' if slot == 0 else 'front_passenger',
                          'preserveNativeSeat': ET.tostring(seat, encoding='unicode')})
    for slot, name in enumerate(('front_bench_pose41', 'rear_bench_pose41'), 2):
        guide = next(c for c in guides['candidates'] if c['name'] == name)
        occupants.append(dict(guide, slot=slot, role='dinette', avatarFitAccepted=False))

    def surface(name, height, xmin, xmax, zmin, zmax):
        selected = [f for f in measurements['horizontalFaces']
                    if abs(f['center'][1]-height) < .001
                    and all(xmin-.002 <= p[0] <= xmax+.002 and zmin-.002 <= p[2] <= zmax+.002
                            for p in (f['a'], f['b'], f['c']))]
        if len(selected) != 2:
            raise ValueError('Exactly two measured top-plane triangles required: ' + name)
        points = [p for f in selected for p in (f['a'], f['b'], f['c'])]
        minimum = [min(p[i] for p in points) for i in range(3)]
        maximum = [max(p[i] for p in points) for i in range(3)]
        return {'surface': name, 'minimum': minimum, 'maximum': maximum,
                'surfaceCenter': [(a+b)/2 for a, b in zip(minimum, maximum)],
                'sourceFaces': selected, 'avatarFitAccepted': False}

    bed = surface('elevated_bed_top', 1.919, -.773, .754, -.474, .262)
    bed_root = list(bed['surfaceCenter'])
    bed_root[0] += .12
    bed_root[1] += .12
    occupants.append(dict(bed, slot=4, role='lying_bed', pose=41, attachment=bed_root, yaw=0,
                          nativeLyingPoseVerified=False, poseStatus='AUTHORED_HUMANOID_LYING_CLIP_REQUIRES_FIT',
                          note='Straight adult body may exceed measured bed width; qualify bent knees/side posture.'))
    sink = surface('sink_adjacent_top', 1.538, -.866, -.263, -1.168, -.602)
    sink_root = list(sink['surfaceCenter'])
    sink_root[1] -= .9005  # Same provisional pelvis/root offset as the dinette candidate.
    occupants.append(dict(sink, slot=5, role='sink_adjacent_seated', pose=41, attachment=sink_root, yaw=90,
                          poseStatus='SEATED_PELVIS_OFFSET_REQUIRES_AVATAR_FIT',
                          note='Use actual pelvis offset; test legs, sink/cabinet, gas bottle and aisle clearance.'))
    folder = ROOT / 'reports/cabin_seating_candidates' / uuid.uuid4().hex
    folder.mkdir(parents=True)
    (folder / 'baseline_camper_vehicles.xml').write_bytes(original)
    for rear in seats[2:]:
        vehicle.remove(rear)
    template = seats[1]
    exits = [[1.7, 0, -2.3], [-1.7, 0, -2.3], [0, 0, -4.1], [1.7, 0, 0]]
    authoring = []
    for occupant in occupants[2:]:
        seat = copy.deepcopy(template)
        seat.set('class', 'seat' + str(occupant['slot']))
        for key, value in {'pose': '41', 'position': ', '.join(format(v, '.8g') for v in occupant['attachment']),
                           'rotation': '0, ' + str(occupant['yaw']) + ', 0',
                           'exit': ' ~ '.join(', '.join(str(v) for v in point) for point in exits)}.items():
            fields = seat.findall(f"property[@name='{key}']")
            if len(fields) != 1:
                raise ValueError('Native passenger field unavailable: ' + key)
            fields[0].set('value', value)
        vehicle.append(seat)
        def vec(values):
            return dict(zip(('x', 'y', 'z'), values))
        authoring.append({'index': occupant['slot'], 'pose': 41, 'position': vec(occupant['attachment']),
                          'rotation': vec([0, occupant['yaw'], 0]), 'hipGuide': vec(occupant.get('surfaceCenter', occupant['attachment'])),
                          'exits': [vec(point) for point in exits]})
    tree = ET.fromstring(original)
    old_vehicle = tree.find("./append/vehicle[@name='rebirthCamper80s']")
    parent = tree.find('./append')
    parent.remove(old_vehicle)
    parent.append(vehicle)
    ET.indent(tree, space='  ')
    candidate = ET.tostring(tree, encoding='utf-8') + b'\n'
    (folder / 'camper_vehicles.xml').write_bytes(candidate)
    report = {'status': 'STAGED_SIX_OCCUPANT_CANDIDATE_REQUIRES_AVATAR_FIT',
              'capacity': 6, 'installed': False, 'installable': False, 'visualAcceptance': False,
              'requiresAuthoredBedPoseBundle': True, 'authoringSeats': authoring,
              'candidateSha256': hashlib.sha256(candidate).hexdigest(),
              'source': str(source), 'sourceSha256': hashlib.sha256(original).hexdigest(),
              'geometryReport': str(geometry), 'geometrySha256': hashlib.sha256(geometry.read_bytes()).hexdigest(),
              'occupants': occupants, 'unchangedFrontSeats': True,
              'remaining': ['generate/verify authored humanoid lying clip and production runtime hook',
                            'qualified pelvis/root offsets', 'simultaneous six-avatar comfort/clearance',
                            'safe entry/exit and replicated occupancy']}
    (folder / 'result.json').write_text(json.dumps(report, indent=2))
    if source.read_bytes() != original:
        raise RuntimeError('Installed configuration changed concurrently.')
    return folder


def validate_stage(report_path, bed_pose_report=None):
    report_path = Path(report_path)
    report = json.loads(report_path.read_text())
    six = report.get('capacity') == 6
    if report.get('installable') is False and not six:
        raise ValueError('Measurement plan has unresolved occupant poses and cannot be installed.')
    if six and (not isinstance(bed_pose_report, dict) or bed_pose_report.get('prefabVerified') is not True
                or bed_pose_report.get('bedPoseAuthored') is not True
                or bed_pose_report.get('bedPoseClipName') != 'rebirthCamperBedLying'
                or 'M/BedPoseAsset' not in bed_pose_report.get('transformPaths', [])):
        raise ValueError('Verified generated humanoid bed-pose bundle required for six-seat candidate.')
    folder = report_path.parent
    baseline = (folder / 'baseline_camper_vehicles.xml').read_bytes()
    candidate = (folder / 'camper_vehicles.xml').read_bytes()
    expected_status = 'STAGED_SIX_OCCUPANT_CANDIDATE_REQUIRES_AVATAR_FIT' if six else 'STAGED_FOUR_OCCUPANT_CANDIDATE_REQUIRES_AVATAR_FIT'
    capacity = 6 if six else 4
    if (report.get('status') != expected_status
            or report.get('capacity') != capacity or report.get('installed') is not False
            or report.get('visualAcceptance') is not False
            or hashlib.sha256(baseline).hexdigest() != report.get('sourceSha256')
            or hashlib.sha256(candidate).hexdigest() != report.get('candidateSha256')):
        raise ValueError('Candidate layout receipt or source fingerprints invalid.')
    tree = ET.fromstring(candidate)
    vehicle = tree.find("./append/vehicle[@name='rebirthCamper80s']")
    if vehicle is None:
        raise ValueError('Candidate camper definition missing.')
    seats = [s for s in vehicle if s.get('class', '').startswith('seat')]
    if [s.get('class') for s in seats] != [f'seat{i}' for i in range(capacity)]:
        raise ValueError('Exactly the declared contiguous candidate seats required.')
    for slot, seat in enumerate(seats):
        def value(name):
            properties = seat.findall(f"property[@name='{name}']")
            if len(properties) != 1:
                raise ValueError('Missing or duplicate seat field: ' + name)
            return properties[0].get('value')
        if value('pose') != ('40' if slot == 0 else '41') or not value('exit'):
            raise ValueError('Native seat pose or exits invalid.')
        if six and slot == 4 and any(prop.get('name', '').lower().startswith(('ikhand', 'ikfoot', 'lefthand', 'righthand', 'leftfoot', 'rightfoot')) for prop in seat.findall('property')):
            raise ValueError('Bed slot must not carry native sitting hand/foot IK targets.')
        for name in ('position', 'rotation'):
            numbers = [float(v.strip()) for v in value(name).split(',')]
            if len(numbers) != 3 or not all(math.isfinite(v) for v in numbers):
                raise ValueError('Seat coordinates must be finite triples.')
    for seat in seats[2:]:
        vehicle.remove(seat)
    def semantic(node):
        return node.tag, tuple(sorted(node.attrib.items())), tuple(semantic(child) for child in node)
    baseline_tree = ET.fromstring(baseline)
    baseline_vehicle = baseline_tree.find("./append/vehicle[@name='rebirthCamper80s']")
    if baseline_vehicle is None:
        raise ValueError('Baseline camper definition missing.')
    baseline_seats = [s for s in baseline_vehicle if s.get('class', '').startswith('seat')]
    if [s.get('class') for s in baseline_seats] not in ([f'seat{i}' for i in range(n)] for n in (2, 4, 6)):
        raise ValueError('Unreviewed baseline seating capacity.')
    for seat in baseline_seats[2:]:
        baseline_vehicle.remove(seat)
    if semantic(tree) != semantic(baseline_tree):
        raise ValueError('Candidate changes settings other than the two rear seats.')
    return report, baseline, candidate


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--capacity', type=int, choices=(4, 6), default=6)
    parser.add_argument('--geometry', type=Path)
    args = parser.parse_args()
    print(prepare(args.capacity, args.geometry))
