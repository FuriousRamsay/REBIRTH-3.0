using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Text;
using System.Globalization;
using System.Collections.Generic;

// Complete member list is durable before the first member publication. A cold
// process never discovers a transaction's extent by guessing from file absence.
internal sealed class RebirthNpcLiveRecoveryGroup
{
    internal const string FileName="RebirthNpcLiveRecovery.xml";
    internal sealed class Member{internal string File,Hash,BeforeHash,TerminalHash;internal ulong TerminalSequence;}
    private static readonly HashSet<string> Allowed=new HashSet<string>(StringComparer.Ordinal)
    {"RebirthNpcStableIdentities.xml","rebirth_npc_inventory.xml","rebirth_npc_equipment.xml","RebirthNpcExecutionState.xml","rebirth_npc_settlement.xml","RebirthNpcWorkCheckpoints.xml","RebirthNpcWorkOutcomes.xml","RebirthNpcSocialState.xml","RebirthNpcCombatState.xml","RebirthNpcPersistentRecords.xml","RebirthNpcWorldIntegration.xml"};
    internal string Directory,Scope,Target;
    internal Guid Operation;
    internal ulong PreviousGeneration,Generation;
    internal byte Phase;
    internal string ClosingProjection;
    internal Member[] Members;
    private string PathName=>Path.Combine(Directory,FileName);
    private XElement Image()
    {
        var root=new XElement("npcLiveRecoveryGroup",new XAttribute("version",2),new XAttribute("scope",Scope),new XAttribute("target",Target),new XAttribute("operation",Operation.ToString("N")),
            new XAttribute("previousGeneration",PreviousGeneration.ToString(CultureInfo.InvariantCulture)),new XAttribute("generation",Generation.ToString(CultureInfo.InvariantCulture)),new XAttribute("phase",Phase),new XAttribute("closingProjection",ClosingProjection??string.Empty));
        foreach(var member in Members)root.Add(new XElement("store",new XAttribute("file",member.File),new XAttribute("hash",member.Hash),new XAttribute("beforeHash",member.BeforeHash),new XAttribute("terminalHash",member.TerminalHash??string.Empty),new XAttribute("terminalSequence",member.TerminalSequence.ToString(CultureInfo.InvariantCulture))));return root;
    }
    internal static RebirthNpcLiveRecoveryGroup Read(string directory)
    {
        string path=Path.Combine(directory,FileName);
        if(!File.Exists(path))
        {if(File.Exists(path+".bak"))throw new InvalidDataException("NPC recovery group primary missing with retained backup");return null;}
        if(new FileInfo(path).Length>32768)throw new InvalidDataException("NPC recovery group oversized");
        XElement root;using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=32768}))root=XElement.Load(reader);
        if(root.Name!="npcLiveRecoveryGroup"||((int?)root.Attribute("version")!=1&&(int?)root.Attribute("version")!=2)||!Allowed.Contains((string)root.Attribute("target")??"")||
            !Guid.TryParseExact((string)root.Attribute("operation"),"N",out var operation)||operation==Guid.Empty||
            !ulong.TryParse((string)root.Attribute("previousGeneration"),NumberStyles.None,CultureInfo.InvariantCulture,out var previous)||previous==ulong.MaxValue||
            !ulong.TryParse((string)root.Attribute("generation"),NumberStyles.None,CultureInfo.InvariantCulture,out var generation)||generation!=previous+1||
            !byte.TryParse((string)root.Attribute("phase"),out var phase)||phase>3)throw new InvalidDataException("NPC recovery group invalid");
        var members=new List<Member>();var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in root.Elements())
        {
            string file=(string)row.Attribute("file"),hash=(string)row.Attribute("hash"),before=(string)row.Attribute("beforeHash");
            if(row.Name!="store"||!Allowed.Contains(file??"")||!names.Add(file)||hash==null||!System.Text.RegularExpressions.Regex.IsMatch(hash,"\\A[0-9a-f]{64}\\z")||members.Count>=11)
                throw new InvalidDataException("NPC recovery group member invalid");
            if(before!="absent"&&(before==null||!System.Text.RegularExpressions.Regex.IsMatch(before,"\\A[0-9a-f]{64}\\z")))throw new InvalidDataException("NPC recovery member original hash invalid");
            string terminal=(string)row.Attribute("terminalHash")??string.Empty;ulong terminalSequence=0;
            if(row.Attribute("terminalSequence")!=null&&!ulong.TryParse((string)row.Attribute("terminalSequence"),NumberStyles.None,CultureInfo.InvariantCulture,out terminalSequence))throw new InvalidDataException("Closing member sequence invalid");
            if(phase==3)
            {if((int?)root.Attribute("version")!=2||!System.Text.RegularExpressions.Regex.IsMatch(terminal,@"\A[0-9a-f]{64}\z"))throw new InvalidDataException("Legacy closing group lacks complete publication proof");}
            else if(terminal.Length!=0||terminalSequence!=0)throw new InvalidDataException("Premature recovery terminal binding");
            members.Add(new Member{File=file,Hash=hash,BeforeHash=before,TerminalHash=terminal,TerminalSequence=terminalSequence});
        }
        string closing=(string)root.Attribute("closingProjection")??string.Empty;
        if(phase==3?!System.Text.RegularExpressions.Regex.IsMatch(closing,"\\A[0-9a-f]{64}\\z"):closing.Length!=0)throw new InvalidDataException("Recovery closing projection proof invalid");
        string target=(string)root.Attribute("target");
        if(!names.Contains(target)||members.Count==0||(target==RebirthNpcAggregatePersistenceStore.FileName&&!names.Contains("RebirthNpcWorldIntegration.xml")))throw new InvalidDataException("NPC recovery group incomplete");
        if(members.Count!=(target==RebirthNpcAggregatePersistenceStore.FileName?2:1))throw new InvalidDataException("Recovery group contains a member with no matching runtime adapter");
        members.Sort((a,b)=>string.CompareOrdinal(a.File,b.File));
        return new RebirthNpcLiveRecoveryGroup{Directory=Path.GetFullPath(directory),Scope=(string)root.Attribute("scope"),Target=target,Operation=operation,PreviousGeneration=previous,Generation=generation,Phase=phase,ClosingProjection=closing,Members=members.ToArray()};
    }
    private void RequireLeases()
    {
        var scope=RebirthNpcSaveScope.ObserveCurrent();
        if(!ThreadManager.IsMainThread()||GameManager.Instance?.World?.IsRemote()!=false||scope==null||scope.Fingerprint!=Scope||!string.Equals(Path.GetFullPath(scope.SaveDirectory),Directory,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("NPC recovery group scope changed");
        foreach(var member in Members)if(!RebirthNpcPersistenceFile.HasRecoveryLease(Path.Combine(Directory,member.File),Operation))throw new InvalidOperationException("NPC recovery group member writer fence missing");
    }
    private void Save()
    {
        RequireLeases();string staged=PathName+".write."+Guid.NewGuid().ToString("N");
        using(var stream=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None))
        using(var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=true,CloseOutput=false}))
        {new XDocument(Image()).Save(writer);writer.Flush();stream.Flush(true);}
        if(!RebirthDurableFileCommit.TryPublish(staged,PathName,out var error))throw new IOException("NPC recovery group publication failed: "+error);
        if(!XNode.DeepEquals(Read(Directory).Image(),Image()))throw new InvalidDataException("NPC recovery group readback differs");
    }
    internal static RebirthNpcLiveRecoveryGroup Create(string directory,string target,Guid operation,ulong previous,IEnumerable<Member> members)
    {
        var scope=RebirthNpcSaveScope.ObserveCurrent();var copy=new List<Member>();var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var member in members??new Member[0])
        {
            if(member==null||!Allowed.Contains(member.File)||!names.Add(member.File))throw new InvalidDataException("NPC recovery group proposed member invalid");
            string staged=Path.Combine(directory,member.File)+".recovery."+operation.ToString("N")+".candidate.xml";
            if(RebirthNpcLiveRecoveryIntent.Hash(staged)!=member.Hash)throw new InvalidDataException("NPC recovery member candidate was not durably frozen");
            string primary=Path.Combine(directory,member.File),prior=primary+".recovery."+operation.ToString("N")+".prior.xml";
            if((File.Exists(primary)?RebirthNpcLiveRecoveryIntent.Hash(primary):"absent")!=member.BeforeHash||
                (member.BeforeHash!="absent"&&RebirthNpcLiveRecoveryIntent.Hash(prior)!=member.BeforeHash))throw new InvalidDataException("NPC recovery member original preimage differs");
            copy.Add(new Member{File=member.File,Hash=member.Hash,BeforeHash=member.BeforeHash});
        }
        if(!names.Contains(target)||copy.Count==0||copy.Count>11||previous==ulong.MaxValue||scope==null||operation==Guid.Empty)throw new InvalidDataException("NPC recovery group proposal incomplete");
        if(target==RebirthNpcAggregatePersistenceStore.FileName&&!names.Contains("RebirthNpcWorldIntegration.xml"))throw new InvalidDataException("NPC aggregate recovery omitted name generation");
        if(copy.Count!=(target==RebirthNpcAggregatePersistenceStore.FileName?2:1))throw new InvalidDataException("Recovery group contains an unsupported related runtime adapter");
        copy.Sort((a,b)=>string.CompareOrdinal(a.File,b.File));
        var group=new RebirthNpcLiveRecoveryGroup{Directory=Path.GetFullPath(directory),Target=target,Scope=scope.Fingerprint,Operation=operation,PreviousGeneration=previous,Generation=checked(previous+1),Members=copy.ToArray()};
        if(Read(directory)!=null)throw new InvalidOperationException("Previous NPC recovery group remains pending");group.Save();return group;
    }
    internal void Advance(byte expected,byte next)
    {
        RequireLeases();var saved=Read(Directory);
        if(saved==null||saved.Operation!=Operation||saved.Generation!=Generation||saved.Scope!=Scope||saved.Target!=Target)throw new InvalidDataException("NPC recovery group immutable identity changed");
        byte old=Phase;Phase=saved.Phase;
        if(!XNode.DeepEquals(saved.Image(),Image())){Phase=old;throw new InvalidDataException("NPC recovery group members changed");}
        if(Phase==next)return;
        if(Phase!=expected||next!=expected+1||next>2)throw new InvalidOperationException("NPC recovery group transition is not monotone");
        Phase=next;Save();
    }
    // Joint positive closing intent is durable BEFORE any member terminal is
    // written. Cold closing never guesses the unrecorded remaining file heads.
    internal void BeginClosing(string projectionProof)
    {
        RequireLeases();var saved=Read(Directory);
        if(saved==null||saved.Operation!=Operation||saved.Generation!=Generation||saved.Phase!=2||saved.Scope!=Scope||
            !System.Text.RegularExpressions.Regex.IsMatch(projectionProof??string.Empty,@"\A[0-9a-f]{64}\z"))throw new InvalidOperationException("Complete current projection proof required for closing");
        var final=new List<Member>();foreach(var row in saved.Members)
        {
            string path=Path.Combine(Directory,row.File);
            if(!RebirthNpcRecoveryPublications.TryResolveLatest(path,Operation,Generation,out _,out ulong sequence,out string hash,out var error))throw new InvalidDataException(error);
            final.Add(new Member{File=row.File,Hash=row.Hash,BeforeHash=row.BeforeHash,TerminalHash=hash,TerminalSequence=sequence});
        }
        Members=final.ToArray();ClosingProjection=projectionProof;Phase=3;Save();
    }    internal static bool Fences(string path)
    {
        try
        {
            var group=Read(Path.GetDirectoryName(Path.GetFullPath(path)));if(group==null)return false;
            foreach(var member in group.Members)if(member.File==Path.GetFileName(path))return group.Phase!=2||!RebirthNpcRecoveryPublications.IsInstalled(path);
            return false;
        }
        catch{return true;} // malformed/missing-primary-with-backup is unknown, never no transaction
    }
    internal void RetireAfterMembers()
    {
        RequireLeases();var saved=Read(Directory);
        if(saved==null||saved.Operation!=Operation||saved.Generation!=Generation||saved.Phase!=3||saved.Scope!=Scope)throw new InvalidOperationException("Actual converged recovery group required before retirement");
        saved.RequireLeases();
        foreach(var member in saved.Members)
        {
            string primary=Path.Combine(Directory,member.File),intent=RebirthNpcPersistenceFile.RecoveryIntentPath(primary);
            if(File.Exists(intent))throw new InvalidOperationException("Recovery member fence must retire before group fence");
            string completed=primary+".recovery-completed."+Operation.ToString("N")+".xml";
            if(!File.Exists(completed)||new FileInfo(completed).Length>16384)throw new InvalidDataException("Saved completed member unavailable");
            XElement root;using(var reader=XmlReader.Create(completed,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=16384}))root=XElement.Load(reader);
            if(root.Name!="npcLiveRecovery"||((int?)root.Attribute("version")!=1&&(int?)root.Attribute("version")!=2)||(int?)root.Attribute("phase")!=3||
                (string)root.Attribute("file")!=member.File||(string)root.Attribute("scope")!=Scope||(string)root.Attribute("operation")!=Operation.ToString("N")||
                (string)root.Attribute("generation")!=Generation.ToString(CultureInfo.InvariantCulture)||(string)root.Attribute("candidateHash")!=member.Hash||
                (string)root.Attribute("beforeHash")!=member.BeforeHash||(string)root.Attribute("terminalHash")!=member.TerminalHash||
                (string)root.Attribute("terminalSequence")!=member.TerminalSequence.ToString(CultureInfo.InvariantCulture)||member.TerminalHash!=RebirthNpcLiveRecoveryIntent.Hash(primary))
                throw new InvalidDataException("Completed member differs from original group/latest primary");
        }
        string archive=PathName+".completed."+Operation.ToString("N");
        if(File.Exists(PathName+".bak"))
        {if(File.Exists(archive+".bak"))throw new InvalidDataException("Completed group backup destination conflicts");File.Move(PathName+".bak",archive+".bak");}
        RequireLeases();if(File.Exists(archive))throw new InvalidDataException("Completed group destination conflicts");
        File.Move(PathName,archive); // final durable fence is retired last
    }
}
