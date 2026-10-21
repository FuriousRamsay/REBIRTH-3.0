using System;
using HarmonyLib;

// Native server admission for the future remote management request endpoint.
// This grants no cursor/item mutation or remote UI activation by itself.
public static class RebirthCamperRemoteAdmission
{
    // Native LockEntry and its owner lookup are public; only the dictionary
    // field is private. Resolve that one field once rather than scanning locks.
    private static readonly AccessTools.FieldRef<LockManager, OneToManyDictionary<int, LockManager.LockEntry>> locks
        = AccessTools.FieldRefAccess<LockManager, OneToManyDictionary<int, LockManager.LockEntry>>("singleLocks");
    internal static int NativeInteractionOwner(EntityVehicle vehicle)
    {
        if(vehicle?.world==null)return -1;
        if(vehicle.world.IsRemote())
        {
            string text;int owner;
            return vehicle.vehicle.GetUpdatedItemValue().TryGetMetadata(RebirthCamperInteractionVisibility.OwnerKey,out text)
                &&int.TryParse(text,out owner)?owner:-1;
        }
        var manager=LockManager.Instance;int nativeOwner;
        return manager!=null&&locks(manager).TryGetByValue(new LockManager.LockEntry(vehicle,0),out nativeOwner)?nativeOwner:-1;
    }

    public static bool CanAccess(EntityVehicle vehicle, EntityPlayer player, ClientInfo sender,
        Func<bool> authenticatedCharacter)
    {
        var connection=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (!ThreadManager.IsMainThread() || connection?.IsServer!=true || vehicle == null || vehicle.vehicle == null
            || vehicle.world == null || vehicle.world.IsRemote() || player == null || player.IsDead()
            || !ReferenceEquals(GameManager.Instance?.World,vehicle.world)
            || sender == null || sender.InternalId == null || sender.entityId != player.entityId||!sender.loginDone||sender.disconnecting
            || !ReferenceEquals(connection.Clients?.ForEntityId(player.entityId),sender)
            || sender.netConnection==null||sender.netConnection.Length==0||sender.netConnection[0]==null||sender.netConnection[0].IsDisconnected()
            || authenticatedCharacter == null || !authenticatedCharacter()
            || !ReferenceEquals(vehicle.world, player.world)
            || !ReferenceEquals(vehicle.world.GetEntity(vehicle.entityId), vehicle)
            || !ReferenceEquals(vehicle.world.GetEntity(player.entityId), player)
            || !RebirthCamperRadial.IsCamper(vehicle)
            || (vehicle.position - player.position).sqrMagnitude > 64f) return false;
        // EntityVehicle.AllowActivationCommand uses the local platform player.
        // Apply its exact service security rule to this authenticated sender.
        var user = sender.InternalId;
        if (vehicle.IsLocked() && vehicle.hasLock()
            && !(vehicle.vehicle.OwnerId != null && vehicle.vehicle.OwnerId.Equals(user))
            && !vehicle.isAllowedUser(user)) return false;
        int nativeOwner;
        var nativeLocks = LockManager.Instance;
        return nativeLocks != null && locks(nativeLocks).TryGetByValue(new LockManager.LockEntry(vehicle, 0), out nativeOwner)
            && nativeOwner == sender.entityId && authenticatedCharacter();
    }
}
