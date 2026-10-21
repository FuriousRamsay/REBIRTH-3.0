using System;
using System.Linq;
using System.Xml.Linq;
using System.Globalization;

// Block-purpose owed payload only. A declined actual native attempt retains
// its tool wear; only exact removed materials are returned, without source
// rollback. Never-permitted originals owe only their remote paid fragments.
internal static partial class RebirthRemoteResourceBlockRefund
{
    private static Guid ChildFor(RebirthRemoteResourceClientOriginal.Intent original,int deaths)=>RebirthCookingRemoteAdmissionBackend.StationKey(original.World,original.Operation.ToString("N")+"|native-block-owed-refund"+(deaths==original.Deaths?"":"|adopted:"+deaths.ToString(CultureInfo.InvariantCulture)));
    internal static Guid Child(RebirthRemoteResourceClientOriginal.Intent original)=>ChildFor(original,DeliveryDeaths(original));
    internal static int DeliveryDeaths(RebirthRemoteResourceClientOriginal.Intent original)
        =>ClientRead("RemoteBlockRefundChildren",original,out var request,out _)?(int?)request.Attribute("deaths")??original.Deaths:original.Deaths;
    internal static bool Current(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(RebirthRemoteResourceClientOriginal.Current(player,original))return LiveBeltPolicy(player,original);
        try{return RebirthRemoteResourceBlockAdoption.QueryScope(player,original)&&
            RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var source,out var binding,out _)&&
            HasClientOwed(original,source,binding,out _,out _)&&
            (!ClientRead("RemoteBlockRefundChildren",original,out var request,out _)||(int?)request.Attribute("deaths")==player.Died)&&LiveBeltPolicy(player,original);}
        catch{return false;}
    }
    internal static bool Owed(RebirthRemoteResourceClientOriginal.Intent original,RebirthRemoteResourceDurableJournal.Record root,out ItemStack[] payload,out string noUse)
    {
        payload=null;noUse=null;
        try
        {
            if(original==null||root==null||!RebirthRemoteResourceBlockAdmission.TryReadOriginal(original.Operation,out var retained)||retained.Digest!=original.Digest||
                root.Owner!=RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner)||root.Creation!=original.Creation||
                root.Operation!=RebirthRemoteResourceDurableJournal.Operation(root.World,root.Owner,original.Creation,original.Epoch,original.Request,original.Kind)||
                root.Phase!=2&&root.Phase!=3&&root.Phase!=4)return false;
            var image=root.Copy();
            bool partial=(root.Phase==3||root.Phase==4)&&(int?)image.Attribute("outcome")==2&&image.Element("ownerAction")==null&&
                image.Element("sources").Elements("source").All(source=>(int)source.Attribute("attempted")==0||(int)source.Attribute("published")==1&&((int?)source.Attribute("restored")??0)==0);
            if((int?)image.Attribute("deaths")!=original.Deaths||(int?)image.Attribute("kind")!=(int)original.Kind||((int?)image.Attribute("outcome")!=1&&!partial)||
                (int?)image.Attribute("ownerRejecting")!=1||root.Phase==4&&(string)image.Attribute("disposition")!="refunded")return false;
            var remote=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(image.Element("removed").Value));
            if(image.Element("ownerAction")==null)
            {
                if(!RebirthRemoteResourceBlockRejection.ValidateUnissuedRootProof(image)||!RebirthRemoteResourceDurableJournal.NeverPreparedOwnerAction(root)||
                    !RebirthRemoteResourceBlockCommand.CanIssueOwnerPermission(original))return false;
                payload=remote.Select(stack=>stack.Clone()).ToArray();
            }
            else
            {
                if(RebirthRemoteResourceBlockRejection.ValidateIssuedRootProof(image))payload=remote.Select(stack=>stack.Clone()).ToArray();
                else
                {
                    if((!RebirthRemoteResourceBlockRejection.ValidateDeclinedRootProof(image)&&!RebirthRemoteResourceBlockAdoption.ValidateEndedOwnerRoot(image))||!RebirthRemoteResourceBlockSettlement.CanonicalAction(original,root,out var action))return false;
                    var local=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(action.Element("localRemoved").Value));payload=local.Concat(remote).Select(stack=>stack.Clone()).ToArray();
                }
            }
            if(payload.Length<1||payload.Length>8192||payload.Any(stack=>stack==null||stack.IsEmpty()||stack.count<1)||
                RebirthCamperInventorySnapshot.Capture(payload).Length>12*1024*1024)return false;
            noUse=(string)image.Attribute("ownerNoUseProof");return true;
        }
        catch{payload=null;noUse=null;return false;}
    }
    internal static bool Plan(RebirthCamperEnergyActorImage before,ItemStack[] payload,out RebirthCamperEnergyActorImage after)
    {
        after=null;
        if(before?.BagLocks==null||before.BagLocks.Length!=before.Bag.Length||payload==null||payload.Length<1||
            !RebirthRecyclerOwnerDelivery.Plan(before.Bag,before.Belt,before.BagLocks,new bool[before.Belt.Length],0,payload,out var bag,out var belt))return false;
        after=before.Copy();after.Bag=bag;after.Belt=belt;return true;
    }
    internal static XElement Body(RebirthRemoteResourceClientOriginal.Intent original,Guid source,string binding,string noUse,RebirthCamperEnergyActorImage before,RebirthCamperEnergyActorImage after,int deliveryDeaths=-1,XElement beltPolicy=null)
    {
        if(deliveryDeaths<0)deliveryDeaths=original.Deaths;
        var body=new XElement("blockRefund",new XAttribute("original",original.Digest),new XAttribute("source",source.ToString("N")),new XAttribute("binding",binding),
            new XAttribute("noUseProof",noUse),new XAttribute("child",ChildFor(original,deliveryDeaths).ToString("N")),
            new XElement("beforeActor",RebirthRemoteResourceClientOriginal.ActorImage(before)),new XElement("afterActor",RebirthRemoteResourceClientOriginal.ActorImage(after)));
        if(deliveryDeaths!=original.Deaths)body.SetAttributeValue("deaths",deliveryDeaths);if(beltPolicy!=null)body.Add(new XElement(beltPolicy));return body;
    }
    internal static string TerminalProof(string binding,string noUse,XElement request)=>RebirthRemoteResourceDurableJournal.Digest("native-block-refund|"+binding+"|"+noUse+"|"+RebirthRemoteResourceDurableJournal.Digest(request.ToString(SaveOptions.DisableFormatting)));
    internal static bool HasSavedTerminal(World world,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||world.worldState.Guid!=original.World||
                !RemoteResourceWorldIdentity.TryGet(world,out var sourceWorld))return false;
            string owner=RebirthStablePlayerIdentity.ComputeStorageKey(original.Owner);Guid source=RebirthRemoteResourceDurableJournal.Operation(sourceWorld,owner,original.Creation,original.Epoch,original.Request,original.Kind);
            if(!RebirthRemoteResourceDurableJournal.TryRead(owner,source,out var root,out bool unknown)||unknown||root?.Phase!=4||!Owed(original,root,out var payload,out var noUse))return false;
            var request=root.Copy().Element("ownerRefund")?.Element("blockRefund");if(request==null)return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);
            return Plan(before,payload,request.Element("beltPolicy"),out var planned)&&after.Matches(planned)&&XNode.DeepEquals(request,Body(original,source,root.Binding,noUse,before,after,(int?)request.Attribute("deaths")??original.Deaths,request.Element("beltPolicy")))&&
                (string)root.Copy().Attribute("ownerProof")==TerminalProof(root.Binding,noUse,request);
        }
        catch{return false;}
    }
    internal static bool Host(EntityPlayer actor,RebirthRemoteResourceClientOriginal.Intent original,XElement request,bool complete,out bool permission)
    {
        permission=false;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||!actor.IsSpawned()||actor.Died<original.Deaths||actor.world.worldState.Guid!=original.World||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                !RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var identity)||identity.CanonicalId!=original.Owner||!RebirthOriginalActionScope.Matches(actor,identity.StorageKey,original.Creation)||
                !RemoteResourceWorldIdentity.TryGet(actor.world,out var world))return false;
            Guid source=RebirthRemoteResourceDurableJournal.Operation(world,identity.StorageKey,original.Creation,original.Epoch,original.Request,original.Kind);
            if(!RebirthRemoteResourceDurableJournal.TryRead(identity.StorageKey,source,out var root,out bool unknown)||unknown||!Owed(original,root,out var payload,out var noUse))return false;
            int deliveryDeaths=(int?)request.Attribute("deaths")??original.Deaths,currentDeaths=actor.Died;
            if(deliveryDeaths<original.Deaths||deliveryDeaths>currentDeaths||currentDeaths>original.Deaths&&
                !RebirthRemoteResourceBlockAdoption.HasSavedAdmission(actor,original,root))return false;
            var before=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("beforeActor").Value);var after=RebirthRemoteResourceClientOriginal.ReadActor(request.Element("afterActor").Value);
            if(!Plan(before,payload,request.Element("beltPolicy"),out var planned)||!after.Matches(planned)||!XNode.DeepEquals(request,Body(original,source,root.Binding,noUse,before,after,deliveryDeaths,request.Element("beltPolicy"))))return false;
            string proof=TerminalProof(root.Binding,noUse,request);var image=root.Copy();
            if(root.Phase==4)return (string)image.Attribute("ownerProof")==proof&&XNode.DeepEquals(image.Element("ownerRefund")?.Element("blockRefund"),request);
            if(!RebirthCamperEnergySavedOwner.TryRead(identity,out var actual,out var saved)||saved.id!=actor.entityId||saved.deaths!=currentDeaths||
                !RebirthCookingRemoteAdmissionBackend.NativePhase(saved,RebirthRemoteResourceOwnerAction.Receipt(ChildFor(original,deliveryDeaths)),out float phase,out bool present)||!present)return false;
            bool Current()=>!actor.IsDead()&&actor.Died==currentDeaths&&ReferenceEquals(GameManager.Instance?.World,actor.world)&&ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)&&
                actor.world.worldState.Guid==original.World&&RebirthOriginalActionScope.Matches(actor,identity.StorageKey,original.Creation);
            var existing=image.Element("ownerRefund");
            if(existing==null)
            {
                if(complete||deliveryDeaths!=currentDeaths||phase!=1f||!before.Matches(actual)||!SavedBeltPolicy(actor,saved,request.Element("beltPolicy")))return false;
                string receipt=RebirthRemoteResourceDurableJournal.Digest(request.ToString(SaveOptions.DisableFormatting)+"|native-block-refund-before|"+RebirthRemoteResourceClientOriginal.ActorImage(actual));
                var wrapper=new XElement("ownerRefund",new XAttribute("nativeBeforeProof",receipt),new XElement(request));
                if(!RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>value.Add(new XElement(wrapper)),Current,out var admitted))return false;
                permission=(admitted.Phase==2||admitted.Phase==3)&&XNode.DeepEquals(admitted.Copy().Element("ownerRefund"),wrapper);return permission;
            }
            if(!XNode.DeepEquals(existing.Element("blockRefund"),request))return false;
            if(!complete){permission=deliveryDeaths==currentDeaths&&phase==1f&&before.Matches(actual)&&SavedBeltPolicy(actor,saved,request.Element("beltPolicy"));return permission;}
            if(phase!=2f||deliveryDeaths==currentDeaths&&!after.Matches(actual))return false;
            return RebirthRemoteResourceDurableJournal.TryAdvance(root,value=>{value.SetAttributeValue("phase",4);value.SetAttributeValue("disposition","refunded");value.SetAttributeValue("ownerProof",proof);},Current,out var terminal)&&terminal.Phase==4;
        }
        catch{permission=false;return false;}
    }
}
