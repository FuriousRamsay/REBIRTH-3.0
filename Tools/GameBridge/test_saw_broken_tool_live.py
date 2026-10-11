"""Qualify durability fixture and real broken-tool admission in CodexTest."""
import json,sys,urllib.error
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'TraderVoices'))
from test_live import call,root
p=dict(x=22,y=44,z=997); blade='FuriousRamsayCircularSawBlade'
window='workstation_WorkbenchCircularMachine001_FR'
def setup(**override):
    args=dict(**p,slot=0,item=blade,durability=0,confirm='CodexTest');args.update(override)
    return call('/stationtoolsetup','POST',**args)
def reject(status,**args):
    try:setup(**args)
    except urllib.error.HTTPError as e:
        assert e.code==status,(e.code,e.read());return
    raise AssertionError('Invalid fixture request accepted')
def tree():return call('/ui/tree',window=window)
assert call('/state',sections='world')['world']['gameName']=='CodexTest'
call('/surroundings');call('/cleararea','POST',radius=40);call('/restore','POST')
initial=call('/station',**p);assert initial['queueCount']==0 and not initial['accessed'],initial
reject(403,confirm='wrong');reject(400,durability=2);reject(409,item='wrong')
broken=setup();assert broken['afterUseTimes']==broken['maxUseTimes'],broken
try:
    assert window in call('/activate','POST',block=1,**p)['openWindows']
    reject(409,durability=1)
    call('/ui/click','POST',recipe='FuriousRamsayWoodPlank',exact=1)
    before=tree()
    call('/ui/click','POST',text='Craft')
    after=tree()
    call('/key','POST',name='Escape')
    blocked=call('/station',**p)
    assert blocked['queueCount']==0,blocked
    assert blocked['output']==initial['output'],(blocked,initial)
finally:
    # Restore the fixture only once the window is closed, preserving the queued-tool lock.
    restored=setup(durability=1)
assert restored['afterUseTimes']==0,restored
result=dict(passed=True,initial=initial,broken=broken,before=before,after=after,blocked=blocked,restored=restored)
(root/'_Documentation/CraftingAudit_20261010/LIVE_SAW_BROKEN_TOOL_TEST.json').write_text(json.dumps(result,indent=2))
print('PASS fixture refusals and broken-tool craft admission; restored blade')
