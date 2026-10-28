"""Read vendor FBX geometry without importing or editing the Unity project.

This reports source coordinates; Unity transforms/axis conversion must be
cross-checked against existing measured Unity faces before using any anchor.
"""
import argparse
import json
import struct
import zlib
import math
import hashlib
from pathlib import Path


def read_fbx(path):
    raw = Path(path).read_bytes()
    if not raw.startswith(b'Kaydara FBX Binary  \x00\x1a\x00'):
        raise ValueError('Binary FBX required')
    version = struct.unpack_from('<I', raw, 23)[0]
    header = '<QQQB' if version >= 7500 else '<IIIB'
    header_size = struct.calcsize(header)

    def property_at(offset):
        kind = chr(raw[offset]); offset += 1
        formats = {'Y': 'h', 'C': '?', 'I': 'i', 'F': 'f', 'D': 'd', 'L': 'q'}
        if kind in formats:
            fmt = '<' + formats[kind]
            return struct.unpack_from(fmt, raw, offset)[0], offset + struct.calcsize(fmt)
        if kind in 'SR':
            length = struct.unpack_from('<I', raw, offset)[0]; offset += 4
            value = raw[offset:offset+length]
            return value.decode('utf-8', errors='replace') if kind == 'S' else value.hex(), offset + length
        if kind in 'fdlicb':
            count, encoding, length = struct.unpack_from('<III', raw, offset); offset += 12
            data = raw[offset:offset+length]
            if encoding == 1: data = zlib.decompress(data)
            elif encoding != 0: raise ValueError('Unsupported FBX array encoding')
            fmt = {'f': 'f', 'd': 'd', 'l': 'q', 'i': 'i', 'c': 'b', 'b': '?'}[kind]
            return list(struct.unpack('<' + str(count) + fmt, data)), offset + length
        raise ValueError('Unsupported FBX property ' + kind)

    def node_at(offset):
        end, count, _, length = struct.unpack_from(header, raw, offset)
        if end == 0: return None, offset + header_size
        offset += header_size
        name = raw[offset:offset+length].decode(); offset += length
        properties = []
        for _ in range(count):
            value, offset = property_at(offset); properties.append(value)
        children = []
        while offset < end:
            child, next_offset = node_at(offset)
            offset = next_offset
            if child is None: break
            children.append(child)
        return {'name': name, 'properties': properties, 'children': children}, end

    nodes = []; offset = 27
    while offset < len(raw):
        node, offset = node_at(offset)
        if node is None: break
        nodes.append(node)
    return nodes


def measure_cabin(source, reference):
    reference_path = Path(reference)
    nodes = read_fbx(source)
    objects = next(n for n in nodes if n['name'] == 'Objects')['children']
    model = next(n for n in objects if n['name'] == 'Model' and n['properties'][1].startswith('camper_interior\x00'))
    connections = next(n for n in nodes if n['name'] == 'Connections')['children']
    ids = [n['properties'][1] for n in connections if n['properties'][2] == model['properties'][0]]
    geometry = next(n for n in objects if n['name'] == 'Geometry' and n['properties'][0] in ids)
    props = next(n for n in model['children'] if n['name'] == 'Properties70')['children']
    values = {n['properties'][0]: n['properties'][4:] for n in props}
    if values['GeometricRotation'] != [-90.0, -0.0, 0.0]:
        raise ValueError('Unreviewed source mesh geometric rotation')
    tx, ty, tz = values['GeometricTranslation']
    raw_vertices = next(n for n in geometry['children'] if n['name'] == 'Vertices')['properties'][0]
    raw_indices = next(n for n in geometry['children'] if n['name'] == 'PolygonVertexIndex')['properties'][0]
    raw_points = list(zip(*[iter(raw_vertices)] * 3))
    reference = json.loads(Path(reference).read_text())
    check_points = {tuple(face[key][axis] for axis in 'xyz') for face in reference['upwardFaces'] for key in 'abc'}
    choices = []
    for x_sign in (-1, 1):
        vertices = [(x_sign * (x + tx), z + ty, -y + tz) for x, y, z in raw_points]
        error = max(min(math.dist(p, q) for q in vertices) for p in check_points)
        choices.append((error, x_sign, vertices))
    error, x_sign, vertices = min(choices)
    if error > .00001:
        raise ValueError('Source coordinates do not match measured Unity vertices: ' + str(error))
    faces = []; polygon = []; polygon_index = 0
    for raw_index in raw_indices:
        polygon.append(raw_index if raw_index >= 0 else -raw_index - 1)
        if raw_index >= 0: continue
        for i in range(1, len(polygon) - 1):
            a, b, c = [vertices[j] for j in (polygon[0], polygon[i], polygon[i+1])]
            u = [b[j] - a[j] for j in range(3)]; v = [c[j] - a[j] for j in range(3)]
            cross = [u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0]]
            area2 = math.sqrt(sum(n*n for n in cross))
            # FBX winding and Unity handedness can differ; explicit horizontal geometry only.
            if area2 > .00002 and abs(cross[1]) / area2 > .8:
                faces.append({'polygon': polygon_index, 'fanTriangle': i - 1, 'a': a, 'b': b, 'c': c,
                              'center': [(a[j]+b[j]+c[j])/3 for j in range(3)],
                              'area': area2 / 2})
        polygon = []
        polygon_index += 1
    return {'status': 'PASS_REFERENCE_MATCHED_FBX_HORIZONTAL_GEOMETRY_NOT_AVATAR_FIT',
            'source': str(source), 'sourceSha256': hashlib.sha256(Path(source).read_bytes()).hexdigest(),
            'reference': str(reference_path), 'referenceSha256': hashlib.sha256(reference_path.read_bytes()).hexdigest(),
            'referenceMaxVertexError': error, 'xSign': x_sign,
            'horizontalFaces': faces, 'note': 'Includes tops and undersides; select actual cushion/bed surfaces explicitly.'}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--surface-reference', type=Path)
    args = parser.parse_args()
    result = measure_cabin(args.source, args.surface_reference) if args.surface_reference else read_fbx(args.source)
    args.output.write_text(json.dumps(result, indent=2))
