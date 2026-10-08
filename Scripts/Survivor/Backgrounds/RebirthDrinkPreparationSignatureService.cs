using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

#nullable disable

public sealed class RebirthPreparedDrinkProfile
{
    public readonly string ItemName;
    public readonly string BuffName;
    public readonly float SkillZeroDurationSeconds;
    public readonly float SkillHundredDurationSeconds;
    public readonly string LegacyIdentity;
    public RebirthPreparedDrinkProfile(string itemName, string buffName, float zeroSeconds, float hundredSeconds, string legacyIdentity)
    {
        ItemName=itemName??string.Empty; BuffName=buffName??string.Empty;
        SkillZeroDurationSeconds=Math.Max(0f,zeroSeconds); SkillHundredDurationSeconds=Math.Max(SkillZeroDurationSeconds,hundredSeconds);
        LegacyIdentity=legacyIdentity??string.Empty;
    }
}

public sealed class RebirthPendingPreparedDrinkCraft
{
    public int PlayerId;
    public int ItemType;
    public ushort Seed;
    public ushort Quality;
    public string RecipeName=string.Empty;
    public DateTime CreatedUtc;
}

public sealed class RebirthPreparedDrinkCorrection
{
    public int PlayerId;
    public ItemValue Corrected;
    public DateTime CreatedUtc;
    public RebirthPreparedDrinkCorrection(int playerId, ItemValue corrected) { PlayerId=playerId; Corrected=corrected!=null?corrected.Clone():null; CreatedUtc=DateTime.UtcNow; }
}

/// <summary>
/// PC024 / Chunk K authority for Drink Preparation and Bartender Master Mixologist provenance.
/// Only audited 2.6 special identities missing from current 3.0 are migrated here. Native/current
/// drink behavior continues through the metabolism preserved-use bridge and is not duplicated.
/// </summary>
public static class RebirthDrinkPreparationSignatureService
{
    public const string SkillId="skill.drink_preparation";
    public const string MasterMixologistBonusId="background_bonus.master_mixologist";
    public const string PreparedDrinkKind="prepared_drink";

    private const double PendingSeconds=120.0;
    private static readonly object Gate=new object();
    private static DateTime NextCleanupUtc = DateTime.MinValue;
    private static readonly Dictionary<string,RebirthPreparedDrinkProfile> Profiles=new Dictionary<string,RebirthPreparedDrinkProfile>(StringComparer.OrdinalIgnoreCase)
    {
        // 2.6 baseline -> old Iron Gut/Slow Metabolism maximum. Chunk K replaces that consumer-side
        // dependency with a 0..100 Drink Preparation craft-time interpolation.
        {"drinkJarBeer",new RebirthPreparedDrinkProfile("drinkJarBeer","buffRebirthPreparedBeerPainResistance",60f,360f,"2.6 +15 PhysicalDamageResist")},
        {"drinkJarRedTea",new RebirthPreparedDrinkProfile("drinkJarRedTea","buffRebirthPreparedRedTeaMobility",300f,600f,"2.6 +15 CarryCapacity, +50% Walk/Run")},
        {"drinkJarGoldenRodTea",new RebirthPreparedDrinkProfile("drinkJarGoldenRodTea","buffRebirthPreparedGoldenrodMining",300f,600f,"2.6 +100% mining damage to stone/metal")},
        // Cooling already exists in current/native Yucca behavior; only the missing 2.6 field-work
        // damage identity is restored to avoid double-applying HyperthermalResist.
        {"drinkJarYuccaJuice",new RebirthPreparedDrinkProfile("drinkJarYuccaJuice","buffRebirthPreparedYuccaWork",300f,600f,"2.6 +105% earth/wood tool damage; cooling retained current/native")}
    };
    private static readonly Dictionary<int,List<RebirthPendingPreparedDrinkCraft>> Pending=new Dictionary<int,List<RebirthPendingPreparedDrinkCraft>>();
    private static readonly Queue<RebirthPreparedDrinkCorrection> Corrections=new Queue<RebirthPreparedDrinkCorrection>();
    private static readonly List<RebirthPreparedDrinkCorrection> ClientPending=new List<RebirthPreparedDrinkCorrection>();
    private static bool installed,localCraftPatched,workstationCraftPatched,inventoryCommitPatched;

    public static string Install(Harmony harmony)
    {
        if(installed)return "[REBIRTH Drink Preparation] already installed";
        installed=true;
        if(harmony!=null)
        {
            MethodInfo giveExp=AccessTools.DeclaredMethod(typeof(XUiC_RecipeStack),"giveExp",new[]{typeof(ItemValue),typeof(ItemClass)});
            if(giveExp!=null){harmony.Patch(giveExp,prefix:new HarmonyMethod(typeof(RebirthDrinkPreparationSignatureService),nameof(LocalCraftPrefix)));localCraftPatched=true;}
            MethodInfo complete=AccessTools.Method(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.AddCraftComplete),new[]{typeof(int),typeof(ItemValue),typeof(string),typeof(string),typeof(int),typeof(int)});
            if(complete!=null){harmony.Patch(complete,prefix:new HarmonyMethod(typeof(RebirthDrinkPreparationSignatureService),nameof(WorkstationCraftPrefix)));workstationCraftPatched=true;}
            Type packageType=AccessTools.TypeByName("NetPackagePlayerInventory");
            MethodInfo process=packageType!=null?AccessTools.Method(packageType,"ProcessPackage",new[]{typeof(World),typeof(GameManager)}):null;
            if(process!=null){harmony.Patch(process,postfix:new HarmonyMethod(typeof(RebirthDrinkPreparationSignatureService),nameof(NativeInventoryCommitPrefix)));inventoryCommitPatched=true;}
        }
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Drink Preparation] installed profiles="+Profiles.Count+" localCraft="+localCraftPatched+" workstationCraft="+workstationCraftPatched+" inventoryCommit="+inventoryCommitPatched;
    }

    public static void LocalCraftPrefix(XUiC_RecipeStack __instance,ItemValue _iv,ItemClass _ic)
    {
        if(__instance==null||_iv==null||_iv.ItemClass==null||__instance.recipe==null||__instance.AmountToRepair>0)return;
        EntityPlayer player=__instance.xui!=null&&__instance.xui.playerUI!=null?__instance.xui.playerUI.entityPlayer:null;
        if(player==null||player.world==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        string recipeName=__instance.recipe.GetName();
        if(!IsEligibleSpecialDrinkRecipe(recipeName,_iv))return;
        if(player.world.IsRemote())
        {
            // Metabolism-owned drinks are stack size 1, so no client prestack projection is needed.
            // The server stamps the exact seed/quality candidate and returns its authoritative item.
            ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(c!=null)c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthPreparedDrinkCraftRequest>().Setup(player.entityId,_iv.type,_iv.Seed,_iv.Quality,recipeName));
        }
        else StampPreparedDrink(player,recipeName,_iv,true);
    }

    public static void WorkstationCraftPrefix(TileEntityWorkstation __instance,int crafterEntityID,ItemValue itemCrafted,string recipeName,int craftedCount)
    {
        if(craftedCount<=0||itemCrafted==null||GameManager.Instance==null||GameManager.Instance.World==null||GameManager.Instance.World.IsRemote())return;
        EntityPlayer player=GameManager.Instance.World.GetEntity(crafterEntityID) as EntityPlayer;
        if(player!=null)StampPreparedDrink(player,recipeName,itemCrafted,true);
    }

    public static void RegisterCraftRequest(EntityPlayer player,int itemType,ushort seed,ushort quality,string recipeName)
    {
        if(player==null||player.world==null||player.world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        Recipe recipe=null;try{recipe=CraftingManager.GetRecipe(recipeName??string.Empty);}catch{}
        if(recipe==null||recipe.itemValueType!=itemType||!string.Equals(RebirthServiceCraftSkillService.ClassifyRecipe(recipe),SkillId,StringComparison.OrdinalIgnoreCase))return;
        ItemClass output=ItemClass.GetForId(itemType);if(output==null||!Profiles.ContainsKey(output.GetItemName()??string.Empty))return;
        lock(Gate)
        {
            CleanupLocked();List<RebirthPendingPreparedDrinkCraft> list;
            if(!Pending.TryGetValue(player.entityId,out list))Pending[player.entityId]=list=new List<RebirthPendingPreparedDrinkCraft>();
            list.RemoveAll(delegate(RebirthPendingPreparedDrinkCraft p){return p!=null&&p.ItemType==itemType&&p.Seed==seed&&p.Quality==quality&&string.Equals(p.RecipeName,recipeName??string.Empty,StringComparison.OrdinalIgnoreCase);});
            list.Add(new RebirthPendingPreparedDrinkCraft{PlayerId=player.entityId,ItemType=itemType,Seed=seed,Quality=quality,RecipeName=recipeName??string.Empty,CreatedUtc=DateTime.UtcNow});
        }
    }

    public static void NativeInventoryCommitPrefix(object __instance,World _world)
    {
        if(__instance==null||!IsServerWorld(_world))return;
        object sender=ReadMember(__instance,"Sender");int playerId=ReadInt(sender,"entityId",-1);if(playerId<0)playerId=ReadInt(sender,"EntityId",-1);if(playerId<0)return;
        EntityPlayer player=_world.GetEntity(playerId) as EntityPlayer;if(player==null)return;
        List<RebirthPendingPreparedDrinkCraft> pending=GetPendingSnapshot(playerId);if(pending.Count==0)return;
        ItemStack[] tool=player.inventory!=null?player.inventory.ItemGrid.items:null;ItemStack[] bag=player.bag!=null?player.bag.ItemGrid.items:null;
        for(int i=0;i<pending.Count;i++)
        {
            RebirthPendingPreparedDrinkCraft q=pending[i];ItemValue candidate=FindCandidate(tool,bag,q);if(candidate==null)continue;
            if(!StampPreparedDrink(player,q.RecipeName,candidate,true))continue;RemovePending(q);lock(Gate)Corrections.Enqueue(new RebirthPreparedDrinkCorrection(playerId,candidate));
        }
    }

    public static bool StampPreparedDrink(EntityPlayer player,string recipeName,ItemValue item,bool authoritative)
    {
        if(player==null||item==null||item.ItemClass==null||player.world==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        if(authoritative&&player.world.IsRemote())return false;
        RebirthPreparedDrinkProfile profile;if(!TryGetProfile(item.ItemClass.GetItemName(),out profile))return false;
        if(!string.Equals(RebirthServiceCraftSkillService.ClassifyRecipe(recipeName),SkillId,StringComparison.OrdinalIgnoreCase))return false;
        float skill=0f;RebirthServiceCraftSkillService.TryGetSkillValue(player,SkillId,out skill);skill=Mathf.Clamp(skill,0f,100f);
        float duration=Mathf.Lerp(profile.SkillZeroDurationSeconds,profile.SkillHundredDurationSeconds,skill/100f);
        bool bartender=RebirthBackgroundBonusService.HasBonus(player,MasterMixologistBonusId);
        if(bartender)duration*=Mathf.Clamp(RebirthFoodFarmingButcherySignatureService.GetBonusTuning(MasterMixologistBonusId,"special_effect_duration_multiplier",2f),1f,4f);
        RebirthProvenanceAuthorSnapshot author;if(!RebirthProvenanceIdentity.TryCapture(player,out author))return false;
        RebirthItemProvenanceAdapter.Stamp(item,new RebirthItemProvenanceSnapshot
        {
            Version=RebirthItemProvenanceAdapter.CurrentVersion,Kind=PreparedDrinkKind,SourceId=recipeName??string.Empty,
            CreatorStableId=author.StablePlayerId,BackgroundId=author.BackgroundId,BonusId=bartender?MasterMixologistBonusId:string.Empty,
            SkillId=SkillId,SkillValue=skill,Workmanship=Mathf.Clamp(duration,1f,7200f),OriginalMaxUseTimes=Math.Max(0f,item.MaxUseTimes),BatchToken=string.Empty
        });
        return true;
    }

    public static float GetPreparedSpecialEffectDurationSeconds(ItemValue item)
    {
        RebirthItemProvenanceSnapshot p;if(!RebirthItemProvenanceAdapter.TryRead(item,out p)||p==null)return 0f;
        if(!string.Equals(p.Kind,PreparedDrinkKind,StringComparison.OrdinalIgnoreCase)||!string.Equals(p.SkillId,SkillId,StringComparison.OrdinalIgnoreCase))return 0f;
        RebirthPreparedDrinkProfile profile;string itemName=item!=null&&item.ItemClass!=null?item.ItemClass.GetItemName():string.Empty;
        return TryGetProfile(itemName,out profile)?Mathf.Clamp(p.Workmanship,1f,7200f):0f;
    }

    public static bool TryActivateSpecialEffect(EntityPlayer consumer,string itemName,float durationSeconds)
    {
        if(consumer==null||consumer.world==null||consumer.world.IsRemote()||consumer.Buffs==null||durationSeconds<=0f||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        RebirthPreparedDrinkProfile profile;if(!TryGetProfile(itemName,out profile)||string.IsNullOrEmpty(profile.BuffName))return false;
        // 7DTD's explicit-duration AddBuff overload preserves replace/nonstacking semantics while
        // avoiding legacy consumer CVars and old Slow Metabolism/Iron Gut duration dependencies.
        consumer.Buffs.AddBuff(profile.BuffName,-1,false,false,Mathf.Clamp(durationSeconds,1f,7200f));
        return true;
    }

    public static bool TryGetProfile(string itemName,out RebirthPreparedDrinkProfile profile){return Profiles.TryGetValue(itemName??string.Empty,out profile);}
    private static bool IsEligibleSpecialDrinkRecipe(string recipeName,ItemValue item){RebirthPreparedDrinkProfile p;return item!=null&&item.ItemClass!=null&&TryGetProfile(item.ItemClass.GetItemName(),out p)&&string.Equals(RebirthServiceCraftSkillService.ClassifyRecipe(recipeName),SkillId,StringComparison.OrdinalIgnoreCase);}

    public static string RunVectors()
    {
        StringBuilder b=new StringBuilder();b.AppendLine("[REBIRTH Chunk K Drink Preparation Vectors]");
        foreach(RebirthPreparedDrinkProfile p in Profiles.Values)b.AppendLine(p.ItemName+": skill0="+p.SkillZeroDurationSeconds.ToString("0",CultureInfo.InvariantCulture)+"s skill100="+p.SkillHundredDurationSeconds.ToString("0",CultureInfo.InvariantCulture)+"s bartenderMax="+(p.SkillHundredDurationSeconds*2f).ToString("0",CultureInfo.InvariantCulture)+"s | "+p.LegacyIdentity);
        b.AppendLine("coffee: audited 2.6 stamina/warmth identity retained through current coffee/metabolism behavior; no duplicate migrated buff.");
        b.AppendLine("onset: first intestinal fluid absorption only; provenance follows consumer handoff and partial container state.");
        return b.ToString().TrimEnd();
    }

    public static string BuildDebugReport(EntityPlayer player)
    {
        float skill=0f;if(player!=null)RebirthServiceCraftSkillService.TryGetSkillValue(player,SkillId,out skill);
        return "[REBIRTH Chunk K Drink Preparation] skill="+skill.ToString("0.###",CultureInfo.InvariantCulture)+" bartender="+(player!=null&&RebirthBackgroundBonusService.HasBonus(player,MasterMixologistBonusId))+" profiles="+Profiles.Count;
    }

    public static void ApplyClientCorrection(int playerId,ItemValue corrected)
    {
        if(corrected==null||GameManager.Instance==null||GameManager.Instance.World==null)return;EntityPlayerLocal local=GameManager.Instance.World.GetPrimaryPlayer();if(local==null||local.entityId!=playerId)return;
        if(!ReplacePlayerItem(local,corrected))lock(Gate)ClientPending.Add(new RebirthPreparedDrinkCorrection(playerId,corrected));
    }

    private static ItemValue FindCandidate(ItemStack[] a,ItemStack[] b,RebirthPendingPreparedDrinkCraft p){ItemValue v=FindCandidate(a,p);return v??FindCandidate(b,p);}
    private static ItemValue FindCandidate(ItemStack[] slots,RebirthPendingPreparedDrinkCraft p)
    {
        if(slots==null||p==null)return null;for(int i=0;i<slots.Length;i++){ItemValue v=slots[i]!=null?slots[i].itemValue:null;if(v==null||v.type!=p.ItemType||v.Seed!=p.Seed||v.Quality!=p.Quality)continue;RebirthItemProvenanceSnapshot existing;if(RebirthItemProvenanceAdapter.TryRead(v,out existing)&&existing!=null&&string.Equals(existing.Kind,PreparedDrinkKind,StringComparison.OrdinalIgnoreCase))continue;return v;}return null;
    }
    private static bool ReplacePlayerItem(EntityPlayer player,ItemValue corrected)
    {
        if(player==null||corrected==null)return false;ItemStack[] t=player.inventory!=null?player.inventory.ItemGrid.items:null;if(ReplaceIn(player,t,true,corrected))return true;ItemStack[] b=player.bag!=null?player.bag.ItemGrid.items:null;return ReplaceIn(player,b,false,corrected);
    }
    private static bool ReplaceIn(EntityPlayer player,ItemStack[] slots,bool toolbelt,ItemValue corrected)
    {
        if(slots==null)return false;
        for(int i=0;i<slots.Length;i++)
        {
            ItemValue v=slots[i]!=null?slots[i].itemValue:null;
            if(v==null||v.type!=corrected.type||v.Seed!=corrected.Seed||v.Quality!=corrected.Quality)continue;
            ItemValue merged=v.Clone();
            if(!RebirthItemProvenanceAdapter.TryApplyOwnedCorrection(merged,corrected))continue;
            ItemStack s=slots[i];s.itemValue=merged;if(toolbelt)player.inventory.SetItem(i,s);else player.bag.SetSlot(i,s);return true;
        }
        return false;
    }
    private static bool IsServerWorld(WorldBase world){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;return world!=null&&!world.IsRemote()&&c!=null&&c.IsServer&&RebirthSurvivorMode.IsEnabledForCurrentWorld();}
    private static object ReadMember(object target,string name){if(target==null)return null;Type t=target.GetType();BindingFlags f=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;FieldInfo fi=t.GetField(name,f);if(fi!=null)return fi.GetValue(target);PropertyInfo pi=t.GetProperty(name,f);return pi!=null?pi.GetValue(target,null):null;}
    private static int ReadInt(object target,string name,int fallback){object o=ReadMember(target,name);if(o==null)return fallback;try{return Convert.ToInt32(o,CultureInfo.InvariantCulture);}catch{return fallback;}}
    private static List<RebirthPendingPreparedDrinkCraft> GetPendingSnapshot(int playerId){lock(Gate){CleanupLocked();List<RebirthPendingPreparedDrinkCraft> list;return Pending.TryGetValue(playerId,out list)?new List<RebirthPendingPreparedDrinkCraft>(list):new List<RebirthPendingPreparedDrinkCraft>();}}
    private static void RemovePending(RebirthPendingPreparedDrinkCraft p){lock(Gate){List<RebirthPendingPreparedDrinkCraft> list;if(p!=null&&Pending.TryGetValue(p.PlayerId,out list)){list.Remove(p);if(list.Count==0)Pending.Remove(p.PlayerId);}}}
    private static void CleanupLocked(){DateTime now=DateTime.UtcNow;if(now<NextCleanupUtc)return;NextCleanupUtc=now.AddSeconds(1);DateTime cutoff=now.AddSeconds(-PendingSeconds);List<int> empty=new List<int>();foreach(KeyValuePair<int,List<RebirthPendingPreparedDrinkCraft>> kv in Pending){kv.Value.RemoveAll(delegate(RebirthPendingPreparedDrinkCraft p){return p==null||p.CreatedUtc<cutoff;});if(kv.Value.Count==0)empty.Add(kv.Key);}for(int i=0;i<empty.Count;i++)Pending.Remove(empty[i]);ClientPending.RemoveAll(delegate(RebirthPreparedDrinkCorrection x){return x==null||x.CreatedUtc<cutoff;});}
    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c!=null&&c.IsServer){for(;;){RebirthPreparedDrinkCorrection x;lock(Gate){if(Corrections.Count==0)break;x=Corrections.Dequeue();}EntityPlayer p=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetEntity(x.PlayerId) as EntityPlayer:null;if(p is EntityPlayerLocal)ApplyClientCorrection(x.PlayerId,x.Corrected);else if(x.Corrected!=null)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthPreparedDrinkCraftCorrection>().Setup(x.PlayerId,x.Corrected),_attachedToEntityId:x.PlayerId);}}
        lock(Gate){for(int i=ClientPending.Count-1;i>=0;i--){RebirthPreparedDrinkCorrection x=ClientPending[i];EntityPlayerLocal local=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;if(local!=null&&local.entityId==x.PlayerId&&ReplacePlayerItem(local,x.Corrected))ClientPending.RemoveAt(i);}CleanupLocked();}
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){ClearRuntime();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){ClearRuntime();}
    private static void ClearRuntime(){lock(Gate){Pending.Clear();Corrections.Clear();ClientPending.Clear();}}
}
