using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// The 2.6 Berserker rage, ported to code.
///  - Page Down: Defensive Rage (Berserker), or a Rage Capsule (Shared) for everyone else.
///  - Page Up: Offensive Rage, consumes a Rage Capsule (Berserker). Eating a capsule does the same.
///  - Blood Moon Rage starts by itself on a Blood Moon and ends with it.
///  - Death Rage: once an hour, outside the Blood Moon, a Berserker survives a killing blow at 50 health.
///  - Defensive, Blood Moon, Shared and Death Rage knock down enemies within 2 m (5 damage per rank, 3.5 s ragdoll).
/// The 2.6 rank (1-15) comes from the 3.0 Rage Skill: rank = ceil(skill x 15 / 100), at least 1 (provisional mapping).
/// Gates kept from the 3.0 Berserker design: Offensive Rage needs Offensive Rage Knowledge and Rage 50,
/// Blood Moon Rage needs Blood Rage Knowledge and Rage 80. The server decides; clients ask it.
/// </summary>
public static class RebirthBerserkerRage
{
    public const string CapsuleItem = "rebirthRageCapsule", CapsuleOtherItem = "rebirthRageCapsuleOther";
    public const float OffensiveSkill = 50f, BloodSkill = 80f, DeathRageHealth = 50f;
    // 2.6 drained food and water while enraged; 3.0 metabolism uses Energy for that, so each rage costs Energy once.
    public const float DefensiveEnergy = 15f, OffensiveEnergy = 20f, OtherEnergy = 15f;
    private const string KnockdownCooldown = "buffInjuryKnockdown01Cooldown";
    private static readonly FastTags<TagGroup.Global> NoKnockdownTags = FastTags<TagGroup.Global>.Parse("player,trader,ally,survivor,deployed,vehicle");

    public enum Kind { Defensive, Offensive, Other }

    private static bool installed;
    private static float nextServerTick, nextOwnerTick;
    private static readonly List<Entity> Nearby = new List<Entity>();

    public static void Install()
    {
        if (installed) return;
        installed = true;
        ModEvents.GameUpdate.RegisterHandler(new ModEvents.ModEventHandlerDelegate<ModEvents.SGameUpdateData>(OnUpdate));
    }

    // ------------------------------------------------------------------ state

    public static bool IsBloodMoon() => GameManager.Instance?.World?.isEventBloodMoon == true;

    public static bool IsBerserker(EntityPlayer p)
    {
        if (p == null || p.world == null || !RebirthSurvivorMode.IsEnabledForCurrentWorld()) return false;
        return p.world.IsRemote() ? RebirthSurvivorClientState.HasProjectedDiscipline(p, RebirthSurvivorIds.DisciplineBerserker) : RebirthRageService.HasBerserker(p);
    }

    public static bool HasKnowledge(EntityPlayer p, string id)
    {
        if (p == null || p.world == null) return false;
        if (p.world.IsRemote())
        {
            var owner = RebirthSurvivorClientState.GetOwnerStateSnapshot();
            if (owner == null || !ReferenceEquals(p.world.GetPrimaryPlayer(), p)) return false;
            foreach (string k in owner.KnowledgeIds) if (string.Equals(k, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        return RebirthWorldCharacterService.TryGet(p, out var r) && r?.Progression != null && r.Progression.KnowledgeIds.Contains(id);
    }

    public static float RageSkill(EntityPlayer p) => RageSkill(p, out _);

    /// <summary>Rage Skill level; progress is the fraction toward the next level.</summary>
    public static float RageSkill(EntityPlayer p, out float progress)
    {
        progress = 0f;
        if (p == null) return 0f;
        return RebirthServiceCraftSkillService.TryGetPracticalSkillProgress(p, RebirthSurvivorIds.SkillRage, out float v, out progress) ? v : 0f;
    }

    public static int Tier(EntityPlayer p) => TierForSkill(RageSkill(p));
    public static int TierForSkill(float skill) => RebirthRageBuffs.Clamp(Mathf.CeilToInt(Mathf.Max(0f, skill) * RebirthRageBuffs.MaxTier / 100f));

    private static bool Has(EntityAlive e, string buff) => e?.Buffs != null && e.Buffs.HasBuff(buff);

    /// <summary>Which rage is running, if any: offensive, defensive, blood, other or death, with its rank.</summary>
    public static bool TryGetActive(EntityAlive e, out string kind, out int tier)
    {
        kind = string.Empty; tier = 0;
        if (e?.Buffs == null) return false;
        if (Has(e, RebirthRageBuffs.Offensive)) { kind = "offensive"; tier = (int)e.Buffs.GetCustomVar(RebirthRageBuffs.OffPrefix + RebirthRageBuffs.Rank); }
        else if (Has(e, RebirthRageBuffs.Defensive)) { kind = "defensive"; tier = (int)e.Buffs.GetCustomVar(RebirthRageBuffs.DefPrefix + RebirthRageBuffs.Rank); }
        else if (Has(e, RebirthRageBuffs.Death)) { kind = "death"; tier = 10; }
        else if (Has(e, RebirthRageBuffs.Other)) { kind = "other"; tier = 5; }
        else if (Has(e, RebirthRageBuffs.Blood)) { kind = "blood"; tier = (int)e.Buffs.GetCustomVar(RebirthRageBuffs.BloodPrefix + RebirthRageBuffs.Rank); }
        else return false;
        return true;
    }

    // ------------------------------------------------------------------ rules (2.6 order)

    /// <summary>Checks a request; returns false with the 2.6 tooltip key when it is refused.</summary>
    public static bool Check(EntityPlayer p, Kind kind, out string tooltip, out bool missingCapsule)
    {
        tooltip = null; missingCapsule = false;
        if (p == null || p.IsDead() || p.Buffs == null) { tooltip = "ttCannotUseAtThisTime"; return false; }
        bool defensive = Has(p, RebirthRageBuffs.Defensive), offensive = Has(p, RebirthRageBuffs.Offensive), other = Has(p, RebirthRageBuffs.Other);
        if (kind == Kind.Other)
        {
            if (Has(p, RebirthRageBuffs.OtherCooldown)) { tooltip = "ttHasRageCapsuleOtherCooldown"; return false; }
            if (other || defensive || offensive) { tooltip = "ttCannotUseAtThisTime"; return false; }
            if (CountItem(p, CapsuleOtherItem) <= 0) { tooltip = "ttRageCapsuleMissing"; missingCapsule = true; return false; }
            return true;
        }
        if (!IsBerserker(p)) { tooltip = "ttCannotUseAtThisTime"; return false; }
        bool defCooldown = Has(p, RebirthRageBuffs.DefensiveCooldown), offCooldown = Has(p, RebirthRageBuffs.OffensiveCooldown), bloodCooldown = Has(p, RebirthRageBuffs.BloodCooldown);
        if (!other && !defensive && !offensive && !defCooldown && !offCooldown && !bloodCooldown)
        {
            if (kind == Kind.Offensive)
            {
                if (RageSkill(p) < OffensiveSkill || !HasKnowledge(p, RebirthSurvivorIds.KnowledgeRageOffensive)) { tooltip = "xuiRebirthRageOffensiveLocked"; return false; }
                if (CountItem(p, CapsuleItem) <= 0) { tooltip = "ttRageCapsuleMissing"; missingCapsule = true; return false; }
            }
            return true;
        }
        if (defCooldown || offCooldown || bloodCooldown) tooltip = "ttRageCooldownActive";
        else if (defensive) tooltip = "ttRageDefensiveActive";
        else if (offensive) tooltip = "ttRageOffensiveActive";
        else tooltip = "ttRageOffensiveOther";
        return false;
    }

    /// <summary>Server: checks again, takes the capsule and starts the rage. Returns the tooltip to show.</summary>
    public static bool ServerStart(EntityPlayer p, Kind kind, int itemType, ushort seed, out string message)
    {
        message = string.Empty;
        if (p == null || p.world == null || p.world.IsRemote()) { message = Localization.Get("ttCannotUseAtThisTime"); return false; }
        if (RebirthBackpackLibraryReservation.BlocksResourceUse(p)) { message = Localization.Get("xuiRebirthLibraryTransferPending"); return false; }
        if (!Check(p, kind, out string tooltip, out _)) { message = Localization.Get(tooltip); return false; }
        if (kind != Kind.Defensive && !TakeOne(p, kind == Kind.Offensive ? CapsuleItem : CapsuleOtherItem, itemType)) { message = Localization.Get("ttRageCapsuleMissing"); return false; }
        int tier = Tier(p);
        if (kind == Kind.Defensive) Start(p, RebirthRageBuffs.Defensive, RebirthRageBuffs.DefPrefix, tier, RebirthRageBuffs.DefensiveStats(tier), RebirthRageBuffs.DefensiveSeconds);
        else if (kind == Kind.Offensive) Start(p, RebirthRageBuffs.Offensive, RebirthRageBuffs.OffPrefix, tier, RebirthRageBuffs.OffensiveStats(tier), RebirthRageBuffs.OffensiveSeconds(tier));
        else p.Buffs.AddBuff(RebirthRageBuffs.Other, p.entityId);
        ChargeEnergy(p, kind == Kind.Defensive ? DefensiveEnergy : kind == Kind.Offensive ? OffensiveEnergy : OtherEnergy);
        RebirthRageService.NoteActivation(p);
        return true;
    }

    /// <summary>Writes the rank values the buff reads, then adds the buff. A length above 0 is a rank-dependent length the
    /// owner's tick enforces (Offensive Rage); the other rages have their 2.6 length on the buff itself.</summary>
    public static void Start(EntityAlive e, string buff, string prefix, int tier, RebirthRageBuffs.Stats stats, float seconds)
    {
        RebirthRageBuffs.WriteStats(e, prefix, tier, stats);
        if (buff == RebirthRageBuffs.Offensive) SetTimer(e, RebirthRageBuffs.OffSeconds, RebirthRageBuffs.OffLeft, seconds);
        e.Buffs.AddBuff(buff, e.entityId);
    }

    private static void SetTimer(EntityAlive e, string total, string left, float seconds)
    {
        e.Buffs.SetCustomVar(total, seconds);
        e.Buffs.SetCustomVar(left, seconds);
    }

    /// <summary>Owner tick for the two rank-length buffs: ends the buff when its own elapsed time reaches the length
    /// and keeps the HUD countdown CVar current. Elapsed time is saved with the buff, so this survives a reload.</summary>
    private static void TickTimed(EntityAlive e, string buff)
    {
        if (buff == RebirthRageBuffs.Blood) return; // lasts until the Blood Moon ends
        BuffValue b = e.Buffs.GetBuff(buff);
        if (b == null || b.Remove) return;
        bool ranked = buff == RebirthRageBuffs.Offensive || buff == RebirthRageBuffs.DefensiveCooldown;
        float length = ranked ? e.Buffs.GetCustomVar(buff == RebirthRageBuffs.Offensive ? RebirthRageBuffs.OffSeconds : RebirthRageBuffs.DefCooldownSeconds) : b.BuffClass.DurationMax;
        if (ranked && length <= 0f) length = buff == RebirthRageBuffs.Offensive ? RebirthRageBuffs.OffensiveSeconds(1) : RebirthRageBuffs.DefensiveCooldownSeconds(1);
        float remaining = Mathf.Ceil(Mathf.Max(0f, length - b.DurationInSeconds));
        string left = RebirthRageBuffs.LeftCvar(buff);
        if (e.Buffs.GetCustomVar(left) != remaining) e.Buffs.SetCustomVar(left, remaining);
        if (ranked && remaining <= 0f) e.Buffs.RemoveBuff(buff);
    }

    // ------------------------------------------------------------------ buff start / finish (runs wherever the buff runs)

    public static void OnRageEvent(EntityAlive self, string kind, string evt)
    {
        if (self == null || self.Buffs == null || self.world == null) return;
        bool owner = !self.isEntityRemote, server = !self.world.IsRemote();
        if (evt == "start")
        {
            if (owner)
            {
                self.Buffs.AddBuff(RebirthRageBuffs.Prefab, self.entityId);
                if (kind == "death")
                {
                    // 2.6: Death Rage clears the Defensive, Offensive and Shared cooldowns.
                    self.Buffs.RemoveBuff(RebirthRageBuffs.DefensiveCooldown);
                    self.Buffs.RemoveBuff(RebirthRageBuffs.OffensiveCooldown);
                    self.Buffs.RemoveBuff(RebirthRageBuffs.OtherCooldown);
                    self.Buffs.AddBuff(RebirthRageBuffs.DeathProtection, self.entityId);
                }
            }
            if (server && kind != "offensive") Knockdown(self, KnockdownRank(self, kind));
            return;
        }
        if (evt == "remove" && owner) self.Buffs.RemoveBuff(RebirthRageBuffs.Prefab);
        if (!owner || self.IsDead()) return;
        // 2.6 onSelfBuffFinish / onSelfBuffRemove rules. A rage that ends on a Blood Moon gets the 30 s cooldown.
        // Offensive Rage is ended by the owner tick (a removal), and gets its cooldown either way, as in 2.6.
        bool finished = evt == "finish", bloodMoon = IsBloodMoon();
        switch (kind)
        {
            case "defensive":
                if (finished)
                {
                    if (bloodMoon) Cooldown(self, RebirthRageBuffs.BloodCooldown, RebirthRageBuffs.BloodCooldownSeconds);
                    else Cooldown(self, RebirthRageBuffs.DefensiveCooldown, RebirthRageBuffs.DefensiveCooldownSeconds((int)self.Buffs.GetCustomVar(RebirthRageBuffs.DefPrefix + RebirthRageBuffs.Rank)));
                }
                break;
            case "offensive":
                if (bloodMoon) Cooldown(self, RebirthRageBuffs.BloodCooldown, RebirthRageBuffs.BloodCooldownSeconds);
                else Cooldown(self, RebirthRageBuffs.OffensiveCooldown, RebirthRageBuffs.OffensiveCooldownSeconds);
                break;
            case "other":
                if (finished) Cooldown(self, RebirthRageBuffs.OtherCooldown, RebirthRageBuffs.OtherCooldownSeconds);
                break;
            case "death":
                Cooldown(self, RebirthRageBuffs.DeathCooldown, RebirthRageBuffs.DeathCooldownSeconds);
                break;
        }
    }

    private static void Cooldown(EntityAlive e, string buff, float seconds)
    {
        if (e.Buffs.HasBuff(buff)) return;
        if (buff == RebirthRageBuffs.DefensiveCooldown) SetTimer(e, RebirthRageBuffs.DefCooldownSeconds, RebirthRageBuffs.DefCooldownLeft, seconds);
        e.Buffs.AddBuff(buff, e.entityId);
    }

    private static int KnockdownRank(EntityAlive e, string kind)
        => kind == "defensive" ? (int)e.Buffs.GetCustomVar(RebirthRageBuffs.DefPrefix + RebirthRageBuffs.Rank)
         : kind == "blood" ? (int)e.Buffs.GetCustomVar(RebirthRageBuffs.BloodPrefix + RebirthRageBuffs.Rank)
         : kind == "death" ? 10 : 5;

    /// <summary>2.6 FuriousRamsayKnockdownBerserkerTierN: every living non-player, non-ally within 2 m takes 5 damage per rank
    /// and is ragdolled, unless the native knockdown cooldown protects it.</summary>
    public static int Knockdown(EntityAlive self, int rank)
    {
        int hit = 0;
        float r = RebirthRageBuffs.KnockdownRange;
        Vector3 c = self.position;
        Nearby.Clear();
        self.world.GetEntitiesInBounds(typeof(EntityAlive), BoundsUtils.BoundsForMinMax(c - new Vector3(r, r, r), c + new Vector3(r, r, r)), Nearby);
        foreach (Entity ent in Nearby)
        {
            if (!(ent is EntityAlive e) || e == self || e.IsDead() || e is EntityPlayer || e is EntityVehicle) continue;
            if ((e.position - c).sqrMagnitude > r * r || e.HasAnyTags(NoKnockdownTags) || Has(e, RebirthRageBuffs.Knockdown) || Has(e, KnockdownCooldown)) continue;
            var source = new DamageSource(EnumDamageSource.External, EnumDamageTypes.Disease) { CreatorEntityId = self.entityId };
            e.DamageEntity(source, Mathf.RoundToInt(RebirthRageBuffs.KnockdownDamagePerRank * RebirthRageBuffs.Clamp(rank)), false, 1f);
            if (!e.IsDead()) e.Buffs.AddBuff(RebirthRageBuffs.Knockdown, self.entityId);
            hit++;
        }
        Nearby.Clear();
        Log.Out("[REBIRTH Rage] knockdown rank " + RebirthRageBuffs.Clamp(rank) + " by " + self.entityId + ": " + hit + " enemies within " + r + " m, " + (RebirthRageBuffs.KnockdownDamagePerRank * RebirthRageBuffs.Clamp(rank)) + " damage each");
        return hit;
    }

    // ------------------------------------------------------------------ client input

    /// <summary>Owner side: 2.6 feedback first (denied sound, tooltip, missing item), then the server request.</summary>
    public static void Request(EntityPlayerLocal p, Kind kind, int itemType = 0, ushort seed = 0)
    {
        if (p == null || p.world == null) return;
        if (!Check(p, kind, out string tooltip, out bool missing))
        {
            Deny(p, tooltip, missing ? (kind == Kind.Offensive ? CapsuleItem : CapsuleOtherItem) : null);
            return;
        }
        if (!p.world.IsRemote())
        {
            if (!ServerStart(p, kind, itemType, seed, out string message)) Deny(p, null, null, message);
            return;
        }
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        var request = NetPackageManager.GetPackage<NetPackageRebirthSurvivorSupportActionRequest>();
        ItemValue item = itemType > 0 ? new ItemValue(itemType) { Seed = seed } : null;
        c?.SendToServer(request.Setup(p.entityId, RebirthSurvivorSupportAction.UseRageCapsule, item, SlotFor(kind)));
    }

    public static string SlotFor(Kind kind) => kind == Kind.Defensive ? "rage.defensive" : kind == Kind.Other ? "rage.other" : "rage.offensive";
    public static Kind KindFromSlot(string slot) => slot == "rage.defensive" ? Kind.Defensive : slot == "rage.other" ? Kind.Other : Kind.Offensive;

    private static void Deny(EntityPlayerLocal p, string key, string missingItem, string text = null)
    {
        Audio.Manager.PlayInsidePlayerHead(missingItem != null ? "misc/missingitemtorepair" : "ui_denied");
        string message = text ?? (key != null ? Localization.Get(key) : null);
        if (!string.IsNullOrEmpty(message)) GameManager.ShowTooltip(p, message);
        ItemClass ic = missingItem != null ? ItemClass.GetItemClass(missingItem) : null;
        if (ic != null) p.AddUIHarvestingItem(new ItemStack(new ItemValue(ic.Id), 0), true);
    }

    /// <summary>Page Down / Page Up, as 2.6 read them. Only when no window or text field has focus.</summary>
    private static void PollKeys(World world)
    {
        EntityPlayerLocal p = world.GetPrimaryPlayer();
        if (p == null || p.IsDead() || p.AttachedToEntity != null) return;
        bool down = Input.GetKeyDown(KeyCode.PageDown), up = Input.GetKeyDown(KeyCode.PageUp);
        if (!down && !up) return;
        var wm = LocalPlayerUI.GetUIForPlayer(p)?.windowManager;
        if (wm == null || wm.IsModalWindowOpen() || wm.IsInputActive()) return;
        if (down) Request(p, IsBerserker(p) ? Kind.Defensive : Kind.Other);
        else if (IsBerserker(p)) Request(p, Kind.Offensive);
    }

    // ------------------------------------------------------------------ inventory and energy

    public static int CountItem(EntityPlayer p, string name)
    {
        ItemValue iv = ItemClass.GetItem(name);
        if (iv == null || iv.IsEmpty() || p == null) return 0;
        return (p.bag?.GetItemCount(iv) ?? 0) + (p.inventory?.GetItemCount(iv) ?? 0);
    }

    /// <summary>Takes one capsule: toolbelt first, then backpack (2.6 order).</summary>
    private static bool TakeOne(EntityPlayer p, string name, int itemType)
    {
        ItemValue iv = ItemClass.GetItem(name);
        if (iv == null || iv.IsEmpty() || (itemType > 0 && itemType != iv.type)) return false;
        if (TakeFrom(p.inventory.ItemGrid.items, iv.type, i => p.inventory.SetItem(i, p.inventory.ItemGrid.items[i]))) return true;
        return TakeFrom(p.bag.ItemGrid.items, iv.type, i => p.bag.SetSlot(i, p.bag.ItemGrid.items[i]));
    }

    private static bool TakeFrom(ItemStack[] slots, int type, Action<int> commit)
    {
        if (slots == null) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            ItemStack s = slots[i];
            if (s == null || s.IsEmpty() || s.itemValue.type != type) continue;
            s.count--;
            if (s.count <= 0) slots[i] = ItemStack.Empty.Clone();
            commit(i);
            return true;
        }
        return false;
    }

    private static void ChargeEnergy(EntityPlayer p, float amount)
    {
        try
        {
            RebirthMetabolismState m = RebirthMetabolismStateRepository.GetOrCreate(p);
            if (m == null || amount <= 0f) return;
            m.Energy = Mathf.Max(0f, m.Energy - amount);
            m.Touch();
            RebirthMetabolismStateRepository.SaveIfDirty("rage-activation");
        }
        catch (Exception e) { Log.Warning("[REBIRTH Rage] energy charge failed: " + e.Message); }
    }

    // ------------------------------------------------------------------ server tick: Blood Moon Rage

    private static void OnUpdate(ref ModEvents.SGameUpdateData data)
    {
        World world = GameManager.Instance?.World;
        if (world == null) return;
        if (!GameManager.IsDedicatedServer)
        {
            PollKeys(world);
            EntityPlayerLocal local = world.GetPrimaryPlayer();
            if (local != null && local.Buffs != null && Time.time >= nextOwnerTick)
            {
                nextOwnerTick = Time.time + 0.25f;
                foreach (string b in RebirthRageBuffs.Rages) TickTimed(local, b);
                foreach (string b in RebirthRageBuffs.Cooldowns) TickTimed(local, b);
            }
        }
        if (world.IsRemote() || Time.time < nextServerTick) return;
        nextServerTick = Time.time + 1f;
        bool bloodMoon = IsBloodMoon();
        List<EntityPlayer> players = world.Players?.list;
        if (players == null) return;
        for (int i = 0; i < players.Count; i++) TickBloodMoon(players[i], bloodMoon);
    }

    /// <summary>2.6 BloodMoonRageBuffSet and its checks: on a Blood Moon a qualifying Berserker carries the Blood Moon Rage
    /// of their rank; it ends when the Blood Moon ends or they no longer qualify, and follows rank changes.</summary>
    private static void TickBloodMoon(EntityPlayer p, bool bloodMoon)
    {
        if (p == null || p.Buffs == null || p.IsDead()) return;
        bool want = bloodMoon && IsBerserker(p) && RageSkill(p) >= BloodSkill && HasKnowledge(p, RebirthSurvivorIds.KnowledgeRageBlood);
        bool has = p.Buffs.HasBuff(RebirthRageBuffs.Blood);
        if (!want) { if (has) p.Buffs.RemoveBuff(RebirthRageBuffs.Blood); return; }
        int tier = Tier(p);
        if (!has)
        {
            Start(p, RebirthRageBuffs.Blood, RebirthRageBuffs.BloodPrefix, tier, RebirthRageBuffs.BloodStats(tier), 0f);
            RebirthRageService.NoteActivation(p);
        }
        else if ((int)p.Buffs.GetCustomVar(RebirthRageBuffs.BloodPrefix + RebirthRageBuffs.Rank) != tier)
            RebirthRageBuffs.WriteStats(p, RebirthRageBuffs.BloodPrefix, tier, RebirthRageBuffs.BloodStats(tier));
    }

    // ------------------------------------------------------------------ Death Rage

    /// <summary>2.6: a fatal hit on a living Berserker outside the Blood Moon, with no Death Rage cooldown, leaves them at
    /// 50 health and starts Death Rage instead.</summary>
    public static bool TryDeathRage(EntityPlayer p, ref DamageResponse response)
    {
        if (p == null || !p.IsAlive() || p.Buffs == null || IsBloodMoon()) return false;
        if (!(response.Fatal || response.Strength >= p.Health)) return false;
        if (p.Buffs.HasBuff(RebirthRageBuffs.Death) || p.Buffs.HasBuff(RebirthRageBuffs.DeathCooldown) || !IsBerserker(p)) return false;
        response.Fatal = false;
        response.Strength = 0;
        response.Stun = EnumEntityStunType.None;
        p.Health = (int)DeathRageHealth;
        if (!p.isEntityRemote) { p.Buffs.AddBuff(RebirthRageBuffs.Death, p.entityId); RebirthRageService.NoteActivation(p); }
        return true;
    }
}

[HarmonyPatch(typeof(EntityAlive), nameof(EntityAlive.ProcessDamageResponseLocal))]
internal static class RebirthDeathRagePatch
{
    private static void Prefix(EntityAlive __instance, ref DamageResponse _dmResponse)
    {
        if (__instance is EntityPlayer p) RebirthBerserkerRage.TryDeathRage(p, ref _dmResponse);
    }
}

[Preserve]
public sealed class RebirthBerserkerRageModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        var harmony = new Harmony("rebirth.berserker-rage");
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthRageBuffs.RegisterPatch));
        RebirthHarmonyBootstrap.PatchClassOnce(harmony, typeof(RebirthDeathRagePatch));
        RebirthBerserkerRage.Install();
    }
}

/// <summary>Testing: runs the same path as Page Down / Page Up, plus status and rage-buff clearing.</summary>
[Preserve]
public sealed class ConsoleCmdRebirthRage : ConsoleCmdAbstract
{
    public override bool IsExecuteOnClient => true;
    public override int DefaultPermissionLevel => 0;
    public override string[] getCommands() => new[] { "rbrage" };
    public override string getDescription() => "Berserker rage: press Page Down/Up, show status, or clear rage buffs.";
    public override string getHelp() => "rbrage pagedown | pageup | other | status | clear";

    private static string SkillText(EntityPlayer p, string id)
        => RebirthServiceCraftSkillService.TryGetPracticalSkillProgress(p, id, out float v, out float pr) ? id + "=" + (v + pr).ToString("0.0000") : id + "=?";

    public override void Execute(List<string> args, CommandSenderInfo sender)
    {
        EntityPlayerLocal p = GameManager.Instance?.World?.GetPrimaryPlayer();
        var con = SingletonMonoBehaviour<SdtdConsole>.Instance;
        if (p == null) { con.Output("No local player."); return; }
        string mode = args.Count > 0 ? args[0].ToLowerInvariant() : "status";
        if (mode == "pagedown") RebirthBerserkerRage.Request(p, RebirthBerserkerRage.IsBerserker(p) ? RebirthBerserkerRage.Kind.Defensive : RebirthBerserkerRage.Kind.Other);
        else if (mode == "pageup") RebirthBerserkerRage.Request(p, RebirthBerserkerRage.Kind.Offensive);
        else if (mode == "other") RebirthBerserkerRage.Request(p, RebirthBerserkerRage.Kind.Other);
        else if (mode == "clear")
        {
            int n = 0;
            foreach (string b in RebirthRageBuffs.Rages) if (p.Buffs.HasBuff(b)) { p.Buffs.RemoveBuff(b); n++; }
            foreach (string b in RebirthRageBuffs.Cooldowns) if (p.Buffs.HasBuff(b)) { p.Buffs.RemoveBuff(b); n++; }
            con.Output("Removed " + n + " rage buffs.");
            return;
        }
        float skill = RebirthBerserkerRage.RageSkill(p, out float progress);
        string kind; int tier; bool active = RebirthBerserkerRage.TryGetActive(p, out kind, out tier);
        var buffs = new List<string>();
        foreach (BuffValue b in p.Buffs.ActiveBuffs)
        {
            string n = b?.BuffClass?.Name;
            if (n != null && n.StartsWith("rebirthrage")) buffs.Add(n + (b.BuffClass.DurationMax > 0 ? "(" + (b.BuffClass.DurationMax - b.DurationInSeconds).ToString("0") + "s left)" : "(" + b.DurationInSeconds.ToString("0") + "s in)"));
        }
        con.Output("berserker=" + RebirthBerserkerRage.IsBerserker(p) + " skill=" + skill.ToString("0") + " progress=" + progress.ToString("0.0000")
            + " rank=" + RebirthBerserkerRage.Tier(p) + " active=" + (active ? kind + tier : "none") + " bloodMoon=" + RebirthBerserkerRage.IsBloodMoon()
            + " capsules=" + RebirthBerserkerRage.CountItem(p, RebirthBerserkerRage.CapsuleItem) + " other=" + RebirthBerserkerRage.CountItem(p, RebirthBerserkerRage.CapsuleOtherItem)
            + " " + SkillText(p, RebirthSurvivorIds.SkillBlackMagic) + " " + SkillText(p, "skill.clubs")
            + " health=" + p.Health + " registered=" + RebirthRageBuffs.Registered + " buffs=[" + string.Join(", ", buffs) + "]");
    }
}
