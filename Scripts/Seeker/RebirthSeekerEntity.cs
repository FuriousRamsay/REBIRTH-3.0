using UnityEngine;
using UnityEngine.Scripting;

// The native zombie/copy/loot lifecycle remains authoritative. No registered Purge
// room or normal BossEvent state is created for independent Seeker encounters.
[Preserve]
public class EntityRebirthSeeker : EntityZombie
{
    private float nextDoor;
    public override void OnAddedToWorld()
    {
        base.OnAddedToWorld();
        if (ReferenceEquals(GameManager.Instance?.World, world) && !GameManager.IsDedicatedServer && NavObjectManager.HasInstance && NavObject == null)
            AddNavObject("FuriousRamsaySeeker", "", "");
    }
    public override void fallHitGround(float distance, Vector3 motion)
    {
        base.fallHitGround(distance, motion);
        if (world?.IsRemote() == false) Buffs.SetCustomVar("$FR_SeekerSkyDropNoFallDamage", 0f);
    }
    public override void OnUpdateLive()
    {
        base.OnUpdateLive();
        if (world == null || world.IsRemote() || IsDead() || Time.time < nextDoor) return;
        nextDoor = Time.time + 1f;
        RebirthSeekerDoors.TryOpen(this);
    }
    public override void dropItemOnDeath()
    {
        if (world?.IsRemote() == false && ReferenceEquals(GameManager.Instance?.World, world)
            && RebirthSandboxOptionManager.Current.SeekerRewards) base.dropItemOnDeath();
    }
}

// Minions retain native cop explosion and ranged AI, rather than importing the
// unrelated 2.6 EntityZombieCopRebirth implementation and its NPC dependencies.
[Preserve]
public class EntityRebirthSeekerMinion : EntityZombieCop
{
    private float nextDoor;
    public override void OnUpdateLive()
    {
        base.OnUpdateLive();
        if (world == null || world.IsRemote() || IsDead() || Time.time < nextDoor) return;
        nextDoor = Time.time + 1f;
        RebirthSeekerDoors.TryOpen(this);
    }
}

internal static class RebirthSeekerDoors
{
    internal static void TryOpen(EntityAlive entity)
    {
        if (!ReferenceEquals(GameManager.Instance?.World, entity.world)
            || !ReferenceEquals(entity.world.GetEntity(entity.entityId), entity)) return;
        if (entity.Buffs.GetCustomVar("$varFuriousRamsayOpenDoors") != 1f) return;
        // Native composite doors replace the removed legacy secure-door class.
        // Use the native feature and its lock/replication checks.
        if (entity.GetAttackTarget() == null) return;
        var center = entity.GetBlockPosition();
        for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++)
        {
            var tile = entity.world.GetTileEntity(center + new Vector3i(x, 0, z)) as TileEntityComposite;
            var door = tile?.GetFeature<TEFeatureDoor>();
            bool pick;
            if (door != null && !door.IsOpen() && door.CanOpen(out pick)) door.SetOpen(true, true);
        }
    }
}

[Preserve]
public sealed class MinEventActionRebirthSeekerRoar : MinEventActionBase
{
    public override void Execute(MinEventParams args)
    {
        var seeker = args.Self as EntityRebirthSeeker;
        if (seeker == null || seeker.world == null || seeker.world.IsRemote() || seeker.IsDead()
            || !ReferenceEquals(GameManager.Instance?.World, seeker.world)
            || !ReferenceEquals(seeker.world.GetEntity(seeker.entityId), seeker)) return;
        var data = new ExplosionData(seeker.EntityClass.Properties.Classes["Explosion"], null);
        GameManager.Instance.ExplosionServer(seeker.position, seeker.GetBlockPosition(), seeker.qrotation,
            data, seeker.entityId, 0f, false, ItemClass.GetItem("rebirthSeekerRoarSource", false));
    }
}

[Preserve]
public sealed class MinEventActionRebirthSeekerMinions : MinEventActionBase
{
    public override void Execute(MinEventParams args)
    {
        var seeker = args.Self as EntityRebirthSeeker;
        if (seeker == null || seeker.world == null || seeker.world.IsRemote() || seeker.IsDead()
            || !ReferenceEquals(GameManager.Instance?.World, seeker.world)
            || !ReferenceEquals(seeker.world.GetEntity(seeker.entityId), seeker)
            || RebirthSeekerPolicy.IsBloodMoon(GameManager.Instance?.World)) return;
        var player = seeker.GetAttackTarget() as EntityPlayer;
        if (player == null) player = seeker.world.GetClosestPlayer(seeker.position, 120f, false);
        if (player == null || !player.IsAlive() || (player.position - seeker.position).sqrMagnitude > 1600f) return;
        string family = seeker.Buffs.GetCustomVar(".dummySpawnSeekerMinions") == 0
            ? "rebirthSeekerKamikaze" : "rebirthSeekerCop";
        string suffix = player.gameStage >= 380 ? "Tainted" : player.gameStage >= 260 ? "Radiated" : player.gameStage >= 140 ? "Feral" : "";
        var nearby = seeker.world.GetEntitiesInBounds(typeof(EntityAlive), new Bounds(seeker.position, new Vector3(80, 30, 80)), new System.Collections.Generic.List<Entity>());
        int count = 0;
        foreach (var entity in nearby)
            if (entity is EntityRebirthSeekerMinion alive && !alive.IsDead()
                && alive.EntityClass.entityClassName.StartsWith(family, System.StringComparison.Ordinal)) count++;
        if (count >= 3) return;
        Vector3 position;
        if (!seeker.world.GetMobRandomSpawnPosWithWater(seeker.position, 1, 6, 1, true, out position)) return;
        RebirthSeekerRuntime.Spawn(seeker.world, player, family + suffix, position, false, family == "rebirthSeekerCop" ? 1.2f : 1f);
    }
}

