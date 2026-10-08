"""Record the additional cookware icon and the resolved legacy recipe image."""
import json
from pathlib import Path
import sys
import assets

m = assets.init()
root = Path(__file__).resolve().parents[2]
production = root / '_Documentation/FeatureDesign/SurvivorSkillSystem/CookingCatalogue_20260915/Production'
for t in m['tasks']:
    if t['key'] == 'C-L02':
        t['prompt'] = t['prompt'].split('Recipe ingredients:')[0] + 'Recipe ingredients: cooked meat, carrot, onion, goldenrod and water. The lower band should depict carrot and onion. No rotten flesh, brains, medicine or vitamin imagery. Preserve the referenced food and its angle. True transparent exterior.'
    if t['key'] == 'L02':
        t['prompt'] = 'Rad-atouille: cooked meat, carrot, onion and goldenrod stew in the approved worn pot; transparent background. See integration_decisions.json for the resolved formula.'
if not any(t['key'] == 'TOOL-PAN' for t in m['tasks']):
    m['tasks'].append(dict(key='TOOL-PAN', category='tool', title='Frying Pan', icon='rebirthCookingFryingPan',
        path=str(root / 'UIAtlases/ItemIconAtlas/rebirthCookingFryingPan.png'),
        master=str(production / 'Masters/rebirthCookingFryingPan.png'),
        prompt='Empty seasoned cast-iron frying pan, long handle toward upper right, realistic worn game inventory style, transparent background.',
        refs=[str(production / 'Masters/rebirthCookingFoodN39.png')], status='pending'))
assets.write(m)
index = next(i for i,t in enumerate(m['tasks']) if t['key'] == 'TOOL-PAN')
assets.accept(m, index, Path(sys.argv[1]))
