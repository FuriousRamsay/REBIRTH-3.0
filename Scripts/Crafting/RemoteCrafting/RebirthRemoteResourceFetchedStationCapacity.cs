using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;

// Exact native serializer measurement of a detached candidate only. Neither
// this item nor its detached Bag is adopted into a live entity/inventory.
internal static class RebirthRemoteResourceFetchedStationCapacity
{
    private sealed class Scope{internal EntityVehicle Entity;internal Vehicle Vehicle;internal Bag ActualBag,CandidateBag;internal ItemValue Item;internal int ItemReads,BagWrites;}
    [ThreadStatic]private static Scope scope;
    private static bool installed;
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.remote-resource.fetched-station.capacity");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthFetchedStationCapacityItem));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony,typeof(RebirthFetchedStationCapacityBag));installed=true;
    }
    private static byte[] StationManifest(RebirthCamperModuleRuntime runtime,RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(0x314D4352u);writer.Write(runtime.Identity.ToByteArray());writer.Write(runtime.Stations.Revision);writer.Write(RebirthCamperCraftingStation.ModuleSlots);bool found=false;
            for(int slot=0;slot<RebirthCamperCraftingStation.ModuleSlots;slot++)
            {
                var installation=runtime.Stations.GetSlot(slot);writer.Write(installation!=null);if(installation==null)continue;
                writer.Write(installation.Id.ToByteArray());writer.Flush();
                using(var native=MemoryPools.poolBinaryWriter.AllocSync(true)){native.SetBaseStream(stream);installation.InstalledItem.itemValue.Write(native);native.Flush();}
                byte[] state;
                if(!RebirthCamperStationAddress.TryParse(plan.Address,out var address))throw new InvalidDataException();
                if(installation.Id==address.Installation){if(found||RebirthNativeItemCodec.Encode(installation.InstalledItem.itemValue)!=plan.InstalledDefinition)throw new InvalidDataException();found=true;state=plan.CopyAfterStation();}
                else state=RebirthCamperStationSnapshot.Capture(installation.Id,installation.State);
                writer.Write(state.Length);writer.Write(state);
            }
            if(!found)throw new InvalidDataException();writer.Flush();return stream.ToArray();
        }
    }
    private static string Modules(RebirthCamperModuleRuntime runtime,RebirthRemoteResourceFetchedStationPlan.Plan plan)
    {
        byte[] storage=runtime.Storage.Capture(),stations=StationManifest(runtime,plan);
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {writer.Write(0x314D5652u);writer.Write(runtime.Identity.ToByteArray());writer.Write(storage.Length);writer.Write(storage);writer.Write(stations.Length);writer.Write(stations);writer.Flush();return Convert.ToBase64String(stream.ToArray());}
    }
    private static bool PlannedCargo(RebirthRemoteResourceDurableJournal.Record root,EntityVehicle vehicle,ItemValue candidate,out ItemStack[] after)
    {
        after=ItemStack.Clone(vehicle.bag.ItemGrid.items);bool found=false;
        foreach(var node in root.Copy().Element("sources").Elements("source"))
        {
            int kind=(int)node.Attribute("kind");if(kind!=(int)RemoteResourceSourceKind.VehicleStorage||node.Element("npc")!=null)continue;
            string id=(string)node.Attribute("id");
            // CurrentIncarnation's native GetUpdatedItemValue mutates the live
            // item. Measurement uses only the existing item and exact ID.
            if(id==null||!id.StartsWith("V:",StringComparison.Ordinal)||!int.TryParse(id.Substring(2),NumberStyles.None,CultureInfo.InvariantCulture,out int entity)||
                entity<0||"V:"+entity.ToString(CultureInfo.InvariantCulture)!=id)return false;
            if(!ReferenceEquals(vehicle.world.GetEntity(entity),vehicle))continue;
            if(!RebirthRemoteResourceNativeOriginal.TryRead(RebirthRemoteResourceSourceEffectCodec.Domain.RemoteResource,root.Owner,root.Operation,id,out var original)||
                original.Kind!=RemoteResourceSourceKind.VehicleStorage||vehicle.IsDead()||
                !vehicle.vehicle.itemValue.TryGetMetadata("rebirth.logistics.incarnation",out string incarnation)||incarnation!=original.Incarnation||
                !Guid.TryParseExact(incarnation,"N",out var token)||token==Guid.Empty)return false;
            if(found)return false;found=true;
            if(!RebirthCamperInventorySnapshot.Capture(after).SequenceEqual(RebirthCamperInventorySnapshot.Capture(original.CopyStage(0))))return false;
            byte[] prior;
            if(candidate.HasMetadata(RebirthRemoteResourceSourceEffectCodec.ItemMetadataKey))
            {if(!candidate.TryGetMetadata(RebirthRemoteResourceSourceEffectCodec.ItemMetadataKey,out string text)||text.Length>32768)return false;prior=Convert.FromBase64String(text);}
            else prior=RebirthRemoteResourceSourceEffectCodec.Empty(original.Incarnation);
            bool Terminal(RebirthRemoteResourceSourceEffectCodec.Effect effect)=>RebirthRemoteResourceNativeOriginal.TryRead(effect.Purpose,effect.Owner,effect.Operation,id,out var previous)&&
                previous.Binding==effect.Binding&&previous.Incarnation==original.Incarnation&&previous.StageDigest(effect.Stage)==effect.Delta&&previous.SavedTerminal();
            if(!RebirthRemoteResourceSourceEffectCodec.TryRetire(prior,original.Incarnation,Terminal,out var pruned)||
                !RebirthRemoteResourceSourceEffectCodec.TryRead(pruned,original.Incarnation,out var entries)||entries.Any(effect=>effect.Purpose==original.Domain&&effect.Operation==original.Operation)||
                !RebirthRemoteResourceSourceEffectCodec.TryPlan(pruned,original.Incarnation,original.Domain,original.Operation,original.Owner,original.Binding,1,original.StageDigest(1),out var next))return false;
            candidate.SetMetadata(RebirthRemoteResourceSourceEffectCodec.ItemMetadataKey,Convert.ToBase64String(next));after=original.CopyStage(1);
        }
        return true;
    }
    internal static bool TryMeasure(EntityVehicle vehicle,RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceFetchedStationPlan.Plan plan,out int entityBytes)
    {
        entityBytes=0;
        try
        {
            if(!installed||scope!=null||!ThreadManager.IsMainThread()||vehicle?.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,vehicle.world)||
                !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||root?.Phase>1||root==null||plan==null||root.Operation!=plan.Source||root.Binding!=plan.Binding||
                root.Copy().Element("sources").Elements("source").Any(node=>(int)node.Attribute("attempted")!=0)||!RebirthCamperModuleRuntime.TryGetExisting(vehicle,out var runtime))return false;
            string liveItem=RebirthNativeItemCodec.Encode(vehicle.vehicle.itemValue);
            byte[] liveCargo=RebirthCamperInventorySnapshot.Capture(vehicle.bag.ItemGrid.items);
            var item=vehicle.vehicle.itemValue.Clone();item.UseTimes=(int)vehicle.Stats.Health.BaseMax-vehicle.Health;item.Meta=(int)(vehicle.vehicle.GetFuelLevel()*50f);
            item.SetMetadata("RebirthCamperIdentity",runtime.Identity.ToString("N"));item.SetMetadata("RebirthCamperModules",Modules(runtime,plan));item.SetMetadata("RebirthCamperTransferPending",runtime.TransferPending?"1":"0");
            if(!PlannedCargo(root,vehicle,item,out var cargo)||item.Metadata?.Count>byte.MaxValue)return false;
            Bag detached;
            using(var bytes=new MemoryStream())
            {
                using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(bytes);vehicle.bag.Write(writer,StreamModeWrite.Persistency);writer.Flush();}
                bytes.Position=0;using(var reader=MemoryPools.poolBinaryReader.AllocSync(true)){reader.SetBaseStream(bytes);detached=Bag.Read(reader,vehicle.bag.StackLocation,StreamModeRead.Persistency,null);if(bytes.Position!=bytes.Length)return false;}
            }
            if(ReferenceEquals(detached,vehicle.bag)||ReferenceEquals(detached.ItemGrid,vehicle.bag.ItemGrid)||detached.ItemGrid.items.Length!=cargo.Length)return false;
            for(int i=0;i<cargo.Length;i++)detached.ItemGrid.items[i]=cargo[i].Clone();
            var measuring=new Scope{Entity=vehicle,Vehicle=vehicle.vehicle,ActualBag=vehicle.bag,CandidateBag=detached,Item=item};scope=measuring;
            EntityCreationData record;
            try{record=new EntityCreationData(vehicle,StreamModeWrite.Persistency);}finally{scope=null;}
            if(measuring.ItemReads!=1||measuring.BagWrites!=2||record.entityData==null||record.bagData==null||record.entityData.Length>ushort.MaxValue||
                !ReferenceEquals(GameManager.Instance?.World,vehicle.world)||!ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId),vehicle)||
                RebirthNativeItemCodec.Encode(vehicle.vehicle.itemValue)!=liveItem||!RebirthCamperInventorySnapshot.Capture(vehicle.bag.ItemGrid.items).SequenceEqual(liveCargo))return false;
            entityBytes=checked((int)record.entityData.Length);return true;
        }
        catch{scope=null;return false;}
    }
    internal static ItemStack[] GetItems(Vehicle vehicle)
    {
        var current=scope;if(current==null||!ReferenceEquals(current.Vehicle,vehicle))return vehicle.GetItems();
        if(!ThreadManager.IsMainThread()||++current.ItemReads!=1)throw new InvalidDataException("Native candidate item serializer scope changed.");
        return new[]{new ItemStack(current.Item.Clone(),1)};
    }
    internal static bool WriteBag(Bag actual,PooledBinaryWriter writer,StreamModeWrite mode)
    {
        var current=scope;if(current==null||!ReferenceEquals(actual,current.ActualBag))return true;
        if(!ThreadManager.IsMainThread()||++current.BagWrites>2)throw new InvalidDataException("Native candidate cargo serializer scope changed.");
        current.CandidateBag.Write(writer,mode);return false;
    }
}

[HarmonyPatch(typeof(EntityVehicle),nameof(EntityVehicle.WriteSyncData))]
internal static class RebirthFetchedStationCapacityItem
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> input)
    {
        var target=AccessTools.Method(typeof(Vehicle),nameof(Vehicle.GetItems));var replacement=AccessTools.Method(typeof(RebirthRemoteResourceFetchedStationCapacity),nameof(RebirthRemoteResourceFetchedStationCapacity.GetItems));int count=0;
        foreach(var instruction in input)
        {if(instruction.Calls(target)){instruction.opcode=OpCodes.Call;instruction.operand=replacement;count++;}yield return instruction;}
        if(count!=1)throw new MissingMethodException("Exact native vehicle item serialization call-site changed.");
    }
}
[HarmonyPatch(typeof(Bag),nameof(Bag.Write))]
internal static class RebirthFetchedStationCapacityBag
{
    private static bool Prefix(Bag __instance,PooledBinaryWriter bw,StreamModeWrite _eStreamMode)=>RebirthRemoteResourceFetchedStationCapacity.WriteBag(__instance,bw,_eStreamMode);
}
