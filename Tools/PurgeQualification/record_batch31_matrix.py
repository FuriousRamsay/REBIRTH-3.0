import csv,io
from pathlib import Path
p=Path(__file__).resolve().parents[2]/'_Documentation/Purge/FullRegression20261102/LEGACY_REQUIREMENT_MATRIX.csv'
notes={
'T09':'B31 actual known Bungalow03 enter/leave/reenter verified; no duplicate discovery pair, original discoverer/time105092 retained. New HiddenHollow correct Snow discovery pair. Local only.',
'T11':'B31 omission FIX NATIVE SHUTDOWN PASS_LOCAL:23positively observed untouched/current/0actor/absent candidates stayed absent after native quit. All67prior records/epochs/actors/death receipts retained. No old record pruning; hidden/excluded/remote scopes still pending.',
'T12':'B31 cold feedback0; all67 prior records/epochs/actor tokens/class/points/death receipts retained after real shutdown.23idle keys remain absent; subsequent cold and remote scopes separate.',
'T19':'B31 native wilderness_filler17 Snow mixed5rooms (2real sleepers+4zero-actor SVS sound/trigger rooms) Clearrev610/2death receipts/verifiedRooms5; one matching clear tooltip/audio pair2, inspected colored toast. Force/stagedcombat not ordinary full traversal or MP.',
'T20':'B31 2/2dead initiallyDiscovered because actualnative room761script/spawning stillrunning; HUD4/5/~1 honestlypending. Poll6 cleared when scriptfinished; all5wasCleared/no operations/scripts. Mixedzero-actor-room local success, not full original zero-left/MP acceptance.',
'T26':'B31 direct Snow travel/actualclear allowed while Pine10/320 incomplete. Frost/temperature screenshot observed; all-biome/hazard/buff scopes remain pending.',
'T27':'B31 actual Snow1/328 after2native deaths; Pine10/320 unchanged, otherbiomes0. PASS_LOCAL out-of-order Snow progression, otherbiome/milestone/absentbiome scopes remain pending.',
'T44':'B31 finished607.343s14325samplesallocOFF,GC6max1.606s/runtime324max1.555s; heap4.463GBused3.903GB not leakproof, mixedrun no controlledFPS attribution.',
'T45':'B31 finished full log review: no pathfinding/Purge exception found this run; parked NPC save-transition warning and native XBL uninitialize error retained. No global clean-error acceptance.'}
lines=p.read_text(encoding='utf-8-sig').splitlines(keepends=True);h=next(csv.reader([lines[0]]));ei=h.index('current_evidence');si=h.index('current_status');assert len(lines)==68
for i in range(1,len(lines)):
 row=next(csv.reader([lines[i]]));n=notes.get(row[0])
 if not n or n in row[ei]:continue
 row[ei]=(row[ei]+' '+n).strip()
 if row[0] in ('T26','T27'):row[si]='PARTIAL'
 out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerow(row);lines[i]=out.getvalue()
p.write_text(''.join(lines),encoding='utf-8');print('B31 evidence recorded; all67 original requirements retained')
