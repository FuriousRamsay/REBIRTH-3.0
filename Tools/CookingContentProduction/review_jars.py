"""Contact sheet comparing revised jars with their native game reference."""
import json
import textwrap
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915/Production'
tasks = {t['key']: t for t in json.loads((OUT/'asset_manifest.json').read_text())['tasks']}
keys = ['N37', 'N38', 'P03', 'P04', 'L11', 'existing-pickles']
cards = ['C-N37', 'C-N38', 'C-P03', 'C-P04', 'C-L11', 'C-pickles']
sheet = Image.new('RGB', (1330, 470), '#25282c')
draw = ImageDraw.Draw(sheet)
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 12)
def place(path, col, row, title):
    icon = Image.open(path).convert('RGBA')
    x, y = col * 190 + 15, row * 230 + 8
    sheet.paste(icon, (x, y), icon)
    draw.multiline_text((x, y+166), '\n'.join(textwrap.wrap(title, 24)), font=font, fill='white', spacing=2)
place(OUT/'JarRevision/drinkJarBoiledWater.png', 0, 0, 'Base game: Boiled Water (open jar)')
draw.multiline_text((15, 245), 'Matching recipe cards\nuse the revised jars.', font=font, fill='white', spacing=4)
for i, (key, card) in enumerate(zip(keys, cards), 1):
    place(tasks[key]['path'], i, 0, tasks[key]['title'])
    place(tasks[card]['path'], i, 1, tasks[key]['title']+' card')
path = OUT/'JarRevision/jars-and-cards-review.png'
sheet.save(path)
print(path)
