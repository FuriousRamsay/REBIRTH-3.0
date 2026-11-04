import csv, io
from pathlib import Path
p=Path(__file__).resolve().parents[2]/'_Documentation/Purge/FullRegression20261102/LEGACY_REQUIREMENT_MATRIX.csv'
notes={
 'T02':'B24 same-process native menu/save reload retained Purge theme and144/140/3/3 ledger. Current saved-theme UI lock not exercised.',
 'T12':'B24 actual native Exit/menu/Continue/same disposable Start in SAME profiler PID; restored feedback trace0, no old clear/discovery dispatch replay. Remote/audible coverage pending.',
 'T13':'B24 living native station epoch2 had exactly2rooms, thresholdfalse/markers0, then actual3deathsClear. Two-room low-tier exclusion PASS_LOCAL; three-room boundary still pending.',
 'T19':'B24 exact actual native deaths/Clear3+5+16+8 across station/cabin10/resetcabin13/cabin14, four clear feedback pairs. Obstructed staging failures retained separately; recovery verified final actual receipts. No ordinary traversal/remote claim.',
 'T24':'B24 native cr completed new station epoch2 and cabin13epoch1, followed by actual3/16deaths and Clear. Real rally/quest reset and remote coverage remain pending.',
 'T31':'B24 actual total144/spent140/earned3/delivered3 after third native entitlement with equipped tier5. Cap and all tiers remain pending.',
 'T34':'B24 fresh actual ownedcrate503/born64087 descended from native cached209 to actualground38; valid original stamp and delivered3. Plane visuals/rejection not qualified.',
 'T37':'B24 independently reproduced: fresh503 saved authoritative Persistency26->103 after native same-process worldreload; PROVENunload/return loses trailer at26 withsame ID/birth/world. Root writeguard used Unity destroyed-object null. Corrected CLR-nullguard passes36actual-helper checks and builds, but native fix acceptance PENDING; failure status retained.',
 'T42':'B24 four completed native clear notices not replayed after actual menu/save reload (trace0), ledger unchanged. Automatic deferred teaching/remote remain pending.',
 'T44':'B24 completed45.7minute mixedrun with nativeworldreload; allocationsOFF, heapend6.202GBreserved/5.922used, samplerbrackets10.70%wall includingcontextwait. Heapincrease not leakproof or controlledFPS evidence; broad audit remains secondary.',
 'T45':'B24 final native error observer0; Player.log retained; raw precondition/combat/staging failures not reclassified as passes.'
}
lines=p.read_text(encoding='utf-8-sig').splitlines(keepends=True);h=next(csv.reader([lines[0]]));ei=h.index('current_evidence');si=h.index('current_status')
for i,line in enumerate(lines[1:],1):
 row=next(csv.reader([line]));note=notes.get(row[0])
 if not note or note in row[ei]:continue
 row[ei]=(row[ei]+' '+note).strip()
 if row[0]=='T02' and row[si]=='PENDING':row[si]='PARTIAL'
 out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerow(row);lines[i]=out.getvalue()
p.write_text(''.join(lines),encoding='utf-8');print('B24 evidence recorded; unload failure remains until native fix acceptance, full67 scope unchanged')
