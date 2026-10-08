using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

#nullable disable

/// <summary>
/// The loot bag a killed zombie drops (an EntityLootContainer, with probability LootDropProb x LootBagChance - 4 % normally). Once the fight is over and nothing
/// awake is near, walk to each bag, open it like a player (E), and take what fits (R).
/// Keep all collected loot; the POI routine handles storage trips when encumbered.
/// </summary>
public static class RebirthGameBridgeLootBags
{
    public static bool Encumbered(EntityPlayerLocal p)
    {
        try { return p.Buffs.HasBuff("buffEncumberedInv"); } catch { return false; }
    }

    private static bool Danger(EntityPlayerLocal p)
    {
        try { return RebirthGameBridgeCombat.AwakeThreats(p, 14f, false).Count > 0; } catch { return false; }
    }

    private static List<EntityLootContainer> Bags(EntityPlayerLocal p, float radius)
    {
        var list = new List<EntityLootContainer>();
        foreach (Entity e in p.world.Entities.list)
        {
            var b = e as EntityLootContainer;
            if (b == null || (b.position - p.position).magnitude > radius) continue;
            list.Add(b);
        }
        list.Sort((x, y) => (x.position - p.position).sqrMagnitude.CompareTo((y.position - p.position).sqrMagnitude));
        return list;
    }

    private static bool Fast(EntityPlayerLocal p) { try { return p.Stats.Stamina.Value > 40f; } catch { return false; } }   // use the stamina: run between bags

    public static readonly List<Vector3> Spots = new List<Vector3>();   // where zombies died (fallback when no bag is in sight)
    private static bool LootOpen() { return RebirthGameBridgeUi.IsWindowOpen("bagStorage") || RebirthGameBridgeUi.IsWindowOpen("looting") || RebirthGameBridgeUi.IsWindowOpen("windowLooting"); }

    /// <summary>Stand where the bag can be seen by the crosshair (grass and slopes hide small bags), then hold E once.</summary>
    private static IEnumerator OpenBag(EntityPlayerLocal p, EntityLootContainer bag, Action<string> log)
    {
        // Stand once, then centre the view on the bag (its collider) and nudge the aim in small steps until the crosshair is on it. Never walk away and retry.
        if ((bag.position - p.position).magnitude > 1.9f || (bag.position - p.position).magnitude < 1.0f)
        {
            Vector3 dir = p.position - bag.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            yield return RebirthGameBridgePlayer.WalkTo(p, bag.position + dir.normalized * 1.5f, 0.3f, Fast(p), 12f, m => { });
        }
        if (Danger(p)) { log("bag: danger, giving up on it"); yield break; }
        Vector3 center = bag.position - Vector3.up * 0.15f;   // the collider sits at/below the reported position; aiming higher hits the grass in front of it
        float[] dxs = { 0f, 0f, 0f, 0.12f, -0.12f, 0f, 0.12f, -0.12f, 0.25f, -0.25f, 0f, 0.12f, -0.12f };
        float[] dys = { 0f, -0.12f, -0.25f, -0.12f, -0.12f, 0.12f, -0.25f, -0.25f, -0.12f, -0.12f, -0.4f, -0.4f, -0.4f };
        bool aimed = false;
        for (int k = 0; k < dxs.Length && !aimed; k++)
        {
            Vector3 right = Quaternion.Euler(0f, p.rotation.y, 0f) * Vector3.right;
            Vector3 aimPt = center + right * dxs[k] + Vector3.up * dys[k];
            yield return RebirthGameBridgePlayer.TurnToRoutine(p, () => aimPt, 0f, 0f, 1.2f);
            yield return null; yield return null;
            JObject t = RebirthGameBridgePlayer.DescribeTarget(p);
            JToken id = t["entity"] == null ? null : t["entity"]["id"];
            Vector2 pv;
            aimed = (id != null && (int)id == bag.entityId) || RebirthGameBridgeUi.TryFindByText("interactionPrompt", "Loot", out pv);
        }
        if (!aimed) { log("bag: crosshair never found it (" + RebirthGameBridgePlayer.DescribeTarget(p).ToString(Newtonsoft.Json.Formatting.None) + ")"); yield break; }
        {
            var dummy = new BridgeRequest { Method = "POST", Path = "/activate" };
            yield return RebirthGameBridgePlayer.ActivateRoutine(dummy, p, null, null, 0.35f);   // press and hold briefly; the game then runs its own opening timer
            float until = Time.realtimeSinceStartup + 1.0f;
            while (Time.realtimeSinceStartup < until && !LootOpen()) yield return null;
            // let the opening timer finish: stand still until the timer window is gone and the bag window is up
            until = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < until && !LootOpen() && RebirthGameBridgeUi.IsWindowOpen("timer")) yield return null;
            for (int w = 0; w < 30 && !LootOpen(); w++) yield return null;
        }
    }

    /// <summary>Loot every dropped bag, nearest first (whichever dropped when), once nothing awake is near.</summary>
    private static bool busy;

    public static IEnumerator Routine(EntityPlayerLocal p, Action<string> log, Action<int> done)
    {
        while (busy) yield return null;      // a looting round is already running (started right after the kills): wait for it, then check what is left
        busy = true;
        int n = 0;
        yield return Run(p, log, c => n = c);
        busy = false;
        done(n);
    }

    private static IEnumerator Run(EntityPlayerLocal p, Action<string> log, Action<int> done)
    {
        int looted = 0;
        float giveUp = Time.realtimeSinceStartup + 240f;
        var tried = new HashSet<int>();
        var spots = new List<Vector3>(Spots);
        Spots.Clear();
        spots.RemoveAll(s => (s - p.position).magnitude > 25f);
        log("loot bags: " + Bags(p, 25f).Count + " bag(s) in sight, " + spots.Count + " death spot(s)");
        while (Time.realtimeSinceStartup < giveUp && !p.IsDead())
        {
            if (Danger(p)) { log("loot bags: something awake is near - not now"); Spots.AddRange(spots); break; }
            EntityLootContainer bag = null;
            foreach (EntityLootContainer b in Bags(p, 25f)) if (!tried.Contains(b.entityId)) { bag = b; break; }   // Bags() is sorted nearest first
            if (bag == null)
            {
                if (spots.Count == 0) break;
                int ni = 0;   // nothing in sight: the nearest death spot may still hold one that has not loaded
                for (int i = 1; i < spots.Count; i++) if ((spots[i] - p.position).sqrMagnitude < (spots[ni] - p.position).sqrMagnitude) ni = i;
                Vector3 spot = spots[ni]; spots.RemoveAt(ni);
                if ((spot - p.position).magnitude > 3f) { log("walking to where something died (" + (spot - p.position).magnitude.ToString("0") + " m)"); yield return RebirthGameBridgePlayer.WalkTo(p, spot, 1.5f, Fast(p), 25f, m => { }); }
                yield return new WaitForSeconds(0.5f);
                continue;
            }
            // bags next to the one we are heading for are nearest-first by definition; spots are only a fallback
            tried.Add(bag.entityId);
            log("loot bag " + (bag.position - p.position).magnitude.ToString("0.0") + " m away");
            yield return OpenBag(p, bag, log);
            if (LootOpen())
            {
                log("window open - reading it for 3 s"); yield return new WaitForSeconds(3.0f);   // keep it open so a watcher can see what is in it
                foreach (var rb in RebirthGameBridgeInput.FindByKey("R")) RebirthGameBridgeInput.HoldFrames(rb, 3); yield return new WaitForSeconds(0.8f);
                var ui = LocalPlayerUI.GetUIForPlayer(p);
                var remaining = ui != null && ui.xui != null ? ui.xui.LootContainer : null;
                bool emptied = remaining != null && remaining.IsEmpty();
                if (emptied) looted++;
                log(emptied ? "emptied the bag" : "take attempted; bag not verified empty");
                RebirthGameBridgeInput.ReleaseCursor();
                yield return RebirthGameBridgeUi.CloseMenus();
                if (Encumbered(p)) { log("encumbered - preserving loot for a storage trip"); break; }
            }
            else log("could not open the bag");
        }
        done(looted);
    }
}
