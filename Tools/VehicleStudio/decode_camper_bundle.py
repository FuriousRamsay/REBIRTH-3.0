"""Decode actual packaged mesh buffers for independent comparison/CPU preview."""
import argparse
import hashlib
import json
import sys
from pathlib import Path


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def decode(bundle, folder):
    import UnityPy
    from UnityPy.helpers.MeshHelper import MeshHandler
    raw, preview = [], []
    for obj in UnityPy.load(str(bundle)).objects:
        if obj.type.name != 'Mesh':
            continue
        mesh = obj.read()
        handler = MeshHandler(mesh)
        handler.process()
        triangles = handler.get_triangles()
        raw.append(dict(name=mesh.m_Name, pathId=obj.path_id,
                        vertices=handler.m_Vertices, triangles=triangles, uv=handler.m_UV0))
        points = handler.m_Vertices
        normals = handler.m_Normals or []
        if len(normals) != len(points):
            raise ValueError(f'Missing actual normals: {mesh.m_Name}')
        preview.append(dict(name=mesh.m_Name,
                            vertices=[dict(zip('xyz', p[:3])) for p in points],
                            normals=[dict(zip('xyz', p[:3])) for p in normals],
                            tangents=[dict(zip('xyzw', p[:4])) for p in (handler.m_Tangents or [])],
                            uv=[dict(zip('xy', p[:2])) for p in (handler.m_UV0 or [])],
                            submeshes=[dict(indices=[i for tri in sub for i in tri]) for sub in triangles]))
    if not raw or len({r['name'] for r in raw}) != len(raw):
        raise ValueError('Empty or ambiguous mesh-name mapping; require object-ID mapping')
    folder.mkdir(parents=True, exist_ok=True)
    sha = digest(bundle)
    decoded = folder / 'decoded-meshes.json'
    readable = folder / 'preview-meshes.json'
    decoded.write_text(json.dumps(raw, allow_nan=False))
    readable.write_text(json.dumps(dict(bundleSha256=sha, meshes=preview), allow_nan=False))
    receipt = dict(bundleSha256=sha, decodedSha256=digest(decoded), previewSha256=digest(readable),
                   embeddedMeshes=len(raw), scope='Actual bundle mesh buffers; builtin primitive resources are separate')
    (folder / 'decoded-meshes.receipt.json').write_text(json.dumps(receipt, indent=2))
    print(json.dumps(receipt))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('bundle', type=Path)
    parser.add_argument('--folder', type=Path)
    parser.add_argument('--python-libs', type=Path)
    args = parser.parse_args()
    if args.python_libs:
        sys.path.insert(0, str(args.python_libs))
    decode(args.bundle.resolve(strict=True), args.folder or args.bundle.parent)
