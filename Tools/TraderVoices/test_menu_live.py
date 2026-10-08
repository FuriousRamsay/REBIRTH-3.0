"""Run with Rebirth Options > Traders open. Real menu input, restore original voice."""
from test_live import call, root
import json,time

def nodes(): return call('/ui/tree',window='rebirthSandboxOptions')['nodes']
def node(suffix): return next(n for n in nodes() if n['path'].endswith(suffix))
def click(suffix):
    result=call('/ui/click','POST',path=node(suffix)['path']);time.sleep(.3);return result
def value(): return node('traderVoiceJen/voiceSelector/currentValue')['text']
def reopen():
    click('/btnBack')
    call('/ui/click','POST',id='btnRebirthOptions');time.sleep(.3)
    call('/ui/click','POST',text='Traders');time.sleep(.3)

original=value(); results=[]
direction='back' if original=='Sardonic' else 'forward'
reverse='forward' if direction=='back' else 'back'
click('traderVoiceJen/voiceSelector/'+direction)
changed=value();assert changed!=original
assert node('/btnSave').get('enabled',True)
click('/btnSave');reopen()
assert value()==changed
assert not node('/btnSave').get('enabled',True)
results.append({'check':'save and reopen','pass':True})
click('traderVoiceJen/voiceSelector/'+reverse)
assert value()==original
reopen()
assert value()==changed,'Back unexpectedly saved pending voice'
results.append({'check':'discard on Back','pass':True})
click('traderVoiceJen/voiceSelector/'+reverse)
click('/btnSave')
assert value()==original
results.append({'check':'original restored','pass':True})
for trader in ['Rekt','Hugh','Jen','Joel','Bob']:
    assert node('traderVoice'+trader+'/voiceLock').get('clickable')
results.append({'check':'five visible lock controls','pass':True})
(root/'_Documentation/ReleaseFollowup/voice_menu.json').write_text(json.dumps(results,indent=2))
print('PASS voice Save, reopen, discard, restored original; five lock controls present')
