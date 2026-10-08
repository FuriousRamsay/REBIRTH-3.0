using System;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using UnityEngine;

#nullable disable

/// <summary>Explicit, read-only, single-target diagnostics. Tokens identify local object instances only.</summary>
public static class RebirthGameBridgeTargetCombatSnapshot
{
    private sealed class Token { public readonly string Value = Guid.NewGuid().ToString("N"); }
    private static readonly ConditionalWeakTable<World, Token> Worlds = new ConditionalWeakTable<World, Token>();
    private static readonly ConditionalWeakTable<EntityZombie, Token> Targets = new ConditionalWeakTable<EntityZombie, Token>();

    public static bool TryCapture(World world, int entityId, out JObject snapshot, out string reason)
    {
        try { return Capture(world, entityId, out snapshot, out reason); }
        catch { snapshot = null; reason = "diagnostic_capture_unavailable"; return false; }
    }
    private static bool Capture(World world, int entityId, out JObject snapshot, out string reason)
    {
        snapshot = null;
        reason = null;
        if (world == null || entityId <= 0) { reason = "invalid_world_or_target"; return false; }
        var game = GameManager.Instance;
        var state = world.worldState;
        if (game == null || !ReferenceEquals(game.World, world) || state == null)
        { reason = "current_world_scope_unavailable"; return false; }
        var nativeGuid = state.Guid;
        if (string.IsNullOrWhiteSpace(nativeGuid)) { reason = "native_world_identity_unavailable"; return false; }
        EntityZombie target = world.GetEntity(entityId) as EntityZombie;
        if (target == null || target.entityId != entityId || !ReferenceEquals(target.world, world))
        { reason = "exact_zombie_not_present"; return false; }
        var unavailable = new JArray();
        var value = new JObject
        {
            ["snapshotVersion"] = 1,
            ["worldSession"] = Worlds.GetValue(world, w => new Token()).Value,
            ["nativeWorldGuid"] = nativeGuid.ToString(),
            ["entityInstance"] = Targets.GetValue(target, t => new Token()).Value,
            ["identityScope"] = "local_diagnostic_object_instance_only",
            ["entityId"] = entityId,
            ["sampleTime"] = Number(Time.realtimeSinceStartup, "sampleTime", unavailable),
            ["alive"] = !target.IsDead() && target.Health > 0,
            ["health"] = target.Health,
            ["position"] = new JObject { ["x"] = Number(target.position.x, "position.x", unavailable), ["y"] = Number(target.position.y, "position.y", unavailable), ["z"] = Number(target.position.z, "position.z", unavailable) },
            ["currentStun"] = Enum.IsDefined(typeof(EnumEntityStunType), target.bodyDamage.CurrentStun) ? (JToken)new JValue(target.bodyDamage.CurrentStun.ToString()) : JValue.CreateNull(),
            ["stunDuration"] = Number(target.bodyDamage.StunDuration, "stunDuration", unavailable),
            ["stunProne"] = target.bodyDamage.StunProne,
            ["stunKnee"] = target.bodyDamage.StunKnee,
            ["events"] = JValue.CreateNull(),
            ["complete"] = false,
            ["eventStatus"] = "unavailable_no_damage_observer",
            ["movementStatus"] = "unavailable_no_intent_or_displacement_recording"
        };
        // Partial native movement request only; no pursuit/render/authority claim.
        value["sampledNativeMovementRequest"] = new JObject {
            ["x"] = Number(target.moveDirection.x, "moveDirection.x", unavailable),
            ["y"] = Number(target.moveDirection.y, "moveDirection.y", unavailable),
            ["z"] = Number(target.moveDirection.z, "moveDirection.z", unavailable),
            ["absolute"] = target.isMoveDirAbsolute,
            ["remoteRepresentation"] = target.isEntityRemote,
            ["provesIntentOrRenderedMovement"] = false
        };
        value["bleedActive"] = Read(() => target.Buffs == null ? null : (JToken)new JValue(target.Buffs.HasBuff("buffInjuryBleeding")), "bleedActive", unavailable);
        value["bleedCounter"] = CVar(target, "bleedCounter", unavailable);
        value["bleedDuration"] = CVar(target, "$bleedDuration", unavailable);
        value["bleedAmount"] = CVar(target, "$bleedAmount", unavailable);
        value["bleedSlowdown"] = CVar(target, "$bleedSlowdown", unavailable);
        value["stunnedCVar"] = CVar(target, "_stunned", unavailable);
        value["animationStun"] = Read(() => target.emodel == null || target.emodel.avatarController == null ? null : (JToken)new JValue(target.emodel.avatarController.IsAnimationStunRunning()), "animationStun", unavailable);
        value["ragdollActive"] = Read(() => target.emodel == null ? null : (JToken)new JValue(target.emodel.IsRagdollActive), "ragdollActive", unavailable);
        value["ragdollMovement"] = Read(() => target.emodel == null ? null : (JToken)new JValue(target.emodel.IsRagdollMovement), "ragdollMovement", unavailable);
        value["unavailable"] = unavailable;
        var availability = new JObject();
        foreach (var field in new[] { "sampleTime", "health", "position", "currentStun", "stunDuration", "stunProne", "stunKnee", "bleedActive", "bleedCounter", "bleedDuration", "bleedAmount", "bleedSlowdown", "stunnedCVar", "animationStun", "ragdollActive", "ragdollMovement", "events" })
            availability[field] = value[field] == null || value[field].Type == JTokenType.Null ? "unavailable" : "available";
        var position = (JObject)value["position"];
        foreach (var axis in new[] { "x", "y", "z" })
            availability["position." + axis] = position[axis].Type == JTokenType.Null ? "unavailable" : "available";
        if (position["x"].Type == JTokenType.Null || position["y"].Type == JTokenType.Null || position["z"].Type == JTokenType.Null) availability["position"] = "unavailable";
        value["availability"] = availability;
        if (!ReferenceEquals(GameManager.Instance, game) || !ReferenceEquals(game.World, world) || !ReferenceEquals(world.worldState, state) || state.Guid != nativeGuid || !ReferenceEquals(world.GetEntity(entityId), target) || !ReferenceEquals(target.world, world) || target.entityId != entityId)
        { reason = "target_scope_changed"; return false; }
        snapshot = value;
        return true;
    }

    private static JToken CVar(EntityZombie target, string name, JArray unavailable)
    {
        return Read(() =>
        {
            float value;
            if (target.Buffs == null || target.Buffs.CVars == null || !target.Buffs.CVars.TryGetValue(name, out value)) return null;
            return Number(value, name, unavailable);
        }, name, unavailable);
    }
    private static JToken Read(Func<JToken> read, string field, JArray unavailable)
    {
        try { JToken value = read(); if (value != null) return value; }
        catch { }
        unavailable.Add(field);
        return JValue.CreateNull();
    }
    private static JToken Number(float number, string field, JArray unavailable)
    {
        if (!float.IsNaN(number) && !float.IsInfinity(number)) return new JValue(number);
        unavailable.Add(field);
        return JValue.CreateNull();
    }
}



