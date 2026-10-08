"""Write a portable review index for the exported cooking artwork."""
from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / '_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915/Production'
tasks = json.loads((OUT / 'asset_manifest.json').read_text())['tasks']
lines = ['# Cooking icon review', '',
         '[Native food and drink card comparison](NATIVE_CARD_AUDIT.md)', '',
         '[Ingredient and artwork consistency audit](ART_CONSISTENCY_AUDIT.md) · [Open jars and matching cards](JarRevision/README.md)', '',
         'Contact sheets show the exported icons at their actual 160-pixel size. Each label uses the catalogue title.', '']
for category in ('food', 'card', 'magazine', 'book', 'tool'):
    selected = [t for t in tasks if t['category'] == category]
    completed = [t for t in selected if t['status'] != 'pending']
    lines += ['## ' + category.title(), '', f'{len(completed)} of {len(selected)} assets exported.', '']
    for sheet in sorted(OUT.glob('review-' + category + '-*.png')):
        lines += [f'![{category.title()} contact sheet]({sheet.name})', '']
    lines += ['| Catalogue ID | Title | Full-size artwork |', '|---|---|---|']
    for task in completed:
        master = Path(task['master']).relative_to(OUT).as_posix()
        title = task['title'].replace('|', '\\|')
        lines.append(f"| {task['key']} | {title} | [View]({master}) |")
    lines.append('')
(OUT / 'ICON_REVIEW.md').write_text('\n'.join(lines) + '\n', encoding='utf-8')
print(OUT / 'ICON_REVIEW.md')
