using System;
using System.Collections.Generic;
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
internal static class RebirthWorkstationCraftCompletion
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
        internal EntityPlayer Player;
        internal TileEntityWorkstation Tile;
        internal Dictionary<int,int> Counts;
    }
    private sealed class Addition { internal int Count; internal float Until; }
    private sealed class Pending
    {
        internal EntityPlayer Viewer, Owner;
        internal TileEntityWorkstation Tile;
        internal Recipe Recipe;
        internal string Receipt;
        internal float Until;
    }
    private static readonly Dictionary<string,Addition> Additions=new Dictionary<string,Addition>();
    private static readonly Dictionary<string,Pending> PendingReceipts=new Dictionary<string,Pending>();
    private static readonly Dictionary<string,Recipe> Recipes=new Dictionary<string,Recipe>();
    private static readonly Dictionary<string,float> QueueOwners=new Dictionary<string,float>();
    private static World world;
    private static string session=Guid.NewGuid().ToString("N");
    private static long sequence,serial;
    [ThreadStatic] internal static int OutputDepth;
    private static float nextPump;
    internal static void Install(Harmony h)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWorkstationOutputPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(h,typeof(RebirthWorkstationRetryPatch));
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
        world=current; Additions.Clear();PendingReceipts.Clear();Recipes.Clear();QueueOwners.Clear();
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
        if(w==null||!success||!ReferenceEquals(world,w.Viewer.world))return;
        if(w.Retry&&(serial!=w.Serial||entry.isInventoryFull||
            ReferenceEquals(entry.recipe,w.Recipe)&&entry.recipeCount>=w.QueueCount))return;
        int now=Count(w.Tile.Output,w.Recipe.itemValueType)+Count(w.Viewer.bag.ItemGrid.items,w.Recipe.itemValueType)+Count(w.Viewer.inventory.ItemGrid.items,w.Recipe.itemValueType);
        if(now-w.Before<w.Recipe.count)return;
        ++serial;
        string receipt="wc1:"+session+":"+(++sequence);
        if(!world.IsRemote())
        {
            Credit(world.GetEntity(w.Owner) as EntityPlayer,w.Tile,w.Recipe,receipt);return;
        }
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        // Force native snapshots before the reliable notice, before the output can be moved/used.
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageTileEntity>().Setup(w.Tile,StreamModeWrite.ToServer));
        connection.SendToServer(NetPackageManager.GetPackage<NetPackagePlayerInventory>().Setup(w.Viewer,true,true,false,false));
        connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthStationCompletion>().Setup(
            w.Viewer.entityId,w.Owner,w.Tile.ToWorldPos(),Key(w.Recipe),receipt));
    }
    private static int Count(ItemStack[] slots,int type)=>slots==null?0:slots.Where(s=>s!=null&&!s.IsEmpty()&&s.itemValue.type==type).Sum(s=>s.count);
    private static Dictionary<int,int> Counts(Snapshot s)
    {
        var result=new Dictionary<int,int>();
        var slots=s.Tile!=null?s.Tile.Output:s.Player.bag.ItemGrid.items.Concat(s.Player.inventory.ItemGrid.items).ToArray();
        foreach(var item in slots)
            if(item!=null&&!item.IsEmpty()){result.TryGetValue(item.itemValue.type,out int old);result[item.itemValue.type]=old+item.count;}
        return result;
    }
    private static string Pool(EntityPlayer p,TileEntityWorkstation tile,int type)=>p.entityId+":"+(tile==null?"inventory":tile.ToWorldPos().ToString())+":"+type;
    private static string OwnerKey(TileEntityWorkstation tile,int owner,Recipe recipe)=>tile.ToWorldPos()+":"+owner+":"+Key(recipe);
    private static EntityPlayer Sender(NetPackage packet,World current)
    {
        if(current==null||current.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return null;
        Ready(current);
        return packet.Sender==null?null:current.GetEntity(packet.Sender.entityId) as EntityPlayer;
    }
    private static void BeforeTile(NetPackageTileEntity __instance,World _world,Vector3i ___teWorldPos,out Snapshot __state)
    {
        __state=null;var player=Sender(__instance,_world);
        var tile=_world?.GetTileEntity(___teWorldPos) as TileEntityWorkstation;
        if(player==null||tile==null||(player.position-tile.ToWorldCenterPos()).sqrMagnitude>144f)return;
        __state=new Snapshot{Player=player,Tile=tile};__state.Counts=Counts(__state);
        RememberQueue(tile);
    }
    private static void RememberQueue(TileEntityWorkstation tile)
    {
        if(tile.Queue==null)return;
        foreach(var q in tile.Queue)
            if(q?.Recipe!=null&&q.Multiplier>0&&!RebirthCookingBatch.IsBatch(q.Recipe)&&QueueOwners.Count<512)
                QueueOwners[OwnerKey(tile,q.StartingEntityId,q.Recipe)]=UnityEngine.Time.realtimeSinceStartup+120;
    }
    private static void BeforeInventory(NetPackagePlayerInventory __instance,World _world,out Snapshot __state)
    {
        __state=null;var player=Sender(__instance,_world);if(player==null)return;
        __state=new Snapshot{Player=player};__state.Counts=Counts(__state);
    }
    private static void AfterSnapshot(Snapshot __state)
    {
        if(__state==null)return;
        if(__state.Tile!=null)RememberQueue(__state.Tile);
        foreach(var value in Counts(__state))
        {
            __state.Counts.TryGetValue(value.Key,out int old);int added=value.Value-old;if(added<=0)continue;
            string key=Pool(__state.Player,__state.Tile,value.Key);
            if(!Additions.TryGetValue(key,out var a)||a.Until<UnityEngine.Time.realtimeSinceStartup)a=new Addition();
            a.Count=Math.Min(3276700,a.Count+added);a.Until=UnityEngine.Time.realtimeSinceStartup+120;
            if(Additions.Count<2048||Additions.ContainsKey(key))Additions[key]=a;
        }
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
        if(viewer?.world==null||viewer.world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        Ready(viewer.world);
        if(receipt==null||receipt.Length>100||!receipt.StartsWith("wc1:",StringComparison.Ordinal)||key==null||key.Length!=64)return;
        var parts=receipt.Split(':');
        if(parts.Length!=3||!Guid.TryParseExact(parts[1],"N",out _)||!long.TryParse(parts[2],out long number)||number<1)return;
        var tile=world.GetTileEntity(position) as TileEntityWorkstation;var owner=world.GetEntity(ownerId) as EntityPlayer;
        if(tile==null||owner==null||(viewer.position-tile.ToWorldCenterPos()).sqrMagnitude>144f)return;
        if(Recipes.Count==0)foreach(var r in CraftingManager.GetAllRecipes())if(!r.IsScrap&&!RebirthCookingBatch.IsBatch(r))Recipes[Key(r)]=r;
        if(!Recipes.TryGetValue(key,out var recipe)||string.IsNullOrEmpty(recipe.craftingArea)||recipe.count<1||recipe.count>32767)return;
        if(ownerId!=viewer.entityId&&(!QueueOwners.TryGetValue(OwnerKey(tile,ownerId,recipe),out float until)||until<UnityEngine.Time.realtimeSinceStartup))return;
        if(!RebirthCapabilityService.EvaluateRecipe(owner,recipe.GetName()).IsAllowed)return;
        string id=viewer.entityId+":"+receipt;
        if(PendingReceipts.ContainsKey(id)||PendingReceipts.Count>=256)return;
        PendingReceipts[id]=new Pending{Viewer=viewer,Owner=owner,Tile=tile,Recipe=recipe,Receipt=receipt,Until=UnityEngine.Time.realtimeSinceStartup+120};
        Drain();
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
        float now=UnityEngine.Time.realtimeSinceStartup;
        foreach(var pair in PendingReceipts.ToArray())
        {
            var p=pair.Value;
            if(p.Until<now||!ReferenceEquals(world.GetEntity(p.Viewer.entityId),p.Viewer)){PendingReceipts.Remove(pair.Key);continue;}

            Additions.TryGetValue(Pool(p.Viewer,p.Tile,p.Recipe.itemValueType),out var a);
            if(a==null||a.Until<now||a.Count<p.Recipe.count)Additions.TryGetValue(Pool(p.Viewer,null,p.Recipe.itemValueType),out a);
            if(a==null||a.Until<now||a.Count<p.Recipe.count)continue;
            // Remove before sending owner-state events, which can synchronously re-enter on a host.
            PendingReceipts.Remove(pair.Key);
            if(Credit(p.Owner,p.Tile,p.Recipe,p.Receipt))a.Count-=p.Recipe.count;
            else PendingReceipts[pair.Key]=p; // Keep confirmed delivery available for a transient save/owner retry.
        }
    }
    private static void Pump(ref ModEvents.SGameUpdateData data)
    {
        if(UnityEngine.Time.realtimeSinceStartup<nextPump)return;nextPump=UnityEngine.Time.realtimeSinceStartup+1;
        Ready(GameManager.Instance?.World);if(world==null||world.IsRemote())return;Drain();
        foreach(var k in Additions.Where(p=>p.Value.Until<UnityEngine.Time.realtimeSinceStartup).Select(p=>p.Key).ToArray())Additions.Remove(k);
        foreach(var k in QueueOwners.Where(p=>p.Value<UnityEngine.Time.realtimeSinceStartup).Select(p=>p.Key).ToArray())QueueOwners.Remove(k);
    }
}

[HarmonyPatch(typeof(XUiC_RecipeStack),"outputStack")]
internal static class RebirthWorkstationOutputPatch
{
    private static void Prefix(XUiC_RecipeStack __instance,out RebirthWorkstationCraftCompletion.Witness __state)
    {__state=RebirthWorkstationCraftCompletion.Before(__instance,false);if(__state!=null)++RebirthWorkstationCraftCompletion.OutputDepth;}
    private static void Postfix(XUiC_RecipeStack __instance,bool __result,RebirthWorkstationCraftCompletion.Witness __state)
    {RebirthWorkstationCraftCompletion.After(__instance,__state,__result);}
    private static void Finalizer(RebirthWorkstationCraftCompletion.Witness __state)
    {if(__state!=null)--RebirthWorkstationCraftCompletion.OutputDepth;}
}
[HarmonyPatch(typeof(XUiC_RecipeStack),"Update")]
internal static class RebirthWorkstationRetryPatch
{
    private static void Prefix(XUiC_RecipeStack __instance,out RebirthWorkstationCraftCompletion.Witness __state)
    {__state=__instance.isInventoryFull?RebirthWorkstationCraftCompletion.Before(__instance,true):null;}
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
