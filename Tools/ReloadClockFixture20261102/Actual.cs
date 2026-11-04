using System;using System.Collections.Generic;using System.Globalization;using System.Runtime.CompilerServices;
public static class Baseline{private sealed class RuntimeReloadStart
    {
        public string Key = string.Empty;
        public string ItemName = string.Empty;
        public float StartedAt;
        public float EffectiveReloadMultiplier = 1f;
    }public static void OnRuntimeReloadStarted(EntityPlayer player)
    {
        if (player == null || player.inventory == null) return;
        ItemValue held = player.inventory.holdingItemItemValue;
        if (held == null || held.IsEmpty() || held.ItemClass == null) return;
        string skillId = RebirthProgressionRuntimeConfig.ClassifyCombat(held);
        if (!RebirthWeaponFamilySkillService.IsRangedSkill(skillId)) return;
        string key = BuildReloadMeasurementKey(player, held, skillId);
        float effectiveMultiplier = ResolveEffectiveReloadMultiplier(player, held, skillId, true);
        lock (Gate)
        {
            ActiveReloads[player.entityId] = new RuntimeReloadStart
            {
                Key = key,
                ItemName = held.ItemClass.GetItemName() ?? string.Empty,
                StartedAt = Time.realtimeSinceStartup,
                EffectiveReloadMultiplier = effectiveMultiplier
            };
        }
        if (ReloadTimingDebug)
        {
            Log.Out("[REBIRTH RELOAD TIMING] START item='" + (held.ItemClass.GetItemName() ?? string.Empty) +
                "' entity=" + player.entityId.ToString(CultureInfo.InvariantCulture) +
                " authored=" + ReadReloadSeconds(held.ItemClass, held.ItemClass.Actions != null && held.ItemClass.Actions.Length > 0 ? held.ItemClass.Actions[0] : null).ToString("0.###", CultureInfo.InvariantCulture) +
                " effectiveMultiplier=" + effectiveMultiplier.ToString("0.###", CultureInfo.InvariantCulture));
            LogReloadAnimationClips(player);
        }
    }public static void OnRuntimeReloadCompleted(EntityPlayer player)
    {
        if (player == null) return;
        RuntimeReloadStart start = null;
        lock (Gate)
        {
            ActiveReloads.TryGetValue(player.entityId, out start);
            ActiveReloads.Remove(player.entityId);
        }
        if (start == null) return;
        float elapsed = Time.realtimeSinceStartup - start.StartedAt;
        if (!Finite(elapsed) || elapsed < 0.05f || elapsed > 30f) return;
        float multiplier = Mathf.Max(0.05f, start.EffectiveReloadMultiplier);
        // Runtime elapsed is neutralDuration / ReloadSpeedMultiplier, so recover the neutral
        // cycle by multiplying. The neutral value can then be reused with future live modifiers.
        float normalizedBase = elapsed * multiplier;
        lock (Gate)
        {
            MeasuredBaseReloadSeconds[start.Key] = normalizedBase;
            Cache.Clear();
            RecentHeld.Clear();
        }
        if (ReloadTimingDebug)
            Log.Out("[REBIRTH RELOAD TIMING] COMPLETE item='" + start.ItemName +
                "' entity=" + player.entityId.ToString(CultureInfo.InvariantCulture) +
                " measuredEffective=" + elapsed.ToString("0.###", CultureInfo.InvariantCulture) + "s" +
                " effectiveMultiplier=" + multiplier.ToString("0.###", CultureInfo.InvariantCulture) +
                " normalizedBase=" + normalizedBase.ToString("0.###", CultureInfo.InvariantCulture) + "s");
    }private static string BuildReloadMeasurementKey(EntityPlayer player, ItemValue item, string skillId)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (item != null ? item.type : 0);
            h = h * 31 + (item != null ? item.Quality : 0);
            h = h * 31 + (item != null ? item.Seed : 0);
            h = h * 31 + (item != null ? item.SelectedAmmoTypeIndex : 0);
            ItemValue[] mods = item != null ? item.modifications : null;
            h = h * 31 + (mods != null ? mods.Length : 0);
            for (int i = 0; mods != null && i < mods.Length; i++)
            {
                ItemValue mod = mods[i];
                h = h * 31 + (mod != null ? mod.type : 0);
                h = h * 31 + (mod != null ? mod.Quality : 0);
                h = h * 31 + (mod != null ? mod.Seed : 0);
            }
            // Do not key the normalized base cycle by player reload buffs or skill. Those are
            // applied live after measurement so Bandolier and other reload modifiers update immediately.
            return (player != null ? player.entityId.ToString(CultureInfo.InvariantCulture) : "none") + "|" + h.ToString(CultureInfo.InvariantCulture);
        }
    }
static object Gate=new object();static Dictionary<int,RuntimeReloadStart> ActiveReloads=new();static Dictionary<string,float> MeasuredBaseReloadSeconds=new();static Dictionary<int,int> Cache=new(),RecentHeld=new();static bool ReloadTimingDebug=false;
static float ResolveEffectiveReloadMultiplier(EntityPlayer p,ItemValue v,string s,bool held)=>p.Multiplier;
static float ReadReloadSeconds(ItemClass c,object a)=>5;static void LogReloadAnimationClips(EntityPlayer p){}static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
public static int Learned=>MeasuredBaseReloadSeconds.Count;public static int Pending=>ActiveReloads.Count;public static float LearnedValue{get{foreach(var v in MeasuredBaseReloadSeconds.Values)return v;return -1;}}
public static void Reset(){ActiveReloads.Clear();MeasuredBaseReloadSeconds.Clear();}
}
public static class BaselineStartHook{public static void Prefix(ItemActionRanged __instance)
    {
        if (__instance == null || GameManager.Instance == null || GameManager.Instance.World == null) return;
        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
        if (player == null || player.inventory == null) return;
        ItemValue held = player.inventory.holdingItemItemValue;
        if (held == null || held.IsEmpty() || held.ItemClass == null || held.ItemClass.Actions == null || held.ItemClass.Actions.Length == 0) return;
        if (!(__instance is ItemActionRanged)) return;
        Baseline.OnRuntimeReloadStarted(player);
    }}
public static class BaselineCompleteHook{public static void Postfix(ItemActionRanged __instance)
    {
        if (__instance == null || GameManager.Instance == null || GameManager.Instance.World == null) return;
        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
        if (player == null || player.inventory == null) return;
        ItemValue held = player.inventory.holdingItemItemValue;
        if (held == null || held.IsEmpty() || held.ItemClass == null || held.ItemClass.Actions == null || held.ItemClass.Actions.Length == 0) return;
        if (!(__instance is ItemActionRanged)) return;
        Baseline.OnRuntimeReloadCompleted(player);
    }}
public static class Proposed{private sealed class RuntimeReloadStart
    {
        public EntityPlayerLocal Owner;
        public ItemActionRanged Action;
        public ItemActionRanged.ItemActionDataRanged Data;
        public ItemValue Item;
        public string Key = string.Empty;
        public string ItemName = string.Empty;
        public float StartedAt;
        public double StartedGameTime;
        public float EffectiveReloadMultiplier = 1f;
    }public static void OnRuntimeReloadStarted(ItemActionRanged action, ItemActionData actionData)
    {
        if (!TryResolveRuntimeReload(action, actionData, out var player, out var held, out var data)) return;
        string skillId = RebirthProgressionRuntimeConfig.ClassifyCombat(held);
        if (!RebirthWeaponFamilySkillService.IsRangedSkill(skillId)) return;
        string key = BuildReloadMeasurementKey(player, held, skillId);
        float effectiveMultiplier = ResolveEffectiveReloadMultiplier(player, held, skillId, true);
        lock (Gate)
        {
            if (ActiveReloads.TryGetValue(player.entityId, out var existing) &&
                ReferenceEquals(existing.Owner, player) && ReferenceEquals(existing.Action, action) &&
                ReferenceEquals(existing.Data, data) && ReferenceEquals(existing.Item, held) &&
                existing.Key == key && Time.realtimeSinceStartup - existing.StartedAt <= 30f) return;
            ActiveReloads[player.entityId] = new RuntimeReloadStart
            {
                Owner = player, Action = action, Data = data, Item = held,
                Key = key,
                ItemName = held.ItemClass.GetItemName() ?? string.Empty,
                StartedAt = Time.realtimeSinceStartup,
                StartedGameTime = Time.timeAsDouble,
                EffectiveReloadMultiplier = effectiveMultiplier
            };
        }
        if (ReloadTimingDebug)
        {
            Log.Out("[REBIRTH RELOAD TIMING] START item='" + (held.ItemClass.GetItemName() ?? string.Empty) +
                "' entity=" + player.entityId.ToString(CultureInfo.InvariantCulture) +
                " authored=" + ReadReloadSeconds(held.ItemClass, held.ItemClass.Actions != null && held.ItemClass.Actions.Length > 0 ? held.ItemClass.Actions[0] : null).ToString("0.###", CultureInfo.InvariantCulture) +
                " effectiveMultiplier=" + effectiveMultiplier.ToString("0.###", CultureInfo.InvariantCulture));
            LogReloadAnimationClips(player);
        }
    }public static void OnRuntimeReloadCompleted(ItemActionRanged action, ItemActionData actionData)
    {
        var owner = actionData?.invData?.holdingEntity as EntityPlayerLocal;
        if (owner == null) return;
        RuntimeReloadStart start = null;
        lock (Gate)
        {
            if (!ActiveReloads.TryGetValue(owner.entityId, out start) ||
                !ReferenceEquals(start.Owner, owner) || !ReferenceEquals(start.Action, action) ||
                !ReferenceEquals(start.Data, actionData)) return;
            ActiveReloads.Remove(owner.entityId);
        }
        if (!TryResolveRuntimeReload(action, actionData, out var player, out var held, out var data) ||
            !ReferenceEquals(start.Item, held) || data.isReloadCancelled || data.isWeaponReloadCancelled ||
            start.Key != BuildReloadMeasurementKey(player, held, RebirthProgressionRuntimeConfig.ClassifyCombat(held))) return;
        float elapsed = Time.realtimeSinceStartup - start.StartedAt;
        double gameElapsed = Time.timeAsDouble - start.StartedGameTime;
        float multiplier = Mathf.Max(0.05f, start.EffectiveReloadMultiplier);
        // Runtime elapsed is neutralDuration / ReloadSpeedMultiplier, so recover the neutral
        // cycle by multiplying. The neutral value can then be reused with future live modifiers.
        bool accepted = TryNormalizeReloadSample(elapsed, gameElapsed, multiplier, out float normalizedBase);
        lock (Gate)
        {
            var sample = ReloadTimingObservations.GetOrCreateValue(player);
            sample.Item = start.ItemName;
            sample.WallSeconds = elapsed;
            sample.GameSeconds = gameElapsed;
            sample.NormalizedBase = normalizedBase;
            sample.Accepted = accepted;
            if (accepted) sample.AcceptedCount++; else sample.RejectedCount++;
        }
        if (!accepted)
        {
            if (ReloadTimingDebug)
                Log.Out("[REBIRTH RELOAD TIMING] REJECT item='" + start.ItemName + "' wall=" + elapsed.ToString("0.###", CultureInfo.InvariantCulture) +
                    "s game=" + gameElapsed.ToString("0.###", CultureInfo.InvariantCulture) + "s reason=invalid-or-divergent-clocks");
            return;
        }
        lock (Gate)
        {
            MeasuredBaseReloadSeconds[start.Key] = normalizedBase;
            Cache.Clear();
            RecentHeld.Clear();
        }
        if (ReloadTimingDebug)
            Log.Out("[REBIRTH RELOAD TIMING] COMPLETE item='" + start.ItemName +
                "' entity=" + player.entityId.ToString(CultureInfo.InvariantCulture) +
                " measuredEffective=" + elapsed.ToString("0.###", CultureInfo.InvariantCulture) + "s" +
                " gameElapsed=" + gameElapsed.ToString("0.###", CultureInfo.InvariantCulture) + "s" +
                " effectiveMultiplier=" + multiplier.ToString("0.###", CultureInfo.InvariantCulture) +
                " normalizedBase=" + normalizedBase.ToString("0.###", CultureInfo.InvariantCulture) + "s");
    }private static string BuildReloadMeasurementKey(EntityPlayer player, ItemValue item, string skillId)
    {
        unchecked
        {
            int h = 17;
            h = h * 31 + (item != null ? item.type : 0);
            h = h * 31 + (item != null ? item.Quality : 0);
            h = h * 31 + (item != null ? item.Seed : 0);
            h = h * 31 + (item != null ? item.SelectedAmmoTypeIndex : 0);
            ItemValue[] mods = item != null ? item.modifications : null;
            h = h * 31 + (mods != null ? mods.Length : 0);
            for (int i = 0; mods != null && i < mods.Length; i++)
            {
                ItemValue mod = mods[i];
                h = h * 31 + (mod != null ? mod.type : 0);
                h = h * 31 + (mod != null ? mod.Quality : 0);
                h = h * 31 + (mod != null ? mod.Seed : 0);
            }
            // Do not key the normalized base cycle by player reload buffs or skill. Those are
            // applied live after measurement so Bandolier and other reload modifiers update immediately.
            return (player != null ? player.entityId.ToString(CultureInfo.InvariantCulture) : "none") + "|" + h.ToString(CultureInfo.InvariantCulture);
        }
    }private static bool TryResolveRuntimeReload(ItemActionRanged action, ItemActionData actionData,
        out EntityPlayerLocal player, out ItemValue held, out ItemActionRanged.ItemActionDataRanged data)
    {
        data = actionData as ItemActionRanged.ItemActionDataRanged;
        player = data?.invData?.holdingEntity as EntityPlayerLocal;
        held = data?.invData?.itemValue;
        if (action == null || player == null || player.isEntityRemote || player.inventory == null ||
            held == null || held.IsEmpty() || held.ItemClass?.Actions == null ||
            actionData.indexInEntityOfAction != 0 || held.ItemClass.Actions.Length == 0 ||
            !ReferenceEquals(held.ItemClass.Actions[0], action) ||
            !ReferenceEquals(player.inventory.holdingItemData, data.invData) ||
            !ReferenceEquals(player.inventory.holdingItemItemValue, held) ||
            data.invData.actionData == null || data.invData.actionData.Count == 0 ||
            !ReferenceEquals(data.invData.actionData[0], data)) return false;
        return true;
    }public static void OnRuntimeReloadCancelled(ItemActionRanged action, ItemActionData actionData)
    {
        var player = actionData?.invData?.holdingEntity as EntityPlayerLocal;
        if (player == null) return;
        lock (Gate)
            if (ActiveReloads.TryGetValue(player.entityId, out var start) &&
                ReferenceEquals(start.Owner, player) && ReferenceEquals(start.Action, action) &&
                ReferenceEquals(start.Data, actionData)) ActiveReloads.Remove(player.entityId);
    }private sealed class ReloadTimingObservation
    {
        public string Item = string.Empty;
        public float WallSeconds;
        public double GameSeconds;
        public float NormalizedBase;
        public bool Accepted;
        public int AcceptedCount, RejectedCount;
    }internal static bool TryNormalizeReloadSample(float wallSeconds, double gameSeconds, float multiplier, out float normalized)
    {
        normalized = 0f;
        if (!Finite(wallSeconds) || wallSeconds < 0.05f || wallSeconds > 30f ||
            double.IsNaN(gameSeconds) || double.IsInfinity(gameSeconds) || gameSeconds < 0.05 ||
            !Finite(multiplier) || multiplier <= 0f) return false;
        // Animation/game time is capped during a long frame and stops while paused.
        // Do not teach wall-clock stalls as weapon reload downtime, nor substitute an
        // assumed animation duration. Keep the prior valid sample/fallback instead.
        // Allow ordinary frame-boundary skew (250 ms or 10% of the cycle).
        if (Math.Abs(wallSeconds - gameSeconds) > Math.Max(0.25, wallSeconds * 0.1)) return false;
        normalized = wallSeconds * Mathf.Max(0.05f, multiplier);
        return Finite(normalized) && normalized > 0f;
    }
static object Gate=new object();static Dictionary<int,RuntimeReloadStart> ActiveReloads=new();static Dictionary<string,float> MeasuredBaseReloadSeconds=new();static Dictionary<int,int> Cache=new(),RecentHeld=new();static bool ReloadTimingDebug=false;
static float ResolveEffectiveReloadMultiplier(EntityPlayer p,ItemValue v,string s,bool held)=>p.Multiplier;
static float ReadReloadSeconds(ItemClass c,object a)=>5;static void LogReloadAnimationClips(EntityPlayer p){}static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
public static int Learned=>MeasuredBaseReloadSeconds.Count;public static int Pending=>ActiveReloads.Count;public static float LearnedValue{get{foreach(var v in MeasuredBaseReloadSeconds.Values)return v;return -1;}}
public static void Reset(){ActiveReloads.Clear();MeasuredBaseReloadSeconds.Clear();}
static ConditionalWeakTable<EntityPlayer,ReloadTimingObservation> ReloadTimingObservations=new();}
public static class ProposedStartHook{public static void Prefix(ItemActionRanged __instance, ItemActionData __0)
    {
        Proposed.OnRuntimeReloadStarted(__instance, __0);
    }}
public static class ProposedCompleteHook{public static void Postfix(ItemActionRanged __instance, ItemActionRanged.ItemActionDataRanged __0)
    {
        Proposed.OnRuntimeReloadCompleted(__instance, __0);
    }}
public static class Time{public static float realtimeSinceStartup;public static double timeAsDouble;}
public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);}
public static class Log{public static void Out(string s){}}
public class EntityAlive{public int entityId;public bool isEntityRemote;public Inventory inventory;}
public class EntityPlayer:EntityAlive{public float Multiplier=1;}
public class EntityPlayerLocal:EntityPlayer{}
public class Inventory{public ItemValue holdingItemItemValue;public ItemInventoryData holdingItemData;}
public class ItemInventoryData{public EntityAlive holdingEntity;public ItemValue itemValue;public List<ItemActionData> actionData=new List<ItemActionData>();}
public class ItemActionData{public ItemInventoryData invData;public int indexInEntityOfAction;}
public class ItemActionRanged{public class ItemActionDataRanged:ItemActionData{public bool isReloadCancelled,isWeaponReloadCancelled;}}
public class ItemClass{public ItemActionRanged[] Actions;public string GetItemName()=>"AK";}
public class ItemValue{public int type=1,Quality=3,Seed=531,Meta;public byte SelectedAmmoTypeIndex;public ItemValue[] modifications;public ItemClass ItemClass;public bool IsEmpty()=>type==0;}
public static class RebirthProgressionRuntimeConfig{public static string ClassifyCombat(ItemValue v)=>"ranged";}
public static class RebirthWeaponFamilySkillService{public static bool IsRangedSkill(string s)=>s=="ranged";}
public class World{public EntityPlayerLocal Primary;public EntityPlayerLocal GetPrimaryPlayer()=>Primary;}
public class GameManager{public static GameManager Instance=new GameManager{World=new World()};public World World;}
public static class Checks{
 static int checks;static void A(bool b,string why){checks++;if(!b)throw new Exception(why);}
 static ItemActionRanged.ItemActionDataRanged Setup(EntityAlive owner,int id=1){owner.entityId=id;var action=new ItemActionRanged();var value=new ItemValue{ItemClass=new ItemClass{Actions=new[]{action}}};var inv=new ItemInventoryData{holdingEntity=owner,itemValue=value};var data=new ItemActionRanged.ItemActionDataRanged{invData=inv};inv.actionData.Add(data);owner.inventory=new Inventory{holdingItemItemValue=value,holdingItemData=inv};return data;}
 static ItemActionRanged Act(ItemActionData d)=>d.invData.itemValue.ItemClass.Actions[0];
 static void Reset(){Baseline.Reset();Proposed.Reset();Time.realtimeSinceStartup=10;Time.timeAsDouble=Time.realtimeSinceStartup;}
 static void Start(ItemActionData d)=>ProposedStartHook.Prefix(Act(d),d);
 static void End(ItemActionRanged.ItemActionDataRanged d){Time.realtimeSinceStartup=13;Time.timeAsDouble=Time.realtimeSinceStartup;ProposedCompleteHook.Postfix(Act(d),d);}
 public static void Run(){
 foreach(float multiplier in new[]{.5f,1,2}){
  Reset();var p=new EntityPlayerLocal{Multiplier=multiplier};var d=Setup(p);GameManager.Instance.World.Primary=p;
  BaselineStartHook.Prefix(Act(d));Start(d);d.invData.itemValue.Meta=30;Time.realtimeSinceStartup=13;Time.timeAsDouble=Time.realtimeSinceStartup;BaselineCompleteHook.Postfix(Act(d));ProposedCompleteHook.Postfix(Act(d),d);
  A(Proposed.Learned==1&&Proposed.Pending==0&&Proposed.LearnedValue==Baseline.LearnedValue&&Proposed.LearnedValue==3*multiplier,"valid local same receipt/math remains equivalent");
 }
 // Actual old hook ignores the NPC's data and contaminates the primary local receipt.
 Reset();var local=new EntityPlayerLocal();var own=Setup(local);GameManager.Instance.World.Primary=local;var npc=Setup(new EntityAlive(),2);
 BaselineStartHook.Prefix(Act(npc));ProposedStartHook.Prefix(Act(npc),npc);A(Baseline.Pending==1&&Proposed.Pending==0,"foreign start old negative reproduced");
 Reset();BaselineStartHook.Prefix(Act(own));Start(own);Time.realtimeSinceStartup=13;Time.timeAsDouble=Time.realtimeSinceStartup;BaselineCompleteHook.Postfix(Act(npc));ProposedCompleteHook.Postfix(Act(npc),npc);A(Baseline.Learned==1&&Proposed.Learned==0&&Proposed.Pending==1,"foreign completion old negative reproduced; valid pending preserved");End(own);A(Proposed.Learned==1,"true local can finish after foreign event");
 foreach(string mutation in new[]{"remote","data","inv","item","action","ammo","mod","cancel","weaponCancel","switchback","ownerReuse","index","heldItem"}){
  Reset();var p=new EntityPlayerLocal();var d=Setup(p);var a=Act(d);Start(d);
  switch(mutation){case "remote":p.isEntityRemote=true;break;case "data":d.invData.actionData[0]=new ItemActionData();break;case "inv":p.inventory.holdingItemData=new ItemInventoryData();break;case "item":var replacement=new ItemValue{ItemClass=d.invData.itemValue.ItemClass};p.inventory.holdingItemItemValue=replacement;d.invData.itemValue=replacement;break;case "action":d.invData.itemValue.ItemClass.Actions[0]=new ItemActionRanged();break;case "ammo":d.invData.itemValue.SelectedAmmoTypeIndex=1;break;case "mod":d.invData.itemValue.modifications=new[]{new ItemValue{type=99}};break;case "cancel":d.isReloadCancelled=true;break;case "weaponCancel":d.isWeaponReloadCancelled=true;break;case "switchback":Proposed.OnRuntimeReloadCancelled(a,d);break;case "ownerReuse":d.invData.holdingEntity=new EntityPlayerLocal{entityId=p.entityId};break;case "index":d.indexInEntityOfAction=1;break;case "heldItem":p.inventory.holdingItemItemValue=new ItemValue{ItemClass=d.invData.itemValue.ItemClass};break;}
  Time.realtimeSinceStartup=13;Time.timeAsDouble=Time.realtimeSinceStartup;Proposed.OnRuntimeReloadCompleted(a,d);A(Proposed.Learned==0,"reject changed receipt "+mutation);
 }
 foreach(float elapsed in new[]{0,.01f,31,float.NaN,float.PositiveInfinity}){Reset();var d=Setup(new EntityPlayerLocal());Start(d);Time.realtimeSinceStartup=10+elapsed;Time.timeAsDouble=Time.realtimeSinceStartup;Proposed.OnRuntimeReloadCompleted(Act(d),d);A(Proposed.Learned==0,"existing elapsed guard "+elapsed);}
 Reset();var dup=Setup(new EntityPlayerLocal());Start(dup);Time.realtimeSinceStartup=11;Time.timeAsDouble=Time.realtimeSinceStartup;Start(dup);End(dup);A(Proposed.LearnedValue==3,"duplicate start cannot shorten receipt");End(dup);A(Proposed.Learned==1,"duplicate completion ignored");
 Reset();var before=Setup(new EntityPlayerLocal());Start(before);var later=Setup((EntityPlayerLocal)before.invData.holdingEntity);Start(later);Time.realtimeSinceStartup=12;Time.timeAsDouble=Time.realtimeSinceStartup;Proposed.OnRuntimeReloadCompleted(Act(before),before);A(Proposed.Pending==1&&Proposed.Learned==0,"stale different data cannot consume newer receipt");End(later);A(Proposed.Learned==1,"newer receipt accepted");
 Reset();var bad=Setup(new EntityPlayerLocal());bad.indexInEntityOfAction=1;Start(bad);A(Proposed.Pending==0,"invalid action index start");Proposed.OnRuntimeReloadStarted(null,null);Proposed.OnRuntimeReloadCompleted(null,null);Proposed.OnRuntimeReloadCancelled(null,null);A(Proposed.Pending==0,"null guards");
 Reset();var host=Setup(new EntityPlayerLocal());Start(host);Proposed.OnRuntimeReloadCancelled(Act(host),Setup(new EntityAlive(),8));A(Proposed.Pending==1,"foreign cancel preserves local");Proposed.OnRuntimeReloadCancelled(Act(host),host);A(Proposed.Pending==0,"real cancel/stop invalidation");End(host);A(Proposed.Learned==0,"interrupted receipt not learned");
 Reset();var remote=Setup(new EntityPlayerLocal{isEntityRemote=true});Start(remote);A(Proposed.Pending==0,"remote-local start rejected");
 Reset();var switchedPlayer=new EntityPlayerLocal();var first=Setup(switchedPlayer);Baseline.OnRuntimeReloadStarted(switchedPlayer);Start(first);Setup(switchedPlayer);Time.realtimeSinceStartup=13;Time.timeAsDouble=Time.realtimeSinceStartup;Baseline.OnRuntimeReloadCompleted(switchedPlayer);Proposed.OnRuntimeReloadCompleted(Act(first),first);A(Baseline.Learned==1&&Proposed.Learned==0,"old public completion accepts switched item; new rejects original receipt");

 Reset();var clockOwner=new EntityPlayerLocal();var clockData=Setup(clockOwner);Start(clockData);End(clockData);
 A(Proposed.LearnedValue==3,"initial valid receipt");
 Time.realtimeSinceStartup=20;Time.timeAsDouble=20;Start(clockData);
 Time.realtimeSinceStartup=46;Time.timeAsDouble=23;Proposed.OnRuntimeReloadCompleted(Act(clockData),clockData);
 A(Proposed.LearnedValue==3&&Proposed.Pending==0,"26 second hitch cannot replace valid calibration");
 Reset();clockData=Setup(new EntityPlayerLocal());Start(clockData);Time.realtimeSinceStartup=36;Time.timeAsDouble=13;
 Proposed.OnRuntimeReloadCompleted(Act(clockData),clockData);A(Proposed.Learned==0,"cold stalled reload keeps fallback");
 foreach(var sample in new[]{(3f,3d,1f,true),(3.2f,3d,2f,true),(26f,3d,1f,false),(3f,0d,1f,false),(3f,6d,1f,false),(3f,double.NaN,1f,false),(3f,double.PositiveInfinity,1f,false),(3f,3d,float.NaN,false),(3f,3d,0f,false),(31f,31d,1f,false)}){
  bool ok=Proposed.TryNormalizeReloadSample(sample.Item1,sample.Item2,sample.Item3,out float value);
  A(ok==sample.Item4,"paired-clock validity "+sample);
  if(ok)A(value==sample.Item1*sample.Item3,"preserves measured normalization");
 }
 Console.WriteLine($"PASS {checks} actual-method/hook checks; owner/item/action/ammo/cancel/stale negatives and valid unchanged math; native execution NOT RUN");
 }
}

public static class Program{public static void Main()=>Checks.Run();}