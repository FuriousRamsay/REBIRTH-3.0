using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

// Read-only native observations. No Load, Prepare, original identity allocation,
// save, setter, owner offer, source advance or terminal acknowledgment occurs.
internal static class RebirthVehicleTransferAudit
{
    internal static JObject Capture(EntityPlayer player,Guid operation)
    {
        var result=new JObject{["schema"]=1,["available"]=false};
        try
        {
            if(!ThreadManager.IsMainThread()||player?.world==null||!ReferenceEquals(player.world,GameManager.Instance?.World)||
                !ReferenceEquals(player.world.GetEntity(player.entityId),player))return result;
            RebirthStablePlayerIdentity identity;
            if(player is EntityPlayerLocal)
            {if(!RebirthStablePlayerIdentity.TryFromLocalPlatform(out identity))return result;}
            else if(!RebirthStablePlayerIdentity.TryFromPersistentPlayer(GameManager.Instance.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(player.entityId),out identity))return result;
            var world=player.world;var game=GameManager.Instance;int deaths=player.Died;
            bool Current()=>ThreadManager.IsMainThread()&&ReferenceEquals(game,GameManager.Instance)&&ReferenceEquals(game.World,world)&&
                ReferenceEquals(player.world,world)&&ReferenceEquals(world.GetEntity(player.entityId),player)&&player.Died==deaths;
            result["world"]=world.worldState.Guid;result["owner"]=identity.CanonicalId;result["deaths"]=deaths;
            if(player is EntityPlayerLocal local)
            {
                result["ownerCustody"]=RebirthVehicleClientCustody.CaptureTransferAudit(local,operation);
                result["nativeOwner"]=new JObject{["bag"]=RebirthVehicleTransferCustody.Slots(local.bag.ItemGrid.items),["belt"]=RebirthVehicleTransferCustody.Slots(local.inventory.ItemGrid.items),
                    ["vehicleHold"]=RebirthVehicleOwnerCustody.BlocksInventory(local.bag,true)||RebirthVehicleOwnerCustody.BlocksInventory(local.inventory,false)};
            }
            if(operation!=Guid.Empty&&!world.IsRemote())
            {
                var store=new RebirthVehicleTransferPlanStore(GameIO.GetSaveGameDir(),GameIO.GetSaveGameDir,Current);var plan=store.Read(operation);
                if(plan!=null)
                {
                    if(plan.ActorId!=identity.CanonicalId||plan.NativeDeaths<0||plan.NativeDeaths>deaths||plan.NativeDeaths<deaths&&!store.HasCompletion(plan))return result;
                    string original=plan.ToXml().ToString(SaveOptions.DisableFormatting);
                    var source=new JObject{["original"]=original,["digest"]=RebirthVehicleTransferCustody.Hash(original),["fingerprint"]=plan.Fingerprint,
                        ["completionSaved"]=store.HasCompletion(plan),["rejectedCompletionSaved"]=store.HasRejectedCompletion(plan),
                        ["trainingRequired"]=plan.Training!=null,["trainingCompletionSaved"]=plan.Training!=null&&store.HasTrainingCompletion(plan)};
                    if(RebirthVehicleTransferResolver.TryObserve(world,plan,Current,out var actual,out var position,out var entity))
                    {
                        var native=entity<0?null:world.GetEntity(entity) as EntityVehicle;
                        source["nativeAssembly"]=new JObject{["payload"]=RebirthVehicleAssemblySerializer.ToBase64(actual),["entity"]=entity,["x"]=position.x,["y"]=position.y,["z"]=position.z,
                            ["fuelItem"]=native?.GetVehicle()?.GetFuelItem(),["fuelLevel"]=native?.vehicle?.GetFuelLevel(),["fuelCapacity"]=native?.vehicle?.GetMaxFuelLevel(),["health"]=native?.Health,["maxHealth"]=native?.GetMaxHealth()};
                    }
                    source["runtimeCurrent"]=RebirthVehicleTransferCoordinator.TryGet(operation,out var runtime);
                    source["runtimeFinished"]=runtime!=null&&runtime.Finished;result["source"]=source;
                    if(RebirthGearNativePlayerFile.TryRead(identity,out var file)&&file.deaths==deaths&&file.buffData!=null&&file.buffData.Length>0&&file.buffData.Length<=1024*1024&&
                        RebirthVehicleReceiptReader.TryRead(file.buffData.ToArray(),plan.Items.Select(item=>item.ReceiptId).Concat(new[]{operation}),out var receipts))
                    {
                        result["savedNativeOwner"]=new JObject{["bag"]=RebirthVehicleTransferCustody.Slots(RebirthGearPlayerDataInventory.ReadSlots(file,true)),
                            ["belt"]=RebirthVehicleTransferCustody.Slots(RebirthGearPlayerDataInventory.ReadSlots(file,false)),
                            ["receipts"]=new JArray(receipts.Select(pair=>new JObject{["receipt"]=pair.Key.ToString("N"),["stage"]=pair.Value.ToString()}))};
                    }
                }
            }
            result["available"]=Current();return result;
        }
        catch(Exception error){result["missingReason"]=error.GetType().Name;return result;}
    }
}
internal static partial class RebirthVehicleClientCustody
{
    internal static JObject CaptureTransferAudit(EntityPlayerLocal player,Guid operation)
    {
        if(!Scope(player,out var root,out var owner,out var world,out var creation)||unavailable||entries.Count>64||
            loadedScope!=null&&loadedScope!=DirectoryFor(root,owner,world,creation))return new JObject{["available"]=false};
        var rows=new JArray();
        foreach(var entry in entries.Values)
        {
            if(entry.Owner!=owner||entry.World!=world||entry.Creation!=creation)continue;
            rows.Add(new JObject{["operation"]=entry.Id.ToString("N"),["digest"]=FrameHash(entry.Frame),["original"]=entry.Frame,["phase"]=entry.Phase,["outcome"]=entry.Outcome,
                ["fingerprint"]=NetPackageRebirthVehicleAssemblyRequest.ReadCustodyFrame(entry.Frame).TransferFingerprint,
                ["current"]=Current(player,entry),["reserveReceipt"]=entry.ReserveReceipt.ToString("N"),["settleReceipt"]=entry.SettleReceipt.ToString("N")});
        }
        return new JObject{["available"]=true,["operations"]=rows,["selected"]=operation==Guid.Empty?null:rows.OfType<JObject>().SingleOrDefault(row=>(string)row["operation"]==operation.ToString("N"))};
    }
}
