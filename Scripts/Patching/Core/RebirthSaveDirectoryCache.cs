using System;
using System.Threading;
using HarmonyLib;
using UnityEngine.Scripting;

#nullable disable

/// <summary>
/// GameIO.GetSaveGameDir rebuilds its path string on every call, and hundreds of REBIRTH
/// call sites read it at runtime. The result is reused only while all three inputs of the
/// native method are unchanged, so changing world, save or storage type still takes effect at once.
/// </summary>
[HarmonyPatch(typeof(GameIO), nameof(GameIO.GetSaveGameDir), new Type[0])]
internal static class RebirthSaveDirectoryCache
{
    private sealed class Entry
    {
        internal string World, Game, Path;
        internal int Storage;
    }
    private static Entry current;

    private static bool Prefix(ref string __result)
    {
        string world = GamePrefs.GetString(EnumGamePrefs.GameWorld);
        string game = GamePrefs.GetString(EnumGamePrefs.GameName);
        int storage = GamePrefs.GetInt(EnumGamePrefs.GameSaveStorageType);
        Entry entry = Volatile.Read(ref current);
        if (entry == null || entry.Storage != storage ||
            !string.Equals(entry.World, world, StringComparison.Ordinal) ||
            !string.Equals(entry.Game, game, StringComparison.Ordinal))
        {
            entry = new Entry { World = world, Game = game, Storage = storage,
                Path = GameIO.GetSaveGameDir(world, game, (Platform.UserDataStorageType)storage) };
            Volatile.Write(ref current, entry);
        }
        __result = entry.Path;
        return false;
    }
}

[Preserve]
public sealed class RebirthSaveDirectoryCacheModApi : IModApi
{
    public void InitMod(Mod mod)
    {
        RebirthHarmonyBootstrap.PatchClassOnce(new Harmony("rebirth.save-directory-cache"), typeof(RebirthSaveDirectoryCache));
    }
}
