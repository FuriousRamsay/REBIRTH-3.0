import csv,io
from pathlib import Path
p=Path(__file__).resolve().parents[2]/'_Documentation/Purge/FullRegression20261102/LEGACY_REQUIREMENT_MATRIX.csv'
notes={
 'T19':'B25 Fort Choppa native epoch1 reached44/44 terminal death receipts/33combat rooms/Clearedrev370; one clear-tooltip44/audio dispatch pair.43 actors have player contributor; vulture552 has no contributor, correctly not supply-credited. Staged/protected recovery after player death is not ordinary traversal/MP acceptance.',
 'T20':'B25 actual player death unloaded unresolved native participants; journal retained incomplete state until all44 native terminal receipts. Actual physical volume entry restored pending actors, native guard/pistol combat completed, no forced room completion. Further incomplete-spawn/remote cases pending.',
 'T24':'B25 native Fort reset epoch1 reclear44participants, durableClearrev370 retained through B27 cold load. Quest-rally/native multi-client reset remains pending.',
 'T31':'B25 fourth normal reward:187observed/180spent/4earned/4delivered; equippedTier5 price40. Fort cumulative supply85=prior42+new43 attributed deaths; uncredited vulture552 has native death/no contributor. All tiers/cap pending.',
 'T34':'B25 fresh644born72332 genuinely owned/delivered4; B27 native landed crate opened and collected. Plane visuals/rejection remain separate.',
 'T36':'B27 owner native base aim/Activate opened persisted crate644 after actual chunk unload/reload; full inventory caused correct partial take, remaining loot retained, then full native collection after storing8test weapons. Nonowner/MP not exercised.',
 'T37':'B27 FIX VERIFIED LOCAL LANDED: actual644loadedowned -> distant teleport observerloadedfalse -> return same644/born72332/Persistencyparsed26->103/savedSourcetrue/currentworld+owner+ID+birthtrue. Account187/180/4/4 unchanged. Full native take-all removes644, exact item-count conservationPASS. B24 failing unload control retained. In-flight save/reconnect/remote coverage still pending; statusPARTIAL supersedes local unload failure.',
 'T38':'B27 actual landed persisted pine_forest reward13stacks opened through native15second search and bagStorage UI. Full item-name/count conservation across player+fixturestorage+rewardPASS, crate removed/accountunchanged. Remaining biome/objective selectors and per-instance mod identity not qualified.',
 'T44':'B25/B27 completed profiler captures reviewed after normalquit: GCmax2.812/2.278seconds; allocationsOFF, mixed startup/stagedplay prevents controlledFPS/leak claim. Full reportsBATCH25.md/BATCH27.md.',
 'T45':'B27 fresh ingame log491->933 errorobserver0, normalgame/profilerEXIT0. B25 sameprocessreload NPC aggregate exception captured and parked; causeofspawnstall notproven, no bypass. Globalerroracceptance incomplete.',
 'T52':'B27 native Character/Purge summary19discovered/9cleared/187kills/4earned/4launched matches currentdurableledger/account; filters/wheels ran, summary screenshotvisuallyreviewed. All overflow/history/remote scopes remain partial.'
}
lines=p.read_text(encoding='utf-8-sig').splitlines(keepends=True)
header=next(csv.reader([lines[0]]));ei=header.index('current_evidence');si=header.index('current_status')
for i in range(1,len(lines)):
 row=next(csv.reader([lines[i]]));note=notes.get(row[0])
 if not note or note in row[ei]:continue
 row[ei]=(row[ei]+' '+note).strip()
 if row[0] in ('T37','T38'):row[si]='PARTIAL'
 out=io.StringIO(newline='');csv.writer(out,lineterminator='\n').writerow(row);lines[i]=out.getvalue()
p.write_text(''.join(lines),encoding='utf-8');print('B25/B27 scoped evidence recorded; all67 requirements retained')
