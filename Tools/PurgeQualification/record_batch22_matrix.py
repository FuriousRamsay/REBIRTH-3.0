"""Append scoped native evidence; preserve existing status and unrelated CSV rows."""
import csv, io
from pathlib import Path
p=Path(__file__).resolve().parents[2]/'_Documentation/Purge/FullRegression20261102/LEGACY_REQUIREMENT_MATRIX.csv'
updates={
 'T09':'B22 cabin_10 boundary walk yielded one fresh discovery tooltip/audio dispatch pair after asynchronous publication. Earlier immediate snapshot was unqualified, not missing registration. Previously discovered cabins correctly silent. Audible/remote coverage still pending.',
 'T12':'B22 cold restored cleared station with feedback trace total zero (no discovery/clear dispatch replay). Discovered-only reconnect and audible playback still unqualified.',
 'T19':'B22 cabin_13 actual native activation and staged native combat: 16 real deaths, 9 verified combat rooms, Clear rev234, one clear dispatch pair. Ordinary all-room walking and remote notice recipients remain unqualified.',
 'T34':'B22 credits96->112/spent75->105/earned1->2/delivered2, actual owned loaded crate459 at[820,47,-330], map gold marker/cycle button inspected. Full flight trajectory and rejection still pending.',
 'T36':'B22 owned crate access attempt refused target settlement before Activate; no opening or collection occurred. Remains unqualified; crate preserved for corrected low-aim script.',
 'T44':'B22 finished capture records sampler suspension32.08% of wall time, allocationsOFF, substantial perturbation. No controlled FPS/leak conclusion; sampling overhead must be addressed before causal performance claims.',
 'T45':'B22 final error observer zero entries, retained raw failures from fixture capacity/obstruction/target-settlement. No broad all-scenario error-free claim.',
 'T49':'B22 native final HUD Forest7/320 after new16-death cabin clear; map green checks inspected. Reload/remote total stability still pending.'
}
lines=p.read_text(encoding='utf-8-sig').splitlines(keepends=True); header=next(csv.reader([lines[0]])); index=header.index('current_evidence')
for i,line in enumerate(lines[1:],1):
 row=next(csv.reader([line])); note=updates.get(row[0])
 if note and note not in row[index]:
  row[index]=(row[index]+' '+note).strip(); out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerow(row);lines[i]=out.getvalue()
p.write_text(''.join(lines),encoding='utf-8')
print('B22 evidence appended to eight rows; statuses unchanged')
