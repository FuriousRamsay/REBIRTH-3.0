"""Creative catalogue/backpack and container proxy scrolling via real bridge UI events."""
from test_inventory_scroll_live import *
from profile_inventory_extra import measure
# Allow the UI's path selector without colliding with the historical helper's endpoint argument.
def request(endpoint,method='GET',**args):
 req=urllib.request.Request('http://127.0.0.1:'+str(session['port'])+endpoint+'?'+urllib.parse.urlencode(args),headers={'X-Bridge-Token':session['token']},method=method,data=b'' if method=='POST' else None)
 with urllib.request.urlopen(req,timeout=60) as r:data=json.load(r)
 assert data.get('ok'),data
 return data
close()
# Creative is a key-opened window; enable only if the native command says it was switched off.
r=call('/console','POST',cmd='cm');print(r,flush=True)
if 'off' in json.dumps(r).lower():call('/console','POST',cmd='cm')
call('/key','POST',name='U');time.sleep(1)
assert 'creative' in call('/ui')['openWindows'],call('/ui')
check('creative_backpack','creative',{'id':'characterBackpackScroll'},'XUiC_RebirthCharacterBackpackSlot')
check('creative_catalogue','creative',{'id':'creativeCatalogueTrack'},'XUiC_RebirthCreativeStatsStack')
measure('expanded_creative','creative');close()
call('/setblock','POST',x=14,y=45,z=995,name='cntWoodWritableCrate');time.sleep(1)
call('/activate','POST',x=14,y=45,z=995,block=1);time.sleep(3)
if 'rebirthContextNavigation' not in call('/ui')['openWindows']:call('/activate','POST',x=14,y=45,z=995,block=1);time.sleep(3)
t=call('/ui/tree');(out/'container_initial_ui.json').write_text(json.dumps(t));print(t['openWindows'],flush=True)
tracks=[n['path'] for n in t['nodes'] if n['id']=='contextScrollTrack'];assert tracks,tracks
for track in tracks:
 before=call('/ui/tree')
 for i in range(3):
  request('/ui/scroll','POST',path=track,delta=-1)
  results.append({'label':'container','screenshot':call('/screenshot','POST',name='verified_scroll_container_'+str(i))['path']})
 after=call('/ui/tree');assert before['nodes']!=after['nodes']
(out/'scroll_extra_results.json').write_text(json.dumps(results,indent=2))
measure('expanded_container','rebirthContextNavigation');close()
print('PASS Creative and container scrolling',flush=True)
