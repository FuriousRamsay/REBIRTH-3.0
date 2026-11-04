import csv,io
from pathlib import Path
p=Path(__file__).resolve().parents[2]/'_Documentation/Purge/FullRegression20261102/LEGACY_REQUIREMENT_MATRIX.csv'
notes={'T09':'B30 three actual new house boundary entries (gambrel01/modular07/victorian08) each completed discovery tooltip/audio pair; Victorian colored toast visually inspected. Overall PARTIAL, remote/audible/reset and idle-neighbor proof pending.', 'T11':'B30 final gameplay67records/rev604 unchanged after shutdown, but positive idle witness hidden behind64-row diagnostic cap: native omission test UNQUALIFIED (exit1). Ordering corrected after quit; needs positive native proof. No record cleanup.', 'T12':'B30 cold62records/rev591 feedback0, all prior closed records/epochs/actor tokens/class/points/death receipts retained after another real quit. Mine79death receipts retained. Remote/reconnect remains pending.', 'T44':'B30 finished493.344s11097samples,GC6max2.012s/runtime329max3.292s,allocOFF, no controlledFPS/leak proof.', 'T45':'B30 log has native ChunkProvider error,6AstarVoxelGrid.CalculateConnections shutdown NREs, NPC save-transition warning parked, Xbox uninitialize error. No error-free acceptance; not attributed to Purge without evidence.'}
lines=p.read_text(encoding='utf-8-sig').splitlines(keepends=True);h=next(csv.reader([lines[0]]));ei=h.index('current_evidence');assert len(lines)==68
for i in range(1,len(lines)):
 row=next(csv.reader([lines[i]]));n=notes.get(row[0])
 if not n or n in row[ei]:continue
 row[ei]=(row[ei]+' '+n).strip();out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerow(row);lines[i]=out.getvalue()
p.write_text(''.join(lines),encoding='utf-8');print('B30 evidence recorded; all67 rows retained')
