using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

// A positive CURRENT-A observation only. It is neither a detached bundle nor
// permission to clear any domain. Missed/changed capture retains every old cache.
internal static class RebirthNpcSaveQuarantineCapture
{
    internal sealed class Snapshot
    {
        internal readonly RebirthNpcSaveQuarantineScope Scope;
        internal readonly RebirthNpcOriginalEffectAdmission.TransitionSeal Custody;
        internal readonly RebirthNpcStableId[] Ids;
        internal readonly uint NetworkEpoch;
        private readonly string[] heads;
        internal Snapshot(RebirthNpcSaveQuarantineScope scope,RebirthNpcOriginalEffectAdmission.TransitionSeal seal,RebirthNpcStableId[] ids,string[] observed,uint networkEpoch)
        {Scope=scope;Custody=seal;Ids=(RebirthNpcStableId[])ids.Clone();heads=(string[])observed.Clone();NetworkEpoch=networkEpoch;}
        internal bool IsUnchangedOriginal(out string error)
        {
            error="Original NPC transition observation changed";
            try
            {
                if(!ThreadManager.IsMainThread()||!Scope.IsUnchangedOriginal()||!RebirthNpcNetworkEpoch.MatchesOriginalObservation(Scope.World,NetworkEpoch)||!Same(Ids,RebirthNpcSaveQuarantineIds.Collect())||!Same(heads,ReadHeads(Scope.Directory)))return false;
                return Custody.IsUnchangedOriginal(Scope.World,Scope.Directory,Scope.Fingerprint,Scope.Generation,out error);
            }
            catch{return false;}
        }
        internal System.Xml.XmlElement CopyEvidence(System.Xml.XmlDocument owner)
        {
            var root=owner.CreateElement("originalCapture");root.SetAttribute("networkEpoch",NetworkEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture));
            root.SetAttribute("custodyPending",Custody.PendingOriginalCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach(var id in Ids){var row=owner.CreateElement("stable");row.InnerText=id.ToString();root.AppendChild(row);}
            foreach(string head in heads){var row=owner.CreateElement("backingHead");row.InnerText=head;root.AppendChild(row);}
            return root;
        }
    }
    private static readonly List<Snapshot> retained=new List<Snapshot>();
    internal static bool RememberCurrent(out string error)
    {
        error="Original transition observation unavailable";
        try
        {
            var scope=RebirthNpcSaveQuarantineScope.CaptureCurrent();var ids=RebirthNpcSaveQuarantineIds.Collect();var heads=ReadHeads(scope.Directory);
            if(!RebirthNpcOriginalEffectAdmission.TryCaptureTransitionSeal(scope.World,ids,scope.Fingerprint,scope.Generation,out var seal,out error))return false;
            if(!RebirthNpcNetworkEpoch.TryObserveCurrentOriginal(scope.World,out uint networkEpoch))return false;
            var next=new Snapshot(scope,seal,ids,heads,networkEpoch);if(!next.IsUnchangedOriginal(out error)||!scope.IsExactCurrentWorld)return false;
            int index=retained.FindIndex(value=>ReferenceEquals(value.Scope.World,scope.World)&&RebirthNpcSaveQuarantineScope.SameDirectory(value.Scope.Directory,scope.Directory));
            if(index>=0)retained[index]=next;else{if(retained.Count>=16){error="Original transition observation capacity retained";return false;}retained.Add(next);}error=string.Empty;return true;
        }
        catch(Exception exception){error="Original transition observation refused: "+exception.GetType().Name;return false;}
    }
    internal static Snapshot Find(World originalWorld,string originalDirectory)
    {if(!ThreadManager.IsMainThread())return null;foreach(var value in retained)if(ReferenceEquals(value.Scope.World,originalWorld)&&RebirthNpcSaveQuarantineScope.SameDirectory(value.Scope.Directory,originalDirectory))return value;return null;}
    private static bool Same<T>(T[] a,T[] b)
    {if(a==null||b==null||a.Length!=b.Length)return false;var compare=EqualityComparer<T>.Default;for(int i=0;i<a.Length;i++)if(!compare.Equals(a[i],b[i]))return false;return true;}
    private static string[] ReadHeads(string directory)
    {
        if(string.IsNullOrWhiteSpace(directory)||!Path.IsPathRooted(directory))throw new InvalidDataException("Original NPC directory unavailable");
        // Rooted Windows paths can still depend on the process's current drive
        // or per-drive directory (\\save and C:save). Never resolve that ambient
        // state into the identity of a retained original backing store.
        string pathRoot=Path.GetPathRoot(directory);
        string normalizedRoot=pathRoot?.Replace(Path.AltDirectorySeparatorChar,Path.DirectorySeparatorChar);
        bool qualified=!string.IsNullOrEmpty(normalizedRoot);
        if(qualified&&Path.DirectorySeparatorChar=='\\')
            qualified=normalizedRoot.Length==3&&char.IsLetter(normalizedRoot[0])&&normalizedRoot[1]==':'&&normalizedRoot[2]=='\\'||
                normalizedRoot.StartsWith("\\\\",StringComparison.Ordinal)&&normalizedRoot.Substring(2).Split(new[]{'\\'},StringSplitOptions.RemoveEmptyEntries).Length>=2;
        if(!qualified)
            throw new InvalidDataException("Original NPC directory is not fully qualified");
        directory=Path.GetFullPath(directory);RequireOriginalDirectory(directory);
        const long maximum=512L*1024*1024;long bytes=0;var names=new List<string>();
        foreach(string path in Directory.EnumerateFiles(directory,"*",SearchOption.TopDirectoryOnly))
        {
            string name=Path.GetFileName(path);
            if(!name.StartsWith("RebirthNpc",StringComparison.OrdinalIgnoreCase)&&!name.StartsWith("rebirth_npc_",StringComparison.OrdinalIgnoreCase))continue;
            // New immutable retention evidence is not an original backing store.
            if(name.StartsWith("RebirthNpcQuarantineBundle.",StringComparison.Ordinal))continue;
            if(names.Count>=4096)throw new InvalidDataException("NPC original head membership bound");names.Add(name);
        }
        foreach(string pending in Directory.EnumerateDirectories(directory,".rebirth-npc-presave-*",SearchOption.TopDirectoryOnly))throw new InvalidDataException("Unresolved coordinated pre-save evidence retains original domain");
        names.Sort(StringComparer.Ordinal);var result=new string[names.Count];
        for(int index=0;index<names.Count;index++)
        {
            RequireOriginalDirectory(directory);
            string path=Path.Combine(directory,names[index]);
            if((File.GetAttributes(path)&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)throw new InvalidDataException("NPC original head redirected");
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                long length=stream.Length;
                if(length<0||length>maximum-bytes)throw new InvalidDataException("NPC original head byte bound");
                bytes+=length;using(var sha=SHA256.Create())result[index]=names[index]+"|"+length+"|"+BitConverter.ToString(sha.ComputeHash(stream));
                if(stream.Length!=length||stream.Position!=length||(File.GetAttributes(path)&(FileAttributes.ReparsePoint|FileAttributes.Directory))!=0)
                    throw new InvalidDataException("NPC original head changed during capture");
            }
        }
        RequireOriginalDirectory(directory);
        return result;
    }
    private static void RequireOriginalDirectory(string directory)
    {
        var root=new DirectoryInfo(directory);if(!root.Exists)throw new DirectoryNotFoundException();
        for(var parent=root;parent!=null;parent=parent.Parent)
            if(parent.Exists&&(parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Original NPC directory redirected");
    }
}
