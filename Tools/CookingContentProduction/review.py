"""Produce labelled, actual-game-size contact sheets and validate icon exports."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import json, sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915/Production'
data = json.loads((OUT / 'asset_manifest.json').read_text())
category = sys.argv[1] if len(sys.argv) > 1 else 'food'
tasks = [t for t in data['tasks'] if t['category'] == category and t['status'] != 'pending']
issues = []
font = ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf', 11)
for t in tasks:
    im = Image.open(t['path'])
    if im.size != (160, 160) or im.mode != 'RGBA':
        issues.append(f"{t['key']}: invalid export size or mode")
    elif im.getchannel('A').getextrema() != (0, 255):
        issues.append(f"{t['key']}: unexpected alpha range")
for start in range(0, len(tasks), 20):
    page = tasks[start:start + 20]
    sheet = Image.new('RGB', (1000, ((len(page) + 4) // 5) * 210), '#25282c')
    draw = ImageDraw.Draw(sheet)
    for i, t in enumerate(page):
        x, y = (i % 5) * 200, (i // 5) * 210
        icon = Image.open(t['path']).convert('RGBA')
        sheet.paste(icon, (x + 20, y + 5), icon)
        draw.text((x + 8, y + 170), t['key'], fill='white', font=font)
        title = t['title']
        lines = [title[j:j + 29] for j in range(0, min(len(title), 58), 29)]
        for n, line in enumerate(lines):
            draw.text((x + 8, y + 183 + n * 12), line, fill='#dddddd', font=font)
    path = OUT / f'review-{category}-{start // 20 + 1:02}.png'
    sheet.save(path)
    print(path)
print(json.dumps({'category': category, 'reviewed_exports': len(tasks), 'format_issues': issues}))
