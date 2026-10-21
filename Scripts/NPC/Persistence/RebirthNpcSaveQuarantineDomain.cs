using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml;
using System.Text;

// A typed adapter supplies these closures over its own PRIVATE dictionaries,
// original native handles and binding fields. There is no reflection, deserialize
// to live state, native action, or inferred payment authority in this container.
internal sealed class RebirthNpcSaveQuarantineDomain
{
    internal readonly string Name;
    private readonly XmlDocument evidence;
    private readonly Func<bool> current;
    private readonly Action detach,restore;
    private bool detached;
    internal RebirthNpcSaveQuarantineDomain(string name,XmlDocument document,Func<bool> current,Action detach,Action restore)
    {
        if(string.IsNullOrEmpty(name)||document?.DocumentElement==null||current==null||detach==null||restore==null)throw new ArgumentException("Complete typed quarantine adapter required");
        // Refuse before cloning an unexpectedly large tree. This is a retention
        // bound, never truncation or permission to discard the original state.
        EvidenceBytes=MeasureEvidence(document);
        captureBudget?.Reserve(EvidenceBytes);
        Name=name;evidence=(XmlDocument)document.CloneNode(true);this.current=current;this.detach=detach;this.restore=restore;
    }
    internal readonly long EvidenceBytes;
    internal const long MaximumDomainBytes=32L*1024*1024;
    [ThreadStatic] private static EvidenceBudget captureBudget;
    internal sealed class EvidenceBudget:IDisposable
    {
        private readonly long maximum;private long used;private bool disposed;
        internal EvidenceBudget(long maximum)
        {if(maximum<1||captureBudget!=null)throw new InvalidOperationException("One bounded NPC evidence capture required");this.maximum=maximum;captureBudget=this;}
        internal void Reserve(long bytes)
        {if(disposed||bytes<0||bytes>maximum-used)throw new InvalidDataException("NPC whole evidence capture exceeds retention bound");used+=bytes;}
        public void Dispose(){if(disposed)return;disposed=true;if(ReferenceEquals(captureBudget,this))captureBudget=null;}
    }
    private static long MeasureEvidence(XmlDocument document)
    {
        using(var sink=new EvidenceCounter())
        {
            var settings=new XmlWriterSettings{Encoding=new UTF8Encoding(false),CloseOutput=false,Indent=false};
            using(var writer=XmlWriter.Create(sink,settings))document.WriteTo(writer);
            return sink.Length;
        }
    }
    private sealed class EvidenceCounter:Stream
    {
        private long length;
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>length;public override long Position{get=>length;set=>throw new NotSupportedException();}
        public override void Flush(){}
        public override void Write(byte[] buffer,int offset,int count)
        {if(buffer==null||offset<0||count<0||offset>buffer.Length-count)throw new ArgumentOutOfRangeException();if(count>MaximumDomainBytes-length)throw new InvalidDataException("NPC quarantine domain exceeds retained evidence bound");length+=count;}
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
    }
    internal bool IsCurrent=>!detached&&current();
    internal bool IsDetached=>detached;
    internal XmlElement CopyEvidence(XmlDocument owner)
    {
        var element=owner.CreateElement("domain");element.SetAttribute("id",Name);
        element.AppendChild(owner.ImportNode(evidence.DocumentElement,true));return element;
    }
    internal void Detach()
    {
        if(!IsCurrent)throw new InvalidOperationException("Quarantine domain changed before detach: "+Name);
        // Set first: rollback must restore even a partially executed assignment
        // closure if an unexpected synchronous exception occurs.
        detached=true;detach();
    }
    internal void Rollback()
    {
        if(!detached)return;
        restore();detached=false;
    }
}

// This is an original-scope read witness only. It can neither load a backup nor
// authorize importing its retained DTO into another native world incarnation.
internal sealed class RebirthNpcSaveQuarantineScope
{
    internal readonly World World;
    internal readonly string Directory,Fingerprint;
    internal readonly ulong Generation;
    private readonly string generationHead;
    private static StringComparison DirectoryComparison=>Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
    internal static string ScopeDirectory(string directory)
    {
        string full=Path.GetFullPath(directory),root=Path.GetPathRoot(full);
        return string.Equals(full,root,DirectoryComparison)?full:full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
    }
    internal static bool SameDirectory(string first,string second)
    {
        try{return !string.IsNullOrWhiteSpace(first)&&!string.IsNullOrWhiteSpace(second)&&Path.IsPathRooted(first)&&Path.IsPathRooted(second)&&
            string.Equals(ScopeDirectory(first),ScopeDirectory(second),DirectoryComparison);}
        catch{return false;}
    }
    private RebirthNpcSaveQuarantineScope(World world,string directory,string fingerprint,ulong generation)
    {World=world;Directory=ScopeDirectory(directory);Fingerprint=fingerprint;Generation=generation;generationHead=GenerationHead(Directory);}
    internal static RebirthNpcSaveQuarantineScope CaptureCurrent()
    {
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||SingletonMonoBehaviour<ConnectionManager>.Instance?.IsServer!=true)throw new InvalidOperationException("Authoritative original scope capture required");
        var scope=RebirthNpcSaveScope.ObserveCurrent();if(scope==null)throw new InvalidDataException("Original NPC scope unavailable");
        return new RebirthNpcSaveQuarantineScope(GameManager.Instance.World,scope.SaveDirectory,scope.Fingerprint,RebirthNpcRecoveryGeneration.RequireCurrent());
    }
    internal bool MatchesBinding(World world,string file,bool hasState)
    {
        if(world==null)return !hasState&&string.IsNullOrEmpty(file);
        return ReferenceEquals(world,World)&&!string.IsNullOrEmpty(file)&&
            string.Equals(ScopeDirectory(Path.GetDirectoryName(Path.GetFullPath(file))),Directory,DirectoryComparison);
    }
    internal bool IsUnchangedOriginal()
    {
        try{return Fingerprint==RebirthNpcSaveScope.ComputeFingerprint(Directory)&&generationHead==GenerationHead(Directory);}catch{return false;}
    }
    internal bool IsExactCurrentWorld=>ReferenceEquals(World,GameManager.Instance?.World)&&string.Equals(Directory,ScopeDirectory(GameIO.GetSaveGameDir()),DirectoryComparison)&&IsUnchangedOriginal();
    private static string GenerationHead(string directory)
    {
        string path=Path.Combine(directory,"RebirthNpcRecoveryGeneration.xml");
        if(!File.Exists(path))
        {
            if(System.IO.Directory.Exists(directory))foreach(var ignored in System.IO.Directory.EnumerateFiles(directory,"RebirthNpcRecoveryGeneration.xml.*"))throw new InvalidDataException("Original generation evidence unresolved");
            return "legacy-zero";
        }
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(stream.Length<1||stream.Length>4096)throw new InvalidDataException("Original generation size bound");using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-",string.Empty);}
    }
}
