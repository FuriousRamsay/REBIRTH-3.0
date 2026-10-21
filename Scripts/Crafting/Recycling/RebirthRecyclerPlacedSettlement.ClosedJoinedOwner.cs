using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    private static void TryBeginActualClosedJoinedOwner(ClosedOwnerGrant grant,XElement envelope)
    {
        bool unaccepted=(bool?)envelope.Attribute("unacceptedRejoin")==true;
        Guid nonce=Guid.Empty,generation;
        if(!ReferenceEquals(receivingClosedGrant,grant))return;
        if(unaccepted)
        {if((bool?)envelope.Attribute("restored")==true||!Guid.TryParseExact((string)envelope.Attribute("generation"),"N",out generation)||generation==Guid.Empty||
            (int?)envelope.Attribute("phase")!=0||(int?)envelope.Attribute("completed")!=0||(string)envelope.Attribute("stage")!="before")return;}
        else{if(!Guid.TryParseExact((string)envelope.Attribute("nonce"),"N",out nonce)||nonce==Guid.Empty)return;generation=nonce;}
        var joined=joinedRecyclerOwners.SingleOrDefault(value=>ReferenceEquals(value.Actor,grant.Actor)&&ReferenceEquals(value.Connection,grant.Connection)&&
            value.Current()&&value.Pending.Contains(grant.Operation));if(joined==null)return;
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),grant.Operation.ToString("N")+".remote-client.xml");
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var readMemory))return;
        using(readMemory)
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var node=record?.Element("before")?.Elements().SingleOrDefault();
            var request=record?.Element("recyclerRemoteClientOriginal");int phase=(int?)envelope.Attribute("phase")??-1,count=(int?)envelope.Attribute("completed")??-1;
            string stage=(string)envelope.Attribute("stage");
            if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=grant.Operation.ToString("N")||
                (string)record.Attribute("world")!=grant.WorldKey||node==null||(bool?)request?.Attribute("closedAuthority")!=true||
                !unaccepted&&(string)envelope.Attribute("original")!=RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)))||
                stage!="before"&&stage!="after"||stage=="before"&&((phase!=0&&phase!=1)||count!=0))return;
            var source=Original.FromActualClosedJoin(grant,joined,node,phase,count);if(source==null)return;
            IDisposable automatic=null;bool owned=false;
            try
            {
                if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out automatic))return;
                var client=new RecyclerClient{Source=source,Connection=grant.Connection,Request=grant.Group,Nonce=nonce,
                    ColdJoined=joined,ColdPhase=phase,ColdCompleted=count,Generation=generation,AutomaticMemory=automatic};
                grant.Client=client;recyclerClients.Add(client);owned=true;
                joined.Pending=joined.Pending.Where(value=>value!=grant.Operation).ToArray();joined.UnqualifiedStations.Remove(grant.Operation);joined.Unstarted.Remove(grant.Operation);
                SendPrepare(client);
            }
            finally{if(!owned){automatic?.Dispose();source.Dispose();}}
        }
    }
    internal sealed partial class OwnerPublication
    {
        internal static OwnerPublication FromActualClosedJoin(RecyclerClient client)
        {
            var original=client?.Source;var joined=client?.ColdJoined;
            if(original?.ClosedOwner==null||joined?.Current()!=true||!client.Live()||client.Generation==Guid.Empty||
                RebirthCookingOutputDelivery.IsHeld(original.Actor)||RebirthBackpackLibraryReservation.IsHeld(original.Actor)||
                RebirthGearOwnerReservation.IsHeld(original.Actor)||RebirthDogLifecycleService.IsHireInventoryHeld(original.Actor)||
                RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(original.Actor)||
                !original.MatchesOwnerCheckpoint(joined.Bag,joined.Belt,client.ColdCompleted)||
                original.Actor.Buffs.GetCustomVar(original.Receipt)!=client.ColdPhase||original.Actor.Buffs.GetCustomVar(original.Checkpoint)!=client.ColdCompleted||
                !original.MatchesStationStage(client.Stage>=3)||
                !RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*(joined.Native.Length+original.Image.Length)+65536),out var memory))return null;
            using(memory)
            {
                var record=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("world",original.WorldKey),
                    new XElement("before",XElement.Parse(Encoding.UTF8.GetString(original.Image))),new XElement("recyclerClosedJoinedOwner",
                        new XAttribute("generation",client.Generation.ToString("N")),new XAttribute("actualNativeJoin",true),new XAttribute("phase",client.ColdPhase),
                        new XAttribute("completed",client.ColdCompleted),new XElement(client.LastReply),new XElement("nativeJoinedOwner",Convert.ToBase64String(joined.Native))));
                string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),original.Operation.ToString("N")+".reentry."+client.Generation.ToString("N")+".original.xml");
                if(!RebirthWorkstationCompletionPublication.Write(path,record)||!XNode.DeepEquals(record,RebirthWorkstationCompletionPublication.ReadPrimary(path))||!joined.Current())return null;
                var result=new OwnerPublication(original){completed=client.ColdCompleted,initialPhase=client.ColdPhase==0?1:client.ColdPhase,
                    generation=client.Generation,stationPaid=client.Stage>=3};ownerPublications.Add(result);
                if(client.ColdPhase==0)
                    try{original.Actor.Buffs.SetCustomVar(original.Receipt,1f);original.Actor.Buffs.SetCustomVar(original.Checkpoint,0f);result.SaveCheckpoint(1);}
                    catch{result.unknown=true;}
                client.ColdJoined=null;
                if(joined.Pending.Length==0)joined.Dispose();
                return result;
            }
        }
    }
    internal sealed partial class Original
    {
        internal static Original FromActualClosedJoin(ClosedOwnerGrant grant,RecyclerJoining joined,XElement node,int phase,int count)
        {
            if(!ReferenceEquals(receivingClosedGrant,grant)||joined?.Current()!=true||!ReferenceEquals(joined.Actor,grant.Actor)||
                !ReferenceEquals(joined.Connection,grant.Connection)||!joined.Pending.Contains(grant.Operation)||
                !RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return null;
            int length=Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting));
            if(length<1||length>8*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*length+65536),out var memory))return null;
            Original issued=null;
            try
            {
                using var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder();if(decoder==null)return null;
                var native=grant.NativeOriginal;
                if(node.Name!="placedRecyclerOriginal"||(int?)node.Attribute("version")!=1||(string)node.Attribute("purpose")!="complete"||
                    (string)node.Attribute("operation")!=grant.Operation.ToString("N")||(string)node.Attribute("owner")!=grant.Owner||
                    (string)node.Attribute("account")!=grant.Account||(string)node.Attribute("creation")!=grant.Creation||
                    (int?)node.Attribute("deaths")!=grant.Deaths||(string)node.Attribute("world")!=grant.WorldKey||
                    !XNode.DeepEquals(node.Element("closedAuthority"),new XElement("closedAuthority",new XAttribute("ticket",grant.Ticket.ToString("N")),
                        new XAttribute("group",grant.Group.ToString("N")),new XAttribute("nativeOriginal",grant.NativeDigest))))return null;
                foreach(string name in new[]{"x","y","z","block","beforeOffset","afterOffset","workSeconds"})
                    if((string)node.Attribute(name)!=(string)native.Attribute(name))return null;
                byte[] before=ColdBytes(node.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),after=ColdBytes(node.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                int beforeOffset=(int)node.Attribute("beforeOffset"),afterOffset=(int)node.Attribute("afterOffset");
                if(!before.SequenceEqual(ColdBytes(native.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes))||
                    !after.SequenceEqual(ColdBytes(native.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes))||
                    !RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var projected,out _))return null;
                var beforeBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("beforeBag"),4*1024*1024));
                var beforeBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("beforeBelt"),4*1024*1024));
                var afterBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("afterBag"),4*1024*1024));
                var afterBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("afterBelt"),4*1024*1024));
                var returns=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("returns"),4*1024*1024));
                string bagBits=(string)node.Attribute("bagLocks"),beltBits=(string)node.Attribute("beltLocks");
                if(beforeBag==null||beforeBelt==null||afterBag==null||afterBelt==null||returns==null||bagBits==null||beltBits==null||
                    bagBits.Length!=beforeBag.Length||beltBits.Length!=beforeBelt.Length||bagBits.Any(c=>c!='0'&&c!='1')||beltBits.Any(c=>c!='0'&&c!='1'))return null;
                var bagLocks=bagBits.Select(c=>c=='1').ToArray();var beltLocks=beltBits.Select(c=>c=='1').ToArray();int beltCount=(int)node.Attribute("beltCount");
                if(!SameOwnerGrid(returns,RebirthCamperInventorySnapshot.Restore(ColdBytes(native.Element("returns"),4*1024*1024)))||
                    !RebirthRecyclerOwnerDelivery.Plan(beforeBag,beforeBelt,bagLocks,beltLocks,beltCount,returns,out var expectedBag,out var expectedBelt)||
                    !SameOwnerGrid(expectedBag,afterBag)||!SameOwnerGrid(expectedBelt,afterBelt))return null;
                var result=issued=new Original(grant.Operation,"complete",joined.Actor,null,identity,beforeBag,beforeBelt,afterBag,afterBelt,returns,
                    before,after,beforeOffset,afterOffset,projected.Input,projected.Queue,projected.Output,projected.Tools,
                    Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)),memory){ClosedOwner=grant,Cycle=true};memory=null;
                if(!(phase==0&&count==0||ValidOwnerCheckpointStage(phase,count,result.Slots.Length))||
                    !PhaseBytes(joined.Buffs,result.Receipt,out float actualPhase)||actualPhase!=phase||
                    !PhaseBytes(joined.Buffs,result.Checkpoint,out float actualCount)||actualCount!=count||
                    !result.MatchesOwnerCheckpoint(joined.Bag,joined.Belt,count)||!result.BagLocks.SequenceEqual(bagLocks)||
                    !result.BeltLocks.SequenceEqual(beltLocks)||result.BeltCount!=beltCount||!result.Current()||!joined.Current()||HasUnresolvedOwnerSlotAttempt(result,count))return null;
                issued=null;return result;
            }
            catch{return null;}
            finally{issued?.Dispose();memory?.Dispose();}
        }
    }
}
