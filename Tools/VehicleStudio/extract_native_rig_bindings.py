"""Read-only extraction of exact native rig references; requires UnityPy."""
import argparse
import hashlib
import json
from pathlib import Path
import UnityPy


def extract(bundle):
    bundle = bundle.resolve(strict=True)
    env = UnityPy.load(str(bundle))
    objects = {o.path_id: o for o in env.objects}
    cache = {}

    def read(pid):
        if pid not in cache:
            cache[pid] = objects[pid].read_typetree()
        return cache[pid]

    def ref(value):
        if value['m_FileID'] != 0:
            raise ValueError('External reference requires dependency resolution')
        return value['m_PathID']

    def transform_path(pid):
        if not pid:
            return None
        data = read(pid)
        name = read(ref(data['m_GameObject']))['m_Name']
        parent = ref(data['m_Father'])
        return (transform_path(parent) + '/' if parent else '') + name

    def component_path(data):
        go = read(ref(data['m_GameObject']))
        transforms = [ref(c['component']) for c in go['m_Component']
                      if objects[ref(c['component'])].type.name == 'Transform']
        if len(transforms) != 1:
            raise ValueError('Ambiguous component transform')
        return transform_path(transforms[0])

    rows = []
    for obj in env.objects:
        if obj.type.name != 'MonoBehaviour':
            continue
        data = read(obj.path_id)
        if 'm_RigLayers' in data:
            rows.append(dict(kind='builder', path=component_path(data),
                layers=[dict(path=component_path(read(ref(x['m_Rig']))),
                             active=bool(x['m_Active'])) for x in data['m_RigLayers']]))
        elif isinstance(data.get('m_Data'), dict) and 'm_Root' in data['m_Data']:
            values = dict(data['m_Data'])
            for key in ('m_Root', 'm_Mid', 'm_Tip', 'm_Target', 'm_Hint'):
                values[key] = transform_path(ref(values[key]))
            rows.append(dict(kind='twoBone', path=component_path(data),
                             weight=data['m_Weight'], data=values))
    if not rows:
        raise ValueError('No native rig bindings found')
    return dict(source=str(bundle), sha256=hashlib.sha256(bundle.read_bytes()).hexdigest(),
                scope='Exact serialized references; not runtime pose validation', bindings=rows)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('bundle', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    result = extract(args.bundle)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2), encoding='utf-8')
    print(f"Extracted {len(result['bindings'])} binding records to {args.output}")
