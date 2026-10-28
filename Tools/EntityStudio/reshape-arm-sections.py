"""Non-destructive Farmer upper-arm section diagnostic; preserves each section's area.

Works on exchange JSON, never saved Unity assets. The explicit x band is a Farmer
review parameter, not yet a generic anatomical landmark detector.
"""
import argparse
import hashlib
import json
from pathlib import Path
import numpy as np

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('source', type=Path)
parser.add_argument('output', type=Path)
args = parser.parse_args()
if args.source.resolve() == args.output.resolve():
    raise RuntimeError('Use a separate candidate folder')
args.output.mkdir(parents=True, exist_ok=True)
source_bytes = (args.source/'Input.json').read_bytes()
data = json.loads(source_bytes)
normal_data = json.loads((args.source/'SourceNormals.json').read_text())
if normal_data['inputSha256'] != hashlib.sha256(source_bytes).hexdigest():
    raise RuntimeError('Source normal checksum mismatch')

def array(values):
    return np.array([[v['x'], v['y'], v['z']] for v in values], dtype=float)

def objects(values):
    return [dict(zip(('x', 'y', 'z'), map(float, row))) for row in values]

def smoothstep(lo, hi, value):
    t = np.clip((value-lo)/(hi-lo), 0, 1)
    return t*t*(3-2*t)

source_points = array(data['vertices'])
source_normals = array(normal_data['normals'])
report = {'scope': 'Temporary Farmer geometry candidate, no Unity assets saved', 'arms': []}
for part in (1, 2):
    arm = json.loads((args.source/f'Arm{part}_Transferred.json').read_text())
    points = array(arm['vertices'])
    knots = np.arange(.18, .641, .01)
    centers, scales = [], []
    for x in knots:
        section = points[np.abs(np.abs(points[:, 0])-x) < .02]
        if len(section) < 8:
            raise RuntimeError(f'Insufficient section samples at {x}')
        low, high = np.quantile(section[:, 1:], [.02, .98], axis=0)
        centers.append((low+high)*.5)
        span = high-low
        # Equalize the two radii without adding section area. Upper bound avoids
        # an extreme correction if a narrow sample slice contains an outlier.
        scale = np.clip(np.sqrt(span[1]/span[0]), 1, 1.65)
        blend = smoothstep(.19, .27, x)*(1-smoothstep(.43, .61, x))
        scales.append(np.exp(np.log(scale)*blend))
    centers = np.array(centers)
    scales = np.array(scales)

    def deform(values):
        result = values.copy()
        x = np.abs(values[:, 0])
        cy = np.interp(x, knots, centers[:, 0])
        cz = np.interp(x, knots, centers[:, 1])
        scale = np.interp(x, knots, scales, left=1, right=1)
        result[:, 1] = cy+(values[:, 1]-cy)*scale
        result[:, 2] = cz+(values[:, 2]-cz)/scale
        return result

    changed = deform(points)
    arm['vertices'] = objects(changed)
    # Normals will be regenerated for this changed target surface during bake.
    arm.pop('normals', None)
    (args.output/f'Arm{part}_Transferred.json').write_text(json.dumps(arm))
    selected = np.array(data['parts']) == part
    original = source_points[selected].copy()
    source_points[selected] = deform(original)
    # Transform exact saved source normals by the deformation Jacobian inverse
    # transpose, including its longitudinal derivative. No guessed face normals.
    eps = 1e-5
    columns = []
    for dimension in range(3):
        delta = np.zeros(3)
        delta[dimension] = eps
        columns.append((deform(original+delta)-deform(original-delta))/(2*eps))
    jacobian = np.stack(columns, axis=2)
    determinants = np.linalg.det(jacobian)
    if not np.all(np.isfinite(jacobian)) or np.min(determinants) < .99:
        raise RuntimeError('Invalid or non-area-preserving deformation')
    normals = np.linalg.solve(jacobian.transpose(0, 2, 1), source_normals[selected, :, None])[:, :, 0]
    lengths = np.linalg.norm(normals, axis=1)
    valid = lengths > 1e-12
    normals[valid] /= lengths[valid, None]
    normals[~valid] = 0  # Preserve Unity's zero normals on unused/degenerate vertices.
    if not np.all(np.isfinite(normals)):
        raise RuntimeError('Normal transformation produced nonfinite values')
    source_normals[selected] = normals
    report['arms'].append({'part': part, 'maximumDisplacement': float(np.linalg.norm(changed-points, axis=1).max()),
                           'maximumVerticalScale': float(scales.max()), 'minimumJacobianDeterminant': float(determinants.min()),
                           'maximumJacobianDeterminant': float(determinants.max()), 'zeroSourceNormals': int((~valid).sum())})

data['vertices'] = objects(source_points)
output_bytes = json.dumps(data).encode()
(args.output/'Input.json').write_bytes(output_bytes)
normal_data.update(inputSha256=hashlib.sha256(output_bytes).hexdigest(), normals=objects(source_normals))
(args.output/'SourceNormals.json').write_text(json.dumps(normal_data))
report['originalInputSha256'] = hashlib.sha256(source_bytes).hexdigest()
report['candidateInputSha256'] = normal_data['inputSha256']
(args.output/'SectionReshape.json').write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
