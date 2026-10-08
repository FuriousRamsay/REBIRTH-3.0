using System;
using System.Collections;
using Newtonsoft.Json.Linq;
using UnityEngine;

#nullable disable

/// <summary>
/// Helpers for the skill playtest (REBIRTH 48-skill checklist): set up exactly one weapon in the toolbelt so that a fight trains exactly that weapon family.
/// </summary>
public static class RebirthGameBridgeSkills
{
    // Fixture preparation only, never qualification evidence. Deliberately refuses every
    // save except the disposable CodexTest save and requires an explicit confirmation.
    public static void SetupSkill(BridgeRequest req)
    {
        EntityPlayerLocal player = RebirthGameBridgeCombat.PlayerFor(req);
        if (player == null) return;
        if (GamePrefs.GetString(EnumGamePrefs.GameName) != "CodexTest" || req.QueryString("confirm") != "CodexTest"
            || player.world == null || player.world.IsRemote())
        { req.Fail("Skill fixture setup is restricted to the local disposable CodexTest save.", 403); return; }
        RebirthStablePlayerIdentity identity; RebirthWorldCharacterRecord record;
        if (!RebirthSkillAwardService.TryGetEligible(player, out identity, out record))
        { req.Fail("No eligible Rebirth test character.", 409); return; }
        string id = req.QueryString("skill");
        RebirthSkillRuntimeState skill;
        if (id == null || !record.Progression.Skills.TryGetValue(id, out skill) || skill == null)
        { req.Fail("Unknown skill.", 400); return; }
        float requested = req.QueryFloat("value", skill.Value + skill.Progress);
        if (float.IsNaN(requested) || float.IsInfinity(requested) || requested < -100f || requested > 100f)
        { req.Fail("Skill fixture value must be finite and between -100 and 100.", 400); return; }
        float before = skill.Value + skill.Progress;
        skill.Value = Mathf.Floor(requested); skill.Progress = requested - skill.Value;
        record.Touch("bridge-disposable-skill-fixture:" + id);
        bool saved = RebirthWorldCharacterRepository.SaveIfDirty(identity, "bridge-disposable-skill-fixture");
        RebirthSurvivorNetworkService.SendOwnerState(player, 0L, true, "bridge-disposable-skill-fixture");
        req.Complete(new JObject { ["fixtureOnly"] = true, ["skill"] = id, ["before"] = before, ["after"] = requested, ["saved"] = saved });
    }

    /// <summary>POST /loadout?weapon=ITEM [&amp;quality=Q] [&amp;ammo=ITEM&amp;ammoCount=N]. Every weapon in the toolbelt moves to the backpack; the weapon goes to slot 1 and is held. No weapon = bare hands.</summary>
    public static void Loadout(BridgeRequest req)
    {
        EntityPlayerLocal p = RebirthGameBridgeCombat.PlayerFor(req);
        if (p == null) return;
        string weapon = req.QueryString("weapon");
        var moved = new JArray();
        int size = RebirthGameBridgeNeeds.ToolbeltSize(p);
        for (int i = 0; i < size; i++)
        {
            ItemStack s = p.inventory.GetItem(i);
            if (s == null || s.IsEmpty()) continue;
            ItemClass ic = s.itemValue.ItemClass;
            if (i != 0 && !(RebirthGameBridgeCombat.IsMeleeWeaponPublic(ic) || RebirthGameBridgeCombat.IsRangedPublic(ic))) continue;
            if (!p.bag.AddItem(s.Clone())) { req.Fail("the backpack is full - cannot put " + ic.GetItemName() + " away", 409); return; }
            moved.Add(ic.GetItemName());
            p.inventory.SetItem(i, ItemStack.Empty.Clone());
        }
        if (!string.IsNullOrEmpty(weapon) && weapon != "hands")
        {
            ItemValue proto = ItemClass.GetItem(weapon, true);
            if (proto == null || proto.IsEmpty()) { req.Fail("unknown weapon '" + weapon + "'", 404); return; }
            int q = Mathf.Clamp(req.QueryInt("quality", 3), 1, 6);
            ItemStack existing = p.inventory.GetItem(0);
            if (existing != null && !existing.IsEmpty()) { p.bag.AddItem(existing.Clone()); }
            p.inventory.SetItem(0, new ItemStack(new ItemValue(proto.type, q, q, true), 1));
        }
        string ammo = req.QueryString("ammo");
        if (!string.IsNullOrEmpty(ammo))
        {
            ItemValue ap = ItemClass.GetItem(ammo, true);
            if (ap == null || ap.IsEmpty()) { req.Fail("unknown ammo '" + ammo + "'", 404); return; }
            if (!p.bag.AddItem(new ItemStack(new ItemValue(ap.type), Mathf.Max(1, req.QueryInt("ammoCount", 60)))))
            { req.Fail("the backpack cannot hold the requested ammunition", 409); return; }
        }
        p.inventory.CallOnToolbeltChangedInternal();
        p.inventory.SetHoldingItemIdx(0);
        p.inventory.Hand.OnSlotChanged(p.inventory.SelectedSlot);
        string held = "";
        try { held = p.inventory.holdingItemItemValue.ItemClass.GetItemName(); } catch { }
        var res = new JObject { ["movedToBackpack"] = moved, ["held"] = held };
        try
        {
            RebirthWeaponSustainedDpsService.Profile pr;
            if (RebirthWeaponSustainedDpsService.TryGetCombatProfile(p, p.inventory.holdingItemItemValue, out pr) && pr != null)
                res["profile"] = new JObject { ["valid"] = pr.Valid, ["skill"] = pr.SkillId, ["damagePerAttack"] = pr.DamagePerAttack, ["projectiles"] = pr.ProjectilesPerAttack, ["rpm"] = pr.RoundsPerMinute, ["magazine"] = pr.MagazineSize, ["ammoPerAttack"] = pr.AmmoPerAttack, ["reloadSeconds"] = pr.ReloadSeconds, ["burstDps"] = pr.BurstDps, ["sustainedDps"] = pr.SustainedDps, ["detail"] = pr.Detail };
        }
        catch (Exception ex) { res["profileError"] = ex.Message; }
        req.Complete(res);
    }
}

/// <summary>Switch for skill-evaluation diagnostics in the progression patches (set by the bridge when REBIRTH_BRIDGE_HUMAN is not needed: always on while the bridge runs).</summary>
public static class RebirthSkillEvalDiagnostics
{
    public static bool On = false;   // the bridge probe switches it on
}

public static class RebirthGameBridgeBlocks
{
    /// <summary>POST /setblock?x=&amp;y=&amp;z=&amp;name=BLOCK  Test setup only: put a block into the world (for Mining/Salvage/Construction scenarios).</summary>
    public static void SetBlock(BridgeRequest req)
    {
        EntityPlayerLocal p = RebirthGameBridgeCombat.PlayerFor(req);
        if (p == null) return;
        string name = req.QueryString("name");
        Block b = name != null ? Block.GetBlockByName(name, true) : null;
        if (b == null) { req.Fail("unknown block '" + name + "'", 404); return; }
        var pos = new Vector3i(Mathf.FloorToInt(req.QueryFloat("x", p.position.x)), Mathf.FloorToInt(req.QueryFloat("y", p.position.y)), Mathf.FloorToInt(req.QueryFloat("z", p.position.z)));
        BlockValue bv = b.ToBlockValue();
        p.world.SetBlockRPC(pos, bv);
        req.Complete(new JObject { ["block"] = name, ["x"] = pos.x, ["y"] = pos.y, ["z"] = pos.z });
    }
}

public static class RebirthGameBridgeFlags
{
    /// <summary>POST /testflag?name=waterbypass&amp;on=1 : test-only switches (farming: crops grow without a water source).</summary>
    public static void Set(BridgeRequest req)
    {
        string name = req.QueryString("name") ?? "";
        bool on = req.QueryBool("on", true);
        switch (name)
        {
            case "waterbypass": AdvancedFarmingCatchupService.DiagnosticBypassWaterConsumption = on; break;
            default: req.Fail("unknown flag '" + name + "'", 404); return;
        }
        req.Complete(new JObject { ["flag"] = name, ["on"] = on });
    }
}

public static class RebirthGameBridgeCraft
{
    /// <summary>POST /craft?recipe=NAME[&amp;count=N][&amp;stock=1] : queue a recipe in the open crafting window (personal crafting or the workstation window that is open), like pressing Craft. stock=1 first gives the ingredients.</summary>
    public static void Craft(BridgeRequest req)
    {
        EntityPlayerLocal p = RebirthGameBridgeCombat.PlayerFor(req);
        if (p == null) return;
        string name = req.QueryString("recipe");
        Recipe recipe = name != null ? CraftingManager.GetRecipe(name) : null;
        if (recipe == null) { req.Fail("unknown recipe '" + name + "'", 404); return; }
        int count = Mathf.Max(1, req.QueryInt("count", 1));
        XUi xui = LocalPlayerUI.GetUIForPlayer(p).xui;
        XUiC_CraftingWindowGroup group = null;
        foreach (XUiWindowGroup window in xui.WindowGroups)
        {
            if (window == null || !window.isShowing || window.Controller == null) continue;
            var candidate = window.Controller as XUiC_CraftingWindowGroup;
            if (candidate == null) candidate = window.Controller.GetChildByType<XUiC_CraftingWindowGroup>();
            if (candidate != null) { group = candidate; break; }
        }
        if (group == null) { req.Fail("no crafting window is open (open the crafting screen or the workstation first)", 409); return; }
        // Reject incompatible station queues rather than silently crediting personal crafting.
        if (!string.Equals(recipe.craftingArea ?? "", group.workstation ?? "", StringComparison.OrdinalIgnoreCase))
        { req.Fail("recipe requires '" + recipe.craftingArea + "', open queue is '" + group.workstation + "'", 409); return; }
        if (req.QueryBool("cancel", false))
        {
            var queue = group.GetChildByType<XUiC_CraftingQueue>();
            int cancelled = 0;
            if (queue != null)
                foreach (XUiC_RecipeStack stack in queue.GetRecipesToCraft())
                    if (stack != null && stack.HasRecipe() && stack.GetRecipe() == recipe)
                    { stack.ForceCancel(); cancelled++; }
            req.Complete(new JObject { ["recipe"] = name, ["cancelled"] = cancelled }, cancelled > 0 ? 200 : 409);
            return;
        }
        if (req.QueryBool("stock", false))
        {
            foreach (ItemStack ing in recipe.ingredients)
            {
                if (ing == null || ing.itemValue == null || ing.itemValue.IsEmpty()) continue;
                if (!p.bag.AddItem(new ItemStack(new ItemValue(ing.itemValue.type), ing.count * count)))
                { req.Fail("backpack cannot hold ingredient " + ing.itemValue.ItemClass.GetItemName(), 409); return; }
            }
        }
        bool ok = group.AddItemToQueue(recipe, count);
        req.Complete(new JObject { ["recipe"] = name, ["count"] = count, ["queued"] = ok, ["window"] = group.GetType().Name }, ok ? 200 : 409);
    }
}
