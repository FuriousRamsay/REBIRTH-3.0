"""Open real station blocks and capture their layout and steady-state profile. CodexTest only."""
from test_inventory_scroll_live import close
from profile_inventory_extra import measure
from test_capacity_live import call, root
import json,time

out=root/'_Documentation/ReleaseFollowup'
results=[]
assert call('/ping')['state']=='ingame'
call('/surroundings');call('/cleararea','POST',radius=40)
for name in ('forge','cementMixer','workbench','chemistryStation','cntWoodBurningStove','WorkbenchGasStove001_FR','WorkbenchMortarPestle001_FR','WorkbenchIronOven001_FR'):
    close()
    call('/setblock','POST',x=12,y=45,z=995,name=name)
    time.sleep(.6)
    call('/walkto','POST',x=13.5,z=993.8)
    call('/activate','POST',x=12,y=45,z=995,block=1)
    time.sleep(1)
    tree=call('/ui/tree')
    windows=[w for w in tree['openWindows'] if w.startswith('workstation_')]
    if not windows:
        call('/activate','POST',x=12,y=45,z=995,block=1);time.sleep(1)
        tree=call('/ui/tree');windows=[w for w in tree['openWindows'] if w.startswith('workstation_')]
    result={'block':name,'windows':windows}
    results.append(result)
    (out/'station_windows.json').write_text(json.dumps(results,indent=2))
    assert windows,(name,tree['openWindows'])
    result['screenshot']=call('/screenshot','POST',name='station_'+name)['path']
    (out/('station_'+name+'_ui.json')).write_text(json.dumps(tree))
    measure('station_'+name,windows[0])
    (out/'station_windows.json').write_text(json.dumps(results,indent=2))
    print('PASS station opens',name,flush=True)
close()
