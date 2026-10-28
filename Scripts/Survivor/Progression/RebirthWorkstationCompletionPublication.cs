using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Purpose-specific primary publication. Unknown staged evidence is never
// overwritten, adopted, deleted or replaced by a fresh completion candidate.
internal static class RebirthWorkstationCompletionPublication
{
    internal const int MaximumBytes=24*1024*1024;
    private sealed class Candidate
    {
        internal string Path,Temporary,Identity,BeforeHash;internal bool HadPrimary,Created,Flushed;
        internal byte[] Bytes;
    }
    private static readonly Dictionary<string,Candidate> owned=new Dictionary<string,Candidate>(StringComparer.OrdinalIgnoreCase);
    private static long ownedBytes;
    private static readonly object gate=new object();
    private sealed class BoundedStream:MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        {if(count<0||Position>MaximumBytes-count)throw new InvalidDataException("Completion candidate byte bound");base.Write(buffer,offset,count);}
        public override void WriteByte(byte value)
        {if(Position>=MaximumBytes)throw new InvalidDataException("Completion candidate byte bound");base.WriteByte(value);}
        public override void SetLength(long value)
        {if(value<0||value>MaximumBytes)throw new InvalidDataException("Completion candidate byte bound");base.SetLength(value);}
    }
    private static byte[] Bytes(XElement image)
    {
        using(var stream=new BoundedStream())
        {
            using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=false,NewLineHandling=NewLineHandling.Entitize,CloseOutput=false}))
            {new XDocument(new XElement(image)).Save(writer);writer.Flush();}
            if(stream.Length<1||stream.Length>MaximumBytes)throw new InvalidDataException("Completion XML bound");return stream.ToArray();
        }
    }
    // Caller reserves clone/read workspace first. This is an immutable byte
    // identity only; it does not issue a native original from a parsed file.
    internal static byte[] CopyPublishedBytes(string path)
    {
        string full=QualifiedPath(path);
        if(File.Exists(full+".tmp"))throw new InvalidDataException("Completion staged original retained");
        var bytes=ReadBytes(full);
        Parse(bytes); // Validate the exact image returned, without a second file read.
        return bytes;
    }
    // Retained native PDS is binary, unlike the surrounding XML receipts.
    // This supplies bytes only; the purpose-specific saved PDS reader must
    // independently qualify their exact length, digest and owner projection.
    internal static byte[] CopyRetainedNativeBytes(string path)
    {
        string full=QualifiedPath(path);
        if(!full.EndsWith(".native",StringComparison.Ordinal)||File.Exists(full+".tmp"))throw new InvalidDataException("Retained native primary required");
        var bytes=ReadBytes(full);
        if(File.Exists(full+".tmp")||!MatchesPublishedBytes(full,bytes))throw new InvalidDataException("Retained native primary changed");
        return bytes;
    }
    internal static long PublishedLength(string path)
    {
        try
        {
            path=QualifiedPath(path);if(File.Exists(path+".tmp"))return -1;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {long length=stream.Length;RejectReparseChain(path);return length>0&&length<=MaximumBytes?length:-1;}
        }
        catch{return -1;}
    }
    internal static bool MatchesPublishedBytes(string path,byte[] expected)
    {
        try
        {
            if(expected==null||expected.Length<1||expected.Length>MaximumBytes)return false;
            path=QualifiedPath(path);if(File.Exists(path+".tmp"))return false;
            RejectReparseChain(path);
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                if(input.Length!=expected.Length)return false;var buffer=new byte[8192];int offset=0,read;
                while((read=input.Read(buffer,0,buffer.Length))>0){for(int i=0;i<read;i++)if(buffer[i]!=expected[offset+i])return false;offset+=read;}
                if(offset!=expected.Length)return false;
            }
            RejectReparseChain(path);return true;
        }
        catch{return false;}
    }
    private static string Hash(byte[] bytes)
    {using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(bytes));}
    private static byte[] ReadBytes(string path)
    {
        RejectReparseChain(path);
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            if(stream.Length<1||stream.Length>MaximumBytes)throw new InvalidDataException("Completion primary byte bound");
            byte[] result=new byte[(int)stream.Length];int offset=0;
            while(offset<result.Length){int count=stream.Read(result,offset,result.Length-offset);if(count<1)throw new EndOfStreamException();offset+=count;}
            if(stream.ReadByte()!=-1)throw new InvalidDataException("Completion primary changed during read");RejectReparseChain(path);return result;
        }
    }
    private static XmlReaderSettings ReaderSettings()=>new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,
        MaxCharactersInDocument=MaximumBytes,MaxCharactersFromEntities=1,CloseInput=false};
    private static XElement Parse(byte[] bytes)
    {
        using(var stream=new MemoryStream(bytes,false))
        {
            int nodes=0;
            using(var reader=XmlReader.Create(stream,ReaderSettings()))
                while(reader.Read())if(reader.Depth>64||++nodes>262144)throw new InvalidDataException("Completion XML structure bound");
            stream.Position=0;
            using(var reader=XmlReader.Create(stream,ReaderSettings()))
            {var document=XDocument.Load(reader,LoadOptions.None);return document.Root??throw new InvalidDataException("Completion primary root required");}
        }
    }
    private static string QualifiedPath(string path)
    {
        if(!ThreadManager.IsMainThread())throw new InvalidOperationException("Completion main thread required");
        string full=System.IO.Path.GetFullPath(path),root=System.IO.Path.GetFullPath(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","Survivor","WorkstationCompletions"));
        if(!full.StartsWith(root+System.IO.Path.DirectorySeparatorChar,RebirthStationCompletionOriginalScope.PathComparison))throw new InvalidDataException("Actual completion save scope required");
        RejectReparseChain(root);RejectReparseChain(full);RejectReparseChain(full+".tmp");RejectReparseChain(full+".bak");
        return full;
    }
    private static void RejectReparseChain(string path)
    {
        // Inspect each existing file/directory, including the actual save and
        // scope ancestors. Exists() alone misses dangling reparse entries.
        for(string current=System.IO.Path.GetFullPath(path);!string.IsNullOrEmpty(current);current=System.IO.Path.GetDirectoryName(current))
        {
            try{if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Completion reparse path retained/refused");}
            catch(FileNotFoundException){}
            catch(DirectoryNotFoundException){}
        }
    }
    internal static void RequireSafeDirectory(string path)
    {
        string full=System.IO.Path.GetFullPath(path),root=System.IO.Path.GetFullPath(System.IO.Path.Combine(GameIO.GetSaveGameDir(),"RebirthData","Survivor","WorkstationCompletions"));
        if(!string.Equals(full,root,RebirthStationCompletionOriginalScope.PathComparison)&&
            !full.StartsWith(root+System.IO.Path.DirectorySeparatorChar,RebirthStationCompletionOriginalScope.PathComparison))throw new InvalidDataException("Completion directory scope required");
        RejectReparseChain(full);
    }
    internal static void RequireClearDestination(string path)
    {
        string full=QualifiedPath(path);
        if(File.Exists(full+".tmp")||Directory.Exists(full+".tmp")||!File.Exists(full)&&File.Exists(full+".bak"))
            throw new InvalidDataException("Completion archive destination uncertain");
    }
    internal static void RequireNativeSaveFile(string path)
    {
        string full=System.IO.Path.GetFullPath(path),root=System.IO.Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(System.IO.Path.DirectorySeparatorChar,System.IO.Path.AltDirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
        if(!full.StartsWith(root,RebirthStationCompletionOriginalScope.PathComparison))throw new InvalidDataException("Actual native save path required");
        RejectReparseChain(full);
    }
    private static string Identity(XElement image)
    {
        XElement original=image.Name=="workstationCompletionOutbox"?image.Element("original")?.Elements().SingleOrDefault():null;
        string operation=(string)(original??image).Attribute("operation"),world=(string)(original??image).Attribute("world");
        if(!Guid.TryParseExact(operation,"N",out var id)||id==Guid.Empty||string.IsNullOrEmpty(world)||
            world!=RebirthWorkstationCompletionOutbox.CurrentGuid(GameManager.Instance?.World))throw new InvalidDataException("Completion original publication identity required");
        if(original!=null)return image.Name+"|"+operation+"|"+world+"|"+(string)image.Attribute("role")+"|"+RebirthWorkstationCompletionOriginal.Hash(original.ToString(SaveOptions.DisableFormatting));
        if(image.Name!="workstationCompletionIntent"||image.Element("before")?.Elements().Count()!=1)throw new InvalidDataException("Completion publication purpose required");
        return image.Name+"|"+operation+"|"+world+"|"+RebirthWorkstationCompletionOriginal.Hash(image.Element("before").ToString(SaveOptions.DisableFormatting));
    }
    internal static XElement ReadPrimary(string path)
    {
        string full=QualifiedPath(path);
        if(File.Exists(full+".tmp"))throw new InvalidDataException("Completion staged original retained");
        return Parse(ReadBytes(full)); // no backup, fallback, migration or repair
    }
    internal static string ReadEvidence(string path)
    {var bytes=ReadBytes(QualifiedPath(path));Parse(bytes);return new UTF8Encoding(false,true).GetString(bytes);}
    private static bool Publish(Candidate candidate)
    {
        QualifiedPath(candidate.Path);
        if(!candidate.Created)return false; // failed/partial create remains unknown even to this process
        if(!File.Exists(candidate.Temporary))
        {
            // Atomic rename succeeded but its final readback failed. Only the
            // exact retained candidate primary can complete that same retry.
            if(!File.Exists(candidate.Path)||!ReadBytes(candidate.Path).SequenceEqual(candidate.Bytes))return false;
            Parse(candidate.Bytes);return true;
        }
        var staged=ReadBytes(candidate.Temporary);
        if(!staged.SequenceEqual(candidate.Bytes)||Identity(Parse(staged))!=candidate.Identity)return false;
        if(!candidate.Flushed)
        {
            // Only a successfully CreateNew-owned, exact full candidate can
            // retry a failed fsync. No missing/partial/foreign bytes are filled.
            using(var stream=new FileStream(candidate.Temporary,FileMode.Open,FileAccess.ReadWrite,FileShare.None))stream.Flush(true);
            if(!ReadBytes(candidate.Temporary).SequenceEqual(candidate.Bytes))return false;candidate.Flushed=true;
        }
        if(File.Exists(candidate.Path)!=candidate.HadPrimary)return false;
        if(candidate.HadPrimary&&Hash(ReadBytes(candidate.Path))!=candidate.BeforeHash)return false;
        if(candidate.HadPrimary)File.Replace(candidate.Temporary,candidate.Path,null);
        else File.Move(candidate.Temporary,candidate.Path);
        return ReadBytes(candidate.Path).SequenceEqual(candidate.Bytes)&&XNode.DeepEquals(Parse(candidate.Bytes),Parse(ReadBytes(candidate.Path)));
    }
    internal static bool Write(string path,XElement image)
    {
        lock(gate)
        {
            try
            {
                string full=QualifiedPath(path),identity=Identity(image);byte[] bytes=Bytes(image);
                if(!XNode.DeepEquals(image,Parse(bytes)))return false;
                if(owned.TryGetValue(full,out var previous))
                {
                    if(previous.Identity!=identity||!Publish(previous))return false;
                    owned.Remove(full);ownedBytes-=previous.Bytes.Length;
                    if(previous.Bytes.SequenceEqual(bytes))return true;
                    // A later monotone in-memory stage may have arrived while
                    // the exact earlier owned candidate was awaiting readback.
                    // Publish that original first; never overwrite its staging.
                }
                if(File.Exists(full+".tmp"))return false;
                bool exists=File.Exists(full);byte[] before=exists?ReadBytes(full):null;
                if(before!=null)
                {
                    if(Identity(Parse(before))!=identity)return false;
                    if(before.SequenceEqual(bytes))return true;
                }
                else if(File.Exists(full+".bak"))return false;
                if(owned.Count>=4096||ownedBytes>64L*1024*1024-bytes.Length)return false;
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
                QualifiedPath(full); // created scope must still be a real local chain
                var candidate=new Candidate{Path=full,Temporary=full+".tmp",Identity=identity,Bytes=bytes,
                    HadPrimary=exists,BeforeHash=before==null?null:Hash(before)};
                // Ownership is retained before the first possible byte write;
                // CreateNew can never overwrite foreign staging.
                owned.Add(full,candidate);ownedBytes+=bytes.Length;
                using(var stream=new FileStream(candidate.Temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                {candidate.Created=true;stream.Write(bytes,0,bytes.Length);stream.Flush(true);candidate.Flushed=true;}
                if(!ReadBytes(candidate.Temporary).SequenceEqual(bytes))return false;
                if(!Publish(candidate))return false;owned.Remove(full);ownedBytes-=candidate.Bytes.Length;return true;
            }
            catch{return false;} // exact unknown evidence remains untouched
        }
    }
}
