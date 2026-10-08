using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

#nullable disable

public sealed class RebirthPendingMetalCraftSignature
{
    public int PlayerId;
    public int ItemType;
    public ushort Seed;
    public ushort Quality;
    public string RecipeName = string.Empty;
    public DateTime CreatedUtc;
}

public sealed class RebirthMetalCraftCorrection
{
    public int PlayerId;
    public ItemValue Corrected;
    public DateTime CreatedUtc;
    public RebirthMetalCraftCorrection(int playerId, ItemValue corrected) { PlayerId=playerId; Corrected=corrected!=null?corrected.Clone():null; CreatedUtc=DateTime.UtcNow; }
}

/// <summary>
/// Chunk G authority for structural/trap workmanship, Metal Worker Heat Treatment and Mechanic
/// vehicle-parts salvage. Placed durability is virtual per-instance max durability: persisted
/// workmanship scales positive native block damage while repair/upgrade damage remains native.
/// This preserves BlockValue serialization and prevents repeated multiplication on upgrades,
/// repairs, pickup/redeploy, rewiring or ownership handoff.
/// </summary>
public static class RebirthWorkmanshipSignatureService
{
    public const string BuiltToLastBonusId="background_bonus.built_to_last";
    public const string HeatTreatmentBonusId="background_bonus.heat_treatment";
    public const string EngineeredReliabilityBonusId="background_bonus.engineered_reliability";
    public const string PartsSalvagerBonusId="background_bonus.parts_salvager";
    private const double PendingSeconds=120.0;
    private static readonly object Gate=new object();
    private static DateTime NextCleanupUtc = DateTime.MinValue;
    private static readonly Dictionary<int,List<RebirthPendingMetalCraftSignature>> PendingMetal=new Dictionary<int,List<RebirthPendingMetalCraftSignature>>();
    private static readonly Queue<RebirthMetalCraftCorrection> MetalCorrections=new Queue<RebirthMetalCraftCorrection>();
    private static readonly Dictionary<string,int> MechanicReplayDamage=new Dictionary<string,int>(StringComparer.Ordinal);
    private static readonly List<RebirthMetalCraftCorrection> ClientPendingCorrections=new List<RebirthMetalCraftCorrection>();
    private static bool installed;
    private static bool inventoryCommitPatched;
    private static int blockDamagePatchCount;
    private static bool localCraftPatched,workstationCraftPatched,harvestPatched;
    [ThreadStatic]
    private static HashSet<string> activeBlockDamagePositions;

    public static string Install(Harmony harmony)
    {
        if(installed)return "[REBIRTH Workmanship Signatures] already installed";
        installed=true;
        if(harmony!=null)
        {
            blockDamagePatchCount=PatchBlockDamageMethods(harmony);
            MethodInfo giveExp=AccessTools.DeclaredMethod(typeof(XUiC_RecipeStack),"giveExp",new[]{typeof(ItemValue),typeof(ItemClass)});
            if(giveExp!=null){harmony.Patch(giveExp,prefix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(LocalCraftPrefix)));localCraftPatched=true;}
            MethodInfo complete=AccessTools.Method(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.AddCraftComplete),new[]{typeof(int),typeof(ItemValue),typeof(string),typeof(string),typeof(int),typeof(int)});
            if(complete!=null){harmony.Patch(complete,prefix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(WorkstationCraftPrefix)));workstationCraftPatched=true;}
            MethodInfo harvest=AccessTools.Method(typeof(GameUtils),"collectHarvestedItem");
            if(harvest!=null){harmony.Patch(harvest,prefix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(MechanicHarvestPrefix)),postfix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(MechanicHarvestPostfix)));harvestPatched=true;}
            Type packageType=AccessTools.TypeByName("NetPackagePlayerInventory");
            MethodInfo process=packageType!=null?AccessTools.Method(packageType,"ProcessPackage",new[]{typeof(World),typeof(GameManager)}):null;
            if(process!=null){harmony.Patch(process,postfix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(NativeInventoryCommitPrefix)));inventoryCommitPatched=true;}
        }
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Workmanship Signatures] installed blockDamagePatches="+blockDamagePatchCount+" localCraft="+localCraftPatched+" workstationCraft="+workstationCraftPatched+" harvest="+harvestPatched+" inventoryCommit="+inventoryCommitPatched;
    }

    public static float CalculatePlacedDurabilityMultiplier(RebirthPlacedWorkmanshipRecord record)
    {
        if(record==null)return 1f;
        float delta=record.Kind==RebirthPlacedProvenanceKind.TechnicalTrapPlacement
            ? RebirthServiceCraftSkillService.SignedEndpoint(record.SkillValue,RebirthProgressionRuntimeConfig.TechnicalTrapDurabilityNegative,RebirthProgressionRuntimeConfig.TechnicalTrapDurabilityPositive)
            : RebirthServiceCraftSkillService.SignedEndpoint(record.SkillValue,RebirthProgressionRuntimeConfig.ConstructionDurabilityNegative,RebirthProgressionRuntimeConfig.ConstructionDurabilityPositive);
        float multiplier=1f+delta;
        if(record.Kind==RebirthPlacedProvenanceKind.ConstructionPlacement && string.Equals(record.BonusId,BuiltToLastBonusId,StringComparison.OrdinalIgnoreCase))
            multiplier+=Math.Max(0f,GetBonusMultiplierDelta(BuiltToLastBonusId,"max_durability_multiplier",1.15f));
        else if(record.Kind==RebirthPlacedProvenanceKind.TechnicalTrapPlacement && string.Equals(record.BonusId,EngineeredReliabilityBonusId,StringComparison.OrdinalIgnoreCase))
            multiplier+=Math.Max(0f,GetBonusMultiplierDelta(EngineeredReliabilityBonusId,"max_durability_multiplier",1.50f));
        return Mathf.Clamp(multiplier,0.50f,2.50f);
    }

    public static int CalculateEffectivePlacedMax(RebirthPlacedWorkmanshipRecord record)
    {
        if(record==null)return 0;
        return Math.Max(1,Mathf.RoundToInt(Math.Max(1,record.OriginalBaseMaxDurability)*CalculatePlacedDurabilityMultiplier(record)));
    }

    // V3.2 Block.OnBlockDamaged(WorldBase, BlockValueRef, BlockValue, int, int, AttackHitInfo, bool, bool, int).
    // Positional Harmony arguments intentionally avoid depending on upstream parameter names.
    public static void BlockDamagePrefix(WorldBase __0,BlockValueRef __1,ref int __3,bool __7,out bool __state)
    {
        __state=false;
        if(__3<=0||__7||!IsServerWorld(__0)||__1.Type!=BlockValueRefType.Block)return;
        Vector3i blockPos=__1.BlockPosition;
        RebirthPlacedWorkmanshipRecord r;
        if(!RebirthPlacedWorkmanshipService.TryGet(__0,blockPos,out r)||r==null)return;
        string key=blockPos.x+","+blockPos.y+","+blockPos.z;
        if(activeBlockDamagePositions==null)activeBlockDamagePositions=new HashSet<string>(StringComparer.Ordinal);
        if(activeBlockDamagePositions.Contains(key))return;
        activeBlockDamagePositions.Add(key);__state=true;
        float multiplier=CalculatePlacedDurabilityMultiplier(r);
        if(Math.Abs(multiplier-1f)<0.0001f)return;
        __3=Math.Max(1,Mathf.CeilToInt(__3/multiplier));
    }

    public static void BlockDamagePostfix(BlockValueRef __1,bool __state)
    {
        if(!__state||activeBlockDamagePositions==null||__1.Type!=BlockValueRefType.Block)return;
        Vector3i blockPos=__1.BlockPosition;
        activeBlockDamagePositions.Remove(blockPos.x+","+blockPos.y+","+blockPos.z);
    }

    public static void LocalCraftPrefix(XUiC_RecipeStack __instance,ItemValue _iv,ItemClass _ic)
    {
        if(__instance==null||_iv==null||_iv.ItemClass==null||__instance.recipe==null||__instance.AmountToRepair>0)return;
        EntityPlayer player=__instance.xui!=null&&__instance.xui.playerUI!=null?__instance.xui.playerUI.entityPlayer:null;
        if(player==null||player.world==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        string recipe=__instance.recipe.GetName();
        if(!IsEligibleMetalCraft(player,recipe,_iv))return;
        if(player.world.IsRemote())RequestRemoteMetalCraft(player,recipe,_iv);
        else TryApplyMetalHeatTreatment(player,recipe,_iv,"local-authoritative-craft");
    }

    public static void WorkstationCraftPrefix(TileEntityWorkstation __instance,int crafterEntityID,ItemValue itemCrafted,string recipeName,int craftedCount)
    {
        if(craftedCount<=0||itemCrafted==null||GameManager.Instance==null||GameManager.Instance.World==null||GameManager.Instance.World.IsRemote())return;
        EntityPlayer player=GameManager.Instance.World.GetEntity(crafterEntityID) as EntityPlayer;
        if(player!=null)TryApplyMetalHeatTreatment(player,recipeName,itemCrafted,"server-workstation-complete");
    }

    public static bool TryApplyMetalHeatTreatment(EntityPlayer player,string recipeName,ItemValue item,string source)
    {
        if(player==null||item==null||item.ItemClass==null||item.MaxUseTimes<=0||player.world==null||player.world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return false;
        if(!IsEligibleMetalCraft(player,recipeName,item))return false;
        RebirthItemProvenanceSnapshot existing;
        if(RebirthItemProvenanceAdapter.TryRead(item,out existing))
            return existing!=null&&string.Equals(existing.BonusId,HeatTreatmentBonusId,StringComparison.OrdinalIgnoreCase);
        int nativeMax=Math.Max(0,item.MaxUseTimes); if(nativeMax<=0)return false;
        float mult=GetBonusTuning(HeatTreatmentBonusId,"max_durability_multiplier",1.15f);
        int target=Math.Max(nativeMax+1,Mathf.CeilToInt(nativeMax*Mathf.Max(1f,mult)));
        int applied;string reason;
        if(!RebirthItemConditionStatAdapter.TrySetEffectiveMaxUseTimes(item,target,target,out applied,out reason))
        {Log.Warning("[REBIRTH Workmanship] Heat Treatment failed closed source="+source+" recipe="+recipeName+" target="+target+" reason="+reason);return false;}
        RebirthProvenanceAuthorSnapshot author;if(!RebirthProvenanceIdentity.TryCapture(player,out author))return false;
        float skill=0f;RebirthServiceCraftSkillService.TryGetSkillValue(player,"skill.metalworking",out skill);
        float workmanship=Mathf.Clamp01((Mathf.Clamp(skill,-50f,100f)+50f)/150f);
        RebirthItemProvenanceAdapter.Stamp(item,new RebirthItemProvenanceSnapshot{Version=RebirthItemProvenanceAdapter.CurrentVersion,Kind="crafted_item",SourceId=recipeName??string.Empty,CreatorStableId=author.StablePlayerId,BackgroundId=author.BackgroundId,BonusId=HeatTreatmentBonusId,SkillId="skill.metalworking",SkillValue=skill,Workmanship=workmanship,OriginalMaxUseTimes=applied,BatchToken="metal:"+item.Seed+":"+(recipeName??string.Empty)});
        return true;
    }

    public static void RegisterMetalCraftRequest(EntityPlayer player,int itemType,ushort seed,ushort quality,string recipeName)
    {
        if(player==null||player.world==null||player.world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        Recipe recipe=null;try{recipe=CraftingManager.GetRecipe(recipeName??string.Empty);}catch{}
        if(recipe==null||recipe.itemValueType!=itemType||!string.Equals(RebirthServiceCraftSkillService.ClassifyRecipe(recipe),"skill.metalworking",StringComparison.OrdinalIgnoreCase))return;
        if(!RebirthBackgroundBonusService.HasBonus(player,HeatTreatmentBonusId))return;
        lock(Gate)
        {
            CleanupLocked();List<RebirthPendingMetalCraftSignature> list;if(!PendingMetal.TryGetValue(player.entityId,out list))PendingMetal[player.entityId]=list=new List<RebirthPendingMetalCraftSignature>();
            list.RemoveAll(delegate(RebirthPendingMetalCraftSignature p){return p!=null&&p.ItemType==itemType&&p.Seed==seed&&p.Quality==quality;});
            list.Add(new RebirthPendingMetalCraftSignature{PlayerId=player.entityId,ItemType=itemType,Seed=seed,Quality=quality,RecipeName=recipeName??string.Empty,CreatedUtc=DateTime.UtcNow});
        }
    }

    public static void NativeInventoryCommitPrefix(object __instance,World _world)
    {
        if(__instance==null||!IsServerWorld(_world))return;
        object sender=ReadMember(__instance,"Sender");int playerId=ReadInt(sender,"entityId",-1);if(playerId<0)playerId=ReadInt(sender,"EntityId",-1);if(playerId<0)return;
        EntityPlayer player=_world.GetEntity(playerId) as EntityPlayer;if(player==null)return;
        List<RebirthPendingMetalCraftSignature> pending=GetPendingSnapshot(playerId);if(pending.Count==0)return;
        ItemStack[] incomingTool=player.inventory!=null?player.inventory.ItemGrid.items:null;ItemStack[] incomingBag=player.bag!=null?player.bag.ItemGrid.items:null;
        for(int i=0;i<pending.Count;i++)
        {
            RebirthPendingMetalCraftSignature p=pending[i];ItemValue post=FindItem(incomingTool,incomingBag,p.ItemType,p.Seed,p.Quality);if(post==null||post.MaxUseTimes<=0)continue;
            if(TryApplyMetalHeatTreatment(player,p.RecipeName,post,"server-inventory-commit"))
            {RemovePending(p);lock(Gate)MetalCorrections.Enqueue(new RebirthMetalCraftCorrection(playerId,post));}
        }
    }

    public struct MechanicHarvestState
    {
        public EntityPlayer Player;public Vector3i Position;public string BlockName;public uint BlockRawData;public int DamageBefore;public int ItemType;public int CountBefore;public bool Eligible;
    }

    public static void MechanicHarvestPrefix(ItemActionData _actionData,ItemValue _iv,out MechanicHarvestState __state)
    {
        __state=new MechanicHarvestState();
        EntityPlayer player=_actionData!=null&&_actionData.invData!=null?_actionData.invData.holdingEntity as EntityPlayer:null;
        if(player==null||_iv==null||_iv.ItemClass==null||_actionData.attackDetails==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        if(!string.Equals(_iv.ItemClass.GetItemName(),"resourceMechanicalParts",StringComparison.OrdinalIgnoreCase))return;
        BlockValue damagedValue=_actionData.attackDetails.blockBeingDamaged;Block block=damagedValue.Block;string blockName=block!=null?(block.GetBlockName()??string.Empty):string.Empty;
        if(!IsVehicleBlockName(blockName))return;
        if(!string.Equals(RebirthProgressionRuntimeConfig.ClassifyHarvestTool(_actionData.invData.itemValue),"skill.salvage",StringComparison.OrdinalIgnoreCase))return;
        __state.Player=player;__state.Position=_actionData.attackDetails.raycastHitPosition;__state.BlockName=blockName;__state.BlockRawData=damagedValue.rawData;__state.DamageBefore=damagedValue.damage;__state.ItemType=_iv.type;__state.CountBefore=CountPlayerItem(player,_iv.type);__state.Eligible=true;
    }

    public static void MechanicHarvestPostfix(MechanicHarvestState __state)
    {
        if(!__state.Eligible||__state.Player==null)return;int after=CountPlayerItem(__state.Player,__state.ItemType);int delta=after-__state.CountBefore;if(delta<=0)return;
        if(__state.Player.world!=null&&__state.Player.world.IsRemote())
        {
            ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c!=null)c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthMechanicSalvageRequest>().Setup(__state.Player.entityId,__state.Position,__state.BlockName,__state.BlockRawData,__state.DamageBefore,__state.ItemType,delta));
        }
        else ProcessMechanicSalvageRequest(__state.Player,__state.Position,__state.BlockName,__state.BlockRawData,__state.DamageBefore,__state.ItemType,delta);
    }

    public static void ProcessMechanicSalvageRequest(EntityPlayer player,Vector3i pos,string requestedBlockName,uint expectedRawData,int damageBefore,int itemType,int baseCount)
    {
        if(player==null||baseCount<=0||player.world==null||player.world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthBackgroundBonusService.HasBonus(player,PartsSalvagerBonusId))return;
        ItemClass ic=ItemClass.GetForId(itemType);if(ic==null||!string.Equals(ic.GetItemName(),"resourceMechanicalParts",StringComparison.OrdinalIgnoreCase))return;
        if(!IsVehicleBlockName(requestedBlockName))return;
        int authoredMax=GetAuthoredVehicleMechanicalPartsMax(requestedBlockName);
        if(authoredMax<=0)return;
        baseCount=Math.Min(baseCount,authoredMax);
        if(baseCount<=0)return;
        if((player.position-pos.ToVector3()).sqrMagnitude>(Constants.cDigAndBuildDistance+3f)*(Constants.cDigAndBuildDistance+3f))return;
        ItemValue held=player.inventory!=null?player.inventory.holdingItemItemValue:null;if(!string.Equals(RebirthProgressionRuntimeConfig.ClassifyHarvestTool(held),"skill.salvage",StringComparison.OrdinalIgnoreCase))return;
        BlockValue current=player.world.GetBlock(pos);string currentName=current.Block!=null?(current.Block.GetBlockName()??string.Empty):string.Empty;
        if(current.rawData!=expectedRawData||current.damage<=damageBefore)return;
        if(!string.Equals(currentName,requestedBlockName,StringComparison.OrdinalIgnoreCase)&&!string.Equals(currentName,"carRespawner_FR",StringComparison.OrdinalIgnoreCase))return;
        string replay=player.entityId+"|"+pos.x+","+pos.y+","+pos.z+"|"+itemType;int damage=current.damage;int previous;
        lock(Gate){if(MechanicReplayDamage.TryGetValue(replay,out previous)&&damage<=previous)return;MechanicReplayDamage[replay]=damage;}
        float multiplier=GetBonusTuning(PartsSalvagerBonusId,"mechanical_component_multiplier",1.5f);float raw=Math.Max(0f,baseCount*(Mathf.Max(1f,multiplier)-1f));int extra=Mathf.FloorToInt(raw);float fraction=raw-extra;
        if(fraction>0f&&player.world.GetGameRandom().RandomFloat<fraction)extra++;if(extra<=0)return;
        GrantMechanicExtra(player.entityId,itemType,extra);
    }

    public static void GrantMechanicExtra(int playerId,int itemType,int count)
    {
        if(count<=0||GameManager.Instance==null||GameManager.Instance.World==null)return;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;EntityPlayer p=GameManager.Instance.World.GetEntity(playerId) as EntityPlayer;
        if(c!=null&&c.IsServer&&!(p is EntityPlayerLocal)){c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMechanicSalvageGrant>().Setup(playerId,itemType,count),_attachedToEntityId:playerId);return;}
        EntityPlayerLocal local=GameManager.Instance.World.GetPrimaryPlayer();if(local==null||local.entityId!=playerId)return;
        ItemValue value=new ItemValue(itemType);if(value==null||value.ItemClass==null)return;ItemStack stack=new ItemStack(value,count);LocalPlayerUI ui=LocalPlayerUI.GetUIForPlayer(local);
        if(ui!=null&&ui.xui!=null&&ui.xui.PlayerInventory.AddItem(stack))return;
        if(stack.count>0)local.world.gameManager.ItemDropServer(stack,local.GetDropPosition(),Vector3.zero,local.entityId,60f,false);
    }

    public static void ApplyClientMetalCorrection(int playerId,ItemValue corrected)
    {
        if(corrected==null||GameManager.Instance==null||GameManager.Instance.World==null)return;
        EntityPlayerLocal local=GameManager.Instance.World.GetPrimaryPlayer();if(local==null||local.entityId!=playerId)return;
        if(!ReplacePlayerItem(local,corrected))lock(Gate)ClientPendingCorrections.Add(new RebirthMetalCraftCorrection(playerId,corrected));
    }

    public static string BuildDebugReport(EntityPlayer player,Vector3i? pos)
    {
        StringBuilder b=new StringBuilder();b.AppendLine("[REBIRTH Workmanship Signatures]");
        b.AppendLine("status=IMPLEMENTED_PREBOOT blockDamagePatches="+blockDamagePatchCount+" inventoryCommit="+inventoryCommitPatched+" mechanicHarvest="+harvestPatched);
        b.AppendLine("constructionCurve="+RebirthProgressionRuntimeConfig.ConstructionDurabilityNegative.ToString("0.###",CultureInfo.InvariantCulture)+".."+RebirthProgressionRuntimeConfig.ConstructionDurabilityPositive.ToString("0.###",CultureInfo.InvariantCulture)+" trapCurve="+RebirthProgressionRuntimeConfig.TechnicalTrapDurabilityNegative.ToString("0.###",CultureInfo.InvariantCulture)+".."+RebirthProgressionRuntimeConfig.TechnicalTrapDurabilityPositive.ToString("0.###",CultureInfo.InvariantCulture));
        if(player!=null){RebirthBackgroundBonusDefinition d=RebirthBackgroundBonusService.GetSignatureBonus(player);b.AppendLine("player="+player.entityId+" bonus="+(d!=null?d.Id:"<none>"));}
        if(pos.HasValue){RebirthPlacedWorkmanshipRecord r;if(RebirthPlacedWorkmanshipService.TryGet(GameManager.Instance!=null?GameManager.Instance.World:null,pos.Value,out r))b.AppendLine("placed="+pos.Value+" block="+r.BlockName+" skill="+r.SkillId+"@"+r.SkillValue.ToString("0.###")+" effectiveMax="+CalculateEffectivePlacedMax(r)+" multiplier="+CalculatePlacedDurabilityMultiplier(r).ToString("0.###"));}
        return b.ToString().TrimEnd();
    }

    private static bool IsEligibleMetalCraft(EntityPlayer player,string recipeName,ItemValue item)
    {
        return player!=null&&item!=null&&item.ItemClass!=null&&item.MaxUseTimes>0&&RebirthBackgroundBonusService.HasBonus(player,HeatTreatmentBonusId)&&string.Equals(RebirthServiceCraftSkillService.ClassifyRecipe(recipeName),"skill.metalworking",StringComparison.OrdinalIgnoreCase);
    }
    private static void RequestRemoteMetalCraft(EntityPlayer player,string recipeName,ItemValue item){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c!=null)c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthMetalCraftSignatureRequest>().Setup(player.entityId,item.type,item.Seed,item.Quality,recipeName));}
    private static float GetBonusMultiplierDelta(string bonusId,string key,float fallback){return Math.Max(0f,GetBonusTuning(bonusId,key,fallback)-1f);}
    public static float GetBonusTuning(string bonusId,string key,float fallback){RebirthBackgroundBonusDefinition d;RebirthBackgroundBonusTuningValue v;float f;return RebirthBackgroundBonusRegistry.TryGet(bonusId,out d)&&d!=null&&d.TryGetTuning(key,out v)&&v!=null&&float.TryParse(v.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out f)?f:fallback;}
    public static bool IsVehicleBlockName(string name){string n=(name??string.Empty).ToLowerInvariant();return n.StartsWith("repairablevehicle")||n.StartsWith("cntcar")||n.Contains("sedan")||n.Contains("minivan")||n.Contains("pickuptruck")||n.Contains("semitruck")||n.Contains("armytruck")||n.Contains("farmtruck")||n.Contains("firetruck")||n.Contains("suv")||n.Contains("schoolbus")||n.Contains("ambulance")||n.Contains("taxi")||n.Contains("tractor")||n.Contains("excavator")||n.Contains("backhoe")||n.Contains("forklift");}
    private static int GetAuthoredVehicleMechanicalPartsMax(string blockName)
    {
        if(string.IsNullOrEmpty(blockName))return 0;
        BlockValue bv;try{bv=Block.GetBlockValue(blockName);}catch{return 0;}
        Block block=bv.Block;if(block==null||!IsVehicleBlockName(block.GetBlockName()))return 0;
        List<Block.SItemDropProb> drops;
        if(block.itemsToDrop==null||!block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest,out drops)||drops==null)return 0;
        int total=0;
        for(int i=0;i<drops.Count;i++)
        {
            Block.SItemDropProb d=drops[i];
            if(string.Equals(d.name,"resourceMechanicalParts",StringComparison.OrdinalIgnoreCase))
                total+=Math.Max(0,d.maxCount);
        }
        return Math.Min(total,64);
    }
    private static bool IsServerWorld(WorldBase world){ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;return world!=null&&!world.IsRemote()&&c!=null&&c.IsServer&&RebirthSurvivorMode.IsEnabledForCurrentWorld();}
    private static int PatchBlockDamageMethods(Harmony harmony){int count=0;HashSet<MethodBase> seen=new HashSet<MethodBase>();MethodInfo prefix=AccessTools.Method(typeof(RebirthWorkmanshipSignatureService),nameof(BlockDamagePrefix));MethodInfo postfix=AccessTools.Method(typeof(RebirthWorkmanshipSignatureService),nameof(BlockDamagePostfix));Type[] args={typeof(WorldBase),typeof(BlockValueRef),typeof(BlockValue),typeof(int),typeof(int),typeof(ItemActionAttack.AttackHitInfo),typeof(bool),typeof(bool),typeof(int)};BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;foreach(Assembly assembly in new[]{typeof(Block).Assembly,typeof(RebirthWorkmanshipSignatureService).Assembly}){Type[] types;try{types=assembly.GetTypes();}catch(ReflectionTypeLoadException ex){types=ex.Types;}if(types==null)continue;for(int i=0;i<types.Length;i++){Type t=types[i];if(t==null||!typeof(Block).IsAssignableFrom(t))continue;MethodInfo m=t.GetMethod(nameof(Block.OnBlockDamaged),flags,null,args,null);if(m==null||!seen.Add(m))continue;harmony.Patch(m,prefix:new HarmonyMethod(prefix),postfix:new HarmonyMethod(postfix));count++;}}return count;}
    private static int CountPlayerItem(EntityPlayer player,int type){int total=0;CountSlots(player!=null&&player.inventory!=null?player.inventory.ItemGrid.items:null,type,ref total);CountSlots(player!=null&&player.bag!=null?player.bag.ItemGrid.items:null,type,ref total);return total;}
    private static void CountSlots(ItemStack[] slots,int type,ref int total){if(slots==null)return;for(int i=0;i<slots.Length;i++)if(slots[i]!=null&&slots[i].itemValue!=null&&slots[i].itemValue.type==type)total+=Math.Max(0,slots[i].count);}
    private static ItemValue FindItem(ItemStack[] a,ItemStack[] b,int type,ushort seed,ushort quality){ItemValue v=FindItem(a,type,seed,quality);return v??FindItem(b,type,seed,quality);}
    private static ItemValue FindItem(ItemStack[] slots,int type,ushort seed,ushort quality){if(slots==null)return null;for(int i=0;i<slots.Length;i++){ItemValue v=slots[i]!=null?slots[i].itemValue:null;if(v!=null&&v.type==type&&v.Seed==seed&&v.Quality==quality)return v;}return null;}
    private static bool ReplacePlayerItem(EntityPlayer player,ItemValue corrected){if(player==null||corrected==null)return false;ItemStack[] t=player.inventory!=null?player.inventory.ItemGrid.items:null;for(int i=0;t!=null&&i<t.Length;i++){ItemValue v=t[i]!=null?t[i].itemValue:null;if(v!=null&&v.type==corrected.type&&v.Seed==corrected.Seed&&v.Quality==corrected.Quality){ItemValue merged=v.Clone();if(!RebirthItemProvenanceAdapter.TryApplyOwnedCorrection(merged,corrected))continue;ItemStack s=t[i];s.itemValue=merged;player.inventory.SetItem(i,s);return true;}}ItemStack[] b=player.bag!=null?player.bag.ItemGrid.items:null;for(int i=0;b!=null&&i<b.Length;i++){ItemValue v=b[i]!=null?b[i].itemValue:null;if(v!=null&&v.type==corrected.type&&v.Seed==corrected.Seed&&v.Quality==corrected.Quality){ItemValue merged=v.Clone();if(!RebirthItemProvenanceAdapter.TryApplyOwnedCorrection(merged,corrected))continue;ItemStack s=b[i];s.itemValue=merged;player.bag.SetSlot(i,s);return true;}}return false;}
    private static object ReadMember(object target,string name){if(target==null)return null;Type t=target.GetType();BindingFlags f=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;FieldInfo fi=t.GetField(name,f);if(fi!=null)return fi.GetValue(target);PropertyInfo pi=t.GetProperty(name,f);return pi!=null?pi.GetValue(target,null):null;}
    private static int ReadInt(object target,string name,int fallback){try{object o=ReadMember(target,name);return o!=null?Convert.ToInt32(o):fallback;}catch{return fallback;}}
    private static List<RebirthPendingMetalCraftSignature> GetPendingSnapshot(int playerId){lock(Gate){CleanupLocked();List<RebirthPendingMetalCraftSignature> l;return PendingMetal.TryGetValue(playerId,out l)?new List<RebirthPendingMetalCraftSignature>(l):new List<RebirthPendingMetalCraftSignature>();}}
    private static void RemovePending(RebirthPendingMetalCraftSignature p){if(p==null)return;lock(Gate){List<RebirthPendingMetalCraftSignature> l;if(PendingMetal.TryGetValue(p.PlayerId,out l))l.Remove(p);}}
    private static void CleanupLocked(){DateTime now=DateTime.UtcNow;if(now<NextCleanupUtc)return;NextCleanupUtc=now.AddSeconds(1);DateTime cutoff=now.AddSeconds(-PendingSeconds);List<int> empty=new List<int>();foreach(KeyValuePair<int,List<RebirthPendingMetalCraftSignature>> kv in PendingMetal){kv.Value.RemoveAll(delegate(RebirthPendingMetalCraftSignature p){return p==null||p.CreatedUtc<cutoff;});if(kv.Value.Count==0)empty.Add(kv.Key);}for(int i=0;i<empty.Count;i++)PendingMetal.Remove(empty[i]);ClientPendingCorrections.RemoveAll(delegate(RebirthMetalCraftCorrection x){return x==null||x.CreatedUtc<cutoff;});}
    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c!=null&&c.IsServer){for(;;){RebirthMetalCraftCorrection x;lock(Gate){if(MetalCorrections.Count==0)break;x=MetalCorrections.Dequeue();}EntityPlayer p=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetEntity(x.PlayerId) as EntityPlayer:null;if(p is EntityPlayerLocal)ApplyClientMetalCorrection(x.PlayerId,x.Corrected);else if(x.Corrected!=null)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetalCraftCorrection>().Setup(x.PlayerId,x.Corrected),_attachedToEntityId:x.PlayerId);}}
        lock(Gate){for(int i=ClientPendingCorrections.Count-1;i>=0;i--){RebirthMetalCraftCorrection x=ClientPendingCorrections[i];EntityPlayerLocal local=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;if(local!=null&&local.entityId==x.PlayerId&&ReplacePlayerItem(local,x.Corrected))ClientPendingCorrections.RemoveAt(i);}CleanupLocked();}
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){ClearRuntime();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){ClearRuntime();}
    private static void ClearRuntime(){lock(Gate){PendingMetal.Clear();MetalCorrections.Clear();MechanicReplayDamage.Clear();ClientPendingCorrections.Clear();}}
}
