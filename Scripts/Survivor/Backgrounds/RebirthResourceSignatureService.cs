using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

#nullable disable

public sealed class RebirthTreeContributorCredit
{
    public int PlayerId;
    public int BaseWood;
    public int SettledBaseWood;
    public int ToolType;
    public DateTime LastContributionUtc;
}

public sealed class RebirthTreeHarvestLedger
{
    public Vector3i Position;
    public string BlockName=string.Empty;
    public int BlockType;
    public int AuthoredWoodMax;
    public bool Destroyed;
    public DateTime DestroyedUtc;
    public DateTime LastTouchedUtc;
    public readonly Dictionary<int,RebirthTreeContributorCredit> Contributors=new Dictionary<int,RebirthTreeContributorCredit>();
}

internal sealed class RebirthTreeWoodPayout
{
    public int PlayerId;
    public int BaseWood;
    public int ToolType;
    public string BlockName=string.Empty;
}

internal sealed class RebirthTreeDamageEvidence
{
    public int PlayerId;
    public Vector3i Position;
    public string BlockName=string.Empty;
    public int Allowance;
    public DateTime CreatedUtc;
}

/// <summary>
/// Chunk H authority for deferred tree settlement, Professional Logging and Fire Resistant.
/// Tree wood is withheld only after vanilla has calculated the exact item/count/probability result;
/// the server independently requires recent tree-damage evidence before accepting that deferred
/// credit. Destruction settles every contributor separately, preventing last-hit theft.
/// </summary>
public static class RebirthResourceSignatureService
{
    public const string ProfessionalLoggingBonusId="background_bonus.professional_logging";
    public const string FireResistantBonusId="background_bonus.fire_resistant";
    private const double EvidenceLifetimeSeconds=2.5;
    private const double LedgerLifetimeSeconds=180.0;
    private const double DestructionSettleDelaySeconds=0.45;
    private const double PostDestructionRetentionSeconds=4.0;
    private static readonly object Gate=new object();
    private static DateTime NextCleanupUtc = DateTime.MinValue;
    private static readonly Dictionary<string,RebirthTreeHarvestLedger> Ledgers=new Dictionary<string,RebirthTreeHarvestLedger>(StringComparer.Ordinal);
    private static readonly List<RebirthTreeDamageEvidence> Evidence=new List<RebirthTreeDamageEvidence>();
    private static bool installed;
    private static int heatDamagePatchCount;
    private static float nextFireDurationPass;
    private static readonly Dictionary<int,float> HeatDamageResidual=new Dictionary<int,float>();

    public static string Install(Harmony harmony)
    {
        if(installed)return "[REBIRTH Resource Signatures] already installed";
        installed=true;
        if(harmony!=null)heatDamagePatchCount=PatchHeatDamageMethods(harmony);
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Resource Signatures] installed heatDamagePatches="+heatDamagePatchCount;
    }

    public static float GetTuning(string bonusId,string key,float fallback)
    {
        RebirthBackgroundBonusDefinition d;RebirthBackgroundBonusTuningValue v;float f;
        return RebirthBackgroundBonusRegistry.TryGet(bonusId,out d)&&d!=null&&d.TryGetTuning(key,out v)&&v!=null&&float.TryParse(v.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out f)?f:fallback;
    }

    public static bool IsTree(BlockValue value){return !value.isair&&value.Block is BlockModelTree;}
    public static bool IsTree(Block block){return block is BlockModelTree;}
    public static bool IsWood(ItemValue value){return value!=null&&value.ItemClass!=null&&string.Equals(value.ItemClass.GetItemName(),"resourceWood",StringComparison.OrdinalIgnoreCase);}
    public static bool IsLoggingTool(ItemValue value,string blockName){return string.Equals(RebirthProgressionRuntimeConfig.ClassifyHarvest(value,blockName),"skill.logging",StringComparison.OrdinalIgnoreCase);}

    public static bool TryWithholdTreeWood(ItemActionData actionData,ItemValue item,int incomingCount,float probability,bool scaleCountOnDamage,ref GameRandom random)
    {
        EntityPlayerLocal player=actionData!=null&&actionData.invData!=null?actionData.invData.holdingEntity as EntityPlayerLocal:null;
        if(player==null||player.world==null||actionData.attackDetails==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!IsWood(item))return false;
        // Block-only ledger/evidence hooks cannot authorize prop-ref tree credit.
        // Preserve native harvest instead of withholding wood that can never settle.
        if(actionData.attackDetails.hitRef.Type!=BlockValueRefType.Block)return false;
        BlockValue tree=actionData.attackDetails.blockBeingDamaged;
        if(!IsTree(tree))return false;
        string blockName=tree.Block.GetBlockName()??string.Empty;

        ConnectionManager connection=null;
        NetPackageRebirthTreeWoodCreditRequest request=null;
        if(player.world.IsRemote())
        {
            connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
            if(connection==null||connection.IsServer||!connection.IsConnected)return false;
            try
            {
                NetPackageManager.GetPackageId(typeof(NetPackageRebirthTreeWoodCreditRequest));
                request=NetPackageManager.GetPackage<NetPackageRebirthTreeWoodCreditRequest>();
                if(request==null)return false;
            }
            catch{return false;} // Native harvest remains untouched when packet admission is unavailable.
            var channels=connection.GetConnectionToServer();int channel=request.Channel;
            if(channels==null||channel<0||channel>=channels.Length||channels[channel]==null||channels[channel].IsDisconnected())return false;
        }

        if(random==null){random=GameRandomManager.Instance.CreateGameRandom();random.SetSeed((int)System.Diagnostics.Stopwatch.GetTimestamp());}
        int count=incomingCount;
        if(scaleCountOnDamage&&count>0)
        {
            float per=(float)actionData.attackDetails.damageMax/(float)count;
            if(per>0f)
            {
                int before=(int)((Utils.FastMin(actionData.attackDetails.damageTotalOfTarget,(float)actionData.attackDetails.damageMax)-actionData.attackDetails.damageGiven)/per+0.5f);
                int through=Mathf.Min((int)(actionData.attackDetails.damageTotalOfTarget/per+0.5f),count);
                int cap=count;count=through-before;
                if(actionData.attackDetails.damageTotalOfTarget>actionData.attackDetails.damageMax)count=Mathf.Min(count,cap);
            }
        }
        if(random.RandomFloat>probability||count<=0)return true; // Match native probability draw ordering, including zero-count damage slices.

        int toolType=actionData.invData.itemValue!=null?actionData.invData.itemValue.type:0;
        Vector3i pos=actionData.attackDetails.raycastHitPosition;
        if(player.world.IsRemote())
        {
            connection.SendToServer(request.Setup(player.entityId,pos,blockName,tree.type,toolType,count));
        }
        else
            SubmitTreeWoodCredit(player,pos,blockName,tree.type,toolType,count);
        return true;
    }

    public static void RecordTreeDamageEvidence(WorldBase world,Vector3i pos,BlockValue value,int playerId,int damagePoints,bool useHarvestTool)
    {
        if(world==null||world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||damagePoints<=0||!IsTree(value))return;
        EntityPlayer player=world.GetEntity(playerId) as EntityPlayer;if(player==null)return;
        string blockName=value.Block.GetBlockName()??string.Empty;
        int authored=GetAuthoredTreeWoodMax(value.Block);if(authored<=0)return;
        float fraction=Mathf.Clamp01((float)Math.Min(damagePoints,Math.Max(1,value.Block.MaxDamage))/(float)Math.Max(1,value.Block.MaxDamage));
        // Evidence is deliberately generous enough for native HarvestCount/modifier channels but remains server bounded.
        int allowance=Mathf.Clamp(Mathf.CeilToInt(authored*Mathf.Max(0.08f,fraction)*4f),1,Math.Max(4,authored*4));
        lock(Gate)
        {
            CleanupLocked();
            Evidence.Add(new RebirthTreeDamageEvidence{PlayerId=playerId,Position=pos,BlockName=blockName,Allowance=allowance,CreatedUtc=DateTime.UtcNow});
            if(Evidence.Count>256)Evidence.RemoveRange(0,Evidence.Count-256);
        }
    }

    public static void SubmitTreeWoodCredit(EntityPlayer player,Vector3i pos,string blockName,int blockType,int toolType,int requestedCount)
    {
        if(player==null||player.world==null||player.world.IsRemote()||requestedCount<=0||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        if((player.position-pos.ToVector3()).sqrMagnitude>225f)return;
        Block block=ResolveTreeBlock(blockName,blockType);if(block==null)return;
        if(toolType>0&&ItemClass.GetForId(toolType)==null)return;
        int authored=GetAuthoredTreeWoodMax(block);if(authored<=0)return;
        int accepted=ConsumeEvidence(player.entityId,pos,blockName,requestedCount);
        if(accepted<=0)return;
        string key=TreeKey(pos,blockName);
        lock(Gate)
        {
            CleanupLocked();
            RebirthTreeHarvestLedger ledger;
            if(!Ledgers.TryGetValue(key,out ledger)||ledger==null)
            {
                ledger=new RebirthTreeHarvestLedger{Position=pos,BlockName=blockName,BlockType=blockType,AuthoredWoodMax=authored,LastTouchedUtc=DateTime.UtcNow};
                Ledgers[key]=ledger;
            }
            int totalCap=Math.Max(16,ledger.AuthoredWoodMax*12);
            RebirthTreeContributorCredit credit;
            if(!ledger.Contributors.TryGetValue(player.entityId,out credit)||credit==null)
            {
                credit=new RebirthTreeContributorCredit{PlayerId=player.entityId,ToolType=toolType};ledger.Contributors[player.entityId]=credit;
            }
            int room=Math.Max(0,totalCap-credit.BaseWood);accepted=Math.Min(accepted,room);if(accepted<=0)return;
            credit.BaseWood+=accepted;credit.ToolType=toolType;credit.LastContributionUtc=DateTime.UtcNow;ledger.LastTouchedUtc=DateTime.UtcNow;
        }
    }

    public static void OnTreeDestroyedServer(WorldBase world,Vector3i pos,BlockValue tree,int entityId,bool useHarvestTool)
    {
        if(world==null||world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!IsTree(tree))return;
        string name=tree.Block.GetBlockName()??string.Empty;string key=TreeKey(pos,name);int authored=GetAuthoredTreeWoodMax(tree.Block);
        lock(Gate)
        {
            RebirthTreeHarvestLedger ledger;
            if(!Ledgers.TryGetValue(key,out ledger)||ledger==null){ledger=new RebirthTreeHarvestLedger{Position=pos,BlockName=name,BlockType=tree.type,AuthoredWoodMax=authored};Ledgers[key]=ledger;}
            ledger.Destroyed=true;ledger.DestroyedUtc=DateTime.UtcNow;ledger.LastTouchedUtc=DateTime.UtcNow;
        }
    }

    public static void GrantTreeWood(int playerId,int itemType,int count,int toolType,string treeBlockName)
    {
        if(count<=0||GameManager.Instance==null||GameManager.Instance.World==null)return;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;EntityPlayer p=GameManager.Instance.World.GetEntity(playerId) as EntityPlayer;
        if(c!=null&&c.IsServer&&!(p is EntityPlayerLocal))
        {c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthTreeWoodGrant>().Setup(playerId,itemType,count,toolType,treeBlockName),_attachedToEntityId:playerId);return;}
        EntityPlayerLocal local=GameManager.Instance.World.GetPrimaryPlayer();if(local==null||local.entityId!=playerId)return;
        ItemValue value=new ItemValue(itemType);if(value.ItemClass==null)return;ItemStack stack=new ItemStack(value,count);int delivered=count;
        LocalPlayerUI ui=LocalPlayerUI.GetUIForPlayer(local);if(ui!=null&&ui.xui!=null)ui.xui.PlayerInventory.AddItem(stack);
        if(stack.count>0)local.world.gameManager.ItemDropServer(stack,local.GetDropPosition(),Vector3.zero,local.entityId,60f,false);
        try
        {
            ItemValue tool=toolType>0?new ItemValue(toolType):local.inventory.holdingItemItemValue;BlockValue tree=Block.GetBlockValue(treeBlockName);
            QuestEventManager.Current.HarvestedItem(tool,new ItemStack(value,delivered),tree);
            local.Progression.AddLevelExp((int)(value.ItemClass.MadeOfMaterial.Experience*(float)delivered),"_xpFromHarvesting",global::Progression.XPTypes.Harvesting,true);
        }
        catch(Exception ex){Log.Warning("[REBIRTH Logging] payout event/XP failed: "+ex.Message);}
    }

    public static void RefundLoggerCuttingStamina(ItemActionMelee.InventoryDataMelee actionData,WorldRayHitInfo hitInfo)
    {
        EntityPlayer player=actionData!=null&&actionData.invData!=null?actionData.invData.holdingEntity as EntityPlayer:null;
        if(player==null||hitInfo==null||!hitInfo.bHitValid||player.Buffs==null||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthBackgroundBonusService.HasBonus(player,ProfessionalLoggingBonusId))return;
        Vector3i pos=hitInfo.hit.blockPos;BlockValue bv=actionData.invData.world.GetBlock(pos);if(!IsTree(bv))return;
        string name=bv.Block.GetBlockName()??string.Empty;if(!IsLoggingTool(actionData.invData.itemValue,name))return;
        FastTags<TagGroup.Global> tags=actionData.indexInEntityOfAction==0?FastTags<TagGroup.Global>.Parse("primary"):FastTags<TagGroup.Global>.Parse("secondary");
        float native=EffectManager.GetValue(PassiveEffects.StaminaLoss,actionData.invData.itemValue,_entity:player,tags:tags);if(native<=0f)return;
        float mult=Mathf.Clamp(GetTuning(ProfessionalLoggingBonusId,"cutting_stamina_multiplier",0.75f),0.25f,1f);
        float refund=native*(1f-mult);if(refund>0f)player.AddStamina(refund);
    }

    public static void HeatDamagePrefix(EntityAlive __instance,DamageSource _damageSource,ref int _strength)
    {
        EntityPlayer player=__instance as EntityPlayer;if(player==null||player.world==null||player.world.IsRemote()||_damageSource==null||_strength<=0||!RebirthSurvivorMode.IsEnabledForCurrentWorld()||!RebirthBackgroundBonusService.HasBonus(player,FireResistantBonusId))return;
        if(_damageSource.GetDamageType()!=EnumDamageTypes.Heat)return;
        float mult=Mathf.Clamp(GetTuning(FireResistantBonusId,"fire_damage_multiplier",0.5f),0.1f,1f);
        float residual;HeatDamageResidual.TryGetValue(player.entityId,out residual);float scaled=_strength*mult+residual;int applied=Mathf.FloorToInt(scaled);HeatDamageResidual[player.entityId]=scaled-applied;_strength=Math.Max(0,applied);
    }

    private static int PatchHeatDamageMethods(Harmony harmony)
    {
        Type[] args={typeof(DamageSource),typeof(int),typeof(bool),typeof(float)};
        MethodInfo target=AccessTools.DeclaredMethod(typeof(EntityPlayer),nameof(EntityPlayer.DamageEntity),args) ?? AccessTools.DeclaredMethod(typeof(EntityAlive),nameof(EntityAlive.DamageEntity),args);
        MethodInfo prefix=AccessTools.Method(typeof(RebirthResourceSignatureService),nameof(HeatDamagePrefix));
        if(target==null||prefix==null)return 0;harmony.Patch(target,prefix:new HarmonyMethod(prefix));return 1;
    }

    private static int ConsumeEvidence(int playerId,Vector3i pos,string blockName,int requested)
    {
        lock(Gate)
        {
            CleanupLocked();
            for(int i=Evidence.Count-1;i>=0;i--)
            {
                RebirthTreeDamageEvidence e=Evidence[i];if(e==null||e.PlayerId!=playerId||e.Position!=pos||!string.Equals(e.BlockName,blockName,StringComparison.OrdinalIgnoreCase)||e.Allowance<=0)continue;
                int take=Math.Min(requested,e.Allowance);e.Allowance-=take;if(e.Allowance<=0)Evidence.RemoveAt(i);return take;
            }
        }
        return 0;
    }

    private static int GetAuthoredTreeWoodMax(Block block)
    {
        if(block==null||!IsTree(block)||block.itemsToDrop==null)return 0;List<Block.SItemDropProb> drops;int total=0;
        if(block.itemsToDrop.TryGetValue(EnumDropEvent.Harvest,out drops)&&drops!=null)for(int i=0;i<drops.Count;i++)if(string.Equals(drops[i].name,"resourceWood",StringComparison.OrdinalIgnoreCase))total+=Math.Max(0,drops[i].maxCount);
        return Math.Min(total,512);
    }
    private static Block ResolveTreeBlock(string name,int type){try{if(type>0&&type<Block.list.Length&&Block.list[type] is BlockModelTree)return Block.list[type];BlockValue v=Block.GetBlockValue(name);return v.Block as BlockModelTree;}catch{return null;}}
    private static string TreeKey(Vector3i p,string n){return p.x+","+p.y+","+p.z+"|"+(n??string.Empty).ToLowerInvariant();}

    private static void SettleReadyLedgers(World world)
    {
        // Resolve the payout resource before consuming contributor credit.
        int woodType=ItemClass.GetItem("resourceWood")?.type??0;if(woodType<=0)return;
        List<RebirthTreeWoodPayout> payouts=null;DateTime now=DateTime.UtcNow;
        lock(Gate)
        {
            CleanupLocked();
            if(Ledgers.Count==0)return;
            List<string> remove=null;
            foreach(KeyValuePair<string,RebirthTreeHarvestLedger> kv in Ledgers)
            {
                RebirthTreeHarvestLedger l=kv.Value;if(l==null||!l.Destroyed)continue;double age=(now-l.DestroyedUtc).TotalSeconds;if(age<DestructionSettleDelaySeconds)continue;
                foreach(KeyValuePair<int,RebirthTreeContributorCredit> ck in l.Contributors)
                {
                    RebirthTreeContributorCredit c=ck.Value;if(c==null||world.GetEntity(c.PlayerId) as EntityPlayer==null)continue;int unsettled=Math.Max(0,c.BaseWood-c.SettledBaseWood);if(unsettled<=0)continue;
                    c.SettledBaseWood+=unsettled;
                    if(payouts==null)payouts=new List<RebirthTreeWoodPayout>();
                    payouts.Add(new RebirthTreeWoodPayout{PlayerId=c.PlayerId,BaseWood=unsettled,ToolType=c.ToolType,BlockName=l.BlockName});
                }
                if(age>=PostDestructionRetentionSeconds){if(remove==null)remove=new List<string>();remove.Add(kv.Key);}
            }
            if(remove!=null)for(int i=0;i<remove.Count;i++)Ledgers.Remove(remove[i]);
        }
        if(payouts==null)return;

        for(int i=0;i<payouts.Count;i++)
        {
            RebirthTreeWoodPayout q=payouts[i];if(q==null||q.BaseWood<=0)continue;EntityPlayer player=world.GetEntity(q.PlayerId) as EntityPlayer;if(player==null)continue;
            float mult=RebirthBackgroundBonusService.HasBonus(player,ProfessionalLoggingBonusId)?Mathf.Max(1f,GetTuning(ProfessionalLoggingBonusId,"wood_output_multiplier",1.5f)):1f;
            float raw=q.BaseWood*mult;int payout=Mathf.FloorToInt(raw);float frac=raw-payout;if(frac>0f&&world.GetGameRandom().RandomFloat<frac)payout++;if(payout>0)GrantTreeWood(q.PlayerId,woodType,payout,q.ToolType,q.BlockName);
        }
    }

    private static void AdjustFirefighterBurnTimers(World world)
    {
        if(world==null||world.Players==null||world.Players.list==null)return;
        List<EntityPlayer> players=world.Players.list;
        for(int i=0;i<players.Count;i++)
        {
            EntityPlayer p=players[i];if(p==null||p.Buffs==null||!RebirthBackgroundBonusService.HasBonus(p,FireResistantBonusId))continue;
            float mult=Mathf.Clamp(GetTuning(FireResistantBonusId,"burn_duration_multiplier",0.5f),0.1f,1f);
            CapCVar(p,"$buffBurningMolotovDuration",16f*mult);CapCVar(p,"$buffBurningFlamingArrowDuration",14f*mult);CapCVar(p,"$buffBurningElementDuration",10f*mult);CapCVar(p,"$BurningEnvironmentDuration",1f*mult);
            if(p.Buffs.ActiveBuffs==null)continue;
            for(int j=0;j<p.Buffs.ActiveBuffs.Count;j++)
            {
                BuffValue v=p.Buffs.ActiveBuffs[j];BuffClass bc=v!=null?v.BuffClass:null;if(bc==null||bc.DamageType!=EnumDamageTypes.Heat||bc.DurationMax<=0f||!IsAuthoredBurnBuff(bc.Name))continue;
                float minElapsed=bc.DurationMax*(1f-mult);if(v.DurationInSeconds<minElapsed)v.DurationInTicks=(uint)Mathf.Max(0,Mathf.CeilToInt(minElapsed*20f));
            }
        }
    }
    private static bool IsAuthoredBurnBuff(string name){string n=(name??string.Empty).ToLowerInvariant();return n.StartsWith("buffburning")||n=="buffisonfire"||n.StartsWith("furiousramsayfire")||n.Contains("burningtrap");}
    private static void CapCVar(EntityPlayer p,string key,float cap){float v=p.Buffs.GetCustomVar(key);if(v>cap&&cap>=0f)p.Buffs.SetCustomVar(key,cap);}

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        if(GameManager.Instance==null||GameManager.Instance.World==null)return;World world=GameManager.Instance.World;ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(world.IsRemote()||c==null||!c.IsServer||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        SettleReadyLedgers(world);
        float now=Time.realtimeSinceStartup;if(now>=nextFireDurationPass){nextFireDurationPass=now+0.20f;AdjustFirefighterBurnTimers(world);}
    }
    private static void CleanupLocked(){if(Evidence.Count==0&&Ledgers.Count==0)return;DateTime now=DateTime.UtcNow;if(now<NextCleanupUtc)return;NextCleanupUtc=now.AddSeconds(1);Evidence.RemoveAll(e=>e==null||(now-e.CreatedUtc).TotalSeconds>EvidenceLifetimeSeconds);List<string> dead=new List<string>();foreach(KeyValuePair<string,RebirthTreeHarvestLedger> kv in Ledgers)if(kv.Value==null||(now-kv.Value.LastTouchedUtc).TotalSeconds>LedgerLifetimeSeconds)dead.Add(kv.Key);for(int i=0;i<dead.Count;i++)Ledgers.Remove(dead[i]);}
    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data){ClearRuntime();}
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data){ClearRuntime();}
    private static void ClearRuntime(){lock(Gate){Ledgers.Clear();Evidence.Clear();HeatDamageResidual.Clear();}nextFireDurationPass=0f;NextCleanupUtc=DateTime.MinValue;}

    public static string BuildDebugReport(EntityPlayer player)
    {
        StringBuilder b=new StringBuilder();b.AppendLine("[REBIRTH Chunk H Resource Signatures]");lock(Gate)b.AppendLine("treeLedgers="+Ledgers.Count+" evidence="+Evidence.Count);
        b.AppendLine("logger woodMultiplier="+GetTuning(ProfessionalLoggingBonusId,"wood_output_multiplier",1.5f).ToString("0.###")+" cuttingStaminaMultiplier="+GetTuning(ProfessionalLoggingBonusId,"cutting_stamina_multiplier",0.75f).ToString("0.###"));
        b.AppendLine("firefighter heatDamageMultiplier="+GetTuning(FireResistantBonusId,"fire_damage_multiplier",0.5f).ToString("0.###")+" burnDurationMultiplier="+GetTuning(FireResistantBonusId,"burn_duration_multiplier",0.5f).ToString("0.###")+" patchedMethods="+heatDamagePatchCount);
        if(player!=null)b.AppendLine("player="+player.entityId+" logging="+RebirthBackgroundBonusService.HasBonus(player,ProfessionalLoggingBonusId)+" firefighter="+RebirthBackgroundBonusService.HasBonus(player,FireResistantBonusId));return b.ToString().TrimEnd();
    }
}

[HarmonyPatch(typeof(GameUtils),"collectHarvestedItem")]
internal static class RebirthTreeHarvestSettlementPatch
{
    private static bool Prefix(ItemActionData _actionData,ItemValue _iv,int _count,float _prob,bool _bScaleCountOnDamage,ref GameRandom ___random)
    {return !RebirthResourceSignatureService.TryWithholdTreeWood(_actionData,_iv,_count,_prob,_bScaleCountOnDamage,ref ___random);}
}

[HarmonyPatch(typeof(ItemActionMelee),"hitTheTarget")]
internal static class RebirthLoggerCuttingCostPatch
{
    private static void Prefix(ItemActionMelee.InventoryDataMelee _actionData,WorldRayHitInfo hitInfo)
    {RebirthResourceSignatureService.RefundLoggerCuttingStamina(_actionData,hitInfo);}
}
