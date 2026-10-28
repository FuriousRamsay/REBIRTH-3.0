"""Measure baked map coverage within remeshed arm UV charts; modifies no images."""
import argparse
import json
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('folder', type=Path)
args = parser.parse_args()
report = {'scope': 'UV chart pixel diagnostics only; black pixels can also be authored source content.', 'arms': []}
for part in (1, 2):
    mesh = json.loads((args.folder/f'Arm{part}_Baked.json').read_text())
    with Image.open(args.folder/f'Arm{part}_Albedo.png') as image:
        width, height = image.size
    mask = Image.new('L', (width, height))
    draw = ImageDraw.Draw(mask)
    for t in range(0, len(mesh['triangles']), 3):
        polygon = [(mesh['uv'][i]['x']*(width-1), (1-mesh['uv'][i]['y'])*(height-1)) for i in mesh['triangles'][t:t+3]]
        draw.polygon(polygon, fill=255)
    interior = np.asarray(mask.filter(ImageFilter.MinFilter(5))) > 0
    used = int(interior.sum())
    if not used:
        raise RuntimeError('No interior UV pixels')
    maps = {}
    for suffix in ('Albedo', 'roughness', 'metallic', 'normal', 'Coverage'):
        with Image.open(args.folder/f'Arm{part}_{suffix}.png') as image:
            pixels = np.asarray(image.convert('RGBA'))
        if pixels.shape[:2] != (height, width):
            raise RuntimeError('Map dimensions differ')
        values = pixels[interior]
        item = {'transparentFraction': float((values[:, 3] == 0).mean()),
                'blackFraction': float((values[:, :3].max(axis=1) <= 1).mean())}
        if suffix == 'normal':
            lengths = np.linalg.norm(values[:, :3].astype(float)/127.5-1, axis=1)
            item['nonunitFractionAbovePoint1'] = float((abs(lengths-1) > .1).mean())
            item['meanLengthError'] = float(abs(lengths-1).mean())
        maps[suffix] = item
    report['arms'].append({'part': part, 'interiorPixels': used, 'maps': maps})
output = args.folder/'BakeCoverageAudit.json'
output.write_text(json.dumps(report, indent=2))
print(json.dumps(report, indent=2))
