using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

// One retained original drives all effects. This runner never recreates a
// missing original from a current inventory image or an incoming paid image.
internal sealed class RebirthRemoteResourceSourceCoordinator:IDisposable
{
    private readonly EntityPlayer actor;
    private readonly string owner,creation,world,binding;
    private readonly Guid operation;
    private readonly int deaths;
    private readonly Dictionary<string,RebirthRemoteResourceSourceCustody.Lease> native=
        new Dictionary<string,RebirthRemoteResourceSourceCustody.Lease>(StringComparer.Ordinal);
    private bool closed;private bool? ownerEnvelopeSupported;
    private readonly RebirthRemoteResourceNetworkScope network;
    internal RebirthRemoteResourceSourceCoordinator(EntityPlayer actor,RebirthRemoteResourceDurableJournal.Record original)
    {
        this.actor=actor;owner=original.Owner;creation=original.Creation;world=original.World;
        operation=original.Operation;binding=original.Binding;deaths=(int)original.Copy().Attribute("deaths");
        network=RebirthRemoteResourceNetworkScope.Capture(actor);
    }
    private bool Current()=>!closed&&network?.Current==true&&ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&
        ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
        !actor.IsDead()&&actor.Died==deaths&&RebirthOriginalActionScope.Matches(actor,owner,creation)&&
        RemoteResourceWorldIdentity.TryGet(actor.world,out string actual)&&actual==world;
    private bool Read(out RebirthRemoteResourceDurableJournal.Record original)
    {
        original=null;return Current()&&RebirthRemoteResourceDurableJournal.TryRead(owner,operation,out original,out _)&&
            original!=null&&original.Binding==binding;
    }
    private bool Advance(RebirthRemoteResourceDurableJournal.Record original,Action<XElement> change,
        out RebirthRemoteResourceDurableJournal.Record next)=>RebirthRemoteResourceDurableJournal.TryAdvance(original,change,Current,out next);
    private static XElement Find(XElement root,string id)=>root.Element("sources").Elements("source").Single(source=>(string)source.Attribute("id")==id);
    private static bool Npc(XElement source)=>(int)source.Attribute("kind")==5;
    private bool Reserve(RebirthRemoteResourceDurableJournal.Record original)
    {
        // Capacity and every native hold are acquired before the first item
        // setter. A failed subset retains its original holds; it grants no pay.
        foreach(var source in original.Copy().Element("sources").Elements("source"))
        {
            string id=(string)source.Attribute("id");
            if(Npc(source))
            {
                if(!RebirthRemoteResourceDurableOutcome.TryReadNpcEffectPlan(actor,operation,id,out var plan)||
                    !RebirthNpcOriginalEffectAdmission.TryReserveRemoteResource(actor,operation,id,out var reservation)||!reservation.Current())return false;
                if(plan.DogStorage?!RebirthRemoteResourceDogTransfer.CanAdmit(actor,operation,id):
                    !RebirthNpcInventoryTransactionService.CanAdmitRemoteResourceOriginalPlan(actor,operation,id))return false;
            }
            else
            {
                if(!RebirthRemoteResourceSourceCustody.TryReserveRemoteResource(actor,operation,id,Current,out var lease))return false;
                native[id]=lease;
            }
        }
        return Current();
    }
    private bool SavedBefore(XElement source,out string proof)
    {
        proof=null;string id=(string)source.Attribute("id");
        if(!Npc(source))return native.TryGetValue(id,out var lease)&&lease.TryObserveSaved(0,out proof);
        return RebirthRemoteResourceDurableOutcome.TryReadNpcEffectPlan(actor,operation,id,out var plan)&&
            RebirthRemoteResourceDurableOutcome.TryObserveSavedNpc(plan,false,out proof);
    }
    private static void SaveNpc()
    {
        RebirthNpcInventoryPersistenceStore.Save();RebirthNpcAggregatePersistenceStore.Save();
    }
    private bool RunSource(RebirthRemoteResourceDurableJournal.Record original,XElement source)
    {
        string id=(string)source.Attribute("id");
        if((int)source.Attribute("published")==1)return true;
        if((int)source.Attribute("attempted")==0)
        {
            // The write-ahead attempt is durable before any native setter or
            // logical mutation. A thrown/unknown effect stays attempted.
            return Advance(original,root=>Find(root,id).SetAttributeValue("attempted",1),out _);
        }
        string proof;
        if(!Npc(source))
        {
            if(!native.TryGetValue(id,out var lease))return false;
            if(!lease.TryObserveSaved(1,out proof))
            {if(!lease.ApplyStage(1))return false;return lease.TryObserveSaved(1,out proof)&&Publish(original,id,proof);}
            return Publish(original,id,proof);
        }
        if(!RebirthRemoteResourceDurableOutcome.TryReadNpcEffectPlan(actor,operation,id,out var plan))return false;
        if(RebirthRemoteResourceDurableOutcome.TryObserveSavedNpc(plan,true,out proof))return Publish(original,id,proof);
        using(var authority=RebirthNpcInventoryAuthorityService.Issue("remote-resource-original",RebirthNpcInventoryAuthorityOperations.Mutate,TimeSpan.FromSeconds(5),plan.NpcId))
        {
            if(plan.DogStorage)
            {
                if(!RebirthRemoteResourceDogTransfer.TryApply(actor,operation,id,authority.AuthorityKey,out _,out _))return false;
            }
            else
            {
                // The named service rereads this actual original and stamps
                // its typed effect in the same canonical inventory mutation.
                RebirthNpcInventoryTransactionService.ApplyRemoteResourceOriginalPlan(actor,operation,id,authority.AuthorityKey,out _);
            }
        }
        SaveNpc();
        // A return value, current postimage or forced save is never payment.
        return RebirthRemoteResourceDurableOutcome.TryReadNpcEffectPlan(actor,operation,id,out plan)&&
            RebirthRemoteResourceDurableOutcome.TryObserveSavedNpc(plan,true,out proof)&&Publish(original,id,proof);
    }
    private bool Publish(RebirthRemoteResourceDurableJournal.Record original,string id,string proof)
    {
        bool published=Advance(original,root=>{var source=Find(root,id);source.SetAttributeValue("published",1);source.SetAttributeValue("proof",proof);},out _);
        if(published&&native.TryGetValue(id,out var lease))lease.ReleaseSavedDebitObservation(proof);
        return published;
    }

    // Explicit cancellation of source processing only; this is not owner
    // settlement. Unknown NPC effects cannot enter this restoration branch.
    internal bool RequestSourceAbort()
    {
        if(!Read(out var original)||original.Phase>1)return false;
        if(original.Phase==0)return Finish(original,false,"Original source admission was cancelled before effects.");
        var root=original.Copy();if(root.Element("sources").Elements("source").Any(source=>Npc(source)&&(int)source.Attribute("attempted")!=0))return false;
        if(((int?)root.Attribute("aborting")??0)==1)return true;
        return Advance(original,image=>image.SetAttributeValue("aborting",1),out _);
    }
    private bool Restore(RebirthRemoteResourceDurableJournal.Record original)
    {
        foreach(var source in original.Copy().Element("sources").Elements("source"))
        {
            if((int)source.Attribute("attempted")==0||((int?)source.Attribute("restored")??0)==1)continue;
            string id=(string)source.Attribute("id");
            if(Npc(source)||!native.TryGetValue(id,out var lease))return false;
            if(!lease.TryObserveSaved(3,out string proof))
            {if(!lease.TryRestoreOriginalBefore()||!lease.TryObserveSaved(3,out proof))return false;}
            return Advance(original,root=>{var item=Find(root,id);item.SetAttributeValue("restored",1);item.SetAttributeValue("restoreProof",proof);},out _);
        }
        return Finish(original,false,"Original source processing was cancelled before NPC effects.");
    }
    private bool Finish(RebirthRemoteResourceDurableJournal.Record original,bool success,string reason)
    {
        var paid=original.Copy().Element("sources").Elements("source")
            .Where(source=>(int)source.Attribute("published")==1&&((int?)source.Attribute("restored")??0)==0)
            .SelectMany(source=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element("paid").Value))).ToArray();
        return Advance(original,root=>{root.SetAttributeValue("phase",success?2:3);root.SetAttributeValue("outcome",success?1:2);
            root.Element("removed").Value=Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(paid));root.Element("reason").Value=reason;},out _);
    }
    internal bool Step(out RebirthRemoteResourceDurableJournal.Record original)
    {
        original=null;
        try
        {
            if(!Read(out original))return false;if(original.Phase>=2)return true;
            var sources=original.Copy().Element("sources").Elements("source").ToArray();
            if(sources.Length==0){Finish(original,false,original.Copy().Element("reason").Value);return Read(out original)&&original.Phase>=2;}
            bool fetchedStation=RebirthRemoteResourceFetchedStationOriginal.IsMappedRoot(original);
            bool placedStation=RebirthRemoteResourcePlacedStationOriginal.IsMappedRoot(original);
            if(fetchedStation&&placedStation)return false; // conflicting purpose evidence is unknown
            if(fetchedStation&&RebirthRemoteResourceFetchedStationOriginal.PrepareBeforeSources(actor,original)!=
                RebirthRemoteResourceFetchedStationOriginal.PreparationStatus.Ready)return false;
            // Validate the exact retained final payload before any source
            // effect. A valid immutable plan alone does not establish that
            // its future owner disposition fits its separate codec bounds.
            if(ownerEnvelopeSupported==null)
            {
                var plannedPaid=sources.SelectMany(source=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(source.Element("paid").Value))).ToArray();
                ownerEnvelopeSupported=RebirthCamperInventorySnapshot.Capture(plannedPaid).Length<=6*1024*1024&&
                    ((int)original.Copy().Attribute("kind")!=(int)RemoteResourceClientOperation.Craft||
                        (fetchedStation||placedStation?true:
                            RebirthRemoteResourceCraftProtocol.CanPublishPlannedOwnerAction(actor,original,plannedPaid)))&&
                    (!RebirthRemoteResourceBlockAdmission.IsMappedRoot(original)||RebirthRemoteResourceBlockAdmission.CanPublishPlannedOwnerAction(actor,original,plannedPaid));
            }
            if(ownerEnvelopeSupported!=true)
            {
                if(sources.All(source=>(int)source.Attribute("attempted")==0))Finish(original,false,"Original paid payload cannot be published by its bounded owner protocol.");
                return Read(out original)&&original.Phase>=2;
            }
            if(!Reserve(original))
            {
                // Phase0 authorizes no source effect at all. Persist a refusal
                // before releasing an authenticated zero-payment disposition;
                // never treat a phase1 reservation failure as no-effect proof.
                if(original.Phase==0)Finish(original,false,"Original source admission is no longer available.");
                return Read(out original)&&original.Phase>=2;
            }
            if(placedStation&&!RebirthRemoteResourcePlacedStationOriginal.HoldTargetBeforeSources(actor,original))return false;
            // Completed saved-before observers consume the globally bounded
            // snapshot request slots, not source custody. Keep their private
            // actual positive event while allowing the next source to save.
            foreach(var source in sources.Where(source=>!Npc(source)&&!string.IsNullOrEmpty((string)source.Attribute("beforeProof"))))
                if(native.TryGetValue((string)source.Attribute("id"),out var completed))completed.ReleaseSavedBeforeObservation((string)source.Attribute("beforeProof"));
            if(original.Phase==0)
            {
                foreach(var source in sources.Where(source=>string.IsNullOrEmpty((string)source.Attribute("beforeProof"))))
                {
                    if(!SavedBefore(source,out string proof))return false;string id=(string)source.Attribute("id");
                    if(Advance(original,root=>Find(root,id).SetAttributeValue("beforeProof",proof),out _)&&native.TryGetValue(id,out var justSavedLease))justSavedLease.ReleaseSavedBeforeObservation(proof);
                    return false;
                }
                Advance(original,root=>root.SetAttributeValue("phase",1),out _);return false;
            }
            if(placedStation&&RebirthRemoteResourcePlacedStationOriginal.PrepareBeforeSources(actor,original,native)!=
                RebirthRemoteResourcePlacedStationOriginal.PreparationStatus.Ready)return false;
            if(((int?)original.Copy().Attribute("aborting")??0)==1){Restore(original);return Read(out original)&&original.Phase>=2;}
            // Native publication may yield across frames; no NPC attempt can
            // happen until every native debit is positively saved.
            foreach(var source in sources.Where(source=>!Npc(source)&&(int)source.Attribute("published")==0))
            {RunSource(original,source);return false;}
            foreach(var source in sources.Where(source=>Npc(source)&&(int)source.Attribute("published")==0))
            {RunSource(original,source);return false;}
            Finish(original,true,"");return Read(out original)&&original.Phase>=2;
        }
        catch{return false;}
    }
    public void Dispose(){closed=true;foreach(var lease in native.Values)lease.Dispose();native.Clear();}
}
