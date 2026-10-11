import sys,json,time
sys.path.insert(0,'Tools/StorageGear');from test_capacity_live import root,session
import urllib.request,urllib.parse
def call(route,method='GET',**args):
 req=urllib.request.Request('http://127.0.0.1:'+str(session['port'])+route+'?'+urllib.parse.urlencode(args),headers={'X-Bridge-Token':session['token']},method=method,data=b'' if method=='POST' else None)
 with urllib.request.urlopen(req,timeout=60) as response:data=json.load(response)
 assert data.get('ok'),data
 return data
out=root/'_Documentation/PerformanceAudit_20261010';r={'before':call('/state',sections='quests'),'checks':[]}
for i in (2,3,2):
 path=f'dialog/windowResponses/items/rebirth_conversation_entry#{i}/jobCard'
 r['checks'].append({'hover':call('/ui/hover','POST',path=path)});time.sleep(.4)
 t=call('/ui/tree');r['checks'][-1]['tree']=t
 r['checks'][-1]['shot']=call('/screenshot','POST',name=f'performance_trader_popup_{i}')
 print(json.dumps({'index':i,'popup':[n.get('text') for n in t['nodes'] if 'rebirthTraderJobPopup' in n['path'] and n.get('text')],'shot':r['checks'][-1]['shot']}))
r['after']=call('/state',sections='quests');r['errors']=call('/log',level='error',since=469,limit=100)
(out/'LIVE_POPUP_SWITCH.json').write_text(json.dumps(r,indent=2));print(json.dumps({'errors':r['errors']}))
