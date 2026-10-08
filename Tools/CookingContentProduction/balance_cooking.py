"""Ingredient-cost balance for current/new dishes; reproducible Rebirth-only XML and review table."""
from pathlib import Path
import json, xml.etree.ElementTree as E
from content_model import ROOT, CAT, CATALOGUE, ANCHORS, recipe, food_stats

recipes={recipe(k)['game_id']:recipe(k) for k in CATALOGUE['recipes']}
cache={}
def nutrients(name,seen=()):
    if name in cache:return cache[name]
    a=ANCHORS.get(name,{})
    fallback=(float(a.get('RebirthNutritionUnits',0)),float(a.get('RebirthInitialVolumeMl',a.get('RebirthFoodWaterMl',0))))
    if name not in recipes or name in seen:return fallback
    r=recipes[name]; totals=[0.,0.]
    for i in r['ingredients']:
        values=nutrients(i['id'],seen+(name,))
        for n in range(2):totals[n]+=values[n]*int(i['count'])/int(r['yield_count'])
    nutrition=min(45,totals[0]*1.18)
    if name.startswith('rebirthCookingFoodP') or name=='foodCornMeal':nutrition=totals[0] # Milling/preserving is not free nutrition.
    cache[name]=(round(nutrition,2),round(totals[1],1))
    return cache[name]

runtime={e.get('item'):e for e in E.parse(ROOT/'Config/_Cooking/runtime.xml').getroot().findall('dish')}
item_properties={e.get('name'):{p.get('name'):p.get('value') for p in e.findall('property')} for e in E.parse(ROOT/'Config/_Cooking/items.xml').getroot().findall('.//item')}
root=E.Element('configs');report=['# Cooking food balance — ingredient-based revision','',
    'Rebirth progression only. Named dishes use 118% of ingredient nutrition per output, capped at 45; processing components conserve nutrition. Skill may retain 90–100% of that value. Experimental meals gain 5–10% on raw ingredients only and yield additional portions to stay at or below 35 per portion. Re-cooking prepared food does not multiply its nutrition.',
    '', '| Item | Previous nutrition | New nutrition | Water (mL) |','|---|---:|---:|---:|']
for key in CATALOGUE['recipes']:
    r=recipe(key);name=r['game_id'];a=ANCHORS.get(name,{})
    n,w=nutrients(name);d=runtime[name]
    retention=1 if d.get('station') in ('cold','WorkbenchMortarPestle001_FR') else .6 if d.get('tool')=='FuriousRamsayBakingPan' else .5 if d.get('tool')=='rebirthCookingFryingPan' else .9
    w=round(w*retention,1)
    is_drink=item_properties.get(name,{}).get('RebirthMetabolismType',a.get('RebirthMetabolismType'))=='Drink' or name.startswith('drink')
    w=min(float(a.get('RebirthContainerCapacityMl',500)) if is_drink else 350,w)
    if is_drink:
        # A newly produced drink fills its authored container. Heat retention applies to food
        # water content, not initial bottle fullness (partial bottles cannot be ingredients).
        w=float(a.get('RebirthContainerCapacityMl',item_properties.get(name,{}).get('RebirthContainerCapacityMl',500)))
    if not is_drink:
        # Cooking water is not all retained in eggs, bread or grilled food.
        authored_water=a.get('RebirthFoodWaterMl',item_properties.get(name,{}).get('RebirthFoodWaterMl',350))
        w=min(w,float(authored_water))
    # Upsert explicitly, after all item/content includes, rather than relying on inherited values.
    properties={'RebirthNutritionUnits':n,'RebirthInitialVolumeMl' if is_drink else 'RebirthFoodWaterMl':w}
    for prop,value in properties.items():
        xpath=f"/items/item[@name='{name}']/property[@name='{prop}']"
        E.SubElement(root,'remove',xpath=xpath)
        E.SubElement(E.SubElement(root,'append',xpath=f"/items/item[@name='{name}']"),'property',name=prop,value=str(value))
    old=a.get('RebirthNutritionUnits',food_stats(key)['nutrition'] if key.startswith(('N','P','L')) else '—')
    report.append(f'| `{name}` | {old} | {n:g} | {w:g} |')
E.indent(root,space='  ')
(ROOT/'Config/_Cooking/balance_items.xml').write_text(E.tostring(root,encoding='unicode')+'\n',encoding='utf-8')
(CAT/'COOKING_BALANCE_20260916.md').write_text('\n'.join(report)+'\n',encoding='utf-8')
print(f'Balanced {len(recipes)} dishes; review table written.')
