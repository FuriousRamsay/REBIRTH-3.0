using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    internal static bool RegisterInvokedPartial(string parentTerminal,string save,Evidence preserve)
    {
        try
        {
            if(!ThreadManager.IsMainThread())return false;save=Path.GetFullPath(save);
            lock(cachedLoadGate)
            {
                var same=cachedLoadRequests.Where(value=>string.Equals(value.Directory,save,RebirthStationCompletionOriginalScope.PathComparison)&&
                    string.Equals(value.Custody.Path,parentTerminal,RebirthStationCompletionOriginalScope.PathComparison)).ToArray();
                if(same.Length>1)return false;
                if(same.Length==1&&same[0].PredictedNative&&same[0].Custody.Terminal&&!same[0].Custody.PendingNextWork)
                    return same[0].Custody.PrimaryCurrent();
                if(cachedLoadRequests.Count-same.Length>=4)return false;
                ReadCandidate(parentTerminal,save,true,true,preserve);
                same=cachedLoadRequests.Where(value=>string.Equals(value.Directory,save,RebirthStationCompletionOriginalScope.PathComparison)&&
                    string.Equals(value.Custody.Path,parentTerminal,RebirthStationCompletionOriginalScope.PathComparison)).ToArray();
                return same.Length==1&&same[0].PredictedNative&&same[0].Custody.Terminal&&!same[0].Custody.PendingNextWork&&
                    same[0].Custody.PrimaryCurrent();
            }
        }
        catch{return false;}
    }
    internal static bool RegisterInvokedChild(string parentTerminal,string childOriginal,string save,byte[] childImage)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||childImage==null)return false;
            save=Path.GetFullPath(save);
            lock(cachedLoadGate)
            {
                var existing=cachedLoadRequests.Where(value=>
                    string.Equals(value.Custody.Path,childOriginal,RebirthStationCompletionOriginalScope.PathComparison)||
                    string.Equals(value.Custody.Path,childOriginal+".earned-load",RebirthStationCompletionOriginalScope.PathComparison)).ToArray();
                if(existing.Length!=0)return existing.Length==1&&existing[0].Custody.Active&&
                    string.Equals(existing[0].Directory,save,RebirthStationCompletionOriginalScope.PathComparison)&&
                    existing[0].Custody.Original.SequenceEqual(childImage)&&existing[0].Custody.PrimaryCurrent();
                if(cachedLoadRequests.Count>=4)return false;
                int count=cachedLoadRequests.Count;ReadCandidate(parentTerminal,save,true);
                if(cachedLoadRequests.Count!=count+1)return false;
                var added=cachedLoadRequests[cachedLoadRequests.Count-1];
                if(added.PredictedNative&&string.Equals(added.Custody.Path,childOriginal,RebirthStationCompletionOriginalScope.PathComparison)&&
                    added.Custody.Original.SequenceEqual(childImage)&&added.Custody.PrimaryCurrent())return true;
                cachedLoadRequests.Remove(added);added.Custody.Dispose();return false;
            }
        }
        catch{return false;}
    }
    internal static bool RegisterPublishedTerminal(string path,string save,byte[] expected,Evidence preserve)
    {
        try
        {
            string suffix=path.EndsWith(".tools-load",StringComparison.Ordinal)?".tools-load":path.EndsWith(".successor-load",StringComparison.Ordinal)?".successor-load":
                path.EndsWith(".terminal-load",StringComparison.Ordinal)?".terminal-load":null;
            if(!ThreadManager.IsMainThread()||suffix==null||expected==null||!Matches(path,expected))return false;
            save=Path.GetFullPath(save);string original=path.Substring(0,path.Length-suffix.Length);
            lock(cachedLoadGate)
            {
                var existing=cachedLoadRequests.Where(request=>string.Equals(request.Custody.Path,path,
                    RebirthStationCompletionOriginalScope.PathComparison)).ToArray();
                if(existing.Length!=0)return existing.Length==1&&existing[0].Custody.Terminal&&existing[0].Custody.Active&&
                    string.Equals(existing[0].Directory,save,RebirthStationCompletionOriginalScope.PathComparison)&&
                    existing[0].Custody.Image.SequenceEqual(expected)&&existing[0].Custody.PrimaryCurrent();
                var previous=cachedLoadRequests.Where(request=>string.Equals(request.Directory,save,
                    RebirthStationCompletionOriginalScope.PathComparison)&&
                    (request.PredictedNative&&string.Equals(request.Custody.Path,original,RebirthStationCompletionOriginalScope.PathComparison)||
                     string.Equals(request.Custody.Path,original+".earned-load",RebirthStationCompletionOriginalScope.PathComparison)||
                     (suffix==".successor-load"||suffix==".tools-load")&&string.Equals(request.Custody.Path,original+".terminal-load",RebirthStationCompletionOriginalScope.PathComparison)||
                     suffix==".successor-load"&&string.Equals(request.Custody.Path,original+".tools-load",RebirthStationCompletionOriginalScope.PathComparison))).ToArray();
                if(previous.Length>1||cachedLoadRequests.Count-previous.Length>=4)return false;
                int count=cachedLoadRequests.Count;ReadCandidate(path,save);
                if(cachedLoadRequests.Count!=count+1)return false;
                var added=cachedLoadRequests[cachedLoadRequests.Count-1];
                if(!added.Custody.Terminal||!added.Custody.Image.SequenceEqual(expected)||!added.Custody.PrimaryCurrent())
                {cachedLoadRequests.Remove(added);added.Custody.Dispose();return false;}
                foreach(var old in previous)
                {
                    cachedLoadRequests.Remove(old);
                    // The active loaded artifact still owns this native source
                    // until its marker cleanup; removing lookup membership must
                    // not revoke its in-progress terminal publication.
                    if(!ReferenceEquals(old.Custody,preserve))old.Custody.Dispose();
                }
                return true;
            }
        }
        catch{return false;}
    }
    // Called only after the owning positive terminal/marker cleanup. Releases
    // lookup memory; never deletes the historical files or reissues load proof.
    internal static void ReleaseRegisteredTerminal(Guid birth,Guid operation)
    {
        lock(cachedLoadGate)
            foreach(var request in cachedLoadRequests.Where(value=>value.Custody.Terminal&&
                value.Custody.Frame.Birth==birth&&value.Custody.Frame.Operation==operation).ToArray())
            {cachedLoadRequests.Remove(request);MarkStartupRefillNeeded();request.Custody.Dispose();}
    }
    internal static bool CanLoadSavedCandidate(Evidence source)
    {
        try
        {
            if(source?.PrimaryCurrent()!=true)return false;
            string basePath=StartupOriginalPath(source.Path);
            if(source.Terminal&&!source.ToolsRebased&&!source.Path.EndsWith(".successor-load",StringComparison.Ordinal)&&
                (File.Exists(basePath+".tools-rebased")||File.Exists(basePath+".tools-rebased.tmp")||File.Exists(basePath+".tools-load")||File.Exists(basePath+".tools-load.tmp")))return false;
            if(source.Load?.PredictedNative==true)
            {
                if(source.Terminal&&(source.Path.EndsWith(".terminal-load",StringComparison.Ordinal)||source.ToolsRebased&&source.Path.EndsWith(".tools-load",StringComparison.Ordinal)))
                {
                    string successor=basePath+".successor-load";
                    return !File.Exists(successor)&&!File.Exists(successor+".tmp");
                }
                return source.Path.EndsWith(".rew",StringComparison.Ordinal)&&
                    !File.Exists(source.Path+".terminal-load")&&!File.Exists(source.Path+".terminal-load.tmp")&&
                    !File.Exists(source.Path+".successor-load")&&!File.Exists(source.Path+".successor-load.tmp");
            }
            if(source.Terminal)
            {
                if(source.Path.EndsWith(".successor-load",StringComparison.Ordinal))return true;
                if(source.ToolsRebased)return !File.Exists(basePath+".successor-load")&&!File.Exists(basePath+".successor-load.tmp");
                if(!source.Path.EndsWith(".terminal-load",StringComparison.Ordinal))return false;
                string successor=source.Path.Substring(0,source.Path.Length-".terminal-load".Length)+".successor-load";
                return !File.Exists(successor)&&!File.Exists(successor+".tmp");
            }
            if(!source.Path.EndsWith(".earned-load",StringComparison.Ordinal))return false;
            string terminal=source.Path.Substring(0,source.Path.Length-".earned-load".Length)+".terminal-load";
            // Retained earned requests may predate terminal publication in this
            // world. A later rollback/load must never resurrect their output.
            return !File.Exists(terminal)&&!File.Exists(terminal+".tmp");
        }
        catch{return false;}
    }
    internal static bool RegisterPublishedEarned(string path,string save,byte[] expected)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||expected==null||!path.EndsWith(".earned-load",StringComparison.Ordinal)||
                !Matches(path,expected))return false;
            save=Path.GetFullPath(save);
            string terminal=path.Substring(0,path.Length-".earned-load".Length)+".terminal-load";
            lock(cachedLoadGate)
            {
                // A terminal or uncertain terminal candidate supersedes earned
                // insertion authority. Do not replace it with an older snapshot.
                if(File.Exists(terminal)||File.Exists(terminal+".tmp"))return false;
                var existing=cachedLoadRequests.Where(request=>string.Equals(request.Custody.Path,path,
                    RebirthStationCompletionOriginalScope.PathComparison)).ToArray();
                if(existing.Length!=0)
                    return existing.Length==1&&existing[0].Custody.Active&&!existing[0].Custody.Terminal&&
                        string.Equals(existing[0].Directory,save,RebirthStationCompletionOriginalScope.PathComparison)&&
                        existing[0].Custody.Image.SequenceEqual(expected)&&existing[0].Custody.PrimaryCurrent();
                string original=path.Substring(0,path.Length-".earned-load".Length);
                var predicted=cachedLoadRequests.Where(request=>request.PredictedNative&&
                    string.Equals(request.Custody.Path,original,RebirthStationCompletionOriginalScope.PathComparison)&&
                    string.Equals(request.Directory,save,RebirthStationCompletionOriginalScope.PathComparison)).ToArray();
                if(predicted.Length>1||cachedLoadRequests.Count-predicted.Length>=4)return false;
                // Use the same bounded canonical saved-record admission as cold
                // startup. No tile lookup, current-row equality or synthetic load.
                ReadCandidate(path,save);
                var admitted=cachedLoadRequests.Where(request=>string.Equals(request.Custody.Path,path,
                    RebirthStationCompletionOriginalScope.PathComparison)).ToArray();
                bool valid=admitted.Length==1&&admitted[0].Custody.Active&&!admitted[0].Custody.Terminal&&
                    admitted[0].Loaded==null&&admitted[0].Custody.Image.SequenceEqual(expected)&&
                    admitted[0].Custody.PrimaryCurrent();
                if(!valid)return false;
                foreach(var prior in predicted)
                {
                    if(prior.Custody.Frame.Birth!=admitted[0].Custody.Frame.Birth||prior.Custody.Frame.Operation!=admitted[0].Custody.Frame.Operation||
                        !prior.Custody.Original.SequenceEqual(admitted[0].Custody.Original))return false;
                    cachedLoadRequests.Remove(prior);prior.Custody.Dispose();
                }
                return true;
            }
        }
        catch{return false;}
    }
}
