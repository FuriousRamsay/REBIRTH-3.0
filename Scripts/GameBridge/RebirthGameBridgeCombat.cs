using System.Collections;
using System.Collections.Generic;
using InControl;
using Newtonsoft.Json.Linq;

#nullable disable

/// <summary>
/// Real-time behaviours that cannot be driven call-by-call from outside (a bridge round trip is far
/// slower than a zombie). They run every frame inside the game and use the same virtual input as a
/// player: face the target, walk or run, normal or power attacks, reload.
///
///  /fight  fights until the area is clear (or one target with single=1), switching to whichever enemy
///          is actually on the player.
///  /flee   runs straight away from an enemy.
///  guard   an always-on reflex (toggle with /guard). When an awake enemy comes close and no other
///          fight or flee is running, it interrupts walking, closes open windows, readies the best
///          weapon in the toolbelt and fights back, or flees when unarmed or badly hurt.
///  /give, /spawn  test-setup helpers.
/// </summary>
public static class RebirthGameBridgeCombat
{
    // Fight/flee routines currently running (bridge- or guard-started); the guard waits while > 0.
    private static int activeRoutines;
    // Bumped to cancel whatever fight/flee is running (a new /fight supersedes the previous one).
    private static int combatGeneration;

    public static bool GuardEnabled = true, InstinctHeal = true, InstinctReload = true;
    private static float nextHealAttempt, nextReloadAttempt, nextNoHealLog;
    public static float GuardDistance = 6f;
    private static float nextGuardCheck;
    private static readonly List<string> guardLog = new List<string>();

    public static bool TryDispatch(BridgeRequest req)
    {
        switch (req.Path)
        {
            case "/fight": Fight(req); return true;
            case "/flee": Flee(req); return true;
            case "/guard": Guard(req); return true;
            case "/give": Give(req); return true;
            case "/spawn": Spawn(req); return true;
            case "/restore": Restore(req); return true;
            case "/surroundings": Surroundings(req); return true;
            case "/cleararea": ClearArea(req); return true;
            case "/godmode": GodMode(req); return true;
            case "/aggro": Aggro(req); return true;
            case "/mount": Mount(req); return true;
            case "/dismount": Dismount(req); return true;
            case "/vehicle": VehicleInfo(req); return true;
            case "/fly": Fly(req); return true;
            case "/drive": Drive(req); return true;
            case "/terrain": Terrain(req); return true;
            case "/openground": OpenGround(req); return true;
            case "/use": UseItemEndpoint(req); return true;
            case "/zombiespeed": ZombieSpeed(req); return true;
            case "/enemyspawns": EnemySpawns(req); return true;
            case "/lootbags": LootBagsEndpoint(req); return true;
            case "/loadout": RebirthGameBridgeSkills.Loadout(req); return true;
            case "/skillsetup": RebirthGameBridgeSkills.SetupSkill(req); return true;
            case "/setblock": RebirthGameBridgeBlocks.SetBlock(req); return true;
            case "/testflag": RebirthGameBridgeFlags.Set(req); return true;
            case "/craft": RebirthGameBridgeCraft.Craft(req); return true;
            case "/pillar": Pillar(req); return true;
            case "/treat": Treat(req); return true;
            default: return false;
        }
    }

    private static EntityPlayerLocal PrimaryPlayer()
    {
        return GameManager.Instance != null && GameManager.Instance.World != null ? GameManager.Instance.World.GetPrimaryPlayer() : null;
    }

    public static EntityPlayerLocal PlayerFor(BridgeRequest req) { return Player(req); }
    public static bool IsMeleeWeaponPublic(ItemClass ic) { return IsMeleeWeapon(ic); }
    public static bool IsRangedPublic(ItemClass ic) { return IsRanged(ic); }
    private static EntityPlayerLocal Player(BridgeRequest req)
    {
        EntityPlayerLocal p = PrimaryPlayer();
        if (p == null) req.Fail("no local player (state=" + RebirthGameBridgeHandlers.DescribeGameState() + ")", 409);
        return p;
    }

    private static bool IsThreat(EntityAlive a, EntityPlayerLocal p)
    {
        return a != null && a != p && !IsDeadOrDying(a) && a is EntityEnemy && !a.IsSleeping && !IsControlledAlly(a, p);
    }

    private static bool IsControlledAlly(EntityAlive a, EntityPlayerLocal p)
    {
        return a != null && p != null && RebirthBlackMagicService.ShouldBlockControlledAttack(a, p);
    }

    /// <summary>Nearest awake enemy within radius (optionally class-name filtered), excluding `except`.</summary>
    private static EntityAlive NearestEnemy(EntityPlayerLocal p, float radius, string contains = null, EntityAlive except = null)
    {
        EntityAlive best = null;
        float bestD = radius * radius;
        foreach (Entity e in p.world.Entities.list)
        {
            var a = e as EntityAlive;
            if (!IsThreat(a, p) || a == except) continue;
            if (contains != null && (a.EntityClass == null || a.EntityClass.entityClassName.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0)) continue;
            float d = (a.position - p.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = a; }
        }
        return best;
    }

    private static EntityAlive ResolveEnemy(BridgeRequest req, EntityPlayerLocal p)
    {
        int id;
        if (int.TryParse(req.QueryString("entity"), out id))
        {
            var target = p.world.GetEntity(id) as EntityAlive;
            return IsControlledAlly(target, p) ? null : target;
        }
        return NearestEnemy(p, req.QueryFloat("radius", 30f), req.QueryString("contains"));
    }

    private static float Stamina(EntityPlayerLocal p) { return p.Stats != null ? p.Stats.Stamina.Value : 100f; }

    private static float StaminaFraction(EntityPlayerLocal p)
    {
        float max = p.Stats != null && p.Stats.Stamina.ModifiedMax > 0f ? p.Stats.Stamina.ModifiedMax : 100f;
        return Stamina(p) / max;
    }

    /// <summary>Awake enemies within radius, nearest first.</summary>
    private static List<EntityAlive> ThreatsSorted(EntityPlayerLocal p, float radius)
    {
        var list = new List<EntityAlive>();
        float r2 = radius * radius;
        foreach (Entity e in p.world.Entities.list)
        {
            var a = e as EntityAlive;
            if (!IsThreat(a, p)) continue;
            if ((a.position - p.position).sqrMagnitude <= r2) list.Add(a);
        }
        list.Sort((x, y) => (x.position - p.position).sqrMagnitude.CompareTo((y.position - p.position).sqrMagnitude));
        return list;
    }

    private static float Flat(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return (a - b).magnitude; }

    private static string ClassName(EntityAlive e) { return e != null && e.EntityClass != null ? e.EntityClass.entityClassName : "?"; }

    // ------------------------------------------------------------------ weapons

    private static bool IsRanged(ItemClass ic) { return ic != null && ic.Actions != null && ic.Actions.Length > 0 && ic.Actions[0] is ItemActionRanged; }

    private static bool IsMeleeWeapon(ItemClass ic)
    {
        if (ic == null || ic.Actions == null || ic.Actions.Length == 0 || ic.Actions[0] == null) return false;
        if (ic.GetItemName().IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0 && ic.GetItemName().StartsWith("melee", StringComparison.OrdinalIgnoreCase)) return false; // bare hands
        return ic.Actions[0] is ItemActionMelee || ic.Actions[0] is ItemActionDynamicMelee;
    }

    /// <summary>A bow (drawn by holding the attack button, fires on release).</summary>
    public static bool IsBow(ItemClass ic) { return ic != null && ic.Actions != null && ic.Actions.Length > 0 && ic.Actions[0] is ItemActionCatapult; }

    private static int ArrowCount(EntityPlayerLocal p)
    {
        int n = 0;
        foreach (ItemStack s in p.inventory.ItemGrid.items)
            if (s != null && !s.IsEmpty() && s.itemValue.ItemClass != null && s.itemValue.ItemClass.GetItemName().StartsWith("ammoArrow", StringComparison.Ordinal)) n += s.count;
        foreach (ItemStack s in p.bag.ItemGrid.items)
            if (s != null && !s.IsEmpty() && s.itemValue.ItemClass != null && s.itemValue.ItemClass.GetItemName().StartsWith("ammoArrow", StringComparison.Ordinal)) n += s.count;
        return n;
    }

    /// <summary>Toolbelt slot of a bow we can shoot (arrows on hand), or -1.</summary>
    private static int BowSlot(EntityPlayerLocal p)
    {
        if (ArrowCount(p) <= 0) return -1;
        ItemStack[] slots = p.inventory.ItemGrid.items;
        for (int i = 0; i < slots.Length && i < RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.Length); i++)
            if (slots[i] != null && !slots[i].IsEmpty() && IsBow(slots[i].itemValue.ItemClass)) return i;
        return -1;
    }

    public static bool HasBow(EntityPlayerLocal p) { return BowSlot(p) >= 0; }

    private static int AmmoOnHand(EntityPlayerLocal p, string itemName)
    {
        ItemValue iv = ItemClass.GetItem(itemName, true);
        if (iv == null || iv.IsEmpty()) return 0;
        return p.bag.GetItemCount(iv) + p.inventory.GetItemCount(iv);
    }

    /// <summary>
    /// A ranged weapon fires its SELECTED ammo type (default: the first in its list, stone arrows for a bow); with only iron arrows on hand the
    /// ammo counter reads 0/0 and nothing loads. A player holds R for the radial menu and picks the arrows; this does the same through the
    /// game's own SetAmmoType: the best arrows we actually carry (iron first), and the weapon reloads with them. Returns true if the selected
    /// type has ammo on hand afterwards.
    /// </summary>
    public static bool SelectBestArrows(EntityPlayerLocal p)
    {
        try
        {
            ItemValue gun = p.inventory.holdingItemItemValue;
            ItemActionRanged action = gun != null && gun.ItemClass != null && gun.ItemClass.Actions != null && gun.ItemClass.Actions.Length > 0 ? gun.ItemClass.Actions[0] as ItemActionRanged : null;
            if (action == null || action.MagazineItemNames == null) return false;
            string[] names = action.MagazineItemNames;
            string[] preference = { "ammoArrowIron", "ammoArrowSteelAP", "ammoArrowStone", "ammoArrowFlaming", "ammoArrowExploding" };
            int cur = gun.SelectedAmmoTypeIndex, best = -1;
            foreach (string want in preference)
            {
                int idx = Array.IndexOf(names, want);
                if (idx >= 0 && AmmoOnHand(p, want) > 0) { best = idx; break; }
            }
            if (best < 0) for (int i = 0; i < names.Length; i++) if (AmmoOnHand(p, names[i]) > 0) { best = i; break; }
            if (best < 0) return false;
            if (best != cur) { action.SetAmmoType(p, ref gun, cur, best); GuardLog("bow: switched ammo to " + names[best]); }
            return AmmoOnHand(p, names[best]) > 0 || gun.Meta > 0;
        }
        catch (Exception ex) { GuardLog("bow: could not select arrows: " + ex.Message); return false; }
    }

    /// <summary>Toolbelt slot of the best melee weapon, or -1.</summary>
    private static int MeleeSlot(EntityPlayerLocal p)
    {
        ItemStack[] slots = p.inventory.ItemGrid.items;
        for (int i = 0; i < slots.Length && i < RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.Length); i++)
            if (slots[i] != null && !slots[i].IsEmpty() && IsMeleeWeapon(slots[i].itemValue.ItemClass)) return i;
        return -1;
    }

    /// <summary>Best toolbelt slot to fight with (0-based) or -1: loaded gun, then melee weapon, then any gun.</summary>
    private static int BestWeaponSlot(EntityPlayerLocal p)
    {
        int melee = -1, emptyGun = -1;
        ItemStack[] slots = p.inventory.ItemGrid.items;
        for (int i = 0; i < slots.Length && i < RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.Length); i++)
        {
            ItemStack s = slots[i];
            if (s == null || s.IsEmpty()) continue;
            ItemClass ic = s.itemValue.ItemClass;
            if (IsRanged(ic)) { if (s.itemValue.Meta > 0) return i; if (emptyGun < 0) emptyGun = i; }
            else if (melee < 0 && IsMeleeWeapon(ic)) melee = i;
        }
        return melee >= 0 ? melee : emptyGun;
    }

    // ------------------------------------------------------------------ healing

    // Best first. Bandages/kits heal over time through buffs, so health keeps rising after use.
    private static readonly string[] HealItems = { "medicalFirstAidKit", "medicalFirstAidBandage", "medicalBandage" };

    /// <summary>
    /// Would the game let us use this healing item right now? The items have conditions: a plain bandage only works while
    /// bleeding, first aid items need missing health, and bandaging heals over time from a pool (cvar medicalRegHealthAmount,
    /// "healing left"): with enough already pending, using another only wastes it - and the game refuses ("can't do that now").
    /// </summary>
    /// <summary>Bleeding drains health and lowers the maximum until stopped: any bandage (plain or first aid) stops it at once.</summary>
    public static bool IsBleeding(EntityPlayerLocal p)
    {
        try { return p.Buffs.HasBuff("buffInjuryBleeding") || p.GetCVar("lacerationBleedingStatus") >= 2f; }
        catch { return false; }
    }

    private static readonly string[] BleedingItems = { "medicalBandage", "medicalFirstAidBandage", "medicalFirstAidKit" };

    public static bool HealItemUsable(EntityPlayerLocal p, string name)
    {
        if (p == null || p.Stats == null) return false;
        float max = p.Stats.Health.ModifiedMax, hp = p.Health;
        float pending = Mathf.Max(0f, p.GetCVar("medicalRegHealthAmount"));
        bool bleeding = p.GetCVar("lacerationBleedingStatus") >= 2f;
        float stillMissing = max - hp - pending;
        switch (name)
        {
            case "medicalBandage": return bleeding;
            case "medicalFirstAidBandage": return hp < max && (stillMissing >= 12f || bleeding);
            case "medicalFirstAidKit": return hp < max && stillMissing >= 25f;
            default: return hp < max && stillMissing >= 12f;
        }
    }

    /// <summary>True when any healing item would currently do something useful (and be accepted).</summary>
    public static bool CanHealNow(EntityPlayerLocal p)
    {
        foreach (string name in HealItems) if (HealItemUsable(p, name)) return true;
        return false;
    }

    /// <summary>Toolbelt slot (0-based) of the best healing item, or -1. With onlyUsable, only items the game would accept now.</summary>
    private static int HealSlot(EntityPlayerLocal p, bool onlyUsable = false)
    {
        ItemStack[] slots = p.inventory.ItemGrid.items;
        foreach (string name in (IsBleeding(p) ? BleedingItems : HealItems))
        {
            if (onlyUsable && !HealItemUsable(p, name)) continue;
            for (int i = 0; i < slots.Length && i < RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.Length); i++)
            {
                ItemStack s = slots[i];
                if (s != null && !s.IsEmpty() && s.itemValue.ItemClass != null && s.itemValue.ItemClass.GetItemName() == name) return i;
            }
        }
        return -1;
    }

    private static bool BagHasHealItem(EntityPlayerLocal p)
    {
        foreach (ItemStack s in p.bag.ItemGrid.items)
            if (s != null && !s.IsEmpty() && s.itemValue.ItemClass != null && Array.IndexOf(HealItems, s.itemValue.ItemClass.GetItemName()) >= 0) return true;
        return false;
    }

    private static IEnumerator Wait(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    private static IEnumerator SelectSlot(EntityPlayerLocal p, int slot)
    {
        if (slot < 0 || slot == p.inventory.holdingItemIdx) yield break;
        PlayerAction key = RebirthGameBridgeInput.Find("InventorySlot" + (slot + 1));
        if (key != null) RebirthGameBridgeInput.HoldFrames(key, 3);
        yield return Wait(0.6f); // equip animation
    }

    /// <summary>
    /// Heal like a player: switch to the healing item, use it, switch back to what was in hand.
    /// Logs the outcome (or that no healing item is in the toolbelt).
    /// </summary>
    private static IEnumerator HealRoutine(EntityPlayerLocal p, Action<string> log)
    {
        int slot = HealSlot(p, true);
        if (slot < 0)
        {
            if (HealSlot(p) >= 0) log("healing items in hand's reach, but the game would refuse them now (health " + p.Health + ", healing left " + p.GetCVar("medicalRegHealthAmount").ToString("0") + ", bleeding " + p.GetCVar("lacerationBleedingStatus").ToString("0") + ") - not wasting one");
            else log(BagHasHealItem(p) ? "hurt: healing items are in the backpack but not the toolbelt" : "hurt: no healing item");
            yield break;
        }
        int previous = p.inventory.holdingItemIdx;
        string item = p.inventory.GetItem(slot).itemValue.ItemClass.GetItemName();
        int hp0 = p.Health;

        // One action at a time, like a player: really get it in hand (equip animation done), then use it
        // once and let the bandaging animation finish before doing anything else.
        bool equipped = false, consumed = false;
        yield return RebirthGameBridgeNeeds.Equip(p, slot, item, ok => equipped = ok);
        if (!equipped)
        {
            log("couldn't get " + item + " in hand (holding " + (p.inventory.holdingItem != null ? p.inventory.holdingItem.GetItemName() : "nothing") + ")");
            healFailedUntil = Time.realtimeSinceStartup + 15f;
            yield break;
        }
        yield return RebirthGameBridgeNeeds.UseHeld(p, item, ok => consumed = ok);
        if (consumed) log("used " + item + ": health " + hp0 + "->" + p.Health + " (heals over time)");
        else { log("tried to use " + item + " but it was not consumed"); healFailedUntil = Time.realtimeSinceStartup + 15f; }
        if (previous != slot && previous >= 0) yield return RebirthGameBridgeNeeds.Equip(p, previous, null, ok => { });
    }

    private static float healFailedUntil, nextPrepareAt;
    public static bool InstinctSupply = true; // test mode: add a bandage to the backpack when there is none

    private static int CountItem(EntityPlayerLocal p, string name)
    {
        int n = 0;
        foreach (ItemStack s in p.inventory.ItemGrid.items)
            if (s != null && !s.IsEmpty() && s.itemValue.ItemClass != null && s.itemValue.ItemClass.GetItemName() == name) n += s.count;
        foreach (ItemStack s in p.bag.ItemGrid.items)
            if (s != null && !s.IsEmpty() && s.itemValue.ItemClass != null && s.itemValue.ItemClass.GetItemName() == name) n += s.count;
        return n;
    }

    /// <summary>Toolbelt slot that can be emptied to make room: not a weapon, not healing, not in hand.</summary>
    private static int FreeableToolbeltSlot(EntityPlayerLocal p)
    {
        ItemStack[] slots = p.inventory.ItemGrid.items;
        for (int i = 0; i < slots.Length && i < RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.Length); i++)
        {
            ItemStack s = slots[i];
            if (s == null || s.IsEmpty() || i == p.inventory.holdingItemIdx) continue;
            ItemClass ic = s.itemValue.ItemClass;
            if (IsRanged(ic) || IsMeleeWeapon(ic) || Array.IndexOf(HealItems, ic.GetItemName()) >= 0) continue;
            return i;
        }
        return -1;
    }

    // Largest magazine seen per gun type, so "partly empty" can be detected without the action data.
    private static readonly Dictionary<int, int> magazineSeen = new Dictionary<int, int>();

    private static void NoteMagazine(ItemValue gun)
    {
        int max;
        if (!magazineSeen.TryGetValue(gun.type, out max) || gun.Meta > max) magazineSeen[gun.type] = gun.Meta;
    }

    // ------------------------------------------------------------------ fight

    private sealed class FightOptions
    {
        public float Range = 2.3f, RetreatStamina = 0.33f, ResumeStamina = 0.75f, FleeHealth = 25f, MaxSeconds = 120f,
            SwingInterval = 0.75f, GunRange = 25f, FireInterval = 0.4f, Radius = 20f;
        public bool Power, AimBody, AimHead, Single;
        public float HoldSwing = 0f;   // seconds to keep the attack button down per swing; 0 = a 3-frame tap. Releasing ends the attack at once (ItemActionDynamicMelee.ExecuteAction released -> SetAttackFinished), cutting off grazing sweeps and anything else that needs the attack still running
        public bool Spacing = false;                           // human style: hold a fixed distance by walking backwards at the walker's pace; the bat out-reaches the zombie
        public float SpacingNear = 1.58f, SpacingFar = 1.85f;   // back off inside the near value, wait beyond the far one
        public bool Stagger = true;                            // the game option "always stagger" is on: a landed hit interrupts the zombie, so no stepping away after a swing (except from runners)
        public bool HumanStyle = false;                        // fight like the recorded player: attack the moment it is in reach, no dodge dance; retreat decisively (turn and go) only for stamina or when it is too close
        public bool AutoTower = true;                         // a tough zombie (max health >= 400) while it is still far: pillar up, shoot from the top (measured: a 683 hp biker dies in ~80 s for 15 hp)
        public bool PowerMix = false;                          // mix in power attacks (double damage, 35.6 stamina) when a hit is certain and stamina allows; never on a downed zombie
        public bool TrueOrigin;                               // measure the swing distance from the real melee-ray origin
        public float StillMax = 0f;                         // press a swing only once we have settled (m/s): drifting backwards during the 0.4 s wind-up puts the hit frame 0.3-0.5 m out of reach
        public bool DanceAuto;                               // choose per target: the dance (back out of its swing, strike in its recovery) against tough zombies, attack-first against ordinary ones
        public bool Dance;                                   // false: attack-first (best measured so far); true: back out of its swing, strike in its recovery
        public float ReachMin = 1.45f, ReachMax = 2.15f, HoldAfterHit = 0.25f, StepBackAfter = 0.3f, AimDamp = 0.05f;   // melee sweet spot: predicted 3D distance (eye to chest) at the hit frame; measured hit rate 8% under 1 m, 67% beyond 1.6 m
        public float Lead = 0f, AimSmooth = 0.06f;   // Lead 0 = measured from the stamina dip (the real press-to-hit delay)   // melee: seconds between click and impact; camera smoothing time
        public float BowDrop = 1.0f;   // fraction of free-fall drop to aim above for arrows (calibrated with combat_eval)
        public string Source = "bridge";
    }

    private static void Fight(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        EntityAlive target = ResolveEnemy(req, p);
        if (target == null) { req.Fail("no enemy found (give ?entity=ID or increase ?radius)", 404); return; }
        string aim = req.QueryString("aim", "auto").ToLowerInvariant();
        var o = new FightOptions
        {
            Range = req.QueryFloat("range", 2.3f),
            Power = req.QueryBool("power", false),
            RetreatStamina = req.QueryFloat("retreatStamina", 0.33f), // fraction of max stamina
            ResumeStamina = req.QueryFloat("resumeStamina", 0.75f),
            FleeHealth = req.QueryFloat("fleeHealth", 25f),
            MaxSeconds = req.QueryFloat("maxSeconds", 120f),
            SwingInterval = req.QueryFloat("swingInterval", 0.75f),
            GunRange = req.QueryFloat("gunRange", 25f),
            FireInterval = req.QueryFloat("fireInterval", 0.4f),
            Radius = req.QueryFloat("radius", 20f),
            AimBody = aim == "body",
            AimHead = aim == "head",
            Single = req.QueryBool("single", false),
            BowDrop = req.QueryFloat("bowDrop", 1.0f),
            Lead = req.QueryFloat("lead", 0f),
            Stagger = req.QueryBool("stagger", true), Dance = req.QueryBool("dance", false), DanceAuto = req.QueryString("dance") == null, StillMax = req.QueryFloat("stillMax", 0f), TrueOrigin = req.QueryBool("trueOrigin", false), PowerMix = req.QueryBool("powerMix", req.QueryBool("humanStyle", false)), AutoTower = req.QueryBool("autoTower", true), HumanStyle = req.QueryBool("humanStyle", false), Spacing = req.QueryBool("spacing", false), SpacingNear = req.QueryFloat("spacingNear", 1.58f), SpacingFar = req.QueryFloat("spacingFar", 1.85f),
            HoldSwing = req.QueryFloat("holdSwing", 0f),
            ReachMin = req.QueryFloat("reachMin", req.QueryBool("humanStyle", false) ? 0.9f : 1.45f), ReachMax = req.QueryFloat("reachMax", req.QueryBool("humanStyle", false) ? 2.0f : 2.15f),
            HoldAfterHit = req.QueryFloat("holdAfterHit", req.QueryBool("humanStyle", false) ? 0.04f : 0.25f), StepBackAfter = req.QueryFloat("stepBackAfter", req.QueryBool("humanStyle", false) ? 0.45f : 0.3f), AimDamp = req.QueryFloat("aimDamp", 0.05f),
            AimSmooth = req.QueryFloat("aimSmooth", 0.06f)
        };
        RebirthGameBridgePump.Instance.Run(FightRoutine(req, p, target, o, ++combatGeneration));
    }

    /// <summary>Fight one specific enemy to the end (used by the POI raid); reports the fight result.</summary>
    public static IEnumerator FightOne(EntityPlayerLocal p, EntityAlive target, Action<string> done)
    {
        var dummy = new BridgeRequest { Method = "POST", Path = "/fight" };
        yield return FightRoutine(dummy, p, target, new FightOptions { Single = true, Source = "raid" }, ++combatGeneration);
        string r = "unknown";
        try { r = (string)JObject.Parse(dummy.ResponseBody)["result"]; } catch { }
        done(r);
    }

    private static int BowHits(List<float[]> log) { int n = 0; foreach (float[] s in log) if (s[6] > 0.5f) n++; return n; }

    /// <summary>One compact line per arrow loosed: seconds since the first, distance, aim error, crosshair on target, loaded rounds, ammo index, hit, aim lift (m).</summary>
    private static JArray BowDetail(List<float[]> log)
    {
        var arr = new JArray();
        float t0 = log.Count > 0 ? log[0][0] : 0f;
        foreach (float[] s in log)
            arr.Add(string.Join(",", new[] { (s[0] - t0).ToString("0.0"), s[1].ToString("0.0"), s[2].ToString("0.0"), s[3].ToString("0"), s[4].ToString("0"), s[5].ToString("0"), s[6].ToString("0"), s[7].ToString("0.00") }));
        return arr;
    }

    /// <summary>One compact line per swing: time since the first, onTarget, blind, aimErr, dist, head, hit, zombieSwinging, speed, stamina, walkType.</summary>
    private static JArray SwingDetail(List<float[]> log)
    {
        var arr = new JArray();
        float t0 = log.Count > 0 ? log[0][0] : 0f;
        foreach (float[] s in log)
            arr.Add(string.Join(",", new[] { (s[0] - t0).ToString("0.00"), s[1].ToString("0"), s[2].ToString("0"), s[3].ToString("0.0"), s[4].ToString("0.00"), s[5].ToString("0"), s[6].ToString("0"), s[7].ToString("0"), s[8].ToString("0.0"), s[9].ToString("0"), s[10].ToString("0"), s[11].ToString("0.00"), s[12].ToString("0"), s[13].ToString("0.00"), s.Length > 15 ? s[14].ToString("0.00") : "0", s.Length > 15 ? s[15].ToString("0.00") : "0" }));
        return arr;
    }

    /// <summary>Hit rate split by how the crosshair stood when the swing started (on the target, off it, blind, aiming at the head or chest).</summary>
    private static JObject SwingAnalysis(List<float[]> log)
    {
        int n = 0, onN = 0, onHit = 0, offN = 0, offHit = 0, blindN = 0, blindHit = 0, headN = 0, headHit = 0, bodyN = 0, bodyHit = 0;
        float distSum = 0f;
        foreach (float[] s in log)
        {
            n++; distSum += s[4];
            bool hit = s[6] > 0.5f;
            if (s[2] > 0.5f) { blindN++; if (hit) blindHit++; }
            else if (s[1] > 0.5f) { onN++; if (hit) onHit++; }
            else { offN++; if (hit) offHit++; }
            if (s[5] > 0.5f) { headN++; if (hit) headHit++; } else { bodyN++; if (hit) bodyHit++; }
        }
        return new JObject
        {
            ["swings"] = n, ["avgDistance"] = n > 0 ? Math.Round(distSum / n, 2) : 0,
            ["onTarget"] = onN + "/" + onHit, ["offTarget"] = offN + "/" + offHit, ["blind"] = blindN + "/" + blindHit,
            ["aimedHead"] = headN + "/" + headHit, ["aimedChest"] = bodyN + "/" + bodyHit
        };
    }

    private static int fightsRunning;
    private static float nextBleedReflex;
    private static bool bleedReflexRunning;
    public static bool BleedReflexEnabled = true;

    /// <summary>
    /// BLEEDING REFLEX, built into the game loop (runs every frame, whatever else is going on, independent of the instincts toggle):
    /// the moment the player bleeds and a bandage (plain or first aid) is in the toolbelt, use it. Bleeding drains health and the maximum
    /// every second; waiting for a decision costs real health. A fight loop handles it itself (same rule, same frame), so the reflex only
    /// acts when none is running.
    /// </summary>
    public static void BleedReflexTick()
    {
        if (!BleedReflexEnabled || bleedReflexRunning || fightsRunning > 0 || Time.realtimeSinceStartup < nextBleedReflex) return;
        EntityPlayerLocal p = PrimaryPlayer();
        if (p == null || p.IsDead() || !p.IsSpawned() || !IsBleeding(p) || HealSlot(p, true) < 0) return;
        nextBleedReflex = Time.realtimeSinceStartup + 6f;
        bleedReflexRunning = true;
        GuardLog("BLEEDING - bandaging immediately");
        RebirthGameBridgePump.Instance.Run(BleedReflexRoutine(p));
    }

    private static IEnumerator BleedReflexRoutine(EntityPlayerLocal p)
    {
        activeRoutines++;

        yield return HealRoutine(p, GuardLog);
        activeRoutines--;
        bleedReflexRunning = false;
    }

    private static IEnumerator FightRoutine(BridgeRequest req, EntityPlayerLocal p, EntityAlive target, FightOptions o, int generation)
    {
        activeRoutines++;
        fightsRunning++;
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        float start = Time.realtimeSinceStartup;
        int playerHp0 = p.Health, lastPlayerHp = p.Health, lastTargetHp = target.Health;
        string targetName = ClassName(target);
        float stamina0 = Stamina(p), minStamina = stamina0;
        int swings = 0, hits = 0, hitsTaken = 0, retreats = 0, reloads = 0;
        bool recovering = false, fleeing = false, reloading = false, triedHealThisRetreat = false;
        float reloadStart = 0f, nextSwing = 0f, nextRetarget = 0f, noFleeUntil = 0f;
        var events = new JArray();
        var kills = new JArray();
        var lost = new JArray();
        var incidents = new JArray();
        var trace = new JArray();
        string result = null;

        // --- aim state -------------------------------------------------------------------------------
        // Aim on the target's live skeleton (0 = head bone, 1 = pelvis), so it follows staggering, crawling or
        // knocked-down bodies. Melee: head first (most damage, knockdowns). Guns: centre-mass unless aim=head.
        // The camera follows the aim point with a critically damped spring (SmoothDampAngle): smooth, no
        // snapping, but it converges on a moving target in ~0.1 s instead of lagging behind it.
        GaitTracker gait = new GaitTracker(target.walkType);
        int aimIndex = 0;
        float offTargetSince = -1f, yawVel = 0f, pitchVel = 0f;
        Vector3 aimPoint = Vector3.zero;
        bool aimInit = false;

        // --- bookkeeping for incidents and chase control -----------------------------------------------
        float lastAttackAt = start;
        // --- melee dance state ---
        float dodgeSince = -1f; bool wasSwinging = false, strafeRight = UnityEngine.Random.value < 0.5f;
        float punishUntil = 0f, plantUntil = 0f, stepOutUntil = 0f, nextStrafeFlip = 0f, closingSpeed = 0f, lastDistForClosing = -1f;
        int enemySwings = 0;
        // Per-swing log: was the crosshair on the target when the swing started, and did its health drop within 1.2 s?
        bool curOnTarget = false, curBlind = false, curHead = false, curZSwing = false; float curAim = 0f, curDist = 0f, curSpeed = 0f;
        RebirthGameBridgeMeleeProbe.Take();
        RebirthGameBridgeFightRecorder.Begin("fight_" + ClassName(target));
        int grazes = 0;
        var swingLog = new List<float[]>();   // time, onTarget, blind, aimErr, dist, head, hit
        var bowLog = new List<float[]>();     // time, dist(3D), aimErr, onTarget, loaded(Meta), ammoIndex, hit, aimLift
        // Swing confirmation: a press only starts a swing if the game takes it (not mid-swing, not staggered). A swing costs ~20 stamina,
        // so a stamina dip after the press proves it started; no dip means it was ignored and the press is repeated right away.
        float pendStamina = -1f, pendAt = 0f, pendMin = 0f, cycleEst = 0.9f, lastRegAt = 0f, hitDelay = 0.35f, holdStillUntil = 0f; int swingsRegistered = 0, swingsIgnored = 0, delaySamples = 0;
        Action<PlayerAction, bool> Swing = (atk, finisher) =>
        {
            if (o.HoldSwing > 0.05f) RebirthGameBridgeInput.HoldSeconds(atk, o.HoldSwing);   // keep the button down so the attack runs to completion
            else RebirthGameBridgeInput.HoldFrames(atk, 3);     // one normal swing (a tap)
            swings++;
            RebirthGameBridgeFightRecorder.Event(atk == a.Secondary ? "PRESS power" : "PRESS normal");
            RebirthGameBridgeMeleeProbe.Target = target as EntityAlive;
            pendStamina = Stamina(p); pendAt = Time.realtimeSinceStartup; pendMin = pendStamina;
            holdStillUntil = pendAt + hitDelay + o.HoldAfterHit;   // moving while the swing fires shakes the camera pitch and shifts us: the ray lands above or beside the target
            swingLog.Add(new[] { Time.realtimeSinceStartup, curOnTarget ? 1f : 0f, curBlind ? 1f : 0f, curAim, curDist, curHead ? 1f : 0f, 0f, curZSwing ? 1f : 0f, curSpeed, Stamina(p), (float)target.walkType, -1f, 0f, 0f, target.position.y - p.position.y, new Vector2(target.position.x - p.position.x, target.position.z - p.position.z).magnitude });
            lastAttackAt = Time.realtimeSinceStartup;
            nextSwing = lastAttackAt + o.SwingInterval * (StaminaFraction(p) < 0.3f ? 1.6f : 1f);   // tired: slower rhythm instead of running away
            plantUntil = lastAttackAt + (o.Dance ? 0.08f : (o.HumanStyle ? 0.1f : 0.3f));
            stepOutUntil = o.HumanStyle ? holdStillUntil + (finisher ? 0f : 1.4f)              // human: step back as soon as the hit frame is past, keep going until it cannot reach us
                         : o.Dance ? holdStillUntil + (finisher ? 0f : 0.55f)          // the hit frame has passed: back out of its reach before it answers
                                   : plantUntil + (finisher ? 0f : o.StepBackAfter);
        };

        bool retreatMoving = false, escapeOk = true;
        float nextEscapeProbe = 0f;
        // Pack awareness: who else is coming. Scanned a few times a second.
        List<EntityAlive> pack = new List<EntityAlive>();
        float nextPackScan = 0f, kiteSpent = 0f, lastPackSeen = start, weaponSwapLock = 0f, noLosSince = -1f, lastLockAt = start, blindAt = 0f, lastReloadPress = 0f, headSpeed = 5f, lastStunnedAt = -10f, lastAmmoCheck = 0f, bleedingSince = -1f, nextBleedTreat = 0f, rollSpeed = 0f, hugSince = -1f, playerSpeed = 0f, drawStartAt = -1f, bowDrawStart = 0f, bowCooldownUntil = 0f, tBowOut = -1f, tFirstDraw = -1f, tFirstRelease = -1f, tMeleeSwitch = -1f; bool drawChecked = false, bowDrawing = false, lastZSwing = false, lastBusy = false; int arrowsLoosed = 0;
        string curActionName = "", lastTakenAct = "";
        Vector3 pivotPos = Vector3.zero;
        float frozenYaw = 0f, frozenPitch = 0f, lineUpEnd = 0f, nextLineUpCheck = 0f; bool lineUpMode = false, lineUpAround = false, lineUpMany = false; int lineUps = 0, threatCount = 0; Vector3 lineUpDir = Vector3.forward;   // line-up: run in a straight line so chasers string out behind each other
        int cyc = 0; float cycAt = 0f, waitSince = -1f, backSince = -1f, pivotGoal = 7.5f, pivotStart = 0f;   // the spacing cycle: 0 hold, 1 press+step in, 2 retreat from the hit frame
        float zsCalm = 0f;   // the target's speed measured only while it is not mid-swing (an attack lunge is not running)
        Vector3 targetVelFast = Vector3.zero;   // the target's velocity, lightly smoothed (reacts within ~0.1 s) - to notice the moment a walker starts running
        float pivotUntil = 0f, pivotCool = 0f, lastTakenAt = -10f, nextPowerAt = 0f, recoverCool = 0f; int powerSwings = 0;   // pivot-run: against a fast attacker a backstep is too slow - turn, run a few strides, turn back
        var takenLog = new List<string>();   // every hit we take: time, damage, distance, zombie mid-swing, our swing running, our speed, what we were doing
        Vector3 lastPlayerPos = Vector3.zero;
        Vector3 lastRollPos = Vector3.zero; float bowLift = 0f;
        Vector3 lastHeadPt = Vector3.zero; bool headMode = false;
        Vector3 escapeDir = Vector3.forward;
        int lockFrames = 0;                         // consecutive frames with the crosshair on the target
        Vector3 lastTargetPos = target.position, targetVel = Vector3.zero;
        float lastHitAt = start, closeSince = -1f, nextTrace = 0f;
        float nextIncident = 0f, farSince = -1f, farBestDist = float.MaxValue;
        string lastAction = "";

        Action<string> log = msg => events.Add(string.Format("{0:0.0}s {1}", Time.realtimeSinceStartup - start, msg));
        Action<string> incident = msg =>
        {
            if (Time.realtimeSinceStartup < nextIncident) return;
            nextIncident = Time.realtimeSinceStartup + 3f;
            incidents.Add(string.Format("{0:0.0}s {1}", Time.realtimeSinceStartup - start, msg));
            log("INCIDENT: " + msg);
        };
        Action<EntityAlive, string> setTarget = (t, why) =>
        {
            target = t; targetName = ClassName(t); lastTargetHp = t.Health; aimIndex = 0; offTargetSince = -1f; gait = new GaitTracker(t.walkType);
            farSince = -1f; farBestDist = float.MaxValue;
            log(why + " " + targetName + " #" + t.entityId + " at " + Flat(t.position, p.position).ToString("0.0") + "m");
        };
        log("engaging " + targetName + " #" + target.entityId + " at " + Flat(target.position, p.position).ToString("0.0") + "m");
        // A tough, relentless zombie: ground melee cannot win (stamina). While it is still far away, build a pillar and fight from the top with the bow.
        if (o.AutoTower && !towerActive && p.onGround && Flat(target.position, p.position) > 11f && ((EntityAlive)target).GetMaxHealth() >= 400 && BowSlot(p) >= 0 && ArrowCount(p) >= 12)
        {
            int blockSlot = -1; ItemStack[] bslots = p.inventory.ItemGrid.items;
            for (int bi = 0; bi < bslots.Length && bi < RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.Length); bi++)
                if (bslots[bi] != null && !bslots[bi].IsEmpty() && bslots[bi].itemValue.ItemClass != null && bslots[bi].itemValue.ItemClass.GetItemName() == "frameShapes:cube" && bslots[bi].count >= 6) { blockSlot = bi; break; }
            if (blockSlot >= 0)
            {
                log("tough one (" + ((EntityAlive)target).GetMaxHealth() + " hp) coming - building a pillar to shoot from");
                var pr = new BridgeRequest { Method = "POST", Path = "/pillar" };
                yield return PillarRoutine(pr, p, blockSlot, "frameShapes:cube", 4);
                log("pillar: " + (towerActive ? "up" : "did not work"));
            }
        }

        while (result == null)
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (target != null)
            {
                bool rb = false; try { rb = p.inventory.IsHoldingItemActionRunning(); } catch { }
                RebirthGameBridgeFightRecorder.Row(recovering ? "recover" : fleeing ? "flee" : "fight", curActionName, p, (EntityAlive)target, Flat(target.position, p.position), TargetSwinging((EntityAlive)target), TargetStunned((EntityAlive)target),
                    zsCalm, Stamina(p), rb, a);
            }
            if (generation != combatGeneration) { result = "superseded"; break; }
            // --- line them up (human style, 2+ awake zombies coming from different directions): run away from all of them in a straight line until they trail in a row ---
            if (o.HumanStyle && o.Spacing && !fleeing && !towerActive && now >= nextLineUpCheck)
            {
                nextLineUpCheck = now + 0.15f;
                float sep = 0f; float nearestD = 99f, secondD = 99f;
                try
                {
                    var thr = AwakeThreats(p, 26f, false);
                    threatCount = thr.Count;
                    if (thr.Count >= 2)
                    {
                        thr.Sort((x, y) => Flat(x.position, p.position).CompareTo(Flat(y.position, p.position)));
                        Vector3 b1 = thr[0].position - p.position, b2 = thr[1].position - p.position; b1.y = 0f; b2.y = 0f;
                        nearestD = b1.magnitude; secondD = b2.magnitude;
                        // spread = the widest angle between any two of the nearest three
                        sep = Vector3.Angle(b1, b2);
                        if (thr.Count >= 3) { Vector3 b3 = thr[2].position - p.position; b3.y = 0f; sep = Mathf.Max(sep, Mathf.Max(Vector3.Angle(b1, b3), Vector3.Angle(b2, b3))); secondD = Mathf.Min(secondD, b3.magnitude); }
                        // "around": run to a point 7 m past the nearest one, on the side away from the centre of the others, so they trail it in a line (with 3+ they split the difference)
                        Vector3 others = Vector3.zero; for (int oi = 1; oi < thr.Count && oi < 4; oi++) others += thr[oi].position; others /= Mathf.Min(thr.Count - 1, 3);
                        Vector3 ab = thr[0].position - others; ab.y = 0f;
                        Vector3 w = thr[0].position + ab.normalized * 7f - p.position; w.y = 0f;
                        lineUpDir = w.normalized;
                    }
                }
                catch { }
                if (!lineUpMode && lineUps < 3 && sep > 40f && nearestD > 4.5f)
                {
                    lineUpMode = true; lineUps++; lineUpEnd = now + 7f; pivotStart = now; pivotGoal = 999f; pivotUntil = now + 7f;
                    lineUpMany = threatCount >= 4;                              // a crowd: walk away from all of them and let them bunch into a group behind
                    lineUpAround = !lineUpMany && nearestD > 7f && secondD > 7f;
                    log("two coming from different sides (" + sep.ToString("0") + " deg apart) - " + (lineUpAround ? "circling past one to line them up" : "running away to line them up"));
                }
                else if (lineUpMode && (sep < 22f || now > lineUpEnd || nearestD < 3.0f))
                {
                    lineUpMode = false; pivotUntil = 0f; pivotCool = now + 0.3f;
                    log("lined up (" + sep.ToString("0") + " deg) - turning to fight the first one");
                }
            }
            if (p.IsDead()) { result = "player_died"; break; }
            if (req.IsAbandoned) { result = "abandoned"; break; }
            if (now - start > o.MaxSeconds) { result = fleeing ? "fled" : "timeout"; break; }

            // Domination can change allegiance during a fight. Never count releasing an ally as a kill.
            if (IsControlledAlly(target, p))
            {
                RebirthGameBridgeInput.Release(a.Primary);
                RebirthGameBridgeInput.Release(a.Secondary);
                log("released controlled ally " + targetName);
                EntityAlive next = o.Single ? null : NearestEnemy(p, o.Radius);
                if (next == null) { result = o.Single ? "target_friendly" : "area_clear"; break; }
                setTarget(next, "next hostile target");
            }

            // Target gone: killed (dead) or lost (despawned/unloaded - not a kill). Then next enemy.
            if (target == null || IsDeadOrDying(target))
            {
                if (target != null) { kills.Add(targetName); log("killed " + targetName); RebirthGameBridgeLootBags.Spots.Add(target.position); }
                else { lost.Add(targetName); log("lost track of " + targetName + " (despawned)"); }
                EntityAlive next = o.Single ? null : NearestEnemy(p, o.Radius);
                if (next == null) { result = o.Single ? (kills.Count > 0 ? "killed" : "lost") : "area_clear"; break; }
                setTarget(next, "next target");
            }

            float dist = Flat(target.position, p.position);
            if (lastDistForClosing >= 0f && dt > 0f)
                closingSpeed = Mathf.Lerp(closingSpeed, (lastDistForClosing - dist) / dt, 0.25f);
            lastDistForClosing = dist;

            // Whoever is actually on us takes priority over a target further away.
            if (!o.Single && now >= nextRetarget)
            {
                nextRetarget = now + 0.4f;
                // Anything within 3.5 m that is nearer than the current target gets faced now, including from
                // behind: turn around to face it, never back up into it.
                EntityAlive closest = NearestEnemy(p, 3.5f, null, target);
                if (closest != null && Flat(closest.position, p.position) + 0.5f < dist)
                {
                    setTarget(closest, Mathf.Abs(Bearing(p, closest.position)) > 100f ? "turning around to face" : "switching to closer");
                    dist = Flat(target.position, p.position);
                }
            }

            if (target.Health < lastTargetHp) { for (int bi = bowLog.Count - 1; bi >= 0; bi--) if (now - bowLog[bi][0] < 1.8f && bowLog[bi][6] < 0.5f) { bowLog[bi][6] = 1f; break; } bool mainHit = !(p.inventory.holdingItem != null && p.inventory.holdingItem.Actions != null && p.inventory.holdingItem.Actions.Length > 0 && p.inventory.holdingItem.Actions[0] is ItemActionDynamicMelee) || Time.realtimeSinceStartup - RebirthGameBridgeMeleeProbe.LastMainHitAt < 0.9f;   // a graze (the lighter in-swing hit) is not a successful hit
                if (mainHit) { for (int si = swingLog.Count - 1; si >= 0; si--) if (now - swingLog[si][0] < 1.2f) { swingLog[si][6] = 1f; break; } hits++; } else grazes++;
                lastHitAt = now; log("hit " + targetName + " " + lastTargetHp + "->" + target.Health); }
            if (p.Health <= lastPlayerHp - 3)   // a real hit; bleeding and infection tick 1 at a time
            {
                lastTakenAt = now; lastTakenAct = curActionName ?? "";
                RebirthGameBridgeFightRecorder.Event("DAMAGE " + (lastPlayerHp - p.Health));
                takenLog.Add(string.Join(",", new[] { (now - start).ToString("0.0"), (lastPlayerHp - p.Health).ToString("0"), dist.ToString("0.00"), lastZSwing ? "1" : "0", lastBusy ? "1" : "0", playerSpeed.ToString("0.0"), (curActionName ?? "").Replace(",", " ") }));
                hitsTaken++;
                log("took damage " + lastPlayerHp + "->" + p.Health);
                if (now - lastHitAt > 5f && !fleeing && !recovering) incident("taking damage without landing a hit for " + (now - lastHitAt).ToString("0") + "s");
            }
            lastTargetHp = target.Health; lastPlayerHp = p.Health;

            // On a pillar / high ground (target well below us): hold the position. No dodging, approaching or backing off (a step
            // sideways is a fall), shoot down with the bow, melee only what is within reach.
            {
                Vector3 pd = p.position - lastPlayerPos; pd.y = 0f;
                playerSpeed = lastPlayerPos == Vector3.zero || dt <= 0f ? 0f : Mathf.Lerp(playerSpeed, pd.magnitude / dt, 0.4f);
                lastPlayerPos = p.position;
            }
            bool elevated = HeightAboveTerrain(p) >= 2.5f;
            // A knocked-down zombie that is still rolling (down a slope, from the knockback) can not be hit: the swing or arrow would be
            // wasted stamina/ammo. Wait until it comes to rest, then hit it as often as possible.
            {
                float rdt = dt > 0f ? dt : 0.016f;
                rollSpeed = Mathf.Lerp(rollSpeed, (target.position - lastRollPos).magnitude / rdt, 0.35f);
                lastRollPos = target.position;
            }
            bool ragdolled = false;
            try { ragdolled = target.emodel != null && target.emodel.IsRagdollActive; } catch { }
            bool rolling = ragdolled && rollSpeed > 0.8f;
            float stamina = Stamina(p);
            minStamina = Mathf.Min(minStamina, stamina);
            if (pendAt > 0f)
            {
                pendMin = Mathf.Min(pendMin, stamina);
                if (pendMin < pendStamina - 5f)
                {
                    // The stamina is paid when the game fires the melee sweep (the hit frame of the animation): that is the real delay between
                    // pressing and the hit, i.e. how far ahead of the zombie's movement to aim.
                    if (swingLog.Count > 0 && target != null)
                    {
                        float[] sw = swingLog[swingLog.Count - 1];
                        try
                        {
                            Ray look = p.GetLookRay();
                            Vector3 ld = look.direction.normalized, lo = look.origin;
                            float best = 99f;
                            foreach (float al in new[] { 0f, 0.35f, 1f })
                            {
                                Vector3 v = RebirthGameBridgePlayer.EntityAimPoint(target, al) - lo;
                                float perp = (v - ld * Vector3.Dot(v, ld)).magnitude;
                                if (perp < best) best = perp;
                            }
                            WorldRayHitInfo hh = p.HitInfo;
                            Entity he = hh != null && hh.bHitValid && hh.transform != null ? hh.transform.GetComponentInParent<Entity>() : null;
                            sw[11] = best; sw[12] = he == target ? 1f : 0f;
                            sw[13] = (RebirthGameBridgePlayer.EntityAimPoint(target, 0.35f) - lo).magnitude;
                        }
                        catch { }
                    }
                    float measured = now - pendAt;
                    if (measured > 0.05f && measured < 0.9f) { hitDelay = delaySamples == 0 ? measured : Mathf.Lerp(hitDelay, measured, 0.35f); delaySamples++; }
                    swingsRegistered++;
                    if (lastRegAt > 0f) cycleEst = Mathf.Clamp(Mathf.Lerp(cycleEst, pendAt - lastRegAt, 0.3f), 0.6f, 1.8f);   // learn the weapon's real rhythm
                    lastRegAt = pendAt; nextSwing = pendAt + cycleEst * (StaminaFraction(p) < 0.3f ? 1.4f : 1f); pendAt = 0f;
                }
                else if (now - pendAt > 0.8f) { swingsIgnored++; pendAt = 0f; nextSwing = now; plantUntil = 0f; stepOutUntil = 0f; }   // not taken: press again now
            }

            if (now >= nextPackScan) { nextPackScan = now + 0.3f; pack = ThreatsSorted(p, 14f); if (pack.Count > 0) lastPackSeen = now; }
            // BLEEDING IS CRITICAL: it drains health and cuts the maximum. Bandage it at the first safe moment - nothing close, high ground,
            // or the zombie is down - and at the latest after 4 s even if it means taking a hit while bandaging.
            if (IsBleeding(p))
            {
                if (bleedingSince < 0f) bleedingSince = now;
                float nearBleed = pack.Count > 0 ? Flat(pack[0].position, p.position) : 99f;
                bool safeToBandage = nearBleed > 2.8f || elevated || TargetStunned(target);
                // Immediately, whatever is around: bleeding drains health and the maximum every second, a bandage takes ~2.5 s.
                if (now >= nextBleedTreat && HealSlot(p, true) >= 0)
                {
                    nextBleedTreat = now + 8f;
                    log("bleeding - bandaging now" + (safeToBandage ? "" : " (nothing safer is coming)"));
                    yield return HealRoutine(p, log);
                    aimInit = false; continue;
                }
            }
            else bleedingSince = -1f;
            // Running from a lone zombie and coming back is worse than finishing it: only flee from a pack, or when nearly dead.
            if (!fleeing && o.FleeHealth > 0f && p.Health <= o.FleeHealth && now > noFleeUntil && !elevated) { fleeing = true; triedHealThisRetreat = false; log("health " + p.Health + " - backing off to heal"); }
            // Stamina like a player: at about a third, step away a few metres, face it and catch a breath; come
            // back when recovered - or earlier if it walks back into range (then hit first).
            float staminaMax = p.Stats != null && p.Stats.Stamina.ModifiedMax > 0f ? p.Stats.Stamina.ModifiedMax : 100f;
            // Tired with a zombie right there: keep fighting at a slower rhythm (see Swing); only catch a breath when nothing is close.
            float retreatAt = o.PowerMix ? Mathf.Max(o.RetreatStamina, 44f / Mathf.Max(1f, staminaMax)) : o.RetreatStamina;
            if (!fleeing && !recovering && now >= recoverCool && RebirthCombatStaminaPolicy.NeedsRecovery(stamina, staminaMax, retreatAt) && !elevated)
            {
                recovering = true; retreatMoving = true; retreats++;
                log("stamina " + stamina.ToString("0") + " (about a third) - stepping away to recover");
            }
            if (recovering)
            {
                if (RebirthCombatStaminaPolicy.Recovered(stamina, staminaMax, o.PowerMix ? Mathf.Max(o.ResumeStamina, 0.92f) : o.ResumeStamina)) { recovering = false; log("stamina " + stamina.ToString("0") + " - re-engaging"); }
                else if (stamina >= 0.5f * staminaMax && dist <= 2.2f && now - lastTakenAt < 1f) // cornered after recovering enough for an effective counterattack
                { recovering = false; log("it keeps following (stamina " + stamina.ToString("0") + ") - standing and hitting first as it steps in"); recoverCool = now + 6f; }
            }

            ItemValue held = p.inventory.holdingItemItemValue;
            bool ranged = IsRanged(held != null ? held.ItemClass : null);
            float effRange = ranged ? o.GunRange : o.Range;
            string action = "";

            // --- weapon choice: the bow while it is far (shot crouched and standing still), the melee weapon once it is close ---
            if (!fleeing && !recovering && now >= weaponSwapLock)
            {
                bool heldBow = IsBow(held != null ? held.ItemClass : null);
                int meleeSlot = MeleeSlot(p), bowSlot = BowSlot(p);
                float nearestFlat = pack.Count > 0 ? Flat(pack[0].position, p.position) : dist;
                bool outOfArrows = ArrowCount(p) == 0 && held.Meta <= 0;
                // High ground: ranged all the time; melee only when one is right next to us (a jumping zombie can not swing while jumping,
                // so a quick bat swing only when it stands at the foot of the pillar), and back to the bow as soon as it is not.
                if (elevated && !outOfArrows && bowSlot >= 0)
                {
                    if (!heldBow && nearestFlat > 2.3f) { weaponSwapLock = now + 1.5f; log("high ground, nothing right next to me - back to the bow"); yield return SelectSlot(p, bowSlot); SelectBestArrows(p); aimInit = false; continue; }
                    if (heldBow && nearestFlat < 1.6f && meleeSlot >= 0) { weaponSwapLock = now + 1.5f; log("one right next to the pillar (" + nearestFlat.ToString("0.0") + "m) - melee for it"); yield return SelectSlot(p, meleeSlot); aimInit = false; continue; }
                }
                // Keep the bow until the zombie is really on us: a shot not yet loosed beats a swing, so melee only when it is within ~2 m
                // (3 m once an arrow has gone), never in the middle of a draw unless it is point blank, or when out of arrows.
                if (heldBow && !elevated && meleeSlot >= 0 && (outOfArrows || dist < 1.6f || (!bowDrawing && dist < (arrowsLoosed == 0 ? 2.2f : 3.2f))))
                {
                    weaponSwapLock = now + 3f;
                    bowDrawing = false;
                    if (tMeleeSwitch < 0f) tMeleeSwitch = now - start;
                    log(outOfArrows ? "out of arrows - switching to the melee weapon" : "it is " + dist.ToString("0.0") + "m away - switching from the bow to the melee weapon");
                    yield return SelectSlot(p, meleeSlot);
                    aimInit = false; continue;
                }
                if (false && heldBow && ((dist < 5.5f && !elevated) || (ArrowCount(p) == 0 && held.Meta <= 0)) && meleeSlot >= 0)
                {
                    weaponSwapLock = now + 3f;
                    log(ArrowCount(p) == 0 && held.Meta <= 0 ? "out of arrows - switching to the melee weapon" : "it is " + dist.ToString("0.0") + "m away - switching from the bow to the melee weapon");
                    yield return SelectSlot(p, meleeSlot);
                    aimInit = false; continue;
                }
                float nearest = pack.Count > 0 ? Flat(pack[0].position, p.position) : dist;
                if (!heldBow && !ranged && bowSlot >= 0 && !elevated && (dist > Mathf.Clamp(closingSpeed * 3.6f, 7f, 30f) && nearest > Mathf.Clamp(closingSpeed * 3.0f, 6f, 24f)) && !o.AimBody)
                {
                    weaponSwapLock = now + 3f;
                    log(targetName + " is " + dist.ToString("0") + "m away - taking out the bow");
                    yield return SelectSlot(p, bowSlot);
                    SelectBestArrows(p);
                    if (tBowOut < 0f) tBowOut = Time.realtimeSinceStartup - start;
                    aimInit = false; continue;
                }
            }

            if (fleeing || recovering)
            {
                // Breathing room while fleeing: patch up, then re-engage. Without a healing item keep going.
                if (fleeing && dist >= 10f && !triedHealThisRetreat)
                {
                    triedHealThisRetreat = true;
                    int hpBefore = p.Health;
                    yield return HealRoutine(p, log);
                    if (p.Health > hpBefore || HealSlot(p) >= 0 || p.Health > o.FleeHealth)
                    {
                        fleeing = false;
                        noFleeUntil = Time.realtimeSinceStartup + 8f; // heal ticks over time; don't panic again immediately
                        log("patched up (health " + p.Health + ") - re-engaging");
                        aimInit = false;
                        continue;
                    }
                }
                float safe = fleeing ? 25f : 8f;
                if (fleeing && dist >= safe) { result = "fled"; break; }
                // Recovering stamina: walk off steadily to ~10 m, then turn round and watch it while stamina
                // comes back; only move off again if it closes within 6 m (hysteresis - no stop/start inching).
                if (!fleeing)
                {
                    if (dist < 5f) retreatMoving = true;
                    else if (dist >= safe) retreatMoving = false;
                }
                if (!fleeing && retreatMoving)
                {
                    // Out of stamina (or it is too close): turn and walk/run clear, about 6 m, then face it while the stamina refills.
                    bool hS, hJ;
                    Vector3 hAway = SafeEscape(p, EscapeDirection(p, 15f, target), out hS, out hJ);
                    float hYaw = Mathf.Atan2(hAway.x, hAway.z) * Mathf.Rad2Deg;
                    RebirthGameBridgePlayer.SmoothTurn(p, hYaw, -5f, 0.05f);
                    float hErr = Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, hYaw));
                    if (hErr < 70f && hS) RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1);
                    if (hErr < 35f && hS && stamina > 8f && dist < 5f) RebirthGameBridgeInput.HoldFrames(a.Run, 1);
                    action = "retreat"; aimInit = false;
                    Trace(trace, ref nextTrace, now - start, p, target, dist, -1f, false, action);
                    yield return null;
                    continue;
                }
                if (fleeing)
                {
                    // Turn around and go the opposite way; walk once mostly turned, back off while turning.
                    bool escapeSafe, escapeJump;
                    if (now >= nextEscapeProbe)
                    {
                        nextEscapeProbe = now + 0.15f;
                        // Away from every nearby threat, over ground that is safe to run on.
                        escapeDir = SafeEscape(p, EscapeDirection(p, 15f, target), out escapeSafe, out escapeJump);
                        escapeOk = escapeSafe; if (escapeJump && p.onGround) RebirthGameBridgeInput.HoldFrames(a.Jump, 4);
                    }
                    Vector3 away = escapeDir;
                    float awayYaw = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                    RebirthGameBridgePlayer.SmoothTurn(p, awayYaw, -5f, 0.15f);
                    float turnErr = Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, awayYaw));
                    if (turnErr < 70f && escapeOk) RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1);
                    else if (dist < 2.5f) SafeBackOff(p, a, 1);
                    if (turnErr < 30f && escapeOk && stamina > 8f && (fleeing || dist < 1.8f)) RebirthGameBridgeInput.HoldFrames(a.Run, 1); // recovering: walk (running burns the stamina we are saving)
                    action = (fleeing ? "flee" : "recover") + (escapeOk ? "" : " (no safe ground)");
                }
                else
                {
                    // Face it while catching our breath (never stand with our back to it).
                    float wy, wp;
                    RebirthGameBridgePlayer.AnglesTo(p, RebirthGameBridgePlayer.EntityAimPoint(target, 0.3f), out wy, out wp);
                    RebirthGameBridgePlayer.SmoothTurn(p, wy, wp, 0.18f);
                    action = "watch";
                }
                aimInit = false;
                Trace(trace, ref nextTrace, now - start, p, target, dist, -1f, false, action);
                yield return null;
                continue;
            }

            // --- pivot-run ---
            if (pivotUntil > now && dist >= pivotGoal && now - pivotStart > 0.5f) pivotUntil = 0f;     // far enough: stop, turn and wait for it
            if (pivotUntil > now && !elevated)
            {
                bool pSafe, pJump;
                Vector3 pAway = SafeEscape(p, (lineUpMode && lineUpAround) ? lineUpDir : EscapeDirection(p, 15f, target), out pSafe, out pJump);
                // No progress for 0.6 s (blocked, or boxed in by the other one): stop retreating, drop the fancy line-up and fight from here.
                if (now - pivotStart > 0.6f)
                {
                    if ((p.position - pivotPos).sqrMagnitude < 0.25f || !pSafe) { pivotUntil = 0f; lineUpMode = false; pivotCool = now + 2.5f; log("retreat is blocked - standing and fighting"); }
                    pivotPos = p.position; pivotStart = now;
                }
                float pYaw = Mathf.Atan2(pAway.x, pAway.z) * Mathf.Rad2Deg;
                RebirthGameBridgePlayer.SmoothTurn(p, pYaw, -5f, 0.05f);
                float pErr = Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, pYaw));
                if (pErr < 80f && pSafe) RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1);
                if (pErr < 60f && pSafe && stamina > 6f && !(lineUpMode && lineUpMany)) RebirthGameBridgeInput.HoldFrames(a.Run, 1);     // do not hesitate to run: far away the stamina comes back
                lastAttackAt = now; aimInit = false;
                Trace(trace, ref nextTrace, now - start, p, target, dist, -1f, false, "pivot-run");
                yield return null;
                continue;
            }

            // --- aim ---
            // Moving zombies: upper body (reliable hits). Stunned/knocked down or aim=head: the head.
            // Melee: the head first, always (most damage, stuns and knocks down); upper body only as the fallback when the
            // crosshair keeps missing the head. Guns: centre mass unless aim=head.
            // The head bobs by about +-10 cm; at 1 m that is +-6 degrees, more than the camera can follow, so a moving zombie is
            // hit in the upper chest (a much bigger target) and the head is taken whenever it holds still enough (it stops to
            // attack, is stunned, crawls slowly or walks straight at us) - with hysteresis so the aim does not flip back and forth.
            RebirthGameBridgeMeleeProbe.Target = target as EntityAlive;
            if (o.DanceAuto && !o.HumanStyle && !ranged) { try { o.Dance = ((EntityAlive)target).GetMaxHealth() >= 190; } catch { } }   // measured: halves the damage taken from bikers, costs ordinary zombies
            Vector3 headPt = RebirthGameBridgePlayer.EntityAimPoint(target, 0f);
            if (dt > 0f && lastHeadPt != Vector3.zero) headSpeed = Mathf.Lerp(headSpeed, (headPt - lastHeadPt).magnitude / dt, 0.15f);
            lastHeadPt = headPt;
            headMode = headMode ? headSpeed < 2.4f : headSpeed < 1.2f;
            bool sleeping = false; try { sleeping = ((EntityAlive)target).IsSleeping; } catch { }
            bool headNow = o.AimHead || (!o.AimBody && (ranged ? sleeping : (TargetStunned(target) || (o.HumanStyle && o.Spacing) || (headMode && dist > 1.3f))));
            float[] along = headNow ? new[] { 0f, 0.12f, 0.3f } : (ranged ? new[] { 0.3f, 0.15f, 0.5f } : new[] { 0.22f, 0.12f, 0.4f });
            if (aimIndex >= along.Length) aimIndex = 0;
            float predDist3 = 99f;   // melee: where the chest will be (3D, from the eye) when the swing lands
            Vector3 raw = RebirthGameBridgePlayer.EntityAimPoint(target, along[aimIndex]);
            // Lead a moving target for melee: a swing lands ~0.25 s after the click.
            if (dt > 0f) targetVel = Vector3.Lerp(targetVel, (target.position - lastTargetPos) / dt, 0.05f); // heavily smoothed: per-frame deltas are noisy
            if (dt > 0f) targetVelFast = Vector3.Lerp(targetVelFast, (target.position - lastTargetPos) / dt, 0.45f);
            lastTargetPos = target.position;
            if (!ranged)
            {
                // Anticipate like a player who has learned this zombie's walk: carry the body forward with its velocity and repeat the
                // head's bob/sway pattern (period learned per walk type) to where it will be when the swing lands.
                gait.Add(now, target.position, RebirthGameBridgePlayer.EntityAimPoint(target, 0f), RebirthGameBridgePlayer.EntityAimPoint(target, 1f));
                Vector3 predHead, predPelvis;
                gait.Predict(now, o.Lead > 0f ? o.Lead : Mathf.Clamp(hitDelay, 0.15f, 0.7f), out predHead, out predPelvis);
                raw = Vector3.Lerp(predHead, predPelvis, along[aimIndex]);
                Vector3 rayO = p.position + Vector3.up * p.GetEyeHeight();
                if (o.TrueOrigin) { try { Vector3 lo = p.GetLookRay().origin; if ((lo - rayO).magnitude < 3f) rayO = lo; } catch { } }   // the melee ray starts at the camera, up to ~1.5 m behind the eye; reach (2.4 m) is measured from there
                predDist3 = (Vector3.Lerp(predHead, predPelvis, 0.35f) - rayO).magnitude;
            }
            if (ranged && IsBow(held != null ? held.ItemClass : null))
            {
                // Arrows fly slower than bullets and drop: aim above by the drop over the flight time, and ahead of a walking target.
                float flight = Mathf.Clamp(dist / 49.3f, 0.05f, 0.9f);   // measured: the wooden bow arrow flies at 49.3 m/s
                bowLift = 0.5f * 3.5f * flight * flight * o.BowDrop;   // measured arrow gravity 3.5 m/s²
                raw.y += bowLift;
                Vector3 bowV = targetVel; bowV.y = 0f;
                raw += bowV * flight;
            }
            bool lowTarget = TargetStunned(target) || now - lastStunnedAt < 1.6f;   // down, or still getting up
            if (!ranged && lowTarget) { raw = RebirthGameBridgePlayer.EntityAimPoint(target, 0.5f); aimInit = false; }   // knocked down: look at where it really lies on the ground, no damping from the walking aim
            aimPoint = aimInit ? Vector3.Lerp(aimPoint, raw, 1f - Mathf.Exp(-dt / (ranged ? 0.12f : o.AimDamp))) : raw; // damp bone/walk-cycle wobble
            aimInit = true;
            float yawT, pitchT;
            RebirthGameBridgePlayer.AnglesTo(p, aimPoint, out yawT, out pitchT);
            // A person keeps the crosshair at eye level and lets the zombie walk into it (recorded: median -9 deg, mostly -7.5, at the hit frame);
            // chasing the head down to the ground makes the swing pass over/under it.
            if (o.HumanStyle && o.Spacing && !ranged && !lowTarget && !rolling && dist > 0.95f) pitchT = Mathf.Clamp(pitchT, -14f, -2f);
            // Commit to the swing: from the press to the hit frame the camera does not move (a view still turning at the hit frame is what makes it miss).
            if (o.HumanStyle && o.Spacing && !ranged && cyc == 1 && !TargetStunned(target)) { yawT = frozenYaw; pitchT = frozenPitch; }   // not while it lies on the ground (the view must go down to it)
            float aimError = RebirthGameBridgePlayer.SmoothTurn(p, yawT, pitchT, ranged ? 0.1f : o.AimSmooth); // shared smooth camera

            // --- what is under the crosshair ---
            WorldRayHitInfo hi = p.HitInfo;
            Entity hitEntity = hi != null && hi.bHitValid && hi.transform != null ? hi.transform.GetComponentInParent<Entity>() : null;
            var hitEnemy = hitEntity as EntityAlive;
            if (hitEnemy != null && hitEnemy != target && IsThreat(hitEnemy, p) && Mathf.Sqrt(hi.hit.distanceSq) <= effRange)
                setTarget(hitEnemy, "switching to the one in front of the crosshair:");
            bool onTarget = hitEntity == target;
            curOnTarget = onTarget; curAim = 0f; curDist = dist; curHead = headNow; curBlind = false; curSpeed = new Vector3(targetVel.x, 0f, targetVel.z).magnitude; curZSwing = TargetSwinging(target);
            float hitDistance = onTarget ? Mathf.Sqrt(hi.hit.distanceSq) : dist;
            // Grass/plants right in front of a (knocked-down) zombie block the ray; a player just swings through.
            bool throughPlant = !onTarget && hi != null && hi.bHitValid && hitEntity == null && dist <= effRange
                && aimError < 2f && IsPlantBlock(hi)
                && Mathf.Sqrt(hi.hit.distanceSq) <= Vector3.Distance(p.GetLookRay().origin, aimPoint) + 0.5f;
            lockFrames = onTarget || throughPlant ? lockFrames + 1 : 0;

            if (onTarget || aimError > 3f || dist > effRange + 1.5f) offTargetSince = -1f;
            else if (offTargetSince < 0f) offTargetSince = now;
            else if (now - offTargetSince > 0.45f) { aimIndex = (aimIndex + 1) % along.Length; offTargetSince = -1f; }

            // --- don't chase what isn't coming: an enemy that stays far and never gets closer is dropped ---
            if (dist > effRange + 4f)
            {
                if (farSince < 0f) { farSince = now; farBestDist = dist; }
                if (dist < farBestDist - 1f) { farBestDist = dist; farSince = now; } // it is closing in (or we are)
                if (now - farSince > 5f)
                {
                    incident(targetName + " is not engaging (" + dist.ToString("0") + "m, not closing) - holding position instead of chasing");
                    EntityAlive other = NearestEnemy(p, Mathf.Min(o.Radius, effRange + 4f), null, target);
                    if (other != null) { setTarget(other, "switching to"); continue; }
                    result = "holding"; break;
                }
            }
            else farSince = -1f;

            // --- several enemies at once: string them out instead of standing in the middle of the pack ---
            // Zombies walk at different speeds and stumble over things; running from a bunch that is still a few metres
            // away makes them arrive one at a time, and one at a time is a fight we win. Bounded (kiteSpent), and only
            // while the nearest one is not yet on us. Melee only: a gun can shoot as they come.
            if (pack.Count == 0 && now - lastPackSeen > 6f) kiteSpent = 0f;
            if (!o.Single && !ranged && !elevated && pack.Count >= 2)
            {
                float d1 = Flat(pack[0].position, p.position), d2 = Flat(pack[1].position, p.position);
                bool stretched = d2 - d1 >= 3.5f || d2 > 9f;
                if (!stretched && d1 > 5.5f && kiteSpent < 14f)
                {
                    bool kSafe = true, kJump = false;
                    if (now >= nextEscapeProbe)
                    {
                        nextEscapeProbe = now + 0.15f;
                        escapeDir = SafeEscape(p, EscapeDirection(p, 15f, pack[0]), out kSafe, out kJump);
                        escapeOk = kSafe; if (kJump && p.onGround) RebirthGameBridgeInput.HoldFrames(a.Jump, 4);
                    }
                    if (escapeOk)
                    {
                        float awayYaw = Mathf.Atan2(escapeDir.x, escapeDir.z) * Mathf.Rad2Deg;
                        RebirthGameBridgePlayer.SmoothTurn(p, awayYaw, -5f, 0.15f);
                        float turnErr = Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, awayYaw));
                        if (turnErr < 70f) RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1);
                        if (turnErr < 30f && stamina > 15f) RebirthGameBridgeInput.HoldFrames(a.Run, 1);
                        if (kiteSpent <= 0f) log(pack.Count + " enemies close together (nearest " + d1.ToString("0.0") + "m, next " + d2.ToString("0.0") + "m) - pulling back to string them out");
                        kiteSpent += dt;
                        aimInit = false; lastAttackAt = now;
                        Trace(trace, ref nextTrace, now - start, p, target, dist, -1f, false, "string them out");
                        yield return null;
                        continue;
                    }
                }
            }

            // --- attack / move ---
            if (ranged)
            {
                NoteMagazine(held);
                if (held.Meta <= 0 && IsBow(held.ItemClass) && ArrowCount(p) > 0)
                {
                    // A bow loads its own arrow when the draw starts (AutoReload); an occasional R press helps, but do not wait for it.
                    if (now - lastReloadPress > 1.6f && !RebirthGameBridgeInput.IsHeld(a.Reload)) { RebirthGameBridgeInput.HoldFrames(a.Reload, 3); lastReloadPress = now; }
                }
                if (held.Meta <= 0 && !IsBow(held.ItemClass))
                {
                    if (!reloading)
                    {
                        RebirthGameBridgeInput.HoldFrames(a.Reload, 3); lastReloadPress = now;
                        reloading = true; reloadStart = now; reloads++;
                        log("magazine empty - reloading");
                    }
                    else if (now - reloadStart > 9f) { result = "out_of_ammo"; break; }
                    else if (now - lastReloadPress > 1.5f && !RebirthGameBridgeInput.IsHeld(a.Reload)) { RebirthGameBridgeInput.HoldFrames(a.Reload, 3); lastReloadPress = now; }   // a press during the equip animation is lost: ask again
                    if (dist < 6f) SafeBackOff(p, a, 1); // buy time during the reload (never into another zombie)
                    Trace(trace, ref nextTrace, now - start, p, target, dist, aimError, onTarget, "reload");
                    lastAttackAt = now;
                    yield return null;
                    continue;
                }
                if (reloading) { reloading = false; log("reloaded (" + held.Meta + " rounds, " + (now - reloadStart).ToString("0.0") + "s)"); }
                bool bowNow = IsBow(held != null ? held.ItemClass : null);
                if (bowNow && now - lastAmmoCheck > 1.5f) { lastAmmoCheck = now; SelectBestArrows(p); }
                if (bowNow)
                {
                    // Bow: crouch at range (steadier and quieter), stand still to draw, release after a full draw. No line of
                    // sight for a couple of seconds (something in the way): move a little closer instead of waiting.
                    if (dist > 8f) RebirthGameBridgeInput.HoldFrames(a.Crouch, 1);
                    if (!onTarget && aimError < 3f) { if (noLosSince < 0f) noLosSince = now; } else noLosSince = -1f;
                    bool drawing = bowDrawing;                                  // holding the string back: stand still and keep tracking
                    float fullDraw = 0.85f;
                    try { var cad0 = p.inventory.holdingItemData.actionData[0] as ItemActionCatapult.ItemActionDataCatapult; if (cad0 != null && cad0.m_MaxStrainTime > 0.2f) fullDraw = cad0.m_MaxStrainTime; } catch { }
                    if (!drawing && dist < 3.5f && !elevated && arrowsLoosed > 0) action = SafeBackOff(p, a, 1);
                    else if (!drawing && !elevated && noLosSince >= 0f && now - noLosSince > 2.5f && dist > 7f && GroundSafe(p, target.position - p.position, 2f))
                    { RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1); action = "closer for a clear shot"; }
                    else if (!drawing && !elevated && dist > effRange && GroundSafe(p, target.position - p.position, 2f)) { RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1); action = "approach"; }

                    if (held.Meta <= 0) { bowDrawing = false; action = "loading the bow"; }   // a press on an empty chamber only reloads; the draw starts once an arrow is nocked
                    else if (bowDrawing)
                    {
                        // Keep the string back while the camera tracks the target; let go when fully drawn and on target (or when it is on top of us).
                        RebirthGameBridgeInput.HoldFrames(a.Primary, 2);
                        float drawn = now - bowDrawStart;
                        bool fullNow = drawn >= fullDraw + 0.05f;
                        bool aimedNow = onTarget || aimError < 2.0f;
                        bool pointBlank = dist < 2.6f;
                        bool overHeld = drawn > fullDraw + 1.8f;
                        action = fullNow ? "drawn - tracking" : "drawing";
                        if ((fullNow && aimedNow && !rolling) || overHeld || (pointBlank && fullNow))
                        {
                            bowDrawing = false;                                  // stop pressing: the arrow is released this frame
                            arrowsLoosed++; swings++; lastAttackAt = now;
                            if (tFirstRelease < 0f) tFirstRelease = now - start;
                            bowCooldownUntil = now + 0.9f;                       // nock the next one
                            bowLog.Add(new[] { now, (target.position + Vector3.up - p.position).magnitude, aimError, onTarget ? 1f : 0f, (float)held.Meta, (float)held.SelectedAmmoTypeIndex, 0f, bowLift });
                            action = "release";
                        }
                    }
                    else if (now >= bowCooldownUntil && !rolling && (dist <= effRange))
                    {
                        bowDrawing = true; bowDrawStart = now;                    // start drawing as soon as an arrow is nocked, aim while drawing
                        if (tFirstDraw < 0f) tFirstDraw = now - start;
                        RebirthGameBridgeInput.HoldFrames(a.Primary, 2);
                        action = "start draw";
                    }
                    else if (action == "") action = "aiming";
                    if (bowDrawing || action == "release") { if (dist > 8f) RebirthGameBridgeInput.HoldFrames(a.Crouch, 1); }
                    if (dist > 8f) RebirthGameBridgeInput.HoldFrames(a.Crouch, 1);
                    if (!onTarget && aimError < 3f) { if (noLosSince < 0f) noLosSince = now; } else noLosSince = -1f;
                }                else
                {
                if (!onTarget && !throughPlant)
                {
                    if (noLosSince < 0f) { noLosSince = now; log("shot obstructed - looking for a safe side angle"); }
                    float blockedFor = now - noLosSince;
                    if (blockedFor > 8f) { result = "obstructed"; break; }
                    // Move only around a real solid obstruction. Empty air or light foliage needs
                    // a settled aim; strafing there makes the sight chase a moving target indefinitely.
                    bool solidObstruction = hi != null && hi.bHitValid && hitEntity == null && !IsPlantBlock(hi);
                    if (!elevated && solidObstruction && aimError < 3f && blockedFor > 1f)
                    {
                        Vector3 right = Quaternion.Euler(0f, p.rotation.y, 0f) * Vector3.right;
                        bool goRight = blockedFor < 4f;
                        Vector3 side = goRight ? right : -right;
                        if (GroundSafe(p, side, 2f))
                        {
                            RebirthGameBridgeInput.HoldFrames(goRight ? a.MoveRight : a.MoveLeft, 1);
                            action = "side angle for a clear shot";
                        }
                    }
                }
                else noLosSince = -1f;
                if (dist < 4f) action = SafeBackOff(p, a, 1); // keep shooting distance, but never back into another zombie
                else if (dist > effRange && GroundSafe(p, target.position - p.position, 2f)) { RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1); action = "approach"; }
                if ((onTarget || throughPlant) && hitDistance <= effRange && now >= nextSwing)
                {
                    RebirthGameBridgeInput.HoldFrames(a.Primary, 3); // one trigger pull
                    swings++; lastAttackAt = now; action = "shoot";
                    nextSwing = now + o.FireInterval;
                }
                }
            }
            else
            {
                // --- melee is a dance: bait its swing, step out so it whiffs, step in and punish, never stand still ---
                PlayerAction attack = o.Power ? a.Secondary : a.Primary;
                // Power attack (double damage, 35.6 stamina, slower): only when the hit is certain - crosshair on it, in reach, it is not mid-swing and
                // is standing still in its stance - and stamina can pay for it with enough left to step back. Never on a downed zombie.
                bool powerOk = o.PowerMix && !o.Power && !TargetStunned(target) && onTarget && stamina >= 38f
                    && !TargetSwinging(target) && predDist3 >= 0.9f && predDist3 <= 2.0f;   // measured from a human: power hits do 1.8x (3.6x on the head); 3 power attacks from full stamina, then back off to refill
                bool zSwinging = TargetSwinging(target), zStunned = TargetStunned(target);
                lastZSwing = zSwinging;
                // A knocked-down zombie is slow to get up, and standing up takes a while after the ragdoll ends: the safest time to hit
                // it, so the get-up phase counts as helpless too (it can not swing back while it is getting up).
                if (zStunned) lastStunnedAt = now;
                bool gettingUp = !zStunned && now - lastStunnedAt < 1.6f && !zSwinging;
                if (gettingUp) zStunned = true;
                if (zSwinging && !wasSwinging) enemySwings++;
                if (!zSwinging && wasSwinging) punishUntil = now + 1.0f;   // its swing just ended: recovery window
                wasSwinging = zSwinging;
                if (!zSwinging || dist >= ZombieReach) dodgeSince = -1f;
                // On target = the crosshair is on it (any aim error) or we are lined up; a melee swing sweeps wider than the crosshair
                // ray, so when we have been aimed and in range for a moment and the ray still says "nothing", swing anyway like a player.
                if (lockFrames >= 2) lastLockAt = now;
                bool locked = lockFrames >= 2 && (onTarget || aimError < 2.5f);
                curAim = aimError;
                // A swing with the crosshair ray on "nothing" still hits (the game's melee sweeps wider than the ray) - but only when we have been
                // lined up and in reach for a moment; loosening this further (swing as soon as we face it) measured WORSE (more damage taken).
                bool blind = !locked && aimError < 5f && predDist3 <= o.ReachMax && now - lastLockAt > 0.35f && now - blindAt > 0.35f;
                if (blind) { locked = true; blindAt = now; curBlind = true; }
                // The game ignores an attack press while the previous swing (wind-up + recovery) is still running, and the press is not
                // queued: so only press when no swing is in progress, and spend the swing animation dodging/positioning instead.
                bool swingBusy = false;
                try { swingBusy = p.inventory.IsHoldingItemActionRunning(); } catch { }
                lastBusy = swingBusy;
                if (predDist3 < o.ReachMin) { if (hugSince < 0f) hugSince = now; } else hugSince = -1f;
                bool hugged = hugSince >= 0f && now - hugSince > 1.2f;   // it will not let go: hit it from where we are
                bool sweet = (predDist3 >= o.ReachMin && predDist3 <= o.ReachMax) || hugged;
                // Repeated dodge/pivot cycles must not indefinitely veto an available attack.
                // Use actual registered swings, not the activity timer reset by movement.
                bool pressureDue = now - (lastRegAt > 0f ? lastRegAt : start) > 2.2f
                    && stamina >= 20f && onTarget && predDist3 >= 0.7f && predDist3 <= o.ReachMax + 0.15f;
                bool canSwing = locked && now >= nextSwing && !swingBusy && !rolling && (sweet || zStunned) && dist <= o.Range
                    && (!o.Dance || playerSpeed < 0.6f || hugged || pressureDue) && (o.StillMax <= 0f || playerSpeed < o.StillMax || hugged || pressureDue)
                    && !(o.Dance && zSwinging && !zStunned && dist < ZombieReach + 0.25f && !pressureDue);
                bool groundAhead = GroundSafe(p, target.position - p.position, Mathf.Min(2f, dist));

                // Fight the zombie: hit first as it comes in and keep hitting on a steady rhythm. Only get out of
                // the way of a swing that can actually reach us, then step straight back in. Stamina is managed
                // above (recover at ~1/3 and come back with a first hit).
                // every other awake zombie counts: do not back into one standing behind us, and do not step towards one beside us
                bool otherBehind = false, otherClose = false;
                {
                    Vector3 fw = Quaternion.Euler(0f, p.rotation.y, 0f) * Vector3.forward;
                    foreach (EntityAlive z in pack)
                    {
                        if (z == null || z == target || z.IsDead()) continue;
                        Vector3 dz = z.position - p.position; dz.y = 0f;
                        float dm = dz.magnitude;
                        if (dm < 2.2f) otherClose = true;
                        if (dm < 2.8f && dm > 0.01f && Vector3.Dot(dz / dm, fw) < 0.3f) otherBehind = true;
                    }
                }
                if (o.HumanStyle && o.Spacing && !rolling && !zStunned && !elevated && dist < 5f)
                {
                    // The measured cycle of a recorded player vs a biker (period ~1.3 s): hold ~2.2 m (outside its reach); press and step in for ~0.3 s;
                    // the hit lands at ~1.3 m; from the HIT FRAME itself walk straight back at full speed until ~2.3 m; repeat. Its attack starts when
                    // we are inside ~1.45 m and lands 0.4-0.5 s later at ~1.3 m, so the back-off must already be under way at the hit frame.
                    if (!zSwinging) zsCalm = Mathf.Lerp(zsCalm, new Vector3(targetVelFast.x, 0f, targetVelFast.z).magnitude, 0.3f);
                    bool runner = zsCalm >= (now - RebirthGameBridgeMeleeProbe.LastMainHitAt < 3.5f ? 1.5f : 1.9f);   // right after a hit it may rage and speed up
                    bool idleOne = zsCalm < 0.35f && !zSwinging && closingSpeed < 0.3f;    // standing there (stuck on blocks, busy with something): free hits
                    if (cyc == 1)
                    {
                        if (now - cycAt < 0.28f && dist > 1.55f && !otherClose) { RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1); action = "step in"; }   // never walk into its body
                        else action = "swing";
                        if (RebirthGameBridgeMeleeProbe.LastRaycastAt > cycAt && now - RebirthGameBridgeMeleeProbe.LastRaycastAt >= (RebirthGameBridgeMeleeProbe.LastMainHitAt >= RebirthGameBridgeMeleeProbe.LastRaycastAt - 0.05f ? 0.28f : 0f) || now - cycAt > 1.2f) { cyc = (o.Stagger && !runner) ? 0 : 2; cycAt = now; backSince = -1f; }   // with stagger on there is no need to step away after a swing (not for runners). commit: the swing runs to its hit frame; after a LANDED hit wait ~0.28 s for the stagger, THEN retreat (at once after a miss)
                    }
                    else if (cyc == 2)
                    {
                        // retreat: committed, continuous backward walk (no pulsing); a runner or a failed back-off turns into a real retreat
                        if (idleOne && !runner) { cyc = 0; cycAt = now; action = "it is just standing there"; }
                        else if (runner && now >= pivotCool)
                        {
                            pivotUntil = now + 2.6f; pivotGoal = 7.5f; pivotStart = now; pivotCool = now + 0.6f; cyc = 0; action = "retreat (turn and go)";
                        }
                        else
                        {
                            if (otherBehind) { cyc = 0; cycAt = now; action = "something behind me - stand and fight"; }
                            else { RebirthGameBridgeInput.HoldFrames(a.MoveBack, 1); if (stamina > 30f) RebirthGameBridgeInput.HoldFrames(a.Run, 1); }
                            action = "back off";
                            if (dist >= 2.3f || now - cycAt > 1.1f) { cyc = 0; cycAt = now; }
                        }
                    }
                    else
                    {
                        // Press when the zombie will be ~1.2-1.8 m away at the hit frame (it walks closer by closing speed x hit delay while we step in).
                        float closing = Mathf.Max(0f, closingSpeed);
                        float predHit = dist - closing * Mathf.Max(0.35f, hitDelay) - 0.25f;     // 0.25 = our own short step in
                        float pressLo = runner ? 1.4f : (o.Stagger ? 1.0f : (idleOne ? 1.25f : 1.95f)), pressHi = runner ? 2.3f : (idleOne ? 1.75f : 2.6f);
                        bool predOk = idleOne || runner || o.Stagger || (predHit >= 1.15f && predHit <= 1.85f);
                        // Never attack on fumes: keep enough stamina to run away. Power attack from 70 (leaves >= 34), a normal one from 45.
                        bool ready = !swingBusy && now >= nextSwing && onTarget && (!zSwinging || (o.Stagger && !runner && dist < 2.0f)) && stamina >= 45f;   // with stagger on, hitting into its wind-up beats backing off (it hits us anyway)
                        if (ready && dist >= pressLo && dist <= pressHi && predOk)
                        {
                            if (stamina >= 70f) { powerSwings++; Swing(a.Secondary, false); action = "press (power)"; }
                            else { Swing(a.Primary, false); action = "press"; }
                            cyc = 1; cycAt = now; waitSince = -1f; frozenYaw = p.rotation.y; frozenPitch = p.rotation.x;
                        }
                        else if (!otherBehind && (dist < pressLo - 0.05f && !runner || (zSwinging && dist < 2.0f && !(o.Stagger && !runner && stamina >= 45f))))   // a normal-speed one with stagger on: stand, do not back away from its swing
                        {
                            RebirthGameBridgeInput.HoldFrames(a.MoveBack, 1); if (stamina > 30f) RebirthGameBridgeInput.HoldFrames(a.Run, 1);       // inside its range: step straight back (running), keep facing it
                            lastAttackAt = now;
                            action = "back off (hold)";
                            if (backSince < 0f) backSince = now;
                            if (runner && now - backSince > 0.3f && dist < 1.7f && now >= pivotCool) { pivotUntil = now + 2.6f; pivotGoal = 7.5f; pivotStart = now; pivotCool = now + 0.6f; backSince = -1f; action = "retreat (walking back is not keeping the distance)"; }
                        }
                        else
                        {
                            backSince = -1f;
                            if (dist > pressHi) { if (waitSince < 0f) waitSince = now; } else waitSince = -1f;
                            if (idleOne && dist > pressHi && groundAhead) { RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1); if (dist > 4f && stamina > 50f) RebirthGameBridgeInput.HoldFrames(a.Run, 1); action = "walk up to the idle one"; }
                            else if (waitSince >= 0f && now - waitSince > 1.4f && dist > 2.6f && groundAhead) { RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1); if (dist > 4f && stamina > 50f) RebirthGameBridgeInput.HoldFrames(a.Run, 1); action = "close in (it is not coming)"; }
                            else action = dist > pressHi ? "wait for it" : "hold";
                        }
                    }
                }                else if (now < plantUntil || now < holdStillUntil)
                {
                    action = "impact";                                   // feet planted while our swing lands (until the measured hit frame + margin)
                }
                else if (rolling)
                {
                    action = "wait - it is still rolling";
                    lastAttackAt = now;   // deliberate waiting, not idling
                }
                else if (zStunned)
                {
                    // Helpless (stunned / knocked down): go in and hit it while it can't fight back.
                    cyc = 0;   // the swing cycle is over once it is down
                    bool lookedDown = Mathf.Abs(Mathf.DeltaAngle(p.rotation.x, pitchT)) < 5f && Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, yawT)) < 8f;   // look at it on the ground before swinging
                    if (canSwing && lookedDown) { Swing(o.Power ? a.Primary : attack, true); action = "hit stunned"; }
                    else if (canSwing) action = "look down at it";   // downed: normal attacks, save the stamina
                    else if (dist > 1.5f && groundAhead) { RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1); action = "close in on stunned"; }
                    else action = "finish";
                }
                else if (!canSwing && !zStunned && !elevated && !hugged && predDist3 < o.ReachMin && dist < 2.2f)
                {
                    if (o.HumanStyle && now >= pivotCool) { pivotUntil = now + 0.7f; pivotCool = now + 3f; action = "retreat (too close)"; yield return null; continue; }
                    bool tooFast = new Vector3(targetVel.x, 0f, targetVel.z).magnitude >= 1.6f;
                    bool hitBackingOff = now - lastTakenAt < 4f && (lastTakenAct.StartsWith("step out") || lastTakenAct.StartsWith("make room"));
                    if (o.Dance && now >= pivotCool && (tooFast || hitBackingOff))
                    {
                        pivotUntil = now + 0.55f; pivotCool = now + 2.2f;
                        action = "pivot-run"; yield return null; continue;
                    }
                    action = "make room " + SafeBackOff(p, a, 1);
                }
                else if (!(pressureDue && canSwing) && !elevated && !o.HumanStyle && (o.Dance ? (zSwinging && dist < ZombieReach + 0.25f && now >= holdStillUntil) : (zSwinging && dist < ZombieReach && !canSwing)))
                {
                    // Only while our own swing is not ready: a bat hit (about 36) outweighs a zombie hit (about 4-15), so a ready swing always goes first.
                    // A zombie that keeps pace re-swings every time, so the dodge has a budget: after ~1.2 s of
                    // dodging without it whiffing, the next branch trades a hit instead of backing off forever.
                    bool fastAttacker = new Vector3(targetVel.x, 0f, targetVel.z).magnitude >= 1.6f;
                    bool hitWhileStepping = now - lastTakenAt < 4f && (lastTakenAct.StartsWith("step out") || lastTakenAct.StartsWith("make room"));
                    if (o.Dance && now >= pivotCool && (fastAttacker || hitWhileStepping))
                    {
                        pivotUntil = now + 0.55f; pivotCool = now + 2.2f;      // a person turns round and sprints, then turns back to fight
                        action = "pivot-run"; lastAttackAt = now;
                        yield return null; continue;
                    }
                    if (dodgeSince < 0f) dodgeSince = now;
                    action = "step out of its swing " + SafeBackOff(p, a, 1);
                }
                else if (canSwing)
                {
                    // In range and lined up: hit (first, if it's still stepping in - that can stun it).
                    if (powerOk) { powerSwings++; nextPowerAt = now + 3.0f; Swing(a.Secondary, false); action = "power attack"; }
                    else Swing(attack, false);
                    if (action != "power attack") action = closingSpeed > 0.2f ? "hit first" : (now < punishUntil ? "punish" : "hit");
                }
                else if (now < stepOutUntil && dist < (o.HumanStyle ? 2.2f : ZombieReach))
                {
                    // Just hit it: step back out of its reach (human style: keep backing until it can no longer hit us) before it answers.
                    action = "reset " + SafeBackOff(p, a, 1);
                }
                else if (elevated && dist > o.Range - 0.2f)
                {
                    action = "hold the high ground";
                }
                else if (o.HumanStyle && dist < 5f && closingSpeed > -0.2f && !zStunned)
                {
                    // It is coming: stand and wait for it, then hit it as it steps in (never walk back into its reach).
                    float wy2, wp2;
                    RebirthGameBridgePlayer.AnglesTo(p, RebirthGameBridgePlayer.EntityAimPoint(target, 0.3f), out wy2, out wp2);
                    action = "wait for it";
                }
                else if (dist > o.Range - 0.2f)
                {
                    // Close the gap (over safe ground only).
                    if (groundAhead)
                    {
                        RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1);
                        if (dist > 6f && stamina > 50f) RebirthGameBridgeInput.HoldFrames(a.Run, 1);
                        action = "approach";
                    }
                    else action = "wait (unsafe ground toward target)";
                }
                else
                {
                    // In range, waiting for the swing to come back: shuffle sideways a little rather than freeze.
                    if (Mathf.FloorToInt(now / 0.7f) % 3 == 0) RebirthGameBridgeInput.HoldFrames(strafeRight ? a.MoveRight : a.MoveLeft, 1);
                    if (now >= nextStrafeFlip) { strafeRight = !strafeRight; nextStrafeFlip = now + UnityEngine.Random.Range(1.2f, 2.2f); }
                    action = "ready";
                }
            }

            // Incident: in range for 2 s without attacking (aim not landing on it, or something in the way).
            if (dist <= effRange)
            {
                if (closeSince < 0f) closeSince = now;
                float actualAttackAt = ranged ? lastAttackAt : (lastRegAt > 0f ? lastRegAt : start);
                if (!rolling && !recovering && !fleeing && now - closeSince > 3f && now - actualAttackAt > 3f)
                    incident("in range of " + targetName + " for " + (now - actualAttackAt).ToString("0.0") + "s without a registered attack (aimError " + aimError.ToString("0.0") + "deg, crosshair on " + CrosshairName(hi, hitEntity) + ")");
            }
            else closeSince = -1f;

            curActionName = action;
            if (action != lastAction && (action == "approach" || action == "backstep")) lastAction = action;
            Trace(trace, ref nextTrace, now - start, p, target, dist, aimError, onTarget, action, hi, hitEntity);
            yield return null;
        }

        RebirthGameBridgeInput.Release(a.MoveForward);
        RebirthGameBridgeInput.Release(a.MoveBack);
        RebirthGameBridgeInput.Release(a.Run);
        RebirthGameBridgeInput.Release(a.Primary);
        RebirthGameBridgeInput.Release(a.Secondary);
        for (int i = 0; i < 10; i++) yield return null; // let a finishing hit register
        minStamina = Mathf.Min(minStamina, Stamina(p)); // finishing swing can spend stamina after the final loop sample
        activeRoutines--;
        fightsRunning--;

        string tracePath = SaveTrace(trace, o.Source);
        var summary = new JObject
        {
            ["result"] = result,
            ["source"] = o.Source,
            ["kills"] = kills,
            ["lost"] = lost,
            ["player"] = new JObject
            {
                ["healthBefore"] = playerHp0, ["healthAfter"] = p.Health,
                ["staminaBefore"] = Mathf.Round(stamina0), ["staminaMin"] = Mathf.Round(minStamina), ["staminaAfter"] = Mathf.Round(Stamina(p))
            },
            ["swingAnalysis"] = SwingAnalysis(swingLog),
            ["rayProbe"] = new JArray(RebirthGameBridgeMeleeProbe.TakeRecords(80)),
            ["fightFile"] = RebirthGameBridgeFightRecorder.Path,
            ["takenDetail"] = new JArray(takenLog),
            ["bowTimeline"] = string.Join(",", new[] { tBowOut.ToString("0.0"), tFirstDraw.ToString("0.0"), tFirstRelease.ToString("0.0"), tMeleeSwitch.ToString("0.0"), arrowsLoosed.ToString() }),
            ["swingsRegistered"] = swingsRegistered, ["swingsIgnored"] = swingsIgnored, ["swingCycle"] = Math.Round(cycleEst, 2), ["hitDelay"] = Math.Round(hitDelay, 2), ["hitDelaySamples"] = delaySamples,
            ["swingDetail"] = SwingDetail(swingLog),
            ["bowShots"] = bowLog.Count, ["bowHits"] = BowHits(bowLog), ["bowDetail"] = BowDetail(bowLog),
            ["gait"] = gait.Describe(),
            ["swingsOrShots"] = swings, ["reloads"] = reloads, ["hits"] = hits, ["grazes"] = grazes, ["powerSwings"] = powerSwings, ["hitsTaken"] = hitsTaken, ["retreats"] = retreats,
            ["enemySwings"] = enemySwings, ["enemySwingsDodged"] = Math.Max(0, enemySwings - hitsTaken),
            ["seconds"] = (float)Math.Round(Time.realtimeSinceStartup - start, 1),
            ["incidents"] = incidents,
            ["events"] = events,
            ["trace"] = tracePath
        };
        if (o.Source == "guard") GuardLog("fight " + result + ", kills=" + kills.Count + ", incidents=" + incidents.Count + ", health " + playerHp0 + "->" + p.Health);
        RebirthGameBridgeFightRecorder.End();
        bool ok = (result == "killed" || result == "area_clear" || result == "holding") && incidents.Count == 0;
        if (o.HumanStyle && (result == "killed" || result == "area_clear") && !p.IsDead() && req.QueryBool("autoLoot", true))   // loot right after the kills, as a player would
            RebirthGameBridgePump.Instance.Run(RebirthGameBridgeLootBags.Routine(p, m => Log.Out("[REBIRTH GameBridge] lootbags: " + m), c => { }));
        req.Complete(summary, ok ? 200 : 409);
    }

    // ------------------------------------------------------------------ spatial awareness

    private static Vector3 FlatDir(float yaw) { return Quaternion.Euler(0f, yaw, 0f) * Vector3.forward; }

    /// <summary>Signed bearing of a point relative to where the player faces: 0 ahead, +90 right, ±180 behind.</summary>
    private static float Bearing(EntityPlayerLocal p, Vector3 point)
    {
        Vector3 to = point - p.position;
        return Mathf.DeltaAngle(p.rotation.y, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
    }

    /// <summary>Is there an awake enemy within `within` metres in a cone of ±coneDeg around a flat direction?</summary>
    private static bool EnemyInDirection(EntityPlayerLocal p, Vector3 dir, float within, float coneDeg)
    {
        foreach (Entity e in p.world.Entities.list)
        {
            var a = e as EntityAlive;
            if (!IsThreat(a, p)) continue;
            Vector3 to = a.position - p.position;
            to.y = 0f;
            if (to.magnitude > within || to.sqrMagnitude < 0.0001f) continue;
            if (Vector3.Angle(dir, to) <= coneDeg) return true;
        }
        return false;
    }

    /// <summary>Direction away from all threats within `radius` (closer ones weigh more), flat.</summary>
    private static Vector3 EscapeDirection(EntityPlayerLocal p, float radius, EntityAlive fallbackFrom)
    {
        Vector3 sum = Vector3.zero;
        foreach (Entity e in p.world.Entities.list)
        {
            var a = e as EntityAlive;
            if (!IsThreat(a, p)) continue;
            Vector3 away = p.position - a.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d > radius || d < 0.01f) continue;
            sum += away / (d * d);
        }
        if (sum.sqrMagnitude < 1e-6f && fallbackFrom != null) { sum = p.position - fallbackFrom.position; sum.y = 0f; }
        return sum.sqrMagnitude > 1e-6f ? sum.normalized : FlatDir(p.rotation.y + 180f);
    }

    /// <summary>
    /// Step back only if nothing is behind; otherwise sidestep toward a clear side; otherwise hold.
    /// Returns what it did.
    /// </summary>
    /// <summary>
    /// Escape heading: away from the threats, but only over ground that is safe to run on (no big drops, walls
    /// or cliffs; steers around them) and preferring directions without other enemies. `safe` is false when no
    /// safe direction exists within 4 m (then don't run blindly).
    /// </summary>
    private static Vector3 SafeEscape(EntityPlayerLocal p, Vector3 away, out bool safe, out bool jump)
    {
        RebirthGameBridgeTerrain.Line line;
        Vector3 d = RebirthGameBridgeTerrain.BestDirection(p.position, away, 4f, out line,
            dir => EnemyInDirection(p, dir, 8f, 35f) ? 1f : 0f);
        safe = line.Safe || line.SafeLength >= 2f;
        jump = line.JumpNeeded;
        return d;
    }

    /// <summary>Is the ground `metres` in this flat direction safe to step onto?</summary>
    private static bool GroundSafe(EntityPlayerLocal p, Vector3 dir, float metres)
    {
        return RebirthGameBridgeTerrain.Probe(p.position, dir, metres).Safe;
    }

    /// <summary>Metres above the natural ground under the player (towers and pillars count; the target's height does not).</summary>
    // The tower we built (set by /pillar when it finishes): high ground only counts while we stand on it.
    private static bool towerActive;
    private static Vector3 towerTop;

    private static float HeightAboveTerrain(EntityPlayerLocal p)
    {
        if (!towerActive) return 0f;
        float flat = Mathf.Sqrt((p.position.x - towerTop.x) * (p.position.x - towerTop.x) + (p.position.z - towerTop.z) * (p.position.z - towerTop.z));
        if (flat > 1.6f || Mathf.Abs(p.position.y - towerTop.y) > 1.3f) { towerActive = false; return 0f; }   // we left it
        return 4f;
    }

    private static int CountThreats(EntityPlayerLocal p, float radius)
    {
        int n = 0; float r2 = radius * radius;
        foreach (Entity e in p.world.Entities.list)
        {
            var a = e as EntityAlive;
            if (IsThreat(a, p) && (a.position - p.position).sqrMagnitude <= r2) n++;
        }
        return n;
    }

    private static string SafeBackOff(EntityPlayerLocal p, PlayerActionsLocal a, int frames)
    {
        float yaw = p.rotation.y;
        // Step away from the WHOLE pack, not just straight behind us: of back / left / right, take the direction that points
        // most away from everything close (that also keeps the zombies lined up instead of flanking), as long as nothing
        // stands there and the ground is safe. Otherwise hold.
        Vector3 away = EscapeDirection(p, 7f, null);
        bool packNear = CountThreats(p, 7f) >= 2;
        Vector3[] dirs = { FlatDir(yaw + 180f), FlatDir(yaw + 90f), FlatDir(yaw - 90f) };
        string[] names = { "backstep", "sidestep-right", "sidestep-left" };
        float[] within = { 3.5f, 3f, 3f }, cone = { 70f, 60f, 60f };
        int best = -1; float bestDot = -0.3f;
        for (int i = 0; i < 3; i++)
        {
            if (EnemyInDirection(p, dirs[i], within[i], cone[i]) || !GroundSafe(p, dirs[i], 1.5f)) continue;
            // With a pack coming, back straight up (they follow in a line, one behind the other) and do not strafe: circling spreads
            // them out and lets them flank. A lone zombie: take whichever direction points most away.
            float dot = Vector3.Dot(dirs[i], away) + (i == 0 ? (packNear ? 1.2f : 0.15f) : 0f);
            if (dot > bestDot) { bestDot = dot; best = i; }
        }
        if (best < 0) return "hold (no safe ground to step to)";
        RebirthGameBridgeInput.HoldFrames(best == 0 ? a.MoveBack : best == 1 ? a.MoveRight : a.MoveLeft, frames);
        return names[best];
    }

    /// <summary>Awake enemies within radius with distance, bearing (0 ahead, ±180 behind) and whether they approach.</summary>
    public static JArray ScanThreats(EntityPlayerLocal p, float radius)
    {
        var list = new List<KeyValuePair<float, JObject>>();
        foreach (Entity e in p.world.Entities.list)
        {
            var a = e as EntityAlive;
            if (a == null || a == p || a.IsDead() || !(a is EntityEnemy) || IsControlledAlly(a, p)) continue;
            float d = Flat(a.position, p.position);
            if (d > radius) continue;
            Vector3 toPlayer = p.position - a.position; toPlayer.y = 0f;
            Vector3 v = a.motion; v.y = 0f;
            float bearing = Bearing(p, a.position);
            list.Add(new KeyValuePair<float, JObject>(d, new JObject
            {
                ["id"] = a.entityId, ["class"] = ClassName(a), ["distance"] = (float)Math.Round(d, 1),
                ["bearing"] = Mathf.Round(bearing),
                ["side"] = Mathf.Abs(bearing) <= 45f ? "ahead" : Mathf.Abs(bearing) >= 135f ? "behind" : bearing > 0f ? "right" : "left",
                ["awake"] = !a.IsSleeping,
                ["approaching"] = v.sqrMagnitude > 1e-5f && Vector3.Dot(v.normalized, toPlayer.normalized) > 0.5f,
                ["health"] = a.Health
            }));
        }
        list.Sort((x, y) => x.Key.CompareTo(y.Key));
        var arr = new JArray();
        foreach (var kv in list) arr.Add(kv.Value);
        return arr;
    }

    // Melee dance distances (metres, flat). Zombies start their swing roughly inside ~2 m; the bat reaches ~2.3.
    private const float ZombieReach = 1.9f;   // a zombie swing connects inside roughly this distance (with margin)

    /// <summary>
    /// Dead, or dying: health at 0 / dead ragdoll. There are a few frames after a lethal hit where IsDead() is
    /// still false but the body is already a ragdoll - don't keep swinging at it.
    /// </summary>
    private static bool IsDeadOrDying(EntityAlive e)
    {
        if (e == null || e.IsDead() || e.Health <= 0) return true;
        try { return e.emodel != null && e.emodel.IsRagdollDead; } catch { return false; }
    }   // a zombie swing only connects inside roughly this distance

    /// <summary>Is the enemy in its attack animation (winding up / swinging)?</summary>
    public static bool TargetSwinging(EntityAlive e)
    {
        try
        {
            AvatarController ac = e.emodel != null ? e.emodel.avatarController : null;
            return ac != null && (ac.IsAnimationAttackPlaying() || ac.IsAnimationSpecialAttackPlaying());
        }
        catch { return false; }
    }

    /// <summary>Stunned or knocked down (ragdoll): a free window to hit it.</summary>
    public static bool TargetStunned(EntityAlive e)
    {
        try
        {
            EModelBase m = e.emodel;
            if (m == null) return false;
            if (m.IsRagdollDead || e.Health <= 0) return false; // a corpse is not "stunned"
            if (m.IsRagdollActive) return true;
            return m.avatarController != null && m.avatarController.IsAnimationStunRunning();
        }
        catch { return false; }
    }

    private static bool IsPlantBlock(WorldRayHitInfo hi)
    {
        try
        {
            BlockValue bv = GameManager.Instance.World.GetBlock(hi.hit.blockPos.x, hi.hit.blockPos.y, hi.hit.blockPos.z);
            if (bv.isair) return false;
            string n = bv.Block.GetBlockName();
            return n.IndexOf("Grass", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Plant", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("Shrub", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Flower", StringComparison.OrdinalIgnoreCase) >= 0
                || string.Equals(n, "treeAzalea", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string CrosshairName(WorldRayHitInfo hi, Entity hitEntity)
    {
        if (hi == null || !hi.bHitValid) return "nothing";
        if (hitEntity != null) return "entity " + (hitEntity.EntityClass != null ? hitEntity.EntityClass.entityClassName : hitEntity.GetType().Name) + " #" + hitEntity.entityId;
        try
        {
            BlockValue bv = GameManager.Instance.World.GetBlock(hi.hit.blockPos.x, hi.hit.blockPos.y, hi.hit.blockPos.z);
            return "block " + (bv.isair ? "air" : bv.Block.GetBlockName());
        }
        catch { return "block ?"; }
    }

    /// <summary>5 samples/second of what the fight loop saw and did (for diagnosing aim and movement).</summary>
    private static void Trace(JArray trace, ref float nextTrace, float t, EntityPlayerLocal p, EntityAlive target, float dist,
        float aimError, bool onTarget, string action, WorldRayHitInfo hi = null, Entity hitEntity = null)
    {
        if (t < nextTrace || trace.Count >= 1500) return;
        nextTrace = t + 0.2f;
        trace.Add(new JObject
        {
            ["t"] = (float)Math.Round(t, 2),
            ["target"] = target != null ? ClassName(target) + "#" + target.entityId : null,
            ["dist"] = (float)Math.Round(dist, 2),
            ["aimErr"] = (float)Math.Round(aimError, 2),
            ["onTarget"] = onTarget,
            ["crosshair"] = hi != null ? CrosshairName(hi, hitEntity) : null,
            ["yaw"] = (float)Math.Round(p.rotation.y, 1),
            ["pitch"] = (float)Math.Round(p.rotation.x, 1),
            ["action"] = action,
            ["hp"] = p.Health,
            ["stamina"] = Mathf.Round(Stamina(p))
        });
    }

    private static string SaveTrace(JArray trace, string source)
    {
        try
        {
            string dir = Path.Combine(RebirthGameBridge.OutputDir, "traces");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd-HHmmss") + "_fight_" + source + ".json");
            File.WriteAllText(file, trace.ToString(Newtonsoft.Json.Formatting.None));
            return file;
        }
        catch { return null; }
    }

    // ------------------------------------------------------------------ flee

    /// <summary>Run directly away from ?entity (or the nearest enemy) until ?distance or ?maxSeconds.</summary>
    private static void Flee(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        EntityAlive threat = ResolveEnemy(req, p);
        if (threat == null) { req.Fail("no enemy found to flee from", 404); return; }
        RebirthGameBridgePump.Instance.Run(FleeRoutine(req, p, threat, req.QueryFloat("distance", 20f), req.QueryFloat("maxSeconds", 20f), ++combatGeneration, "bridge"));
    }

    private static IEnumerator FleeRoutine(BridgeRequest req, EntityPlayerLocal p, EntityAlive threat, float safeDistance, float maxSeconds, int generation, string source)
    {
        activeRoutines++;
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        float start = Time.realtimeSinceStartup;
        Vector3 lastPos = p.position;
        float lastProgress = start, nextProbe = 0f;
        Vector3 fleeDir = FlatDir(p.rotation.y + 180f);
        bool fleeSafe = true;
        string result = null;
        while (result == null)
        {
            if (generation != combatGeneration) { result = "superseded"; break; }
            if (p.IsDead()) { result = "player_died"; break; }
            if (threat == null || threat.IsDead()) { result = "threat_gone"; break; }
            float dist = Flat(threat.position, p.position);
            if (dist >= safeDistance) { result = "safe"; break; }
            if (Time.realtimeSinceStartup - start > maxSeconds) { result = "timeout"; break; }

            if (Time.realtimeSinceStartup >= nextProbe)
            {
                nextProbe = Time.realtimeSinceStartup + 0.15f;
                bool jump;
                // Away from every nearby threat, over ground that is safe to run on (no ledges/cliffs).
                fleeDir = SafeEscape(p, EscapeDirection(p, 15f, threat), out fleeSafe, out jump);
                if (jump && p.onGround) RebirthGameBridgeInput.HoldFrames(a.Jump, 4);
            }
            float yaw = Mathf.Atan2(fleeDir.x, fleeDir.z) * Mathf.Rad2Deg;
            RebirthGameBridgePlayer.SmoothTurn(p, yaw, -5f, 0.13f);
            float turnErr = Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, yaw));
            if (turnErr < 70f && fleeSafe) RebirthGameBridgeInput.HoldFrames(a.MoveForward, 1);
            if (turnErr < 30f && fleeSafe && Stamina(p) > 5f) RebirthGameBridgeInput.HoldFrames(a.Run, 1);

            if ((p.position - lastPos).sqrMagnitude > 0.16f) { lastPos = p.position; lastProgress = Time.realtimeSinceStartup; }
            else if (Time.realtimeSinceStartup - lastProgress > 0.8f) { RebirthGameBridgeInput.HoldFrames(a.Jump, 6); lastProgress = Time.realtimeSinceStartup; }
            yield return null;
        }
        RebirthGameBridgeInput.Release(a.MoveForward);
        RebirthGameBridgeInput.Release(a.Run);
        activeRoutines--;
        if (source == "guard") GuardLog("flee " + result + ", health " + p.Health);
        req.Complete(new JObject
        {
            ["result"] = result,
            ["distance"] = threat != null ? (float)Math.Round(Flat(threat.position, p.position), 1) : -1f,
            ["health"] = p.Health,
            ["stamina"] = Mathf.Round(Stamina(p)),
            ["seconds"] = (float)Math.Round(Time.realtimeSinceStartup - start, 1)
        }, result == "safe" || result == "threat_gone" ? 200 : 409);
    }

    // ------------------------------------------------------------------ guard reflex

    private static void GuardLog(string msg)
    {
        guardLog.Add(DateTime.Now.ToString("HH:mm:ss") + " " + msg);
        if (guardLog.Count > 40) guardLog.RemoveAt(0);
        Log.Out("[REBIRTH GameBridge] guard: " + msg);
    }

    public static JObject GuardStatus()
    {
        return new JObject
        {
            ["enabled"] = GuardEnabled, ["distance"] = GuardDistance, ["pace"] = RebirthGameBridgeInput.Pace, ["heal"] = InstinctHeal, ["reload"] = InstinctReload, ["busy"] = activeRoutines > 0,
            ["needs"] = PrimaryPlayer() != null ? RebirthGameBridgeNeeds.Status(PrimaryPlayer()) : null,
            ["events"] = new JArray(guardLog.ToArray())
        };
    }

    /// <summary>GET /guard: status + recent events. POST /guard?enabled=0|1&amp;distance=6.</summary>
    private static void Guard(BridgeRequest req)
    {
        if (req.Method == "POST")
        {
            GuardEnabled = req.QueryBool("enabled", GuardEnabled);
            InstinctHeal = req.QueryBool("heal", InstinctHeal);
            InstinctReload = req.QueryBool("reload", InstinctReload);
            GlanceWhenIdle = req.QueryBool("glance", GlanceWhenIdle);
            InstinctSupply = req.QueryBool("supply", InstinctSupply);
            RebirthGameBridgeNeeds.Supply = InstinctSupply;
            RebirthGameBridgeNeeds.Enabled = req.QueryBool("needs", RebirthGameBridgeNeeds.Enabled);
            RebirthGameBridgeAgenda.Enabled = req.QueryBool("agenda", RebirthGameBridgeAgenda.Enabled);
            RebirthGameBridgeWorld.ExploreEnabled = req.QueryBool("explore", RebirthGameBridgeWorld.ExploreEnabled);
            RebirthGameBridgeInput.Pace = Mathf.Clamp(req.QueryFloat("pace", RebirthGameBridgeInput.Pace), 0.1f, 3f);
            GuardDistance = Mathf.Clamp(req.QueryFloat("distance", GuardDistance), 2f, 30f);
        }
        req.Complete(GuardStatus());
    }

    private static bool hadLivePlayer;
    private static Vector3 lastGuardPos;
    private static float arrivalUntil, lastBusyAt, nextGlance;
    public static bool GlanceWhenIdle = false; // only turn with a reason (e.g. facing an approaching zombie)
    private static float nextMenuCloseAt, nextUrgentCookCheck;
    // Flee bookkeeping per enemy: how often we ran from it recently (to break flee -> return -> flee loops).
    private static readonly Dictionary<int, KeyValuePair<int, float>> fleeHistory = new Dictionary<int, KeyValuePair<int, float>>();

    private static int RecentFlees(EntityAlive e)
    {
        KeyValuePair<int, float> h;
        return fleeHistory.TryGetValue(e.entityId, out h) && Time.realtimeSinceStartup - h.Value < 120f ? h.Key : 0;
    }

    private static void NoteFlee(EntityAlive e)
    {
        fleeHistory[e.entityId] = new KeyValuePair<int, float>(RecentFlees(e) + 1, Time.realtimeSinceStartup);
    }

    private static bool IsApproaching(EntityAlive e, EntityPlayerLocal p)
    {
        Vector3 v = e.motion; v.y = 0f;
        Vector3 to = p.position - e.position; to.y = 0f;
        return v.sqrMagnitude > 1e-5f && to.sqrMagnitude > 1e-4f && Vector3.Dot(v.normalized, to.normalized) > 0.5f;
    }

    /// <summary>Turn smoothly to face an approaching enemy (keeps threats in front, like a player would).</summary>
    private static IEnumerator WatchRoutine(EntityPlayerLocal p, EntityAlive e)
    {
        activeRoutines++;
        GuardLog("turning to watch " + ClassName(e) + " #" + e.entityId + " approaching from the " + (Mathf.Abs(Bearing(p, e.position)) >= 135f ? "back" : "side") + " (" + Flat(e.position, p.position).ToString("0") + "m)");
        float end = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < end && e != null && !e.IsDead() && !p.IsDead())
        {
            if (RebirthGameBridgePlayer.SmoothAimAt(p, RebirthGameBridgePlayer.EntityAimPoint(e, 0.3f), 0.2f) < 3f) break;
            yield return null;
        }
        activeRoutines--;
    }

    /// <summary>GET /terrain?distance=4: the ground in 8 directions (relative to facing) + how flat this spot is.</summary>
    // Injuries and illnesses lower the maximum health until they are treated (that is what Health Capacity reflects). The item that cures each:
    // bleeding/abrasion -> bandage, laceration -> first aid kit, sprain -> splint, break -> plaster cast, concussion -> painkillers, infection -> antibiotics.
    private static readonly string[][] Cures =
    {
        new[] { "buffInjuryBleeding", "medicalBandage" }, new[] { "buffLaceration", "medicalFirstAidKit" }, new[] { "buffInjuryAbrasion", "medicalBandage" },
        new[] { "buffLegSprained", "medicalSplint" }, new[] { "buffArmSprained", "medicalSplint" },
        new[] { "buffLegBroken", "medicalPlasterCast" }, new[] { "buffArmBroken", "medicalPlasterCast" },
        new[] { "buffInjuryConcussion", "drugPainkillers" },
        new[] { "buffInfectionMain", "drugAntibiotics" }, new[] { "buffInfection01Untreated", "drugAntibiotics" }, new[] { "buffInfection02Untreated", "drugAntibiotics" },
        new[] { "buffInfection03Untreated", "drugAntibiotics" }, new[] { "buffInfection04", "drugAntibiotics" }
    };

    /// <summary>The first untreated injury/illness we know a cure for: its buff name and the item that treats it (null when none).</summary>
    public static bool NextTreatment(EntityPlayerLocal p, out string buff, out string item)
    {
        buff = null; item = null;
        foreach (BuffValue bv in p.Buffs.ActiveBuffs)
        {
            string n = bv != null && bv.BuffClass != null ? bv.BuffClass.Name : null;
            if (string.IsNullOrEmpty(n) || n.EndsWith("Treated", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (string[] c in Cures) if (string.Equals(n, c[0], StringComparison.OrdinalIgnoreCase)) { buff = n; item = c[1]; return true; }
        }
        return false;
    }

    /// <summary>
    /// POST /treat[?supply=1] : treat the injuries and illnesses that reduce the maximum health, like a player: for each, take the right
    /// medical item from the toolbelt and use it. With supply (test mode, default on) a missing item is first put into the last toolbelt
    /// slot (the displaced item goes to the backpack). Result: what was used, what is still untreated.
    /// </summary>
    private static void Treat(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        RebirthGameBridgePump.Instance.Run(TreatRoutine(req, p, req.QueryBool("supply", true)));
    }

    private static IEnumerator TreatRoutine(BridgeRequest req, EntityPlayerLocal p, bool supply)
    {
        activeRoutines++;
        var used = new JArray(); var failed = new JArray();
        var tried = new HashSet<string>();
        for (int step = 0; step < 10 && !p.IsDead(); step++)
        {
            string buff, item;
            if (!NextTreatment(p, out buff, out item)) break;
            if (!tried.Add(buff)) break;   // one attempt per injury: no loops
            int slot = -1;
            ItemStack[] slots = p.inventory.ItemGrid.items;
            for (int i = 0; i < slots.Length && i < RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.Length); i++)
                if (slots[i] != null && !slots[i].IsEmpty() && slots[i].itemValue.ItemClass != null && slots[i].itemValue.ItemClass.GetItemName() == item) { slot = i; break; }
            if (slot < 0 && supply)
            {
                ItemValue proto = ItemClass.GetItem(item, true);
                if (proto != null && !proto.IsEmpty())
                {
                    int last = RebirthGameBridgeNeeds.ToolbeltSize(p) - 1;
                    ItemStack existing = p.inventory.GetItem(last);
                    if (existing != null && !existing.IsEmpty()) p.bag.AddItem(existing.Clone());
                    p.inventory.SetItem(last, new ItemStack(new ItemValue(proto.type, 1, 1, true), 2));
                    p.inventory.CallOnToolbeltChangedInternal();
                    slot = last;
                    yield return Wait(0.2f);
                }
            }
            if (slot < 0) { failed.Add(buff + " (no " + item + ")"); continue; }
            int previous = p.inventory.holdingItemIdx;
            bool equipped = false, consumed = false;
            yield return RebirthGameBridgeNeeds.Equip(p, slot, item, ok => equipped = ok);
            if (equipped) yield return RebirthGameBridgeNeeds.UseHeld(p, item, ok => consumed = ok);
            if (previous != slot && previous >= 0) yield return RebirthGameBridgeNeeds.Equip(p, previous, null, ok => { });
            (consumed ? used : failed).Add(buff + " <- " + item);
            yield return Wait(0.5f);
        }
        string leftBuff, leftItem;
        var left = new JArray();
        foreach (BuffValue bv in p.Buffs.ActiveBuffs)
        {
            string n = bv != null && bv.BuffClass != null ? bv.BuffClass.Name : null;
            if (string.IsNullOrEmpty(n) || n.EndsWith("Treated", StringComparison.Ordinal)) continue;
            foreach (string[] c in Cures) if (n == c[0]) left.Add(n);
        }
        activeRoutines--;
        req.Complete(new JObject { ["used"] = used, ["failed"] = failed, ["untreated"] = left, ["health"] = p.Health, ["maxHealth"] = Mathf.Round(p.Stats.Health.ModifiedMax) });
    }

    /// <summary>
    /// POST /pillar?height=4[&amp;item=frameShapes:cube] : pillar up like a player: hold the building blocks, look straight down, jump, place a block under
    /// the feet at the top of the jump, land on it, repeat. Zombies can not hit someone standing a few blocks up; shoot down from there.
    /// Blocks must be in the toolbelt. Result: how many blocks went in and the height gained.
    /// </summary>
    private static void Pillar(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        string item = req.QueryString("item") ?? "frameShapes:cube";
        int slot = -1;
        ItemStack[] slots = p.inventory.ItemGrid.items;
        for (int i = 0; i < slots.Length && i < RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.Length); i++)
            if (slots[i] != null && !slots[i].IsEmpty() && slots[i].itemValue.ItemClass != null && slots[i].itemValue.ItemClass.GetItemName() == item) { slot = i; break; }
        if (slot < 0) { req.Fail(item + " is not in the toolbelt", 404); return; }
        RebirthGameBridgePump.Instance.Run(PillarRoutine(req, p, slot, item, Mathf.Clamp(req.QueryInt("height", 4), 1, 12)));
    }

    /// <summary>Walk to the middle of the block cell we stand in (tiny steps), so the jump and the new block are centred: from a corner a landing slips off.</summary>
    private static IEnumerator CenterOnBlock(EntityPlayerLocal p, PlayerActionsLocal a)
    {
        float end = Time.realtimeSinceStartup + 2.5f;
        while (Time.realtimeSinceStartup < end && !p.IsDead())
        {
            float cx = Mathf.Floor(p.position.x) + 0.5f, cz = Mathf.Floor(p.position.z) + 0.5f;
            float dx = cx - p.position.x, dz = cz - p.position.z, d = Mathf.Sqrt(dx * dx + dz * dz);
            if (d < 0.1f) yield break;
            float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            RebirthGameBridgePlayer.SmoothTurn(p, yaw, 10f, 0.05f);
            if (Mathf.Abs(Mathf.DeltaAngle(p.rotation.y, yaw)) < 12f) RebirthGameBridgeInput.HoldFrames(a.MoveForward, d > 0.3f ? 2 : 1);
            yield return null;
        }
    }

    public static IEnumerator PillarRoutine(BridgeRequest req, EntityPlayerLocal p, int slot, string item, int height)
    {
        activeRoutines++;
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        bool equipped = false;
        yield return RebirthGameBridgeNeeds.Equip(p, slot, item, ok => equipped = ok);
        float startY = p.position.y;
        int placed = 0;
        string why = equipped ? "" : "could not get " + item + " in hand";
        int hp0 = p.Health;
        for (int i = 0; i < height && why.Length == 0 && !p.IsDead(); i++)
        {
            // Building takes a few seconds of standing still: never while something is on us. One attempt per block, no spamming.
            if (CountThreats(p, 7f) > 0) { why = "enemies are close - stopped after " + placed + " block(s)"; break; }
            if (p.Health < hp0 - 2) { why = "taking damage - stopped after " + placed + " block(s)"; break; }
            yield return CenterOnBlock(p, a);
            // Stand still, look straight down at the block under us.
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 0.5f || !p.onGround)
            {
                RebirthGameBridgePlayer.SmoothTurn(p, p.rotation.y, -85f, 0.05f);
                if (Time.realtimeSinceStartup - t0 > 2f) break;
                yield return null;
            }
            float y0 = p.position.y;
            RebirthGameBridgeInput.HoldFrames(a.Jump, 3);
            // Wait for the top of the jump (the block can only go in once the feet are above its cell).
            float tj = Time.realtimeSinceStartup, lastY = y0;
            while (Time.realtimeSinceStartup - tj < 0.8f)
            {
                RebirthGameBridgePlayer.SmoothTurn(p, p.rotation.y, -85f, 0.05f);
                float y = p.position.y;
                if (y > y0 + 0.95f || (y < lastY - 0.002f && y > y0 + 0.5f)) break;
                lastY = y;
                yield return null;
            }
            RebirthGameBridgeInput.HoldFrames(a.Secondary, 3);   // place the block under our feet
            float tl = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - tl < 1.2f) { RebirthGameBridgePlayer.SmoothTurn(p, p.rotation.y, -85f, 0.05f); yield return null; if (p.onGround && Time.realtimeSinceStartup - tl > 0.3f) break; }
            if (p.position.y > y0 + 0.3f) placed++;   // sloped ground can make the first block add less than a full metre
            else if (!p.onGround) { yield return Wait(1.0f); if (p.position.y > y0 + 0.3f) placed++; }
            else why = "block " + (i + 1) + " did not go in (height gain " + (p.position.y - y0).ToString("0.00") + ")";
        }
        if (placed >= 2 && p.onGround) { towerActive = true; towerTop = p.position; }
        activeRoutines--;
        req.Complete(new JObject { ["placed"] = placed, ["heightGain"] = Math.Round(p.position.y - startY, 2), ["error"] = why.Length > 0 ? why : null });
    }

    /// <summary>
    /// GET/POST /zombiespeed[?day=0..4&amp;night=0..4&amp;bloodmoon=0..4] : read or set how fast zombies move (0 walk, 1 jog, 2 run, 3 sprint, 4 nightmare)
    /// in daylight, in the dark and on a blood moon. The game reads these live, so already spawned zombies change too.
    /// </summary>
    /// <summary>POST /lootbags : (only when nothing awake is near) walk to every dropped loot bag, open it, take all, and throw away junk if that leaves the player encumbered.</summary>
    private static void LootBagsEndpoint(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        RebirthGameBridgePump.Instance.Run(LootBagsRequest(req, p));
    }

    private static IEnumerator LootBagsRequest(BridgeRequest req, EntityPlayerLocal p)
    {
        var events = new JArray(); int n = 0;
        yield return RebirthGameBridgeLootBags.Routine(p, m => { events.Add(m); Log.Out("[REBIRTH GameBridge] lootbags: " + m); }, c => n = c);
        req.Complete(new JObject { ["bagsLooted"] = n, ["encumbered"] = RebirthGameBridgeLootBags.Encumbered(p), ["events"] = events });
    }

    /// <summary>POST /enemyspawns?on=0|1 : switch the natural enemy spawning (sleepers, hordes, wandering) off or on. Zombies spawned with /spawn are not affected.</summary>
    private static void EnemySpawns(BridgeRequest req)
    {
        if (req.QueryString("on") != null)
        {
            bool on = req.QueryBool("on", true);
            EntityFactory.EnemySpawnMode = on;
            try { GameStats.Set(EnumGameStats.EnemySpawnMode, on); } catch { }
            try { GamePrefs.Set(EnumGamePrefs.EnemySpawnMode, on); } catch { }
        }
        req.Complete(new JObject { ["enemySpawns"] = EntityFactory.EnemySpawnMode });
    }

    private static void ZombieSpeed(BridgeRequest req)
    {
        int v;
        if (int.TryParse(req.QueryString("day"), out v)) GamePrefs.Set(EnumGamePrefs.ZombieMove, Mathf.Clamp(v, 0, 4));
        if (int.TryParse(req.QueryString("night"), out v)) GamePrefs.Set(EnumGamePrefs.ZombieMoveNight, Mathf.Clamp(v, 0, 4));
        if (int.TryParse(req.QueryString("bloodmoon"), out v)) GamePrefs.Set(EnumGamePrefs.ZombieBMMove, Mathf.Clamp(v, 0, 4));
        bool dark = false;
        try { dark = GameManager.Instance.World.IsDark(); } catch { }
        req.Complete(new JObject
        {
            ["day"] = GamePrefs.GetInt(EnumGamePrefs.ZombieMove), ["night"] = GamePrefs.GetInt(EnumGamePrefs.ZombieMoveNight),
            ["bloodmoon"] = GamePrefs.GetInt(EnumGamePrefs.ZombieBMMove), ["feral"] = GamePrefs.GetInt(EnumGamePrefs.ZombieFeralMove), ["isDarkNow"] = dark
        });
    }

    /// <summary>
    /// POST /use?item=drinkJarCoffee[&amp;unlessBuff=buffCoffee] : take a consumable from the toolbelt in hand, use it like a player (equip, use,
    /// wait for the animation) and put the previous item back. With unlessBuff the call does nothing while that buff is active.
    /// </summary>
    private static void UseItemEndpoint(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        string item = req.QueryString("item");
        if (string.IsNullOrEmpty(item)) { req.Fail("item= required", 400); return; }
        string unless = req.QueryString("unlessBuff");
        if (!string.IsNullOrEmpty(unless) && p.Buffs.HasBuff(unless)) { req.Complete(new JObject { ["used"] = false, ["reason"] = unless + " already active" }); return; }
        int slot = -1;
        ItemStack[] slots = p.inventory.ItemGrid.items;
        for (int i = 0; i < slots.Length && i < RebirthToolbeltCapacity.GetOwnedSlotCount(p, p.inventory.Length); i++)
            if (slots[i] != null && !slots[i].IsEmpty() && slots[i].itemValue.ItemClass != null && slots[i].itemValue.ItemClass.GetItemName() == item) { slot = i; break; }
        if (slot < 0) { req.Fail(item + " is not in the toolbelt", 404); return; }
        RebirthGameBridgePump.Instance.Run(UseItemRoutine(req, p, slot, item));
    }

    private static IEnumerator UseItemRoutine(BridgeRequest req, EntityPlayerLocal p, int slot, string item)
    {
        activeRoutines++;
        int previous = p.inventory.holdingItemIdx;
        bool equipped = false, consumed = false;
        yield return RebirthGameBridgeNeeds.Equip(p, slot, item, ok => equipped = ok);
        if (equipped) yield return RebirthGameBridgeNeeds.UseHeld(p, item, ok => consumed = ok);
        if (previous != slot && previous >= 0) yield return RebirthGameBridgeNeeds.Equip(p, previous, null, ok => { });
        activeRoutines--;
        req.Complete(new JObject { ["used"] = consumed, ["equipped"] = equipped, ["item"] = item });
    }

    /// <summary>GET /openground?radius=60 : the flattest, barest (no grass/bushes/trees) spot nearby to fight on, with how cluttered here and there are.</summary>
    private static void OpenGround(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        float score = 0f;
        float flatRange = -1f, flatClutter = -1f;
        Vector3? spot = req.QueryFloat("size", 0f) > 0f
            ? RebirthGameBridgeTerrain.FlatZoneNear(p.position, req.QueryFloat("radius", 60f), req.QueryFloat("size", 14f), out flatRange, out flatClutter)
            : RebirthGameBridgeTerrain.OpenGroundNear(p.position, req.QueryFloat("radius", 60f), out score);
        if (flatRange >= 0f) score = flatRange * 12f + flatClutter * 0.15f;
        if (!spot.HasValue) { req.Fail("no loaded ground nearby", 404); return; }
        req.Complete(new JObject
        {
            ["position"] = new JArray(Math.Round(spot.Value.x, 1), Math.Round(spot.Value.y, 1), Math.Round(spot.Value.z, 1)),
            ["score"] = Math.Round(score, 1),
            ["flatRange"] = Math.Round(flatRange, 2),
            ["surface"] = RebirthGameBridgeTerrain.SurfaceName(Mathf.FloorToInt(spot.Value.x), Mathf.FloorToInt(spot.Value.z)),
            ["clutterThere"] = RebirthGameBridgeTerrain.Clutter(spot.Value, 6f),
            ["clutterHere"] = RebirthGameBridgeTerrain.Clutter(p.position, 6f)
        });
    }
    private static void Terrain(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        float len = req.QueryFloat("distance", 4f);
        string[] names = { "ahead", "ahead-right", "right", "behind-right", "behind", "behind-left", "left", "ahead-left" };
        var dirs = new JArray();
        for (int i = 0; i < 8; i++)
        {
            RebirthGameBridgeTerrain.Line l = RebirthGameBridgeTerrain.Probe(p.position, FlatDir(p.rotation.y + i * 45f), len);
            dirs.Add(new JObject
            {
                ["direction"] = names[i], ["safe"] = l.Safe, ["safeMetres"] = l.SafeLength,
                ["maxDrop"] = (float)Math.Round(l.MaxDrop, 1), ["maxRise"] = (float)Math.Round(l.MaxRise, 1),
                ["why"] = l.Why, ["water"] = l.Water
            });
        }
        req.Complete(new JObject
        {
            ["flatness"] = (float)Math.Round(RebirthGameBridgeTerrain.Flatness(p.position, 2.5f), 1),
            ["onGround"] = p.onGround, ["directions"] = dirs
        });
    }

    /// <summary>GET /surroundings?radius=30: enemies with distance, side (ahead/left/right/behind), awake, approaching.</summary>
    private static void Surroundings(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        JArray scan = ScanThreats(p, req.QueryFloat("radius", 30f));
        req.Complete(new JObject
        {
            ["threats"] = scan, ["count"] = scan.Count, ["health"] = p.Health, ["stamina"] = Mathf.Round(Stamina(p)),
            ["position"] = new JArray((float)Math.Round(p.position.x, 1), (float)Math.Round(p.position.y, 1), (float)Math.Round(p.position.z, 1))
        });
    }

    /// <summary>
    /// Test setup: kill every enemy within ?radius (default 40) so a scenario starts from a known state
    /// (instead of switching the instincts off). Returns how many were removed and what is left.
    /// </summary>
    /// <summary>POST /godmode?on=1|0 - test setup: the player cannot be hurt (keeps stress tests from ending in a death loop).</summary>
    private static void GodMode(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        bool on = req.QueryBool("on", true);
        p.IsGodMode.Value = on;
        req.Complete(new JObject { ["godMode"] = p.IsGodMode.Value });
    }

    /// <summary>POST /mount?entity=ID - test setup: seat the player on a vehicle (full tank); the engine starts as it does for a driver.</summary>
    private static void Mount(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        int id = req.QueryInt("entity", -1);
        var veh = p.world.GetEntity(id) as EntityVehicle;
        if (veh == null) { req.Fail("no vehicle with id " + id, 404); return; }
        veh.vehicle.SetFuelLevel(veh.vehicle.GetMaxFuelLevel());
        int slot = p.AttachToEntity(veh, -1);
        // The vehicle input set is inserted below whatever is on top of the stack, and only the top set is enabled. For scripted driving,
        // make the vehicle set the active one (vanilla does the same when nothing else sits above it).
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(p);
        if (slot >= 0 && ui != null && ui.playerInput != null)
        {
            foreach (PlayerActionSet s in ui.ActionSetManager.PlayerActions) s.Enabled = false;
            ui.playerInput.VehicleActions.Enabled = true;
        }
        req.Complete(new JObject { ["slot"] = slot, ["fuel"] = veh.vehicle.GetFuelLevel(), ["attached"] = p.AttachedToEntity != null });
    }

    /// <summary>GET /vehicle - diagnostics for the vehicle the player is on (engine, fuel, speed, the input the vehicle sees).</summary>
    private static void VehicleInfo(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        var inv = p.inventory;
        int focused = -999;
        try { LocalPlayerUI fui = LocalPlayerUI.GetUIForPlayer(p); focused = fui.xui.PlayerInventory.Toolbelt.GetFocusedItemIdx(); } catch { }
        var invInfo = new JObject { ["holdingItemIdx"] = inv.holdingItemIdx, ["slotsLength"] = inv.Length, ["handMode"] = inv.Hand.mode.ToString(), ["publicSlots"] = RebirthToolbeltCapacity.GetOwnedSlotCount(p, inv.Length), ["focusedItemIdx"] = focused };
        var veh = p.AttachedToEntity as EntityVehicle;
        if (veh == null) { req.Complete(new JObject { ["attached"] = false, ["inventory"] = invInfo }); return; }
        var vr = veh.vehicleRB;
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(p);
        PlayerActionsVehicle va = ui != null && ui.playerInput != null ? ui.playerInput.VehicleActions : null;
        req.Complete(new JObject
        {
            ["inventory"] = invInfo, ["vehicleSetEnabled"] = va != null && va.Enabled, ["vehicleMoveY"] = va != null ? va.Move.Y : -99f, ["vehicleForwardValue"] = va != null ? va.MoveForward.Value : -99f,
            ["vehicleForwardPressed"] = va != null && va.MoveForward.IsPressed, ["actionSetStack"] = ui != null ? string.Join(" > ", ui.ActionSetManager.PlayerActions.ConvertAll(s => s.GetType().Name + (s.Enabled ? "(on)" : "(off)")).ToArray()) : "",
            ["attached"] = true, ["engineRunning"] = veh.IsEngineRunning, ["fuel"] = veh.vehicle.GetFuelLevel(),
            ["speed"] = vr != null ? vr.velocity.magnitude : -1f, ["rbActive"] = veh.RBActive, ["kinematic"] = vr != null && vr.isKinematic,
            ["moveForward"] = veh.movementInput != null ? veh.movementInput.moveForward : -99f,
            ["moveStrafe"] = veh.movementInput != null ? veh.movementInput.moveStrafe : -99f,
            ["wheelMotor"] = veh.wheelMotor, ["motorTorque"] = veh.motorTorque, ["hasDriver"] = veh.hasDriver,
            ["broken"] = veh.vehicle.GetVehicleQuality() <= 0, ["position"] = new JArray(veh.position.x, veh.position.y, veh.position.z)
        });
    }

    /// <summary>
    /// POST /drive?speed=28&amp;seconds=20&amp;heading=90 - test setup: the mounted vehicle travels along a compass heading (0 = +z, 90 = +x) at the
    /// given speed in m/s. Velocity is set each physics step while gravity, collisions and the vehicle stay real, so chunks stream in as for a
    /// fast rider. Completes with the distance covered and how often the vehicle was held up.
    /// </summary>
    private static void Drive(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        var veh = p.AttachedToEntity as EntityVehicle;
        if (veh == null || veh.vehicleRB == null) { req.Fail("mount a vehicle first (/mount)", 409); return; }
        float speed = req.QueryFloat("speed", 28f), seconds = req.QueryFloat("seconds", 20f), heading = req.QueryFloat("heading", 0f);
        RebirthGameBridgePump.Instance.Run(DriveRoutine(req, p, veh, speed, seconds, heading));
    }

    private static IEnumerator DriveRoutine(BridgeRequest req, EntityPlayerLocal p, EntityVehicle veh, float speed, float seconds, float heading)
    {
        Vector3 start = veh.position, last = start;
        float end = Time.realtimeSinceStartup + seconds, nextCheck = Time.realtimeSinceStartup + 2f;
        int blocked = 0; float travelled = 0f;
        Quaternion face = Quaternion.Euler(0f, heading, 0f);
        Vector3 dir = face * Vector3.forward;
        while (Time.realtimeSinceStartup < end && veh != null && p.AttachedToEntity == veh)
        {
            Rigidbody rb = veh.vehicleRB;
            Vector3 v = rb.velocity;
            rb.velocity = new Vector3(dir.x * speed, v.y, dir.z * speed);
            rb.MoveRotation(face);
            rb.angularVelocity = Vector3.zero;
            if (Time.realtimeSinceStartup >= nextCheck)
            {
                nextCheck = Time.realtimeSinceStartup + 2f;
                Vector3 now = veh.position;
                float d = Mathf.Sqrt((now.x - last.x) * (now.x - last.x) + (now.z - last.z) * (now.z - last.z));
                travelled += d; last = now;
                if (d < speed * 2f * 0.35f)
                {   // held up by something: swing 40 degrees so the next stretch finds a way round
                    blocked++;
                    heading += (blocked % 2 == 0 ? 40f : -40f);
                    face = Quaternion.Euler(0f, heading, 0f); dir = face * Vector3.forward;
                }
            }
            yield return new WaitForFixedUpdate();
        }
        Vector3 fin = veh != null ? veh.position : last;
        req.Complete(new JObject { ["travelled"] = Mathf.Round(travelled), ["straightLine"] = Mathf.Round((fin - start).magnitude), ["blockedCount"] = blocked, ["heading"] = heading });
    }

    /// <summary>POST /fly?on=1|0 - test setup: hover (no gravity), e.g. well above the ground where zombies can target the player but never reach.</summary>
    private static void Fly(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        p.IsFlyMode.Value = req.QueryBool("on", true);
        req.Complete(new JObject { ["fly"] = p.IsFlyMode.Value, ["position"] = new JArray(p.position.x, p.position.y, p.position.z) });
    }

    /// <summary>POST /dismount - test setup: leave the vehicle.</summary>
    private static void Dismount(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        Entity ride = p.AttachedToEntity;
        if (ride != null) p.Detach();
        LocalPlayerUI ui = LocalPlayerUI.GetUIForPlayer(p);
        if (ui != null && !ui.ActionSetManager.Empty) { foreach (PlayerActionSet s in ui.ActionSetManager.PlayerActions) s.Enabled = false; ui.ActionSetManager.Top.Enabled = true; }
        req.Complete(new JObject { ["attached"] = p.AttachedToEntity != null });
    }

    /// <summary>POST /aggro?radius=80 - test setup: every hostile within the radius targets the player right now.</summary>
    private static void Aggro(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        float radius = req.QueryFloat("radius", 80f);
        int n = 0, total = 0;
        foreach (Entity e in new List<Entity>(p.world.Entities.list))
        {
            var a = e as EntityEnemy;
            if (a == null || a.IsDead()) continue;
            total++;
            if (Flat(a.position, p.position) > radius) continue;
            a.SetAttackTarget(p, 6000);
            n++;
        }
        req.Complete(new JObject { ["targeting"] = n, ["hostilesLoaded"] = total });
    }

    private static void ClearArea(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        float radius = req.QueryFloat("radius", 40f);
        var killed = new JArray();
        foreach (Entity e in new List<Entity>(p.world.Entities.list))
        {
            var a = e as EntityAlive;
            if (a == null || a == p || a.IsDead() || !(a is EntityEnemy) || IsControlledAlly(a, p) || Flat(a.position, p.position) > radius) continue;
            a.DamageEntity(new DamageSource(EnumDamageSource.External, EnumDamageTypes.None), 99999, false, 1f);
            killed.Add(ClassName(a));
        }
        req.Complete(new JObject { ["killed"] = killed, ["remaining"] = ScanThreats(p, radius) });
    }

    /// <summary>Called every frame by the pump. Cheap: one scan every 0.2 s.</summary>
    public static void GuardTick()
    {
        if (!GuardEnabled || Time.realtimeSinceStartup < nextGuardCheck) return;
        nextGuardCheck = Time.realtimeSinceStartup + 0.2f;
        float now = Time.realtimeSinceStartup;
        EntityPlayerLocal p = PrimaryPlayer();
        if (p == null || p.IsDead() || !p.IsSpawned()) { hadLivePlayer = false; return; }
        // Keep tracking position while busy, so a long flee/walk isn't mistaken for a teleport ("arrived").
        if (activeRoutines > 0) { lastGuardPos = p.position; lastBusyAt = now; return; }

        // Arrival (spawn, respawn, load, teleport): look around first and report what is here.
        bool arrived = !hadLivePlayer || (p.position - lastGuardPos).sqrMagnitude > 20f * 20f;
        hadLivePlayer = true;
        lastGuardPos = p.position;
        if (arrived)
        {
            arrivalUntil = now + 10f;
            JArray scan = ScanThreats(p, 30f);
            var parts = new List<string>();
            foreach (JToken t in scan)
                parts.Add(t["class"] + " " + t["distance"] + "m " + t["side"] + ((bool)t["awake"] ? "" : " (asleep)") + ((bool)t["approaching"] ? " approaching" : ""));
            GuardLog("arrived: " + (scan.Count == 0 ? "no threats within 30m" : scan.Count + " threat(s) within 30m: " + string.Join(", ", parts.ToArray())));
        }

        // Engage: anything close; while just arrived, anything awake within 12 m; anything approaching within 10 m.
        EntityAlive threat = NearestEnemy(p, now < arrivalUntil ? 12f : GuardDistance);
        if (threat == null)
        {
            EntityAlive near = NearestEnemy(p, 10f);
            if (near != null && IsApproaching(near, p)) threat = near;
        }
        if (threat != null) { RebirthGameBridgePump.Instance.Run(GuardReact(p, threat)); return; }

        // Watch: turn to face an approaching enemy that is off to the side or behind (when not walking somewhere).
        EntityAlive watch = NearestEnemy(p, 20f);
        if (watch != null && IsApproaching(watch, p) && Mathf.Abs(Bearing(p, watch.position)) > 50f
            && !RebirthGameBridgePlayer.IsMoving && !RebirthGameBridgeUi.AnyModalOpen() && !RebirthGameBridgeWorld.RaidActive)
        {
            RebirthGameBridgePump.Instance.Run(WatchRoutine(p, watch));
            return;
        }
        // Urgent beats menus: food about to burn pulls the player out of whatever menu is open.
        float burnLeft;
        if (now >= nextUrgentCookCheck)
        {
            nextUrgentCookCheck = now + 1.5f;
            if (RebirthGameBridgeUi.TryReadBurnTimer(out burnLeft) && burnLeft < 45f && NearestEnemy(p, 12f) == null)
            {
                RebirthGameBridgePump.Instance.Run(Busy(RebirthGameBridgeNeeds.UrgentRescue(p, burnLeft, GuardLog)));
                return;
            }
        }
        if (RebirthGameBridgeUi.AnyModalOpen())
        {
            // Allow time to inspect a screenshot or read a recipe. Threat reactions above
            // still take priority; only idle housekeeping waits for the inspection window.
            if (RebirthGameBridge.LastRequestAge > 120f && now - lastBusyAt > 5f && now >= nextMenuCloseAt && !RebirthGameBridgeWorld.RaidActive)
            {
                nextMenuCloseAt = now + 10f;
                GuardLog("closing a menu nobody is using (" + string.Join(",", RebirthGameBridgeUi.OpenWindowIdList()) + ")");
                RebirthGameBridgePump.Instance.Run(Busy(RebirthGameBridgeUi.CloseMenus()));
            }
            return; // otherwise the agent is busy in a menu
        }
        // Hurt and nobody close: patch up.
        if (InstinctHeal && now >= nextHealAttempt && now >= healFailedUntil && p.Stats != null && (p.Health < p.Stats.Health.ModifiedMax * 0.6f || IsBleeding(p)) && NearestEnemy(p, 10f) == null)
        {
            nextHealAttempt = now + 10f;
            if (!CanHealNow(p)) { /* pending healing will cover it, or nothing would be accepted: wait */ }
            else if (HealSlot(p, true) >= 0) RebirthGameBridgePump.Instance.Run(InstinctHealRoutine(p));
            else if ((BagHasHealItem(p) || InstinctSupply) && now >= nextPrepareAt && NearestEnemy(p, 12f) == null)
            {
                // Like a player: open the inventory, move a bandage to the toolbelt, close, then heal next tick.
                nextHealAttempt = now + 1f;
                RebirthGameBridgePump.Instance.Run(Busy(RebirthGameBridgeNeeds.Prepare(p, RebirthGameBridgeNeeds.IsHealing, InstinctSupply ? "medicalFirstAidBandage" : null, "a healing item", GuardLog)));
            }
            else if (now >= nextNoHealLog)
            {
                nextNoHealLog = now + 60f;
                GuardLog("health " + p.Health + " but " + (BagHasHealItem(p) ? "no safe moment to move a bandage to the toolbelt" : "no healing item"));
            }
            return;
        }
        // Needs (drink, eat) and chores (food about to burn) - only with nobody close and nothing coming for us.
        List<EntityAlive> coming = AwakeThreats(p, 25f, true);
        if (coming.Count > 0 && NearestEnemy(p, 12f) == null && Flat(coming[0].position, p.position) < 18f && !RebirthGameBridgeUi.AnyModalOpen())
        {
            // A zombie is on its way: meet it on our terms (it will be engaged at close range) - face it now.
            if (Mathf.Abs(Bearing(p, coming[0].position)) > 25f && !RebirthGameBridgePlayer.IsMoving && !RebirthGameBridgeWorld.RaidActive)
                RebirthGameBridgePump.Instance.Run(WatchRoutine(p, coming[0]));
            return;
        }
        if (NearestEnemy(p, 12f) == null && coming.Count == 0)
        {
            IEnumerator need = RebirthGameBridgeNeeds.Tick(p, GuardLog);
            if (need != null) { RebirthGameBridgePump.Instance.Run(Busy(need)); return; }
            // Downtime agenda (only when the agent has not been driving for a bit).
            if (RebirthGameBridge.LastRequestAge > 3f && BridgeRequest.OpenCount == 0)
            {
                IEnumerator task = RebirthGameBridgeAgenda.Tick(p, GuardLog);
                if (task != null) { RebirthGameBridgePump.Instance.Run(Busy(task)); return; }
            }
        }
        // Quiet: top up a partly empty magazine.
        ItemValue held = p.inventory.holdingItemItemValue;
        int seen;
        if (InstinctReload && now >= nextReloadAttempt && held != null && IsRanged(held.ItemClass)
            && magazineSeen.TryGetValue(held.type, out seen) && held.Meta < seen && NearestEnemy(p, 15f) == null)
        {
            nextReloadAttempt = now + 15f;
            RebirthGameBridgeInput.HoldFrames(RebirthGameBridgeInput.LocalActions().Reload, 3);
            GuardLog("reloading while quiet (" + held.Meta + "/" + seen + ")");
            return;
        }

        // Idle (no goal from the agent for a while, nothing to do): glance around instead of freezing.
        if (GlanceWhenIdle && now >= nextGlance && !RebirthGameBridgePlayer.IsMoving && RebirthGameBridge.LastRequestAge > 15f && now - lastBusyAt > 15f)
        {
            nextGlance = now + UnityEngine.Random.Range(25f, 45f);
            RebirthGameBridgePump.Instance.Run(GlanceRoutine(p));
        }
    }

    /// <summary>
    /// Move the best healing item from the backpack to an empty toolbelt slot through the real inventory UI
    /// (Tab, click the bandage, click the toolbelt slot, Esc) - what a player does when they realise their
    /// bandages are buried in the backpack.
    /// </summary>
    private static IEnumerator PrepareHealRoutine(EntityPlayerLocal p)
    {
        activeRoutines++;
        nextPrepareAt = Time.realtimeSinceStartup + 30f; // one attempt per 30 s, whatever happens
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();

        // Nothing to heal with at all: in test mode, put a bandage in the backpack first.
        if (HealSlot(p) < 0 && !BagHasHealItem(p) && InstinctSupply)
        {
            ItemValue iv = ItemClass.GetItem("medicalFirstAidBandage", true);
            if (iv != null && !iv.IsEmpty() && p.bag.AddItem(new ItemStack(new ItemValue(iv.type, 1, 1, false), 1)))
                GuardLog("no healing item anywhere - added medicalFirstAidBandage to the backpack (test supply)");
            yield return Wait(0.3f);
        }

        // Open the inventory and wait for it to be up.
        RebirthGameBridgeInput.HoldSeconds(a.PermanentActions.Inventory, 0.12f);
        float until = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < until && !RebirthGameBridgeUi.AnyModalOpen()) yield return null;
        yield return Wait(0.35f);
        RebirthGameBridgeInput.LockCursor = true; // our clicks: don't hand the cursor back mid-move

        string outcome = null;
        string name = null;
        foreach (string n in HealItems) { Vector2 dummy; if (RebirthGameBridgeUi.TryFindItemSlot(n, false, out dummy)) { name = n; break; } }
        if (!RebirthGameBridgeUi.AnyModalOpen()) outcome = "inventory did not open";
        else if (name == null) outcome = "no healing item visible in the backpack";
        else
        {
            Vector2 from, to; int slot;
            // Toolbelt full: make room by moving the least important item into the backpack.
            if (!RebirthGameBridgeUi.TryFindEmptyToolbeltSlot(out to, out slot))
            {
                int free = FreeableToolbeltSlot(p);
                Vector2 slotPt, bagPt;
                if (free >= 0 && RebirthGameBridgeUi.TryFindToolbeltSlot(free, out slotPt) && RebirthGameBridgeUi.TryFindEmptyBackpackSlot(out bagPt))
                {
                    string evicted = p.inventory.GetItem(free).itemValue.ItemClass.GetItemName();
                    yield return RebirthGameBridgeUi.ClickAt(slotPt);
                    yield return RebirthGameBridgeUi.ClickAt(bagPt);
                    GuardLog("toolbelt full - moved " + evicted + " from slot " + (free + 1) + " to the backpack");
                }
            }
            if (!RebirthGameBridgeUi.TryFindEmptyToolbeltSlot(out to, out slot)) outcome = "toolbelt is full and nothing can be moved out";
            else
            {
                for (int attempt = 0; attempt < 2 && outcome == null; attempt++)
                {
                    if (!RebirthGameBridgeUi.TryFindItemSlot(name, false, out from)) { outcome = name + " disappeared from the backpack"; break; }
                    yield return RebirthGameBridgeUi.ClickAt(from);                  // pick up the stack
                    yield return Wait(0.15f);                                        // let the cursor stack update
                    if (RebirthGameBridgeUi.HeldItemName() != name) { yield return Wait(0.2f); continue; } // pick failed - retry
                    yield return RebirthGameBridgeUi.ClickAt(to);                    // drop it in the toolbelt
                    yield return Wait(0.2f);
                    if (HealSlot(p) >= 0) outcome = "ok: moved " + name + " -> toolbelt slot " + (slot + 1);
                    else if (RebirthGameBridgeUi.HeldItemName() != null) { yield return RebirthGameBridgeUi.ClickAt(from); outcome = "place failed (put it back)"; }
                }
                if (outcome == null) outcome = "could not pick up " + name;
            }
        }

        RebirthGameBridgeInput.LockCursor = false;
        RebirthGameBridgeInput.ReleaseCursor();
        RebirthGameBridgeInput.HoldSeconds(a.PermanentActions.Cancel, 0.12f);
        yield return Wait(0.5f);
        bool ok = outcome.StartsWith("ok:");
        GuardLog(ok ? outcome.Substring(4) + " (was only in the backpack)" : "failed to move a healing item to the toolbelt: " + outcome);
        if (ok) nextPrepareAt = 0f;
        activeRoutines--;
    }

    /// <summary>
    /// Idle: glance around like a person standing still (look left or right, hold, look back), so the
    /// player doesn't freeze and actually notices things approaching.
    /// </summary>
    private static IEnumerator GlanceRoutine(EntityPlayerLocal p)
    {
        // Idle look-around like a person: now and then turn slowly to face a new direction and stay that way
        // (no left-right-left wobbling).
        activeRoutines++;
        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float yaw = p.rotation.y + side * UnityEngine.Random.Range(60f, 120f);
        float pitch = UnityEngine.Random.Range(-4f, 2f); // chosen once
        float end = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < end && !p.IsDead())
        {
            if (NearestEnemy(p, GuardDistance) != null || RebirthGameBridge.LastRequestAge < 0.5f) break;
            if (RebirthGameBridgePlayer.SmoothTurn(p, yaw, pitch, 0.45f) < 1f) break;
            yield return null;
        }
        activeRoutines--;
    }
    /// <summary>Run a routine while marked busy (so the instincts don't start something else on top of it).</summary>
    /// <summary>
    /// Awake enemies within `radius`, nearest first. With `comingOnly`, only those heading for us or already
    /// within 8 m (a zombie shambling off elsewhere is left alone).
    /// </summary>
    public static List<EntityAlive> AwakeThreats(EntityPlayerLocal p, float radius, bool comingOnly)
    {
        var list = new List<EntityAlive>();
        foreach (Entity e in p.world.Entities.list)
        {
            var a = e as EntityAlive;
            if (!IsThreat(a, p)) continue;
            float d = Flat(a.position, p.position);
            if (d > radius) continue;
            if (comingOnly && d > 8f && !IsApproaching(a, p) && a.GetAttackTarget() != p) continue;
            list.Add(a);
        }
        list.Sort((x, y) => Flat(x.position, p.position).CompareTo(Flat(y.position, p.position)));
        return list;
    }

    /// <summary>
    /// Pull back from a group over safe ground (away from all of them), then turn to face the nearest: they
    /// string out on the way and arrive one at a time.
    /// </summary>
    public static IEnumerator PullBack(EntityPlayerLocal p, float metres)
    {
        activeRoutines++;
        bool safe, jump;
        Vector3 dir = SafeEscape(p, EscapeDirection(p, 25f, null), out safe, out jump);
        if (safe)
        {
            string r = null;
            yield return RebirthGameBridgePlayer.WalkTo(p, p.position + dir * metres, 2f, true, 8f, x => r = x);
        }
        EntityAlive e = NearestEnemy(p, 30f);
        float end = Time.realtimeSinceStartup + 1.2f;
        while (e != null && !e.IsDead() && Time.realtimeSinceStartup < end
            && RebirthGameBridgePlayer.SmoothAimAt(p, RebirthGameBridgePlayer.EntityAimPoint(e, 0.3f), 0.2f) > 3f)
            yield return null;
        activeRoutines--;
    }

    /// <summary>An instinct (fight, heal, meal, chore) is running right now.</summary>
    public static bool InstinctBusy { get { return activeRoutines > 0; } }

    private static IEnumerator Busy(IEnumerator routine)
    {
        activeRoutines++;
        yield return routine;
        activeRoutines--;
    }

    private static IEnumerator InstinctHealRoutine(EntityPlayerLocal p)
    {
        activeRoutines++;
        yield return HealRoutine(p, GuardLog);
        activeRoutines--;
    }

    private static IEnumerator GuardReact(EntityPlayerLocal p, EntityAlive threat)
    {
        activeRoutines++; // reserve so the guard doesn't fire twice while getting ready
        int generation = ++combatGeneration;
        GuardLog(ClassName(threat) + " #" + threat.entityId + " within " + Flat(threat.position, p.position).ToString("0.0") + "m - reacting");
        RebirthGameBridgePlayer.CancelMovement();

        // Close any open window first (a player would drop the menu to deal with it).
        PlayerActionsLocal a = RebirthGameBridgeInput.LocalActions();
        yield return RebirthGameBridgeUi.CloseMenus();

        // Keep the weapon in hand if there is one (the player/agent chose it); otherwise ready the best
        // weapon in the toolbelt.
        ItemClass heldNow = p.inventory.holdingItem;
        bool holdingWeapon = IsRanged(heldNow) || IsMeleeWeapon(heldNow);
        int slot = holdingWeapon ? p.inventory.holdingItemIdx : BestWeaponSlot(p);
        if (slot >= 0 && slot != p.inventory.holdingItemIdx)
        {
            PlayerAction key = RebirthGameBridgeInput.Find("InventorySlot" + (slot + 1));
            if (key != null) RebirthGameBridgeInput.HoldFrames(key, 3);
            float t = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < t) yield return null;
        }
        activeRoutines--;

        var dummy = new BridgeRequest { Method = "POST", Path = "/guard" };
        bool armed = slot >= 0;
        bool hurt = p.Health <= 25;
        // Running from the same lone walker again and again fixes nothing (it never tires). A person either
        // patches up when there's room or, with a weapon in hand, deals with it.
        bool lone = NearestEnemy(p, 15f, null, threat) == null;
        if (hurt && armed && lone && RecentFlees(threat) >= 2 && p.Health > 10)
        {
            GuardLog("ran from " + ClassName(threat) + " " + RecentFlees(threat) + " times already - it's alone, fighting it instead");
            hurt = false;
        }
        if (!armed || hurt)
        {
            NoteFlee(threat);
            yield return FleeRoutine(dummy, p, threat, 25f, 20f, generation, "guard");
        }
        else
            yield return FightRoutine(dummy, p, threat, new FightOptions { Source = "guard", FleeHealth = Mathf.Min(15f, p.Health - 6f) }, generation);
    }

    // ------------------------------------------------------------------ spawn (test setup)

    /// <summary>
    /// ?entity=zombieArlene&amp;distance=8&amp;angle=0: spawn an entity class on the ground at `distance`
    /// metres from the player, `angle` degrees clockwise from where the player faces, facing the player.
    /// </summary>
    private static void Spawn(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        string name = req.QueryString("entity");
        int classId = name != null ? EntityClass.FromString(name) : 0;
        EntityClass ec;
        if (name == null || !EntityClass.list.TryGetValue(classId, out ec) || ec == null) { req.Fail("unknown entity class '" + name + "'", 404); return; }

        float distance = Mathf.Clamp(req.QueryFloat("distance", 8f), 1f, 80f);
        float yaw = p.rotation.y + req.QueryFloat("angle", 0f);
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 pos = p.position + dir * distance;
        // Terrain, not the highest block: GetHeight counts trees/bushes/roofs and spawned zombies up there.
        pos.y = p.world.GetTerrainHeight(Mathf.FloorToInt(pos.x), Mathf.FloorToInt(pos.z)) + 1.05f;
        var rot = new Vector3(0f, yaw + 180f, 0f);

        Entity e = EntityFactory.CreateEntity(classId, pos, rot);
        if (req.QueryBool("lootDrop", true)) e.lootDropProb = 1000f;     // the real chance is LootDropProb ~4 % x LootBagChance; tests want the bag every time
        p.world.SpawnEntityInWorld(e);
        if (req.QueryBool("sleeper", false)) { try { EntityAlive sl = e as EntityAlive; sl.SetSleeper(); sl.TriggerSleeperPose(0, false); } catch (Exception ex) { Log.Warning("[REBIRTH GameBridge] sleeper: " + ex.Message); } }
        req.Complete(new JObject
        {
            ["id"] = e.entityId, ["class"] = ec.entityClassName,
            ["position"] = new JArray((float)Math.Round(pos.x, 2), (float)Math.Round(pos.y, 2), (float)Math.Round(pos.z, 2)),
            ["distance"] = distance
        });
    }

    // ------------------------------------------------------------------ restore (test setup)

    /// <summary>Test setup: full health and stamina (e.g. at the start of a scenario).</summary>
    private static void Restore(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        // Rebirth lowers the maximum health after injuries ("Health Capacity", recovering slowly). A test that starts every fight from
        // the previous fight's leftovers is not repeatable, so restore also lifts that restriction (capacity=0 keeps it).
        if (req.QueryBool("capacity", true))
        {
            // The capacity target is "potential minus the injury/illness penalties of active debuffs": clear those first, or the
            // restriction comes straight back (that made every test fight start weaker than the last).
            var toClear = new List<string>();
            foreach (BuffValue bv in p.Buffs.ActiveBuffs)
            {
                string n = bv != null && bv.BuffClass != null ? bv.BuffClass.Name : null;
                if (string.IsNullOrEmpty(n) || n.StartsWith("buffArmor", StringComparison.OrdinalIgnoreCase)) continue;
                if (n.StartsWith("buffInjury", StringComparison.OrdinalIgnoreCase) || n.StartsWith("buffLeg", StringComparison.OrdinalIgnoreCase) || n.StartsWith("buffArm", StringComparison.OrdinalIgnoreCase)
                    || n.StartsWith("buffInfection", StringComparison.OrdinalIgnoreCase) || n.StartsWith("buffDysentery", StringComparison.OrdinalIgnoreCase)
                    || n.StartsWith("buffConcuss", StringComparison.OrdinalIgnoreCase) || n.StartsWith("buffIllness", StringComparison.OrdinalIgnoreCase)
                    || n.StartsWith("buffLaceration", StringComparison.OrdinalIgnoreCase) || n.StartsWith("buffFatigued", StringComparison.OrdinalIgnoreCase)) toClear.Add(n);
            }
            foreach (string n in toClear) p.Buffs.RemoveBuff(n);
            RebirthWorldCharacterRecord record;
            if (RebirthWorldCharacterService.TryGet(p, out record) && record != null && record.Condition != null)
            {
                float potential = RebirthHealthCapacityService.GetPotential(record);
                if (potential > 0f) { record.Condition.HealthCapacity = potential; RebirthHealthCapacityService.ApplyNativeHealthCapacity(p.Stats as PlayerEntityStats, p); }
            }
        }
        p.Stats.Health.Value = p.Stats.Health.ModifiedMax;
        p.Stats.Stamina.Value = p.Stats.Stamina.ModifiedMax;
        req.Complete(new JObject { ["health"] = p.Health, ["maxHealth"] = Mathf.Round(p.Stats.Health.ModifiedMax), ["stamina"] = Mathf.Round(Stamina(p)) });
    }

    // ------------------------------------------------------------------ give (test setup)

    /// <summary>?item=name&amp;count=1&amp;quality=1[&amp;toolbelt=1..10] - into a toolbelt slot or the backpack.</summary>
    private static void Give(BridgeRequest req)
    {
        EntityPlayerLocal p = Player(req);
        if (p == null) return;
        string name = req.QueryString("item");
        ItemValue proto = name != null ? ItemClass.GetItem(name, true) : null;
        if (proto == null || proto.IsEmpty()) { req.Fail("unknown item '" + name + "'", 404); return; }
        int count = Mathf.Max(1, req.QueryInt("count", 1));
        int quality = Mathf.Clamp(req.QueryInt("quality", 1), 1, 6);
        var stack = new ItemStack(new ItemValue(proto.type, quality, quality, true), count);
        int slot = req.QueryInt("toolbelt", 0);
        if (slot >= 1)
        {
            int size = RebirthGameBridgeNeeds.ToolbeltSize(p);
            if (slot > size) { req.Fail("toolbelt slot " + slot + " does not exist (the toolbelt has " + size + " slots)"); return; }
            // Never destroy what is already there (the instincts may have just put a drink in this slot):
            // it goes to the backpack, as a player would move it.
            ItemStack existing = p.inventory.GetItem(slot - 1);
            string displaced = null;
            if (existing != null && !existing.IsEmpty())
            {
                if (!p.bag.AddItem(existing.Clone())) { req.Fail("toolbelt slot " + slot + " holds " + existing.itemValue.ItemClass.GetItemName() + " and the backpack is full", 409); return; }
                displaced = existing.itemValue.ItemClass.GetItemName();
            }
            p.inventory.SetItem(slot - 1, stack);
            p.inventory.CallOnToolbeltChangedInternal();
            if (p.inventory.holdingItemIdx == slot - 1) p.inventory.Hand.OnSlotChanged(p.inventory.SelectedSlot); // re-equip: otherwise the held action is stale
            var res = new JObject { ["item"] = name, ["count"] = count, ["toolbelt"] = slot };
            if (displaced != null) res["movedToBackpack"] = displaced;
            req.Complete(res);
            return;
        }
        bool ok = p.bag.AddItem(stack);
        req.Complete(new JObject { ["item"] = name, ["count"] = count, ["backpack"] = ok }, ok ? 200 : 409);
    }
}
