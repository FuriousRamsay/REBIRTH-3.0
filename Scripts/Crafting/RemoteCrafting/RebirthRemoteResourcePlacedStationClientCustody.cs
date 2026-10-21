using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Fresh local UI custody only. Cold lease/object replacement and terminal
// release require the separate authenticated protocol, not raw Before equality.
// No installation/capture caller exists until full placed source qualification.
internal static partial class RebirthRemoteResourcePlacedStationClientCustody
{
    private sealed class Hold
    {
        internal EntityPlayerLocal Player;internal World World;internal Chunk Chunk;internal TileEntityWorkstation Station;
        internal string Directory;internal RebirthRemoteResourceClientOriginal.Intent Original;
        internal bool Current()=>ReferenceEquals(GameManager.Instance?.World,World)&&World.IsRemote()&&Directory==GameIO.GetSaveGameDir()&&
            Player.IsSpawned()&&!Player.IsDead()&&Player.Died==Original.Deaths&&ReferenceEquals(World.GetPrimaryPlayer(),Player)&&
            ReferenceEquals(World.GetEntity(Player.entityId),Player)&&ReferenceEquals(World.GetTileEntity(Station.ToWorldPos()),Station)&&
            ReferenceEquals(Station.GetChunk(),Chunk)&&Chunk!=null&&!Chunk.InProgressUnloading&&
            RebirthRemoteResourceClientOriginal.Current(Player,Original);
    }
    private static readonly Dictionary<Guid,Hold> held=new Dictionary<Guid,Hold>();
    private sealed class Capture
    {
        internal ItemActionEntryCraft Entry;internal RebirthRemoteResourceClientOriginal.Intent Original;internal TileEntityWorkstation Station;internal Chunk Chunk;
        internal bool Current()=>Original!=null&&RebirthRemoteResourceClientOriginal.Current(Entry?.ItemController?.xui?.playerUI?.entityPlayer,Original)&&
            RebirthRemoteResourcePlacedStationScope.TryObserveWindow(Entry.ItemController.xui.playerUI.entityPlayer,Entry,out var actual,out _)&&ReferenceEquals(actual,Station)&&ReferenceEquals(Station.GetChunk(),Chunk);
    }
    private static readonly Dictionary<Guid,Capture> fresh=new Dictionary<Guid,Capture>();
    private static bool installed;
    internal static bool IsInstalled=>installed;
    internal static bool RememberActualCapture(ItemActionEntryCraft entry,RebirthRemoteResourceClientOriginal.Intent original)
    {
        if(!installed||entry==null||original==null||fresh.Count>=32||fresh.ContainsKey(original.Operation)||
            !RebirthRemoteResourcePlacedStationScope.TryObserveWindow(entry.ItemController?.xui?.playerUI?.entityPlayer,entry,out var station,out _))return false;
        fresh.Add(original.Operation,new Capture{Entry=entry,Original=original,Station=station,Chunk=station.GetChunk()});return true;
    }
    internal static bool Blocks(TileEntityWorkstation station)=>RebirthRemoteResourcePlacedStationOriginal.CancellationBlocks(station)||station!=null&&
        (held.Values.Any(value=>ReferenceEquals(value.Station,station)&&value.Current())||fresh.Values.Any(value=>ReferenceEquals(value.Station,station)&&value.Current()));
    internal static bool OwnsOriginal(EntityPlayerLocal player,RebirthRemoteResourceClientOriginal.Intent original)
        =>original!=null&&held.TryGetValue(original.Operation,out var hold)&&ReferenceEquals(hold.Player,player)&&hold.Original.Digest==original.Digest&&hold.Current()&&
            LockManager.Instance?.IsLockedByLocalPlayer(hold.Station,0)==true;
    internal static bool TryHoldFresh(EntityPlayerLocal player,ItemActionEntryCraft entry,RebirthRemoteResourceClientOriginal.Intent original)
    {
        try
        {
            // Retire transient references only. Every original remains in its
            // durable client journal; this is no financial terminal/release.
            foreach(var stale in held.Where(pair=>!pair.Value.Current()).Select(pair=>pair.Key).ToArray())held.Remove(stale);
            if(!installed||!RebirthRemoteResourcePlacedStationOriginal.IsCandidate(original)||
                !RebirthRemoteResourceClientOriginal.Current(player,original)||!RebirthRemoteResourceOwnerAction.HoldsOriginalScope(player,original)||
                player.Buffs.GetCustomVar(RebirthRemoteResourceOwnerAction.Receipt(original.Operation))!=10f||
                !RebirthRemoteResourcePlacedStationOriginal.ValidateInput(original,out var position,out uint block,out _,out _,out _,out _,out _,out _,out _,out _,out var input)||
                !RebirthRemoteResourcePlacedStationScope.TryObserveWindow(player,entry,out var station,out _)||station.ToWorldPos()!=position||station.blockValue.rawData!=block||
                input.Length!=station.Input.Length||input.Where((item,index)=>!RebirthStationGridIngredients.IsSameStackSnapshot(item,station.Input[index])).Any())return false;
            if(held.TryGetValue(original.Operation,out var prior))return prior.Original.Digest==original.Digest&&ReferenceEquals(prior.Station,station)&&prior.Current();
            if(!fresh.TryGetValue(original.Operation,out var captured)||!ReferenceEquals(captured.Entry,entry)||!ReferenceEquals(captured.Original,original)||
                !ReferenceEquals(captured.Station,station)||!ReferenceEquals(captured.Chunk,station.GetChunk()))return false;
            if(held.Count>=32||held.Values.Any(value=>ReferenceEquals(value.Player,player)&&value.Current()))return false;
            if(!RebirthRemoteResourceClientOriginal.TryRead(original.Operation,out var saved)||saved.Digest!=original.Digest)return false;
            var queue=RebirthCookingRemoteAdmission.ReadProtocolXml(original.Copy().Element("action").Element("queue").Value,RebirthRemoteResourcePlacedStationQueue.MaximumBytes);
            if(queue.Element("native")?.Value!=RebirthCookingRemoteAdmissionBackend.Queue(station.Queue))return false;
            var hold=new Hold{Player=player,World=player.world,Chunk=station.GetChunk(),Station=station,Directory=GameIO.GetSaveGameDir(),Original=original};
            if(!hold.Current())return false;held.Add(original.Operation,hold);fresh.Remove(original.Operation);return true;
        }
        catch{return false;}
    }
    internal static void Install()
    {
        if(installed)return;var harmony=new Harmony("rebirth.remote.resources.placed.client.custody.v1");
        try{foreach(var patch in new[]{typeof(Model),typeof(WindowSync),typeof(WindowTick),typeof(Incoming),typeof(Queue),typeof(NativeTick),typeof(StationWrite)})RebirthHarmonyBootstrap.PatchClassOnce(harmony,patch);installed=true;}
        catch{harmony.UnpatchSelf();throw;}
    }
    [HarmonyPatch]
    private static class StationWrite
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(string name in new[]{"Input","Output","Tools","Fuel","Queue","IsBurning"})
                yield return AccessTools.PropertySetter(typeof(TileEntityWorkstation),name)??throw new MissingMethodException(name);
            yield return AccessTools.Method(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.ResetTickTime))??throw new MissingMethodException();
        }
        [HarmonyPriority(Priority.First)]private static bool Prefix(TileEntityWorkstation __instance,MethodBase __originalMethod,object[] __args)
            =>!Blocks(__instance)||ConsumeHydration(__instance,__originalMethod,__args)||RebirthRemoteResourcePlacedStationOriginal.ConsumeCancellationClientSetter(__instance,__originalMethod,__args)||
            RebirthRemoteResourcePlacedStationOriginal.ConsumeBeforeFlushSetter(__instance,__originalMethod,__args);
    }
    [HarmonyPatch]
    private static class Model
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(string name in new[]{"Input","Output","Tool","Fuel"})
            {
                yield return Required("Set"+name+"Stacks",typeof(ItemStack[]));
                yield return Required("Set"+name+"InSlot",typeof(int),typeof(ItemStack));
            }
            yield return Required("SetRecipeQueueItems",typeof(RecipeQueueItem[]));yield return Required("SetQueueInSlot",typeof(int),typeof(RecipeQueueItem));
            yield return Required("SetIsBurning",typeof(bool));
        }
        private static MethodBase Required(string name,params Type[] arguments)=>AccessTools.Method(typeof(XUiM_Workstation),name,arguments)??throw new MissingMethodException(name);
        [HarmonyPriority(Priority.First)]private static bool Prefix(XUiM_Workstation __instance)=>!Blocks(__instance?.TileEntity)||RebirthRemoteResourcePlacedStationOriginal.IsExecutingBeforeFlush(__instance?.TileEntity);
    }
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),"syncTEfromUI")]
    private static class WindowSync
    {[HarmonyPriority(Priority.First)]private static bool Prefix(XUiC_WorkstationWindowGroup __instance)=>!Blocks(__instance?.WorkstationData?.TileEntity)||RebirthRemoteResourcePlacedStationOriginal.IsExecutingBeforeFlush(__instance);}
    [HarmonyPatch(typeof(XUiC_WorkstationWindowGroup),nameof(XUiC_WorkstationWindowGroup.Update))]
    private static class WindowTick
    {[HarmonyPriority(Priority.First)]private static bool Prefix(XUiC_WorkstationWindowGroup __instance)=>!Blocks(__instance?.WorkstationData?.TileEntity);}
    [HarmonyPatch(typeof(NetPackageTileEntity),nameof(NetPackageTileEntity.ProcessPackage))]
    private static class Incoming
    {
        [HarmonyPriority(Priority.First)]private static bool Prefix(NetPackageTileEntity __instance,World _world)
            =>_world?.IsRemote()!=true||!ReferenceEquals(GameManager.Instance?.World,_world)||
            !(_world.GetTileEntity(__instance.teWorldPos) is TileEntityWorkstation station)||!Blocks(station);
    }
    [HarmonyPatch]
    private static class Queue
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(XUiC_RecipeStack),nameof(XUiC_RecipeStack.Update))??throw new MissingMethodException();
            yield return AccessTools.Method(typeof(XUiC_RecipeStack),"HandleOnPress")??throw new MissingMethodException();
        }
        [HarmonyPriority(Priority.First)]private static bool Prefix(XUiC_RecipeStack __instance)
            =>!Blocks(__instance?.GetParentByType<XUiC_WorkstationWindowGroup>()?.WorkstationData?.TileEntity);
    }
    [HarmonyPatch]
    private static class NativeTick
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach(string name in new[]{nameof(TileEntityWorkstation.UpdateTick),nameof(TileEntityWorkstation.HandleRecipeQueue),nameof(TileEntityWorkstation.HandleMaterialInput),nameof(TileEntityWorkstation.HandleFuel)})
                yield return AccessTools.Method(typeof(TileEntityWorkstation),name)??throw new MissingMethodException(name);
        }
        [HarmonyPriority(Priority.First)]private static bool Prefix(TileEntityWorkstation __instance)=>!Blocks(__instance);
    }
}
