using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

internal static partial class RebirthRepairOriginalCorrection
{
    private const int MaximumJournalBytes=MaximumImage*12;
    private sealed class OwnedCandidate {internal string Path,ImageHash,ExpectedHash;}
    private static readonly Dictionary<string,OwnedCandidate> ownedCandidates=new Dictionary<string,OwnedCandidate>(StringComparer.OrdinalIgnoreCase);
    private static bool RepairPath(string path)
    {
        try
        {
            string root=Path.GetFullPath(Path.Combine(GameIO.GetSaveGameDir(),"rebirth","repair-original"));
            string full=Path.GetFullPath(path);
            if(!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))return false;
            for(string current=full;current!=null;current=Path.GetDirectoryName(current))
            {
                try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)return false;}
                catch(FileNotFoundException){}catch(DirectoryNotFoundException){}
            }
            return true;
        }
        catch{return false;}
    }
    private static string ReadRepairText(string path)
    {
        if(!RepairPath(path))throw new InvalidDataException();
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            long length=stream.Length;if(length<1||length>MaximumJournalBytes)throw new InvalidDataException();
            var bytes=new byte[checked((int)length)];int offset=0;
            while(offset<bytes.Length){int count=stream.Read(bytes,offset,bytes.Length-offset);if(count<=0)throw new EndOfStreamException();offset+=count;}
            if(stream.Position!=stream.Length||stream.ReadByte()!=-1||!RepairPath(path))throw new InvalidDataException();
            return new UTF8Encoding(false,true).GetString(bytes);
        }
    }
    private static string JournalHash(string image)=>image==null?"absent":RebirthRemoteResourceDurableJournal.Digest(image);
    private static void ReleaseCommittedRepairCandidate(string path,string image)
    {
        string full=Path.GetFullPath(path);
        if(ownedCandidates.TryGetValue(full,out var candidate)&&candidate.ImageHash==JournalHash(image)&&
            !File.Exists(candidate.Path)&&RepairPath(full)&&ReadRepairText(full)==image)ownedCandidates.Remove(full);
    }
    private static bool PublishRepairCandidate(string path,string image,string expected)
    {
        if(!RepairPath(path)||!RepairPath(path+".bak")||Encoding.UTF8.GetByteCount(image)>MaximumJournalBytes)return false;
        string full=Path.GetFullPath(path),hash=JournalHash(image),prior=JournalHash(expected);
        if(expected==null?File.Exists(full):!File.Exists(full)||ReadRepairText(full)!=expected)return false;
        Directory.CreateDirectory(Path.GetDirectoryName(full));if(!RepairPath(full))return false;
        if(!ownedCandidates.TryGetValue(full,out var candidate))
        {
            if(ownedCandidates.Count>=32||Directory.EnumerateFiles(Path.GetDirectoryName(full),Path.GetFileName(full)+"*.tmp").Any())return false;
            candidate=new OwnedCandidate{Path=full+"."+Guid.NewGuid().ToString("N")+".tmp",ImageHash=hash,ExpectedHash=prior};
            ownedCandidates.Add(full,candidate);
            // Ownership is installed before creation; a thrown/partial write is
            // retained. Only exact same-process full readback can resume commit.
            var bytes=new UTF8Encoding(false,true).GetBytes(image);
            using(var stream=new FileStream(candidate.Path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
        }
        if(candidate.ImageHash!=hash||candidate.ExpectedHash!=prior||!RepairPath(candidate.Path)||ReadRepairText(candidate.Path)!=image||Encode(Decode(image))!=image)return false;
        if(expected==null?File.Exists(full):!File.Exists(full)||ReadRepairText(full)!=expected)return false;
        if(!RepairPath(full+".bak")||!RebirthDurableFileCommit.TryPublish(candidate.Path,full,out _)||!RepairPath(full)||ReadRepairText(full)!=image)return false;
        ownedCandidates.Remove(full);return true;
    }
}
