"""Compare exported Unity source geometry with decoded bundle meshes.

Built-in primitives must additionally pass the Unity reference/buffer check.
"""
import argparse
import hashlib
import json
from pathlib import Path

import numpy as np


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def compare(folder, bundle=None, source_manifest=None):
    bundle = bundle or folder / 'rebirth_camper80s.unity3d'
    source = json.loads((source_manifest or folder / 'source-meshes.json').read_text())['meshes']
    receipt = json.loads((folder / 'decoded-meshes.receipt.json').read_text())
    bundle_sha = digest(bundle)
    if receipt['bundleSha256'] != bundle_sha or receipt['decodedSha256'] != digest(folder / 'decoded-meshes.json'):
        raise ValueError('Decoded buffers are absent, altered or bound to another bundle')
    decoded = json.loads((folder / 'decoded-meshes.json').read_text())
    rows, builtins = [], []
    for mesh in source:
        candidates = [m for m in decoded if m['name'] == mesh['name']]
        if not candidates and mesh['name'] in ('Cube', 'Cylinder'):
            builtins.append(mesh['path'])
            continue
        passed = False  # Never carry a previous mesh's result into this one.
        v = np.array([[p['x'], p['y'], p['z']] for p in mesh['vertices']], dtype=np.float32)
        uv = np.array([[p['x'], p['y']] for p in mesh['uv']], dtype=np.float32).astype(np.float64)
        for candidate in candidates:
            w = np.array(candidate['vertices'], dtype=np.float32)
            u = np.array(candidate['uv'], dtype=np.float64)[:, :2]
            t = np.array(candidate['triangles']).reshape(-1)
            nearest = uv.astype(np.float16).astype(np.float64)
            uv_ok = np.array_equal(uv, u) or (
                uv.shape == u.shape
                and np.array_equal(u, u.astype(np.float16).astype(np.float64))
                and np.array_equal(abs(uv - u), abs(uv - nearest))
            )
            passed = np.array_equal(v, w) and np.array_equal(mesh['triangles'], t) and uv_ok
            if passed:
                break
        rows.append({'path': mesh['path'], 'mesh': mesh['name'], 'pass': bool(passed)})
    report = {
        'status': 'PASS_EMBEDDED' if rows and all(r['pass'] for r in rows) else 'FAIL',
        'bundleSha256': bundle_sha,
        'scope': 'Embedded positions/indices exact; UV0 exact or nearest half, either midpoint tie. Builtins require separate Unity verification.',
        'rows': rows,
        'builtinPathsRequiringUnityVerification': builtins,
    }
    (folder / 'geometry-comparison.json').write_text(json.dumps(report, indent=2))
    print(report['status'], 'embedded:', len(rows), 'builtin:', len(builtins))
    return report['status'] == 'PASS_EMBEDDED'


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('folder', type=Path)
    parser.add_argument('--bundle', type=Path)
    parser.add_argument('--source-manifest', type=Path)
    args = parser.parse_args()
    raise SystemExit(0 if compare(args.folder, args.bundle, args.source_manifest) else 1)
