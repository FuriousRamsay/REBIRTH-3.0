using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using System.Globalization;

// Immutable artifact publication only. No phase, lease, permission, payment or
// terminal outcome is inferred from the existence of this file.
internal static class RebirthRemoteResourcePlacedStationArtifactStore
{
    internal sealed class Header
    {
        internal readonly Guid Source;internal readonly string Birth;internal readonly long Bytes,SourceBytes,Revision,SourceGraph,SourceWorkspace;
        internal Header(Guid source,string birth,long bytes,long sourceBytes,long revision,long graph,long workspace)
        {Source=source;Birth=birth;Bytes=bytes;SourceBytes=sourceBytes;Revision=revision;SourceGraph=graph;SourceWorkspace=workspace;}
    }
    private static void CheckAncestors(string path)
    {
        for(var current=new DirectoryInfo(Path.GetFullPath(path));current!=null;current=current.Parent)
            if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
    }
    private static string DirectoryPath()=>Path.Combine(GameIO.GetSaveGameDir(),"REBIRTH","NativePlacedFetchedPlans");
    private static bool Scope(RebirthRemoteResourceDurableJournal.Record root)=>ThreadManager.IsMainThread()&&root!=null&&root.Operation!=Guid.Empty&&
        GameManager.Instance?.World?.IsRemote()==false&&RemoteResourceWorldIdentity.TryGet(GameManager.Instance.World,out string world)&&world==root.World;
    private static string ArtifactPath(RebirthRemoteResourceDurableJournal.Record root,string ownTemporary=null)=>ArtifactPath(root.Operation,ownTemporary);
    private static string ArtifactPath(Guid operation,string ownTemporary=null)
    {
        if(operation==Guid.Empty)throw new InvalidDataException();
        string directory=DirectoryPath();CheckAncestors(directory);string name=operation.ToString("N")+".rpa";
        if(Directory.Exists(directory))
        {
            int count=0;
            foreach(string candidate in Directory.EnumerateFileSystemEntries(directory,operation.ToString("N")+"*"))
            {
                if(++count>32||(Path.GetFileName(candidate)!=name&&!string.Equals(candidate,ownTemporary,StringComparison.Ordinal))||Directory.Exists(candidate)||(File.GetAttributes(candidate)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException();
            }
        }
        return Path.Combine(directory,name);
    }
    // Small v2 framing observation BEFORE source history/whole-artifact reads.
    // The header grants a bounded allocation only, never original/effect proof.
    internal static bool TryReadHeader(Guid operation,out Header header)
    {
        header=null;
        try
        {
            if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false)return false;
            string path=ArtifactPath(operation);using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(stream.Length<1||stream.Length>RebirthRemoteResourcePlacedStationArtifact.MaximumBytes)return false;
                // Six fixed root attributes, each <=128 UTF16 characters;
                // reserve worst UTF8 width plus fixed names/delimiters. Read
                // only this prefix so a forged giant attribute cannot make
                // the XML reader allocate the full artifact as a "header".
                const int maximumHeaderBytes=64+6*(64+4*128+4);
                var prefix=new byte[checked((int)Math.Min(stream.Length,maximumHeaderBytes))];int filled=0;
                while(filled<prefix.Length){int read=stream.Read(prefix,filled,prefix.Length-filled);if(read<=0)return false;filled+=read;}
                using(var input=new MemoryStream(prefix,false))
                using(var xml=XmlReader.Create(input,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,
                    MaxCharactersInDocument=maximumHeaderBytes,IgnoreComments=false,IgnoreProcessingInstructions=false,CloseInput=false}))
                {
                    if(!xml.Read()||xml.NodeType!=XmlNodeType.Element||xml.Name!="placedFetchedOriginal"||xml.AttributeCount!=6||xml.GetAttribute("version")!="2")return false;
                    if(xml.MoveToFirstAttribute())do{if(xml.Value.Length>128)return false;}while(xml.MoveToNextAttribute());xml.MoveToElement();
                    bool Number(string name,out long value)=>long.TryParse(xml.GetAttribute(name),NumberStyles.None,CultureInfo.InvariantCulture,out value)&&value>=0&&value.ToString(CultureInfo.InvariantCulture)==xml.GetAttribute(name);
                    string birth=xml.GetAttribute("birth");
                    if(!Guid.TryParseExact(birth,"N",out var token)||token==Guid.Empty||token.ToString("N")!=birth||
                        !Number("sourcePreparedBytes",out long source)||source<1||source>RebirthRemoteResourceJournalFormat.MaximumPlan||
                        !Number("sourceRevision",out long revision)||!Number("sourceGraphBytes",out long graph)||graph<1||
                        !Number("sourceReadWorkspace",out long workspace)||workspace<checked(8*graph+64L*1024*1024)||workspace>RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
                    header=new Header(operation,birth,stream.Length,source,revision,graph,workspace);return true;
                }
            }
        }
        catch{header=null;return false;}
    }
    private static byte[] ReadBytes(string path)
    {
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<1||stream.Length>RebirthRemoteResourcePlacedStationArtifact.MaximumBytes)throw new InvalidDataException();
            var bytes=new byte[checked((int)stream.Length)];int offset=0;
            while(offset<bytes.Length){int read=stream.Read(bytes,offset,bytes.Length-offset);if(read==0)throw new EndOfStreamException();offset+=read;}
            if(stream.ReadByte()!=-1)throw new InvalidDataException();return bytes;
        }
    }
    internal static string Digest(byte[] bytes)
    {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    internal static bool TryRead(RebirthRemoteResourceDurableJournal.Record root,out byte[] bytes,out RebirthRemoteResourcePlacedStationArtifactReader.Image artifact,
        RebirthRemoteResourcePlacedStationAllocation.Admission allocation=null,RebirthRemoteResourcePlacedStationPreparation.ColdRead cold=null)
    {
        bytes=null;artifact=null;RebirthRemoteResourcePlacedStationAllocation.Scratch scratch=null;
        try
        {
            if(!Scope(root)||allocation!=null&&cold!=null)return false;string path=ArtifactPath(root);if(!File.Exists(path))return false;
            if(allocation!=null)
            {if(!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Artifact,out scratch))return false;}
            else if(cold!=null&&!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(cold,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Artifact,out scratch))return false;
            if(cold!=null&&(!cold.Current()||cold.Header.Source!=root.Operation||new FileInfo(path).Length!=cold.Header.Bytes||!cold.ReserveReadWorkspace(3*cold.Header.Bytes)))return false;
            if(allocation!=null&&(allocation.Source!=root.Operation||allocation.Binding!=root.Binding||!allocation.ReserveArtifact(new FileInfo(path).Length)))return false;
            var value=ReadBytes(path);
            if(!RebirthRemoteResourcePlacedStationArtifactReader.TryRead(value,out var parsed,allocation,cold,scratch)||!RebirthRemoteResourcePlacedStationArtifactReader.TryValidateSource(parsed,root))return false;
            bytes=value;artifact=parsed;return true;
        }
        catch{bytes=null;artifact=null;return false;}
        finally{scratch?.Dispose();}
    }
    internal static bool TryPublish(RebirthRemoteResourceDurableJournal.Record root,RebirthRemoteResourceClientOriginal.Intent original,string birth,
        RebirthRemoteResourcePlacedStationPlan.Plan plan,RebirthRemoteResourcePlacedStationBefore.Evidence before,out string digest)
    {
        digest=null;RebirthRemoteResourcePlacedStationAllocation.Scratch scratch=null;
        try
        {
            if(!Scope(root)||plan?.Allocation==null||!RebirthRemoteResourcePlacedStationAllocation.TryBeginScratch(plan.Allocation,RebirthRemoteResourcePlacedStationAllocation.ReadPhase.Artifact,out scratch)||
                !RebirthRemoteResourcePlacedStationArtifact.TryEncode(root,original,birth,plan,before,out var bytes)||
                !RebirthRemoteResourcePlacedStationArtifactReader.TryRead(bytes,out var parsed,plan.Allocation,null,scratch)||!RebirthRemoteResourcePlacedStationArtifactReader.TryValidateSource(parsed,root))return false;
            string path=ArtifactPath(root);string directory=Path.GetDirectoryName(path);Directory.CreateDirectory(directory);CheckAncestors(directory);
            if(File.Exists(path))
            {
                if(!ReadBytes(path).SequenceEqual(bytes))return false;
                digest=Digest(bytes);return true;
            }
            // A failed/interrupted candidate is retained and blocks subsequent
            // publication. Never delete it or substitute a new plan silently.
            string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(!Scope(root)||ArtifactPath(root,temporary)!=path)return false;
            File.Move(temporary,path);
            if(!ReadBytes(path).SequenceEqual(bytes))return false;digest=Digest(bytes);return true;
        }
        catch{digest=null;return false;}
        finally{scratch?.Dispose();}
    }
}
