using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// The native open workstation queue completes in XUi, not AddCraftComplete. Observe confirmed
/// delivery (including the separate full-output retry), and validate remote notices against native
/// output/inventory replication. This retains the game's client-owned workstation inventory model;
/// it does not claim to replace that model with server-side ingredient simulation.
/// </summary>
internal static partial class RebirthWorkstationCraftCompletion
{
    internal sealed class Witness
    {
        internal EntityPlayerLocal Viewer;
        internal TileEntityWorkstation Tile;
        internal Recipe Recipe;
        internal int Owner, Before, QueueCount;
        internal long Serial;
        internal bool Retry;
    }
    internal sealed class Snapshot
    {
        internal World World;
        internal EntityPlayer Player;
        internal TileEntityWorkstation Tile;
        internal string NativeOutput,NativeBag,NativeBelt,NativeQueue,NativeEquipment;
        internal string NativeViewerKey,NativeViewerCreation;
    }
    private static World world;
    private static string session=Guid.NewGuid().ToString("N");
    private static long sequence,serial;
    [ThreadStatic] internal static int OutputDepth;
    private static float nextPump;
    internal static bool NativeLocalOwner(XUiC_RecipeStack entry)
    {
        var current=GameManager.Instance?.World;
        var viewer=entry?.xui?.playerUI?.entityPlayer;
        return current?.IsRemote()==false&&viewer!=null&&ReferenceEquals(viewer.world,current)&&
            ReferenceEquals(current.GetPrimaryPlayer(),viewer)&&entry.startingEntityId==viewer.entityId&&
            !RebirthRecipeOutputs.HasOutputs(entry.recipe);
    }
    internal static void Install(Harmony h)
    {
        RebirthRecipeOutputIntegration.Install();
        RebirthProductiveEarnedUiQueueOwner.Install();
        RebirthProductiveEarnedUiDeferredTransport.InstallSelectedSendObserver();
        RebirthProductiveEarnedUiDeferredTransport.InstallRecipientWindowObserver();
        RebirthWorkstationCompletionPipeline.InstallCachedCustody();
        RebirthProductiveEarnedUiOwnerFile.Install();
        RebirthProductiveEarnedCancellation.InstallBeforeFlushObserver();
        RebirthWorkstationReagentPublication.Install(h);
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWorkstationOutputPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWorkstationRetryPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWorkstationLocalRetryPatch));
        h.Patch(AccessTools.Method(typeof(NetPackageTileEntity),"ProcessPackage"),
            prefix:new HarmonyMethod(typeof(RebirthWorkstationCraftCompletion),nameof(BeforeTile)),
            postfix:new HarmonyMethod(typeof(RebirthWorkstationCraftCompletion),nameof(AfterSnapshot)));
        h.Patch(AccessTools.Method(typeof(NetPackagePlayerInventory),"ProcessPackage"),
            prefix:new HarmonyMethod(typeof(RebirthWorkstationCraftCompletion),nameof(BeforeInventory)),
            postfix:new HarmonyMethod(typeof(RebirthWorkstationCraftCompletion),nameof(AfterSnapshot)));
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(Pump));
    }
    private static void Ready(World current)
    {
        if(ReferenceEquals(world,current))return;
        RebirthWorkstationCompletionPipeline.SweepNativeHostWorld(current);
        world=current;
        session=Guid.NewGuid().ToString("N");sequence=serial=0;OutputDepth=0;
    }
    internal static Witness Before(XUiC_RecipeStack entry,bool retry)
    {
        if(!RebirthSurvivorMode.IsEnabledForCurrentWorld()||entry?.recipe==null||entry.AmountToRepair>0||
            entry.recipe.IsScrap||RebirthCookingBatch.IsBatch(entry.recipe)||
            string.IsNullOrEmpty(RebirthServiceCraftSkillService.ClassifyRecipe(entry.recipe))||
            !(entry.windowGroup?.Controller is XUiC_WorkstationWindowGroup group))return null;
        var viewer=entry.xui?.playerUI?.entityPlayer;var tile=group.WorkstationData?.TileEntity;
        if(viewer?.world==null||tile==null)return null;
        Ready(viewer.world);
        return new Witness{Viewer=viewer,Tile=tile,Recipe=entry.recipe,Owner=entry.startingEntityId,
            Before=Count(tile.Output,entry.recipe.itemValueType)+Count(viewer.bag.ItemGrid.items,entry.recipe.itemValueType)+
                Count(viewer.inventory.ItemGrid.items,entry.recipe.itemValueType),QueueCount=entry.recipeCount,Serial=serial,Retry=retry};
    }
    internal static void After(XUiC_RecipeStack entry,Witness w,bool success)
    {
        if(w==null||!success||!NativeLocalOwner(entry)||!ReferenceEquals(world,w.Viewer.world))return;
        if(w.Retry&&(serial!=w.Serial||entry.isInventoryFull||
            ReferenceEquals(entry.recipe,w.Recipe)&&entry.recipeCount>=w.QueueCount))return;
        int now=Count(w.Tile.Output,w.Recipe.itemValueType)+Count(w.Viewer.bag.ItemGrid.items,w.Recipe.itemValueType)+
            Count(w.Viewer.inventory.ItemGrid.items,w.Recipe.itemValueType);
        if(now-w.Before<w.Recipe.count)return;
        ++serial;
        Credit(world.GetEntity(w.Owner) as EntityPlayer,w.Tile,w.Recipe,"wc1:"+session+":"+(++sequence));
    }
    private static int Count(ItemStack[] slots,int type)=>slots==null?0:slots.Where(s=>s!=null&&!s.IsEmpty()&&s.itemValue.type==type).Sum(s=>s.count);
    private static EntityPlayer Sender(NetPackage packet,World current)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ThreadManager.IsMainThread()||current==null||current.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,current)||
            !RebirthSurvivorMode.IsEnabledForCurrentWorld()||packet?.Sender==null||connection==null||!connection.IsServer||
            !ReferenceEquals(connection.Clients?.ForEntityId(packet.Sender.entityId),packet.Sender))return null;
        Ready(current);
        var player=current.GetEntity(packet.Sender.entityId) as EntityPlayer;
        return player!=null&&ReferenceEquals(player.world,current)?player:null;
    }
    private static void BeforeTile(NetPackageTileEntity __instance,World _world,Vector3i ___teWorldPos,out Snapshot __state)
    {
        __state=null;var player=Sender(__instance,_world);
        var tile=_world?.GetTileEntity(___teWorldPos) as TileEntityWorkstation;
        if(player==null||tile==null||(player.position-tile.ToWorldCenterPos()).sqrMagnitude>144f)return;
        __state=new Snapshot{World=_world,Player=player,Tile=tile};if(RebirthStablePlayerIdentity.TryFromClientInfo(__instance.Sender,out var actualViewer))__state.NativeViewerKey=actualViewer.StorageKey;CaptureNativeSnapshot(__state);
    }
    private static void BeforeInventory(NetPackagePlayerInventory __instance,World _world,out Snapshot __state)
    {
        __state=null;var player=Sender(__instance,_world);if(player==null)return;
        __state=new Snapshot{World=_world,Player=player};if(RebirthStablePlayerIdentity.TryFromClientInfo(__instance.Sender,out var actualViewer))__state.NativeViewerKey=actualViewer.StorageKey;CaptureNativeSnapshot(__state);
    }
    private static void AfterSnapshot(Snapshot __state,bool __runOriginal)
    {
        if(!__runOriginal||__state==null||!ThreadManager.IsMainThread()||
            !ReferenceEquals(world,__state.World)||!ReferenceEquals(GameManager.Instance?.World,__state.World)||
            !ReferenceEquals(__state.Player?.world,__state.World)||
            !ReferenceEquals(__state.World.GetEntity(__state.Player.entityId),__state.Player)||
            __state.Tile!=null&&!ReferenceEquals(__state.World.GetTileEntity(__state.Tile.ToWorldPos()),__state.Tile))return;
        ObserveAcceptedNativeSnapshot(__state);
        Drain();
    }
    internal static string Key(Recipe r)
    {
        string raw=r.GetName()+"|"+r.craftingArea+"|"+r.count+"|"+
            string.Join(",",r.ingredients.Select(i=>i.itemValue.type+":"+i.count));
        using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-","");
    }
    internal static void Receive(EntityPlayer viewer,int ownerId,Vector3i position,string key,string receipt)
    {
        // V1 count-only notices cannot create a durable completion original.
    }
    private static bool Credit(EntityPlayer owner,TileEntityWorkstation tile,Recipe recipe,string receipt)
    {
        if(owner==null||!RebirthSkillAwardService.TryGetEligible(owner,out var identity,out var record))return false;
        if(record.Progression.SkillAwardReceipts.Contains(receipt))
            return RebirthWorldCharacterRepository.SaveIfDirty(identity,"workstation-completion-retry");
        string skill=RebirthServiceCraftSkillService.ClassifyRecipe(recipe);
        if(string.IsNullOrEmpty(skill))return true;
        var model=RebirthCraftTrainingRules.BuildModel(owner,recipe,skill);
        if(!RebirthSkillAwardService.TryAwardCraft(owner,skill,model,1,1f,"workstation-craft:"+recipe.GetName(),receipt))return false;
        RebirthStatisticsService.RecordItemsCrafted(owner,recipe.GetName(),recipe.count);
        RebirthMedicalLootCraftSignatureService.OnSuccessfulWorkstationCraft(tile,owner.entityId,recipe.GetName(),recipe.count);
        return true;
    }
    private static void Drain()
    {
        RebirthWorkstationCompletionPipeline.Pump();
    }
    private static void Pump(ref ModEvents.SGameUpdateData data)
    {
        if(UnityEngine.Time.realtimeSinceStartup<nextPump)return;nextPump=UnityEngine.Time.realtimeSinceStartup+1;
        Ready(GameManager.Instance?.World);if(world==null)return;Drain();
    }
}

[HarmonyPatch(typeof(XUiC_RecipeStack),"outputStack")]
internal static class RebirthWorkstationOutputPatch
{
    private static bool Prefix(XUiC_RecipeStack __instance,out RebirthWorkstationCraftCompletion.Witness __state)
    {
        bool local=RebirthWorkstationCraftCompletion.NativeLocalOwner(__instance);
        __state=local?RebirthWorkstationCraftCompletion.Before(__instance,false):null;
        if(__state!=null)++RebirthWorkstationCraftCompletion.OutputDepth;
        return local||RebirthWorkstationCompletionPipeline.AllowOutput(__instance);
    }
    private static void Postfix(XUiC_RecipeStack __instance,bool __result,RebirthWorkstationCraftCompletion.Witness __state)
    {RebirthWorkstationCraftCompletion.After(__instance,__state,__result);}
    private static void Finalizer(RebirthWorkstationCraftCompletion.Witness __state)
    {if(__state!=null)--RebirthWorkstationCraftCompletion.OutputDepth;}
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"Update")]
internal static class RebirthWorkstationRetryPatch
{
    private static bool Prefix(XUiC_RecipeStack __instance,out RebirthWorkstationCompletionPipeline.Frame __state)
    {if(RebirthWorkstationCraftCompletion.NativeLocalOwner(__instance)){__state=null;return true;}return RebirthWorkstationCompletionPipeline.Begin(__instance,out __state);}
    private static void Postfix(XUiC_RecipeStack __instance,bool __runOriginal,RebirthWorkstationCompletionPipeline.Frame __state)
    {RebirthWorkstationCompletionPipeline.After(__instance,__runOriginal,__state);}
    private static Exception Finalizer(XUiC_RecipeStack __instance,bool __runOriginal,Exception __exception,RebirthWorkstationCompletionPipeline.Frame __state)
    {RebirthWorkstationCompletionPipeline.End(__instance,__runOriginal,__state);return __exception;}
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"Update")]
internal static class RebirthWorkstationLocalRetryPatch
{
    private static void Prefix(XUiC_RecipeStack __instance,out RebirthWorkstationCraftCompletion.Witness __state)
    {__state=RebirthWorkstationCraftCompletion.NativeLocalOwner(__instance)&&__instance.isInventoryFull?
        RebirthWorkstationCraftCompletion.Before(__instance,true):null;}
    private static void Postfix(XUiC_RecipeStack __instance,RebirthWorkstationCraftCompletion.Witness __state)
    {RebirthWorkstationCraftCompletion.After(__instance,__state,true);}
}
[Preserve]
public sealed class NetPackageRebirthStationCompletion:NetPackage
{
    private int viewer,owner;private Vector3i position;private string key,receipt;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthStationCompletion Setup(int v,int o,Vector3i p,string k,string r)
    {viewer=v;owner=o;position=p;key=k;receipt=r;return this;}
    public override void write(PooledBinaryWriter writer)
    {base.write(writer);var w=(BinaryWriter)writer;w.Write(viewer);w.Write(owner);w.Write(position.x);w.Write(position.y);w.Write(position.z);w.Write(key);w.Write(receipt);}
    public override void read(PooledBinaryReader reader)
    {var r=(BinaryReader)reader;viewer=r.ReadInt32();owner=r.ReadInt32();position=new Vector3i(r.ReadInt32(),r.ReadInt32(),r.ReadInt32());key=r.ReadString();receipt=r.ReadString();}
    public override void ProcessPackage(World world,GameManager callbacks)
    {if(world!=null&&!world.IsRemote()&&ValidEntityIdForSender(viewer))RebirthWorkstationCraftCompletion.Receive(world.GetEntity(viewer) as EntityPlayer,owner,position,key,receipt);}
    public int GetLength()=>0;
}
