using System;

#nullable disable

/// <summary>Hides trader drone help from players who do not own a drone.</summary>
public sealed class DialogRequirementRebirthHasDrone : BaseDialogRequirement
{
    public override string GetRequiredDescription(EntityPlayer player)
    {
        return string.Empty;
    }

    public override bool CheckRequirement(EntityPlayer player, EntityNPC talkingTo)
    {
        if (player == null) return false;

        // The owned-entity list also exists on remote clients, where DroneManager has no data.
        foreach (OwnedEntityData owned in player.ownedEntities)
        {
            EntityClass entityClass;
            if (owned != null && EntityClass.list.TryGetValue(owned.ClassId, out entityClass) &&
                typeof(EntityDrone).IsAssignableFrom(entityClass.classname))
                return true;
        }

        DroneManager drones = DroneManager.Instance;
        if (drones == null) return false;
        foreach (EntityDrone drone in drones.dronesActive)
            if (drone != null && drone.belongsPlayerId == player.entityId) return true;
        foreach (EntityCreationData drone in drones.dronesUnloaded)
            if (drone != null && drone.belongsPlayerId == player.entityId) return true;
        return false;
    }
}
