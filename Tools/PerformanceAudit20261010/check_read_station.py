import sys,json,time
sys.path.insert(0,'Tools/StorageGear')
from test_capacity_live import call,root
out=root/'_Documentation/PerformanceAudit_20261010'
r={}
r['click']=call('/ui/click','POST',text='Your backpack: For Sale');time.sleep(.5)
r['selected']=call('/ui/tree');r['shot']=call('/screenshot','POST',name='performance_journal_read_current')
call('/key','POST',name='Escape');time.sleep(.3)
call('/walkto','POST',x=14,z=996)
call('/activate','POST',x=14,y=45,z=998,block=1);time.sleep(.8)
r['station']=call('/ui/tree');r['stationShot']=call('/screenshot','POST',name='performance_station_current')
r['errors']=call('/log',since=477,level='error',limit=100)
(out/'LIVE_CURRENT_READ_STATION.json').write_text(json.dumps(r,indent=2))
print(json.dumps({'selectedShot':r['shot'],'stationShot':r['stationShot'],'windows':r['station']['openWindows'],'errors':r['errors']}))
