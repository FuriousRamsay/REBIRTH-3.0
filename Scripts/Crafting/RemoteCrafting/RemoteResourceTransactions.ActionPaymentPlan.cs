using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionCountStream:Stream
    {
        internal long Count;private readonly long limit;internal ActionCountStream(long bound){limit=bound;}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>Count;public override long Position{get=>Count;set=>throw new NotSupportedException();}
        public override void Write(byte[] bytes,int offset,int count){if(count<0||Count+count>limit)throw new InvalidDataException("Action native frame exceeds reserved bound");Count+=count;}
        public override void WriteByte(byte value){if(Count>=limit)throw new InvalidDataException();Count++;}
        public override void Flush(){}public override int Read(byte[] bytes,int offset,int count)=>throw new NotSupportedException();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long length)=>throw new NotSupportedException();
    }
    private static byte[] CaptureActionFrame(ItemStack[] slots,ref long remaining)
    {
        if(slots==null||slots.Length<1||slots.Length>short.MaxValue||remaining<8)throw new InvalidDataException("Action source dimensions invalid");
        long size=8;foreach(var stack in slots)
        {
            if(stack?.itemValue==null||stack.count<0)throw new InvalidDataException("Action native source incomplete");
            using var count=new ActionCountStream(remaining-size-8);using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {var encoding=writer.Encoding;try{writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(count);stack.itemValue.Write(writer);writer.Flush();}finally{writer.Encoding=encoding;}}
            size=checked(size+8+count.Count);if(size>remaining)throw new InvalidDataException("Action aggregate native frame exceeds bound");
        }
        var image=RebirthCamperInventorySnapshot.Capture(slots);if(image.Length!=size)throw new InvalidDataException("Native frame changed during measurement");remaining-=size;return image;
    }
    private sealed class ActionNpcBodyStream:MemoryStream
    {
        public override void Write(byte[] bytes,int offset,int count){if(count<0||Position+count>ushort.MaxValue)throw new InvalidDataException("Action NPC native body exceeds persisted bound");base.Write(bytes,offset,count);}
        public override void WriteByte(byte value){if(Position>=ushort.MaxValue)throw new InvalidDataException();base.WriteByte(value);}
    }
    private static bool[] ActionLocks(IRemoteResourceSource source,int length)
    {var locks=new bool[length];if(source?.SlotLocks!=null){if(source.SlotLocks.Length!=length)throw new InvalidDataException();for(int i=0;i<length;i++)locks[i]=source.SlotLocks[i];}return locks;}
    private static bool ActionOtherOwnerHold(EntityPlayer actor)=>RebirthNpcMetabolismCustody.HoldsPlayer(actor)||
        RebirthCamperStationNativeActorPayment.Blocks(actor.bag)||RebirthCamperStationNativeActorPayment.Blocks(actor.inventory)||
        actor is EntityPlayerLocal local&&(RebirthGearOwnerReservation.IsHeld(local)||RebirthRemoteResourceOwnerAction.IsHeld(local)||RemoteResourceClientTransactionCoordinator.HasPendingInventoryOperation(local));
    internal static bool TryBeginElectricalPayment(EntityPlayer actor,TileEntityPowered tile,RebirthElectricalServiceRecord candidate,IList<ItemStack> requirements,out ActionPayment payment,out string failure)
    {
        payment=null;failure="Electrical original payment is unavailable.";
        if(!ThreadManager.IsMainThread())return false;
        InstallActionPaymentNativeObservers();
        if(!ThreadManager.IsMainThread()||actor?.world?.IsRemote()!=false||actor.IsDead()||tile==null||candidate==null||!ReferenceEquals(actor.world.GetTileEntity(candidate.Position),tile)||
            !RebirthElectricalSourceIdentity.Matches(actor.world,candidate)||!RebirthStablePlayerIdentity.TryResolveServerEntity(actor,out var owner)||
            RebirthBackpackLibraryReservation.BlocksResourceUse(actor)||ActionOtherOwnerHold(actor)||actor is EntityPlayerLocal local&&(RebirthDogLifecycleService.IsHireInventoryHeld(local)||local.PlayerUI?.xui?.DragAndDropWindow?.IsEmpty()!=true))return false;
        string creation=RebirthCamperCharacterAccess.CurrentCreation(actor);if(string.IsNullOrWhiteSpace(creation)||creation.Length>1024)return false;
        foreach(var original in actionPayments)
            if(!original.Terminal&&!original.Retired&&(ReferenceEquals(original.Actor,actor)||ReferenceEquals(original.ServiceTarget,tile)))
            {payment=original;failure="The same original electrical payment is retained.";return original.Action=="electrical-service"&&ReferenceEquals(original.Actor,actor)&&ReferenceEquals(original.ServiceTarget,tile)&&original.Target==candidate.SourceIncarnation;}
        if(HasDetachedActionTarget(actor.world,candidate.Position)){failure="The original electrical service is awaiting its native owner continuation.";return false;}
        if(actionPayments.Count>=8||actionPayments.Count(value=>!value.Terminal&&!value.Retired)>=4||!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var memory))return false;
        ActionPayment prepared=null;
        try
        {
            var remoteOwner=actionRemoteAdmission;
            if(remoteOwner!=null&&(!RemoteActionOriginalCurrent(remoteOwner)||!ReferenceEquals(remoteOwner.Actor,actor)||!ReferenceEquals(remoteOwner.Target,tile)))return false;
            var plan=new Plan();var aggregated=AggregateRequirements(requirements,1);List<IRemoteResourceSource> sources=null;
            foreach(var required in aggregated.Values)
            {
                int needed=required.count;
                // Server proxy inventories never stand in for actual remote owner setters.
                if(actor is EntityPlayerLocal&&ReferenceEquals(actor.world.GetPrimaryPlayer(),actor))needed=PlanLocal(actor,required.itemValue,needed,plan);
                else if(remoteOwner!=null)needed=PlanActionRemoteOwner(actor,remoteOwner,required.itemValue,needed,plan);
                if(needed>0){sources??=RemoteResourceSourceDiscovery.ResolveNearby(actor.world,actor,RemoteResourcesRuntimePolicy.Radius);needed=PlanRemote(actor,required.itemValue,needed,plan,sources);}
                if(needed>0){failure="Electrical service requires "+required.itemValue.ItemClass.GetItemName()+"; remote owner-native contribution has not been granted.";return false;}
            }
            if(!ValidateActionPlan(actor,plan,remoteOwner,out failure))return false;
            var legs=new List<ActionLeg>();long remaining=2L*1024*1024,nativeMetadataRemaining=2L*1024*1024;
            long plannedGraphs=0,plannedFrames=0;
            bool ReserveFrameCopies(byte[] frame,int copies)
            {
                long graphs=checked(plannedGraphs+copies*MeasureActionRetainedFrame(frame)),frames=checked(plannedFrames+frame.LongLength);
                // Native/canonical metadata preparation and full XML measuring
                // remain temporary; decoded Before/After/Progress/Paid copies
                // are admitted by their actual item structure before hydration.
                long peak=Math.Max(128L*1024*1024,checked(graphs+16*frames+96L*1024*1024));
                if(!RebirthProductiveEarnedObservationBudget.TryResizeDetached(memory,peak))return false;
                plannedGraphs=graphs;plannedFrames=frames;return true;
            }
            ItemStack[] ownerBag=null,ownerBelt=null;
            if(plan.Local.Count!=0)
            {
                byte[] bag=CaptureActionFrame(remoteOwner?.BagBefore??actor.bag.ItemGrid.items,ref remaining),belt=CaptureActionFrame(remoteOwner?.BeltBefore??actor.inventory.ItemGrid.items,ref remaining);
                if(!ReserveFrameCopies(bag,plan.Local.Any(take=>!take.Toolbelt)?5:1)||!ReserveFrameCopies(belt,plan.Local.Any(take=>take.Toolbelt)?5:1))return false;
                ownerBag=RebirthCamperInventorySnapshot.Restore(bag);ownerBelt=RebirthCamperInventorySnapshot.Restore(belt);
            }
            foreach(bool belt in new[]{false,true})
            {
                var takes=plan.Local.Where(take=>take.Toolbelt==belt).ToArray();if(takes.Length==0)continue;
                var grid=remoteOwner==null?(belt?actor.inventory.ItemGrid:actor.bag.ItemGrid):null;
                var before=ItemStack.Clone(belt?ownerBelt:ownerBag);var after=ItemStack.Clone(before);
                foreach(var take in takes){after[take.Slot].count-=take.Count;if(after[take.Slot].count==0)after[take.Slot]=ItemStack.Empty.Clone();}
                var paid=takes.Select(take=>new ItemStack(before[take.Slot].itemValue.Clone(),take.Count)).ToArray();
                var ownerLocks=new bool[before.Length];if(grid?.SlotLocks!=null){if(grid.SlotLocks.Length!=ownerLocks.Length)throw new InvalidDataException("Owner lock dimensions changed");for(int i=0;i<ownerLocks.Length;i++)ownerLocks[i]=grid.SlotLocks[i];}
                if(before.Any(stack=>stack.count>ushort.MaxValue))throw new InvalidDataException("Owner counts exceed native persisted range");
                legs.Add(new ActionLeg(belt?"owner-belt":"owner-bag",null,before,after,ownerLocks,takes.Select(take=>take.Slot).ToArray(),paid,actor,grid,null,true,belt,0){RemoteOwner=remoteOwner!=null});
            }
            foreach(var group in plan.Remote.GroupBy(take=>take.Source.StableId,StringComparer.Ordinal))
            {
                var takes=group.ToArray();var source=takes[0].Source;byte[] frame=CaptureActionFrame(source.Slots,ref remaining);
                if(!ReserveFrameCopies(frame,4))return false;
                var before=RebirthCamperInventorySnapshot.Restore(frame);var after=ItemStack.Clone(before);
                foreach(var take in takes){after[take.Slot].count-=take.Count;if(after[take.Slot].count==0)after[take.Slot]=ItemStack.Empty.Clone();}
                object endpoint;ItemStackGrid grid=null;
                if(source is StaticContainerResourceSource container){endpoint=container.NativeTile;grid=(container.NativeTile as TileEntityComposite)?.GetFeature<TEFeatureStorage>()?.ItemGrid;}
                else if(source is WorkstationOutputResourceSource station)endpoint=station.NativeStation;
                else if(source is VehicleResourceSource vehicle){endpoint=vehicle.Vehicle;grid=vehicle.Vehicle?.bag?.ItemGrid;}
                else if(source is DroneResourceSource drone){endpoint=drone.Drone;grid=drone.Drone?.bag?.ItemGrid;}
                else if(source is RemoteNpcResourceSource npc)endpoint=npc.NativeActor;
                else throw new InvalidDataException("Unsupported original action source");
                if(endpoint==null)throw new InvalidDataException("Source native provider vanished");
                if(RebirthRemoteResourceSourceCustody.BlocksSource(source)||ActionBlocksNative(endpoint)||grid!=null&&ActionBlocksNative(grid)||
                    source is RemoteNpcResourceSource heldNpc&&(ActionBlocksNpc(heldNpc.NativeIdentity)||RebirthNpcMetabolismCustody.Blocks(heldNpc.NativeIdentity)))throw new InvalidDataException("Original source is already held by another operation");
                var leg=new ActionLeg(source.StableId,source,before,after,ActionLocks(source,before.Length),takes.Select(take=>take.Slot).ToArray(),takes.Select(take=>new ItemStack(before[take.Slot].itemValue.Clone(),take.Count)).ToArray(),endpoint,grid,null,false,false,source.Revision);
                if(leg.Npc!=null)
                {
                    if(leg.Npc.OriginalRevision>uint.MaxValue-2)throw new InvalidDataException("NPC revision exhausted");
                    if(!leg.Npc.IsDogStorage)
                    {
                        leg.NpcBefore=RebirthNpcInventoryTransactionService.GetSnapshot(leg.Npc.NativeIdentity);if(leg.NpcBefore.Revision!=leg.Npc.OriginalRevision)throw new InvalidDataException("Full NPC source changed");
                        if(leg.NpcBefore.NativeStacks!=null){if(!leg.NpcBefore.NativeStacks.TryMeasureObservationBytes(nativeMetadataRemaining,out long size))throw new InvalidDataException("Aggregate native NPC metadata exceeds original allocation");nativeMetadataRemaining-=size;}
                        leg.NpcAfter=ActionExpectedNpc(leg,true,false);
                    }
                    using(var body=new ActionNpcBodyStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(body);leg.Npc.NativeActor.Write(writer,StreamModeWrite.Persistency);writer.Flush();if(!leg.Npc.NativeActor.NativeIdentityReadable||body.Length<RebirthNpcNativeStableIdentityCodec.Size)throw new InvalidDataException("NPC native identity unavailable");leg.NpcBodyBefore=body.ToArray();}
                }
                else if(before.Any(stack=>stack.count>ushort.MaxValue))throw new InvalidDataException("Native source counts exceed persisted range");
                legs.Add(leg);
            }
            if(legs.Count>32)throw new InvalidDataException("Action participant bound exceeded");
            // Every possible debit/refund slot intent and actual return must fit
            // the causal journal before the first source can be touched.
            if(24+legs.Sum(leg=>leg.Npc!=null?8:4*leg.Slots.Length+4)>256)throw new InvalidDataException("Action slot plan exceeds complete settlement journal bound");
            prepared=new ActionPayment(actor,owner.CanonicalId,creation,Path.GetFullPath(GameIO.GetSaveGameDir()),legs,tile,candidate,memory,ownerBag,ownerBelt,remoteOwner);
            MeasurePreparedAction(prepared);
            if(!NarrowActionPaymentMemory(prepared))return false;
            if(!prepared.ActorCurrent()||!ValidateActionPlan(actor,plan,remoteOwner,out failure)||!RebirthInfrastructureWorkService.ReservePaidAction(prepared))return false;
            lock(actionOwnerGate)actionPayments.Add(prepared);payment=prepared;memory=null;failure="Electrical payment original reserved.";return true;
        }
        catch(Exception error){failure="Original electrical payment retained/refused: "+error.Message;return false;}
        finally{if(memory!=null&&prepared?.AdmissionReserved==true)RebirthInfrastructureWorkService.ReleasePaidAction(prepared);memory?.Dispose();}
    }
    private static string ActionFrame(ItemStack[] slots)=>Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(slots));
    private static XElement ElectricalCandidate(RebirthElectricalServiceRecord value)=>new XElement("electrical",new XAttribute("x",value.Position.x),new XAttribute("y",value.Position.y),new XAttribute("z",value.Position.z),new XAttribute("clr",value.ClrIdx),new XAttribute("kind",(byte)value.Kind),new XAttribute("condition",value.Condition),new XAttribute("workmanship",value.Workmanship),new XAttribute("time",value.LastServiceWorldTime),new XAttribute("count",value.ServiceCount),new XAttribute("block",value.SourceBlock),new XAttribute("incarnation",value.SourceIncarnation),new XAttribute("author",value.ConfiguredByStableId??""),new XAttribute("background",value.ConfiguredByBackgroundId??""),new XAttribute("bonus",value.ConfiguredByBonusId??""),new XAttribute("skill",value.ConfiguredSkillValue));
    private static XElement ActionOriginalImage(ActionPayment payment)
    {
        payment.ServiceBefore??=RebirthInfrastructureWorkService.CapturePaidActionBefore(payment);
        if(payment.ServiceBefore==null)throw new InvalidOperationException("Electrical original canonical Before unavailable");
        var image=new XElement("resourceActionOriginal",new XAttribute("version",1),new XAttribute("operation",payment.Operation.ToString("N")),new XAttribute("action",payment.Action),new XAttribute("account",payment.Account),new XAttribute("creation",payment.Creation),new XAttribute("deaths",payment.Deaths),new XAttribute("nativeId",payment.NativeEntityId),new XAttribute("save",payment.Save),new XAttribute("world",payment.NativeWorld),new XAttribute("recovery",payment.Recovery),ElectricalCandidate(payment.ServiceCandidate),
            new XElement(payment.ServiceBefore),ActionTrainingImage(payment.Operation,payment.TrainingRaw,payment.TrainingRequired),
            payment.OwnerBagBefore==null?null:new XElement("ownerOriginal",new XElement("bag",ActionFrame(payment.OwnerBagBefore)),new XElement("belt",ActionFrame(payment.OwnerBeltBefore))),
            new XElement("sources",payment.Legs.Select(leg=>new XElement("source",new XAttribute("id",leg.Id),new XAttribute("kind",leg.Local?0:(int)leg.Source.Kind),new XAttribute("revision",leg.Revision),new XAttribute("debit",leg.DebitMutation.ToString("N")),new XAttribute("return",leg.ReturnMutation.ToString("N")),new XAttribute("incarnation",leg.Native?.Incarnation??leg.Npc?.NativeIdentity.ToString()??"owner"),new XAttribute("belt",leg.Belt),new XElement("before",ActionFrame(leg.Before)),new XElement("after",ActionFrame(leg.After)),new XElement("locks",string.Concat(leg.Locks.Select(value=>value?'1':'0'))),new XElement("paid",ActionFrame(leg.Paid)),new XElement("takes",leg.Slots.Select((slot,index)=>new XElement("take",new XAttribute("slot",slot),new XAttribute("count",leg.Paid[index].count)))),ActionNpcSnapshot(leg.NpcBefore),leg.NpcBodyBefore==null?null:new XElement("nativeBody",new XAttribute("class",leg.Npc.NativeActor.entityClass),new XAttribute("stable",leg.Npc.NativeIdentity),Convert.ToBase64String(leg.NpcBodyBefore))))));
        return image;
    }
    private static void MeasurePreparedAction(ActionPayment payment)
    {
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var scratch))throw new InvalidDataException("Original preparation scratch unavailable");
        using(scratch)using(var count=new ActionCountStream(8L*1024*1024-32*32))using(var writer=System.Xml.XmlWriter.Create(count,new System.Xml.XmlWriterSettings{Encoding=new UTF8Encoding(false,true)})){ActionOriginalImage(payment).WriteTo(writer);writer.Flush();payment.OriginalBytesBound=checked(count.Count+32*32);}
    }
    private static void PrepareActionOriginal(ActionPayment payment)
    {
        if(payment.Original!=null){ActionRecord(payment,"prepared");RememberActionPendingTarget(payment);return;}
        if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(128L*1024*1024,out var scratch))throw new InvalidDataException("Original publication scratch unavailable");
        using(scratch)
        {
        var image=ActionOriginalImage(payment);
        using(var count=new ActionCountStream(8L*1024*1024))using(var writer=System.Xml.XmlWriter.Create(count,new System.Xml.XmlWriterSettings{Encoding=new UTF8Encoding(false,true)})){image.WriteTo(writer);writer.Flush();}
        payment.Original=image.ToString(SaveOptions.DisableFormatting);if(Encoding.UTF8.GetByteCount(payment.Original)>8*1024*1024)throw new InvalidDataException("Action original exceeds bound");
        }
        ActionPath(payment.Directory);Directory.CreateDirectory(payment.Directory);ActionRecord(payment,"prepared");RememberActionPendingTarget(payment);
    }
}
