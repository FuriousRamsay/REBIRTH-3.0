from pathlib import Path
import json,hashlib,zipfile
r=Path(__file__).resolve().parents[2];d=r/'_Documentation/BackpackRows'
rows=json.loads((d/'full_gear_results.json').read_text());assert len(rows)==8
assert [x['capacity'] for x in rows]==list(range(65,157,13))
assert all(x['encumbranceSlots']==26 and all(x[k] for k in ('shiftIn','shiftOut','dragIn','dragOut','conserved')) for x in rows)
ov=json.loads((d/'overflow_results.json').read_text());assert ov['backpackRecovered'] and ov['beltRecovered']
persist=json.loads((d/'persistence_verify.json').read_text());assert persist['backpackCapacity']==169 and persist['encumbranceSlots']==26
errors=json.loads((d/'final_errors.json').read_text(encoding='utf-8-sig'));assert errors['count']==0
final=json.loads((d/'final_state.json').read_text());assert final['backpackCapacity']==52 and final['encumbranceSlots']==26 and final['gearBackground']=='background.chef'
summary={'status':'PASS - local backpack qualification','base':{'total':52,'usable':26,'encumbered':26},'tierTotals':[x['capacity'] for x in rows],'scavengerBonus':13,'maximumWithScavenger':169,'realInputBackpackTransfers':32,'overflowRecovery':'PASS backpack and belt; recovered exact stacks','persistence':'PASS full restart with3 gold in last slot168','craftingTail':'PASS last13-column row visible; gold dragged back to toolbelt','runtimeErrorsFinalInterval':errors['count'],'buildErrors':0,'buildWarnings':9,'warnings':'existing GameBridge unused fields/variables','limitations':['Local single-player on3.2b10 only; remote/dedicated not run.','Recovery timers checked from runtime values, not elapsed expiry.','Cooking/Creative/native fallback presenter capacities structurally checked; no full station regression.','Not a full release sign-off.'],'commits':False}
(d/'final_validation.json').write_text(json.dumps(summary,indent=2))
with (d/'README.txt').open('a',encoding='utf-8') as f:f.write('''

FINAL QUALIFICATION (2026-10-02)
PASS local installed3.2b10 disposable CodexTest: eight tiers,32 real shift/drag equip/unequip transfers with item conservation and constant26encumbrance; Maintenance Technician belt18 and Scavenger pack169. Occupied slot155 overflow to3600-second recovery bag; exact3gold recovered. Belt overflow11rounds with1800-second runtime lifetime recovered. Maximum169/143usable/26encumbered and3gold at index168 persisted across full process restart. Main Crafting scrollbar reached index168; screenshot inspected and gold dragged to toolbelt. Final interval zero errors. Two builds succeeded,0errors/9existing GameBridge warnings. Original Chef background and attributes restored, god buff removed, game closed.

Traceability: base counts and profile increments -> gear service / support_profiles / static_audit / full_gear_results. Scavenger -> background service/data/localization / background_storage_results / persistence_verify. Item conservation -> full_gear_results and overflow_results. Save compatibility -> persistence_prepare and persistence_verify. UI ceiling -> windows.xml, workspace_windows.xml, inventory bridge; Character and Crafting final slots inspected. Cooking runtime retains its26-column layout while the authored pool now covers169; other custom crafting layouts remain13columns. Native fallback pool175, Character/Creative170, nonphysical cells hidden.

Completion: approved capacity change implemented and locally qualified. Multiplayer packet layout remains unchanged; server/remote paths inspected but not live tested. Recovery lifespan verified from runtime configuration, not a timed hour. Cooking/Creative/fallback ceilings structurally checked; no exhaustive station regression. Existing carry traits retain deliberate modifiers to usable capacity. This is not a full release qualification. Changed-source package includes current cumulative versions of shared files and DLL; preserve other agents' uncommitted work when merging. No commits.
''')
log=r/'_Documentation/Codex_Changes/CUMULATIVE_LOG.txt'
with log.open('a',encoding='utf-8') as f:f.write('''

2026-10-02 — Backpack13-slot rows
Base inventory52physical (26usable/26encumbered); eight backpack tiers add13each, totals65/78/91/104/117/130/143/156. Scavenger bonus now13usable, maximum169. Updated gear limits, carry offset, background tuning, support profiles, localization, presenter pools and authored vector harness. Existing recipes/art/loot/IDs retained. Changes supersede previous45-slot and5-slot-row values above.
Build0errors/9existing warnings. Local GameBridge PASS:32pack transfers, all tiers maintain26encumbered; background bonuses; exact overflow recovery;169-slot save/reload with last-slot item preserved; final Crafting row visible and usable; final checked log interval0errors. Test-only god buff forced CarryCapacity45 and was removed; plain-click placement test corrected to actual drag. Original test character restored; game closed. Multiplayer, elapsed recovery expiry and full station regression not claimed. Documentation/evidence: _Documentation/BackpackRows. No commits; Claude owns commits. Changed-files overlay: _Documentation/REBIRTH_20261002_BACKPACK_ROWS.zip.
''')
(d/'scope.txt').write_text('Changed-files overlay for current cumulative REBIRTH3.0 workspace on game3.2b10. Approved52base/13per-tier/Scavenger13 implemented and locally qualified. See README.txt and final_validation.json for scope and limitations. No commits.\n')
files=json.loads((d/'changed_sources.json').read_text())+['RebirthUtils.dll','RebirthUtils.pdb','Tools/GameBridge/tests/README.md','Tools/StorageGear/finalize_backpack_rows.py','_Documentation/Codex_Changes/CUMULATIVE_LOG.txt']
files += [p.relative_to(r).as_posix() for p in (r/'Tools/StorageGear').glob('test_backpack_rows*live.py')]
files += [p.relative_to(r).as_posix() for p in d.iterdir() if p.is_file() and p.name not in ('package_manifest.json',)]
files=sorted(set(files));assert all((r/p).is_file() for p in files)
manifest=[{'path':p,'sha256':hashlib.sha256((r/p).read_bytes()).hexdigest()} for p in files]
(d/'package_manifest.json').write_text(json.dumps(manifest,indent=2));files.append('_Documentation/BackpackRows/package_manifest.json')
p=r/'_Documentation/REBIRTH_20261002_BACKPACK_ROWS.zip'
with zipfile.ZipFile(p,'w',zipfile.ZIP_DEFLATED) as z:
 for f in files:z.write(r/f,f)
with zipfile.ZipFile(p) as z:
 assert z.testzip() is None
 for entry in manifest:assert hashlib.sha256(z.read(entry['path'])).hexdigest()==entry['sha256']
p.with_suffix('.zip.sha256').write_text(hashlib.sha256(p.read_bytes()).hexdigest()+'  '+p.name+'\n')
print('PASS package CRC and all file hashes:',len(files),'files',p)
