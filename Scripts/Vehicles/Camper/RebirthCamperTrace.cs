using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

public static class RebirthCamperTrace
{
    private static JArray Inventory(ItemStack[] slots)
    {
        var result = new JArray();
        using (var hash = System.Security.Cryptography.SHA256.Create())
            for (int slot = 0; slots != null && slot < slots.Length; slot++)
            {
                var stack = slots[slot];
                bool empty = stack == null || stack.IsEmpty();
                result.Add(new JObject { ["slot"] = slot,
                    ["name"] = empty ? null : stack.itemValue.ItemClass.GetItemName(),
                    ["count"] = empty ? 0 : stack.count,
                    ["nativeHash"] = empty ? null : Convert.ToBase64String(hash.ComputeHash(
                        RebirthCamperInventorySnapshot.Capture(new[] { stack.Clone() }))) });
            }
        return result;
    }
    public static bool Enabled;
    private static JObject RecipeAdmission(Block block)
    {
        if(block==null)return null;
        string raw=block.Properties.Contains("Workstation","CraftingAreaRecipes")?
            block.Properties.GetString("Workstation","CraftingAreaRecipes"):block.GetBlockName();
        var areas=new JArray();
        if(!string.IsNullOrEmpty(raw))foreach(string area in raw.Split(','))areas.Add(area.Trim()=="player"?"":area.Trim());
        return new JObject{["block"]=block.GetBlockName(),["rawAreas"]=raw,["normalizedAreas"]=areas};
    }
    private static readonly Queue<JObject> events=new Queue<JObject>();
    public static void Write(string stage,EntityVehicle vehicle=null,string detail=null)
    {
        if(!Enabled)return;
        var entry=new JObject{["utc"]=DateTime.UtcNow.ToString("o"),["stage"]=stage,["vehicle"]=vehicle?.entityId??-1,["detail"]=detail??""};
        if(vehicle?.vehicle!=null)
        {
            string flag;vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata("RebirthCamperTransferPending",out flag);
            entry["metadataPending"]=flag;entry["lockOwner"]=RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle);
            entry["cargoSlots"]=vehicle.bag?.ItemGrid?.items.Length??-1;
        }
        events.Enqueue(entry);while(events.Count>256)events.Dequeue();
        Log.Out("[CamperTrace] "+entry.ToString(Newtonsoft.Json.Formatting.None));
    }
    public static JObject Snapshot(EntityVehicle vehicle)
    {
        var runtime=RebirthCamperModuleRuntime.For(vehicle);
        var player=vehicle.world?.GetPrimaryPlayer() as EntityPlayerLocal;
        var held=player==null?null:LocalPlayerUI.GetUIForPlayer(player)?.xui?.DragAndDropWindow;
        var timer=GameTimer.Instance;ulong nativeTicks=timer.ticks,nativeLoadedTicks=timer.ticksSincePlayfieldLoaded;
        var result=new JObject{["events"]=new JArray(events),["entity"]=vehicle.entityId,["pending"]=runtime.TransferPending,
            ["capacity"]=runtime.Storage.Capacity,["cargoSlots"]=vehicle.bag.ItemGrid.items.Length,
            ["lockOwner"]=RebirthCamperRemoteAdmission.NativeInteractionOwner(vehicle),
            ["inventorySchema"]=1,["vehicleIdentity"]=runtime.Identity.ToString("N"),
            ["nativeGameTicks"]=nativeTicks,["nativeTicksSinceWorldLoaded"]=nativeLoadedTicks,
            ["nativeClockAtWorldLoaded"]=nativeTicks>=nativeLoadedTicks?nativeTicks-nativeLoadedTicks:0,
            ["cargo"]=Inventory(vehicle.bag.ItemGrid.items)};
        result["cursorEmpty"]=held==null?JValue.CreateNull():new JValue(held.IsEmpty());
        result["localPlayerEntityId"]=player?.entityId??-1;
        result["localPlayerAttachedEntityId"]=player?.AttachedToEntity?.entityId??-1;
        result["vehicleDriverEntityId"]=vehicle.GetAttached(0)?.entityId??-1;
        var storage=new JArray();var stations=new JArray();int queuedJobs=0;
        for(int i=0;i<20;i++)
        {
            var item=runtime.Storage.GetSlot(i);if(item!=null)storage.Add(new JObject{["slot"]=i,["item"]=item.itemValue.ItemClass.GetItemName()});
            var installed=runtime.Stations.GetSlot(i);if(installed==null)continue;
            var queue=new JArray();foreach(var job in installed.State.Queue)if(job?.Recipe!=null)
                queue.Add(new JObject{["recipe"]=job.Recipe.GetName(),["remaining"]=job.CraftingTimeLeft,["count"]=job.Multiplier});
            queuedJobs+=queue.Count;
            stations.Add(new JObject{["slot"]=i,["id"]=installed.Id.ToString("N"),["block"]=installed.State.InstalledBlock.Block.GetBlockName(),["queue"]=queue,
                ["recipeAdmission"]=RecipeAdmission(installed.State.block),
                ["queueCapacity"]=installed.State.Queue?.Length??0,["outputCapacity"]=installed.State.Output?.Length??0,["fuelSeconds"]=installed.State.BurnTotalTimeLeft,
                ["burning"]=installed.State.IsBurning,["tools"]=Inventory(installed.State.Tools),
                ["fuel"]=Inventory(installed.State.Fuel),["input"]=Inventory(installed.State.Input),
                ["output"]=Inventory(installed.State.Output)});
        }
        result["storage"]=storage;result["stations"]=stations;
        result["storageCount"]=storage.Count;result["stationCount"]=stations.Count;
        result["queuedJobCount"]=queuedJobs;
        return result;
    }
}
