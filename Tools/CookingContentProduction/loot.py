"""Bounded cooking loot pools: catalogue size does not multiply container yield."""
import xml.etree.ElementTree as ET
from content_model import CATALOGUE, ASSETS, recipe

def build():
    root = ET.Element('configs')
    definitions = ET.SubElement(root, 'insertBefore', xpath='/lootcontainers/lootgroup[1]')
    groups = {}
    for category, catalogue_key in [('card','cards'),('book','books'),('magazine','magazines')]:
        records = {r['id']: r for r in CATALOGUE[catalogue_key]}
        for task in ASSETS.values():
            if task['category'] != category:
                continue
            row = records[task['key']]
            rarity = str(row.get('loot','Common')).split(':')[0].lower()
            weight = '0.35' if 'rare' in rarity and 'uncommon' not in rarity else '0.65' if 'uncommon' in rarity else '1'
            name = 'rebirthCooking'+category.title()+'Pool'
            if name not in groups:
                groups[name] = ET.SubElement(definitions, 'lootgroup', name=name, count='1')
            ET.SubElement(groups[name], 'item', name=task['icon'], count='1', prob=weight)
    food_group = ET.SubElement(definitions, 'lootgroup', name='rebirthCookingFoodPool', count='1')
    for key in CATALOGUE['recipes']:
        if key.startswith(('N','L','P')):
            ET.SubElement(food_group, 'item', name=recipe(key)['game_id'], count='1', prob='1')
    attachments = {
        'groupCupboard01':[('rebirthCookingCardPool','0.08')],
        'groupBookcase01':[('rebirthCookingBookPool','0.12'),('rebirthCookingMagazinePool','0.10')],
        'groupBookPile01':[('rebirthCookingBookPool','0.10'),('rebirthCookingMagazinePool','0.10')],
        'groupMailbox01':[('rebirthCookingMagazinePool','0.08'),('rebirthCookingCardPool','0.04')],
        'groupFoodUncommon':[('rebirthCookingFoodPool','0.08')],
    }
    for target, pools in attachments.items():
        append = ET.SubElement(root, 'append', xpath="/lootcontainers/lootgroup[@name='"+target+"']")
        for pool, weight in pools:
            ET.SubElement(append, 'item', group=pool, count='1', prob=weight)
    tools = ET.SubElement(root, 'append', xpath="/lootcontainers/lootgroup[@name='groupCookingTools']")
    for ident in ('rebirthCookingFryingPan','FuriousRamsayBakingPan'):
        ET.SubElement(tools, 'item', name=ident, count='1', prob='0.5', random_durability='false')
    return root
