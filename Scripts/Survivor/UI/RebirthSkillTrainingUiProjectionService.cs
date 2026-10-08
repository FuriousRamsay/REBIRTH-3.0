using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// Read-only Phase-11 bridge from the selected Skill UI to the authoritative training projection.
/// Numeric values are produced only by RebirthSkillAwardService.TryPreviewTrainingEvidence, the
/// same final Skill projection path used by accepted live awards. Remote clients request the
/// projection from the server; they never calculate or submit an XP amount.
/// </summary>
public static class RebirthSkillTrainingUiProjectionService
{
    private sealed class Cached
    {
        public string Text = string.Empty;
        public float RequestedAt = -999f;
    }

    private static readonly object Gate = new object();
    private static readonly Dictionary<string,Cached> Cache = new Dictionary<string,Cached>(StringComparer.OrdinalIgnoreCase);
    private const float RequestIntervalSeconds = 0.75f;
    private static long revision;
    private static int worldScope;

    public static long Revision { get { return Interlocked.Read(ref revision); } }

    public static string GetOrRequest(EntityPlayer player,string skillId)
    {
        if(string.IsNullOrEmpty(skillId))return string.Empty;
        RebirthSkillTrainingProfile profile;
        if(!RebirthSkillTrainingProfileRegistry.TryGet(skillId,out profile)||profile==null)return L("xuiRebirthTrainingProjectionUnavailable","Training contribution unavailable.");
        if(player==null||player.world==null)return ReferenceOnly(profile,ContextHint(skillId));
        EnsureScope(player.world);

        ConnectionManager connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        bool remote=player.world.IsRemote() && connection!=null && !connection.IsServer;
        if(!remote)return BuildAuthoritative(player,skillId);

        string cached=string.Empty;
        bool send=false;
        float now=Time.realtimeSinceStartup;
        lock(Gate)
        {
            Cached c;
            if(!Cache.TryGetValue(skillId,out c)||c==null){c=new Cached();Cache[skillId]=c;}
            cached=c.Text??string.Empty;
            if(now-c.RequestedAt>=RequestIntervalSeconds){c.RequestedAt=now;send=true;}
        }
        if(send)
        {
            try{connection.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthSkillTrainingUiProjectionRequest>().Setup(player.entityId,skillId));}
            catch(Exception ex){if(RebirthSurvivorDebug.Enabled)Log.Warning("[REBIRTH Training UI] request failed skill="+skillId+" ex="+ex.GetType().Name);}
        }
        return cached.Length>0?cached:ReferenceOnly(profile,L("xuiRebirthTrainingProjectionServerPending","Checking authoritative expected gain..."));
    }

    internal static void Receive(string skillId,string text)
    {
        if(string.IsNullOrEmpty(skillId))return;
        string value=text??string.Empty;
        bool changed=false;
        lock(Gate)
        {
            Cached c;
            if(!Cache.TryGetValue(skillId,out c)||c==null){c=new Cached();Cache[skillId]=c;}
            if(!string.Equals(c.Text,value,StringComparison.Ordinal)){c.Text=value;changed=true;}
        }
        if(changed)Interlocked.Increment(ref revision);
    }

    internal static string BuildAuthoritative(EntityPlayer player,string skillId)
    {
        RebirthSkillTrainingProfile profile;
        if(!RebirthSkillTrainingProfileRegistry.TryGet(skillId,out profile)||profile==null)return L("xuiRebirthTrainingProjectionUnavailable","Training contribution unavailable.");
        if(player==null||player.world==null||player.world.IsRemote())return ReferenceOnly(profile,ContextHint(skillId));

        // Current held weapon: exact per-reference-unit projection for the selected weapon Skill,
        // and for Stealth/Rage overlays when the held weapon is a valid underlying method.
        ItemValue held=player.inventory!=null?player.inventory.holdingItemItemValue:null;
        RebirthWeaponSustainedDpsService.Profile weapon;
        if(held!=null&&!held.IsEmpty()&&RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player,held,out weapon)&&weapon!=null&&weapon.Valid&&weapon.SustainedDps>0f)
        {
            bool exactFamily=string.Equals(weapon.SkillId,skillId,StringComparison.OrdinalIgnoreCase);
            bool stealth=string.Equals(skillId,"skill.stealth",StringComparison.OrdinalIgnoreCase);
            bool rage=string.Equals(skillId,"skill.rage",StringComparison.OrdinalIgnoreCase)&&weapon.Melee;
            if(exactFamily||stealth||rage)
            {
                RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence
                {
                    SkillId=skillId,SourceKey="ui:phase11:current-weapon",ReferenceDescription=profile.ReferenceUnit,
                    AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.ContinuousWork,
                    CreditedWork=100f,LiveWorkRate=weapon.SustainedDps
                };
                string label=profile.ReferenceUnit+" with current "+FriendlyItemName(weapon.ItemName);
                string exact=Preview(player,evidence,label);
                if(exact.Length>0)return exact;
            }
        }

        // Current mining/logging tool: exact 100-work projection from its live cadence. The target
        // still has to be eligible at runtime; this is only the selected Skill's reference-unit UI.
        if(held!=null&&!held.IsEmpty()&&(string.Equals(skillId,"skill.mining",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.logging",StringComparison.OrdinalIgnoreCase)))
        {
            string heldSkill=RebirthProgressionRuntimeConfig.ClassifyHarvestTool(held);
            RebirthWeaponSustainedDpsService.Profile workProfile;
            float rate;
            if(string.Equals(heldSkill,skillId,StringComparison.OrdinalIgnoreCase)&&
               RebirthWeaponSustainedDpsService.TryGetDisplayProfile(player,held,out workProfile)&&workProfile!=null&&workProfile.Valid&&
               workProfile.NormalBlockDamagePerAttack>0f&&
               RebirthWeaponSustainedDpsService.TryGetWorldWorkRate(player,held,workProfile.NormalBlockDamagePerAttack,out rate)&&rate>0f)
            {
                RebirthSkillTrainingEvidence evidence=new RebirthSkillTrainingEvidence
                {
                    SkillId=skillId,SourceKey="ui:phase11:current-world-tool",ReferenceDescription=profile.ReferenceUnit,
                    AuthoritativeSuccess=true,Mode=RebirthSkillTrainingEvidenceMode.ContinuousWork,
                    CreditedWork=100f,LiveWorkRate=rate
                };
                string exact=Preview(player,evidence,profile.ReferenceUnit+" with current "+FriendlyItemName(held.ItemClass!=null?held.ItemClass.GetItemName():string.Empty));
                if(exact.Length>0)return exact;
            }
        }

        // Stable reference jobs can be previewed without inventing a target. Values come from the
        // route owners / locked runtime tuning, then pass through the same final preview calculator.
        RebirthSkillTrainingEvidence fixedEvidence;
        string fixedLabel;
        if(TryBuildStableReference(skillId,profile,out fixedEvidence,out fixedLabel))
        {
            string exact=Preview(player,fixedEvidence,fixedLabel);
            if(exact.Length>0)return exact;
        }

        if(string.Equals(skillId,"skill.black_magic",StringComparison.OrdinalIgnoreCase))
        {
            string dominate=Preview(player,Discrete(skillId,RebirthBlackMagicService.DominationReferenceGain,"ui:phase11:black-magic-domination"),L("xuiRebirthTrainingBlackMagicDomination","successful domination"));
            string bind=Preview(player,Discrete(skillId,RebirthBlackMagicService.BindingReferenceGain,"ui:phase11:black-magic-binding"),L("xuiRebirthTrainingBlackMagicBinding","successful permanent binding"));
            if(dominate.Length>0&&bind.Length>0)return dominate+"\n"+bind+"\n"+L("xuiRebirthTrainingBlackMagicCombatContext","Attributable controlled-undead combat uses the live normalized damage profile.");
        }

        return ReferenceOnly(profile,ContextHint(skillId));
    }

    private static bool TryBuildStableReference(string skillId,RebirthSkillTrainingProfile profile,out RebirthSkillTrainingEvidence evidence,out string label)
    {
        evidence=null;label=profile!=null?profile.ReferenceUnit:string.Empty;
        float raw=0f;
        if(string.Equals(skillId,"skill.explosives",StringComparison.OrdinalIgnoreCase)){raw=RebirthProgressionRuntimeConfig.ExplosivesBlockAward;label=L("xuiRebirthTrainingExplosivesBlock","qualified explosive block-work event");}
        else if(string.Equals(skillId,"skill.drone_operations",StringComparison.OrdinalIgnoreCase)){raw=RebirthProgressionRuntimeConfig.DroneStockRecoveryAward;label=L("xuiRebirthTrainingDroneRecovery","meaningful stock-drone recovery");}
        else if(string.Equals(skillId,"skill.salvage",StringComparison.OrdinalIgnoreCase)){raw=RebirthPhase8WorldOutputTrainingService.SalvageReferenceGain;label=profile.ReferenceUnit;}
        else if(string.Equals(skillId,"skill.farming",StringComparison.OrdinalIgnoreCase)){raw=RebirthPhase8WorldOutputTrainingService.FarmingMatureReferenceGain;label=profile.ReferenceUnit;}
        else if(string.Equals(skillId,"skill.animal_processing",StringComparison.OrdinalIgnoreCase)){raw=RebirthPhase8WorldOutputTrainingService.AnimalProcessingReferenceGain;label=profile.ReferenceUnit;}
        else if(string.Equals(skillId,"skill.animal_handling",StringComparison.OrdinalIgnoreCase)){raw=RebirthAnimalHandlingService.SuccessfulExerciseReferenceGain;label=profile.ReferenceUnit;}
        else if(string.Equals(skillId,"skill.maintenance",StringComparison.OrdinalIgnoreCase)){raw=RebirthPhase9TechnicalTrainingService.MaintenanceFullReferenceGain;label=profile.ReferenceUnit;}
        else if(string.Equals(skillId,"skill.construction",StringComparison.OrdinalIgnoreCase)){raw=RebirthProgressionRuntimeConfig.ConstructionUpgradeAward;label=L("xuiRebirthTrainingConstructionUpgrade","committed structural upgrade");}
        else if(string.Equals(skillId,"skill.electrical",StringComparison.OrdinalIgnoreCase)){raw=RebirthPhase9TechnicalTrainingService.ElectricalDefaultReferenceGain;label=L("xuiRebirthTrainingElectricalDefault","default-complexity committed electrical service");}
        else if(string.Equals(skillId,"skill.gunsmithing",StringComparison.OrdinalIgnoreCase)){raw=RebirthPhase9TechnicalTrainingService.GunsmithingFullReferenceGain;label=profile.ReferenceUnit;}
        else if(string.Equals(skillId,"skill.lockpicking",StringComparison.OrdinalIgnoreCase)){raw=RebirthPhase9TechnicalTrainingService.LockpickStandardReferenceGain;label=L("xuiRebirthTrainingLockpickStandard","successful standard lock open");}
        else if(string.Equals(skillId,"skill.athletics",StringComparison.OrdinalIgnoreCase)){raw=RebirthProgressionRuntimeConfig.AthleticsAward;label=profile.ReferenceUnit;}
        else if(string.Equals(skillId,"skill.armor_proficiency",StringComparison.OrdinalIgnoreCase)){raw=RebirthProgressionRuntimeConfig.ArmorAward;label=profile.ReferenceUnit;}
        else if(string.Equals(skillId,"skill.tailoring",StringComparison.OrdinalIgnoreCase)){raw=RebirthPhase9TechnicalTrainingService.TailoringFullReferenceGain;label=L("xuiRebirthTrainingTailoringRepair","full Tailoring-owned garment repair reference");}
        if(!(raw>0f)||float.IsNaN(raw)||float.IsInfinity(raw))return false;
        evidence=Discrete(skillId,raw,"ui:phase11:stable-reference");
        evidence.ReferenceDescription=label;
        return true;
    }

    private static RebirthSkillTrainingEvidence Discrete(string skillId,float raw,string source)
    {
        return new RebirthSkillTrainingEvidence
        {
            SkillId=skillId,SourceKey=source??string.Empty,AuthoritativeSuccess=true,
            Mode=RebirthSkillTrainingEvidenceMode.DiscreteAward,CreditedWork=1f,DiscreteRawAward=raw
        };
    }

    private static string Preview(EntityPlayer player,RebirthSkillTrainingEvidence evidence,string label)
    {
        RebirthSkillTrainingComputation c;
        if(!RebirthSkillAwardService.TryPreviewTrainingEvidence(player,evidence,out c)||c==null)return string.Empty;
        string gain=c.ExpectedFinalGain>0f?"+"+c.ExpectedFinalGain.ToString("0.###",CultureInfo.InvariantCulture):"0";
        return L("xuiRebirthTrainingExpected","Expected")+" "+gain+" / "+(label??c.ReferenceUnit??string.Empty);
    }

    private static string ReferenceOnly(RebirthSkillTrainingProfile profile,string hint)
    {
        string reference=profile!=null?(profile.ReferenceUnit??string.Empty):string.Empty;
        string text=L("xuiRebirthTrainingReference","Reference")+": "+reference;
        if(!string.IsNullOrEmpty(hint))text+="\n"+hint;
        return text;
    }

    private static string ContextHint(string skillId)
    {
        if(string.Equals(skillId,"skill.cooking",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.drink_preparation",StringComparison.OrdinalIgnoreCase)||
           string.Equals(skillId,"skill.metalworking",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.chemistry",StringComparison.OrdinalIgnoreCase))
            return L("xuiRebirthTrainingRecipeContext","Exact expected gain depends on the selected recipe, inputs, process difficulty and current assistance; the Expected Outcome calculation is authoritative.");
        if(string.Equals(skillId,"skill.medicine",StringComparison.OrdinalIgnoreCase))
            return L("xuiRebirthTrainingMedicineContext","Exact expected gain depends on the treatment and actual healing or condition outcome.");
        if(string.Equals(skillId,"skill.mechanics",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.electrical",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.lockpicking",StringComparison.OrdinalIgnoreCase))
            return L("xuiRebirthTrainingJobContext","Exact expected gain depends on the current successful job, difficulty and assistance.");
        if(string.Equals(skillId,"skill.tracking",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.bartering",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.trading",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.teaching",StringComparison.OrdinalIgnoreCase))
            return L("xuiRebirthTrainingOutcomeContext","Exact expected gain depends on the current verified target or committed outcome.");
        if(string.Equals(skillId,"skill.deployable_turrets",StringComparison.OrdinalIgnoreCase))
            return L("xuiRebirthTrainingDeviceContext","Exact gain uses owner-attributed actual damage and the live device effectiveness profile.");
        if(string.Equals(skillId,"skill.mining",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.logging",StringComparison.OrdinalIgnoreCase)||
           string.Equals(skillId,"skill.stealth",StringComparison.OrdinalIgnoreCase)||string.Equals(skillId,"skill.rage",StringComparison.OrdinalIgnoreCase))
            return L("xuiRebirthTrainingEquipmentContext","Equip the relevant live weapon or tool to show an exact per-reference-unit gain.");
        return L("xuiRebirthTrainingContextRequired","Exact expected gain is calculated from the live weapon, tool, recipe, target or committed job when that context is available.");
    }

    private static string FriendlyItemName(string itemName)
    {
        string id=itemName??string.Empty;
        if(id.Length==0)return L("xuiRebirthTrainingCurrentEquipment","equipment");
        try
        {
            ItemClass item=ItemClass.GetItemClass(id);
            if(item!=null){string n=item.GetLocalizedItemName();if(!string.IsNullOrEmpty(n))return n;}
        }
        catch{}
        return id;
    }

    private static void EnsureScope(WorldBase world)
    {
        int next=world!=null?RuntimeHelpers.GetHashCode(world):0;
        lock(Gate)
        {
            if(next==worldScope)return;
            worldScope=next;Cache.Clear();Interlocked.Increment(ref revision);
        }
    }

    private static string L(string key,string fallback)
    {
        string value=Localization.Get(key);
        return string.IsNullOrEmpty(value)||string.Equals(value,key,StringComparison.OrdinalIgnoreCase)?fallback:value;
    }
}

[Preserve]
public sealed class NetPackageRebirthSkillTrainingUiProjectionRequest : NetPackage
{
    private int playerId;
    private string skillId=string.Empty;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }
    public NetPackageRebirthSkillTrainingUiProjectionRequest Setup(int entityId,string id){playerId=entityId;skillId=id??string.Empty;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;playerId=b.ReadInt32();skillId=RebirthSurvivorNetworkCodec.ReadString(b,128);}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;b.Write(playerId);RebirthSurvivorNetworkCodec.WriteString(b,skillId??string.Empty,128);}
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||!ValidEntityIdForSender(playerId)||string.IsNullOrEmpty(skillId))return;
        EntityPlayer player=world.GetEntity(playerId) as EntityPlayer;if(player==null)return;
        string text=RebirthSkillTrainingUiProjectionService.BuildAuthoritative(player,skillId);
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c!=null&&c.IsServer)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthSkillTrainingUiProjectionResponse>().Setup(skillId,text),_attachedToEntityId:playerId);
    }
    public int GetLength(){return 12+RebirthSurvivorNetworkCodec.EstimateString(skillId,128);}
}

[Preserve]
public sealed class NetPackageRebirthSkillTrainingUiProjectionResponse : NetPackage
{
    private string skillId=string.Empty;
    private string text=string.Empty;
    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }
    public NetPackageRebirthSkillTrainingUiProjectionResponse Setup(string id,string value){skillId=id??string.Empty;text=value??string.Empty;return this;}
    public override void read(PooledBinaryReader reader){BinaryReader b=(BinaryReader)reader;skillId=RebirthSurvivorNetworkCodec.ReadString(b,128);text=RebirthSurvivorNetworkCodec.ReadString(b,1024);}
    public override void write(PooledBinaryWriter writer){base.write(writer);BinaryWriter b=(BinaryWriter)writer;RebirthSurvivorNetworkCodec.WriteString(b,skillId??string.Empty,128);RebirthSurvivorNetworkCodec.WriteString(b,text??string.Empty,1024);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthSkillTrainingUiProjectionService.Receive(skillId,text);}
    public int GetLength(){return 12+RebirthSurvivorNetworkCodec.EstimateString(skillId,128)+RebirthSurvivorNetworkCodec.EstimateString(text,1024);}
}
