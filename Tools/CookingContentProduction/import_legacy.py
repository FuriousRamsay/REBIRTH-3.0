"""Preview or import the original 2.6 food artwork. Run --apply between generation batches."""
import json, shutil, sys
from pathlib import Path
from PIL import Image, ImageDraw
import assets

SOURCE = Path('C:/Program Files (x86)/Steam/steamapps/common/7 Days To Die 2.6/Mods/zzz_REBIRTH__Core/UIAtlases/ItemIconAtlas')
catalogue = json.loads((assets.CAT / 'COOKING_LITERATURE_CATALOGUE.json').read_text())
manifest = assets.init()
legacy = [t for t in manifest['tasks'] if t['category'] == 'food' and t['key'].startswith('L')]
sheet = Image.new('RGB', (1000, 630), '#25282c')
draw = ImageDraw.Draw(sheet)
for i, t in enumerate(legacy):
    source = SOURCE / (catalogue['recipes'][t['key']]['game_id'] + '.png')
    im = Image.open(source).convert('RGBA')
    im.thumbnail((160,160), Image.Resampling.LANCZOS)
    x,y = (i % 5) * 200, (i // 5) * 210
    sheet.paste(im, (x + (200-im.width)//2, y + 5), im)
    draw.text((x+8,y+170), t['key'], fill='white')
    draw.text((x+8,y+185), t['title'][:29], fill='white')
    if '--apply' in sys.argv and t['key'] != 'L02' and t['status'] == 'pending':
        shutil.copy2(source, t['master'])
        assets.export(source, t['path'])
        t.update(status='reused_legacy',source=str(source),alpha_extrema=im.getchannel('A').getextrema())
if '--apply' in sys.argv:
    assets.write(manifest)
path = assets.OUT / 'review-original-legacy-foods.png'
sheet.save(path)
print(path)
