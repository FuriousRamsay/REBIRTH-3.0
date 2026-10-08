using System;
using System.Collections.Generic;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.Scripting;

#nullable disable

internal static class RebirthFireMinEventHelpers
{
    public static Vector3i ResolveBlockPosition(MinEventParams parameters, bool useEventPosition)
    {
        if (useEventPosition)
            return new Vector3i(parameters.Position);
        if (Voxel.voxelRayHitInfo.bHitValid)
            return Voxel.voxelRayHitInfo.hit.blockPos;
        return new Vector3i(parameters.Position);
    }

    public static bool ParsePositionTarget(XAttribute attribute, ref bool useEventPosition)
    {
        if (attribute.Name.LocalName != "target")
            return false;
        useEventPosition = attribute.Value.Equals(
            "positionAOE",
            StringComparison.OrdinalIgnoreCase);
        return true;
    }

    public static bool IsMeleeHitWithinBlockRange(MinEventParams parameters, bool useEventPosition)
    {
        if (useEventPosition || !Voxel.voxelRayHitInfo.bHitValid || parameters == null ||
            parameters.Self == null || parameters.Self.inventory == null ||
            parameters.Self.inventory.holdingItemData == null ||
            parameters.Self.inventory.holdingItemData.item == null ||
            parameters.Self.inventory.holdingItemData.item.Actions == null ||
            parameters.Self.inventory.holdingItemData.item.Actions.Length == 0)
            return true;

        ItemAction action = parameters.Self.inventory.holdingItemData.item.Actions[0];
        float blockRange;
        if (action is ItemActionMelee melee)
            blockRange = melee.BlockRange;
        else if (action is ItemActionDynamicMelee dynamicMelee)
            blockRange = dynamicMelee.BlockRange;
        else
            return true;

        if (blockRange <= 0f)
            return true;
        return Vector3.Distance(Voxel.voxelRayHitInfo.hit.blockPos, parameters.Position) <= blockRange;
    }

    public static int ResolveSourceEntityId(MinEventParams parameters)
    {
        if (parameters.Self != null)
            return parameters.Self.entityId;
        if (parameters.Instigator != null)
            return parameters.Instigator.entityId;
        return -1;
    }

    public static void FillCube(List<Vector3i> output, Vector3i center, int range)
    {
        output.Clear();
        int radius = Math.Max(0, Math.Min(8, range));
        // RP40: build a bounded center-first candidate set. The former xyz traversal
        // selected a negative-corner prefix when the request cap was reached.
        for (int shell = 0; shell <= radius; shell++)
        {
            for (int x = -shell; x <= shell; x++)
            {
                for (int y = -shell; y <= shell; y++)
                {
                    for (int z = -shell; z <= shell; z++)
                    {
                        if (Math.Max(Math.Abs(x), Math.Max(Math.Abs(y), Math.Abs(z))) != shell)
                            continue;
                        if (output.Count >= RebirthFireDefaults.MaxRequestPositions)
                            return;
                        output.Add(center + new Vector3i(x, y, z));
                    }
                }
            }
        }
    }

    public static bool IsServer()
    {
        ConnectionManager manager = SingletonMonoBehaviour<ConnectionManager>.Instance;
        return manager == null || manager.IsServer;
    }
}

[Preserve]
public class MinEventActionRebirthAddFireDamage : MinEventActionBase
{
    private readonly List<Vector3i> positions = new List<Vector3i>();
    private int range;
    private bool useEventPosition;
    private float delaySeconds;
    private RebirthFireIgnitionCause cause = RebirthFireIgnitionCause.BlockEvent;

    public override void Execute(MinEventParams parameters)
    {
        #if DEBUG
        RebirthFireDiagnostics.AddFireMinEventCalls++;
        #endif
        if (!RebirthFireRuntimePolicy.Enabled)
        {
            #if DEBUG
            RebirthFireDiagnostics.AddFireMinEventSkippedDisabled++;
            #endif
            return;
        }
        if (parameters == null)
        {
            #if DEBUG
            RebirthFireDiagnostics.AddFireMinEventSkippedInvalidContext++;
            #endif
            return;
        }
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
        {
            #if DEBUG
            RebirthFireDiagnostics.AddFireMinEventSkippedInvalidContext++;
            #endif
            return;
        }

        if (!RebirthFireMinEventHelpers.IsMeleeHitWithinBlockRange(parameters, useEventPosition))
        {
            #if DEBUG
            RebirthFireDiagnostics.AddFireMinEventSkippedRange++;
            #endif
            return;
        }
        Vector3i center = RebirthFireMinEventHelpers.ResolveBlockPosition(parameters, useEventPosition);
        RebirthFireMinEventHelpers.FillCube(positions, center, range);
        int source = RebirthFireMinEventHelpers.ResolveSourceEntityId(parameters);

        #if DEBUG
        RebirthFireDiagnostics.AddFireMinEventPositions += positions.Count;
        #endif
        if (RebirthFireMinEventHelpers.IsServer())
        {
            #if DEBUG
            RebirthFireDiagnostics.AddFireMinEventServerSchedules++;
            #endif
            RebirthFireService.Instance.ScheduleIgnition(world, positions, source, cause, delaySeconds);
        }
        else
        {
            #if DEBUG
            RebirthFireDiagnostics.AddFireMinEventClientRequests++;
            #endif
            RebirthFireNetwork.SendRequest(
                RebirthFireRequestOperation.Ignite,
                source,
                cause,
                positions,
                delaySeconds);
        }
    }

    public override bool ParseXmlAttribute(XAttribute attribute)
    {
        if (base.ParseXmlAttribute(attribute))
            return true;
        if (RebirthFireMinEventHelpers.ParsePositionTarget(attribute, ref useEventPosition))
            return true;
        switch (attribute.Name.LocalName)
        {
            case "range":
                range = Math.Max(0, (int)StringParsers.ParseFloat(attribute.Value));
                return true;
            case "delayTime":
                // 2.6 fed this value to Task.Delay, so it was milliseconds despite one
                // older documentation example implying seconds.
                delaySeconds = Math.Max(0f, StringParsers.ParseFloat(attribute.Value) / 1000f);
                return true;
            case "delaySeconds":
                delaySeconds = Math.Max(0f, StringParsers.ParseFloat(attribute.Value));
                return true;
            case "cause":
                cause = EnumUtils.Parse<RebirthFireIgnitionCause>(attribute.Value, true);
                return true;
            default:
                return false;
        }
    }
}

[Preserve]
public class MinEventActionRebirthAddFireDamageCascade : MinEventActionBase
{
    private enum CascadeFilter
    {
        Type,
        Material,
        MaterialDamage,
        MaterialSurface
    }

    private readonly List<Vector3i> positions = new List<Vector3i>();
    private readonly List<Vector3i> candidates = new List<Vector3i>();
    private int range;
    private bool useEventPosition;
    private CascadeFilter filter = CascadeFilter.Type;
    private RebirthFireIgnitionCause cause = RebirthFireIgnitionCause.Scripted;

    public override void Execute(MinEventParams parameters)
    {
        if (!RebirthFireRuntimePolicy.Enabled || parameters == null)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;

        Vector3i center = RebirthFireMinEventHelpers.ResolveBlockPosition(parameters, useEventPosition);
        BlockValue target = world.GetBlock(center);
        MaterialBlock targetMaterial = target.Block != null ? target.Block.blockMaterial : null;
        positions.Clear();
        RebirthFireMinEventHelpers.FillCube(candidates, center, range);
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector3i position = candidates[i];
            BlockValue neighbor = world.GetBlock(position);
            if (Matches(target, targetMaterial, neighbor))
                positions.Add(position);
        }

        int source = RebirthFireMinEventHelpers.ResolveSourceEntityId(parameters);
        if (RebirthFireMinEventHelpers.IsServer())
            RebirthFireService.Instance.ScheduleIgnition(world, positions, source, cause, 0f);
        else
            RebirthFireNetwork.SendRequest(
                RebirthFireRequestOperation.Ignite,
                source,
                cause,
                positions,
                0f);
    }

    private bool Matches(BlockValue target, MaterialBlock targetMaterial, BlockValue neighbor)
    {
        if (filter == CascadeFilter.Type)
            return neighbor.type == target.type;
        MaterialBlock neighborMaterial = neighbor.Block != null ? neighbor.Block.blockMaterial : null;
        if (targetMaterial == null || neighborMaterial == null)
            return false;
        if (filter == CascadeFilter.Material)
            return string.Equals(neighborMaterial.id, targetMaterial.id, StringComparison.OrdinalIgnoreCase);
        if (filter == CascadeFilter.MaterialDamage)
            return string.Equals(neighborMaterial.DamageCategory, targetMaterial.DamageCategory, StringComparison.OrdinalIgnoreCase);
        return string.Equals(neighborMaterial.SurfaceCategory, targetMaterial.SurfaceCategory, StringComparison.OrdinalIgnoreCase);
    }

    public override bool ParseXmlAttribute(XAttribute attribute)
    {
        if (base.ParseXmlAttribute(attribute))
            return true;
        if (RebirthFireMinEventHelpers.ParsePositionTarget(attribute, ref useEventPosition))
            return true;
        switch (attribute.Name.LocalName)
        {
            case "range":
                range = Math.Max(0, (int)StringParsers.ParseFloat(attribute.Value));
                return true;
            case "filter":
                filter = EnumUtils.Parse<CascadeFilter>(attribute.Value, true);
                return true;
            case "cause":
                cause = EnumUtils.Parse<RebirthFireIgnitionCause>(attribute.Value, true);
                return true;
            default:
                return false;
        }
    }
}

[Preserve]
public class MinEventActionRebirthCheckFireProximity : MinEventActionBase
{
    private int range = 5;
    private string cvar = "_closeFires";

    public override void Execute(MinEventParams parameters)
    {
        if (parameters == null || parameters.Self == null)
            return;
        Vector3i center = new Vector3i(parameters.Self.position);
        int count = RebirthFireMinEventHelpers.IsServer()
            ? RebirthFireService.Instance.CountVisibleNearby(parameters.Self.entityId, center, range)
            : RebirthFireVisualManager.CountNearby(center, range);
        parameters.Self.Buffs.SetCustomVar(cvar, count == 0 ? -1f : count);
    }

    public override bool ParseXmlAttribute(XAttribute attribute)
    {
        if (base.ParseXmlAttribute(attribute))
            return true;
        if (attribute.Name.LocalName == "range")
        {
            range = Math.Max(0, (int)StringParsers.ParseFloat(attribute.Value));
            return true;
        }
        if (attribute.Name.LocalName == "cvar")
        {
            cvar = attribute.Value;
            return true;
        }
        return false;
    }
}

[Preserve]
public class MinEventActionRebirthRemoveFire : MinEventActionBase
{
    private readonly List<Vector3i> positions = new List<Vector3i>();
    private int range;
    private bool useEventPosition;
    private float smokeSeconds = RebirthFireDefaults.SmokeSeconds;

    public override void Execute(MinEventParams parameters)
    {
        if (parameters == null)
            return;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;

        Vector3i center = RebirthFireMinEventHelpers.ResolveBlockPosition(parameters, useEventPosition);
        RebirthFireMinEventHelpers.FillCube(positions, center, range);
        int source = RebirthFireMinEventHelpers.ResolveSourceEntityId(parameters);

        if (RebirthFireMinEventHelpers.IsServer())
            RebirthFireService.Instance.ExtinguishMany(world, positions, smokeSeconds);
        else
            RebirthFireNetwork.SendRequest(
                RebirthFireRequestOperation.Extinguish,
                source,
                RebirthFireIgnitionCause.Scripted,
                positions,
                smokeSeconds);
    }

    public override bool ParseXmlAttribute(XAttribute attribute)
    {
        if (base.ParseXmlAttribute(attribute))
            return true;
        if (RebirthFireMinEventHelpers.ParsePositionTarget(attribute, ref useEventPosition))
            return true;
        if (attribute.Name.LocalName == "range")
        {
            range = Math.Max(0, (int)StringParsers.ParseFloat(attribute.Value));
            return true;
        }
        if (attribute.Name.LocalName == "smokeTime" || attribute.Name.LocalName == "smokeSeconds")
        {
            smokeSeconds = Math.Max(0f, StringParsers.ParseFloat(attribute.Value));
            return true;
        }
        return false;
    }
}


// Preserve the exact 2.6 XML action names so existing REBIRTH content and third-party
// integrations do not need to be rewritten. The Rebirth-prefixed names remain available
// for new 3.1-authored XML.
[Preserve]
public sealed class MinEventActionAddFireDamage : MinEventActionRebirthAddFireDamage
{
}

[Preserve]
public sealed class MinEventActionAddFireDamageCascade : MinEventActionRebirthAddFireDamageCascade
{
}

[Preserve]
public sealed class MinEventActionCheckFireProximity : MinEventActionRebirthCheckFireProximity
{
}

[Preserve]
public sealed class MinEventActionRemoveFire : MinEventActionRebirthRemoveFire
{
}
