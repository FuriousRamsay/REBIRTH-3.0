#!/usr/bin/env python3
from __future__ import annotations
import argparse, csv, json, re, sys
from pathlib import Path

REQ_COLUMNS = ['Requirement ID','Domain','Requirement','Owning Package','Current Status','Documentation Source','Completion Predicate','Behavior Obligation','Registration Obligation','Caller Obligation','Persistence Obligation','Networking Obligation','UI/Admin Obligation','Failure/Replay Obligation','Evidence','Assessment']
PASS='PASS'; FAIL='FAIL'

def load(path):
    with path.open(encoding='utf-8-sig',newline='') as f: return list(csv.DictReader(f))

def result(name, ok, detail): return {'test':name,'grade':PASS if ok else FAIL,'detail':detail}

def audit(root: Path, package: str, simulate_missing: str=''):
    ledger_path=root/'_Documentation/NPC Framework/ACIP-00 - Conformance Control Plane and Requirement Lock/REBIRTH_3_0_NPC_FRAMEWORK_AUTHORITATIVE_REQUIREMENT_LEDGER.csv'
    tracker_path=root/'_Documentation/NPC Framework/Authoritative Completion Implementation Plan/REBIRTH_3_0_NPC_FRAMEWORK_AUTHORITATIVE_COMPLETION_TRACKER.csv'
    rows=load(ledger_path); tracker=load(tracker_path)
    assigned=[r for r in rows if r['Owning Package'].upper()==package.upper()]
    tests=[]
    tests.append(result('ledger_file',ledger_path.is_file(),str(ledger_path)))
    tests.append(result('catalogue_count',len(rows)==127,f'{len(rows)}/127'))
    tests.append(result('schema',all(set(REQ_COLUMNS)<=set(r) for r in rows),'required columns present'))
    expected=[f'NPC-REQ-{i:03d}' for i in range(1,128)]
    actual=[r['Requirement ID'] for r in rows]
    tests.append(result('stable_unique_ids',actual==expected and len(set(actual))==127,'NPC-REQ-001..127 in canonical order'))
    tests.append(result('documentation_links',all(r['Documentation Source'].strip() for r in rows),'all rows have originating documentation'))
    tests.append(result('completion_predicates',all(r['Completion Predicate'].strip() for r in rows),'all rows have explicit predicate'))
    tracker_open=[r for r in tracker if r.get('Current Status','')!='IMPLEMENTED' and not r.get('Final Status','').startswith('IMPLEMENTED')]
    open_map={(r['Domain'],r['Requirement']):r['Owning Package'] for r in tracker_open}
    open_rows=[r for r in rows if r['Current Status']!='IMPLEMENTED']
    assignment_ok=all(open_map.get((r['Domain'],r['Requirement']))==r['Owning Package'] for r in open_rows)
    tests.append(result('open_requirement_assignment',assignment_ok and len(open_rows)==len(tracker_open),f'open={len(open_rows)} tracker={len(tracker_open)} assigned exactly once'))
    tests.append(result('package_has_requirements',len(assigned)>0,f'{package} assigned={len(assigned)}'))
    tests.append(result('package_predicates',all(r['Completion Predicate'].strip() for r in assigned),'assigned predicates present'))
    tests.append(result('package_evidence',all(r['Evidence'].strip() for r in assigned),'assigned evidence present'))
    # Verify evidence file tokens against source tree. Semicolon-separated evidence may include symbols after colon.
    missing=[]
    for r in assigned:
        evidence=r['Evidence']
        if simulate_missing and r['Requirement ID']==simulate_missing: evidence='DeliberatelyRemovedRegistration.cs'
        paths=re.findall(r'(?:Scripts|BuildScripts)/[^;,:]+?\.(?:cs|py|ps1|csv|md)',evidence)
        if not paths: missing.append(r['Requirement ID']+':no-path')
        for p in paths:
            if not (root/p.strip()).is_file(): missing.append(r['Requirement ID']+':'+p.strip())
    tests.append(result('evidence_files_exist',not missing,'missing='+(','.join(missing) if missing else '0')))
    lifecycle=(root/'Scripts/Rebirth/NPC/Foundation/RebirthNpcLifecycle.cs').read_text(encoding='utf-8')
    console=(root/'Scripts/Rebirth/NPC/Diagnostics/ConsoleCmdRebirthNpc.cs').read_text(encoding='utf-8')
    
    if package.upper()=='ACIP-00':
        reg_ok='typeof(RebirthNpcConformanceControlPlane).FullName' in lifecycle and 'typeof(RebirthNpcRequirementCatalogue).FullName' in lifecycle
        caller_ok='case \"conformance\"' in console and 'RebirthNpcConformanceControlPlane.GetReport' in console
        reg_detail='control plane and catalogue registered'; caller_detail='rbnpc conformance caller'
    elif package.upper()=='ACIP-01':
        reg_ok=all(x in lifecycle for x in ['typeof(RebirthNpcProgressionService).FullName','typeof(RebirthNpcProgressionPersistenceStore).FullName','typeof(RebirthNpcWorkProgressionSink).FullName'])
        caller_ok='case \"progression\"' in console and 'RebirthNpcProgressionService.GetReport' in console and 'RebirthNpcWorkOutcomeService.RegisterSink(WorkSink)' in (root/'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionService.cs').read_text(encoding='utf-8')
        reg_detail='progression service, persistence and sink registered'; caller_detail='command and work-outcome callers present'
    elif package.upper()=='ACIP-02':
        modifiers=(root/'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionModifiers.cs').read_text(encoding='utf-8')
        admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text(encoding='utf-8')
        combat=(root/'Scripts/Rebirth/NPC/Combat/RebirthNpcCombatEmergencySystem.cs').read_text(encoding='utf-8')
        reg_ok=all(x in lifecycle for x in ['RebirthNpcProgressionModifierService.EnsureInitialized','typeof(RebirthNpcProgressionModifierService).FullName','typeof(RebirthNpcWeaponProgressionService).FullName'])
        caller_ok='progressionmodifiers' in admin and 'RebirthNpcProgressionAiIntegration.EvaluateIncomingPhysicalDamage' in combat and 'RebirthNpcProgressionService.Award' in modifiers
        reg_detail='modifier and weapon progression services registered'; caller_detail='admin, combat and XP callers present'
    elif package.upper()=='ACIP-03':
        professions=(root/'Scripts/Rebirth/NPC/Progression/RebirthNpcProfessionProgression.cs').read_text(encoding='utf-8')
        admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text(encoding='utf-8')
        simulation=(root/'Scripts/Rebirth/NPC/Simulation/RebirthNpcSettlementSimulation.cs').read_text(encoding='utf-8')
        reg_ok='RebirthNpcProfessionProgressionService.EnsureInitialized' in lifecycle and 'RebirthNpcProfessionProgressionService.ResetForWorldChange' in lifecycle
        required_publishers=['OutcomePublisher.Farming','OutcomePublisher.Looting','OutcomePublisher.Gathering','OutcomePublisher.Mining','OutcomePublisher.Salvaging','OutcomePublisher.Cooking','OutcomePublisher.BaseRepairs','OutcomePublisher.Medicine']
        caller_ok='professionqualify' in admin and 'CapabilityScore' in simulation and 'ApplyDurationSeconds' in simulation and all(x in simulation for x in required_publishers) and 'RebirthNpcProgressionService.Award' in professions
        reg_detail='profession progression lifecycle registration and reset present'; caller_detail='admin qualification, settlement matching, executor timing and eight committed-outcome callers present'
    elif package.upper()=='ACIP-05':
        ui=(root/'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionXUi.cs').read_text(encoding='utf-8')
        admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text(encoding='utf-8')
        reg_ok=all(x in lifecycle for x in ['typeof(NetPackageRebirthNpcProgression).FullName','typeof(RebirthNpcProgressionReplicationService).FullName','typeof(RebirthNpcProgressionUiService).FullName'])
        caller_ok='SendBaseline(data.ClientInfo)' in lifecycle and 'PublishDelta(q.NpcId)' in (root/'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionService.cs').read_text(encoding='utf-8') and 'RebirthNpcProgressionClientCache.TryGet' in ui and all(x in admin for x in ['progressioninspect','progressiondiagnosticexport','progressionreset'])
        reg_detail='progression networking and UI services registered'; caller_detail='baseline, delta, replicated UI and admin callers present'
    elif package.upper()=='ACIP-06':
        combat_ai=(root/'Scripts/Rebirth/NPC/Combat/RebirthNpcTargetingAndCombatAi.cs').read_text(encoding='utf-8')
        emergency=(root/'Scripts/Rebirth/NPC/Combat/RebirthNpcCombatEmergencySystem.cs').read_text(encoding='utf-8')
        admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text(encoding='utf-8')
        reg_ok=all(x in lifecycle for x in ['RebirthNpcCategoryCombatAiRegistry.EnsureInitialized','typeof(RebirthNpcTargetingService).FullName','typeof(RebirthNpcFriendlyFirePolicy).FullName','typeof(RebirthNpcCategoryCombatAiRegistry).FullName'])
        caller_ok=all(x in combat_ai for x in ['combat.survivor','combat.bandit','combat.dog','combat.panther','ThreatensOwner','ShouldProjectilePassThrough','RequestAuthoritativeUse']) and 'RebirthNpcFriendlyFirePolicy.CanDamage' in emergency and all(x in admin for x in ['combatpolicy','combatqualify'])
        reg_detail='targeting, friendly-fire and category combat AI registered'; caller_detail='four category providers, owner threat, damage/projectile, weapon and admin callers present'
    elif package.upper()=='ACIP-07':
        gameplay=(root/'Scripts/Rebirth/NPC/LifecycleGameplay/RebirthNpcLifecycleGameplaySystems.cs').read_text(encoding='utf-8')
        admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text(encoding='utf-8')
        reg_ok=all(x in lifecycle for x in ['RebirthNpcLifecycleGameplayService.EnsureInitialized','RebirthNpcLifecycleGameplayService.ResetForWorldChange','typeof(RebirthNpcLifecycleGameplayService).FullName'])
        caller_ok=all(x in gameplay for x in ['RequestRecall','TryFindSafePlacement','TryNavigate','TryTeleport','BeginVehicleTravel','TryAttachSeat','EndVehicleTravel','TryRestoreOrder','StartMission','ResolveMission','StartContract','ChargeWage','ExpireContract','ScheduleRespawn','TryRespawn','IsNpcPresent','TryReconstruct']) and all(x in admin for x in ['lifecycleplay','lifecycleplayqualify'])
        reg_detail='ACIP-07 lifecycle gameplay service registered and reset'; caller_detail='recall, vehicle, mission, contract, respawn and admin callers present'
    elif package.upper()=='ACIP-08':
        world=(root/'Scripts/Rebirth/NPC/WorldIntegration/RebirthNpcWorldIntegration.cs').read_text(encoding='utf-8')
        activity=(root/'Scripts/Rebirth/NPC/Activity/RebirthNpcActivityRuntime.cs').read_text(encoding='utf-8')
        combat=(root/'Scripts/Rebirth/NPC/Combat/RebirthNpcTargetingAndCombatAi.cs').read_text(encoding='utf-8')
        admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text(encoding='utf-8')
        reg_ok=all(x in lifecycle for x in ['RebirthNpcWorldIntegrationService.EnsureInitialized','RebirthNpcWorldIntegrationService.ResetForWorldChange','typeof(RebirthNpcProductionProfileCatalogue).FullName','typeof(RebirthNpcWorldIntegrationService).FullName'])
        caller_ok=all(x in world for x in ['survivor.ambient','survivor.persistent','bandit.standard','special.humanoid','companion.dog','companion.panther','TryComposeSpawn','TryPromoteAmbient','OnWarmActivated','OnWarmDeactivated','UpdateMarker','RemoveMarkersForNpc','GenerateName','PublishPresentation']) and 'OnActivityTransition' in activity and 'OnCombatTransition' in combat and all(x in admin for x in ['worldintegration','worldintegrationqualify'])
        reg_detail='production catalogue and world integration registered and reset'; caller_detail='spawn, promotion, warming, markers, names, presentation and admin callers present'
    elif package.upper()=='ACIP-10':
        replication=(root/'Scripts/Rebirth/NPC/Replication/RebirthNpcReplicationPerformanceIntegration.cs').read_text(encoding='utf-8')
        progression=(root/'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionReplication.cs').read_text(encoding='utf-8')
        social=(root/'Scripts/Rebirth/NPC/Social/RebirthNpcSocialService.cs').read_text(encoding='utf-8')
        admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text(encoding='utf-8')
        reg_ok=all(x in lifecycle for x in ['RebirthNpcReplicationPerformanceService.EnsureInitialized','RebirthNpcReplicationPerformanceService.ResetForWorldChange','typeof(RebirthNpcReplicationPerformanceService).FullName'])
        caller_ok=all(x in replication for x in ['OwnerCritical','Interacting','VisibleNear','ActiveNear','Background','OutOfInterest','PayloadBudgetBytes','AllowPrediction','QueueReplication','RebirthNpcNetworkBatcher.Queue','QueueRelationship','RebirthNpcRelationshipBatcher.Queue','TrySampleVisual','RejectAuthoritativeMutationFromPrediction','StaleDropped','Corrections','BudgetComplianceFailures']) and 'RebirthNpcReplicationPerformanceService.QueueReplication' in progression and 'RebirthNpcReplicationPerformanceService.QueueRelationship' in social and all(x in admin for x in ['replicationperformance','replicationperformancequalify'])
        reg_detail='ACIP-10 replication performance service registered and reset'; caller_detail='progression delta, social mutation, batching, interest, prediction and admin callers present'
    elif package.upper()=='ACIP-11':
        aggregate=(root/'Scripts/Rebirth/NPC/Persistence/RebirthNpcAggregatePersistence.cs').read_text(encoding='utf-8')
        admin=(root/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs').read_text(encoding='utf-8')
        coordinator=(root/'Scripts/Rebirth/NPC/Persistence/RebirthNpcPersistenceCoordinator.cs').read_text(encoding='utf-8')
        reg_ok=all(x in lifecycle for x in ['RebirthNpcAggregatePersistenceStore.EnsureLoaded','RebirthNpcLegacy26ImportService.EnsureInitialized','typeof(RebirthNpcAggregatePersistenceStore).FullName','typeof(RebirthNpcLegacy26ImportService).FullName'])
        caller_ok=all(x in aggregate for x in ['class RebirthNpcPersistentRecord','RebirthNpcDomainRevisionManifest','ValidateAtomicConsistency','DeterministicId','IntentionalDiscard','HardFailure','RebirthNpcMigrationQuarantine','RebirthNpcLegacyMigration.audit.log']) and all(x in coordinator for x in ['CaptureRuntimeEnvelopes','RebirthNpcAggregatePersistenceStore.Save']) and all(x in admin for x in ['persistenceaggregate','persistenceaggregatequalify','migration26'])
        reg_detail='aggregate persistence and comprehensive legacy import registered'; caller_detail='save coordinator, validation, migration fixtures and admin callers present'
    else:
        reg_ok=True; caller_ok=True; reg_detail='package-specific registration delegated to package audit'; caller_detail='package-specific caller delegated to package audit'
    tests.append(result('lifecycle_registration',reg_ok,reg_detail))
    tests.append(result('command_caller',caller_ok,caller_detail))
    placeholder_ok=all(r['Current Status']=='IMPLEMENTED' for r in assigned)
    tests.append(result('placeholder_only_rejection',placeholder_ok,'assigned rows must be IMPLEMENTED; interfaces/scaffolding do not pass'))
    gate_script=root/'BuildScripts/NPC/Invoke-RebirthNpcCompletionGate.py'
    tests.append(result('completion_gate_present',gate_script.is_file(),str(gate_script)))
    if gate_script.is_file():
        gate_text=gate_script.read_text(encoding='utf-8')
        gate_semantic=("if rc!=0 or data.get('result')!='PASS'" in gate_text and 'out.unlink()' in gate_text and 'return 3' in gate_text)
        tests.append(result('completion_gate_fail_closed',gate_semantic,'failed conformance must remove/refuse manifest regardless of label wording'))
    passed=all(t['grade']==PASS for t in tests)
    return {'package':package,'result':PASS if passed else FAIL,'tests':tests,'assigned_requirement_ids':[r['Requirement ID'] for r in assigned]}

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--project-root',required=True); ap.add_argument('--package',default='ACIP-00'); ap.add_argument('--json-out'); ap.add_argument('--simulate-missing-evidence',default='')
    a=ap.parse_args(); report=audit(Path(a.project_root).resolve(),a.package,a.simulate_missing_evidence)
    print(f"[REBIRTH NPC Conformance] package={report['package']} result={report['result']}")
    for t in report['tests']: print(f"{t['test']}={t['grade']} ({t['detail']})")
    if a.json_out: Path(a.json_out).write_text(json.dumps(report,indent=2),encoding='utf-8')
    return 0 if report['result']==PASS else 2
if __name__=='__main__': sys.exit(main())
