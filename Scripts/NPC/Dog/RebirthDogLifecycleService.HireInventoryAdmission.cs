public static partial class RebirthDogLifecycleService
{
    // Read-only admission interop. This neither installs nor releases custody.
    internal static bool HasHireInventoryHold(EntityPlayerLocal player)=>player!=null&&
        (HireBlocksInventory(player.bag)||HireBlocksInventory(player.inventory));
}
