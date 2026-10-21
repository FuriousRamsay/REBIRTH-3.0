using System;
using System.Collections.Generic;
using System.Linq;

// Pure pre-source plan. The eventual retained original must bind this plan,
// native incarnation and positive saved Before before any source is attempted.
internal static class RebirthRemoteResourcePlacedStationPlan
{
    internal sealed class Plan
    {
        internal readonly Guid Job,Source;internal readonly string Binding;
        internal readonly int Slot,SourceMarkerGrowth,OriginalEntityId;internal readonly bool Burning;
        internal readonly RebirthRemoteResourcePlacedStationAllocation.Admission Allocation;
        private readonly ItemStack[] input,output;private readonly RebirthCamperEnergyActorImage actor;
        private readonly string queue;private readonly byte[] before,after;internal readonly int ClockOffset;
        internal Plan(Guid job,Guid source,string binding,int slot,bool burning,ItemStack[] nextInput,RebirthCamperEnergyActorImage nextActor,
            string nextQueue,byte[] oldBytes,byte[] newBytes,int clockOffset,ItemStack[] nextOutput,int markerGrowth,int originalEntityId,
            RebirthRemoteResourcePlacedStationAllocation.Admission allocation)
        {Job=job;Source=source;Binding=binding;Slot=slot;Burning=burning;input=ItemStack.Clone(nextInput);actor=nextActor.Copy();queue=nextQueue;
            before=(byte[])oldBytes.Clone();after=(byte[])newBytes.Clone();ClockOffset=clockOffset;output=ItemStack.Clone(nextOutput);SourceMarkerGrowth=markerGrowth;OriginalEntityId=originalEntityId;Allocation=allocation;}
        internal ItemStack[] CopyInput()=>ItemStack.Clone(input);
        internal ItemStack[] CopyOutput()=>ItemStack.Clone(output);
        internal RebirthCamperEnergyActorImage CopyActor()=>actor.Copy();
        internal string QueueImage=>queue;
        internal byte[] CopyBefore()=>(byte[])before.Clone();internal byte[] CopyAfter()=>(byte[])after.Clone();
        internal void WriteArtifact(System.Xml.XmlWriter writer)
        {
            writer.WriteStartElement("plan");writer.WriteAttributeString("job",Job.ToString("N"));writer.WriteAttributeString("source",Source.ToString("N"));writer.WriteAttributeString("binding",Binding);
            writer.WriteAttributeString("slot",System.Xml.XmlConvert.ToString(Slot));writer.WriteAttributeString("burning",System.Xml.XmlConvert.ToString(Burning));
            writer.WriteAttributeString("clockOffset",System.Xml.XmlConvert.ToString(ClockOffset));writer.WriteAttributeString("sourceMarkerGrowth",System.Xml.XmlConvert.ToString(SourceMarkerGrowth));
            writer.WriteAttributeString("entity",System.Xml.XmlConvert.ToString(OriginalEntityId));
            writer.WriteElementString("queue",queue);writer.WriteStartElement("afterTile");writer.WriteBase64(after,0,after.Length);writer.WriteEndElement();
            writer.WriteStartElement("actorAfter");
            using(var sink=new RebirthRemoteResourcePlacedStationArtifact.Base64Sink(writer,6*1024*1024))
            using(var binary=new System.IO.BinaryWriter(sink,System.Text.Encoding.UTF8,true)){RebirthRemoteResourcePlacedStationStreams.WriteActor(binary,actor);binary.Flush();}
            writer.WriteEndElement();writer.WriteEndElement();
        }
    }
    internal static bool TryCreate(TileEntityWorkstation station,RebirthRemoteResourceClientOriginal.Intent original,
        RebirthRemoteResourceDurableJournal.Record source,RebirthCamperEnergyActorImage originalActor,int entityId,bool preview,
        IReadOnlyDictionary<string,RebirthRemoteResourceSourceCustody.Lease> sourceLeases,out Plan plan,
        RebirthRemoteResourcePlacedStationAllocation.Admission allocation=null)
    {
        plan=null;
        try
        {
            if(allocation==null&&!RebirthRemoteResourcePlacedStationAllocation.TryAdmitPlanning(source,original,station,out allocation))return false;
            if(source==null||original==null||allocation.Source!=source.Operation||allocation.Binding!=source.Binding||allocation.Original!=original.Digest)return false;
            // A paid original always reloads its prior immutable plan. Never
            // recreate Before from a post-source current station image.
            if(!preview||station==null||station is RebirthCamperStationState||originalActor==null||source==null||
                !RebirthRemoteResourcePlacedStationOriginal.TryReadAdmission(source,out var admitted)||!original.Copy().ToString().Equals(admitted.Copy().ToString(),StringComparison.Ordinal)||
                !RebirthRemoteResourcePlacedStationOriginal.ValidateInput(original,out var originalPosition,out uint originalBlock,out string originalWindow,out var authored,out var template,out int batches,out int tier,out int belt,out var payment,out bool turnsOn,out var input)||
                RebirthRecipeOutputs.DefinitionIdentity(authored)!=original.Copy().Element("action").Element("outputDefinition").Value||
                !RebirthRecipeOutputs.TryCaptureFrozenOutputs(authored,out string outputImage)||outputImage!=original.Copy().Element("action").Element("outputImage").Value||
                !RebirthStationNativeQueueEntry.TryFindVacantSlot(station.Queue,original.Operation,out int slot)||
                input.Length!=station.Input.Length||input.Where((item,index)=>!RebirthStationGridIngredients.IsSameStackSnapshot(item,station.Input[index])).Any())return false;
            if(!RebirthRemoteResourceClientOriginal.ReadActor(original.Copy().Element("actorBefore").Value).Matches(originalActor))return false;
            var world=GameManager.Instance?.World;
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||world.worldState.Guid!=original.World||!(world.GetEntity(entityId) is EntityPlayer ownerActor)||!ReferenceEquals(ownerActor.world,world)||ownerActor.IsDead()||!ownerActor.IsSpawned()||
                ownerActor.Died!=original.Deaths||!RebirthStablePlayerIdentity.TryResolveServerEntity(ownerActor,out var owner)||owner.StorageKey!=source.Owner||owner.CanonicalId!=original.Owner||
                !RebirthOriginalActionScope.Matches(ownerActor,owner.StorageKey,original.Creation)||
                !RemoteResourceWorldIdentity.TryGet(world,out string sourceWorld)||sourceWorld!=source.World||
                world.GetBlock(originalPosition).rawData!=originalBlock||
                !RebirthRemoteResourcePlacedStationScope.TryResolveLayout(ownerActor,originalPosition,original.Creation,out var currentStation,out string currentWindow,out var currentPayment,out bool currentTurnsOn)||
                !ReferenceEquals(currentStation,station)||currentWindow!=originalWindow||currentPayment!=payment||currentTurnsOn!=turnsOn||
                !RebirthCamperEnergySavedOwner.TryRead(owner,out var savedActor,out var saved)||saved.id!=entityId||saved.deaths!=original.Deaths||!savedActor.Matches(originalActor)||
                !RebirthRemoteResourcePlacedStationSavedReceipt.TryRead(saved,RebirthRemoteResourceOwnerAction.Receipt(original.Operation),out float ownerPhase,out bool present)||!present||ownerPhase!=10f)return false;
            var originalQueue=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Copy().Element("action").Element("queue").Value,RebirthRemoteResourcePlacedStationQueue.MaximumBytes);
            if(originalQueue.Element("native")?.Value!=RebirthCookingRemoteAdmissionBackend.Queue(station.Queue))return false;
            var image=source.Copy();
            if(source.Phase!=1||image.Element("sources").Elements("source").Any(node=>(int)node.Attribute("attempted")!=0))return false;
            if(!RebirthRemoteResourcePlacedStationSources.TryPreview(source,station,sourceLeases,out var outputAfter,out int growth))return false;
            var remote=image.Element("sources").Elements("source").SelectMany(node=>RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(node.Element("paid").Value))).ToArray();
            var required=RebirthCamperInventorySnapshot.Restore(Convert.FromBase64String(original.Copy().Element("requirements").Value));
            if(remote.Length<1||remote.Length>8192)return false;
            ItemStack[] nextInput;RebirthCamperEnergyActorImage nextActor;List<ItemStack> paid;
            if(payment==RebirthRemoteResourcePlacedStationScope.PaymentSource.StationInput)
            {
                if(!RebirthRemoteResourcePlacedStationInputPlan.TryCreate(input,required,remote,out var grid))return false;
                nextInput=grid.CopyAfter();paid=grid.CopyPaid().ToList();nextActor=originalActor.Copy();
            }
            else
            {
                if(!RebirthRemoteResourceCraftPlan.IsExactShortfall(originalActor,required,remote,belt)||
                    !RebirthRemoteResourceCraftPlan.TryDebit(originalActor,required,remote,belt,out nextActor,out paid))return false;
                nextInput=ItemStack.Clone(input);
            }
            if(!RebirthRemoteResourcePlacedStationPaidRow.TryCreate(original.Operation,source.Operation,source.Binding,template,paid,batches,tier,entityId,slot==station.Queue.Length-1,out var row,outputImage))return false;
            // Preserve live unrelated rows by reference for eventual slot-only
            // publication. The serialized detached image is immutable evidence.
            var queue=(RecipeQueueItem[])station.Queue.Clone();queue[slot]=row;bool burning=station.IsBurning||turnsOn;
            string encoded=RebirthRemoteResourcePlacedStationQueue.Encode(queue);
            if(!RebirthRemoteResourcePlacedStationQueue.TryDecode(encoded,out var detached)||
                !RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,input,station.Queue,station.IsBurning,out var before,out int oldClock)||
                !RebirthRemoteResourcePlacedStationCandidate.TrySerialize(station,nextInput,outputAfter,detached,burning,out var after,out int newClock)||oldClock!=newClock)return false;
            plan=new Plan(original.Operation,source.Operation,source.Binding,slot,burning,nextInput,nextActor,encoded,before,after,newClock,outputAfter,growth,entityId,allocation);return true;
        }
        catch{return false;}
    }
}
