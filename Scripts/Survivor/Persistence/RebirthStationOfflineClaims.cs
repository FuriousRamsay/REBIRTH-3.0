using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

// Conservative world-wide exclusion, never a claim acquisition or mutation authority.
public static class RebirthStationOfflineClaims
{
    public const int MaximumFiles=4096;
    public const long MaximumFileBytes=8*1024*1024, MaximumTotalBytes=64*1024*1024;
    public static bool Compatible(string candidateOwner,RebirthStationGridAdmission candidate,string otherOwner,RebirthStationGridAdmission other)
    {
        if(candidate==null||other==null||string.IsNullOrEmpty(candidateOwner)||string.IsNullOrEmpty(otherOwner))return false;
        if(!candidate.SharesStation(other)&&candidate.JobId!=other.JobId)return true;
        return candidateOwner==otherOwner&&XNode.DeepEquals(candidate.Write(),other.Write());
    }
    public static bool CheckDocument(XDocument document,string fileOwner,string candidateOwner,RebirthStationGridAdmission candidate)
    {
        var root=document?.Root;
        if(root==null||root.Name!="rebirthWorldCharacter"||(string)root.Attribute("stablePlayerKey")!=fileOwner||
            root.Elements("origin").Count()!=1||root.Elements("progression").Count()!=1)return false;
        var origin=root.Element("origin");
        if(!RebirthStationGridAdmission.TryNormalizeCreation((string)origin.Attribute("creationId"),out var creation)||
            !RebirthStationPreparationPersistence.TryRead(root.Element("progression"),out var records,out var error)||
            !RebirthStationPreparationPersistence.MatchesOwner(records,creation))return false;
        foreach(var other in records.Values)if(!Compatible(candidateOwner,candidate,fileOwner,other))return false;
        return true;
    }
    public static bool CheckDirectory(string root,string candidateOwner,RebirthStationGridAdmission candidate)
    {
        if(string.IsNullOrEmpty(root)||string.IsNullOrEmpty(candidateOwner)||candidate==null)return false;
        try{
            if(!Directory.Exists(root))return true;
            // Missing final plus backup is unresolved custody, not proof of an empty owner.
            int count=0;foreach(var backup in Directory.EnumerateFiles(root,"*.xml.bak"))
            {if(++count>MaximumFiles||!File.Exists(backup.Substring(0,backup.Length-4)))return false;}
            var files=Directory.EnumerateFiles(root,"*.xml").Take(MaximumFiles+1).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            if(files.Length>MaximumFiles)return false;long total=0;
            var lengths=new long[files.Length];var times=new DateTime[files.Length];
            for(int i=0;i<files.Length;i++){
                var info=new FileInfo(files[i]);lengths[i]=info.Length;times[i]=info.LastWriteTimeUtc;
                if(lengths[i]<=0||lengths[i]>MaximumFileBytes||total>MaximumTotalBytes-lengths[i])return false;total+=lengths[i];
                if(!RebirthAtomicXmlFile.TryLoad(files[i],out var doc,out var error)||
                    !CheckDocument(doc,Path.GetFileNameWithoutExtension(files[i]),candidateOwner,candidate))return false;
            }
            // Reject a repository changing during the scan; retry on a later authority tick.
            var after=Directory.EnumerateFiles(root,"*.xml").Take(MaximumFiles+1).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            if(!files.SequenceEqual(after,StringComparer.Ordinal))return false;
            for(int i=0;i<files.Length;i++){var info=new FileInfo(files[i]);if(info.Length!=lengths[i]||info.LastWriteTimeUtc!=times[i])return false;}
            return true;
        }catch{return false;}
    }
}