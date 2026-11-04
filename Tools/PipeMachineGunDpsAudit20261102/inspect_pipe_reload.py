import sys,json
from pathlib import Path
sys.path.insert(0,str(Path('Tools/AssetBundle/.python').resolve()))
import UnityPy
p=Path('../../Data/Addressables/Standalone/automatic_assets_other/items.bundle')
env=UnityPy.load(str(p));clips=[];controllers=[];prefabs=[]
for obj in env.objects:
 if obj.type.name=='AnimationClip':
  data=obj.read(); name=data.m_Name
  if 'reload' in name.lower() or 'pipe' in name.lower():clips.append({'id':obj.path_id,'name':name,'start':getattr(data,'m_MuscleClip',None) and getattr(data.m_MuscleClip,'m_StartTime',None),'stop':getattr(data,'m_MuscleClip',None) and getattr(data.m_MuscleClip,'m_StopTime',None),'sampleRate':getattr(data,'m_SampleRate',None)})
 if obj.type.name in ['AnimatorController','AnimatorOverrideController']:
  data=obj.read()
  if 'pipe' in data.m_Name.lower():controllers.append({'id':obj.path_id,'type':obj.type.name,'name':data.m_Name,'tree':obj.read_typetree()})
for name,pointer in env.container.items():
 if 'pipemachine' in name.lower():prefabs.append({'name':name,'id':pointer.path_id,'file_id':pointer.file_id})
out=Path('_Documentation/PlayerReports_20261102/PipeMachineGunDps/native-animation-index.json');out.write_text(json.dumps({'bundle':str(p.resolve()),'prefabs':prefabs,'clips':clips,'controllers':controllers},default=str,indent=2))
print(json.dumps({'prefabs':prefabs,'pipeClips':[x for x in clips if 'pipe' in x['name'].lower()],'pipeControllers':[{'id':x['id'],'name':x['name']} for x in controllers],'reloadClips':len(clips)},indent=2))
