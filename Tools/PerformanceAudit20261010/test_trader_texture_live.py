"""Current trader runtime-texture regression; prepared disposable CodexTest beside a trader."""
import sys,json,time,urllib.request,urllib.parse,re
from pathlib import Path
sys.path.insert(0,'Tools/StorageGear')
from test_capacity_live import root,session
out=root/'_Documentation/PerformanceAudit_20261010/LIVE_TRADER_TEXTURE_RETEST.json'
result={}
def call(route,method='GET',**args):
 req=urllib.request.Request('http://127.0.0.1:'+str(session['port'])+route+'?'+urllib.parse.urlencode(args),headers={'X-Bridge-Token':session['token']},method=method,data=b'' if method=='POST' else None)
 with urllib.request.urlopen(req,timeout=60) as response:data=json.load(response)
 assert data.get('ok'),data
 return data
def tree():return call('/ui/tree')
def accepted(t):
 n=next(n for n in t['nodes'] if n['path'].endswith('/jobAcceptanceStatusText'))
 return int(re.search(r'Accepted Jobs: (\d+)',n['text'])[1])
def offers(t):return [n for n in t['nodes'] if '/items/' in n['path'] and n['path'].endswith('/jobCard/prefabName')]
def check(value,name):
 result.setdefault('checks',[]).append({'check':name,'pass':bool(value)})
 assert value,name
 print('PASS '+name,flush=True)
try:
 result['ping']=call('/ping');check(result['ping']['state']=='ingame','Disposable game loaded')
 result['world']=call('/state',sections='world');check(result['world']['world']['gameName']=='CodexTest','Disposable save identity')
 result['surroundings']=call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
 entities=call('/entities',radius=8,contains='trader')['entities'];check(len(entities)>0,'Real nearby trader exists')
 trader=entities[0]['id'];t=tree()
 if 'ingameMenu' in t['openWindows']:call('/ui/click','POST',text='Resume')
 elif 'crafting' in t['openWindows']:call('/key','POST',name='Tab')
 call('/activate','POST',entity=trader);time.sleep(.4)
 check('dialog' in tree()['openWindows'],'Real activation opened dialog')
 call('/ui/click','POST',text='Do you have any jobs?');time.sleep(.4)
 before=tree();result['before']=before;n=accepted(before);o=offers(before);check(len(o)>1,'Multiple real offers available')
 # Choose a unique, visible authored offer rather than buried supplies duplicates.
 chosen=next(x for x in o if 'Buried' not in x['text']);path=chosen['path'].rsplit('/',1)[0]
 call('/ui/hover','POST',path=path);time.sleep(.4);shown=tree();result['hover']=shown
 popup=[x.get('text') for x in shown['nodes'] if '/rebirthTraderJobPopup/jobDetailCard/prefabName' in x['path']]
 check(chosen['text'] in popup,'Preview matches chosen offer')
 check(accepted(shown)==n,'Hover did not accept a job')
 result['beforeShot']=call('/screenshot','POST',name='trader_texture_before_accept')
 call('/ui/click','POST',id='jobDetailAccept');time.sleep(.6)
 after=tree();result['after']=after
 check(accepted(after)==n+1,'Explicit Accept adds exactly one job to accepted count')
 check(chosen['text'] not in [x['text'] for x in offers(after)],'Accepted unique offer removed from refreshed list')
 result['afterShot']=call('/screenshot','POST',name='trader_texture_after_accept')
 call('/ui/click','POST',text='Nevermind.');time.sleep(.3)
 t=tree()
 if 'dialog' in t['openWindows']:call('/key','POST',name='Escape');time.sleep(.3)
 t=tree()
 if 'ingameMenu' in t['openWindows']:call('/ui/click','POST',text='Resume')
 call('/activate','POST',entity=trader);time.sleep(.3);call('/ui/click','POST',text='Do you have any jobs?');time.sleep(.4)
 reopened=tree();result['reopened']=reopened;check(accepted(reopened)==n+1,'Accepted count survives dialogue reopen')
 result['reopenShot']=call('/screenshot','POST',name='trader_texture_reopened')
 result['errors']=call('/log',level='error',since=result['ping']['lastLogSeq'],limit=100)
 check(result['errors']['count']==0,'No error or texture-unload logs during refresh and reopen')
 result['log']=call('/log',since=result['ping']['lastLogSeq'],limit=100)
finally:
 out.write_text(json.dumps(result,indent=2))
