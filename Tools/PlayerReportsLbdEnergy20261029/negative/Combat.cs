using System;using System.Collections.Generic;using System.Runtime.CompilerServices;using System.Xml.Linq;using UnityEngine;
internal static class RebirthSurvivorCombatSkillPatch
{
    internal struct DamageState
    {
        public bool WasDead;
        public DamageSource Source;
        public int RawStrength;
        public int HealthBefore;
        public bool PreHitStealthQualified;
        public RebirthTheorySoloCombatService.Witness SoloOriginal;
    }

    private static void Prefix(EntityAlive __instance, DamageSource _damageSource, int _strength, out DamageState __state)
    {
        __state = new DamageState { WasDead = __instance != null && __instance.IsDead(), Source = _damageSource, RawStrength = _strength, HealthBefore = __instance != null ? __instance.Health : 0, PreHitStealthQualified = RebirthStealthTrainingService.IsPreHitStealthQualified(__instance, _damageSource), SoloOriginal=CaptureSoloOriginal(__instance,_damageSource) };
    }

    private static RebirthTheorySoloCombatService.Witness CaptureSoloOriginal(EntityAlive target,DamageSource source)
    {try{return RebirthTheorySoloCombatService.Before(target,source);}catch{return null;}}

    private static System.Exception Finalizer(System.Exception __exception,DamageState __state)
    {try{RebirthTheorySoloCombatService.Unknown(__state.SoloOriginal,__exception);}catch{}return __exception;}

    private static void Postfix(EntityAlive __instance,DamageResponse __result,DamageState __state,bool __runOriginal)
    {
        try{RebirthTheorySoloCombatService.After(__state.SoloOriginal,__instance,__result,__runOriginal);}catch(System.Exception observer){try{RebirthTheorySoloCombatService.Unknown(__state.SoloOriginal,observer);}catch{}}
        // A target that was already dead before the hit is carcass-processing evidence, not combat.
        // Phase 8 observes the actual resources recovered by HarvestOnAttack instead of corpse-hit damage.
        if (__state.WasDead) return;
        float actualHealthLoss = __instance != null ? System.Math.Max(0, __state.HealthBefore - __instance.Health) : 0f;
        RebirthStatisticsService.RecordDamage(__instance,__result,__state.Source,__state.RawStrength,actualHealthLoss,-1f);
        RebirthSkillEventRouter.OnCombatDamageCompleted(__instance,__result,actualHealthLoss,__state.PreHitStealthQualified,__state.HealthBefore);
    }
}
internal static class RebirthSurvivorReceivedCombatSkillPatch {
internal struct DamageState{} private static void Prefix(EntityAlive __instance,DamageResponse _dmResponse,out DamageState __state){__state=default;}
private static void Postfix(EntityAlive __instance,DamageResponse _dmResponse,DamageState __state,bool __runOriginal){} }
public static class RebirthWeaponSustainedDpsService { private static readonly object Gate=new object();
    private sealed class CombatAward {internal string Skill,Receipt;internal float Amount;}
    private sealed class CombatBatch
    {
        internal World World;internal EntityPlayer Player;internal string Owner,Creation;internal RebirthWorldCharacterRecord Record;
        internal readonly Dictionary<string,float> New=new Dictionary<string,float>(StringComparer.OrdinalIgnoreCase);
        internal readonly List<CombatAward> InFlight=new List<CombatAward>();
    }
    private static readonly Dictionary<string,CombatBatch> PendingCombat=new Dictionary<string,CombatBatch>(StringComparer.Ordinal);
    private sealed class AwardCounts{internal long Settled;}
    private static readonly ConditionalWeakTable<EntityPlayer,AwardCounts> SettledCounts=new ConditionalWeakTable<EntityPlayer,AwardCounts>();
    internal static bool IsPendingCombatReceipt(string receipt)
    {lock(Gate){foreach(var batch in PendingCombat.Values)foreach(var award in batch.InFlight)if(award.Receipt==receipt)return true;return false;}}
    private static ISet<string> ProtectCombatReceipts(HashSet<string> receipts)
    {lock(Gate){var result=new HashSet<string>(StringComparer.Ordinal);foreach(var batch in PendingCombat.Values)foreach(var award in batch.InFlight)result.Add(award.Receipt);return result;}}
    private static string CombatScopeKey(EntityPlayer player,RebirthStablePlayerIdentity owner,RebirthWorldCharacterRecord record)
        =>owner.StorageKey+"|"+record.Origin?.CreationId+"|"+RuntimeHelpers.GetHashCode(player.world)+"|"+RuntimeHelpers.GetHashCode(record);

public static void QueueCombatProgress(EntityPlayer player, string skillId, float amount)
    {
        if (player == null || string.IsNullOrEmpty(skillId) || !Finite(amount) || amount <= 0f) return;
        if(!RebirthSkillAwardService.TryGetEligible(player,out var owner,out var record))return;
        string key=CombatScopeKey(player,owner,record);
        lock (Gate)
        {
            if(!PendingCombat.TryGetValue(key,out var batch))PendingCombat[key]=batch=new CombatBatch{World=player.world,Player=player,Owner=owner.StorageKey,Creation=record.Origin?.CreationId,Record=record};
            if(!ReferenceEquals(batch.World,player.world)||!ReferenceEquals(batch.Record,record))return;
            float current;batch.New.TryGetValue(skillId,out current);
            if(Finite(current+amount))batch.New[skillId]=current+amount;
        }
    }
public static void FlushQueuedCombatProgress(EntityPlayer player)
    {
        if (player == null) return;
        if(!RebirthSkillAwardService.TryGetEligible(player,out var owner,out var record))return;
        string key=CombatScopeKey(player,owner,record);
        CombatBatch batch;CombatAward[] work;
        lock (Gate)
        {
            if(!PendingCombat.TryGetValue(key,out batch)||!ReferenceEquals(batch.World,player.world)||!ReferenceEquals(batch.Record,record))return;
            batch.Player=player;
            if(batch.InFlight.Count<64)
            {
                foreach(var pair in batch.New)batch.InFlight.Add(new CombatAward{Skill=pair.Key,Amount=pair.Value,Receipt="combat-batch:"+Guid.NewGuid().ToString("N")});
                batch.New.Clear();
            }
            work=batch.InFlight.ToArray();
        }
        foreach (var award in work)
        {
            float skill, attribute;
            bool saved=RebirthSkillAwardService.TryAwardCombatDurable(player,award.Skill,award.Amount,"combat-batch:"+award.Skill,0f,award.Receipt,out skill,out attribute);
            if(saved)lock(Gate)if(batch.InFlight.Remove(award))SettledCounts.GetOrCreateValue(player).Settled++;
            if (RebirthSkillEvalDiagnostics.On)
                Log.Out("[REBIRTH SkillEval] combat award skill=" + award.Skill + " raw=" + award.Amount + " applied=" + skill + " attribute=" + attribute+" settled="+saved);
        }
        lock(Gate)if(batch.New.Count==0&&batch.InFlight.Count==0)PendingCombat.Remove(key);
    }
public static float CombatSkillMultiplier(float skillValue)
    {
        if (skillValue < 25f) return RebirthProgressionRuntimeConfig.CombatSkillMultiplier0To24;
        if (skillValue < 50f) return RebirthProgressionRuntimeConfig.CombatSkillMultiplier25To49;
        if (skillValue < 75f) return RebirthProgressionRuntimeConfig.CombatSkillMultiplier50To74;
        if (skillValue < 90f) return RebirthProgressionRuntimeConfig.CombatSkillMultiplier75To89;
        return RebirthProgressionRuntimeConfig.CombatSkillMultiplier90To100;
    }
public static float CalculateCombatProgress(float effectiveDamage, float sustainedDps, float currentSkill)
    {
        if (!Finite(effectiveDamage) || !Finite(sustainedDps) || effectiveDamage <= 0f || sustainedDps <= 0f) return 0f;
        // Training is damage-proportional and normalized by live sustained work rate. The authored
        // equivalent-seconds ceiling bounds one event without reintroducing nominal/overkill damage.
        float seconds = effectiveDamage / sustainedDps;
        float maxEquivalent = Mathf.Max(0f, RebirthProgressionRuntimeConfig.CombatMaxEquivalentSecondsPerHit);
        if (maxEquivalent > 0f) seconds = Mathf.Min(seconds, maxEquivalent);
        float progress = seconds * Mathf.Max(0f, RebirthProgressionRuntimeConfig.CombatProgressPerSustainedSecond) * CombatSkillMultiplier(currentSkill);
        return Finite(progress) ? Mathf.Max(0f, progress) : 0f;
    }
private static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
public sealed class Profile{public bool Valid=true;public float SustainedDps=24;}
public static bool ProfileAvailable=true; public static Profile ActiveProfile=new Profile();
public static bool TryGetCombatProfile(EntityPlayer p,ItemValue i,out Profile profile){profile=ActiveProfile;return ProfileAvailable;}
}
public static partial class RebirthProgressionRuntimeConfig { private static readonly Dictionary<int,string> CombatClassCache=new(); public static List<RebirthSkillSourceMap> CombatMaps=new();
public static string ClassifyCombat(ItemValue item)
    {
        if (item == null || item.ItemClass == null) return string.Empty;
        int type = item.type;
        string cached;
        lock (CombatClassCache)
        {
            if (CombatClassCache.TryGetValue(type, out cached)) return cached;
        }
        string result = ClassifyCombatUncached(item);
        lock (CombatClassCache) CombatClassCache[type] = result;
        return result;
    }
private static string ClassifyCombatUncached(ItemValue item)
    {
        string name = item.ItemClass.GetItemName() ?? string.Empty;
        string tags = item.ItemClass.ItemTags.ToString() ?? string.Empty;
        // Exact item-name authoring wins before fuzzy/token fallback. This prevents overlapping
        // families such as pistol/revolver/heavy-handgun or assault/tactical/long-range rifles
        // from being collapsed by a broad substring.
        for (int i=0;i<CombatMaps.Count;i++)
            if (MatchesExact(name, CombatMaps[i].ItemNames)) return CombatMaps[i].SkillId;
        for (int i=0;i<CombatMaps.Count;i++)
            if (MatchesAny(name, CombatMaps[i].ItemTokens) || MatchesAny(tags, CombatMaps[i].TagTokens))
                return CombatMaps[i].SkillId;
        return string.Empty;
    }
private static bool MatchesExact(string value,string[] tokens) { if(string.IsNullOrEmpty(value)||tokens==null)return false; for(int i=0;i<tokens.Length;i++) if(string.Equals(value,tokens[i],StringComparison.OrdinalIgnoreCase))return true; return false; }
private static bool MatchesAny(string value,string[] tokens) { if(string.IsNullOrEmpty(value)||tokens==null)return false; string x=value.ToLowerInvariant(); for(int i=0;i<tokens.Length;i++) if(tokens[i]!="*" && x.IndexOf(tokens[i],StringComparison.Ordinal)>=0)return true; return false; }
private static RebirthSkillSourceMap ParseMap(XElement e)
    {
        return new RebirthSkillSourceMap { SkillId=((string)e.Attribute("skill")??string.Empty).Trim(), ItemNames=ExactTokens((string)e.Attribute("item_names")), ItemTokens=Tokens((string)e.Attribute("item_tokens")), TagTokens=Tokens((string)e.Attribute("tag_tokens")), ToolTokens=Tokens((string)e.Attribute("tool_tokens")), BlockTokens=Tokens((string)e.Attribute("block_tokens")) };
    }
private static string[] ExactTokens(string raw) { if(string.IsNullOrWhiteSpace(raw))return new string[0]; string[] a=raw.Split(','); List<string> r=new List<string>(); for(int i=0;i<a.Length;i++){string v=(a[i]??string.Empty).Trim();if(v.Length>0)r.Add(v);}return r.ToArray(); }
private static string[] Tokens(string raw) { if(string.IsNullOrWhiteSpace(raw))return new string[0]; string[] a=raw.Split(','); List<string> r=new List<string>(); for(int i=0;i<a.Length;i++){string v=(a[i]??string.Empty).Trim().ToLowerInvariant();if(v.Length>0)r.Add(v);}return r.ToArray(); }
public static void LoadMap(XElement e)=>CombatMaps.Add(ParseMap(e));
}
public static class RebirthWeaponFamilySkillService{    private static readonly HashSet<string> MeleeSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "skill.spears","skill.clubs","skill.swords","skill.axes","skill.batons","skill.hammers","skill.knives","skill.scythes","skill.knuckles","skill.unarmed"
    };

    private static readonly HashSet<string> RangedSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "skill.archery","skill.pistols","skill.revolvers","skill.heavy_handguns","skill.shotguns","skill.assault_rifles","skill.tactical_rifles","skill.long_range_rifles"
    };


public static bool IsChunk7Skill(string skillId)
    { return IsMeleeSkill(skillId) || IsRangedSkill(skillId); }
public static bool IsMeleeSkill(string skillId)
    { return !string.IsNullOrEmpty(skillId) && MeleeSkills.Contains(skillId); }
public static bool IsRangedSkill(string skillId)
    { return !string.IsNullOrEmpty(skillId) && RangedSkills.Contains(skillId); }}