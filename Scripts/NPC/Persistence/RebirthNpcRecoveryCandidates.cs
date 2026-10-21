using System;
using System.Collections.Generic;
using System.Xml;

// Exact administration allow-list -> typed detached adapter. No reflection,
// generic Reset(false), native reconstruction or incremental record adoption.
internal static class RebirthNpcRecoveryCandidates
{
    internal sealed class Candidate
    {
        internal RebirthNpcPreparedRecovery Prepared;
        internal XmlDocument Publication;
        internal RebirthNpcStableId[] Affected;
        internal RebirthNpcInventoryPersistentRecord[] Inventories;
        internal RebirthNpcEquipmentPersistentRecord[] Equipment;
        internal RebirthNpcStableIdentityRecord[] Identities;
        internal RebirthNpcPersistentRecord[] Aggregates;
        internal RebirthNpcSettlementPersistentState Settlement;
        internal RebirthNpcWorkRecoveryCheckpoint[] Checkpoints;
        internal RebirthNpcWorkOutcomeRecord[] Outcomes;
        internal RebirthNpcPreparedRecovery[] Related=new RebirthNpcPreparedRecovery[0];
        internal RebirthNpcRecoveryRuntimeProjection Runtime;
        internal RebirthNpcPreparedRecovery ExecutionProjection;
    }
    internal static bool TryPrepare(string file,XmlDocument document,out Candidate candidate,out string error)
    {return TryPrepareCore(file,document,null,false,out candidate,out error);}
    // Caller first resolves every image through the installed-generation manifest
    // under its exact original writer lease. Never regenerate related names from
    // an initial backup over newer legitimate same-generation names/actions.
    internal static bool TryPrepareInstalled(string file,XmlDocument latest,XmlDocument latestWorldIntegration,out Candidate candidate,out string error)
    {return TryPrepareCore(file,latest,latestWorldIntegration,true,out candidate,out error);}
    private static bool TryPrepareCore(string file,XmlDocument document,XmlDocument installedWorld,bool installed,out Candidate candidate,out string error)
    {
        candidate=null;error=string.Empty;
        if(document?.DocumentElement==null){error="recovery document unavailable";return false;}
        var next=new Candidate();var ids=new HashSet<RebirthNpcStableId>();
        bool prepared;
        switch(file?.ToLowerInvariant())
        {
            case "rebirthnpcstableidentities.xml":
                prepared=RebirthNpcStableIdentityStore.TryPrepareRecovery(document,out next.Prepared,out next.Identities,out error);
                if(prepared)foreach(var row in next.Identities)ids.Add(row.StableId);
                break;
            case "rebirth_npc_inventory.xml":
                prepared=RebirthNpcInventoryPersistenceStore.TryPrepareRecovery(document,out next.Prepared,out next.Inventories,out error);
                if(prepared)
                {
                    foreach(var row in next.Inventories)ids.Add(row.NpcId);
                    foreach(var row in RebirthNpcInventoryTransactionService.CapturePersistentRecords())ids.Add(row.NpcId);
                    var audit=RebirthNpcItemIntegrityService.AuditRecords(next.Inventories,RebirthNpcEquipmentService.CapturePersistentRecords());
                    if(!audit.IsValid){error="recovered inventory conflicts with current equipment/reservations: "+audit.LastError;return false;}
                }
                break;
            case "rebirth_npc_equipment.xml":
                prepared=RebirthNpcEquipmentPersistenceStore.TryPrepareRecovery(document,out next.Prepared,out next.Equipment,out error);
                if(prepared)
                {
                    foreach(var row in next.Equipment)ids.Add(row.NpcId);
                    foreach(var row in RebirthNpcEquipmentService.CapturePersistentRecords())ids.Add(row.NpcId);
                    var audit=RebirthNpcItemIntegrityService.AuditRecords(RebirthNpcInventoryTransactionService.CapturePersistentRecords(),next.Equipment);
                    if(!audit.IsValid){error="recovered equipment conflicts with current inventory/reservations: "+audit.LastError;return false;}
                }
                break;
            case "rebirthnpcexecutionstate.xml":
                prepared=RebirthNpcExecutionPersistenceStore.TryPrepareRecovery(document,out next.Prepared,out var executionIds,out error);
                if(prepared)
                {
                    foreach(var id in executionIds)ids.Add(id);
                    if(!RebirthNpcExecutionLeaseRegistry.TryPrepareRecoveryExecution(RebirthNpcExecutionPersistenceStore.ReadRecoveryRecords(document),out next.ExecutionProjection,out error))return false;
                }
                break;
            case "rebirth_npc_settlement.xml":
                prepared=RebirthNpcSettlementPersistenceStore.TryPrepareRecovery(document,out next.Prepared,out next.Settlement,out error);
                if(prepared)
                {
                    foreach(var row in next.Settlement.Needs)ids.Add(row.NpcId);
                    foreach(var row in next.Settlement.Professions)ids.Add(row.NpcId);
                    foreach(var row in next.Settlement.WorkOrders)if(!row.ClaimedBy.IsEmpty)ids.Add(row.ClaimedBy);
                }
                break;
            case "rebirthnpcworkcheckpoints.xml":
                prepared=RebirthNpcWorkCheckpointPersistenceStore.TryPrepareRecovery(document,out next.Prepared,out next.Checkpoints,out error);
                // Executor/target fields are not universally stable NPC IDs.
                // Whole runtime set below is checked instead of guessing identity.
                break;
            case "rebirthnpcworkoutcomes.xml":
                prepared=RebirthNpcWorkOutcomePersistenceStore.TryPrepareRecovery(document,out next.Prepared,out next.Outcomes,out error);
                if(prepared)foreach(var row in next.Outcomes)ids.Add(row.NpcId);
                break;
            case "rebirthnpcsocialstate.xml":
                prepared=RebirthNpcSocialPersistenceStore.TryPrepareRecovery(document,out next.Prepared,out var socialIds,out error);
                if(prepared)foreach(var id in socialIds)ids.Add(id);
                break;
            case "rebirthnpccombatstate.xml":
                prepared=RebirthNpcCombatPersistenceStore.TryPrepareRecovery(document,out next.Prepared,out var combatIds,out error);
                if(prepared)foreach(var id in combatIds)ids.Add(id);
                break;
            case "rebirthnpcpersistentrecords.xml":
                prepared=RebirthNpcAggregatePersistenceStore.TryPrepareRecovery(document,out next.Prepared,out next.Aggregates,out error);
                if(prepared)
                {
                    foreach(var row in next.Aggregates)ids.Add(row.Identity.StableNpcId);
                    RebirthNpcPreparedRecovery names;
                    if(installed)
                    {
                        if(installedWorld==null){error="installed aggregate recovery needs positively latest related world image";return false;}
                        if(!RebirthNpcWorldIntegrationService.TryPrepareInstalledRecoveryDocument(installedWorld,out names,out error))return false;
                    }
                    else if(!RebirthNpcWorldIntegrationService.TryPrepareRecoveryNames(next.Aggregates,out names,out error))return false;
                    next.Related=new[]{names};
                    if(installed)
                    {if(!RebirthNpcRecoveryRuntimeProjection.TryPrepareInstalled(next.Aggregates,out next.Runtime,out error))return false;}
                    else if(!RebirthNpcRecoveryRuntimeProjection.TryPrepare(next.Aggregates,out next.Runtime,out error))return false;
                }
                break;
            default:error="no typed recovery adapter for this file";return false;
        }
        if(!prepared)return false;
        // Removed identities and live references also need custody/projection
        // validation. Never restrict preflight to rows present in the backup.
        foreach(var state in RebirthNpcRuntimeRegistry.GetSnapshot())ids.Add(state.StableId);
        ids.Remove(default(RebirthNpcStableId));
        next.Affected=new RebirthNpcStableId[ids.Count];ids.CopyTo(next.Affected);
        Array.Sort(next.Affected,(a,b)=>string.CompareOrdinal(a.ToString(),b.ToString()));
        next.Publication=next.Prepared.PublicationDocument??(XmlDocument)document.CloneNode(true);
        candidate=next;return true;
    }
}
