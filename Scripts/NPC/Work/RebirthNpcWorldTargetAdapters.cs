using System;
using System.Globalization;
using UnityEngine;

#nullable disable

/// <summary>Resolves position-backed work targets without scanning the world.</summary>
public sealed class RebirthNpcPositionWorkTargetAdapter : IRebirthNpcWorkTargetAdapter
{
    public string AdapterId => "rebirth.target.position";
    public int Priority => 200;

    public bool CanResolve(RebirthNpcWorkAssignment assignment, RebirthNpcWorkDefinition definition)
    {
        return assignment != null && assignment.HasTargetPosition;
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
