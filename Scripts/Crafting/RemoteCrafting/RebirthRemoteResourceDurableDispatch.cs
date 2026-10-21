using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;

// Retained query/execute dispatcher. Transport must authenticate the original
// native connection before entering; the dispatcher independently scopes actor,
// character, world, death and immutable requirements on every admission/retry.
internal static partial class RebirthRemoteResourceDurableDispatch
{
    private sealed class Pending:IDisposable
    {
        internal EntityPlayer Actor;internal string Owner,Creation;internal Guid Operation;internal int Deaths;
        internal RemoteResourceClientOperation Kind;
        internal RebirthRemoteResourceAdmission Admission;
        internal RebirthRemoteResourceSourceCoordinator Coordinator;
        internal RebirthRemoteResourceNetworkScope Network;
        public void Dispose(){Admission?.Dispose();Coordinator?.Dispose();}
    }
    private static readonly Dictionary<Guid,Pending> pending=new Dictionary<Guid,Pending>();
    private static World world;private static bool installed;private static int offset;
    private static readonly Queue<Tuple<string,Guid>> observations=new Queue<Tuple<string,Guid>>();
    internal static void Install()
    {
        if(installed)return;RebirthRemoteResourceSourceCustody.Install();
        ModEvents.GameUpdate.RegisterHandler(Update);installed=true;
    }
    private static bool Scope(EntityPlayer actor,string owner,string creation)=>ThreadManager.IsMainThread()&&
        actor?.world?.IsRemote()==false&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&!actor.IsDead()&&
        ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&RebirthOriginalActionScope.Matches(actor,owner,creation);
    private static bool CurrentPending(Pending entry)=>entry!=null&&entry.Network?.Current==true&&Scope(entry.Actor,entry.Owner,entry.Creation)&&entry.Actor.Died==entry.Deaths;
    private static void Bind(World current)
    {
        if(ReferenceEquals(world,current))return;
        foreach(var entry in pending.Values)entry.Dispose();pending.Clear();observations.Clear();RebirthRemoteResourceFragments.ClearTransient();world=current;offset=0;
    }
    internal static bool Resolve(EntityPlayer actor,ulong epoch,ulong request,RemoteResourceClientOperation kind,
        IList<ItemStack> requirements,bool queryOnly,out RebirthRemoteResourceDurableJournal.Record original)
    {
        original=null;
        try
        {
            if(!installed||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||
                !RemoteResourceWorldIdentity.TryGet(actor.world,out string worldId))return false;
            string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);
            if(!Scope(actor,identity.StorageKey,creation))return false;Bind(actor.world);
            Guid operation=RebirthRemoteResourceDurableJournal.Operation(worldId,identity.StorageKey,creation,epoch,request,kind);
            if(!observations.Any(value=>value.Item1==identity.StorageKey&&value.Item2==operation))
            {observations.Enqueue(Tuple.Create(identity.StorageKey,operation));while(observations.Count>128)observations.Dequeue();}
            if(!RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,operation,out original,out bool unknown)||unknown)return false;
            string image=null;
            if(!queryOnly)
            {
                if(requirements==null||requirements.Count<1||requirements.Count>4096||requirements.Any(item=>item==null||item.IsEmpty()||item.count<1))return false;
                using(var hash=SHA256.Create())image=BitConverter.ToString(hash.ComputeHash(RebirthCamperInventorySnapshot.Capture(requirements.ToArray()))).Replace("-","").ToLowerInvariant();
            }
            if(original!=null)
            {
                if(original.World!=worldId||original.Creation!=creation||(int)original.Copy().Attribute("deaths")!=actor.Died||
                    !queryOnly&&(string)original.Copy().Attribute("requirementsHash")!=image)return false;
                if(original.Phase>=2)return true;
            }
            else if(queryOnly)return false;
            if(pending.TryGetValue(operation,out var retained))
            {
                // Reconnect may bind only the same original owner/character;
                // the new coordinator rechecks every native endpoint itself.
                if(!ReferenceEquals(retained.Actor,actor)||!CurrentPending(retained))
                {retained.Dispose();pending.Remove(operation);}
                else return false;
            }
            if(pending.Count>=128)return false;
            if(original==null&&(!RemoteResourcesRuntimePolicy.Enabled||kind==RemoteResourceClientOperation.CookingPull&&!RebirthSurvivorMode.IsEnabledForCurrentWorld()||
                !RebirthRemoteResourceSourceCustody.CanAdmitNewOriginal()))return false;
            var entry=new Pending{Actor=actor,Owner=identity.StorageKey,Creation=creation,Operation=operation,Kind=kind,Deaths=actor.Died,
                Network=RebirthRemoteResourceNetworkScope.Capture(actor)};
            if(entry.Network==null)return false;
            if(original==null)entry.Admission=new RebirthRemoteResourceAdmission(actor,entry.Owner,creation,worldId,epoch,request,kind,requirements.ToArray());
            else entry.Coordinator=new RebirthRemoteResourceSourceCoordinator(actor,original);
            pending.Add(operation,entry);return false;
        }
        catch{original=null;return false;}
    }
    internal static JObject CaptureAuditState(EntityPlayer actor,Guid selected=default)
    {
        var result=new JObject{["available"]=false,["installed"]=installed};
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||
            !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity))return result;
        var rows=new JArray();
        var selectedObservations=selected==Guid.Empty?observations.Where(value=>value.Item1==identity.StorageKey).ToArray():new[]{Tuple.Create(identity.StorageKey,selected)};
        foreach(var seen in selectedObservations)
        {
            if(!RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,seen.Item2,out var original,out bool unknown)||unknown)
            {rows.Add(new JObject{["operation"]=seen.Item2.ToString("N"),["unknown"]=true});continue;}
            if(original==null)continue;var root=original.Copy();var sources=new JArray();
            foreach(var source in root.Element("sources").Elements("source"))
            {
                var row=new JObject{["id"]=(string)source.Attribute("id"),["kind"]=(int)source.Attribute("kind"),["attempted"]=(int)source.Attribute("attempted"),
                    ["published"]=(int)source.Attribute("published"),["restored"]=(int?)source.Attribute("restored")??0,
                    ["beforeProof"]=(string)source.Attribute("beforeProof"),["proof"]=(string)source.Attribute("proof"),
                    ["beforeFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(source.Element("before").Value),
                    ["afterFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(source.Element("after").Value),
                    ["paidFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(source.Element("paid").Value)};
                if(!((int)source.Attribute("kind")==5)&&RebirthRemoteResourceNativeBinding.TryResolve(actor.world,(string)source.Attribute("id"),
                    (RemoteResourceSourceKind)(int)source.Attribute("kind"),(string)source.Attribute("incarnation"),out var binding))
                    row["currentNativeFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(binding.CopySlots())));
                sources.Add(row);
            }
            var refund=root.Element("ownerRefund")?.Elements().SingleOrDefault();
            rows.Add(new JObject{["operation"]=original.Operation.ToString("N"),["kind"]=(int)root.Attribute("kind"),["phase"]=original.Phase,["revision"]=original.Revision,["binding"]=original.Binding,
                ["outcome"]=(int)root.Attribute("outcome"),["disposition"]=(string)root.Attribute("disposition")??"",["aborting"]=(int?)root.Attribute("aborting")??0,
                ["removedFingerprint"]=RebirthRemoteResourceDurableJournal.Digest(root.Element("removed").Value),
                ["ownerActionChild"]=(string)root.Element("ownerAction")?.Attribute("child")??"",
                ["ownerActionDomain"]=(string)root.Element("ownerAction")?.Attribute("domain")??"",
                ["ownerRejecting"]=(int?)root.Attribute("ownerRejecting")??0,
                ["ownerNoUseProof"]=(string)root.Attribute("ownerNoUseProof")??"",
                ["ownerAfterFingerprint"]=OwnerAfterFingerprint(root),
                ["craftContinuationProof"]=RebirthRemoteResourceOwnerAction.ReadCraftContinuationAuditProof(original),
                ["blockOriginal"]=RebirthRemoteResourceBlockHost.CaptureAuditState(actor.world,original),
                ["fetchedStationOriginal"]=RebirthRemoteResourceFetchedStationOriginal.CaptureAuditState(actor.world,original),
                ["ownerRefundChild"]=(string)refund?.Attribute("child")??"",
                ["ownerRefundDomain"]=refund?.Name.LocalName??"",
                ["originalDeaths"]=(int)root.Attribute("deaths"),
                ["refundDeaths"]=(int?)refund?.Attribute("deaths")??(int)root.Attribute("deaths"),
                ["refundAfterFingerprint"]=refund?.Element("afterActor")==null?"":RebirthRemoteResourceDurableJournal.Digest(refund.Element("afterActor").Value),
                ["nativeSourceCount"]=root.Element("sources").Elements("source").Count(source=>source.Element("npc")==null),
                ["nativeBeforeProofCount"]=root.Element("sources").Elements("source").Count(source=>source.Element("npc")==null&&!string.IsNullOrEmpty((string)source.Attribute("beforeProof"))),
                ["sourceSnapshotLimit"]=RebirthStationSnapshotEvidence.MaximumRequests,
                ["activeSourceSnapshotWatches"]=RebirthRemoteResourceNativePublication.ActiveSnapshotWatchCount,
                ["ownerProof"]=(string)root.Attribute("ownerProof")??"",["sources"]=sources});
        }
        result["available"]=true;result["originals"]=rows;return result;
    }
    private static string OwnerAfterFingerprint(System.Xml.Linq.XElement root)
    {
        try
        {
            var action=root.Element("ownerAction");var after=action?.Element("afterActor");
            if(after==null&&new[]{"native-craft","native-block"}.Contains((string)action?.Attribute("domain")))
                after=RebirthCookingRemoteAdmission.ReadProtocolXml(action.Element("original").Value,24*1024*1024).Element("afterActor");
            return after==null?"":RebirthRemoteResourceDurableJournal.Digest(after.Value);
        }
        catch{return "";}
    }
    private static void Update(ref ModEvents.SGameUpdateData data)
    {
        var current=GameManager.Instance?.World;Bind(current);if(current?.IsRemote()!=false||pending.Count==0)return;
        var values=pending.Values.ToArray();int maximum=Math.Min(16,values.Length);
        for(int index=0;index<maximum;index++)
        {
            var entry=values[(offset+index)%values.Length];
            try
            {
                if((entry.Kind==RemoteResourceClientOperation.BlockRepair||entry.Kind==RemoteResourceClientOperation.BlockUpgrade)&&RebirthRemoteResourceBlockAdmission.TryReadMappedOriginal(entry.Operation,out var refusedBlock)&&
                    refusedBlock.Creation==entry.Creation&&RebirthStablePlayerIdentity.ComputeStorageKey(refusedBlock.Owner)==entry.Owner&&
                    RebirthRemoteResourcePreAdmissionRefusal.HasSavedServerTerminal(current,refusedBlock))
                {entry.Dispose();pending.Remove(entry.Operation);continue;}
                if(!CurrentPending(entry)){entry.Dispose();pending.Remove(entry.Operation);continue;}
                if(entry.Admission!=null)
                {
                    if(!entry.Admission.Step(out var original))continue;
                    entry.Admission.Dispose();entry.Admission=null;entry.Coordinator=new RebirthRemoteResourceSourceCoordinator(entry.Actor,original);
                }
                if(entry.Coordinator.Step(out var result)&&result.Phase>=2){entry.Dispose();pending.Remove(entry.Operation);}
            }
            catch{/* The immutable on-disk original remains the only authority. */}
        }
        offset=(offset+maximum)%Math.Max(1,values.Length);
    }
}
