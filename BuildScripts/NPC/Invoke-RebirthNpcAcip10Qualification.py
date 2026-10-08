#!/usr/bin/env python3
from pathlib import Path
import argparse, json, sys

def main():
 ap=argparse.ArgumentParser();ap.add_argument('--project-root',required=True);ap.add_argument('--json-out');a=ap.parse_args();r=Path(a.project_root)
 files={
  'replication':r/'Scripts/Rebirth/NPC/Replication/RebirthNpcReplicationPerformanceIntegration.cs',
  'progression':r/'Scripts/Rebirth/NPC/Progression/RebirthNpcProgressionReplication.cs',
  'social':r/'Scripts/Rebirth/NPC/Social/RebirthNpcSocialService.cs',
  'lifecycle':r/'Scripts/Rebirth/NPC/Foundation/RebirthNpcLifecycle.cs',
  'admin':r/'Scripts/Rebirth/NPC/Administration/RebirthNpcAdministrationSupport.cs'}
 text={k:p.read_text(encoding='utf-8') if p.exists() else '' for k,p in files.items()}
 checks={
 'interest_tiers':all(x in text['replication'] for x in ['OwnerCritical','Interacting','VisibleNear','ActiveNear','Background','OutOfInterest']),
 'ownership_interaction_visibility_distance_activity':all(x in text['replication'] for x in ['IsOwner','IsInteracting','IsVisible','Distance','IsActive']),
 'frequency_policy':'FrequencyHz' in text['replication'],'payload_budget':'PayloadBudgetBytes' in text['replication'] and 'ConsumeBudget' in text['replication'],
 'baseline_recovery':'BaselineRecoveries' in text['replication'],'delta_fallback':'AllowDelta' in text['replication'],
 'out_of_interest_suppression':'OutOfInterestSuppressed' in text['replication'],
 'network_batcher':'RebirthNpcNetworkBatcher.Queue' in text['replication'],'production_replication_caller':'RebirthNpcReplicationPerformanceService.QueueReplication' in text['progression'],
 'explicit_exemption':'lowFrequencyExemption' in text['replication'] and 'ExplicitExemptions' in text['replication'],
 'visual_revision_guard':'sample.Revision<=t.Current.Revision' in text['replication'],'interpolation':'Lerp(t.Previous,t.Current,a)' in text['replication'],
 'bounded_prediction':'Math.Min(0.25f' in text['replication'],'authority_guard':'RejectAuthoritativeMutationFromPrediction' in text['replication'],
 'correction_counter':'Corrections' in text['replication'],'relationship_batcher':'RebirthNpcRelationshipBatcher.Queue' in text['replication'],
 'relationship_order':'RelationshipSequences' in text['replication'],'relationship_replay':'RelationshipReplayIds' in text['replication'],
 'production_social_caller':'RebirthNpcReplicationPerformanceService.QueueRelationship' in text['social'],
 'lifecycle_registration':'RebirthNpcReplicationPerformanceService.EnsureInitialized' in text['lifecycle'] and 'RebirthNpcReplicationPerformanceService.ResetForWorldChange' in text['lifecycle'],
 'admin':all(x in text['admin'] for x in ['replicationperformance','replicationperformancequalify']),
 'counters':all(x in text['replication'] for x in ['BudgetDeferred','StaleDropped','Corrections','BudgetComplianceFailures','RelationshipDeferred'])}
 report={'package':'ACIP-10','result':'PASS' if all(checks.values()) else 'FAIL','checks':checks,'passed':sum(checks.values()),'total':len(checks)}
 print('[REBIRTH NPC ACIP-10 Qualification] result=%s checks=%d/%d'%(report['result'],report['passed'],report['total']))
 if a.json_out:Path(a.json_out).write_text(json.dumps(report,indent=2),encoding='utf-8')
 return 0 if report['result']=='PASS' else 2
if __name__=='__main__':sys.exit(main())
