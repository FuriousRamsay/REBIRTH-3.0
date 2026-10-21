using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

// Detached removal-purpose plan. Only an actual applied CamperTransfers
// removal and its exact native saved owner carrier can create this plan.
// Reading/planning never grants inventory, spawns a parcel or retires custody.
internal sealed class RebirthCamperRemovalParcelPlan
{
    internal Guid Operation,Removal,Vehicle,Installation;
    internal string World,Owner,Creation,Source;
    internal int Entity,Deaths,CarrierSlot;
    internal RebirthCamperEnergyActorImage Before,Credited,Retired;
    internal ItemStack[] Contents,Overflow;
    internal ItemStack Carrier;
    internal string Binding=>RebirthStationPublicationRecord.Digest(Bytes(Write()));
    internal static byte[] Bytes(XElement value)=>new UTF8Encoding(false,true).GetBytes(value.ToString(SaveOptions.DisableFormatting));
    private static string Frame(ItemStack[] items)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(items));
    internal static Guid Key(string world,string source,string owner,string creation)=>RebirthCookingRemoteAdmissionBackend.StationKey(world,
        "camper.removal.parcel.v1|"+source+"|"+owner+"|"+creation);
    internal string Receipt=>"rbRemovalParcel_"+Operation.ToString("N");
    internal XElement Write()=>new XElement("removalParcelPlan",new XAttribute("version",1),
        new XAttribute("operation",Operation.ToString("N")),new XAttribute("removal",Removal.ToString("N")),
        new XAttribute("vehicle",Vehicle.ToString("N")),new XAttribute("installation",Installation.ToString("N")),
        new XAttribute("world",World),new XAttribute("owner",Owner),new XAttribute("creation",Creation),
        new XAttribute("source",Source),new XAttribute("entity",Entity),new XAttribute("deaths",Deaths),new XAttribute("carrierSlot",CarrierSlot),
        new XElement("carrier",Frame(new[]{Carrier})),new XElement("contents",Frame(Contents)),new XElement("overflow",Frame(Overflow)),
        new XElement("beforeActor",RebirthRemoteResourceClientOriginal.ActorImage(Before)),
        new XElement("creditedActor",RebirthRemoteResourceClientOriginal.ActorImage(Credited)),
        new XElement("retiredActor",RebirthRemoteResourceClientOriginal.ActorImage(Retired)));
    internal static bool TryCreate(EntityPlayer actor,RebirthStablePlayerIdentity identity,string creation,
        RebirthCamperTransferJournal.Record removal,RebirthCamperEnergyActorImage savedOwner,out RebirthCamperRemovalParcelPlan plan)
    {
        plan=null;
        try
        {
            if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||identity==null||savedOwner==null||
                removal?.State!=RebirthCamperTransferJournal.Phase.Applied||string.IsNullOrEmpty(creation)||
                !RebirthOriginalActionScope.Matches(actor,identity.StorageKey,creation)||
                !ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||
                actor.IsDead()||!actor.IsSpawned())return false;
            string expectedOwner=actor is EntityPlayerLocal?creation:"remote:"+identity.CanonicalId+":"+creation;
            if(removal.OwnerCreation!=expectedOwner)return false;
            var before=RebirthCamperTransferSnapshot.Restore(removal.Vehicle,removal.Before);
            var after=RebirthCamperTransferSnapshot.Restore(removal.Vehicle,removal.After);
            if(!RebirthCamperPortableStation.TryGetSourceAddress(after.Cursor,out var address)||address.Vehicle!=removal.Vehicle||
                after.Stations.Find(address.Installation)!=null||!RebirthCamperStationRemovalContents.TryRead(after.Cursor,out var contents)||
                contents.Length<1||contents.Length>byte.MaxValue)return false;
            var station=before.Stations.Find(address.Installation);if(station==null)return false;
            var prepared=RebirthCamperStationRemovalContents.Prepare(address.Vehicle,station);
            if(!RebirthStationGridIngredients.IsSameStackSnapshot(prepared.Carrier,after.Cursor)||
                !RebirthCamperEnergyItems.Same(prepared.Contents,contents))return false;
            int location=-2,matches=0;
            if(RebirthStationGridIngredients.IsSameStackSnapshot(savedOwner.Cursor,after.Cursor)){location=-1;matches++;}
            for(int slot=0;slot<savedOwner.Bag.Length;slot++)if(RebirthStationGridIngredients.IsSameStackSnapshot(savedOwner.Bag[slot],after.Cursor)){location=slot;matches++;}
            // The actual saved envelope must have one unambiguous owner slot.
            // No caller-provided source location or same-shaped station wins.
            if(matches!=1||savedOwner.BagLocks?.Length!=savedOwner.Bag.Length)return false;
            var value=new RebirthCamperRemovalParcelPlan{Removal=removal.Operation,Vehicle=removal.Vehicle,Installation=address.Installation,
                World=actor.world.worldState.Guid,Owner=identity.StorageKey,Creation=creation,Entity=actor.entityId,Deaths=actor.Died,
                CarrierSlot=location,Before=savedOwner.Copy(),Carrier=after.Cursor.Clone(),Contents=ItemStack.Clone(contents),
                Source=RebirthStationPublicationRecord.Digest(removal.Before)+RebirthStationPublicationRecord.Digest(removal.After)};
            value.Operation=Key(value.World,value.Source,value.Owner,creation);
            if(!value.Build(out value.Credited,out value.Retired,out value.Overflow)||!RebirthCamperRemovalParcelOverflow.CanFit(actor,value.Overflow)||Bytes(value.Write()).Length>24*1024*1024-8192)return false;
            plan=value;return true;
        }
        catch{plan=null;return false;}
    }
    private bool Build(out RebirthCamperEnergyActorImage credited,out RebirthCamperEnergyActorImage retired,out ItemStack[] overflow)
    {
        credited=retired=null;overflow=null;
        if(Before?.BagLocks?.Length!=Before.Bag.Length||CarrierSlot< -1||CarrierSlot>=Before.Bag.Length||
            !RebirthStationGridIngredients.IsSameStackSnapshot(CarrierSlot<0?Before.Cursor:Before.Bag[CarrierSlot],Carrier))return false;
        var locks=(bool[])Before.BagLocks.Clone();if(CarrierSlot>=0)locks[CarrierSlot]=true;
        if(!RebirthRecyclerOwnerDelivery.PlanWithOverflow(Before.Bag,Before.Belt,locks,new bool[Before.Belt.Length],0,Contents,
            out var bag,out var belt,out overflow))return false;
        credited=Before.Copy();credited.Bag=bag;credited.Belt=belt;
        retired=credited.Copy();var stripped=RebirthCamperStationRemovalContents.WithoutContents(Carrier);
        if(CarrierSlot<0)retired.Cursor=stripped;else retired.Bag[CarrierSlot]=stripped;
        return true;
    }
    internal static bool TryRead(XElement node,out RebirthCamperRemovalParcelPlan plan)
    {
        plan=null;
        try
        {
            if(node?.Name!="removalParcelPlan"||(int?)node.Attribute("version")!=1||node.Attributes().Count()!=12||node.Elements().Count()!=6)return false;
            var value=new RebirthCamperRemovalParcelPlan{Operation=Guid.ParseExact((string)node.Attribute("operation"),"N"),
                Removal=Guid.ParseExact((string)node.Attribute("removal"),"N"),Vehicle=Guid.ParseExact((string)node.Attribute("vehicle"),"N"),Installation=Guid.ParseExact((string)node.Attribute("installation"),"N"),
                World=(string)node.Attribute("world"),Owner=(string)node.Attribute("owner"),Creation=(string)node.Attribute("creation"),Source=(string)node.Attribute("source"),
                Entity=(int)node.Attribute("entity"),Deaths=(int)node.Attribute("deaths"),CarrierSlot=(int)node.Attribute("carrierSlot"),
                Before=RebirthRemoteResourceClientOriginal.ReadActor(node.Element("beforeActor").Value),
                Credited=RebirthRemoteResourceClientOriginal.ReadActor(node.Element("creditedActor").Value),Retired=RebirthRemoteResourceClientOriginal.ReadActor(node.Element("retiredActor").Value),
                Contents=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("contents").Value)),
                Overflow=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("overflow").Value))};
            var carrier=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("carrier").Value));
            if(carrier.Length!=1)return false;value.Carrier=carrier[0];
            if(value.Operation==Guid.Empty||value.Removal==Guid.Empty||value.Vehicle==Guid.Empty||value.Installation==Guid.Empty||value.Entity<0||value.Deaths<0||
                string.IsNullOrEmpty(value.World)||string.IsNullOrEmpty(value.Owner)||string.IsNullOrEmpty(value.Creation)||value.Creation.Length>512||
                value.Source?.Length!=128||!value.Source.All(Uri.IsHexDigit)||value.Contents.Length<1||value.Contents.Length>byte.MaxValue||
                value.Operation!=Key(value.World,value.Source,value.Owner,value.Creation)||
                !RebirthCamperStationRemovalContents.TryRead(value.Carrier,out var originalContents)||!RebirthCamperEnergyItems.Same(originalContents,value.Contents)||
                !RebirthCamperPortableStation.TryGetSourceAddress(value.Carrier,out var address)||address.Vehicle!=value.Vehicle||address.Installation!=value.Installation||
                !value.Build(out var credited,out var retired,out var overflow)||!credited.Matches(value.Credited)||!retired.Matches(value.Retired)||
                !RebirthCamperEnergyItems.Same(overflow,value.Overflow)||!XNode.DeepEquals(node,value.Write())||Bytes(node).Length>24*1024*1024)return false;
            plan=value;return true;
        }
        catch{plan=null;return false;}
    }
    internal RebirthCamperTransferJournal.Record Retain()
    {
        byte[] bytes=Bytes(Write());return new RebirthCamperTransferJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","RemovalParcelOriginals"))
            .Prepare(Operation,Vehicle,Creation,0,bytes,bytes);
    }
}
