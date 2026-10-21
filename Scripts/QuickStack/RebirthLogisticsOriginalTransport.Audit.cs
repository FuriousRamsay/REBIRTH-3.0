using System;
using System.Linq;
using Newtonsoft.Json.Linq;

// Explicit read-only bridge projection. Never creates an original, invokes a
// native effect, adopts a body, saves player data or advances a phase.
internal static partial class RebirthLogisticsOriginalTransport
{
    internal static JObject CaptureAuditState(EntityPlayer actor,Guid selected=default)
    {
        var result=new JObject{["available"]=false,["active"]=Active};
        var actual=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||actual==null||actor==null||!ReferenceEquals(actor.world,actual)||
            !ReferenceEquals(actual.GetEntity(actor.entityId),actor)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return result;
        result["available"]=true;result["owner"]=identity.StorageKey;result["remote"]=actual.IsRemote();
        result["creation"]=RebirthCamperCharacterAccess.CurrentCreation(actor);result["deaths"]=actor.Died;result["entity"]=actor.entityId;
        result["world"]=RebirthLogisticsTransferJournal.WorldKey(actual);
        if(RebirthLogisticsOwnerReceipt.TryOperation(actor.Buffs,out var nativeOperation,out int nativePhase))
        {result["nativeOperation"]=nativeOperation.ToString("N");result["nativePhase"]=nativePhase;result["nativeHeld"]=actor.Buffs.GetCustomVar("rbLogisticsOwnerHeld");}
        result["held"]=actor is EntityPlayerLocal local&&RebirthLogisticsOwnerReceipt.HasPendingOriginal(local);
        if(selected==Guid.Empty)
        {
            if(actual.IsRemote())selected=nativeOperation;
            else if(RebirthLogisticsOwnerReceipt.TrySavedOperation(identity,out var savedOperation,out _,out _,out _))selected=savedOperation;
        }
        if(selected==Guid.Empty)return result;
        result["selected"]=selected.ToString("N");
        RebirthLogisticsTransferJournal.Record original=null;
        if(actual.IsRemote())
        {
            if(ReferenceEquals(clientWorld,actual)&&latest?.Operation==selected&&latest.Owner==identity.StorageKey)original=latest;
        }
        else RebirthLogisticsTransferJournal.TryRead(identity.StorageKey,selected,out original);
        if(original==null){result["originalAvailable"]=false;return result;}
        result["originalAvailable"]=true;result["operation"]=original.Operation.ToString("N");
        result["binding"]=RebirthLogisticsTransferJournal.ImmutablePlanDigest(original);result["phase"]=original.Phase;
        result["outcome"]=original.Outcome;result["rejecting"]=original.Rejecting;result["originalDeaths"]=original.Deaths;
        result["ownerDebitProof"]=original.OwnerDebitProof;result["ownerNoEffectProof"]=original.OwnerNoEffectProof;
        // Canonical journal is already bounded by its production 4 MiB codec.
        // The driver compares the same immutable full source/owner plan across
        // genuine UI, reconnect and cold observations, not caller snapshots.
        result["journal"]=RebirthLogisticsTransferJournal.Encode(original);
        result["nativeLegs"]=new JArray(original.Legs.Select(leg=>new JObject{["id"]=leg.Id,["attempted"]=leg.Attempted,["published"]=leg.Published,["restored"]=leg.Restored}));
        result["npcLegs"]=new JArray(original.Npcs.Select(leg=>new JObject{["id"]=leg.StableId,["attempted"]=leg.Attempted,["published"]=leg.Published}));
        if(!actual.IsRemote()&&RebirthLogisticsOwnerReceipt.TrySavedOperation(identity,out var savedOriginal,out int savedPhase,out float savedHeld,out var saved))
        {result["savedOperation"]=savedOriginal.ToString("N");result["savedPhase"]=savedPhase;result["savedHeld"]=savedHeld;result["savedDeaths"]=saved.deaths;result["savedEntity"]=saved.id;}
        if(!actual.IsRemote()&&RebirthLogisticsDeathJournal.TryRead(original,out var physical))
        {
            result["physical"]=new JObject{["state"]=physical.State,["binding"]=physical.Binding,["outcome"]=physical.Outcome,
                ["beforeProof"]=physical.BeforeProof,["finalProof"]=physical.FinalProof,["ownerFileProof"]=physical.OwnerFileProof,
                ["parcels"]=new JArray(physical.Parcels.Select(parcel=>new JObject{["incarnation"]=parcel.Incarnation.ToString("N"),["state"]=parcel.State,["beforeProof"]=parcel.BeforeProof,["finalProof"]=parcel.FinalProof})),
                ["frozenPlanValid"]=RebirthLogisticsDeathPartition.ValidateFrozenPlan(original,physical),
                ["nativeBeforeOnly"]=RebirthLogisticsParcelReferenceJournal.IsNativeBeforeOnly(original,physical)};
        }
        return result;
    }
}
