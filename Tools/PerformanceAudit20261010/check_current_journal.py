import sys,json,time
from pathlib import Path
sys.path.insert(0,'Tools/StorageGear')
from test_capacity_live import call,root
out=root/'_Documentation/PerformanceAudit_20261010'
r={'ping':call('/ping'),'surroundings':call('/surroundings')}
call('/cleararea','POST',radius=40);call('/restore','POST')
r['start']=call('/state',sections='player,performance,world')
call('/key','POST',name='Tab');time.sleep(1)
r['crafting']=call('/ui/tree')
call('/ui/click','POST',text='Journal');time.sleep(1)
r['journal']=call('/ui/tree');assert 'rebirthJournal' in r['journal']['openWindows']
r['screenshot']=call('/screenshot','POST',name='performance_journal_current')
r['errors']=call('/log',since=r['ping']['lastLogSeq'],level='error',limit=100)
(out/'LIVE_CURRENT_JOURNAL.json').write_text(json.dumps(r,indent=2))
print(json.dumps({'windows':r['journal']['openWindows'],'screenshot':r['screenshot'],'errors':r['errors'],'start':r['start']},indent=2))
