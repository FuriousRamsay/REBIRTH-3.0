using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// Restores the REBIRTH 2.6 mutated-spit minion spawn behavior using the native
/// 3.0 onProjectileImpact event. Authority, chance, Blood Moon suppression and
/// population limits are enforced on the server.
/// </summary>
[Preserve]
public sealed class MinEventActionRebirthMutatedSpitImpact : MinEventActionBase
{
    private const string MinionClassName = "FuriousRamsayZombieMutatedMinion";
    private const int SpawnRollSides = 6;
    private const int MaximumNearbyMinions = 12;
    private const float CountRadius = 40f;
    private static readonly FastTags<TagGroup.Global> CanSpawnTag =
        FastTags<TagGroup.Global>.Parse("canSpawn");

    public override void Execute(MinEventParams _params)
    {
        if (!SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)
            return;

        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityAlive shooter = _params != null ? _params.Self : null;
        if (world == null || shooter == null || !shooter.HasAnyTags(CanSpawnTag))
            return;

        // Preserve the 2.6 IsntBloodMoon requirement.
        if (SkyManager.IsBloodMoonVisible())
            return;

        // The old SetNumSpawns value of 6 represented a one-in-six impact roll.
        if (world.GetGameRandom().RandomFloat >= (1f / SpawnRollSides))
            return;

        Vector3 impactPosition = _params.Position;
        if (CountNearbyMinions(world, impactPosition) >= MaximumNearbyMinions)
            return;

        int classId = EntityClass.FromString(MinionClassName);
        if (EntityClass.GetEntityClass(classId) == null)
        {
            Log.Error("[REBIRTH Mutated Spit] Missing entity class '" + MinionClassName + "'.");
            return;
        }

        // Lift the origin slightly so an impact against terrain does not create
        // the minion with its feet embedded in the hit voxel.
        Vector3 spawnPosition = impactPosition + new Vector3(0f, 0.35f, 0f);
        EntityAlive minion = EntityFactory.CreateEntity(classId, spawnPosition) as EntityAlive;
        if (minion == null)
            return;

        world.SpawnEntityInWorld(minion);
    }

    private static int CountNearbyMinions(World world, Vector3 center)
    {
        List<Entity> entities = new List<Entity>();
        Bounds bounds = new Bounds(center, new Vector3(CountRadius * 2f, 30f, CountRadius * 2f));
        world.GetEntitiesInBounds(typeof(EntityAlive), bounds, entities);

        float radiusSquared = CountRadius * CountRadius;
        int count = 0;
        for (int i = 0; i < entities.Count; i++)
        {
            EntityAlive entity = entities[i] as EntityAlive;
            if (entity == null || entity.IsDead() || entity.EntityClass == null)
                continue;
            if (!string.Equals(entity.EntityClass.entityClassName, MinionClassName, StringComparison.Ordinal))
                continue;

            Vector3 delta = entity.position - center;
            delta.y = 0f;
            if (delta.sqrMagnitude <= radiusSquared && ++count >= MaximumNearbyMinions)
                return count;
        }
        return count;
    }
}
