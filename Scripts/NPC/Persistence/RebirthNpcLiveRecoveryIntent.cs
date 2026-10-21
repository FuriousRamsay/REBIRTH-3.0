using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;

// Durable per-store member of a coordinated recovery. File names are fixed by
// typed adapters; no persisted arbitrary destination is followed. Phase2 never
// authorizes reinstalling Candidate over a newer installed publication receipt.
internal sealed class RebirthNpcLiveRecoveryIntent
{
    private const long MaximumDocument=67108864;
    internal string File,Scope,BeforeHash,CandidateHash,TerminalHash;
    internal Guid Operation;
    internal ulong PreviousGeneration,Generation,TerminalSequence;
    internal byte Phase;
    internal string Directory;
    internal string Primary=>Path.Combine(Directory,File);
    internal string Intent=>RebirthNpcPersistenceFile.RecoveryIntentPath(Primary);
    internal string Candidate=>Primary+".recovery."+Operation.ToString("N")+".candidate.xml";
    internal string Prior=>Primary+".recovery."+Operation.ToString("N")+".prior.xml";
    private static bool Digest(string value)=>value!=null&&value.Length==64&&System.Text.RegularExpressions.Regex.IsMatch(value,"\\A[0-9a-f]{64}\\z");
    internal static string Hash(string path)
    {
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        {if(stream.Length<=0||stream.Length>MaximumDocument)throw new InvalidDataException("NPC recovery original size invalid");using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    }
    private void RequireLease()
    {
        var scope=RebirthNpcSaveScope.ObserveCurrent();
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||scope==null||scope.Fingerprint!=Scope||
            !string.Equals(Path.GetFullPath(scope.SaveDirectory),Path.GetFullPath(Directory),StringComparison.OrdinalIgnoreCase)||!RebirthNpcPersistenceFile.HasRecoveryLease(Primary,Operation))
            throw new InvalidOperationException("NPC recovery intent lost original world/save/writer fence");
    }
    private XElement WriteRoot()=>new XElement("npcLiveRecovery",new XAttribute("version",1),new XAttribute("file",File),new XAttribute("scope",Scope),
        new XAttribute("operation",Operation.ToString("N")),new XAttribute("previousGeneration",PreviousGeneration.ToString(CultureInfo.InvariantCulture)),
        new XAttribute("generation",Generation.ToString(CultureInfo.InvariantCulture)),new XAttribute("phase",Phase),
        new XAttribute("beforeHash",BeforeHash),new XAttribute("candidateHash",CandidateHash),
        new XAttribute("terminalHash",TerminalHash??string.Empty),new XAttribute("terminalSequence",TerminalSequence.ToString(CultureInfo.InvariantCulture)));
    private void Save()
    {
        RequireLease();string staged=Intent+".write."+Guid.NewGuid().ToString("N");
        using(var stream=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=true,CloseOutput=false}))
        {new XDocument(WriteRoot()).Save(writer);writer.Flush();stream.Flush(true);}
        if(!RebirthDurableFileCommit.TryPublish(staged,Intent,out var error))throw new IOException("NPC recovery intent publication failed: "+error);
        var actual=Read(Directory,File);
        if(!XNode.DeepEquals(actual.WriteRoot(),WriteRoot()))throw new InvalidDataException("NPC recovery intent readback differs");
    }
    private void Reload()
    {
        RequireLease();var saved=Read(Directory,File);
        if(saved.Operation!=Operation||saved.Scope!=Scope||saved.PreviousGeneration!=PreviousGeneration||saved.Generation!=Generation||saved.BeforeHash!=BeforeHash||saved.CandidateHash!=CandidateHash)
            throw new InvalidDataException("NPC recovery immutable intent changed");
        Phase=saved.Phase;TerminalHash=saved.TerminalHash;TerminalSequence=saved.TerminalSequence;
    }
    private void RequireGroup(byte phase)
    {
        var group=RebirthNpcLiveRecoveryGroup.Read(Directory);
        if(group==null||group.Operation!=Operation||group.Generation!=Generation||group.Scope!=Scope||group.Phase!=phase)throw new InvalidOperationException("Exact complete NPC recovery group phase required");
        foreach(var member in group.Members)if(member.File==File&&member.Hash==CandidateHash&&member.BeforeHash==BeforeHash)return;
        throw new InvalidDataException("NPC recovery member differs from durable complete group");
    }
    internal static RebirthNpcLiveRecoveryIntent Read(string directory,string file)
    {
        if(string.IsNullOrWhiteSpace(file)||Path.GetFileName(file)!=file||file.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidDataException("NPC recovery store file invalid");
        string path=RebirthNpcPersistenceFile.RecoveryIntentPath(Path.Combine(directory,file));
        if(!System.IO.File.Exists(path)||new FileInfo(path).Length>16384)throw new InvalidDataException("NPC recovery intent primary unavailable");
        XElement root;using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384}))root=XElement.Load(reader);
        if(root.Name!="npcLiveRecovery"||(int?)root.Attribute("version")!=1||(string)root.Attribute("file")!=file||
            !Guid.TryParseExact((string)root.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
            !ulong.TryParse((string)root.Attribute("previousGeneration"),NumberStyles.None,CultureInfo.InvariantCulture,out var previous)||previous==ulong.MaxValue||
            !ulong.TryParse((string)root.Attribute("generation"),NumberStyles.None,CultureInfo.InvariantCulture,out var generation)||generation!=previous+1||
            !byte.TryParse((string)root.Attribute("phase"),out var phase)||phase>3||
            !Digest((string)root.Attribute("candidateHash"))||((string)root.Attribute("beforeHash")!="absent"&&!Digest((string)root.Attribute("beforeHash")))||
            !ulong.TryParse((string)root.Attribute("terminalSequence"),NumberStyles.None,CultureInfo.InvariantCulture,out var sequence))throw new InvalidDataException("NPC recovery intent malformed");
        string terminal=(string)root.Attribute("terminalHash");if(phase==3?!Digest(terminal):!string.IsNullOrEmpty(terminal)||sequence!=0)throw new InvalidDataException("NPC recovery terminal intent invalid");
        return new RebirthNpcLiveRecoveryIntent{Directory=Path.GetFullPath(directory),File=file,Scope=(string)root.Attribute("scope"),Operation=operation,PreviousGeneration=previous,Generation=generation,
            Phase=phase,BeforeHash=(string)root.Attribute("beforeHash"),CandidateHash=(string)root.Attribute("candidateHash"),TerminalHash=terminal,TerminalSequence=sequence};
    }
    // Freeze every member with Stage, then save the complete Group, then publish
    // member intents. No primary publication is permitted during this staging.
    internal static RebirthNpcLiveRecoveryIntent Stage(string directory,string file,Guid operation,ulong previous,XmlDocument candidate)
    {
        var scope=RebirthNpcSaveScope.ObserveCurrent();
        if(candidate?.DocumentElement==null||scope==null||previous==ulong.MaxValue||Path.GetFileName(file)!=file)throw new InvalidDataException("NPC recovery candidate/scope invalid");
        var intent=new RebirthNpcLiveRecoveryIntent{Directory=Path.GetFullPath(directory),File=file,Operation=operation,Scope=scope.Fingerprint,PreviousGeneration=previous,Generation=checked(previous+1)};
        intent.RequireLease();
        if(System.IO.File.Exists(intent.Intent)||System.IO.File.Exists(intent.Intent+".bak"))throw new InvalidDataException("Existing NPC recovery intent requires reconciliation");
        // Freeze the previous exact primary before any replacement. Missing is a
        // recorded precondition, not evidence that an original effect was unused.
        if(System.IO.File.Exists(intent.Primary))
        {
            CopyBounded(intent.Primary,intent.Prior);intent.BeforeHash=Hash(intent.Prior);
            if(Hash(intent.Primary)!=intent.BeforeHash)throw new InvalidDataException("NPC recovery prior changed during staging");
        }
        else intent.BeforeHash="absent";
        using(var stream=new FileStream(intent.Candidate,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=true,CloseOutput=false,NewLineHandling=NewLineHandling.Entitize}))
        {candidate.Save(writer);writer.Flush();if(stream.Length>MaximumDocument)throw new InvalidDataException("NPC recovery candidate oversized");stream.Flush(true);}
        intent.CandidateHash=Hash(intent.Candidate);return intent;
    }
    internal static RebirthNpcLiveRecoveryIntent PublishPreparedMember(RebirthNpcLiveRecoveryGroup group,RebirthNpcLiveRecoveryGroup.Member member)
    {
        if(group==null||member==null||group.Phase!=0)throw new InvalidOperationException("Complete prepared recovery group required before member intent");
        var savedGroup=RebirthNpcLiveRecoveryGroup.Read(group.Directory);RebirthNpcLiveRecoveryGroup.Member savedMember=null;
        if(savedGroup==null||savedGroup.Operation!=group.Operation||savedGroup.Generation!=group.Generation||savedGroup.Scope!=group.Scope||savedGroup.Phase!=0)throw new InvalidDataException("Prepared group durable identity differs");
        foreach(var row in savedGroup.Members)if(row.File==member.File)savedMember=row;
        if(savedMember==null||savedMember.Hash!=member.Hash||savedMember.BeforeHash!=member.BeforeHash)throw new InvalidDataException("Member is not in the actual complete prepared group");
        var intent=new RebirthNpcLiveRecoveryIntent{Directory=group.Directory,Scope=group.Scope,Operation=group.Operation,File=member.File,PreviousGeneration=group.PreviousGeneration,Generation=group.Generation,
            BeforeHash=member.BeforeHash,CandidateHash=member.Hash};
        intent.RequireLease();
        if(Hash(intent.Candidate)!=intent.CandidateHash||(System.IO.File.Exists(intent.Primary)?Hash(intent.Primary):"absent")!=intent.BeforeHash||
            (intent.BeforeHash!="absent"&&Hash(intent.Prior)!=intent.BeforeHash))throw new InvalidDataException("Prepared group member original/candidate evidence differs");
        if(System.IO.File.Exists(intent.Intent))
        {
            var existing=Read(group.Directory,member.File);
            if(!XNode.DeepEquals(existing.WriteRoot(),intent.WriteRoot()))throw new InvalidDataException("Prepared member intent conflicts with complete group");
            return existing;
        }
        if(System.IO.File.Exists(intent.Intent+".bak"))throw new InvalidDataException("Prepared intent primary missing with retained backup");
        intent.Save();return intent;
    }
    private static void CopyBounded(string source,string target)
    {
        using(var input=new FileStream(source,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            long remaining=input.Length;if(remaining<=0||remaining>MaximumDocument)throw new InvalidDataException("NPC recovery prior size unsupported; original retained");
            using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {byte[] buffer=new byte[65536];while(remaining>0){int read=input.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));if(read<=0)throw new EndOfStreamException();output.Write(buffer,0,read);remaining-=read;}if(input.ReadByte()!=-1)throw new InvalidDataException("NPC recovery original changed length");output.Flush(true);}
        }
    }
    internal XmlDocument ReadCandidate()
    {
        RequireLease();if(Hash(Candidate)!=CandidateHash)throw new InvalidDataException("NPC recovery staged candidate differs");
        var doc=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(Candidate,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumDocument}))doc.Load(reader);return doc;
    }
    internal void Publish()
    {
        Reload();RequireGroup(1);if(Phase>1)throw new InvalidOperationException("Installed recovery cannot reinstall original candidate");
        if(Hash(Candidate)!=CandidateHash)throw new InvalidDataException("NPC recovery candidate evidence differs");
        string actual=System.IO.File.Exists(Primary)?Hash(Primary):"absent";
        if(actual!=CandidateHash)
        {
            if(Phase!=0||actual!=BeforeHash)throw new InvalidDataException("NPC recovery current primary is neither original nor published candidate");
            string staged=Primary+".recovery."+Operation.ToString("N")+".publish.tmp";
            if(!System.IO.File.Exists(staged))CopyBounded(Candidate,staged);
            if(Hash(staged)!=CandidateHash)throw new InvalidDataException("NPC recovery publication staging differs");
            RequireLease();
            if((System.IO.File.Exists(Primary)?Hash(Primary):"absent")!=BeforeHash)throw new InvalidDataException("NPC recovery primary changed before publication");
            if(!RebirthDurableFileCommit.TryPublish(staged,Primary,out var error))throw new IOException("NPC recovery candidate publication failed: "+error);
        }
        if(Hash(Primary)!=CandidateHash)throw new InvalidDataException("NPC recovery saved candidate readback differs");
        if(Phase==0){Phase=1;Save();}
    }
    internal void MarkInstalled()
    {
        Reload();RequireGroup(1);if(Phase==2&&RebirthNpcRecoveryGeneration.RequireCurrent()==Generation)return;
        if(Phase!=1||Hash(Primary)!=CandidateHash||RebirthNpcRecoveryGeneration.RequireCurrent()!=Generation)throw new InvalidOperationException("NPC recovery local install publication mismatch");
        Phase=2;Save();
    }
    internal void MarkConverged(ulong sequence,string hash)
    {
        Reload();RequireGroup(3);
        var closing=RebirthNpcLiveRecoveryGroup.Read(Directory);bool matches=false;foreach(var member in closing.Members)if(member.File==File&&member.TerminalHash==hash&&member.TerminalSequence==sequence)matches=true;
        if(!matches)throw new InvalidDataException("Member terminal differs from complete closing proof");
        if(Phase==3&&TerminalSequence==sequence&&TerminalHash==hash&&Hash(Primary)==hash)return;
        if(Phase!=2||!Digest(hash)||Hash(Primary)!=hash)throw new InvalidOperationException("NPC recovery latest convergence publication mismatch");
        TerminalSequence=sequence;TerminalHash=hash;Phase=3;Save();
    }
}
