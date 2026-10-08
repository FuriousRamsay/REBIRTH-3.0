"""Fill native consumable card coverage and record an artwork correction queue."""
import csv
import json
import re
import zipfile
import assets
from card_policy import NO_CARD_ITEMS
import xml.etree.ElementTree as ET


def prepare():
    catalogue_path = assets.CAT/'COOKING_LITERATURE_CATALOGUE.json'
    catalogue = json.loads(catalogue_path.read_text())
    manifest = assets.init()
    native = assets.ROOT.parent.parent/'Data/Config'
    labels = {r['Key']: r['english'] for r in csv.DictReader((native/'Localization.csv').open(encoding='utf-8-sig'))}
    archive = zipfile.ZipFile(r'C:/Users/Etienne/Desktop/__0/_0/__Rebirth/7dtd 3.2 ItemIcons.zip')
    known = {r['game_id']: k for k,r in catalogue['recipes'].items()}
    cards = {r['recipe'] for r in catalogue['cards']}
    queue = []
    # Cover consumable recipes, excluding containers and raw-meat salvage/bundling.
    for recipe in ET.parse(native/'recipes.xml').iter('recipe'):
        ident = recipe.get('name', '')
        if not ident.startswith(('food','drink')) or ident in NO_CARD_ITEMS | {'drinkJarEmpty','foodRawMeat','foodRawMeatBundle'}:
            continue
        key = known.get(ident, 'native-'+ident)
        if key not in catalogue['recipes']:
            catalogue['recipes'][key] = dict(id=key, game_id=ident, title=labels[ident],
                source='Native 3.2 recipes.xml and Localization.csv',
                ingredients=[dict(id=i.get('name'),count=i.get('count')) for i in recipe.findall('ingredient')],
                station=recipe.get('craft_area','cold'), tool=recipe.get('craft_tool'),
                yield_count=recipe.get('count','1'), substitutions='No additional substitutions defined.',
                herbs='', status='Existing native recipe', note='Card documents the existing recipe; no recipe or balance changes.',
                books=[], magazines=[], card='C-'+key)
        if key in cards:
            continue
        row = catalogue['recipes'][key]
        title = labels.get(ident, row['title'])+' — Recipe Card'
        card = dict(id='C-'+key,title=title,recipe=key,status='Existing native recipe',
                    ingredients=row['ingredients'],yield_count=row['yield_count'],station=row['station'],
                    tool=row.get('tool'),substitutions=row['substitutions'],
                    herb_guidance='Separate studied herb reference required for suggestions.',
                    loot='Domestic kitchens and relevant food venues')
        catalogue['cards'].append(card)
        row['card']=card['id']
        icon='rebirthCookingCard'+re.sub('[^A-Za-z0-9]','',card['id'])
        ref=assets.OUT/'OriginalFoodReferences'/(ident+'.png')
        ref.write_bytes(archive.read('ItemIcons/'+ident+'.png'))
        manifest['original_food_references'][key]=str(ref)
        task=dict(key=card['id'],category='card',title=title,icon=icon,
                  path=str(assets.ICONS/(icon+'.png')),master=str(assets.MASTERS/(icon+'.png')),
                  refs=[str(assets.REF/'carrot-ginger-recipe-card-approved-style.png'),str(ref)],status='pending')
        task['prompt']='Create one square recipe card matching reference 1 STYLE ONLY: worn sage green card, rounded cream double border, burnt-orange ingredient footer, transparent exterior, no text. Replace the soup with a faithful enlarged copy of reference 2, the ACTUAL native game '+labels[ident]+' inventory icon. Preserve its exact silhouette, angle, container, color, contents, labels and lid arrangement. Do not redesign or beautify the food/drink, do not add ingredients or garnish to it. Fill the upper card with that native item. The footer shows only two simple ingredient illustrations from: '+', '.join(labels.get(i['id'],i['id']) for i in row['ingredients'])+'. No carrot or soup unless actually present. One icon only, no comparison.'
        manifest['tasks'].append(task)
        queue.append(task['key'])
    corrections=['frost','hobo','wings','nuggets','cornbread','tacos','pasta','shepherd','chowder','vegstew','ashstew']
    for key in corrections:
        task=next(t for t in manifest['tasks'] if t['key']=='C-'+key)
        task['refs']=[task['master'],manifest['original_food_references'][key]]
        task['prompt']='Edit reference 1 recipe card: replace ONLY its large main food with a faithful enlarged copy of reference 2, the actual native '+catalogue['recipes'][key]['title']+' icon. Preserve the exact food appearance, silhouette, angle, color, serving container and packaging INCLUDING labels or box lids visible in reference 2. Do NOT invent garnish, change sauce color, food texture, portions, or containers. Reference 1 main food is WRONG and must not be imitated. Keep its weathered sage square card, cream border, orange ingredient footer, rounded corners and transparent exterior. No added text. One card icon. Fidelity to the native item is the priority.'
        task['status']='pending'
        queue.append(task['key'])
    catalogue_path.write_text(json.dumps(catalogue,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
    assets.write(manifest)
    (assets.OUT/'native_card_queue.json').write_text(json.dumps(queue,indent=2)+'\n')
    print(json.dumps({'queued':len(queue),'keys':queue}))


if __name__=='__main__':
    prepare()
