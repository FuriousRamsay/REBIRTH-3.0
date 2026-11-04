from pathlib import Path
import json,hashlib
from lxml import etree as E
r=Path(__file__).resolve().parents[2];t=Path(__file__).parent;d=r/'_Documentation/PlayerReports_20261102/PipeMachineGunDps'
s=(r/'Scripts/Survivor/Progression/RebirthWeaponSustainedDpsService.cs').read_text(encoding='utf-8-sig')
def member(key):
 start=s.index(key);i=s.index('{',start)+1;depth=1
 while depth:
  depth+=(s[i]=='{')-(s[i]=='}');i+=1
 return s[start:i]
parts=[member('internal static float ResolveRangedReloadBase('),member('public static float CalculateRangedSustainedDps(')]
source='using System; public static class Actual {static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);'+''.join(parts)+'}'
(t/'Actual.g.cs').write_text(source)
a=json.loads((d/'native-animation-index.json').read_text());c=json.loads((d/'native-controller-index.json').read_text());anim=c['animators'][0]['tree'];controller=next(x['tree'] for x in c['controllers'] if x['id']==anim['m_Controller']['m_PathID']);names={x[0]:x[1] for x in controller['m_TOS']}
states=[x['data'] for l in controller['m_Controller']['m_StateMachineArray'] for x in l['data']['m_StateConstantArray'] if names.get(x['data']['m_NameID'])=='PipeMachineGunReload'];assert len(states)==1
st=states[0];node=st['m_BlendTreeConstantArray'][0]['data']['m_NodeArray'][0]['data'];ref=controller['m_AnimationClips'][node['m_ClipID']];clip=next(x for x in a['clips'] if x['id']==ref['m_PathID']);duration=clip['stop']-clip['start']
assert anim['m_Controller']['m_PathID']==-6878366795526637927 and st['m_Speed']==1 and names[st['m_SpeedParamID']]=='ReloadSpeed' and st['m_TransitionConstantArray'][0]['data']['m_ExitTime']==1
player=json.loads((d/'native-player-controller-index.json').read_text());fp=next(x['tree'] for x in player['controllers'] if x['id']==-4076251250568844791);fpnames=dict(fp['m_TOS'])
fpstates=[x['data'] for l in fp['m_Controller']['m_StateMachineArray'] for x in l['data']['m_StateConstantArray'] if fpnames.get(x['data']['m_NameID'])=='fpvPipeMachineGunReload'];assert len(fpstates)==1
fps=fpstates[0];fpi=fps['m_BlendTreeConstantArray'][0]['data']['m_NodeArray'][0]['data']['m_ClipID'];fpref=fp['m_AnimationClips'][fpi];fpclip=next(x for x in player['pipeClips'] if x['id']==fpref['m_PathID']);fpduration=fpclip['stop']-fpclip['start']
assert fpclip['name']=='FP_PipeMachineGun_Reload' and fps['m_Speed']==1 and fpnames[fps['m_SpeedParamID']]=='ReloadSpeed'
transition=fps['m_TransitionConstantArray'][0]['data'];assert not transition['m_HasFixedDuration'] and abs(transition['m_ExitTime']+transition['m_TransitionDuration']-1)<1e-5
assert hashlib.sha256(Path(player['bundle']).read_bytes()).hexdigest()==player['sha256']
meta=E.parse(str(r/'Config/_Rebirth/weapon_dps_metadata.xml'));seconds=float(meta.xpath('//property[@name="RebirthDpsReloadSeconds"]/@value')[0]);assert abs(seconds-fpduration)<1e-5
assert 'metadataBaseReload <= 0f && liveHeld' in s
assert 'ResolveRangedReloadBase(hasMeasuredReload ? measuredBaseReload : 0f,' in s
receipt={'nativePrefabId':c['prefabId'],'controller':-6878366795526637927,'state':names[st['m_NameID']],'clip':clip,'duration':duration,'metadataSeconds':seconds,'stateSpeed':st['m_Speed'],'speedParameter':names[st['m_SpeedParamID']],'exitTime':st['m_TransitionConstantArray'][0]['data']['m_ExitTime'],'bundleSHA256':hashlib.sha256(Path(a['bundle']).read_bytes()).hexdigest(),'productionSourceSHA256':hashlib.sha256(s.encode()).hexdigest(),'scope':'Offline serialized prefab/controller/clip binding and exact source math; native timing remains pending.'}
receipt.update({'modelOnlyDuration':duration,'firstPersonController':-4076251250568844791,'firstPersonState':'fpvPipeMachineGunReload','firstPersonClip':fpclip,'firstPersonDuration':fpduration,'firstPersonBundleSHA256':player['sha256'],'scope':'Exact player hands and separate weapon-model bindings; full-cycle metadata follows hands. Native post-edit acceptance pending.'})
(d/'metadata-proof.json').write_text(json.dumps(receipt,indent=2));print('PASS native first-person reload binding: 3.466667s metadata; separate 2.9s model clip retained as evidence')
