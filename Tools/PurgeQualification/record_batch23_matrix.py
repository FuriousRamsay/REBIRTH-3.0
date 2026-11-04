import csv, io
from pathlib import Path
p=Path(__file__).resolve().parents[2]/'_Documentation/Purge/FullRegression20261102/LEGACY_REQUIREMENT_MATRIX.csv'
notes={
 'T12':'B23 cold load feedback trace0; existing clear/owned crate map restored. Local only.',
 'T36':'B23 no access attempt: same crate459 was owned after cold load, but authoritative ownership became false after local unload/return. No Activate/take-all; custody regression must be fixed before owner/nonowner access acceptance.',
 'T37':'B23 FAIL_LOCAL_UNLOAD: actual delivered crate459 persisted and initially owned after cold load; after station visit and return it was loaded at original position but ownedLoadedCrate=false, settled followup confirms, ledger112/105/2/2 unchanged. See BATCH23.md; no ownership bypass or deletion performed.',
 'T44':'B23 explicit25Hz actual15.028Hz,15611samples/no stackdrops. Capture brackets~25%wall include OS/context wait; mixed run cannot prove added pause cost or FPS/leak improvement.',
 'T45':'B23 final error observer0, raw precondition failures preserved. No full all-scenario claim.',
 'T52':'B23 real Character/Purge UI: All/Discovered/Purged and wheel to distinct later rows/back to initial rows; summary/purged/later/top screenshots visually inspected. Summary19discovered/112kills/2earned/2launched matches observer fields;7cleared displayed. Images/borders clipped. Full count reconciliation, all overflow/remote/history remain pending.'
}
lines=p.read_text(encoding='utf-8-sig').splitlines(keepends=True); h=next(csv.reader([lines[0]])); ei=h.index('current_evidence');si=h.index('current_status')
for i,line in enumerate(lines[1:],1):
 row=next(csv.reader([line])); note=notes.get(row[0])
 if not note or note in row[ei]: continue
 row[ei]=(row[ei]+' '+note).strip()
 if row[0]=='T37': row[si]='FAIL_LOCAL_UNLOAD'
 if row[0]=='T52': row[si]='PARTIAL'
 out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerow(row);lines[i]=out.getvalue()
p.write_text(''.join(lines),encoding='utf-8');print('B23 evidence recorded: T37 local failure, T52 partial; full scope retained')
