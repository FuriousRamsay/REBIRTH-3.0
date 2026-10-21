internal static partial class RebirthHydrationOwnerHold
{
    internal static bool TryReleaseRemoteClient(RebirthHydrationRemoteClientTerminal terminal)
    {
        var grant=terminal?.Grant;
        if(grant==null||!terminal.CurrentFor(grant)||!RebirthHydrationTerminalSeenCheckpoint.HasSaved(grant)||!entries.TryGetValue(grant.Player,out var entry)||
            entry.Original.Digest!=grant.Original.Digest||!Current(entry))return false;
        if(!grant.TryRetire(terminal)||!entries.TryGetValue(grant.Player,out var current)||!ReferenceEquals(current,entry))return false;
        if(!entries.Remove(grant.Player))return false;
        var remove=new System.Collections.Generic.List<EntityPlayerLocal>();
        foreach(var pair in entries)if(pair.Value.Original.Digest==grant.Original.Digest&&!Current(pair.Value))remove.Add(pair.Key);
        foreach(var key in remove)entries.Remove(key);
        reenteredRemoteEntries.RemoveAll(value=>value.Original.Digest==grant.Original.Digest&&!Current(value));
        return true;
    }
}
