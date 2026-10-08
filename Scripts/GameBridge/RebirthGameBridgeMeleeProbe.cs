using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

#nullable disable

/// <summary>
/// Ground truth for melee: what the game's own melee ray did at the hit frame. Hooks ItemActionDynamicMelee.Raycast (the call that
/// pays the stamina and looks for something to hit) and records the ray against the fight's target: how far along the ray the
/// target was, how far the ray passed from it, the weapon's range, whether the game found a hit, and what it hit.
/// </summary>
public static class RebirthGameBridgeMeleeProbe
{
    public static EntityAlive Target;
    public static float LastMainHitAt = -10f;
    public static float LastRaycastAt = -10f;   // the last time the melee ray fired (the hit frame), hit or miss   // the last time the game's main melee ray hit the target (not a graze)
    public static readonly List<string> Records = new List<string>();
    private static bool installed;

    public static void Install(Harmony harmony)
    {
        if (installed) return;
        installed = true;
        RebirthSkillEvalDiagnostics.On = true;
        var m = AccessTools.Method(typeof(ItemActionDynamicMelee), "Raycast");
        if (m != null) harmony.Patch(m, postfix: new HarmonyMethod(typeof(RebirthGameBridgeMeleeProbe), nameof(RaycastPostfix)));
        var f = AccessTools.Method(typeof(ProjectileMoveScript), "Fire");
        if (f != null) harmony.Patch(f, postfix: new HarmonyMethod(typeof(RebirthGameBridgeMeleeProbe), nameof(ArrowFirePostfix)));
        var h = AccessTools.Method(typeof(GameUtils), "HarvestOnAttack");   // skill tests: does the game reach it for this hit at all?
        if (h != null) harmony.Patch(h, prefix: new HarmonyMethod(typeof(RebirthGameBridgeMeleeProbe), nameof(HarvestProbePrefix)));
        else Log.Out("[REBIRTH SkillEval] GameUtils.HarvestOnAttack not found");
        var ht = AccessTools.Method(typeof(ItemActionDynamic), "hitTarget");
        if (ht != null) harmony.Patch(ht, prefix: new HarmonyMethod(typeof(RebirthGameBridgeMeleeProbe), nameof(HitTargetProbe)));
    }

    public static int HarvestCalls;
    private static void HitTargetProbe(ItemActionData _actionData, WorldRayHitInfo hitInfo, bool _isGrazingHit)
    {
        try { Log.Out("[REBIRTH SkillEval] hitTarget tag=" + (hitInfo != null ? hitInfo.tag : "null") + " grazing=" + _isGrazingHit + " action=" + (_actionData != null && _actionData.invData != null ? _actionData.invData.item.GetItemName() : "?")); } catch { }
    }
    private static void HarvestProbePrefix(ItemActionData _actionData)
    {
        HarvestCalls++;
        try { var ad = _actionData != null ? _actionData.attackDetails : null; Log.Out("[REBIRTH SkillEval] game HarvestOnAttack #" + HarvestCalls + " block=" + (ad != null && ad.bBlockHit && ad.blockBeingDamaged.Block != null ? ad.blockBeingDamaged.Block.GetBlockName() : "(entity/none)") + " dmg=" + (ad != null ? ad.damageGiven : -1f) + " max=" + (ad != null ? ad.damageMax : -1f)); } catch { }
    }

    private static void RaycastPostfix(ItemActionDynamicMelee __instance, ItemActionDynamic.ItemActionDynamicData _actionData, bool __result)
    {
        try
        {
            if (!(_actionData.invData.holdingEntity is EntityPlayerLocal) || Target == null) return;
            Ray ray = _actionData.ray;
            Vector3 dir = ray.direction.normalized;
            Vector3 chest = RebirthGameBridgePlayer.EntityAimPoint(Target, 0.35f);
            Vector3 v = chest - ray.origin;
            float along = Vector3.Dot(v, dir);
            float perp = (v - dir * along).magnitude;
            WorldRayHitInfo hi = _actionData.hitInfo;
            string tag = hi != null && hi.bHitValid ? (hi.tag ?? "?") : "none";
            float hitDist = hi != null && hi.bHitValid ? (hi.hit.pos - _actionData.rayStartPos).magnitude : -1f;
            Entity he = hi != null && hi.bHitValid && hi.transform != null ? hi.transform.GetComponentInParent<Entity>() : null;
            EntityPlayerLocal pl = (EntityPlayerLocal)_actionData.invData.holdingEntity;
            Ray lr = pl.GetLookRay();
            float angle = Vector3.Angle(lr.direction, dir);
            Vector3 vl = chest - lr.origin; float perpLook = (vl - lr.direction.normalized * Vector3.Dot(vl, lr.direction.normalized)).magnitude;
            bool hasAttackTarget = false; try { hasAttackTarget = pl.GetAttackTarget() != null; } catch { }
            float vertMiss = chest.y - (ray.origin.y + dir.y * along);
            if (Human && swingFile != null)
                swingFile.WriteLine(string.Join(",", new[] { Time.realtimeSinceStartup.ToString("0.00"), __result ? "1" : "0", __instance.Range.ToString("0.00"), along.ToString("0.00"), perp.ToString("0.00"), tag, hitDist.ToString("0.00"),
                    (he as EntityAlive) == Target ? "target" : (he != null ? "other" : "-"), dir.y.ToString("0.00"), new Vector2(Target.position.x - pl.position.x, Target.position.z - pl.position.z).magnitude.ToString("0.00"),
                    (Target.position.y - pl.position.y).ToString("0.00"), Target.EntityName, (ray.origin - (pl.position + Vector3.up * pl.GetEyeHeight())).magnitude.ToString("0.00"), pl.rotation.x.ToString("0.0"), pl.cameraTransform.forward.y.ToString("0.00"), "0",
                    _actionData.indexInEntityOfAction.ToString(), StaminaText(pl), pl.Health.ToString("0"), Target.Health.ToString("0"), FwdSpeed(pl).ToString("0.0"), StrafeSpeed(pl).ToString("0.0"),
                    pl.inventory.holdingItem != null ? pl.inventory.holdingItem.GetItemName() : "" }));
            LastRaycastAt = Time.realtimeSinceStartup;
            RebirthGameBridgeFightRecorder.Event("HITFRAME " + (__result ? "hit " : "miss ") + tag + " d=" + new Vector2(Target.position.x - pl.position.x, Target.position.z - pl.position.z).magnitude.ToString("0.00") + " act=" + _actionData.indexInEntityOfAction);
            if (__result && (he as EntityAlive) == Target) LastMainHitAt = Time.realtimeSinceStartup;
            lock (Records)
                Records.Add(string.Join(",", new[] {
                    Time.realtimeSinceStartup.ToString("0.00"), __result ? "1" : "0", __instance.Range.ToString("0.00"), along.ToString("0.00"), perp.ToString("0.00"),
                    tag, hitDist.ToString("0.00"), (he as EntityAlive) == Target ? "target" : (he != null ? "other:" + he.GetType().Name : "-"), angle.ToString("0.0"), perpLook.ToString("0.00"), vertMiss.ToString("0.00"), hasAttackTarget ? "1" : "0",
                    new Vector2(Target.position.x - pl.position.x, Target.position.z - pl.position.z).magnitude.ToString("0.00"),
                    new Vector2(pl.motion.x, pl.motion.z).magnitude.ToString("0.0"), new Vector2(Target.motion.x, Target.motion.z).magnitude.ToString("0.0"),
                    dir.y.ToString("0.00"), (Target.position.y - pl.position.y).ToString("0.00"),
                    pl.bFirstPersonView ? "1" : "0", (ray.origin - (pl.position + Vector3.up * pl.GetEyeHeight())).magnitude.ToString("0.00"), (ray.origin.y - (pl.position.y + pl.GetEyeHeight())).ToString("0.00"),
                    pl.rotation.x.ToString("0.0"), pl.cameraTransform != null ? pl.cameraTransform.forward.y.ToString("0.00") : "na", pl.cameraTransform != null ? pl.cameraTransform.localEulerAngles.x.ToString("0.0") : "na" }));
        }
        catch { }
    }

    public static void Trace(params string[] f) { lock (Records) Records.Add("TRACE," + string.Join(",", f)); }

    // ---- Human-play recording (REBIRTH_BRIDGE_HUMAN=1): the bridge sends no input; this only watches and writes CSVs. ----
    public static bool Human;
    private static System.IO.StreamWriter traceFile, swingFile;
    private static int humanFrame;

    public static void HumanTick(EntityPlayerLocal pl)
    {
        if (!Human || pl == null || pl.IsDead()) return;
        try
        {
            if (traceFile == null)
            {
                string dir = RebirthGameBridge.OutputDir;
                traceFile = new System.IO.StreamWriter(System.IO.Path.Combine(dir, "human_trace.csv"), false) { AutoFlush = true };
                swingFile = new System.IO.StreamWriter(System.IO.Path.Combine(dir, "human_swings.csv"), false) { AutoFlush = true };
                traceFile.WriteLine("time,rotX,camFwdY,lookRayY,swingRunning,zombie,hdist,zDy,eyeToHead,eyeToChest,zSwinging,originVsEye,playerHp,stamina,fwdSpeed,strafeSpeed,zombieHp,zombieSpeed,held,crouch,sprint,zStun");
                swingFile.WriteLine("time,found,range,alongRay,perp,tag,hitDist,hitEntity,rayPitch,hdist,zDy,zombie,originVsEye,rotX,camFwdY,zSwinging,actionIndex,stamina,playerHp,zombieHp,fwdSpeed,strafeSpeed,held");
            }
            EntityAlive best = null; float bd = 8f;
            foreach (Entity e in GameManager.Instance.World.Entities.list)
            {
                EntityAlive ea = e as EntityAlive;
                if (ea == null || ea == pl || !ea.IsAlive() || !(ea is EntityEnemy)) continue;
                float d = (ea.position - pl.position).magnitude;
                if (d < bd) { bd = d; best = ea; }
            }
            Target = best;
            if (++humanFrame % 3 != 0 || best == null) return;
            Ray lr = pl.GetLookRay();
            Vector3 eye = pl.position + Vector3.up * pl.GetEyeHeight();
            bool busy = false; try { busy = pl.inventory.IsHoldingItemActionRunning(); } catch { }
            Vector3 head = RebirthGameBridgePlayer.EntityAimPoint(best, 0f), chest = RebirthGameBridgePlayer.EntityAimPoint(best, 0.35f);
            traceFile.WriteLine(string.Join(",", new[] { Time.realtimeSinceStartup.ToString("0.00"), pl.rotation.x.ToString("0.0"), pl.cameraTransform.forward.y.ToString("0.00"), lr.direction.y.ToString("0.00"), busy ? "1" : "0",
                best.EntityName, new Vector2(best.position.x - pl.position.x, best.position.z - pl.position.z).magnitude.ToString("0.00"), (best.position.y - pl.position.y).ToString("0.00"),
                (head.y - lr.origin.y).ToString("0.00"), (chest.y - lr.origin.y).ToString("0.00"), RebirthGameBridgeCombat.TargetSwinging(best) ? "1" : "0", (lr.origin - eye).magnitude.ToString("0.00"),
                pl.Health.ToString("0"), StaminaText(pl), FwdSpeed(pl).ToString("0.0"), StrafeSpeed(pl).ToString("0.0"), best.Health.ToString("0"), new Vector2(best.motion.x, best.motion.z).magnitude.ToString("0.0"),
                pl.inventory.holdingItem != null ? pl.inventory.holdingItem.GetItemName() : "", pl.Crouching ? "1" : "0", pl.MovementRunning ? "1" : "0", RebirthGameBridgeCombat.TargetStunned(best) ? "1" : "0" }));
        }
        catch { }
    }

    /// <summary>Every projectile the local player fires: speed, gravity, direction and start, so the bow aim can use the real ballistics.</summary>
    private static void ArrowFirePostfix(ProjectileMoveScript __instance)
    {
        try
        {
            if (Target == null) return;
            var pl = GameManager.Instance.World.GetPrimaryPlayer();
            if (pl == null || __instance.firingEntity != pl) return;
            Vector3 start = __instance.previousPosition;
            Vector3 chest = RebirthGameBridgePlayer.EntityAimPoint(Target, 0.35f);
            lock (Records)
                Records.Add(string.Join(",", new[] { "ARROW", Time.realtimeSinceStartup.ToString("0.00"), __instance.velocity.magnitude.ToString("0.0"), __instance.gravity.ToString("0.00"),
                    __instance.flyDirection.y.ToString("0.000"), (chest - start).magnitude.ToString("0.0"), (chest.y - start.y).ToString("0.00"),
                    new Vector2(Target.motion.x, Target.motion.z).magnitude.ToString("0.0"),
                    MissAt(__instance, start, chest, false).ToString("0.00"), MissAt(__instance, start, chest, true).ToString("0.00"), (pl.rotation.x).ToString("0.0") }));
        }
        catch { }
    }

    /// <summary>How far the arrow's flight passes from the chest (closest approach to the point), with or without gravity.</summary>
    private static float MissAt(ProjectileMoveScript s, Vector3 start, Vector3 chest, bool gravity)
    {
        Vector3 d = s.flyDirection.normalized; float speed = s.velocity.magnitude;
        Vector3 v = chest - start; float along = Vector3.Dot(v, d);
        float flight = Mathf.Max(0f, along / Mathf.Max(1f, speed));
        Vector3 at = start + d * along;
        if (gravity) at += new Vector3(0f, 0.5f * s.gravity * flight * flight, 0f);   // gravity is negative
        return (chest - at).magnitude;
    }

    private static string StaminaText(EntityPlayerLocal pl) { try { return pl.Stats.Stamina.Value.ToString("0"); } catch { return "?"; } }
    private static float FwdSpeed(EntityPlayerLocal pl) { Vector3 f = Quaternion.Euler(0f, pl.rotation.y, 0f) * Vector3.forward; return Vector3.Dot(new Vector3(pl.motion.x, 0f, pl.motion.z), f); }
    private static float StrafeSpeed(EntityPlayerLocal pl) { Vector3 r = Quaternion.Euler(0f, pl.rotation.y, 0f) * Vector3.right; return Vector3.Dot(new Vector3(pl.motion.x, 0f, pl.motion.z), r); }

    /// <summary>The recorded melee/arrow probe lines (capped, never the debug trace), for the API result.</summary>
    public static List<string> TakeRecords(int max)
    {
        var all = Take(); var keep = new List<string>();
        foreach (string s in all) { if (s.StartsWith("TRACE")) continue; keep.Add(s); if (keep.Count >= max) break; }
        return keep;
    }

    public static List<string> Take()
    {
        lock (Records) { var l = new List<string>(Records); Records.Clear(); return l; }
    }
}

