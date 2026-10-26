using System;
using System.Globalization;
using UnityEngine;

/// <summary>Owner-scoped Farm/Structure volume from the saved Companion Desk.</summary>
public sealed class RebirthNpcDeskVolumeWorkTargetAdapter : IRebirthNpcWorkTargetAdapter
{
    public string AdapterId => "rebirth.target.desk-volume";
    public int Priority => 350;
    public bool CanResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition)
        => assignment != null && assignment.TargetKey != null &&
           definition != null &&
           (definition.Kind == RebirthNpcWorkKind.Farming && assignment.TargetKey.EndsWith(":Farm", StringComparison.Ordinal) ||
            (definition.Kind == RebirthNpcWorkKind.Repair || definition.Kind == RebirthNpcWorkKind.Construction) &&
            assignment.TargetKey.EndsWith(":Structure", StringComparison.Ordinal)) &&
           assignment.TargetKey.StartsWith("desk:", StringComparison.Ordinal);

    private static bool Resolve(RebirthNpcWorkAssignment assignment, out Bounds volume, out long revision)
    {
        volume = default(Bounds); revision = 0;
        if (assignment == null || assignment.TargetKey == null) return false;
        var parts = assignment.TargetKey.Split(':');
        if (parts.Length != 3 || parts[0] != "desk" ||
            (parts[2] != "Farm" && parts[2] != "Structure")) return false;
        var world = GameManager.Instance?.World;
        if (world == null || world.IsRemote()) return false;
        var actor = RebirthNpcInteractionMutationHandlers.FindActorPlayer(assignment.ActorId);
        var owner = actor == null ? null : GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
        return owner != null && RebirthCompanionDeskStore.TryResolveTarget(world, owner, parts[1], parts[2], out volume, out revision);
    }

    internal static bool TryResolveFarmVolume(RebirthNpcWorkAssignment assignment,out Bounds volume,out long revision)
    {
        volume=default(Bounds);revision=0;
        return assignment!=null&&assignment.TargetKey!=null&&
            assignment.TargetKey.StartsWith("desk:",StringComparison.Ordinal)&&
            assignment.TargetKey.EndsWith(":Farm",StringComparison.Ordinal)&&
            Resolve(assignment,out volume,out revision);
    }

    internal static bool TryResolveStructureVolume(RebirthNpcWorkAssignment assignment,out Bounds volume,out long revision)
    {
        volume=default(Bounds);revision=0;
        return assignment!=null&&assignment.TargetKey!=null&&
            assignment.TargetKey.StartsWith("desk:",StringComparison.Ordinal)&&
            assignment.TargetKey.EndsWith(":Structure",StringComparison.Ordinal)&&
            Resolve(assignment,out volume,out revision);
    }

    internal static bool ContainsFarmPoint(RebirthNpcWorkAssignment assignment, Vector3 point, out long revision)
        => ContainsPoint(assignment, "Farm", point, out revision);

    internal static bool ContainsStructurePoint(RebirthNpcWorkAssignment assignment, Vector3 point, out long revision)
        => ContainsPoint(assignment, "Structure", point, out revision);

    private static bool ContainsPoint(RebirthNpcWorkAssignment assignment, string kind, Vector3 point,
        out long revision)
    {
        revision = 0;
        var parts = assignment?.TargetKey?.Split(':');
        if (parts == null || parts.Length != 3 || parts[0] != "desk" || parts[2] != kind) return false;
        var world = GameManager.Instance?.World;
        if (world == null || world.IsRemote()) return false;
        var actor = RebirthNpcInteractionMutationHandlers.FindActorPlayer(assignment.ActorId);
        var owner = actor == null ? null :
            GameManager.Instance?.GetPersistentPlayerList()?.GetPlayerDataFromEntityID(actor.entityId)?.PrimaryId;
        return owner != null && RebirthCompanionDeskStore.TryContainsTargetPoint(world, owner, parts[1],
            kind, point, out revision);
    }

    public bool TryResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition,
        out RebirthNpcWorkTargetSnapshot target, out string detail)
    {
        target = null;
        if (!Resolve(assignment, out var volume, out var revision)) { detail = "Saved desk target is unavailable."; return false; }
        target = new RebirthNpcWorkTargetSnapshot
        {
            AdapterId = AdapterId, StableTargetId = assignment.TargetKey,
            TargetRevision = revision.ToString(CultureInfo.InvariantCulture),
            Position = volume.center, IsLoaded = true, IsAvailable = true,
            InteractionRange = Math.Max(2.25f, volume.extents.magnitude),
            Detail = "Saved desk volume resolved."
        };
        detail = target.Detail;
        return true;
    }

    public bool Validate(RebirthNpcConcreteWorkContext context, out RebirthNpcWorkFailureCategory failure,
        out string detail)
    {
        failure = RebirthNpcWorkFailureCategory.TargetUnavailable;
        if (context?.Assignment == null || context.Target == null ||
            context.Target.StableTargetId != context.Assignment.TargetKey ||
            !Resolve(context.Assignment, out var volume, out var revision) ||
            context.Target.TargetRevision != revision.ToString(CultureInfo.InvariantCulture))
        { detail = "Saved desk target is unavailable or changed."; return false; }
        context.Target.Position = volume.center;
        context.Target.InteractionRange = Math.Max(2.25f, volume.extents.magnitude);
        failure = RebirthNpcWorkFailureCategory.None;
        detail = "Saved desk volume remains available.";
        return true;
    }

    public bool IsNpcInRange(RebirthNpcConcreteWorkContext context, out string detail)
    {
        if (context?.Assignment == null || !Resolve(context.Assignment, out var volume, out _) ||
            !RebirthNpcWorkNavigationService.TryResolveNpc(context.Assignment.NpcId, out var npc))
        { detail = "Worker or saved desk volume is unavailable."; return false; }
        bool inside = volume.SqrDistance(npc.position) <= 2.25f * 2.25f;
        detail = inside ? "Worker is inside the desk target volume." : "Worker must travel to the desk target volume.";
        return inside;
    }
}

#nullable disable

/// <summary>Resolves position-backed work targets without scanning the world.</summary>
public sealed class RebirthNpcPositionWorkTargetAdapter : IRebirthNpcWorkTargetAdapter
{
    public string AdapterId => "rebirth.target.position";
    public int Priority => 200;

    public bool CanResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition)
    {
        return assignment != null && assignment.HasTargetPosition &&
            (assignment.TargetKey == null || !assignment.TargetKey.StartsWith("desk:", StringComparison.Ordinal));
    }

    public bool TryResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition,
        out RebirthNpcWorkTargetSnapshot target, out string detail)
    {
        target = null;
        detail = string.Empty;
        if (assignment == null || !assignment.HasTargetPosition)
        {
            detail = "Assignment does not contain a world position.";
            return false;
        }
        Vector3 p = assignment.TargetPosition;
        Vector3i block = new Vector3i(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        BlockValue value = world != null ? world.GetBlock(block) : BlockValue.Air;
        target = new RebirthNpcWorkTargetSnapshot
        {
            AdapterId = AdapterId,
            StableTargetId = "block:" + block.x.ToString(CultureInfo.InvariantCulture) + ":" +
                block.y.ToString(CultureInfo.InvariantCulture) + ":" + block.z.ToString(CultureInfo.InvariantCulture),
            TargetRevision = BuildRevision(value),
            Position = p,
            IsLoaded = world != null,
            IsAvailable = world != null,
            InteractionRange = ResolveRange(definition),
            Detail = world == null ? "World is unavailable." : "Position target resolved."
        };
        detail = target.Detail;
        return true;
    }

    public bool Validate(RebirthNpcConcreteWorkContext context, out RebirthNpcWorkFailureCategory failure,
        out string detail)
    {
        failure = RebirthNpcWorkFailureCategory.None;
        detail = string.Empty;
        if (context == null || context.Target == null)
        {
            failure = RebirthNpcWorkFailureCategory.InvalidTarget;
            detail = "Work target context is unavailable.";
            return false;
        }
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            failure = RebirthNpcWorkFailureCategory.TargetUnavailable;
            detail = "World is not loaded.";
            return false;
        }
        Vector3 p = context.Target.Position;
        Vector3i block = new Vector3i(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), Mathf.FloorToInt(p.z));
        string current = BuildRevision(world.GetBlock(block));
        if (!string.Equals(context.Target.TargetRevision, current, StringComparison.Ordinal))
        {
            failure = RebirthNpcWorkFailureCategory.TargetChanged;
            detail = "Block target changed after assignment resolution.";
            return false;
        }
        detail = "Position target remains valid.";
        return true;
    }

    public bool IsNpcInRange(RebirthNpcConcreteWorkContext context, out string detail)
    {
        detail = string.Empty;
        EntityRebirthNPC npc;
        if (!RebirthNpcWorkNavigationService.TryResolveNpc(context.Assignment.NpcId, out npc))
        {
            detail = "Assigned NPC is not currently loaded.";
            return false;
        }
        float range = Math.Max(0.5f, context.Target.InteractionRange);
        bool inside = (npc.position - context.Target.Position).sqrMagnitude <= range * range;
        detail = inside ? "NPC is inside interaction range." : "NPC must navigate to the work target.";
        return inside;
    }

    private static float ResolveRange(RebirthNpcWorkDefinition definition)
    {
        if (definition == null) return 2.25f;
        switch (definition.Kind)
        {
            case RebirthNpcWorkKind.GuardDuty: return 4f;
            case RebirthNpcWorkKind.Hauling: return 2.75f;
            default: return 2.25f;
        }
    }

    private static string BuildRevision(BlockValue value)
    {
        return value.type.ToString(CultureInfo.InvariantCulture) + ":" + value.meta.ToString(CultureInfo.InvariantCulture) +
            ":" + value.rotation.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>Resolves stable REBIRTH NPC targets encoded as npc:&lt;stable-id&gt;.</summary>
public sealed class RebirthNpcStableEntityWorkTargetAdapter : IRebirthNpcWorkTargetAdapter
{
    public string AdapterId => "rebirth.target.stable-npc";
    public int Priority => 300;

    public bool CanResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition)
    {
        return assignment != null && !string.IsNullOrEmpty(assignment.TargetKey) &&
            assignment.TargetKey.StartsWith("npc:", StringComparison.OrdinalIgnoreCase);
    }

    public bool TryResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition,
        out RebirthNpcWorkTargetSnapshot target, out string detail)
    {
        target = null;
        detail = string.Empty;
        RebirthNpcStableId stableId;
        if (!TryParse(assignment.TargetKey, out stableId))
        {
            detail = "Stable NPC target key is invalid.";
            return false;
        }
        int entityId;
        EntityRebirthNPC entity = null;
        if (RebirthNpcRuntimeRegistry.TryGetEntityId(stableId, out entityId) && GameManager.Instance != null &&
            GameManager.Instance.World != null)
            entity = GameManager.Instance.World.GetEntity(entityId) as EntityRebirthNPC;
        target = new RebirthNpcWorkTargetSnapshot
        {
            AdapterId = AdapterId,
            StableTargetId = "npc:" + stableId,
            TargetRevision = entity != null && entity.RebirthRuntimeState != null ?
                entity.RebirthRuntimeState.Revision.ToString(CultureInfo.InvariantCulture) : string.Empty,
            Position = entity != null ? entity.position : assignment.TargetPosition,
            IsLoaded = entity != null,
            IsAvailable = entity != null && !entity.IsDead(),
            InteractionRange = 3f,
            Detail = entity == null ? "Stable NPC target is currently unloaded." : "Stable NPC target resolved."
        };
        detail = target.Detail;
        return true;
    }

    public bool Validate(RebirthNpcConcreteWorkContext context, out RebirthNpcWorkFailureCategory failure,
        out string detail)
    {
        failure = RebirthNpcWorkFailureCategory.None;
        detail = string.Empty;
        RebirthNpcStableId stableId;
        if (context == null || context.Target == null || !TryParse(context.Target.StableTargetId, out stableId))
        {
            failure = RebirthNpcWorkFailureCategory.InvalidTarget;
            detail = "Stable NPC target identity is invalid.";
            return false;
        }
        EntityRebirthNPC entity;
        if (!RebirthNpcWorkNavigationService.TryResolveNpc(stableId, out entity) || entity.IsDead())
        {
            failure = RebirthNpcWorkFailureCategory.TargetUnavailable;
            detail = "Stable NPC target is unavailable.";
            return false;
        }
        context.Target.Position = entity.position;
        context.Target.IsLoaded = true;
        context.Target.IsAvailable = true;
        detail = "Stable NPC target remains available.";
        return true;
    }

    public bool IsNpcInRange(RebirthNpcConcreteWorkContext context, out string detail)
    {
        EntityRebirthNPC worker;
        if (!RebirthNpcWorkNavigationService.TryResolveNpc(context.Assignment.NpcId, out worker))
        {
            detail = "Assigned NPC is not currently loaded.";
            return false;
        }
        float range = Math.Max(0.5f, context.Target.InteractionRange);
        bool inside = (worker.position - context.Target.Position).sqrMagnitude <= range * range;
        detail = inside ? "NPC is inside interaction range." : "NPC must navigate to the stable entity target.";
        return inside;
    }

    private static bool TryParse(string value, out RebirthNpcStableId stableId)
    {
        stableId = default(RebirthNpcStableId);
        if (string.IsNullOrEmpty(value)) return false;
        int colon = value.IndexOf(':');
        string id = colon >= 0 ? value.Substring(colon + 1) : value;
        return RebirthNpcStableId.TryParse(id, out stableId);
    }
}
