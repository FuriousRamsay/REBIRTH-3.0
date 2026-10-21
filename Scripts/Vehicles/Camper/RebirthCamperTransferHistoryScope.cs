using System;
using System.Collections.Generic;
using System.IO;

// Only current authoritative world lookups reuse metadata. Detached/offline
// journal readers retain the original direct validation path.
internal static class RebirthCamperTransferHistoryScope
{
    private static World world;
    private static string save;
    private static readonly Dictionary<string,RebirthCamperTransferPendingIndex> indexes=
        new Dictionary<string,RebirthCamperTransferPendingIndex>(StringComparer.OrdinalIgnoreCase);
    internal static bool TryGet(RebirthCamperTransferJournal journal,out RebirthCamperTransferPendingIndex index)
    {
        index=null;
        var current=GameManager.Instance?.World;
        if(!ThreadManager.IsMainThread()||current==null||current.IsRemote())return false;
        string directory=GameIO.GetSaveGameDir();if(string.IsNullOrWhiteSpace(directory))return false;
        directory=Path.GetFullPath(directory);
        string prefix=Path.Combine(directory,"REBIRTH")+Path.DirectorySeparatorChar;
        if(!journal.DirectoryPath.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))return false;
        if(!ReferenceEquals(world,current)||!string.Equals(save,directory,StringComparison.OrdinalIgnoreCase))
        {Clear();world=current;save=directory;}
        if(!indexes.TryGetValue(journal.DirectoryPath,out index))
        {index=new RebirthCamperTransferPendingIndex(journal);indexes.Add(journal.DirectoryPath,index);}
        return true;
    }
    internal static void Clear()
    {foreach(var index in indexes.Values)index.Dispose();indexes.Clear();world=null;save=null;}
}
