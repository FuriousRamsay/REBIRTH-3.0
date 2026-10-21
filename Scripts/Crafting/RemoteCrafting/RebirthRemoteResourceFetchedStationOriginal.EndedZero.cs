using System;
using System.Linq;
using System.Text;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    internal static bool EndedClientEffectsAbsent(RebirthRemoteResourceClientOriginal.Intent original)
    {
        try{return IsCandidate(original)&&new[]{"RemoteResourceClientActions","RemoteResourceClientSources","FetchedStationNativePayloads"}.All(name=>AbsentArtifacts(name,original.Operation));}
        catch{return false;}
    }
    internal static bool AcknowledgeEndedZeroTerminal(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(!ReadEndedZeroTerminal(actor,original,out var root,out _))return false;
        string terminal=RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+root.Owner+"|"+root.Binding);
        if(root.Phase!=4&&!RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition","unused-no-payment");
            value.SetAttributeValue("ownerProof",terminal);},()=>ReadEndedZeroTerminal(actor,original,out var current,out _)&&current.Operation==root.Operation&&current.Binding==root.Binding,out _))return false;
        return ReadEndedZeroTerminal(actor,original,out var saved,out _)&&saved.Phase==4&&ReleaseNoPaymentStationHold(saved);
    }
    private static bool EndedZeroScope(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original)=>ThreadManager.IsMainThread()&&actor?.world?.IsRemote()==false&&
        actor.IsSpawned()&&!actor.IsDead()&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
        IsCandidate(original)&&actor.Died>original.Deaths&&actor.world.worldState.Guid==original.World&&RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)&&
        owner.CanonicalId==original.Owner&&RebirthOriginalActionScope.Matches(actor,owner.StorageKey,original.Creation);
    internal static bool ReadEndedZeroTerminal(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,out RebirthRemoteResourceDurableJournal.Record root,out string proof)
    {
        root=null;proof=null;
        try
        {
            if(!EndedZeroScope(actor,original)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||!RemoteResourceWorldIdentity.TryGet(actor.world,out string world))return false;
            Guid source=RebirthRemoteResourceDurableJournal.Operation(world,owner.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            if(!RebirthRemoteResourceDurableJournal.TryRead(owner.StorageKey,source,out var retained,out bool unknown)||unknown||retained==null||
                (retained.Phase!=3&&retained.Phase!=4)||(int)retained.Copy().Attribute("outcome")!=2||retained.Phase==4&&(string)retained.Copy().Attribute("disposition")!="unused-no-payment"||
                !NoSourceEffects(retained)||!TryReadAdmission(retained,out var admitted)||admitted.Digest!=original.Digest||!TryReadPlan(retained,out var plan)||
                !HasOnlyNeverEnteredNativeFiles(plan.Job,true))return false;
            if(retained.Phase==4&&(string)retained.Copy().Attribute("ownerProof")!=RebirthRemoteResourceDurableJournal.Digest("authenticated-no-payment|"+retained.Owner+"|"+retained.Binding))return false;
            var native=new RebirthCamperTransferJournal(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativeActorPayments")).Read(plan.Job);
            if(native?.State!=RebirthCamperTransferJournal.Phase.Rejected||native.Operation!=original.Operation||native.OwnerCreation!=original.Creation||
                !RebirthCamperStationAddress.TryParse(plan.Address,out var address)||native.Vehicle!=address.Vehicle||
                !RebirthCamperStationNativeActorPayment.AreCanonicalFetchedFrames(native.Before,native.After)||
                !RetainedFrameMatchesPlan(native.Before,plan,false)||!RetainedFrameMatchesPlan(native.After,plan,true))return false;
            proof=RebirthRemoteResourceDurableJournal.Digest("ended-fetched-zero|"+source.ToString("N")+"|"+retained.Binding+"|"+original.Digest+"|"+
                RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(native.Before))+"|"+RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(native.After)));
            root=retained;return true;
        }
        catch{root=null;proof=null;return false;}
    }
    // World metadata only. No new-life owner receipt, setters, refund,
    // source mutation, or action permission can originate from this method.
    internal static bool PrepareEndedZeroEffect(RebirthCamperStationState station,Guid job,byte[] before,byte[] after)
    {
        try
        {
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||station?.BoundVehicle==null||!ReferenceEquals(station.BoundVehicle.world,world)||
                !HasOnlyNeverEnteredNativeFiles(job,true)||!RebirthCamperStationNativeActorPayment.AreCanonicalFetchedFrames(before,after)||
                !RebirthCamperModuleRuntime.TryGetExisting(station.BoundVehicle,out var runtime)||runtime.TransferPending)return false;
            var installation=runtime.Stations.Find(station.InstallationId);if(installation?.InstalledItem==null||!ReferenceEquals(installation.State,station))return false;
            var frame=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(after),8*1024*1024);
            var model=RebirthCamperStationSnapshot.Restore(station.InstallationId,installation.InstalledItem,Convert.FromBase64String(frame.Element("stationImage").Value));
            int slot=(int)frame.Attribute("queueSlot");if(slot<0||slot>=model.Queue.Length)return false;
            var marker=model.Queue[slot]?.Recipe?.ingredients?.FirstOrDefault(item=>item?.itemValue?.HasMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey)==true)?.itemValue;
            string ownerKey=(string)frame.Attribute("ownerKey");
            if(marker==null||!marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string text)||!Guid.TryParseExact(text,"N",out var source)||
                !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey,out string binding)||
                !RebirthRemoteResourceDurableJournal.TryRead(ownerKey,source,out var root,out bool unknown)||unknown||root==null||root.Binding!=binding||!NoSourceEffects(root)||
                !TryReadPlan(root,out var plan)||plan.Job!=job||plan.World!=world.worldState.Guid||plan.InstalledDefinition!=RebirthNativeItemCodec.Encode(installation.InstalledItem.itemValue)||
                !TryReadAdmission(root,out var original)||original.Deaths!=plan.Deaths||original.Owner!=plan.CanonicalOwner||original.Creation!=plan.Creation||
                !RetainedFrameMatchesPlan(before,plan,false)||!RetainedFrameMatchesPlan(after,plan,true)||!SameFrozenStation(station,plan))return false;
            bool CurrentOwner()
            {
                var peers=SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.List;if(peers==null)return false;
                foreach(var peer in peers)
                {
                    var actor=world.GetEntity(peer.entityId) as EntityPlayer;
                    if(actor==null||!actor.IsSpawned()||actor.IsDead()||actor.Died<=plan.Deaths||!ReferenceEquals(actor.world,world)||
                        !RebirthCamperStationNativeActorPayment.RemoteIdentity(actor,out var owner,out var sender)||!ReferenceEquals(sender,peer)||
                        owner.StorageKey!=plan.OwnerKey||owner.CanonicalId!=plan.CanonicalOwner||!RebirthOriginalActionScope.Matches(actor,plan.OwnerKey,plan.Creation))continue;
                    return true;
                }
                return false;
            }
            if(!CurrentOwner())return false;
            if(root.Phase==3||root.Phase==4)return (int)root.Copy().Attribute("outcome")==2&&
                (root.Phase==3||(string)root.Copy().Attribute("disposition")=="unused-no-payment");
            if(root.Phase>1)return false;
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,body=>{body.SetAttributeValue("phase",3);body.SetAttributeValue("outcome",2);
                body.Element("reason").Value="Ended original life; exact never-entered station request has no source or owner effects.";},
                ()=>CurrentOwner()&&SameFrozenStation(station,plan)&&HasOnlyNeverEnteredNativeFiles(job,true),out _);
        }
        catch{return false;}
    }
}
