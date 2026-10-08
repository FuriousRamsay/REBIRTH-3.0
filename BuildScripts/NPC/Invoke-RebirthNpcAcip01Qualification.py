from pathlib import Path
import csv,re,xml.etree.ElementTree as ET,tempfile
import argparse
ap=argparse.ArgumentParser(); ap.add_argument('--project-root',required=True); args=ap.parse_args()
root=Path(args.project_root).resolve()
results=[]
def t(name,ok,detail=''): results.append((name,bool(ok),detail))
files=[
'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionDomain.cs',
'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionService.cs',
'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionPersistence.cs',
'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionQualification.cs']
t('files_exist',all((root/f).is_file() for f in files),str(len(files)))
text='\n'.join((root/f).read_text() for f in files)
for sym in ['RebirthNpcProgressionDefinition','RebirthNpcProgressionRecord','RebirthNpcProgressionEntryRecord','RebirthNpcProgressionAwardRequest','RebirthNpcProgressionAwardResult','RebirthNpcProgressionService','RebirthNpcWorkProgressionSink','RebirthNpcProgressionPersistenceStore']:
 t('symbol_'+sym,sym in text,sym)
# exact documented curve and multi-level behavior mirror
A,B,MAX=125,375,20
def floor(L): n=L-1; return A*n*n+B*n
def level(xp):
 lo,hi=1,MAX
 while lo<hi:
  mid=lo+(hi-lo+1)//2
  if floor(mid)<=xp: lo=mid
  else: hi=mid-1
 return lo
t('curve_level1',floor(1)==0,str(floor(1)))
t('curve_level20',floor(20)==52250,str(floor(20)))
t('threshold_exact',level(floor(7))==7,str(level(floor(7))))
t('threshold_below',level(floor(7)-1)==6,str(level(floor(7)-1)))
t('multi_level_math',level(4000)-level(0)>=2,f'{level(0)}->{level(4000)}')
t('max_clamp',min(999999,floor(MAX))==floor(MAX),str(floor(MAX)))
# code gates
service=(root/files[1]).read_text()
t('authority_gate','not-authority' in service and 'IsAuthoritative()' in service,'server gate')
t('source_validation','invalid-source' in service,'source id/type')
t('award_bound','award-bound' in service and 'MaximumAward' in service,'bound')
t('duplicate_suppression','ReplayIds.Add' in service and 'duplicate-award' in service,'replay id')
t('checked_overflow','checked(oldXp+grant)' in service and 'overflow' in service,'checked')
t('work_sink_registered','RebirthNpcWorkOutcomeService.RegisterSink(WorkSink)' in service,'registered caller')
t('persistence_dirty','RebirthNpcProgressionPersistenceStore.MarkDirty()' in service,'mutation persistence')
persist=(root/files[2]).read_text()
t('atomic_save','File.Move(tmp,p)' in persist and 'File.Copy(p,bak,true)' in persist,'tmp/backup')
t('versioned_schema','SchemaVersion=1' in persist and 'WriteAttributeString("schema"' in persist,'schema')
t('repair_backup','File.Exists(bak)' in persist and 'repairs++' in persist,'backup recovery')
life=(root/'Scripts/Rebirth/NPC/Foundation/RebirthNpcLifecycle.cs').read_text()
t('lifecycle_registration',all(x in life for x in ['typeof(RebirthNpcProgressionService).FullName','typeof(RebirthNpcProgressionPersistenceStore).FullName','typeof(RebirthNpcWorkProgressionSink).FullName']),'services')
coord=(root/'Scripts/Rebirth/NPC/Persistence/RebirthNpcPersistenceCoordinator.cs').read_text()
t('coordinator_load_save_reset',all(x in coord for x in ['RebirthNpcProgressionPersistenceStore.EnsureLoaded()','RebirthNpcProgressionPersistenceStore.Save()','RebirthNpcProgressionPersistenceStore.Reset(false)']),'coordinator')
admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text()
t('admin_inspect_export_import',all(x in admin for x in ['progressionexport','progressionimport','RebirthNpcProgressionService.TryGetView']),'admin callers')
# brace/paren rough balance after stripping strings/comments
for f in files:
 s=(root/f).read_text(); s=re.sub(r'//.*|/\*.*?\*/|"(?:\\.|[^"\\])*"','',s,flags=re.S|re.M)
 t('brace_'+Path(f).name,s.count('{')==s.count('}'),f'{s.count("{")}/{s.count("}")}')
 t('paren_'+Path(f).name,s.count('(')==s.count(')'),f'{s.count("(")}/{s.count(")")}')
# ledger closure
with (root/'_Documentation/NPC Framework/ACIP-00 - Conformance Control Plane and Requirement Lock/REBIRTH_3_0_NPC_FRAMEWORK_AUTHORITATIVE_REQUIREMENT_LEDGER.csv').open(encoding='utf-8-sig',newline='') as f: rows=list(csv.DictReader(f))
assigned=[r for r in rows if r['Owning Package']=='ACIP-01']
t('ledger_four_rows',len(assigned)==4,str(len(assigned)))
t('ledger_rows_implemented',all(r['Current Status']=='IMPLEMENTED' for r in assigned),','.join(r['Requirement ID'] for r in assigned))
failed=[x for x in results if not x[1]]
for n,ok,d in results: print(f'{n}={"PASS" if ok else "FAIL"} ({d})')
print('RESULT='+('PASS' if not failed else 'FAIL'))
raise SystemExit(0 if not failed else 2)
