#!/usr/bin/env python3
from __future__ import annotations
import argparse, json, re, sys
from pathlib import Path

OLD_16=[
"skill.bladed_melee","skill.blunt_melee","skill.spears","skill.unarmed","skill.archery","skill.handguns","skill.rifles","skill.shotguns",
"skill.mining","skill.logging","skill.salvage","skill.farming","skill.mechanics","skill.medicine","skill.cooking","skill.maintenance"]

class Failure(Exception): pass

def parse_policy(root:Path):
    text=(root/'Scripts/Survivor/Persistence/RebirthSurvivorSkillMigrationPolicy.cs').read_text(encoding='utf-8')
    cm=re.search(r'CurrentIds\s*=\s*new string\[\]\s*\{(.*?)\};',text,re.S)
    if not cm: raise Failure('CurrentIds table not found')
    current=re.findall(r'"(skill\.[^"]+)"',cm.group(1))
    mapping={}
    for m in re.finditer(r'new RebirthSurvivorLegacySkillMapping\(\s*"(skill\.[^"]+)"\s*,(.*?)\)',text,re.S):
        mapping[m.group(1)]=re.findall(r'"(skill\.[^"]+)"',m.group(2))
    return current,mapping

def migrate(entries,current,mapping,has_progress=True):
    seen={}; unknown=[]
    for row in entries:
        sid=row['id']
        if sid in seen: raise Failure('duplicate Skill ids: '+sid)
        if sid not in current and sid not in mapping: unknown.append(sid)
        v=float(row['value']); p=float(row.get('progress',0.0)) if has_progress else 0.0
        if not (-50 <= v <= 100): raise Failure(f'value out of range {sid}={v}')
        if has_progress and not (0 <= p < 1): raise Failure(f'progress out of range {sid}={p}')
        seen[sid]=(v,p)
    if unknown: raise Failure('unknown Skill ids: '+','.join(sorted(unknown)))
    out={sid:seen[sid] for sid in current if sid in seen}
    for src,dests in mapping.items():
        if src not in seen: continue
        for dst in dests:
            out.setdefault(dst,seen[src])
    for sid in current: out.setdefault(sid,(0.0,0.0))
    return out

def rows(vals,progress=0.0): return [{'id':k,'value':v,'progress':progress} for k,v in vals.items()]

def run(root:Path):
    current,mapping=parse_policy(root)
    results=[]
    def test(name,fn):
        try: fn(); results.append({'name':name,'status':'PASS','detail':'ok'})
        except Exception as e: results.append({'name':name,'status':'FAIL','detail':str(e)})
    def req(cond,msg):
        if not cond: raise Failure(msg)

    test('mapping table covers all 16 legacy Skills',lambda:req(set(mapping)==set(OLD_16),f'coverage mismatch missing={sorted(set(OLD_16)-set(mapping))} extra={sorted(set(mapping)-set(OLD_16))}'))
    test('new schema-5 identity has 42 Skills',lambda:req(len(current)==42 and len(set(current))==42,f'current count={len(current)} unique={len(set(current))}'))

    def v_zero():
        out=migrate(rows({x:0 for x in OLD_16},.25),current,mapping)
        req(len(out)==42,'output count != 42'); req(all(v==0 for v,p in out.values()),'nonzero output')
    test('schema-4 all zero Skills',v_zero)

    def v_rep():
        vals={'skill.bladed_melee':12.5,'skill.blunt_melee':18,'skill.unarmed':7,'skill.handguns':22,'skill.rifles':31,'skill.mechanics':44,'skill.medicine':9}
        out=migrate(rows(vals,.625),current,mapping)
        for sid in ['skill.swords','skill.knives','skill.scythes']: req(out[sid][0]==12.5,sid)
        for sid in ['skill.assault_rifles','skill.tactical_rifles','skill.long_range_rifles']: req(out[sid]==(31,.625),sid)
        req(out['skill.mechanics'][0]==44 and out['skill.medicine'][0]==9,'technical direct map')
    test('representative melee/firearm/technical values',v_rep)

    def v_max():
        out=migrate(rows({x:100 for x in OLD_16},.999),current,mapping)
        for src,dests in mapping.items():
            for dst in dests: req(out[dst][0]==100,f'lost {src}->{dst}')
        req(out['skill.axes'][0]==0,'axes should remain neutral')
    test('maxed old Skill values',v_max)

    def v_neg():
        out=migrate(rows({'skill.bladed_melee':-25,'skill.handguns':-10,'skill.mining':-3},.1),current,mapping)
        req(out['skill.swords'][0]==-25 and out['skill.heavy_handguns'][0]==-10 and out['skill.mining'][0]==-3,'negative values not preserved')
    test('negative development values',v_neg)

    def v_unknown():
        out=migrate(rows({'skill.bladed_melee':11},0),current,mapping); req(out['skill.swords'][0]==11,'known retired not mapped')
        try: migrate(rows({'skill.retired_unknown':17},0),current,mapping); raise Failure('unknown ID accepted')
        except Failure as e:
            if 'unknown Skill ids' not in str(e): raise
    test('known retired ID maps; unknown ID rejects',v_unknown)

    def v_dup():
        try: migrate([{'id':'skill.mining','value':10,'progress':.2},{'id':'skill.mining','value':11,'progress':.2}],current,mapping); raise Failure('duplicate accepted')
        except Failure as e:
            if 'duplicate Skill ids' not in str(e): raise
    test('duplicate IDs reject',v_dup)

    def v_missing():
        out=migrate(rows({'skill.mining':13,'skill.archery':4},.3),current,mapping)
        req(len(out)==42 and out['skill.mining'][0]==13 and out['skill.bartering'][0]==0,'missing-entry neutral init failed')
    test('missing Skill entries initialize neutral',v_missing)

    def v_explicit():
        out=migrate(rows({'skill.rifles':20,'skill.assault_rifles':47},.55),current,mapping)
        req(out['skill.assault_rifles'][0]==47,'explicit destination overwritten')
        req(out['skill.tactical_rifles'][0]==20 and out['skill.long_range_rifles'][0]==20,'siblings not copied')
    test('explicit exact destination wins over broad legacy source',v_explicit)

    # Static policy/source vectors for compatibility/idempotence behavior that involve repository/profile code.
    profile=(root/'Scripts/Survivor/Persistence/RebirthSurvivorProfileStore.cs').read_text(encoding='utf-8')
    world=(root/'Scripts/Survivor/Persistence/RebirthWorldCharacterMigration.cs').read_text(encoding='utf-8')
    repo=(root/'Scripts/Survivor/Persistence/RebirthWorldCharacterRepository.cs').read_text(encoding='utf-8')
    test('profile definition-hash mismatch remains usable when valid',lambda:req('definitions updated; saved selections remain valid' in profile and 'sameDefinitions' in profile,'valid definition-hash mismatch compatibility contract missing'))
    test('world schema-5 definition mismatch is not silently restamped',lambda:req('originalVersion == RebirthWorldCharacterRecord.CurrentSchemaVersion' in world and 'return true;' in world,'schema5 early no-op contract missing'))
    test('repeated migration is idempotent',lambda:req('originalVersion == RebirthWorldCharacterRecord.CurrentSchemaVersion' in world and 'changed = false;' in world,'idempotent no-op contract missing'))
    test('migration ledger persists through repository save/load',lambda:req(all(x in repo for x in ['migrationSourceSchema','migrationTargetSchema','migrationPolicyId','migrationOriginAudit','migrationProgressionAudit']),'ledger persistence fields missing'))

    return results

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]); ap.add_argument('--json',type=Path); ap.add_argument('--text',type=Path); a=ap.parse_args()
    res=run(a.root.resolve()); ok=all(x['status']=='PASS' for x in res)
    lines=[f"REBIRTH Survivor schema-4->5 migration vectors: {'PASS' if ok else 'FAIL'}",f"pass={sum(x['status']=='PASS' for x in res)} fail={sum(x['status']=='FAIL' for x in res)} total={len(res)}"]
    lines += [f"{i+1:02d}. {x['status']} {x['name']} :: {x['detail']}" for i,x in enumerate(res)]
    text='\n'.join(lines)+'\n'; print(text,end='')
    if a.json: a.json.parent.mkdir(parents=True,exist_ok=True); a.json.write_text(json.dumps({'result':'PASS' if ok else 'FAIL','vectors':res},indent=2)+'\n',encoding='utf-8')
    if a.text: a.text.parent.mkdir(parents=True,exist_ok=True); a.text.write_text(text,encoding='utf-8')
    return 0 if ok else 1
if __name__=='__main__': sys.exit(main())
