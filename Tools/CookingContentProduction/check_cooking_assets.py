"""Check burnt output coverage, transparency and the generated balance bounds."""
import json, csv
import xml.etree.ElementTree as E
from PIL import Image
from content_model import ROOT, CAT

manifest = json.loads((CAT/'Production/Burnt/manifest.json').read_text(encoding='utf-8'))
items = {e.get('name'): e for e in E.parse(ROOT/'Config/_Cooking/burnt_items.xml').findall('.//item')}
assert len(items) == len(manifest), 'Burnt item/asset count differs'
with (ROOT/'Config/Localization.csv').open(encoding='utf-8-sig',newline='') as f:
    names={r['Key']:r['english'] for r in csv.DictReader(f)}
for row in manifest:
    name = 'rebirthBurnt_' + row['item']
    assert name in items, name
    assert names[name].endswith(' (Burnt)'), (name,'burnt suffix missing')
    assert float(items[name].find("property[@name='RebirthHydrationCostMl']").get('value')) == 100
    assert float(items[name].find("property[@name='RebirthMoodInfluence']").get('value')) < 0
    assert float(items[name].find("property[@name='RebirthNutritionUnits']").get('value')) <= 2
    assert items[name].find("property[@name='CustomIcon']").get('value') == name
    im = Image.open(ROOT/'UIAtlases/ItemIconAtlas'/(name+'.png'))
    assert im.size == (160,160), (name,im.size)
    assert im.mode == 'RGBA' and im.getchannel('A').getextrema()[0] == 0, (name,'missing alpha')

runtime = E.parse(ROOT/'Config/_Cooking/runtime.xml').getroot()
missing = [d.get('item') for d in runtime.findall('dish')
           if d.get('station') not in ('cold','WorkbenchMortarPestle001_FR','chemistryStation')
           and 'rebirthBurnt_'+d.get('item') not in items]
assert not missing, ('Hot dishes lack burnt icons', missing)
for method in ('Soup','Pan','Baked'):
    for i in range(1,5):
        assert f'rebirthBurnt_rebirthImprovised{method}{i:02}' in items

values = E.parse(ROOT/'Config/_Cooking/balance_items.xml').findall('.//property')
for p in values:
    value = float(p.get('value'))
    assert value >= 0, E.tostring(p)
    limit = 45 if p.get('name') == 'RebirthNutritionUnits' else 500 if p.get('name') == 'RebirthInitialVolumeMl' else 350
    assert value <= limit, E.tostring(p)
print(f'Cooking assets checked: {len(items)} distinct burnt items/icons, 12 improvised variants, {len(values)} bounded stat values.')
