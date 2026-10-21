using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRemoteResourceFetchedStationOriginal
{
    private sealed class CancellationLease{internal EntityPlayerLocal Player;internal World World;internal object Lease;internal string Directory,Hash;}
    private static readonly Dictionary<Guid,CancellationLease> CancellationLeases=new Dictionary<Guid,CancellationLease>();
    private static RebirthRemoteResourceClientOriginalJournal CancellationPayloads()=>new RebirthRemoteResourceClientOriginalJournal(Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","FetchedStationCancellationPayloads"));
    internal static bool CancellationForeignHold(EntityPlayerLocal player)=>player==null||RebirthLogisticsOwnerReceipt.IsHeld(player)||RebirthPlacedOriginal.IsHeld(player)||
        RebirthCookingOutputDelivery.HasOwnHold(player)||RebirthCookingOwnerDelta.HasOwnHold(player)||RebirthCookingGridCustody.IsHeld(player)||
        AdvancedFarmingHarvestOwnerDelta.IsHeld(player)||AdvancedFarmingWaterExchangeOriginal.IsHeld(player)||RebirthGearOwnerReservation.IsHeld(player)||
        RebirthBackpackLibraryReservation.IsHeld(player)||RebirthCamperEnergyOwnerHold.Blocks(player.bag)||RebirthRemoteResourceOwnerAction.IsHeld(player);
    internal static bool IsCancellationClientCandidate(Guid operation)
    {try{return !AbsentArtifacts("FetchedStationCancellationPayloads",operation);}catch{return true;}}
    internal static bool HasCancellationPayload(Guid operation)
    {
        try{return KnownTerminalFiles("FetchedStationCancellationPayloads",operation)&&CancellationPayloads().Read(operation)?.State==RebirthCamperTransferJournal.Phase.Applied;}
        catch{return false;}
    }
    internal static bool MatchesCancellationTerminalPayload(EntityPlayerLocal player,Guid operation,RebirthCamperStationAddress address,string creation,byte[] payload)
    {
        try
        {
            if(!HasCancellationPayload(operation))return false;var record=CancellationPayloads().Read(operation);
            if(record.Vehicle!=address.Vehicle||record.OwnerCreation!=creation||record.Revision!=0||record.Before.Length>12*1024*1024||!record.Before.SequenceEqual(record.After))return false;
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),12*1024*1024);
            if(body.Name!="fetchedStationCancellationPayload"||body.Attributes().Count()!=7||body.Elements().Count()!=1||body.Element("native")==null||
                (int?)body.Attribute("version")!=1||(string)body.Attribute("job")!=operation.ToString("N")||(string)body.Attribute("address")!=address.ToString()||
                !Guid.TryParseExact((string)body.Attribute("parent"),"N",out var parent)||NativeCancellationOperation(parent)!=operation||
                !RebirthRemoteResourceClientOriginal.TryRead(parent,out var original)||!RebirthRemoteResourceClientOriginal.Current(player,original)||original.Creation!=creation||
                (string)body.Attribute("original")!=original.Digest||!RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out _,out string binding,out _)||
                (string)body.Attribute("binding")!=binding||!TryReadClientPayload(player,parent,out var enqueue)||!HasRetirementMetadataBinding(player,parent,address,creation,enqueue))return false;
            byte[] canonical=CanonicalClientPayload(player,operation,address,creation,payload);
            return body.Element("native").Value==Convert.ToBase64String(canonical)&&(string)body.Attribute("payload")==RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(canonical));
        }
        catch{return false;}
    }
    internal static void ForgetTerminalCancellationLease(EntityPlayerLocal player,Guid operation,RebirthCamperStationAddress address,string creation,byte[] payload)
    {if(MatchesCancellationTerminalPayload(player,operation,address,creation,payload))CancellationLeases.Remove(operation);}
    internal static bool RetainCancellationPayload(EntityPlayerLocal player,Guid originalJob,Guid cancellation,RebirthCamperStationAddress address,string creation,byte[] payload,
        ItemStack[] beforeBag,ItemStack[] beforeBelt,ItemStack[] afterBag,ItemStack[] afterBelt,bool[] locks,Func<bool> authenticated)
    {
        try
        {
            if(authenticated?.Invoke()!=true||CancellationForeignHold(player)||NativeCancellationOperation(originalJob)!=cancellation||!KnownTerminalFiles("FetchedStationCancellationPayloads",cancellation,true)||
                !RebirthRemoteResourceClientOriginal.TryRead(originalJob,out var original)||!RebirthRemoteResourceClientOriginal.Current(player,original)||!IsCandidate(original)||original.Creation!=creation||
                !RebirthRemoteResourceClientOriginal.TryReadPaidActionSource(original,out _,out var source,out string binding,out _)||
                !TryReadClientPayload(player,originalJob,out var enqueue)||!HasRetirementMetadataBinding(player,originalJob,address,creation,enqueue)||
                !RebirthCamperStationWindowSession.TryGetCurrent(player,out _,out var actualAddress)||!actualAddress.Equals(address)||
                !RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var lease))return false;
            var originalRecord=ClientPayloads().Read(originalJob);
            var originalBody=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(originalRecord.Before),12*1024*1024);
            string definition=originalBody.Element("definition").Value;if(!RebirthNativeItemCodec.TryDecode(definition,out var installed))return false;
            var message=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(payload),8*1024*1024);
            var before=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(Convert.FromBase64String(message.Element("before").Value)),8*1024*1024);
            var after=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(Convert.FromBase64String(message.Element("after").Value)),8*1024*1024);
            var old=RebirthCamperStationSnapshot.Restore(address.Installation,new ItemStack(installed,1),Convert.FromBase64String(before.Element("stationImage").Value));
            int slot=(int)before.Attribute("queueSlot");if(slot<0||slot>=old.Queue.Length)return false;
            var row=old.Queue[slot];
            if(!RebirthCamperStationNativeCrafting.TryJob(row?.Recipe,out var actualJob)||actualJob!=originalJob||
                old.Queue.Count(card=>RebirthCamperStationNativeCrafting.TryJob(card?.Recipe,out var id)&&id==originalJob)!=1||
                !RebirthCamperStationNativeCrafting.RestoreActorImages(row.Recipe)||!RebirthRemoteResourceFetchedStationPlan.RestoreRecipeFields(row.Recipe))return false;
            var admitted=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(enqueue),8*1024*1024);
            var admittedFrame=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(Convert.FromBase64String(admitted.Element("after").Value)),8*1024*1024);
            var initial=RebirthCamperStationSnapshot.Restore(address.Installation,new ItemStack(installed,1),Convert.FromBase64String(admittedFrame.Element("stationImage").Value));
            var originalRow=initial.Queue[(int)admittedFrame.Attribute("queueSlot")];
            if(!RebirthCamperStationNativeCrafting.RestoreActorImages(originalRow.Recipe)||!RebirthRemoteResourceFetchedStationPlan.RestoreRecipeFields(originalRow.Recipe)||
                row.RepairItem!=null||row.Multiplier<1||row.Multiplier>originalRow.Multiplier||row.Quality!=originalRow.Quality||
                RebirthRemoteResourceOwnerAction.RecipeImage(row.Recipe)!=RebirthRemoteResourceOwnerAction.RecipeImage(originalRow.Recipe)||
                !RebirthCamperStationNativeCrafting.TryRemainingRefund(row,out var remaining))return false;
            var marker=row.Recipe.ingredients[0].itemValue;
            if(!marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.SourceKey,out string src)||src!=source.ToString("N")||
                !marker.TryGetMetadata(RebirthRemoteResourceFetchedStationPlan.BindingKey,out string bound)||bound!=binding)return false;
            foreach(var item in remaining)
            {
                if(!item.itemValue.TryGetMetadata("rebirth.camper.native.actor.owner",out string owner)||owner!=original.Owner||
                    !item.itemValue.TryGetMetadata("rebirth.camper.native.actor.source",out string kind)||(kind!="fetch"&&kind!="bag"&&kind!="belt")||
                    !item.itemValue.TryGetMetadata("rebirth.camper.native.actor.slot",out int index)||(kind=="fetch"?index!=-1:index<0))return false;
                item.itemValue.Metadata.Remove("rebirth.camper.native.actor.owner");item.itemValue.Metadata.Remove("rebirth.camper.native.actor.source");item.itemValue.Metadata.Remove("rebirth.camper.native.actor.slot");
            }
            if(!RebirthRecyclerOwnerDelivery.Plan(beforeBag,beforeBelt,locks,null,player.inventory.Length,remaining,out var expectedBag,out var expectedBelt)||
                !SameArea(expectedBag,afterBag)||!SameArea(expectedBelt,afterBelt))return false;
            old.Queue[slot]=new RecipeQueueItem{StartingEntityId=-1};old.setModified();
            if(!RebirthCamperStationSnapshot.Capture(address.Installation,old).SequenceEqual(Convert.FromBase64String(after.Element("stationImage").Value)))return false;
            byte[] canonical=CanonicalClientPayload(player,cancellation,address,creation,payload);string hash=RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(canonical));
            var body=new XElement("fetchedStationCancellationPayload",new XAttribute("version",1),new XAttribute("original",original.Digest),new XAttribute("job",cancellation.ToString("N")),
                new XAttribute("parent",originalJob.ToString("N")),new XAttribute("address",address.ToString()),new XAttribute("binding",binding),new XAttribute("payload",hash),new XElement("native",Convert.ToBase64String(canonical)));
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(bytes.Length>12*1024*1024)return false;
            var journal=CancellationPayloads();var record=journal.Prepare(cancellation,address.Vehicle,creation,0,bytes,bytes);
            if(!record.Before.SequenceEqual(bytes)||!record.After.SequenceEqual(bytes)||authenticated()!=true)return false;
            if(record.State==RebirthCamperTransferJournal.Phase.Prepared)record=journal.Advance(cancellation,record.State,RebirthCamperTransferJournal.Phase.Uncertain);
            if(record.State==RebirthCamperTransferJournal.Phase.Uncertain)record=journal.Advance(cancellation,record.State,RebirthCamperTransferJournal.Phase.Applied);
            if(record.State!=RebirthCamperTransferJournal.Phase.Applied||!RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var finalLease)||!ReferenceEquals(finalLease,lease)||authenticated()!=true)return false;
            foreach(var key in CancellationLeases.Where(pair=>!ReferenceEquals(pair.Value.World,player.world)||pair.Value.Directory!=GameIO.GetSaveGameDir()).Select(pair=>pair.Key).ToArray())CancellationLeases.Remove(key);
            if(CancellationLeases.Count>=32&&!CancellationLeases.ContainsKey(cancellation))return false;
            CancellationLeases[cancellation]=new CancellationLease{Player=player,World=player.world,Directory=GameIO.GetSaveGameDir(),Lease=lease,Hash=hash};return true;
        }
        catch{return false;}
    }
    internal static bool MatchesCancellationPayload(EntityPlayerLocal player,Guid operation,RebirthCamperStationAddress address,string creation,byte[] payload)
    {
        try
        {
            if(CancellationForeignHold(player)||!HasCancellationPayload(operation)||!CancellationLeases.TryGetValue(operation,out var lease)||!ReferenceEquals(lease.Player,player)||!ReferenceEquals(lease.World,player.world)||lease.Directory!=GameIO.GetSaveGameDir()||
                !RebirthCamperStationWindowSession.TryGetCurrent(player,out _,out var actual)||!actual.Equals(address)||!RebirthCamperStationWindowSession.TryGetLeaseIdentity(player,out var current)||!ReferenceEquals(current,lease.Lease))return false;
            var record=CancellationPayloads().Read(operation);byte[] canonical=CanonicalClientPayload(player,operation,address,creation,payload);
            var body=RebirthCookingRemoteAdmission.ReadProtocolXml(new UTF8Encoding(false,true).GetString(record.Before),12*1024*1024);
            return record.Vehicle==address.Vehicle&&record.OwnerCreation==creation&&record.Revision==0&&record.Before.SequenceEqual(record.After)&&body.Name=="fetchedStationCancellationPayload"&&body.Attributes().Count()==7&&body.Elements().Count()==1&&
                (int?)body.Attribute("version")==1&&(string)body.Attribute("job")==operation.ToString("N")&&(string)body.Attribute("address")==address.ToString()&&
                (string)body.Attribute("payload")==lease.Hash&&lease.Hash==RebirthRemoteResourceDurableJournal.Digest(Convert.ToBase64String(canonical))&&body.Element("native")?.Value==Convert.ToBase64String(canonical);
        }
        catch{return false;}
    }
    internal static void ForgetCancellationLease(EntityPlayerLocal player,Guid operation,RebirthCamperStationAddress address,string creation,byte[] payload)
    {
        // This removes ONLY transient lookup state. It does not remove the
        // native owner hold or any journal/receipt; without a fresh positive
        // receiver binding, future setters consequently refuse.
        if(MatchesCancellationPayload(player,operation,address,creation,payload))CancellationLeases.Remove(operation);
    }
}
