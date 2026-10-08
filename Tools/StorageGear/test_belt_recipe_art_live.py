"""Inspect revised belt recipes through real crafting UI; CodexTest only."""
from test_capacity_live import *
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
start=call('/ping')['lastLogSeq']
call('/ui','POST',action='open',window='crafting')
results=[]
for name in ['Tactical','Artisan','Industrial','Expedition']:
 call('/ui/type','POST',id='rebirthCraftingRecipeSearch',value=name.lower());time.sleep(.5)
 call('/ui/click','POST',recipe='rebirthGear'+name+'Belt');time.sleep(.5)
 tree=call('/ui/tree')
 (out/('recipe_v3_'+name+'.json')).write_text(json.dumps(tree,indent=2))
 shot=call('/screenshot','POST',name='belt_v3_'+name)['path']
 results.append({'name':name,'screenshot':shot})
 print(name,shot,flush=True)
call('/ui/drag','POST',**{'from.x':913,'from.y':315,'to.x':913,'to.y':357});time.sleep(.5)
tree=call('/ui/tree');assert any(n.get('text')=='Armor Parts' for n in tree['nodes'])
call('/screenshot','POST',name='belt_v3_last_material')
(out/'recipe_art_v3_live.json').write_text(json.dumps({'recipes':results,'errors':call('/log',since=start,level='error')},indent=2))
