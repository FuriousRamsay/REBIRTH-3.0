#!/usr/bin/env python3
"""Pure-data regression vectors for post-Revision-2 Chunk 3 Trait reconciliation."""
from __future__ import annotations
import argparse, csv, sys
from collections import Counter, defaultdict
from pathlib import Path
import xml.etree.ElementTree as ET

EXPECTED_COUNTS={"IMPLEMENTED":39,"CREATION_ONLY":13,"PARTIAL":13,"DEFERRED":72}
EXPECTED_PARTIAL={
    "trait.disease_resistant","trait.sickly","trait.sure_footed","trait.bad_knees","trait.optimistic","trait.pessimistic",
    "trait.steady_nerves","trait.anxious","trait.old_training_injury","trait.wrist_wear","trait.trade_knees","trait.shoulder_wear","trait.repetitive_strain",
}

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--root',type=Path,default=Path(__file__).resolve().parents[2]); args=ap.parse_args(); root=args.root.resolve()
    tr=ET.parse(root/'Config/_Survivor/traits.xml').getroot().findall('./trait')
    cp=ET.parse(root/'Config/_Survivor/condition_profiles.xml').getroot().findall('./modifier_profiles/modifier_profile')
    sp=ET.parse(root/'Config/_Survivor/support_profiles.xml').getroot().findall('./support_profile')
    profiles={p.get('owner_trait_id'):p for p in cp}; supports=defaultdict(list); support_by_id={p.get('id'):p for p in sp}
    for p in sp:
        if p.get('habit_trait_id'): supports[p.get('habit_trait_id')].append(p.get('id'))
        for n in p.findall('./supported_traits/trait'): supports[n.get('id')].append(p.get('id'))
    with (root/'Config/Localization.csv').open(encoding='utf-8-sig',newline='') as f:
        rr=csv.reader(f); next(rr,None); english={r[0]:r[1] for r in rr if len(r)>=2 and r[0]}

    checks=[]
    def check(name, ok, detail='ok'): checks.append((name,bool(ok),detail))
    check('authored Trait count',len(tr)==137,f'count={len(tr)}')
    counts=Counter(profiles[t.get('id')].get('implementation_state') for t in tr if t.get('id') in profiles)
    check('canonical state counts',dict(counts)==EXPECTED_COUNTS,f'actual={dict(counts)} expected={EXPECTED_COUNTS}')
    partial={t.get('id') for t in tr if profiles[t.get('id')].get('implementation_state')=='PARTIAL'}
    check('PARTIAL release-gate set',partial==EXPECTED_PARTIAL,f'actual={sorted(partial)}')
    deferred_bad=[t.get('id') for t in tr if (t.get('availability')=='deferred') != (profiles[t.get('id')].get('implementation_state')=='DEFERRED')]
    check('deferred availability contract',not deferred_bad,f'bad={deferred_bad}')
    mismatch=[]
    for t in tr:
        p=profiles[t.get('id')]; a=t.find('./authoring'); desc=english.get(t.get('description_key'),'')
        if not desc or a is None or a.get('effect_summary')!=desc or p.get('effect_summary')!=desc: mismatch.append(t.get('id'))
    check('UI description equals canonical effect summary',not mismatch,f'mismatches={mismatch}')
    implemented_missing=[]; creation_bad=[]; partial_empty=[]
    for t in tr:
        tid=t.get('id'); p=profiles[tid]; state=p.get('implementation_state'); comps=p.findall('./component')
        cr=any(c.get('phase')=='creation' for c in comps); rt=any(c.get('phase')=='runtime' for c in comps); sup=bool(supports[tid])
        if state=='IMPLEMENTED' and not (rt or sup): implemented_missing.append(tid)
        if state=='CREATION_ONLY' and (not cr or rt): creation_bad.append((tid,cr,rt))
        if state=='PARTIAL' and not (cr or rt or sup): partial_empty.append(tid)
    check('IMPLEMENTED Traits have runtime/support path',not implemented_missing,f'bad={implemented_missing}')
    check('CREATION_ONLY Traits are creation-only',not creation_bad,f'bad={creation_bad}')
    check('PARTIAL Traits retain a real existing path',not partial_empty,f'bad={partial_empty}')

    alcohol=support_by_id.get('support.alcohol_habit'); caffeine=support_by_id.get('support.caffeine'); nicotine=support_by_id.get('support.nicotine_patch')
    def effects(p): return {(e.get('target'),e.get('value'),e.get('scope'),e.get('state')) for e in p.findall('./effects/effect')} if p is not None else set()
    ae,ce,ne=effects(alcohol),effects(caffeine),effects(nicotine)
    check('Heavy Drinker/Teetotaler support contract', alcohol is not None and alcohol.get('managed_seconds')=='2700' and ('mood.target','-4','habit_trait','unsatisfied') in ae and ('mood.target','3','habit_trait','managed') in ae and ('mood.target','-6','trait:trait.teetotaler','positive') in ae)
    check('Caffeine Dependent support contract', caffeine is not None and caffeine.get('managed_seconds')=='2700' and ('mood.target','-3','habit_trait','unsatisfied') in ce and ('mood.target','2','habit_trait','managed') in ce)
    check('Smoker support contract', nicotine is not None and nicotine.get('managed_seconds')=='3600' and ('mood.target','-4','habit_trait','unsatisfied') in ne and ('mood.target','4','habit_trait','managed') in ne)
    dusty=profiles['trait.dusty_lungs']; dc={(c.get('target'),c.get('value'),c.get('phase')) for c in dusty.findall('./component')}
    check('Dusty Lungs bounded runtime fix', dusty.get('implementation_state')=='IMPLEMENTED' and ('energy.use.sprint_jump','1.10','runtime') in dc and ('energy.recovery','0.95','runtime') in dc,f'components={sorted(dc)}')
    recon=(root/'Scripts/Survivor/Definitions/RebirthTraitRuntimeReconciliation.cs').read_text(encoding='utf-8',errors='replace')
    cmd=(root/'Scripts/Survivor/Debug/ConsoleCmdRebirthSurvivor.cs').read_text(encoding='utf-8',errors='replace')
    check('generated Aptitudes reconcile as CreationOnly','IsAptitude(trait)) return RebirthTraitImplementationState.CreationOnly' in recon)
    check('Trait diagnostics command wired','RebirthTraitRuntimeReconciliation.BuildSummary()' in cmd and 'RebirthTraitRuntimeReconciliation.BuildTraitReport' in cmd)

    failures=[c for c in checks if not c[1]]
    print('REBIRTH Survivor Trait reconciliation vectors: '+('PASS' if not failures else 'FAIL'))
    print(f'pass={len(checks)-len(failures)} fail={len(failures)} total={len(checks)}')
    for i,(name,ok,detail) in enumerate(checks,1): print(f'{i:02d}. {"PASS" if ok else "FAIL"} {name} :: {detail}')
    return 1 if failures else 0

if __name__=='__main__': sys.exit(main())
