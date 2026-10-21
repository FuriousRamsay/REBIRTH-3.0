using System;
using System.IO;

// Saved fire rows belong to the native world that loaded them. A later global
// save-directory change must never redirect their next autosave or deletion.
internal static class RebirthFirePersistenceScope
{
    private static World originalWorld;
    private static string originalDirectory, originalGuid;

    internal static void Reset()
    {originalWorld=null;originalDirectory=null;originalGuid=null;}

    internal static void Bind(World world)
    {
        if(!Authority(world))throw new InvalidOperationException("Current authority world required for fire persistence");
        string directory=DirectoryNow();
        if(originalWorld!=null)
        {
            if(!ReferenceEquals(originalWorld,world)||originalDirectory!=directory||originalGuid!=world.worldState?.Guid)
                throw new InvalidOperationException("Original fire persistence session changed without reset");
            return;
        }
        originalWorld=world;originalDirectory=directory;originalGuid=world.worldState?.Guid;
    }

    internal static string CurrentPath()
    {
        if(!IsCurrentWorld(originalWorld)||originalDirectory!=DirectoryNow())
            throw new InvalidOperationException("Original fire persistence world or save directory is no longer current");
        return Path.Combine(originalDirectory,"RebirthFireManager.dat");
    }

    // Hot simulation guard: no filesystem/path normalization per block or tick.
    internal static bool IsCurrentWorld(World world)=>originalWorld!=null&&ReferenceEquals(world,originalWorld)&&
        Authority(world)&&originalGuid==world.worldState?.Guid;

    private static string DirectoryNow()
    {
        string directory=GameIO.GetSaveGameDir();
        if(string.IsNullOrEmpty(directory))throw new InvalidOperationException("Actual fire save directory required");
        return Path.GetFullPath(directory);
    }

    private static bool Authority(World world)=>ThreadManager.IsMainThread()&&world!=null&&!world.IsRemote()&&
        ReferenceEquals(world,GameManager.Instance?.World)&&SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer==true;
}
