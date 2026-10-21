using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthRecyclerPlacedSettlement
{
    internal sealed partial class Original
    {
        internal static Original FromActualRemoteHydration(RecyclerJoining joined,RebirthProductiveEarnedUiDeferredTransport.ClientHydration hydration,XElement node,int phase,int count)
        {
            if(joined?.Current()!=true||!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(hydration)||!ReferenceEquals(joined.World,hydration.World)||
                !ReferenceEquals(joined.Actor,hydration.Actor)||!ReferenceEquals(joined.Connection,hydration.Connection)||!RebirthStablePlayerIdentity.TryFromLocalPlatform(out var identity))return null;
            int measured=Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting));
            if(measured<1||measured>8*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(16L*measured+65536),out var memory))return null;
            Original issued=null;
            try
            {
                using(var decoder=RebirthProductiveEarnedObservationBudget.TryEnterDecoder())
                {
                    if(decoder==null||node.Name!="placedRecyclerOriginal"||(int?)node.Attribute("version")!=1||
                        !Guid.TryParseExact((string)node.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
                        (string)node.Attribute("owner")!=joined.Owner||(string)node.Attribute("account")!=joined.Account||(string)node.Attribute("creation")!=joined.Creation||
                        (int?)node.Attribute("deaths")!=joined.Deaths||(string)node.Attribute("world")!=joined.WorldKey||
                        hydration.Station.ToWorldPos()!=new Vector3i((int)node.Attribute("x"),(int)node.Attribute("y"),(int)node.Attribute("z"))||
                        hydration.Station.block.GetBlockName()!=(string)node.Attribute("block"))return null;
                    byte[] before=ColdBytes(node.Element("beforeNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes),after=ColdBytes(node.Element("afterNative"),RebirthProductiveEarnedNativeBody.MaximumBodyBytes);
                    int beforeOffset=(int)node.Attribute("beforeOffset"),afterOffset=(int)node.Attribute("afterOffset");
                    if(!RebirthProductiveEarnedNativeBody.TryRead(before,beforeOffset,out var prior,out _)||!RebirthProductiveEarnedNativeBody.TryRead(after,afterOffset,out var projected,out _))return null;
                    var beforeBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("beforeBag"),4*1024*1024));
                    var beforeBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("beforeBelt"),4*1024*1024));
                    var afterBag=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("afterBag"),4*1024*1024));
                    var afterBelt=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("afterBelt"),4*1024*1024));
                    var returns=RebirthCamperInventorySnapshot.Restore(ColdBytes(node.Element("returns"),4*1024*1024));
                    string bagBits=(string)node.Attribute("bagLocks"),beltBits=(string)node.Attribute("beltLocks"),purpose=(string)node.Attribute("purpose");
                    if(purpose!="enqueue"&&purpose!="cancel"&&purpose!="complete"||beforeBag==null||beforeBelt==null||afterBag==null||afterBelt==null||returns==null||
                        bagBits==null||beltBits==null||bagBits.Length!=beforeBag.Length||beltBits.Length!=beforeBelt.Length||bagBits.Any(value=>value!='0'&&value!='1')||beltBits.Any(value=>value!='0'&&value!='1'))return null;
                    var bagLocks=bagBits.Select(value=>value=='1').ToArray();var beltLocks=beltBits.Select(value=>value=='1').ToArray();int beltCount=(int)node.Attribute("beltCount");
                    if(!RebirthRecyclerOwnerDelivery.Plan(beforeBag,beforeBelt,bagLocks,beltLocks,beltCount,returns,out var expectedBag,out var expectedBelt)||
                        !SameOwnerGrid(afterBag,expectedBag)||!SameOwnerGrid(afterBelt,expectedBelt))return null;
                    var result=issued=new Original(operation,purpose,joined.Actor,hydration.Station,identity,beforeBag,beforeBelt,afterBag,afterBelt,returns,
                        before,after,beforeOffset,afterOffset,projected.Input,projected.Queue,projected.Output,projected.Tools,Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)),memory);
                    memory=null;int cycle=(int?)node.Attribute("cycle")??-1;if(cycle<0||cycle>1||cycle==1&&purpose!="complete")return null;
                    result.Cycle=cycle==1;if(result.Cycle){result.Queue=prior.Queue;result.Queue[result.Queue.Length-1]=new RecipeQueueItem{StartingEntityId=-1};}
                    if(!(phase==0&&count==0&&(string)hydration.Header.Element("recyclerReentry")?.Attribute("stage")=="before"||ValidOwnerCheckpointStage(phase,count,result.Slots.Length))||!PhaseBytes(joined.Buffs,result.Receipt,out float originalPhase)||originalPhase!=phase||
                        !PhaseBytes(joined.Buffs,result.Checkpoint,out float originalCount)||originalCount!=count||
                        !result.MatchesOwnerCheckpoint(joined.Bag,joined.Belt,count)||!result.Current()||!result.BagLocks.SequenceEqual(bagLocks)||!result.BeltLocks.SequenceEqual(beltLocks)||
                        result.BeltCount!=beltCount||!joined.Current()||HasUnresolvedOwnerSlotAttempt(result,count))return null;
                    issued=null;return result;
                }
            }
            catch{return null;}
            finally{issued?.Dispose();memory?.Dispose();}
        }
    }
    private static RecyclerClient PrepareActualColdRecyclerClient(RebirthProductiveEarnedUiDeferredTransport.ClientHydration hydration,XElement header)
    {
        if(recyclerClients.Count>=4||!RebirthProductiveEarnedUiDeferredTransport.IsActualHydrationRead(hydration)||
            !Guid.TryParseExact((string)header.Attribute("operation"),"N",out var operation)||!Guid.TryParseExact((string)header.Attribute("nonce"),"N",out var nonce)||nonce==Guid.Empty)return null;
        var joined=joinedRecyclerOwners.SingleOrDefault(value=>ReferenceEquals(value.Actor,hydration.Actor)&&ReferenceEquals(value.World,hydration.World)&&
            ReferenceEquals(value.Connection,hydration.Connection)&&value.Pending.Contains(operation)&&value.Current());
        if(joined==null)return null;
        string path=Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),operation.ToString("N")+".remote-client.xml");
        long length=RebirthWorkstationCompletionPublication.PublishedLength(path);
        if(length<1||length>24L*1024*1024||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(checked(12L*length+65536),out var readMemory))return null;
        using(readMemory)
        {
            var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);var node=record?.Element("before")?.Elements().SingleOrDefault();var request=record?.Element("recyclerRemoteClientOriginal");
            int phase=(int?)header.Attribute("phase")??-1,count=(int?)header.Attribute("completed")??-1;
            if(record?.Name!="workstationCompletionIntent"||(string)record.Attribute("operation")!=operation.ToString("N")||(string)record.Attribute("world")!=joined.WorldKey||
                node==null||request==null||(string)node.Attribute("operation")!=operation.ToString("N")||
                (string)header.Attribute("original")!=RebirthStationPublicationRecord.Digest(Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)))||
                !Guid.TryParseExact((string)request.Attribute("request"),"N",out var requestId))return null;
            var original=Original.FromActualRemoteHydration(joined,hydration,node,phase,count);if(original==null)return null;
            IDisposable automatic=null;bool retained=false;
            try
            {
                string stage=(string)header.Attribute("stage");bool after=stage=="after";
                if(stage!="before"&&!after||stage=="before"&&((phase!=0&&phase!=1)||count!=0)||
                    !RebirthProductiveEarnedNativeBody.MatchesCurrent(original.Station,after?original.AfterNative:original.BeforeNative,after?original.AfterOffset:original.BeforeOffset)||
                    !RebirthProductiveEarnedObservationBudget.TryRetainDetached(96L*1024*1024,out automatic))return null;
                var client=new RecyclerClient{Source=original,Connection=hydration.Connection,Request=requestId,Nonce=nonce,Slot=(int)request.Attribute("slot"),Units=(int)request.Attribute("units"),
                    Applied=after,NativeSent=after,PrincipalEntered=after,AutomaticMemory=automatic,ColdJoined=joined,ColdPhase=phase,ColdCompleted=count,Generation=hydration.Group,
                    LastReply=new XElement("actualRecyclerColdHydration",new XAttribute("group",hydration.Group.ToString("N")),new XElement(header))};
                retained=true;return client;
            }
            finally{if(!retained){automatic?.Dispose();original.Dispose();}}
        }
    }
}
