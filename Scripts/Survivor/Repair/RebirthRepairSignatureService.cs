using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;

#nullable disable

public sealed class RebirthPendingRepairSignature
{
    public int PlayerId;
    public int ItemType;
    public ushort Seed;
    public ushort Quality;
    public float PreUseTimes;
    public int PreMax;
    public float OriginalCap;
    public DateTime CreatedUtc;
    public int AcceptedRevision;
}

/// <summary>
/// Chunk E authority for Maintenance Technician Nothing Is Disposable and Gunsmith/Tailor Master
/// Restoration.  It modifies only the native V3 DegradationMax item magnitude after a verified
/// successful item repair.  Native repair/death degradation remains the underlying authority.
/// </summary>
public static class RebirthRepairSignatureService
{
    public const string MaintenanceBonusId = "background_bonus.nothing_is_disposable";
    public const string GunsmithBonusId = "background_bonus.gunsmith_master_restoration";
    public const string TailorBonusId = "background_bonus.tailor_master_restoration";
    public const float DefaultRestorationFraction = 0.25f; // implementation tuning; design intentionally unlocked
    private const double PendingSeconds = 900.0;
    private static readonly object Gate = new object();
    private static DateTime NextCleanupUtc = DateTime.MinValue;
    private static readonly Dictionary<int, List<RebirthPendingRepairSignature>> Pending = new Dictionary<int, List<RebirthPendingRepairSignature>>();
    private static readonly Queue<RebirthRepairConditionCorrection> Corrections = new Queue<RebirthRepairConditionCorrection>();
    private static bool installed;
    private static bool nativeInventoryCommitPatched;
    private static bool deathPatched;

    public static string Install(Harmony harmony)
    {
        if (installed) return "[REBIRTH Repair Signatures] already installed";
        installed = true;
        if (harmony != null)
        {
            Type packageType = AccessTools.TypeByName("NetPackagePlayerInventory");
            MethodInfo process = packageType != null ? AccessTools.Method(packageType, "ProcessPackage", new Type[] { typeof(World), typeof(GameManager) }) : null;
            if (process != null)
            {
                harmony.Patch(process, prefix: new HarmonyMethod(typeof(RebirthRepairSignatureService), nameof(NativeInventoryCommitPrefix)));
                nativeInventoryCommitPatched = true;
            }
            // EntityAlive owns the V3.2 virtual death implementation. Patching it once covers
            // EntityPlayer and EntityPlayerLocal (the local override calls base) without noisy
            // AccessTools probes for non-declared overrides.
            PatchDeathMethod(harmony, typeof(EntityAlive));
        }
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnGameUpdate));
        ModEvents.WorldShuttingDown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SWorldShuttingDownData>(OnWorldShuttingDown));
        ModEvents.GameShutdown.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameShutdownData>(OnGameShutdown));
        return "[REBIRTH Repair Signatures] installed nativeInventoryCommit=" + nativeInventoryCommitPatched + " deathBaseline=" + deathPatched;
    }

    public static void BeginRepairFromUi(EntityPlayer player, ItemValue item)
    {
        if (player == null || item == null || item.ItemClass == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        float localCap = ResolveOriginalCap(item, Math.Max(0, item.MaxUseTimes));
        RebirthItemProvenanceAdapter.EnsureDurabilityOriginalMaxUseTimes(item, localCap);
        World world = player.world;
        if (world == null) return;
        if (world.IsRemote())
        {
            ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
            if (c != null) c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthRepairSignatureBegin>().Setup(player.entityId, item.type, item.Seed, item.Quality));
            return;
        }
        BeginServerRepair(player, item.type, item.Seed, item.Quality);
    }

    public static bool BeginServerRepair(EntityPlayer player, int itemType, ushort seed, ushort quality)
    {
        if (player == null || player.world == null || player.world.IsRemote() || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        ItemValue live;
        if (!TryFindPlayerItem(player, itemType, seed, quality, out live) || live == null || live.UseTimes <= 0f) return false;
        float cap = ResolveOriginalCap(live, Math.Max(0, live.MaxUseTimes));
        RebirthItemProvenanceAdapter.EnsureDurabilityOriginalMaxUseTimes(live, cap);
        RebirthPendingRepairSignature pending = BuildPending(player.entityId, live, cap);
        lock (Gate)
        {
            CleanupLocked();
            List<RebirthPendingRepairSignature> list;
            if (!Pending.TryGetValue(player.entityId, out list)) Pending[player.entityId] = list = new List<RebirthPendingRepairSignature>();
            for (int i = list.Count - 1; i >= 0; i--)
                if (SameKey(list[i], itemType, seed, quality)) list.RemoveAt(i);
            list.Add(pending);
        }
        return true;
    }

    public static void CompleteLocalRepair(EntityPlayer player, ItemValue repairedIdentity)
    {
        if (player == null || repairedIdentity == null || player.world == null || player.world.IsRemote()) return;
        RebirthPendingRepairSignature pending = TakePending(player.entityId, repairedIdentity.type, repairedIdentity.Seed, repairedIdentity.Quality, false);
        if (pending == null || !MatchesAcceptedRevision(pending, repairedIdentity)) return;
        bool inBag; int slot; ItemStack stack;
        if (!TryFindPlayerStack(player, pending.ItemType, pending.Seed, pending.Quality, out inBag, out slot, out stack) || stack.itemValue == null) return;
        if (stack.itemValue.UseTimes >= pending.PreUseTimes - 0.001f) return; // retryable non-completion retains exactly the existing token
        bool completed = ApplyAuthoritativeRepairOutcome(player, pending, stack.itemValue, "local-output");
        if (inBag) player.bag.SetSlot(slot, stack); else player.inventory.SetItem(slot, stack);
        if (completed) RemovePending(pending);
    }

    public static void CancelPendingRepair(int playerId, ItemValue identity)
    {
        if (identity == null) return;
        RebirthPendingRepairSignature pending = TakePending(playerId, identity.type, identity.Seed, identity.Quality, false);
        if (pending != null && MatchesAcceptedRevision(pending, identity)) RemovePending(pending);
    }

    public static void StampDeathBaselines(EntityPlayer player)
    {
        if (player == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        StampArray(player.inventory != null ? player.inventory.ItemGrid.items : null);
        StampArray(player.bag != null ? player.bag.ItemGrid.items : null);
    }

    public static int CalculateMaintenanceTarget(int preMax, int nativePostMax, float lossMultiplier)
    {
        preMax = Math.Max(0, preMax); nativePostMax = Math.Max(0, nativePostMax);
        if (nativePostMax >= preMax) return nativePostMax;
        float m = Mathf.Clamp01(lossMultiplier);
        int remainingLoss = Mathf.CeilToInt((preMax - nativePostMax) * m);
        return Math.Max(nativePostMax, preMax - remainingLoss);
    }

    public static int CalculateRestorationTarget(int currentMax, int originalCap, float fraction)
    {
        currentMax = Math.Max(0, currentMax); originalCap = Math.Max(currentMax, originalCap);
        if (currentMax >= originalCap) return currentMax;
        float f = Mathf.Clamp01(fraction);
        int recovery = Mathf.CeilToInt((originalCap - currentMax) * f);
        return Math.Min(originalCap, currentMax + Math.Max(0, recovery));
    }

    public static bool IsFirearm(ItemValue item)
    {
        if (item == null || item.ItemClass == null) return false;
        return RebirthProgressionRuntimeConfig.IsFirearmSkill(RebirthProgressionRuntimeConfig.ClassifyCombat(item));
    }

    public static bool IsWearable(ItemValue item)
    {
        return item != null && item.ItemClass != null && RebirthServiceCraftSkillService.IsTailoringWearableName(item.ItemClass.GetItemName());
    }

    public static string BuildDebugReport(EntityPlayer player)
    {
        StringBuilder b = new StringBuilder();
        b.AppendLine("[REBIRTH Repair Signatures]");
        b.AppendLine("status=IMPLEMENTED_PREBOOT compileClaim=False nativeInventoryCommit=" + nativeInventoryCommitPatched + " deathBaseline=" + deathPatched);
        b.AppendLine("maintenanceLossMultiplier=0.5 locked=True restorationDefault=" + DefaultRestorationFraction.ToString("0.###", CultureInfo.InvariantCulture) + " locked=False");
        b.AppendLine("authority=server verified repair evidence + native inventory persistence commit; adapter=V3 ItemValue.Stats DegradationMax only; failClosed=True");
        if (player != null)
        {
            RebirthBackgroundBonusDefinition d = RebirthBackgroundBonusService.GetSignatureBonus(player);
            b.AppendLine("player=" + player.entityId + " bonus=" + (d != null ? d.Id : "<none>"));
        }
        lock (Gate)
        {
            int count = 0; foreach (List<RebirthPendingRepairSignature> list in Pending.Values) count += list.Count;
            b.AppendLine("pendingRepairs=" + count + " queuedCorrections=" + Corrections.Count);
        }
        return b.ToString();
    }

    // Harmony dynamic Prefix for NetPackagePlayerInventory.ProcessPackage.  Object/reflection is
    // deliberate: the b259 capture predates V3 ItemValue.Stats and we do not bind this project to
    // obsolete package field visibility.
    public static void NativeInventoryCommitPrefix(object __instance, World _world)
    {
        if (__instance == null || _world == null || _world.IsRemote() || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return;
        object sender = ReadMember(__instance, "Sender");
        int playerId = ReadInt(sender, "entityId", -1);
        if (playerId < 0) playerId = ReadInt(sender, "EntityId", -1);
        if (playerId < 0) return;
        EntityPlayer player = _world.GetEntity(playerId) as EntityPlayer;
        if (player == null) return;

        List<RebirthPendingRepairSignature> snapshot = GetPendingSnapshot(playerId);
        if (snapshot.Count == 0) return;
        ItemStack[] incomingTool = ReadMember(__instance, "toolbelt") as ItemStack[];
        ItemStack[] incomingBag = ReadMember(__instance, "bag") as ItemStack[];
        object latest = ReadMember(sender, "latestPlayerData");
        ItemStack[] priorTool = latest != null ? ReadMember(latest, "inventory") as ItemStack[] : null;
        ItemStack[] priorBag = latest != null ? ReadMember(latest, "bag") as ItemStack[] : null;

        for (int i = 0; i < snapshot.Count; i++)
        {
            RebirthPendingRepairSignature p = snapshot[i];
            ItemValue post = FindItem(incomingTool, incomingBag, p.ItemType, p.Seed, p.Quality);
            if (post == null) continue;
            ItemValue pre = FindItem(priorTool, priorBag, p.ItemType, p.Seed, p.Quality);
            float preUse = pre != null ? pre.UseTimes : p.PreUseTimes;
            if (post.UseTimes >= preUse - 0.001f) continue;
            if (pre != null)
            {
                if (!MatchesAcceptedRevision(p, pre)) continue;
                p.PreUseTimes = pre.UseTimes;
                p.PreMax = Math.Max(0, pre.MaxUseTimes);
                p.OriginalCap = ResolveOriginalCap(pre, p.OriginalCap > 0f ? p.OriginalCap : p.PreMax);
            }
            bool completed = ApplyAuthoritativeRepairOutcome(player, p, post, "server-inventory-commit");
            if (!completed) continue; // adapter failure remains retryable without duplicating the token
            RemovePending(p);
            lock (Gate) Corrections.Enqueue(new RebirthRepairConditionCorrection(playerId, post.type, post.Seed, post.Quality, Math.Max(0, post.MaxUseTimes), ResolveOriginalCap(post, p.OriginalCap)));
        }
    }

    public static void ApplyClientCorrection(int playerId, int itemType, ushort seed, ushort quality, int targetMax, float originalCap)
    {
        if (GameManager.Instance == null || GameManager.Instance.World == null) return;
        EntityPlayerLocal player = GameManager.Instance.World.GetPrimaryPlayer();
        if (player == null || player.entityId != playerId) return;
        bool bag; int slot; ItemStack stack;
        if (!TryFindPlayerStack(player, itemType, seed, quality, out bag, out slot, out stack) || stack.itemValue == null) return;
        RebirthItemProvenanceAdapter.ForceDurabilityOriginalMaxUseTimes(stack.itemValue, originalCap);
        int applied; string reason;
        RebirthItemConditionStatAdapter.TrySetEffectiveMaxUseTimes(stack.itemValue, targetMax, Math.Max(targetMax, Mathf.RoundToInt(originalCap)), out applied, out reason);
        if (bag) player.bag.SetSlot(slot, stack); else player.inventory.SetItem(slot, stack);
    }

    private static bool ApplyAuthoritativeRepairOutcome(EntityPlayer player, RebirthPendingRepairSignature p, ItemValue post, string source)
    {
        if (player == null || p == null || post == null) return false;
        int nativePostMax = Math.Max(0, post.MaxUseTimes);
        int preMax = Math.Max(0, p.PreMax);
        float capFloat = p.OriginalCap > 0f ? p.OriginalCap : ResolveOriginalCap(post, Math.Max(preMax, nativePostMax));
        int cap = Math.Max(Math.Max(preMax, nativePostMax), Mathf.RoundToInt(capFloat));
        RebirthItemProvenanceAdapter.ForceDurabilityOriginalMaxUseTimes(post, cap);
        RebirthBackgroundBonusDefinition bonus = RebirthBackgroundBonusService.GetSignatureBonus(player);
        if (bonus == null) return true;

        int target = nativePostMax;
        if (string.Equals(bonus.Id, MaintenanceBonusId, StringComparison.OrdinalIgnoreCase))
        {
            target = CalculateMaintenanceTarget(preMax, nativePostMax, GetTuningFloat(bonus, "repair_permanent_loss_multiplier", 0.5f));
        }
        else if (string.Equals(bonus.Id, GunsmithBonusId, StringComparison.OrdinalIgnoreCase) && IsFirearm(post))
        {
            target = CalculateRestorationTarget(nativePostMax, cap, GetTuningFloat(bonus, "restoration_fraction", DefaultRestorationFraction));
        }
        else if (string.Equals(bonus.Id, TailorBonusId, StringComparison.OrdinalIgnoreCase) && IsWearable(post))
        {
            target = CalculateRestorationTarget(nativePostMax, cap, GetTuningFloat(bonus, "restoration_fraction", DefaultRestorationFraction));
        }
        if (target <= nativePostMax || target > cap) return true;
        int applied; string reason;
        if (!RebirthItemConditionStatAdapter.TrySetEffectiveMaxUseTimes(post, target, cap, out applied, out reason))
        {
            Log.Warning("[REBIRTH Repair Signatures] failed closed source=" + source + " item=" + SafeName(post) + " nativeMax=" + nativePostMax + " target=" + target + " cap=" + cap + " reason=" + reason);
            return false;
        }
        return applied > nativePostMax && applied <= cap;
    }

    private static float ResolveOriginalCap(ItemValue value, float fallback)
    {
        float cap;
        if (RebirthItemProvenanceAdapter.TryGetDurabilityOriginalMaxUseTimes(value, out cap) && cap > 0f) return Math.Max(cap, fallback);
        return Math.Max(0f, fallback);
    }

    private static float GetTuningFloat(RebirthBackgroundBonusDefinition bonus, string key, float fallback)
    {
        RebirthBackgroundBonusTuningValue v; float parsed;
        return bonus != null && bonus.TryGetTuning(key, out v) && v != null && float.TryParse(v.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ? parsed : fallback;
    }

    private static RebirthPendingRepairSignature BuildPending(int playerId, ItemValue item, float cap)
    {
        return new RebirthPendingRepairSignature { PlayerId = playerId, ItemType = item.type, Seed = item.Seed, Quality = item.Quality, PreUseTimes = item.UseTimes, PreMax = Math.Max(0, item.MaxUseTimes), OriginalCap = Math.Max(cap, item.MaxUseTimes), CreatedUtc = DateTime.UtcNow, AcceptedRevision = ItemRevision(item) };
    }

    private static int ItemRevision(ItemValue item)
    {
        if (item == null) return 0;
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + item.type; hash = hash * 31 + item.Seed; hash = hash * 31 + item.Quality;
            hash = hash * 31 + item.UseTimes.GetHashCode(); hash = hash * 31 + item.MaxUseTimes;
            float cap; if (RebirthItemProvenanceAdapter.TryGetDurabilityOriginalMaxUseTimes(item, out cap)) hash = hash * 31 + cap.GetHashCode();
            return hash;
        }
    }
    private static bool MatchesAcceptedRevision(RebirthPendingRepairSignature pending, ItemValue item)
    {
        return pending != null && item != null && pending.AcceptedRevision == ItemRevision(item);
    }

    private static void StampArray(ItemStack[] slots)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemValue v = slots[i] != null ? slots[i].itemValue : null;
            if (v == null || v.ItemClass == null || v.MaxUseTimes <= 0) continue;
            RebirthItemProvenanceAdapter.EnsureDurabilityOriginalMaxUseTimes(v, v.MaxUseTimes);
        }
    }

    private static void PatchDeathMethod(Harmony harmony, Type type)
    {
        if (harmony == null || type == null) return;
        MethodInfo method = type.GetMethod("OnEntityDeath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (method == null) return;
        harmony.Patch(method, prefix: new HarmonyMethod(typeof(RebirthRepairSignatureService), nameof(DeathPrefix)));
        deathPatched = true;
    }

    public static void DeathPrefix(object __instance)
    {
        EntityPlayer player = __instance as EntityPlayer;
        if (player != null) StampDeathBaselines(player);
    }

    private static void OnGameUpdate(ref ModEvents.SGameUpdateData data)
    {
        Cleanup();
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c == null || !c.IsServer) return;
        for (;;)
        {
            RebirthRepairConditionCorrection correction;
            lock (Gate) { if (Corrections.Count == 0) break; correction = Corrections.Dequeue(); }
            EntityPlayer player = GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetEntity(correction.PlayerId) as EntityPlayer : null;
            if (player is EntityPlayerLocal) ApplyClientCorrection(correction.PlayerId, correction.ItemType, correction.Seed, correction.Quality, correction.TargetMax, correction.OriginalCap);
            else c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthRepairConditionCorrection>().Setup(correction.PlayerId, correction.ItemType, correction.Seed, correction.Quality, correction.TargetMax, correction.OriginalCap), _attachedToEntityId: correction.PlayerId);
        }
    }

    private static void OnWorldShuttingDown(ref ModEvents.SWorldShuttingDownData data) { ClearRuntime(); }
    private static void OnGameShutdown(ref ModEvents.SGameShutdownData data) { ClearRuntime(); }
    private static void ClearRuntime() { lock (Gate) { Pending.Clear(); Corrections.Clear(); } }
    private static void Cleanup() { lock (Gate) CleanupLocked(); }
    private static void CleanupLocked()
    {
        if (Pending.Count == 0) return;
        DateTime now = DateTime.UtcNow;
        if (now < NextCleanupUtc) return;
        NextCleanupUtc = now.AddSeconds(1);
        DateTime cutoff = now.AddSeconds(-PendingSeconds);
        List<int> empty = new List<int>();
        foreach (KeyValuePair<int, List<RebirthPendingRepairSignature>> kv in Pending)
        {
            kv.Value.RemoveAll(delegate(RebirthPendingRepairSignature p) { return p == null || p.CreatedUtc < cutoff; });
            if (kv.Value.Count == 0) empty.Add(kv.Key);
        }
        for (int i = 0; i < empty.Count; i++) Pending.Remove(empty[i]);
    }

    private static RebirthPendingRepairSignature TakePending(int playerId, int type, ushort seed, ushort quality, bool remove)
    {
        lock (Gate)
        {
            CleanupLocked(); List<RebirthPendingRepairSignature> list;
            if (!Pending.TryGetValue(playerId, out list)) return null;
            for (int i = list.Count - 1; i >= 0; i--) if (SameKey(list[i], type, seed, quality)) { RebirthPendingRepairSignature p = list[i]; if (remove) list.RemoveAt(i); return p; }
            return null;
        }
    }
    private static List<RebirthPendingRepairSignature> GetPendingSnapshot(int playerId)
    {
        lock (Gate) { CleanupLocked(); List<RebirthPendingRepairSignature> list; return Pending.TryGetValue(playerId, out list) ? new List<RebirthPendingRepairSignature>(list) : new List<RebirthPendingRepairSignature>(); }
    }
    private static void RemovePending(RebirthPendingRepairSignature p) { if (p == null) return; lock (Gate) { List<RebirthPendingRepairSignature> list; if (Pending.TryGetValue(p.PlayerId, out list)) list.Remove(p); } }
    private static bool SameKey(RebirthPendingRepairSignature p, int type, ushort seed, ushort quality) { return p != null && p.ItemType == type && p.Seed == seed && p.Quality == quality; }

    private static bool TryFindPlayerItem(EntityPlayer player, int type, ushort seed, ushort quality, out ItemValue value)
    {
        value = null; bool bag; int slot; ItemStack stack;
        if (!TryFindPlayerStack(player, type, seed, quality, out bag, out slot, out stack)) return false;
        value = stack.itemValue; return value != null;
    }
    private static bool TryFindPlayerStack(EntityPlayer player, int type, ushort seed, ushort quality, out bool bag, out int slot, out ItemStack found)
    {
        bag = false; slot = -1; found = ItemStack.Empty.Clone(); if (player == null) return false;
        ItemStack[] tool = player.inventory != null ? player.inventory.ItemGrid.items : null;
        if (TryFindStack(tool, type, seed, quality, out slot, out found)) return true;
        ItemStack[] bags = player.bag != null ? player.bag.ItemGrid.items : null; int b;
        if (TryFindStack(bags, type, seed, quality, out b, out found)) { bag = true; slot = b; return true; }
        return false;
    }
    private static bool TryFindStack(ItemStack[] slots, int type, ushort seed, ushort quality, out int slot, out ItemStack found)
    {
        slot = -1; found = ItemStack.Empty.Clone(); if (slots == null) return false;
        for (int i = 0; i < slots.Length; i++) { ItemStack s = slots[i]; ItemValue v = s != null ? s.itemValue : null; if (v != null && v.type == type && v.Seed == seed && v.Quality == quality) { slot = i; found = s; return true; } }
        return false;
    }
    private static ItemValue FindItem(ItemStack[] a, ItemStack[] b, int type, ushort seed, ushort quality)
    {
        ItemValue v = FindItem(a, type, seed, quality); return v ?? FindItem(b, type, seed, quality);
    }
    private static ItemValue FindItem(ItemStack[] a, int type, ushort seed, ushort quality)
    {
        if (a == null) return null; for (int i = 0; i < a.Length; i++) { ItemValue v = a[i] != null ? a[i].itemValue : null; if (v != null && v.type == type && v.Seed == seed && v.Quality == quality) return v; } return null;
    }

    private static object ReadMember(object target, string name)
    {
        if (target == null || string.IsNullOrEmpty(name)) return null;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, flags); if (f != null) return f.GetValue(target);
            PropertyInfo p = t.GetProperty(name, flags); if (p != null) return p.GetValue(target, null);
        }
        return null;
    }
    private static int ReadInt(object target, string name, int fallback) { object v = ReadMember(target, name); try { return v != null ? Convert.ToInt32(v) : fallback; } catch { return fallback; } }
    private static string SafeName(ItemValue v) { return v != null && v.ItemClass != null ? (v.ItemClass.GetItemName() ?? "<unnamed>") : "<null>"; }
}

public struct RebirthRepairConditionCorrection
{
    public int PlayerId, ItemType, TargetMax; public ushort Seed, Quality; public float OriginalCap;
    public RebirthRepairConditionCorrection(int playerId, int itemType, ushort seed, ushort quality, int targetMax, float originalCap) { PlayerId = playerId; ItemType = itemType; Seed = seed; Quality = quality; TargetMax = targetMax; OriginalCap = originalCap; }
}

[HarmonyPatch(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.SetRepairRecipe))]
internal static class RebirthRepairSignatureQueuePatch
{
    private static void Prefix(XUiC_RecipeStack __instance, ItemValue _itemToRepair)
    {
        EntityPlayer player = __instance != null && __instance.xui != null && __instance.xui.playerUI != null ? __instance.xui.playerUI.entityPlayer : null;
        RebirthRepairSignatureService.BeginRepairFromUi(player, _itemToRepair);
    }
}

[HarmonyPatch(typeof(XUiC_RecipeStack), nameof(XUiC_RecipeStack.HandleOnPress))]
internal static class RebirthRepairSignatureCancelPatch
{
    internal struct State { public int PlayerId; public ItemValue Identity; }
    private static void Prefix(XUiC_RecipeStack __instance, out State __state)
    {
        __state = new State();
        if (__instance == null || __instance.OriginalItem == null || __instance.OriginalItem.IsEmpty()) return;
        __state.PlayerId = __instance.StartingEntityId;
        __state.Identity = __instance.OriginalItem.Clone();
    }
    private static void Postfix(State __state)
    {
        if (__state.Identity != null) RebirthRepairSignatureService.CancelPendingRepair(__state.PlayerId, __state.Identity);
    }
}

[HarmonyPatch(typeof(XUiC_RecipeStack), "outputStack")]
internal static class RebirthRepairSignatureCompletionPatch
{
    internal struct State { public int PlayerId; public ItemValue Identity; }
    private static void Prefix(XUiC_RecipeStack __instance, out State __state)
    {
        __state = new State(); if (__instance == null || __instance.AmountToRepair <= 0 || __instance.OriginalItem == null) return;
        __state.PlayerId = __instance.StartingEntityId; __state.Identity = __instance.OriginalItem.Clone();
    }
    private static void Postfix(bool __result, State __state)
    {
        if (!__result || __state.Identity == null || GameManager.Instance == null || GameManager.Instance.World == null) return;
        EntityPlayer p = GameManager.Instance.World.GetEntity(__state.PlayerId) as EntityPlayer;
        if (p != null && !GameManager.Instance.World.IsRemote()) RebirthRepairSignatureService.CompleteLocalRepair(p, __state.Identity);
    }
}
