internal static partial class RebirthProductiveEarnedUiDeferredTransport
{
    // Existing observation only, usable by the actual native load callback
    // before its new Tile is installed in the live World. Never initializes.
    internal static bool TryGetObservedNativeReadFence(TileEntityWorkstation station,out long fence)
    {
        fence=0;if(station==null||!nativeReadVersions.TryGetValue(station,out var version)||version.Value<1)return false;
        fence=version.Value;return true;
    }
    // Read-only ABA fence, never a native read/lineage producer. A zero
    // baseline only records that this existing observer has seen no read.
    internal static bool TryReadNativeObservationFence(World world,TileEntityWorkstation station,out long fence)
    {
        fence=0;
        if(!ThreadManager.IsMainThread()||world==null||station==null||!ReferenceEquals(GameManager.Instance?.World,world)||
            !ReferenceEquals(world.GetTileEntity(station.ToWorldPos()),station))return false;
        if(nativeReadVersions.TryGetValue(station,out var version))fence=version.Value;
        return true;
    }
}
