import sys,json,hashlib
from pathlib import Path
sys.path.insert(0,str(Path('Tools/AssetBundle/.python').resolve()));import UnityPy
p=Path('../../Data/Addressables/Standalone/player_assets_entities/player/common.bundle')
env=UnityPy.load(str(p));controllers=[];clips=[]
for obj in env.objects:
 if obj.type.name in ('AnimatorController','AnimatorOverrideController'):
  controllers.append({'id':obj.path_id,'name':obj.read().m_Name,'tree':obj.read_typetree()})
 if obj.type.name=='AnimationClip':
  d=obj.read()
  if 'pipe' in d.m_Name.lower():
   m=getattr(d,'m_MuscleClip',None)
   clips.append({'id':obj.path_id,'name':d.m_Name,'start':getattr(m,'m_StartTime',None),'stop':getattr(m,'m_StopTime',None)})
containers=[{'name':n,'id':v.path_id} for n,v in env.container.items()]
out={'bundle':str(p.resolve()),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'controllers':controllers,'pipeClips':clips,'containers':containers}
Path('_Documentation/PlayerReports_20261102/PipeMachineGunDps/native-player-controller-index.json').write_text(json.dumps(out,default=str,indent=2))
print(json.dumps({'controllers':[{'id':x['id'],'name':x['name']} for x in controllers],'pipeClips':clips,'containers':containers[:12]},indent=2))
