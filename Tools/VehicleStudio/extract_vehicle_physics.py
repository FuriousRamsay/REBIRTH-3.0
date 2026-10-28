"""Read serialized native physics without opening the game or modifying bundles."""
import argparse
import hashlib
import json
from pathlib import Path
import UnityPy

def extract(path):
    env = UnityPy.load(str(path))
    rows = []
    for asset in env.assets:
        objects = {o.path_id: o for o in asset.objects.values()}
        cache = {}
        def read(pid):
            if pid not in cache:
                cache[pid] = objects[pid].read_typetree()
            return cache[pid]
        def local_ref(pointer):
            if pointer['m_FileID'] != 0:
                raise ValueError('External physics reference requires dependency resolution')
            return pointer['m_PathID']
        def transform_path(pid):
            data = read(pid)
            name = read(local_ref(data['m_GameObject']))['m_Name']
            parent = local_ref(data['m_Father'])
            return (transform_path(parent) + '/' if parent else '') + name
        for obj in objects.values():
            if obj.type.name not in ('Rigidbody', 'WheelCollider', 'BoxCollider'):
                continue
            data = read(obj.path_id)
            go = read(local_ref(data['m_GameObject']))
            transform = next(local_ref(c['component']) for c in go['m_Component']
                if objects[local_ref(c['component'])].type.name == 'Transform')
            rows.append({'type': obj.type.name, 'path': transform_path(transform),
                'transform': read(transform), 'component': data})
    return {'source': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'scope': 'Serialized prefab physics; runtime overrides must be checked separately',
            'components': rows}

if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('bundle', type=Path)
    p.add_argument('output', type=Path)
    a = p.parse_args()
    result = extract(a.bundle.resolve(strict=True))
    a.output.parent.mkdir(parents=True, exist_ok=True)
    a.output.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(f"Extracted {len(result['components'])} serialized physics components")
