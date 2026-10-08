"""Real menu randomizer checks. Requires Traders tab and Jen unlocked; Back discards all random choices."""
from test_live import call,root
from pathlib import Path
import json,time,os
labels=['Original','Hostile','Gruff','Warm','Smooth','Practical','Sardonic']
lockfile=Path(os.environ['USERPROFILE'])/'AppData/LocalLow/The Fun Pimps/7 Days To Die/RebirthUtils/Randomizer/rebirth_sandbox_randomizer_locks.txt'
def nodes():return call('/ui/tree',window='rebirthSandboxOptions')['nodes']
def node(suffix):return next(n for n in nodes() if n['path'].endswith(suffix))
def click(suffix):
 call('/ui/click','POST',path=node(suffix)['path']);time.sleep(.25)
def value():return labels.index(node('traderVoiceJen/voiceSelector/currentValue')['text'])
def set_value(v):
 for _ in range(7):
  now=value()
  if now==v:return
  click('traderVoiceJen/voiceSelector/'+('forward' if now<v else 'back'))
 raise AssertionError('selector did not reach value')
def mode():
 return next((s.split('=',1)[1].split('|')[0] for s in lockfile.read_text().splitlines() if s.startswith('TraderVoiceJen=')),'Unlocked')
assert mode()=='Unlocked','Do not overwrite an existing user lock fixture'
original=value();results=[]
try:
 for expected in ('Locked','Minimum','Maximum'):
  set_value(original);click('traderVoiceJen/voiceLock');assert mode()==expected
  values=[]
  for _ in range(5):
   click('/btnRandomize');v=value();values.append(v)
   assert v==original if expected=='Locked' else v>=original if expected=='Minimum' else v<=original
  results.append({'mode':expected,'anchor':original,'values':values,'pass':True})
finally:
 for _ in range(4):
  if mode()=='Unlocked':break
  click('traderVoiceJen/voiceLock')
 assert mode()=='Unlocked'
 click('/btnBack')
call('/ui/click','POST',id='btnRebirthOptions');time.sleep(.3)
call('/ui/click','POST',text='Traders');time.sleep(.3)
assert value()==original
(root/'_Documentation/ReleaseFollowup/voice_locks.json').write_text(json.dumps(results,indent=2))
print('PASS Locked/Minimum/Maximum randomization; unlocked and saved voice restored; pending random settings discarded')
