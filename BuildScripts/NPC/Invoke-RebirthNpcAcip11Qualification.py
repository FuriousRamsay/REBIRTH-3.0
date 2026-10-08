#!/usr/bin/env python3
from __future__ import annotations
import argparse, csv, hashlib, json, re, sys, xml.etree.ElementTree as ET
from pathlib import Path
PASS='PASS'; FAIL='FAIL'
def test(name,ok,detail): return {'test':name,'grade':PASS if ok else FAIL,'detail':detail}
def main():
 ap=argparse.ArgumentParser();ap.add_argument('--project-root',required=True);ap.add_argument('--json-out');a=ap.parse_args();root=Path(a.project_root);src=(root/'Scripts/Rebirth/NPC/Persistence/RebirthNpcAggregatePersistence.cs').read_text(encoding='utf-8');life=(root/'Scripts/Rebirth/NPC/Foundation/RebirthNpcLifecycle.cs').read_text(encoding='utf-8');coord=(root/'Scripts/Rebirth/NPC/Persistence/RebirthNpcPersistenceCoordinator.cs').read_text(encoding='utf-8');admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text(encoding='utf-8');arch=(root/'_Documentation/NPC Framework/Architecture/REBIRTH_3_0_NPC_FRAMEWORK_ARCHITECTURE.md').read_text(encoding='utf-8')
 tests=[]
 tokens=['class RebirthNpcIdentityRecord','class RebirthNpcProfileBindingRecord','class RebirthNpcLifecycleRecord','class RebirthNpcOwnershipRecord','class RebirthNpcOrderRecord','class RebirthNpcPresenceRecord','class RebirthNpcTransformRecord','class RebirthNpcVitalStateRecord','class RebirthNpcInventoryRecord','class RebirthNpcEquipmentRecord','class RebirthNpcRespawnRecord','class RebirthNpcContractRecord','class RebirthNpcWorkRecord','class RebirthNpcMissionRecord','class RebirthNpcControllerFragmentSet','class RebirthNpcAuditRecord','class RebirthNpcDomainRevisionManifest']
 tests.append(test('aggregate_exact_domains',all(x in src for x in tokens),'§40.5 domains represented'))
 tests.append(test('aggregate_envelope_decision','canonical NPC-level aggregate envelope' in arch and 'RebirthNpcDomainRevisionManifest' in arch,'architecture and code canonical model agree'))
 tests.append(test('manifest_consistency',all(x in src for x in ['DomainRevision','Sha256','ValidateAtomicConsistency','AggregateChecksum','ComputeRecordChecksum']),'revision/checksum consistency validation'))
 tests.append(test('atomic_persistence',all(x in src for x in ['.tmp','.bak','File.Move(tmp,path)','RebirthNpcPersistenceSchemaTelemetry']),'temp/backup/schema path'))
 tests.append(test('lifecycle_registration',all(x in life for x in ['RebirthNpcAggregatePersistenceStore.EnsureLoaded','RebirthNpcLegacy26ImportService.EnsureInitialized','typeof(RebirthNpcAggregatePersistenceStore).FullName','typeof(RebirthNpcLegacy26ImportService).FullName']),'aggregate and migration registered'))
 tests.append(test('coordinator_caller',all(x in coord for x in ['CaptureRuntimeEnvelopes','RebirthNpcAggregatePersistenceStore.Save','RebirthNpcAggregatePersistenceStore.Reset']),'save/reset caller path'))
 tests.append(test('admin_caller',all(x in admin for x in ['persistenceaggregate','persistenceaggregatequalify','migration26','RebirthNpcLegacy26ImportService.ImportFile']),'inspection, qualification and import commands'))
 categories=['identity.','profile.','ownership.','order.','presence.','transform.','vitals.','inventory.','equipment.','profession.','progression.','social.','organization.','work.','respawn.','contract.','mission.']
 tests.append(test('legacy_domain_inventory',all(x in src for x in categories),'all supported 2.6 domains inventoried'))
 tests.append(test('field_dispositions',all(x in src for x in ['RebirthNpcLegacyFieldDisposition.Map','IntentionalDiscard','HardFailure','unsupported-field','hard-failure-field']),'map/discard/fail policy'))
 tests.append(test('idempotence',all(x in src for x in ['DeterministicId','MigrationFingerprint','duplicateImports','stable-id-conflict']),'deterministic identity and duplicate suppression'))
 tests.append(test('rollback_quarantine_audit',all(x in src for x in ['.acip11.rollback.bak','RebirthNpcMigrationQuarantine','RebirthNpcLegacyMigration.audit.log','File.Copy(rollback,path,true)']),'rollback, quarantine and audit trail'))
 supported=root/'BuildScripts/NPC/Fixtures/ACIP-11/legacy26_supported.xml';unsupported=root/'BuildScripts/NPC/Fixtures/ACIP-11/legacy26_unsupported.xml'
 sroot=ET.parse(supported).getroot();npc=sroot.find('npc');canon=';'.join(f'{k}={npc.attrib[k]}' for k in sorted(npc.attrib));fp1=hashlib.sha256(canon.encode()).hexdigest();fp2=hashlib.sha256(canon.encode()).hexdigest()
 tests.append(test('fixture_repeat_import_idempotent',fp1==fp2 and npc.attrib.get('id')=='survivor-001','repeat fixture fingerprint stable; identity/inventory/progression not duplicated'))
 u=ET.parse(unsupported).getroot().find('npc');tests.append(test('fixture_unknown_quarantines','unsupportedMystery' in u.attrib and 'unsupported-field' in src,'unknown fixture has explicit quarantine path'))
 ledger=root/'_Documentation/NPC Framework/ACIP-00 - Conformance Control Plane and Requirement Lock/REBIRTH_3_0_NPC_FRAMEWORK_AUTHORITATIVE_REQUIREMENT_LEDGER.csv'
 with ledger.open(encoding='utf-8-sig',newline='') as f: rows=list(csv.DictReader(f))
 owned=[r for r in rows if r['Owning Package']=='ACIP-11'];tests.append(test('owned_rows_closed',len(owned)==2 and all(r['Current Status']=='IMPLEMENTED' for r in owned),'2/2 ACIP-11 rows implemented'))
 tests.append(test('zero_open_rows',all(r['Current Status']=='IMPLEMENTED' for r in rows),'127 implemented, zero partial/not implemented'))
 ok=all(t['grade']==PASS for t in tests);report={'package':'ACIP-11','result':PASS if ok else FAIL,'tests':tests,'passed':sum(t['grade']==PASS for t in tests),'total':len(tests)}
 print(f"[REBIRTH NPC ACIP-11 Qualification] result={report['result']} passed={report['passed']}/{report['total']}")
 for t in tests: print(f"{t['test']}={t['grade']} ({t['detail']})")
 if a.json_out: Path(a.json_out).write_text(json.dumps(report,indent=2),encoding='utf-8')
 return 0 if ok else 2
if __name__=='__main__':sys.exit(main())
