using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Phase 8 authoritative world/output training bridge.
/// One HarvestOnAttack call opens a scope, native collectHarvestedItem outcomes are observed,
/// and the scope closes with at most one award per migrated Skill route. Bonus-yield systems are
/// never used as direct multipliers: output routes are capped by exact authored target/work data.
/// </summary>
public static class RebirthPhase8WorldOutputTrainingService
{
    private sealed class CarcassProfile
    {
        public string EntityClass = string.Empty;
        public float BaseButcherUnits;
        public float DeadBodyHitPoints;
    }

    private sealed class HarvestScope
    {
        public ItemActionData ActionData;
        public EntityPlayer Player;
        public ItemValue HeldItem;
        public string WorldSkillId = string.Empty;
        public string BlockName = string.Empty;
        public Vector3i Position;
        public float CreditedWorldWork;
        public float WorldWorkFraction;
        public bool MatureCrop;
        public int OutputUnits;
        public float SalvageExpectedWeighted;
        public float SalvageObservedWeighted;
        public Dictionary<int,float> SalvageWeights;
        public EntityAlive Carcass;
        public CarcassProfile CarcassProfile;
    }

    public struct CollectedState
    {
        public ItemActionData ActionData;
        public EntityPlayer Player;
        public ItemValue Item;
        public int Before;
        public bool Eligible;
    }

    private static readonly Dictionary<string,CarcassProfile> Carcasses = new Dictionary<string,CarcassProfile>(StringComparer.OrdinalIgnoreCase);
    [ThreadStatic] private static HarvestScope activeScope;
    private static bool loaded;
    private static string loadStatus = "not_loaded";
    private static float salvageReferenceGain = .30f;
    private static float farmingMatureGain = .25f;
    private static float animalReferenceGain = .18f;
    private static float animalReferenceUnits = 97f;
    private static float animalToolWorkFactor = 1f;

    public static float SalvageReferenceGain { get { if(!loaded)Load(); return salvageReferenceGain; } }
    public static float FarmingMatureReferenceGain { get { if(!loaded)Load(); return farmingMatureGain; } }
    public static float AnimalProcessingReferenceGain { get { if(!loaded)Load(); return animalReferenceGain; } }

    public static string Load()
    {
        Carcasses.Clear(); loaded=false; loadStatus="not_loaded";
        string path=Path.Combine(RebirthSurvivorDefinitionLoader.ResolveConfigRoot(),"skill_training.xml");
        XDocument doc=XDocument.Load(path); XElement root=doc.Root;
        XElement phase=root!=null?root.Element("phase8_output_work"):null;
        if(phase==null)throw new InvalidDataException("skill_training.xml missing <phase8_output_work>.");
        salvageReferenceGain=F(phase,"salvage_reference_gain",.000001f,.60f);
        farmingMatureGain=F(phase,"farming_mature_gain",.000001f,.60f);
        animalReferenceGain=F(phase,"animal_reference_gain",.000001f,.60f);
        animalReferenceUnits=F(phase,"animal_reference_units",.000001f,10000f);
        animalToolWorkFactor=F(phase,"animal_tool_work_factor",.000001f,1f);
        XElement carcasses=phase.Element("carcasses");
        if(carcasses==null)throw new InvalidDataException("skill_training.xml Phase 8 section missing <carcasses>.");
        foreach(XElement e in carcasses.Elements("carcass"))
        {
            string id=((string)e.Attribute("entity_class")??string.Empty).Trim();
            if(id.Length==0||Carcasses.ContainsKey(id))throw new InvalidDataException("Invalid/duplicate Phase 8 carcass entity_class '"+id+"'.");
            Carcasses.Add(id,new CarcassProfile{EntityClass=id,BaseButcherUnits=F(e,"base_butcher_units",0f,10000f),DeadBodyHitPoints=F(e,"dead_body_hit_points",.000001f,100000f)});
        }
        if(Carcasses.Count!=24)throw new InvalidDataException("Phase 8 exact carcass table requires 24 rows; found "+Carcasses.Count.ToString(CultureInfo.InvariantCulture)+".");
        int exactCropCount=RebirthCropActivationHarvestGeneratedCropTable.WildOrNaturalCount+RebirthCropActivationHarvestGeneratedCropTable.PlayerGrownCount;
        if(exactCropCount!=33)throw new InvalidDataException("Phase 8 exact mature-crop table requires 33 rows; found "+exactCropCount.ToString(CultureInfo.InvariantCulture)+".");
        loaded=true; loadStatus="crops=33 carcasses=24 salvage=.30 farming=.25 animal=.18";
        return "phase8="+loadStatus;
    }

    public static void BeginHarvest(ItemActionData actionData)
    {
        activeScope=null;
        if(!loaded||actionData==null||actionData.invData==null||actionData.attackDetails==null)return;
        EntityPlayer player=actionData.invData.holdingEntity as EntityPlayer;
        if(player==null||player.world==null||player.world.IsRemote()||!RebirthSurvivorMode.IsEnabledForCurrentWorld())return;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c==null||!c.IsServer)return;
        ItemValue held=actionData.invData.itemValue;
        if(held==null||held.IsEmpty())return;

        HarvestScope scope=new HarvestScope{ActionData=actionData,Player=player,HeldItem=held.Clone(),Position=actionData.attackDetails.raycastHitPosition};
        float damage=Math.Max(0f,actionData.attackDetails.damageGiven);
        float max=Math.Max(0f,actionData.attackDetails.damageMax);
        float total=Math.Max(0f,actionData.attackDetails.damageTotalOfTarget);
        float before=Math.Max(0f,total-damage);
        float remaining=max>0f?Math.Max(0f,max-before):damage;
        scope.CreditedWorldWork=max>0f?Math.Min(damage,remaining):damage;
        scope.WorldWorkFraction=max>0f?Mathf.Clamp01(scope.CreditedWorldWork/max):0f;

        if(actionData.attackDetails.bBlockHit)
        {
            Block block=actionData.attackDetails.blockBeingDamaged.Block;
            scope.BlockName=block!=null?(block.GetBlockName()??string.Empty):string.Empty;
            scope.WorldSkillId=RebirthProgressionRuntimeConfig.ClassifyHarvest(held,scope.BlockName);
            scope.MatureCrop=IsPlayerCropHarvest(scope.BlockName);
            if(string.Equals(scope.WorldSkillId,"skill.salvage",StringComparison.OrdinalIgnoreCase))PrepareSalvage(scope);
        }
        else
        {
            EntityAlive entity=actionData.attackDetails.entityHit as EntityAlive;
            if(entity!=null&&entity.IsDead()&&RebirthProgressionRuntimeConfig.IsAnimalProcessingTool(held))
            {
                string entityClass=entity.EntityClass!=null?(entity.EntityClass.entityClassName??string.Empty):string.Empty;
                CarcassProfile profile;
                if(Carcasses.TryGetValue(entityClass,out profile)&&profile!=null&&profile.BaseButcherUnits>0f)
                { scope.Carcass=entity;scope.CarcassProfile=profile; }
            }
        }

        bool continuous=scope.CreditedWorldWork>0f&&(scope.WorldSkillId=="skill.mining"||scope.WorldSkillId=="skill.logging");
        bool salvage=scope.SalvageExpectedWeighted>0f&&scope.CreditedWorldWork>0f;
        bool crop=scope.MatureCrop;
        bool carcass=scope.Carcass!=null&&scope.CarcassProfile!=null&&scope.CreditedWorldWork>0f;
        if(continuous||salvage||crop||carcass)activeScope=scope;
    }

    public static void CompleteHarvest(ItemActionData actionData,bool successful)
    {
        HarvestScope scope=activeScope; activeScope=null;
        if(!successful||scope==null||scope.Player==null||!ReferenceEquals(scope.ActionData,actionData))return;
        if(scope.CreditedWorldWork>0f&&(scope.WorldSkillId=="skill.mining"||scope.WorldSkillId=="skill.logging"))
            AwardContinuousWorld(scope);
        if(scope.SalvageExpectedWeighted>0f&&scope.SalvageObservedWeighted>0f&&scope.WorldWorkFraction>0f)
            AwardSalvage(scope);
        if(scope.MatureCrop)
        {
            float raw=CalculateFarmingHarvestRaw(scope.OutputUnits);
            if(raw>0f)
            {
                AwardDiscrete(scope.Player,"skill.farming",raw,"phase8:farming:"+scope.BlockName+":"+Pos(scope.Position),1f);
                RebirthTheoryProgressionService.TryAwardInsight(scope.Player,"insight.farming.successful_harvest",
                    "server-observed-mature-crop-output",out var insightTheory,out var insightAlreadyEarned);
            }
        }
        if(scope.Carcass!=null&&scope.CarcassProfile!=null&&scope.OutputUnits>0&&scope.CreditedWorldWork>0f)
            AwardAnimalProcessing(scope);
    }

    public static void AbortHarvest(ItemActionData actionData)
    {
        if(activeScope!=null&&ReferenceEquals(activeScope.ActionData,actionData))activeScope=null;
    }

    public static void CollectedPrefix(ItemActionData actionData,ItemValue item,out CollectedState state)
    {
        state=new CollectedState(); HarvestScope scope=activeScope;
        if(scope==null||actionData==null||item==null||item.IsEmpty()||!ReferenceEquals(scope.ActionData,actionData)||scope.Player==null)return;
        state.ActionData=actionData;state.Player=scope.Player;state.Item=item;state.Before=CountPlayerItem(scope.Player,item.type);state.Eligible=true;
    }

    public static void CollectedPostfix(CollectedState state)
    {
        if(!state.Eligible||state.Player==null||state.Item==null)return;
        HarvestScope scope=activeScope;
        if(scope==null||!ReferenceEquals(scope.ActionData,state.ActionData)||!ReferenceEquals(scope.Player,state.Player))return;
        int delta=CountPlayerItem(state.Player,state.Item.type)-state.Before;
        if(delta<=0)return;
        scope.OutputUnits+=delta;
        if(scope.SalvageWeights!=null)
        {
            float weight;
            if(scope.SalvageWeights.TryGetValue(state.Item.type,out weight)&&weight>0f)scope.SalvageObservedWeighted+=delta*weight;
        }
    }

    private static void AwardContinuousWorld(HarvestScope scope)
    {
        float rate;
        if(!RebirthWeaponSustainedDpsService.TryGetWorldWorkRate(scope.Player,scope.HeldItem,scope.CreditedWorldWork,out rate)||rate<=0f)return;
        RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence
        {
            SkillId=scope.WorldSkillId,SourceKey="phase8:world:"+scope.WorldSkillId+":"+Pos(scope.Position),ReferenceDescription="actual eligible world work",
            AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.ContinuousWork,CreditedWork=scope.CreditedWorldWork,LiveWorkRate=rate
        };
        RebirthSkillTrainingComputation ignored;float gained,attribute;
        RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(scope.Player,evidence,out ignored,out gained,out attribute);
    }

    private static void AwardSalvage(HarvestScope scope)
    {
        float credited;
        float raw=CalculateSalvageRaw(scope.SalvageObservedWeighted,scope.SalvageExpectedWeighted,scope.WorldWorkFraction,out credited);
        if(raw<=0f||credited<=0f)return;
        float ratio=Mathf.Clamp01(credited/scope.SalvageExpectedWeighted);
        AwardDiscrete(scope.Player,"skill.salvage",raw,
            "phase8:salvage:"+scope.BlockName+":"+Pos(scope.Position),ratio);
    }

    private static void AwardAnimalProcessing(HarvestScope scope)
    {
        CarcassProfile p=scope.CarcassProfile;
        if(p==null)return;
        float creditedUnits;
        float raw=CalculateAnimalProcessingRaw(p.BaseButcherUnits,p.DeadBodyHitPoints,scope.CreditedWorldWork,scope.OutputUnits,out creditedUnits);
        if(raw<=0f||creditedUnits<=0f)return;
        AwardDiscrete(scope.Player,"skill.animal_processing",raw,
            "phase8:animal-processing:"+scope.Carcass.entityId,p.BaseButcherUnits>0f?creditedUnits/p.BaseButcherUnits:0f);
    }

    /// <summary>
    /// Advanced Farming uses the exact same locked mature-harvest coefficient as native harvesting.
    /// Output quantity is only a successful-output witness; bonus yield never multiplies training.
    /// </summary>
    public static void AwardAdvancedFarmingHarvest(EntityPlayer player,string cropName,int outputCount)
    {
        if(!loaded)Load();
        float raw=CalculateFarmingHarvestRaw(outputCount);
        if(player==null||raw<=0f||!IsPlayerCropHarvest(cropName))return;
        AwardDiscrete(player,"skill.farming",raw,"phase8:farming:advanced:"+(cropName??string.Empty),1f);
        RebirthTheoryProgressionService.TryAwardInsight(player,"insight.farming.successful_harvest",
            "committed-advanced-crop-output",out var insightTheory,out var insightAlreadyEarned);
    }

    internal static bool IsPlayerCropHarvest(string name) => !string.IsNullOrEmpty(name) && name.EndsWith("3HarvestPlayer",StringComparison.OrdinalIgnoreCase);

    private static float CalculateFarmingHarvestRaw(int outputUnits)
    {
        return outputUnits>0?farmingMatureGain:0f;
    }

    private static float CalculateSalvageRaw(float observedWeighted,float expectedWeighted,float workFraction,out float creditedWeighted)
    {
        creditedWeighted=0f;
        if(observedWeighted<=0f||expectedWeighted<=0f||workFraction<=0f)return 0f;
        float cap=expectedWeighted*Mathf.Clamp01(workFraction);
        creditedWeighted=Math.Min(observedWeighted,cap);
        if(creditedWeighted<=0f)return 0f;
        return salvageReferenceGain*Mathf.Clamp01(creditedWeighted/expectedWeighted);
    }

    private static float CalculateAnimalProcessingRaw(float baseButcherUnits,float deadBodyHitPoints,float creditedWorldWork,int observedOutputUnits,out float creditedUnits)
    {
        creditedUnits=0f;
        if(baseButcherUnits<=0f||deadBodyHitPoints<=0f||creditedWorldWork<=0f||observedOutputUnits<=0)return 0f;
        float workFraction=Mathf.Clamp01(creditedWorldWork/deadBodyHitPoints);
        creditedUnits=Math.Min(observedOutputUnits,baseButcherUnits*workFraction);
        if(creditedUnits<=0f)return 0f;
        float outputFraction=Mathf.Clamp01(creditedUnits/baseButcherUnits);
        return animalReferenceGain*Mathf.Sqrt(baseButcherUnits/animalReferenceUnits)*outputFraction*animalToolWorkFactor;
    }

    private static void AwardDiscrete(EntityPlayer player,string skillId,float raw,string source,float creditedWork)
    {
        if(player==null||raw<=0f||creditedWork<=0f)return;
        RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence
        {
            SkillId=skillId,SourceKey=source,ReferenceDescription="successful authoritative Phase 8 outcome",AuthoritativeSuccess=true,
            Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=creditedWork,DiscreteRawAward=raw
        };
        RebirthSkillTrainingComputation ignored;float gained,attribute;
        RebirthSkillAwardService.TryAwardMigratedTrainingEvidence(player,evidence,out ignored,out gained,out attribute);
    }

    private static void PrepareSalvage(HarvestScope scope)
    {
        List<RebirthAuthoredSalvageDrop> drops=RebirthScavengerSalvageProfileService.GetAuthoredSalvageDrops(scope.BlockName);
        if(drops==null||drops.Count==0)return;
        RebirthSalvageProfileDefinition profile=RebirthScavengerSalvageProfileService.MatchProfile(scope.BlockName);
        Dictionary<int,float> weights=new Dictionary<int,float>();float expected=0f;
        for(int i=0;i<drops.Count;i++)
        {
            RebirthAuthoredSalvageDrop d=drops[i];if(d==null||d.MaxCount<=0||d.Probability<=0f||string.IsNullOrEmpty(d.ItemName))continue;
            float weight=1f;RebirthSalvageValuableEntry valuable=profile!=null?profile.FindValuable(d.ItemName):null;if(valuable!=null)weight=Math.Max(.01f,valuable.Weight);
            ItemValue item=ItemClass.GetItem(d.ItemName,false);if(item==null||item.IsEmpty())continue;
            weights[item.type]=weight;expected+=d.MaxCount*Mathf.Clamp01(d.Probability)*weight;
        }
        if(expected>0f){scope.SalvageWeights=weights;scope.SalvageExpectedWeighted=expected;}
    }

    public static string RunVectors()
    {
        if(!loaded)Load();int trainable=0;foreach(CarcassProfile p in Carcasses.Values)if(p!=null&&p.BaseButcherUnits>0f)trainable++;
        CarcassProfile bear,rabbit;Carcasses.TryGetValue("animalBear",out bear);Carcasses.TryGetValue("animalRabbit",out rabbit);
        float bearCredited=0f,rabbitCredited=0f,bearBonusCredited=0f,salvageCredited=0f;
        float bearFull=bear!=null?CalculateAnimalProcessingRaw(bear.BaseButcherUnits,bear.DeadBodyHitPoints,bear.DeadBodyHitPoints,(int)Math.Ceiling(bear.BaseButcherUnits),out bearCredited):0f;
        float bearBonus=bear!=null?CalculateAnimalProcessingRaw(bear.BaseButcherUnits,bear.DeadBodyHitPoints,bear.DeadBodyHitPoints,(int)Math.Ceiling(bear.BaseButcherUnits*4f),out bearBonusCredited):0f;
        float rabbitFull=rabbit!=null?CalculateAnimalProcessingRaw(rabbit.BaseButcherUnits,rabbit.DeadBodyHitPoints,rabbit.DeadBodyHitPoints,(int)Math.Ceiling(rabbit.BaseButcherUnits),out rabbitCredited):0f;
        float salvageQuarter=CalculateSalvageRaw(100f,10f,.25f,out salvageCredited);
        float farmZero=CalculateFarmingHarvestRaw(0),farmOne=CalculateFarmingHarvestRaw(1),farmBonus=CalculateFarmingHarvestRaw(99);
        int exactCropCount=RebirthCropActivationHarvestGeneratedCropTable.WildOrNaturalCount+RebirthCropActivationHarvestGeneratedCropTable.PlayerGrownCount;
        bool cropExamples=RebirthCropActivationHarvestGeneratedCropTable.Classify("plantedCorn3Harvest")!=RebirthCropActivationKind.Unknown&&
            RebirthCropActivationHarvestGeneratedCropTable.Classify("plantedCorn3HarvestPlayer")!=RebirthCropActivationKind.Unknown&&
            RebirthCropActivationHarvestGeneratedCropTable.Classify("plantedCorn2")==RebirthCropActivationKind.Unknown;
        bool ok=Carcasses.Count==24&&trainable==15&&exactCropCount==33&&cropExamples&&Math.Abs(bearFull-.18f)<.0005f&&rabbitFull>0f&&rabbitFull<bearFull&&
            Math.Abs(bearBonus-bearFull)<.0005f&&Math.Abs(bearBonusCredited-bearCredited)<.0005f&&
            Math.Abs(salvageQuarter-(salvageReferenceGain*.25f))<.0005f&&Math.Abs(salvageCredited-2.5f)<.0005f&&
            farmZero==0f&&Math.Abs(farmOne-farmingMatureGain)<.0005f&&Math.Abs(farmBonus-farmOne)<.0005f&&
            Math.Abs(salvageReferenceGain-.30f)<.0005f&&Math.Abs(farmingMatureGain-.25f)<.0005f;
        return "[REBIRTH Phase8 Vectors] "+(ok?"PASS":"FAIL")+" crops="+exactCropCount+" carcasses="+Carcasses.Count+" trainable="+trainable+
            " bearFull="+bearFull.ToString("0.###",CultureInfo.InvariantCulture)+" bearBonus="+bearBonus.ToString("0.###",CultureInfo.InvariantCulture)+
            " rabbitFull="+rabbitFull.ToString("0.###",CultureInfo.InvariantCulture)+" salvageQuarter="+salvageQuarter.ToString("0.###",CultureInfo.InvariantCulture)+
            " farm1="+farmOne.ToString("0.###",CultureInfo.InvariantCulture)+" farm99="+farmBonus.ToString("0.###",CultureInfo.InvariantCulture);
    }

    public static string BuildDebugReport()
    { return "[REBIRTH Phase8] "+loadStatus+" active="+(activeScope!=null); }

    private static float F(XElement e,string name,float min,float max)
    {
        float v;string raw=((string)e.Attribute(name)??string.Empty).Trim();
        if(!float.TryParse(raw,NumberStyles.Float,CultureInfo.InvariantCulture,out v)||float.IsNaN(v)||float.IsInfinity(v)||v<min||v>max)
            throw new InvalidDataException("Invalid Phase 8 '"+name+"' on <"+e.Name+">.");
        return v;
    }

    private static int CountPlayerItem(EntityPlayer player,int type)
    {
        int total=0;ItemValue value=new ItemValue(type);
        try{if(player!=null&&player.inventory!=null)total+=player.inventory.GetItemCount(value,false,-1,-1);}catch{}
        try{if(player!=null&&player.bag!=null)total+=player.bag.GetItemCount(value,-1,-1,false);}catch{}
        return total;
    }

    private static string Pos(Vector3i p){return p.x+","+p.y+","+p.z;}
}

[HarmonyPatch(typeof(GameUtils),"collectHarvestedItem")]
internal static class RebirthPhase8HarvestCollectedOutputPatch
{
    private static void Prefix(ItemActionData _actionData,ItemValue _iv,out RebirthPhase8WorldOutputTrainingService.CollectedState __state)
    { RebirthPhase8WorldOutputTrainingService.CollectedPrefix(_actionData,_iv,out __state); }
    [HarmonyPriority(Priority.First)]
    private static void Postfix(RebirthPhase8WorldOutputTrainingService.CollectedState __state)
    { RebirthPhase8WorldOutputTrainingService.CollectedPostfix(__state); }
}
