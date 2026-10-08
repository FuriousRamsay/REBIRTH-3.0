"""Compare native icon/card pairs at inventory size; write the coverage audit."""
import json
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw
import assets

manifest=assets.init()
catalogue=json.loads((assets.CAT/'COOKING_LITERATURE_CATALOGUE.json').read_text())
native=assets.ROOT.parent.parent/'Data/Config'
native_ids={n.get('name') for n in ET.parse(native/'recipes.xml').iter('recipe')}
rows=[]
for card in catalogue['cards']:
    recipe=catalogue['recipes'][card['recipe']]
    if recipe['game_id'] not in native_ids:
        continue
    task=next(t for t in manifest['tasks'] if t['key']==card['id'])
    source=manifest['original_food_references'][card['recipe']]
    rows.append((recipe['title'],source,task))
pages=[]
for start in range(0,len(rows),24):
    selected=rows[start:start+24]
    sheet=Image.new('RGB',(1020,((len(selected)+2)//3)*192),(45,45,45))
    draw=ImageDraw.Draw(sheet)
    for i,(title,source,task) in enumerate(selected):
        x=i%3*340;y=i//3*192
        draw.text((x+5,y+3),title,fill='white')
        for col,path in enumerate((source,task['path'])):
            im=Image.open(path).convert('RGBA')
            im.thumbnail((160,160))
            sheet.paste(im,(x+col*165+5,y+24),im)
    name=f'native-card-comparison-{start//24+1}.png'
    sheet.save(assets.OUT/name)
    pages.append(name)
lines=['# Native food and drink recipe-card audit', '',
       '2026-09-16. Native source icons are on the left; installed recipe cards are on the right. Both are displayed at 160 pixels.', '',
       f'Coverage: {len(rows)} native consumable recipes have cards. Retained 13 added cards (12 drinks and Water); seven basic-food cards removed at user request. Empty containers, raw-meat salvage and meat bundles are excluded.', '',
       'Corrected 16 existing cards: Tuna Fish Gravy Toast, Frostbite Smoothie, Hobo Stew, Chicken Wings, Chicken Nuggets, Ash Chicken Stew, Corn Bread, Fish Tacos, Spaghetti, Shepherds Pie, Sham Chowder, Vegetable Stew, Pumpkin Cheesecake, Meat Stew, Gumbo Stew and Steak and Potato Meal.', '',
       'Artwork was edited/generated with the built-in image-generation tool using extracted native icons as the visual source. The cards are reference-based illustrations, not pixel-identical composites. Preserve native silhouette, angle, food color, containers, packaging and recognizable presentation. Prompts and source paths are recorded in asset_manifest.json.', '',
       'New cards have localized names, ingredient descriptions, reusable study registrations and entries in the existing card loot pool. Food recipes, stats, and overall pool attachment probabilities were not changed. In-game appearance still requires a fresh launch.', '']
for page in pages:
    lines += [f'![Native icon and recipe card]({page})','']
lines += ['| Native item | Card item |','|---|---|']
for title,source,task in rows:
    lines.append(f"| {title} | `{task['icon']}` |")
(assets.OUT/'NATIVE_CARD_AUDIT.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(json.dumps({'native_card_count':len(rows),'pages':pages}))
