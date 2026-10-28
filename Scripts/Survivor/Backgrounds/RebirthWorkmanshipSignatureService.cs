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
    [ThreadStatic]
    private static HashSet<string> activeEmptyHandInspections;

    // Restore the 2.6 empty-hand block inspector without logging weapon or tool hits.
    public static void EmptyHandBlockInspectPrefix(Block __instance,WorldBase __0,BlockValueRef __1,int __4,out string __state)
    {
        __state=null;
        if(__instance==null||__0==null||__1.Type!=BlockValueRefType.Block||GameManager.IsDedicatedServer)return;
        EntityPlayerLocal player=__0.GetEntity(__4) as EntityPlayerLocal;
        if(player?.inventory?.holdingItem==null||player.inventory.holdingItem.GetItemName()!="meleeHandPlayer")return;
        Vector3i pos=__1.BlockPosition;
        string key=pos.x+","+pos.y+","+pos.z;
        if(activeEmptyHandInspections==null)activeEmptyHandInspections=new HashSet<string>(StringComparer.Ordinal);
        if(!activeEmptyHandInspections.Add(key))return;
        __state=key;
        Log.Out("[REBIRTH Block Inspect] block="+__instance.GetBlockName()+" position="+pos);
    }

    public static void EmptyHandBlockInspectPostfix(string __state)
    {
        if(__state!=null)activeEmptyHandInspections?.Remove(__state);
    }

    public static string Install(Harmony harmony)
    {
        if(installed)return "[REBIRTH Workmanship Signatures] already installed";
        installed=true;
        if(harmony!=null)
        {
            blockDamagePatchCount=PatchBlockDamageMethods(harmony);
            RebirthSalvageAcceptedHitWitness.InstallSourceInvalidation(harmony);
            MethodInfo giveExp=AccessTools.DeclaredMethod(typeof(XUiC_RecipeStack),"giveExp",new[]{typeof(ItemValue),typeof(ItemClass)});
            if(giveExp!=null){harmony.Patch(giveExp,prefix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(LocalCraftPrefix)));localCraftPatched=true;}
            MethodInfo complete=AccessTools.Method(typeof(TileEntityWorkstation),nameof(TileEntityWorkstation.AddCraftComplete),new[]{typeof(int),typeof(ItemValue),typeof(string),typeof(string),typeof(int),typeof(int)});
            if(complete!=null){harmony.Patch(complete,prefix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(WorkstationCraftPrefix)));workstationCraftPatched=true;}
            MethodInfo harvest=AccessTools.Method(typeof(GameUtils),"collectHarvestedItem");
            if(harvest!=null){harmony.Patch(harvest,prefix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(MechanicHarvestPrefix)),postfix:new HarmonyMethod(typeof(RebirthWorkmanshipSignatureService),nameof(MechanicHarvestPostfix)));harvestPatched=true;}
            RebirthMetalHeatTreatmentCorrection.Install(harmony);inventoryCommitPatched=true;
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
        if(player.world.IsRemote())RebirthMetalHeatTreatmentCorrection.OriginalOutput(__instance,player as EntityPlayerLocal,_iv,recipe);
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

    // Legacy type/seed/quality claims carry no original output-success witness.
    // Kept as a wire compatibility refusal, never as heat-treatment authority.
    public static void RegisterMetalCraftRequest(EntityPlayer player,int itemType,ushort seed,ushort quality,string recipeName) { }
    public static void NativeInventoryCommitPrefix(object __instance,World _world)
    {if(__instance is NetPackage package)RebirthMetalHeatTreatmentCorrection.NativeUpload(package,_world);}
    public struct MechanicHarvestState
    {
        public EntityPlayer Player;public Vector3i Position;public string BlockName;public uint BlockRawData;public int DamageBefore;public int ItemType;public int CountBefore;public int ProducedCount;public bool Eligible;
    }

    public static void MechanicHarvestPrefix(ItemActionData _actionData,ItemValue _iv,int _count,bool _bScaleCountOnDamage,out MechanicHarvestState __state)
    {
        __state=new MechanicHarvestState();
        EntityPlayer player=_actionData!=null&&_actionData.invData!=null?_actionData.invData.holdingEntity as EntityPlayer:null;
        if(player==null||_iv==null||_iv.ItemClass==null||_actionData.attackDetails==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        if(!string.Equals(_iv.ItemClass.GetItemName(),"resourceMechanicalParts",StringComparison.OrdinalIgnoreCase))return;
        BlockValue damagedValue=_actionData.attackDetails.blockBeingDamaged;Block block=damagedValue.Block;string blockName=block!=null?(block.GetBlockName()??string.Empty):string.Empty;
        if(!IsVehicleBlockName(blockName))return;
        if(!string.Equals(RebirthProgressionRuntimeConfig.ClassifyHarvestTool(_actionData.invData.itemValue),"skill.salvage",StringComparison.OrdinalIgnoreCase))return;
        __state.Player=player;__state.Position=_actionData.attackDetails.raycastHitPosition;__state.BlockName=blockName;__state.BlockRawData=damagedValue.rawData;__state.DamageBefore=damagedValue.damage;__state.ItemType=_iv.type;__state.CountBefore=CountPlayerItem(player,_iv.type);__state.ProducedCount=RebirthSalvageAcceptedHitWitness.NativeCollectionCount(_actionData,_count,_bScaleCountOnDamage);__state.Eligible=__state.ProducedCount>0;
    }

    public static void MechanicHarvestPostfix(MechanicHarvestState __state,bool __result,bool __runOriginal)
    {
        if(!__runOriginal||!__result||!__state.Eligible||__state.Player==null)return;
        // Native true confirms actual collection, including its native overflow drop.
        int delta=__state.ProducedCount;
        if(__state.Player.world!=null&&__state.Player.world.IsRemote())
        {
            ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;if(c!=null)c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthMechanicSalvageRequest>().Setup(__state.Player.entityId,__state.Position,__state.BlockName,__state.BlockRawData,__state.DamageBefore,__state.ItemType,delta));
        }
        else ProcessMechanicSalvageRequest(__state.Player,__state.Position,__state.BlockName,__state.BlockRawData,__state.DamageBefore,__state.ItemType,delta);
    }

    public static void ProcessMechanicSalvageRequest(EntityPlayer player,Vector3i pos,string requestedBlockName,uint expectedRawData,int damageBefore,int itemType,int baseCount)
    {
        if(player==null||damageBefore<0||baseCount<=0||player.world==null||player.world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthBackgroundBonusService.HasBonus(player,PartsSalvagerBonusId))return;
        ItemClass ic=ItemClass.GetForId(itemType);if(ic==null||!string.Equals(ic.GetItemName(),"resourceMechanicalParts",StringComparison.OrdinalIgnoreCase))return;
        if(!IsVehicleBlockName(requestedBlockName))return;
        int authoredMax=GetAuthoredVehicleMechanicalPartsMax(requestedBlockName);
        if(authoredMax<=0)return;
        baseCount=Math.Min(baseCount,authoredMax);
        if(baseCount<=0)return;
        if((player.position-pos.ToVector3()).sqrMagnitude>(Constants.cDigAndBuildDistance+3f)*(Constants.cDigAndBuildDistance+3f))return;
        ItemValue held=player.inventory!=null?player.inventory.holdingItemItemValue:null;if(!string.Equals(RebirthProgressionRuntimeConfig.ClassifyHarvestTool(held),"skill.salvage",StringComparison.OrdinalIgnoreCase))return;
        if(!RebirthSalvageAcceptedHitWitness.TryClaim(player,pos,requestedBlockName,expectedRawData,damageBefore,"mechanic:"+itemType))return;
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

    // Legacy corrections cannot identify the original native output. Only the
    // domain receipt/reply path may publish native durability to a current item.
    public static void ApplyClientMetalCorrection(int playerId,ItemValue corrected) { }
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
    private static int PatchBlockDamageMethods(Harmony harmony){int count=0;HashSet<MethodBase> seen=new HashSet<MethodBase>();MethodInfo prefix=AccessTools.Method(typeof(RebirthWorkmanshipSignatureService),nameof(BlockDamagePrefix));MethodInfo postfix=AccessTools.Method(typeof(RebirthWorkmanshipSignatureService),nameof(BlockDamagePostfix));Type[] args={typeof(WorldBase),typeof(BlockValueRef),typeof(BlockValue),typeof(int),typeof(int),typeof(ItemActionAttack.AttackHitInfo),typeof(bool),typeof(bool),typeof(int)};BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;foreach(Assembly assembly in new[]{typeof(Block).Assembly,typeof(RebirthWorkmanshipSignatureService).Assembly}){Type[] types;try{types=assembly.GetTypes();}catch(ReflectionTypeLoadException ex){types=ex.Types;}if(types==null)continue;for(int i=0;i<types.Length;i++){Type t=types[i];if(t==null||!typeof(Block).IsAssignableFrom(t))continue;MethodInfo m=t.GetMethod(nameof(Block.OnBlockDamaged),flags,null,args,null);if(m==null||!seen.Add(m))continue;harmony.Patch(m,prefix:new HarmonyMethod(prefix),postfix:new HarmonyMethod(postfix));harmony.Patch(m,prefix:new HarmonyMethod(typeof(RebirthEmptyHandBlockInspectionPatch),nameof(RebirthEmptyHandBlockInspectionPatch.Prefix)),postfix:new HarmonyMethod(typeof(RebirthEmptyHandBlockInspectionPatch),nameof(RebirthEmptyHandBlockInspectionPatch.Postfix)));harmony.Patch(m,prefix:new HarmonyMethod(typeof(RebirthSalvageAcceptedHitWitness),nameof(RebirthSalvageAcceptedHitWitness.Prefix)),postfix:new HarmonyMethod(typeof(RebirthSalvageAcceptedHitWitness),nameof(RebirthSalvageAcceptedHitWitness.Postfix)),finalizer:new HarmonyMethod(typeof(RebirthSalvageAcceptedHitWitness),nameof(RebirthSalvageAcceptedHitWitness.Finalizer)));count++;}}return count;}
    private static int CountPlayerItem(EntityPlayer player,int type){int total=0;CountSlots(player!=null&&player.inventory!=null?player.inventory.ItemGrid.items:null,type,ref total);CountSlots(player!=null&&player.bag!=null?player.bag.ItemGrid.items:null,type,ref total);return total;}
    private static void CountSlots(ItemStack[] slots,int type,ref int total){if(slots==null)return;for(int i=0;i<slots.Length;i++)if(slots[i]!=null&&slots[i].itemValue!=null&&slots[i].itemValue.type==type)total+=Math.Max(0,slots[i].count);}
    private static ItemValue FindItem(ItemStack[] a,ItemStack[] b,int type,ushort seed,ushort quality){ItemValue v=FindItem(a,type,seed,quality);return v??FindItem(b,type,seed,quality);}
    private static ItemValue FindItem(ItemStack[] slots,int type,ushort seed,ushort quality){if(slots==null)return null;for(int i=0;i<slots.Length;i++){ItemValue v=slots[i]!=null?slots[i].itemValue:null;if(v!=null&&v.type==type&&v.Seed==seed&&v.Quality==quality)return v;}return null;}
    private static bool ReplacePlayerItem(EntityPlayer player,ItemValue corrected)=>false; // legacy marker-only correction is inadmissible
    private static object ReadMember(object target,string name){if(target==null)return null;Type t=target.GetType();BindingFlags f=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;FieldInfo fi=t.GetField(name,f);if(fi!=null)return fi.GetValue(target);PropertyInfo pi=t.GetProperty(name,f);return pi!=null?pi.GetValue(target,null):null;}
    private static int ReadInt(object target,string name,int fallback){try{object o=ReadMember(target,name);return o!=null?Convert.ToInt32(o):fallback;}catch{return fallback;}}
    private static List<RebirthPendingMetalCraftSignature> GetPendingSnapshot(int playerId){lock(Gate){CleanupLocked();List<RebirthPendingMetalCraftSignature> l;return PendingMetal.TryGetValue(playerId,out l)?new List<RebirthPendingMetalCraftSignature>(l):new List<RebirthPendingMetalCraftSignature>();}}
    private static void RemovePending(RebirthPendingMetalCraftSignature p){if(p==null)return;lock(Gate){List<RebirthPendingMetalCraftSignature> l;if(PendingMetal.TryGetValue(p.PlayerId,out l))l.Remove(p);}}
    private static void CleanupLocked(){DateTime now=DateTime.UtcNow;if(now<NextCleanupUtc)return;NextCleanupUtc=now.AddSeconds(1);DateTime cutoff=now.AddSeconds(-PendingSeconds);List<int> empty=new List<int>();foreach(KeyValuePair<int,List<RebirthPendingMetalCraftSignature>> kv in PendingMetal){kv.Value.RemoveAll(delegate(RebirthPendingMetalCraftSignature p){return p==null||p.CreatedUtc<cutoff;});if(kv.Value.Count==0)empty.Add(kv.Key);}for(int i=0;i<empty.Count;i++)PendingMetal.Remove(empty[i]);ClientPendingCorrections.RemoveAll(delegate(RebirthMetalCraftCorrection x){return x==null||x.CreatedUtc<cutoff;});}
    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        RebirthMetalHeatTreatmentCorrection.Pump(GameManager.Instance?.World);
        lock(Gate)CleanupLocked();
    }
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){ClearRuntime();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){ClearRuntime();}
    private static void ClearRuntime(){RebirthSalvageAcceptedHitWitness.Clear();lock(Gate){PendingMetal.Clear();MetalCorrections.Clear();MechanicReplayDamage.Clear();ClientPendingCorrections.Clear();}}
}

// The native owner owns RNG/collection. Server admission additionally requires a
// recent original accepted native hit; client-supplied before damage is not proof.
internal static class RebirthSalvageAcceptedHitWitness
{
    internal sealed class Hit
    {
        internal Hit Previous;
        internal World World;internal EntityPlayer Actor;internal Vector3i Position;
        internal BlockValue Before,After;internal float Until;internal int Operation;
        internal readonly HashSet<string> Claimed=new HashSet<string>(StringComparer.Ordinal);
    }
    private static readonly List<Hit> Hits=new List<Hit>();
    [ThreadStatic]private static Hit currentHit;
    private static int nextOperation;
    private sealed class AuditCounts{internal long Accepted,Claimed;}
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,AuditCounts> Audit=new System.Runtime.CompilerServices.ConditionalWeakTable<EntityPlayer,AuditCounts>();
    public static Newtonsoft.Json.Linq.JObject CaptureAuditState(EntityPlayer player)
    {
        AuditCounts counts=null;if(player!=null)Audit.TryGetValue(player,out counts);
        return new Newtonsoft.Json.Linq.JObject{["available"]=player!=null,["actor"]=player?.entityId??-1,
            ["accepted"]=counts?.Accepted??0,["claimed"]=counts?.Claimed??0,
            ["currentWitnesses"]=Hits.FindAll(hit=>ReferenceEquals(hit.Actor,player)&&ReferenceEquals(hit.World,GameManager.Instance?.World)&&hit.Until>=Time.realtimeSinceStartup).Count};
    }
    internal static void InstallSourceInvalidation(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(NetPackageSetBlock),nameof(NetPackageSetBlock.ProcessPackage)),
            prefix:new HarmonyMethod(typeof(RebirthSalvageAcceptedHitWitness),nameof(PackagePrefix)),
            postfix:new HarmonyMethod(typeof(RebirthSalvageAcceptedHitWitness),nameof(PackagePostfix)));
        var seen=new HashSet<MethodBase>();
        foreach(var assembly in new[]{typeof(Block).Assembly,typeof(RebirthSalvageAcceptedHitWitness).Assembly})
        {
            Type[] types;try{types=assembly.GetTypes();}catch(ReflectionTypeLoadException error){types=error.Types;}
            foreach(var type in types)
            {
                if(type==null||!typeof(Block).IsAssignableFrom(type))continue;
                foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                {
                    if(method.Name!=nameof(Block.OnBlockAdded)&&method.Name!=nameof(Block.OnBlockRemoved))continue;
                    var args=method.GetParameters();
                    if(args.Length<3||args[0].ParameterType!=typeof(WorldBase)||args[2].ParameterType!=typeof(Vector3i)||!seen.Add(method))continue;
                    harmony.Patch(method,prefix:new HarmonyMethod(typeof(RebirthSalvageAcceptedHitWitness),nameof(SourceChanged)));
                }
            }
        }
    }
    public static void SourceChanged(WorldBase __0,Vector3i __2)
    {
        if(ThreadManager.IsMainThread())Hits.RemoveAll(hit=>ReferenceEquals(hit.World,__0)&&hit.Position.Equals(__2));
    }
    public static void PackagePrefix(NetPackageSetBlock __instance,World _world,out List<Hit> __state)
    {
        __state=null;
        if(!ThreadManager.IsMainThread()||_world==null||_world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,_world)||
            __instance?.Sender==null||__instance.blockChanges==null||__instance.blockChanges.Count>512||
            !__instance.ValidEntityIdForSender(__instance.localPlayerThatChanged)||!__instance.ValidUserIdForSender(__instance.persistentPlayerId))return;
        var player=_world.GetEntity(__instance.localPlayerThatChanged) as EntityPlayer;
        if(player==null||player is EntityPlayerLocal)return;
        var rows=new List<Hit>();var positions=new HashSet<Vector3i>();
        foreach(var change in __instance.blockChanges)
        {
            if(change.blockValueRef.Type!=BlockValueRefType.Block||!positions.Add(change.blockValueRef.BlockPosition))continue;
            var pos=change.blockValueRef.BlockPosition;var before=_world.GetBlock(pos);var after=change.blockValue;
            if(before.isair||before.damage<0||before.Equals(after))continue;
            bool partial=before.rawData==after.rawData&&after.damage>before.damage;
            bool final=after.isair||before.Block?.DowngradeBlock.type==after.type;
            if(!partial&&!final)continue;
            rows.Add(new Hit{World=_world,Actor=player,Position=pos,Before=before,After=after});
        }
        __state=rows;
    }
    public static void PackagePostfix(List<Hit> __state,bool __runOriginal)
    {
        if(!__runOriginal||__state==null)return;
        foreach(var hit in __state)
            if(ReferenceEquals(GameManager.Instance?.World,hit.World)&&ReferenceEquals(hit.World.GetEntity(hit.Actor.entityId),hit.Actor)&&
                hit.World.GetBlock(hit.Position).Equals(hit.After))Record(hit);
    }
    internal static int NativeCollectionCount(ItemActionData data,int count,bool scale)
    {
        if(count<=0||data?.attackDetails==null)return 0;
        if(!scale)return count;
        var details=data.attackDetails;
        if(details.damageMax<=0||float.IsNaN(details.damageTotalOfTarget)||float.IsInfinity(details.damageTotalOfTarget))return 0;
        float per=(float)details.damageMax/count;
        int previous=(int)((Utils.FastMin(details.damageTotalOfTarget,details.damageMax)-details.damageGiven)/per+0.5f);
        int current=Mathf.Min((int)(details.damageTotalOfTarget/per+0.5f),count);
        int produced=current-previous;
        if(details.damageTotalOfTarget>details.damageMax)produced=Mathf.Min(produced,count);
        return Math.Max(0,produced);
    }
    public static void Prefix(WorldBase __0,BlockValueRef __1,BlockValue __2,int __3,int __4,bool __6,out Hit __state)
    {
        __state=new Hit{Previous=currentHit};currentHit=__state;var world=__0 as World;
        if(!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)||
            __3<=0||__1.Type!=BlockValueRefType.Block||__2.damage<0)return;
        var player=world.GetEntity(__4) as EntityPlayer;if(player==null)return;
        __state.World=world;__state.Actor=player;__state.Position=__1.BlockPosition;__state.Before=__2;
    }
    public static void Postfix(Hit __state,int __result,bool __runOriginal)
    {
        if(__state?.World==null||!__runOriginal||__result<=__state.Before.damage)return;
        for(var parent=__state.Previous;parent!=null;parent=parent.Previous)
            if(ReferenceEquals(parent.World,__state.World)&&ReferenceEquals(parent.Actor,__state.Actor)&&
                parent.Position.Equals(__state.Position)&&parent.Before.Equals(__state.Before))return;
        var post=__state.World.GetBlock(__state.Position);if(post.Equals(__state.Before))return;
        __state.After=post;Record(__state);
    }
    private static void Record(Hit hit)
    {
        Prune();hit.Until=Time.realtimeSinceStartup+10f;
        hit.Operation=++nextOperation;if(nextOperation<=0)hit.Operation=nextOperation=1;
        Hits.Add(hit);Audit.GetOrCreateValue(hit.Actor).Accepted++;if(Hits.Count>512)Hits.RemoveAt(0);
    }
    public static Exception Finalizer(Exception __exception,Hit __state)
    {if(__state!=null&&ReferenceEquals(currentHit,__state))currentHit=__state.Previous;return __exception;}
    private static void Prune()=>Hits.RemoveAll(hit=>hit.Until<Time.realtimeSinceStartup||!ReferenceEquals(hit.World,GameManager.Instance?.World));
    internal static bool TryClaim(EntityPlayer player,Vector3i pos,string name,uint raw,int before,string consumer)
        =>TryClaim(player,pos,name,raw,before,consumer,out _);
    internal static bool TryClaim(EntityPlayer player,Vector3i pos,string name,uint raw,int before,string consumer,out int operation)
    {
        operation=0;if(!ThreadManager.IsMainThread()||player?.world==null||player.world.IsRemote()||before<0)return false;
        Prune();
        foreach(var hit in Hits)
        {
            if(!ReferenceEquals(hit.World,player.world)||!ReferenceEquals(hit.Actor,player)||!ReferenceEquals(player.world.GetEntity(player.entityId),player)||
                !hit.Position.Equals(pos)||hit.Before.rawData!=raw||hit.Before.damage!=before||
                !string.Equals(hit.Before.Block?.GetBlockName(),name,StringComparison.OrdinalIgnoreCase)||
                !player.world.GetBlock(pos).Equals(hit.After)||hit.Claimed.Contains(consumer))continue;
            hit.Claimed.Add(consumer);Audit.GetOrCreateValue(player).Claimed++;operation=hit.Operation;return true;
        }
        return false;
    }
    internal static void Clear(){Hits.Clear();nextOperation=0;currentHit=null;}
}
