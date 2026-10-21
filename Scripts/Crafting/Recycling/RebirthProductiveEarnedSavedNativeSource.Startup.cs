using System;
using System.IO;
using System.Linq;

internal static partial class RebirthProductiveEarnedSavedNativeSource
{
    private static readonly string[] startupPublicationSuffixes={".successor-load",".tools-load",".terminal-load",".earned-load"};
    private static void ScanStartupCandidates(string save,string root,int priorities=3)
    {
        // Streaming fixed-header passes retain no unbounded path collection.
        // Historical complete terminals must not fill the four live custody
        // slots before pending obligations and initial originals are examined.
        for(int priority=0;priority<priorities;priority++)
        {
            foreach(string folder in Directory.EnumerateDirectories(root))
            {
                if(!RebirthProductiveEarnedArtifact.SafePath(folder))continue;
                foreach(string earned in Directory.EnumerateFiles(folder,"*.earned-load"))
                {
                    if(cachedLoadRequests.Count>=4)return;
                    string path=SelectStartupPublication(earned);
                    if(path!=null&&!StartupCandidateRegistered(path)&&StartupPriority(path)==priority)ReadCandidate(path,save,true);
                }
                if(priority!=1)continue;
                foreach(string location in Directory.EnumerateFiles(folder,"*.native-location"))
                {
                    if(cachedLoadRequests.Count>=4)return;
                    string original=location.Substring(0,location.Length-".native-location".Length);
                    bool published=false;
                    foreach(string suffix in startupPublicationSuffixes)
                        if(File.Exists(original+suffix)||File.Exists(original+suffix+".tmp")){published=true;break;}
                    if(!published&&!StartupCandidateRegistered(original))RegisterPreparedOriginal(original,save);
                }
            }
        }
    }
    private static bool StartupCandidateRegistered(string path)
        =>cachedLoadRequests.Any(request=>string.Equals(request.StartupOrigin,path,RebirthStationCompletionOriginalScope.PathComparison)||
            string.Equals(StartupOriginalPath(request.Custody.Path),StartupOriginalPath(path),RebirthStationCompletionOriginalScope.PathComparison));
    private static string StartupOriginalPath(string path)
    {
        if(path==null)return null;
        foreach(string suffix in startupPublicationSuffixes)
            if(path.EndsWith(suffix,StringComparison.Ordinal))return path.Substring(0,path.Length-suffix.Length);
        return path;
    }
    private static string SelectStartupPublication(string earned)
    {
        if(!RebirthProductiveEarnedArtifact.SafePath(earned))return null;
        string original=earned.Substring(0,earned.Length-".earned-load".Length);
        if((File.Exists(original+".tools-rebased")||File.Exists(original+".tools-rebased.tmp"))&&
            !File.Exists(original+".tools-load")&&!File.Exists(original+".successor-load"))return null;
        foreach(string suffix in startupPublicationSuffixes)
        {
            string path=original+suffix;
            // Unknown newer custody cannot fall back to an older earned row.
            if(File.Exists(path+".tmp"))return null;
            if(File.Exists(path))return path;
        }
        return null;
    }
    private static int StartupPriority(string path)
    {
        try
        {
            if(!RebirthProductiveEarnedArtifact.SafePath(path)||File.Exists(path+".tmp"))return -1;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))using(var reader=new BinaryReader(stream))
            {
                if(stream.Length<128||stream.Length>MaximumRecord||reader.ReadUInt32()!=0x324C4552u)return -1;
                int terminal=reader.ReadByte(),pending=reader.ReadByte();
                if(terminal>1||pending>1||pending==1&&terminal!=1)return -1;
                return pending==1?0:terminal==0?1:2;
            }
        }
        catch{return -1;}
        // Header classification is only ordering. ReadCandidate still performs
        // the full canonical decode, dependency/custody checks and actual native
        // load qualification. This method never issues an original or Loaded.
    }
}
