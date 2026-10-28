"""Compare completed native input traces without claiming gameplay acceptance."""
import argparse
import json
import math
from pathlib import Path


def number(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError('Missing or non-finite native measurement')
    return value


def validate_native_rear_lamps(data):
    """Require actual brake and reverse states with both red emitters in native traces."""
    evidence = {}
    for phase, field in ((3, 'braking'), (4, 'reverse')):
        samples = [s for s in data.get('samples', []) if s.get('phaseIndex') == phase and
                   (s.get('braking') is True if field == 'braking' else number(s.get('forwardVelocity')) < -.05)]
        if len(samples) < 2:
            raise ValueError('Native trace did not exercise actual ' + field)
        matched = []
        for sample in samples[-2:]:
            rear = [light for light in sample.get('lights', []) if '/RearLights/' in '/' + light.get('path', '')]
            if len(rear) != 2:
                raise ValueError('Both native rear emitters required during ' + field)
            for light in rear:
                color = light.get('color', [])
                if len(color) != 3 or number(color[0]) < .99 or abs(number(color[1])) > .001 or abs(number(color[2])) > .001 or \
                        light.get('active') is not True or light.get('enabled') is not True or abs(number(light.get('intensity')) - 1.5) > .001:
                    raise ValueError('Actual ' + field + ' did not produce both red rear emitters at native intensity1.5')
            matched.append(sample['time'])
        evidence[field] = {'sampleCount': len(matched), 'times': matched}
    return evidence


def load(path, expected_name):
    data = json.loads(path.read_text(encoding='utf-8-sig'))
    if (data.get('schema') != 3 or data.get('vehicleName') != expected_name
            or data.get('status') != 'CAPTURED_NATIVE_INPUT_SEQUENCE_NOT_ACCEPTANCE'
            or data.get('failure')):
        raise ValueError('Incomplete or incompatible vehicle sequence: ' + str(path))
    if number(data.get('mass')) <= 0:
        raise ValueError('Invalid vehicle mass')
    if (number(data.get('finalHealth')) < number(data.get('startingHealth'))
            or number(data.get('finalPlayerHealth')) < number(data.get('startingPlayerHealth'))):
        raise ValueError('A damaged vehicle or driver cannot establish matched handling')
    screenshots = data.get('screenshots', [])
    if len(screenshots) != 9:
        raise ValueError('All nine phase screenshots are required')
    for image in screenshots:
        image_path = Path(image)
        if not image_path.is_absolute() or not image_path.is_file():
            raise ValueError('Missing screenshot: ' + image)
        with image_path.open('rb') as stream:
            if stream.read(8) != b'\x89PNG\r\n\x1a\n':
                raise ValueError('Invalid screenshot signature: ' + image)
    phases = {}
    for index in range(9):
        samples = [s for s in data.get('samples', []) if s.get('phaseIndex') == index]
        if not samples:
            raise ValueError('Missing phase samples: ' + str(index))
        phase_names = {s['phase'] for s in samples}
        if len(phase_names) != 1:
            raise ValueError('Conflicting phase names')
        times = [number(s['time']) for s in samples]
        if any(b <= a for a, b in zip(times, times[1:])):
            raise ValueError('Non-increasing measurement times')
        for sample in samples:
            for field, size in [('velocity', 3), ('localVelocity', 3), ('angularVelocity', 3), ('rotation', 4)]:
                vector = sample.get(field, [])
                if len(vector) != size:
                    raise ValueError('Invalid native vector: ' + field)
                for component in vector:
                    number(component)
        speeds = [math.sqrt(sum(v*v for v in s['velocity'])) for s in samples]
        phases[str(index)] = {
            'action': next(iter(phase_names)), 'sampleCount': len(samples),
            'observedDuration': times[-1] - times[0],
            'peakSpeed': max(speeds), 'endSpeed': speeds[-1],
            'peakTiltDegrees': max(number(s.get('tiltDegrees')) for s in samples),
            'peakYawRate': max(abs(s['angularVelocity'][1]) for s in samples),
            'endForwardSpeed': samples[-1]['localVelocity'][2],
            'healthLost': number(samples[0]['health']) - min(number(s['health']) for s in samples),
        }
    return data, phases


def compare(native_path, camper_path):
    native, baseline = load(native_path, 'vehicleTruck4x4')
    camper, candidate = load(camper_path, 'rebirthCamper80s')
    differences = {}
    for phase, donor in baseline.items():
        truck = candidate[phase]
        if donor['action'] != truck['action']:
            raise ValueError('Vehicle input phases differ')
        differences[phase] = {'action': donor['action']}
        for metric in ('peakSpeed', 'endSpeed', 'peakTiltDegrees', 'peakYawRate', 'endForwardSpeed'):
            differences[phase][metric] = {'native': donor[metric], 'camper': truck[metric],
                'difference': truck[metric] - donor[metric],
                'ratio': truck[metric] / donor[metric] if abs(donor[metric]) > 1e-6 else None}
    return {'kind': 'NATIVE_VEHICLE_SEQUENCE_COMPARISON', 'status': 'MEASURED_NOT_ACCEPTANCE',
        'sources': [str(native_path.resolve()), str(camper_path.resolve())],
        'nativeMass': native['mass'], 'camperMass': camper['mass'],
        'nativePhases': baseline, 'camperPhases': candidate, 'differences': differences,
        'limitations': 'Requires matched track/start conditions, full-log health evidence and visual/audio review. No handling acceptance threshold is inferred.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('native', type=Path)
    parser.add_argument('camper', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    result = compare(args.native, args.camper)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False), encoding='utf-8')
    print(result['status'] + ': ' + str(args.output.resolve()))
