"""All breed conversions via real personal crafting input; disposable CodexTest only."""
import sys,time,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'Tools/TraderVoices'))
from test_live import call
root=Path(__file__).resolve().parents[2]
assert call('/ping')['state']=='ingame'
def count(name):
 s=call('/state',sections='inventory');return sum(x['count'] for k in ['backpack','toolbelt'] for x in s[k] if x['name']==name)
call('/surroundings');call('/cleararea','POST',radius=40)
call('/give','POST',item='BlueDog3',count=8)
call('/ui','POST',action='open',window='crafting');time.sleep(.4)
rows=[]
for breed in ['Shepherd','Pitbull','Labrador','Husky','GoldenRetriever','Doberman','Dalmatian','BullTerrier']:
 name='FuriousRamsaySpawnCube'+breed+'Dog001_FR'
 search={'GoldenRetriever':'Golden Retriever','BullTerrier':'Bull Terrier'}.get(breed,breed)
 call('/ui/type','POST',id='rebirthCraftingRecipeSearch',value=search);time.sleep(.4)
 call('/ui/click','POST',recipe=name);time.sleep(.2)
 before=count(name);token=count('BlueDog3')
 call('/ui/click','POST',window='crafting',text='Craft')
 end=time.time()+10
 while time.time()<end and count(name)!=before+1:time.sleep(.25)
 assert count(name)==before+1,(breed,'missing output')
 assert count('BlueDog3')==token-1,(breed,'wrong token cost')
 rows.append({'breed':breed,'output':name,'pass':True})
 (root/'_Documentation/ReleaseFollowup/dog_conversions.json').write_text(json.dumps(rows,indent=2))
 print('PASS',breed,flush=True)
