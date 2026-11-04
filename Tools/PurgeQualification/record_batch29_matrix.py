import csv,io
from pathlib import Path
p=Path(__file__).resolve().parents[2]/'_Documentation/Purge/FullRegression20261102/LEGACY_REQUIREMENT_MATRIX.csv'
notes={
'T09':'B29 LOCAL NATIVE FIX PASS: prepublished first-generation Bungalow03/nohistory actual evidence-owner attempt1, tooltip+audio pair, inspected visible colored toast, map/history while0enrolled/0deaths. Actual leave/reenter no duplicate. B28 failure superseded for this branch; overall PARTIAL: separate untouched-empty shutdown discovery bug confirmed, remote/audible/reset scopes stillpending.',
'T11':'B29 closed record count49->62 confirms untouched-empty candidate shutdown discovery publication. Conservative absent-custody guard plus40 extracted-production checks and clean build prepared; native post-fix pending. Existing records preserved, no cleanup guessed from absent history.',
'T14':'B29 actual tier5 mine01 has48 native combat/script rooms;128 actors/79 actualdeathreceipts, remainingliveactors/scripts prevented oneabove/at3 boundary. PARTIAL; no invented clear or threshold acceptance. Offline22 count omitted SVS scripts and candidate tool corrected.',
'T20':'B29 unfinished native mine01 retained Discovered epoch0rev565 with128 enrolled/79deathreceipts. Native script waves/crawl-space live actors remain; not an empty-POI clear failure reproduction. Preserve custody; no forced clear.',
'T32':'B29 exact Backpack910 native search/R recovery returned actualhandsLumberjackGloves/ModPurgeTier5. TemporarystressgodmodeON later explicitlyOFF before safe normalquit; normalcombat acceptance not claimed.',
'T44':'B29 finishedcapture2636.750s/59489samples,GC6/total5.373s/max2.010s;runtime347/total10.115s/max1.929s;allocOFF/heap4.696GBused4.200GB not leakproof. Samplerbrackets5.2425%wall include scheduling; no controlledFPS or causalfreeze attribution.'}
lines=p.read_text(encoding='utf-8-sig').splitlines(keepends=True);h=next(csv.reader([lines[0]]));ei=h.index('current_evidence');si=h.index('current_status')
assert len(lines)==68
for i in range(1,len(lines)):
 row=next(csv.reader([lines[i]]));note=notes.get(row[0])
 if not note or note in row[ei]:continue
 row[ei]=(row[ei]+' '+note).strip()
 if row[0]=='T09':row[si]='PARTIAL'
 out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerow(row);lines[i]=out.getvalue()
p.write_text(''.join(lines),encoding='utf-8');print('B29 evidence recorded; all67 original rows retained')
