using System;

// Caches only the native path calculation, never saved identity or custody data.
internal sealed class RebirthNativeSaveDirectoryCache
{
    private readonly object gate = new object();
    private bool initialized;
    private Platform.UserDataStorageType storage;
    private string root, world, game, directory;

    internal string Get()
    {
        lock (gate)
        {
            // Match the installed native GetSaveGameDir evaluation order. Keep the
            // selected native root lookup live, including its fallback/errors.
            string currentWorld = GamePrefs.GetString(EnumGamePrefs.GameWorld);
            string currentGame = GamePrefs.GetString(EnumGamePrefs.GameName);
            var currentStorage = (Platform.UserDataStorageType)GamePrefs.GetInt(EnumGamePrefs.GameSaveStorageType);
            string currentRoot = GameIO.GetSaveGameRootDir(currentStorage);
            if (initialized && storage == currentStorage &&
                string.Equals(root, currentRoot, StringComparison.Ordinal) &&
                string.Equals(world, currentWorld, StringComparison.Ordinal) &&
                string.Equals(game, currentGame, StringComparison.Ordinal)) return directory;

            // Compose from this SAME observed tuple; re-reading native inputs here
            // could publish a changed path under the earlier tuple's cache key.
            // Preserve native slash concatenation and null-as-empty semantics.
            string currentDirectory = string.Concat(string.Concat(currentRoot, "/", currentWorld), "/", currentGame);
            storage = currentStorage; root = currentRoot; world = currentWorld;
            game = currentGame; directory = currentDirectory; initialized = true;
            return directory;
        }
    }
}
