using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

// Observational CURRENT-save seal only. It grants no effects, releases or
// recovery authority. Validation deliberately never consults current GameIO.
internal static class RebirthRemoteResourceNpcTransitionSeal
{
    internal sealed class Seal
    {
        private readonly World world;private readonly string save,root;
        private readonly long epoch;private readonly string[] heads;
        private readonly string[] originals;
        internal Seal(World capturedWorld,string directory,string path,long generation,string[] files,string[] records)
        {world=capturedWorld;save=directory;root=path;epoch=generation;heads=files;originals=(string[])records.Clone();}
        internal bool IsUnchangedOriginal(World originalWorld,string originalSave,out string error)
        {
            error="remote-resource custody changed or unreadable";
            try
            {
                if(!ThreadManager.IsMainThread()||!RebirthRemoteResourceDurableJournal.TransitionQuiescent||!ReferenceEquals(world,originalWorld)||
                    Path.GetFullPath(originalSave)!=save||epoch!=RebirthRemoteResourceDurableJournal.MutationEpoch||
                    !TryHeads(root,out var current)||!heads.SequenceEqual(current,StringComparer.Ordinal)||
                    epoch!=RebirthRemoteResourceDurableJournal.MutationEpoch||!RebirthRemoteResourceDurableJournal.TransitionQuiescent)return false;
                // Compact immutable original identities were semantically read
                // on both sides of capture, never rebound to the current save.
                if(originals.Any(record=>string.IsNullOrEmpty(record)))return false;
                error=null;return true;
            }
            catch{return false;}
        }
    }
    internal static bool TryCaptureNpcTransitionSeal(World world,out Seal seal)
    {
        seal=null;
        try
        {
            if(!ThreadManager.IsMainThread()||!RebirthRemoteResourceDurableJournal.TransitionQuiescent||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world))return false;
            string save=Path.GetFullPath(GameIO.GetSaveGameDir());long epoch=RebirthRemoteResourceDurableJournal.MutationEpoch;
            if(!RebirthRemoteResourceDurableJournal.TryReadPending(world,out var pending))return false;
            var identities=OriginalHeads(pending);
            string root=Path.Combine(save,"RebirthData","RemoteResources","Outcomes");
            if(!TryHeads(root,out var heads)||!TryHeads(root,out var confirmed)||!heads.SequenceEqual(confirmed,StringComparer.Ordinal)||epoch!=RebirthRemoteResourceDurableJournal.MutationEpoch||
                !RebirthRemoteResourceDurableJournal.TransitionQuiescent||!ReferenceEquals(GameManager.Instance?.World,world)||Path.GetFullPath(GameIO.GetSaveGameDir())!=save)return false;
            if(!RebirthRemoteResourceDurableJournal.TryReadPending(world,out var again)||!identities.SequenceEqual(OriginalHeads(again),StringComparer.Ordinal)||
                epoch!=RebirthRemoteResourceDurableJournal.MutationEpoch||!RebirthRemoteResourceDurableJournal.TransitionQuiescent)return false;
            seal=new Seal(world,save,root,epoch,heads,identities);return true;
        }
        catch{return false;}
    }
    private static string[] OriginalHeads(RebirthRemoteResourceDurableJournal.Record[] records)
    {
        if(records==null||records.Length>4096)throw new InvalidDataException();
        return records.Select(record=>record.Owner+"|"+record.Operation.ToString("N")+"|"+record.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture)+"|"+record.Binding+"|"+
            StateDigest(record)).OrderBy(value=>value,StringComparer.Ordinal).ToArray();
    }
    private static string StateDigest(RebirthRemoteResourceDurableJournal.Record record)
    {
        using(var hash=SHA256.Create())using(var sink=new CryptoStream(Stream.Null,hash,CryptoStreamMode.Write))
        {
            using(var writer=System.Xml.XmlWriter.Create(sink,new System.Xml.XmlWriterSettings{Encoding=new System.Text.UTF8Encoding(false,true),
                OmitXmlDeclaration=true,Indent=false,NewLineHandling=System.Xml.NewLineHandling.None,CloseOutput=false}))
            {record.WriteDetachedTo(writer);writer.Flush();}
            sink.FlushFinalBlock();return BitConverter.ToString(hash.Hash);
        }
    }
    private static bool TryHeads(string root,out string[] heads)
    {
        heads=null;
        try
        {
            // Check all existing ancestors on every observation, including
            // after switching worlds: a substituted junction is not the saved A.
            for(var ancestor=new DirectoryInfo(root);ancestor!=null;ancestor=ancestor.Parent)
                if(ancestor.Exists&&(ancestor.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            var result=new List<string>();var queue=new Queue<string>();queue.Enqueue(root);long bytes=0;int entries=1;
            while(queue.Count!=0)
            {
                string path=queue.Dequeue();
                if(!Directory.Exists(path)){if(path!=root)return false;result.Add("absent:"+path);continue;}
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)return false;result.Add("directory:"+path);
                foreach(string child in Directory.EnumerateFileSystemEntries(path))
                {
                    if(++entries>32768)return false;
                    if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)return false;
                    if(Directory.Exists(child)){queue.Enqueue(child);continue;}
                    using(var stream=new FileStream(child,FileMode.Open,FileAccess.Read,FileShare.Read))
                    {
                        long length=stream.Length;if(length<1||length>128L*1024*1024||(bytes=checked(bytes+length))>512L*1024*1024)return false;
                        using(var hash=SHA256.Create())result.Add("file:"+child+":"+length+":"+BitConverter.ToString(hash.ComputeHash(stream)));
                        if(stream.Length!=length||stream.Position!=length)return false;
                    }
                }
            }
            heads=result.OrderBy(value=>value,StringComparer.Ordinal).ToArray();return true;
        }
        catch{return false;}
    }
}
