import sys,json
from pathlib import Path
sys.path.insert(0,str(Path('Tools/AssetBundle/.python').resolve()));import UnityPy
p=Path('../../Data/Addressables/Standalone/automatic_assets_other/items.bundle');env=UnityPy.load(str(p));controllers=[];animators=[];prefab=None
for obj in env.objects:
 if obj.type.name in ['AnimatorController','AnimatorOverrideController']:
  d=obj.read();controllers.append({'id':obj.path_id,'type':obj.type.name,'name':d.m_Name,'tree':obj.read_typetree()})
 if obj.path_id==-4133653590010929352:prefab=obj
# Follow the exact weapon prefab transform tree, not unrelated reload clips.
seen=set()
def walk(go):
 if go.path_id in seen:return
 seen.add(go.path_id);d=go.read()
 for c in d.m_Component:
  ptr=c.component if hasattr(c,'component') else c[1];co=ptr.read()
  if ptr.type.name=='Animator':animators.append({'go':d.m_Name,'id':ptr.path_id,'tree':ptr.read_typetree()})
  if ptr.type.name in ['Transform','RectTransform']:
   for child in co.m_Children:walk(child.read().m_GameObject)
walk(prefab)
out={'prefabId':prefab.path_id,'animators':animators,'controllers':controllers,'externals':[str(x) for x in prefab.assets_file.externals]}
Path('_Documentation/PlayerReports_20261102/PipeMachineGunDps/native-controller-index.json').write_text(json.dumps(out,default=str,indent=2))
print(json.dumps({'animators':animators,'controllers':[{'id':x['id'],'name':x['name'],'type':x['type']} for x in controllers],'externals':out['externals']},default=str,indent=2)[:7000])
