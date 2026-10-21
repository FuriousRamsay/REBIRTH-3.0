using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

// Exact authenticated command identity only. No inventories, effect outcomes,
// generic absence-as-unused classification or native effect writer lives here.
internal static partial class RebirthCamperRemoteStationCommandStatusStore
{
    internal enum AdmissionDisposition : byte { Unknown=0, NewlyPublished=1, ExistingStatusOnly=2 }
    private const int MaximumBytes=8192,MaximumRecords=4096;
    private static string ScopePath()=>Path.GetFullPath(GameIO.GetSaveGameDir()).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
    private static bool Safe(string path)
    {
        for(var item=new DirectoryInfo(Path.GetFullPath(path));item!=null;item=item.Parent)
            if(item.Exists&&(item.Attributes&FileAttributes.ReparsePoint)!=0)return false;
        return true;
    }
    private static bool Resolve(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out string save,out string owner,out string root,out string path,out XElement expected)
    {
        save=owner=root=path=null;expected=null;
        if(!ThreadManager.IsMainThread()||actor==null||command==null||!address.IsValid||actor.IsDead()||actor.world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,actor.world)||!ReferenceEquals(actor.world.GetEntity(actor.entityId),actor)||creation!=RebirthCamperCharacterAccess.CurrentCreation(actor)||
            !RebirthCamperRemoteStationTransport.IsCurrent(actor,address,creation,lease)||!RebirthCamperStationNativeActorPayment.RemoteIdentity(actor,out var identity,out _))return false;
        save=ScopePath();owner=identity.StorageKey;if(!RebirthOriginalActionScope.Matches(actor,owner,creation)||!Safe(save))return false;
        root=Path.Combine(save,"REBIRTH","RemoteStationCommands");path=Path.Combine(root,owner,command.Operation.ToString("N")+".xml");if(!Safe(Path.GetDirectoryName(path)))return false;
        // Terminal history preserves identity forever without consuming the
        // bounded active-admission set. It grants no saved effect authority.
        string history=Path.Combine(save,"REBIRTH","RemoteStationCommandHistory",owner,command.Operation.ToString("N")+".xml");
        if(!Safe(Path.GetDirectoryName(history))||!Candidates(history))return false;
        if(!File.Exists(path)&&!File.Exists(history)&&(File.Exists(path+".refusal.xml")||File.Exists(history+".refusal.xml")))return false;
        if(File.Exists(history))
        {if(File.Exists(path)||!Candidates(path)||File.Exists(path+".refusal.xml"))return false;path=history;}
        string binding=RebirthCamperRemoteStationCommandStatus.Binding(command,owner,creation,address);
        int version=2,deaths=actor.Died;
        if(File.Exists(path))
        {
            var header=ReadInvocationMetadata(path);version=(int?)header.Attribute("version")??0;
            if(version==2)deaths=(int?)header.Attribute("admittedDeaths")??-1;
            else if(version!=1)return false;
        }
        if(version==2&&deaths<0)return false;
        expected=new XElement("stationCommandIdentity",new XAttribute("version",version),new XAttribute("world",actor.world.worldState.Guid),new XAttribute("save",save),new XAttribute("owner",owner),new XAttribute("creation",creation),new XAttribute("address",address.ToString()),new XAttribute("operation",command.Operation.ToString("N")),new XAttribute("kind",(byte)command.Kind),new XAttribute("job",command.Job.ToString("N")),new XAttribute("recipe",command.RecipeName),new XAttribute("batches",command.Batches),new XAttribute("tier",command.Tier),new XAttribute("binding",binding));
        if(version==2)expected.Add(new XAttribute("invocationProtocol",1),new XAttribute("admittedDeaths",deaths));
        return Encoding.UTF8.GetByteCount(expected.ToString(SaveOptions.DisableFormatting))<=MaximumBytes;
    }
    private static bool Exact(string path,XElement expected)
    {
        var info=new FileInfo(path);if(!info.Exists||info.Length<1||info.Length>MaximumBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
        using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))
        {var actual=XElement.Load(reader);return XNode.DeepEquals(actual,expected);}
    }
    private static bool Candidates(string path)
    {
        string directory=Path.GetDirectoryName(path);if(!Directory.Exists(directory))return true;
        foreach(var candidate in Directory.EnumerateFileSystemEntries(directory,Path.GetFileNameWithoutExtension(path)+"*"))
            if(candidate!=path&&candidate!=path+".refusal.xml")return false;
        return true;
    }
    internal static bool TryReadExact(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out string binding)
    {
        binding=null;
        try
        {
            if(!Resolve(actor,address,creation,lease,command,out _,out _,out _,out var path,out var body)||!Candidates(path)||!Exact(path,body))return false;
            binding=(string)body.Attribute("binding");return true;
        }catch{return false;}
    }
    // Only migrates complete identity metadata after an independently qualified
    // saved terminal observation. Every later query still revalidates its native
    // effect witness; this archive never becomes an outcome/payment receipt.
    internal static bool TryArchiveAppliedIdentity(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,
        RebirthCamperRemoteStationCommand command,RebirthCamperRemoteStationCommandStatus.Observation observation)
        =>TryArchiveTerminalIdentity(actor,address,creation,lease,command,observation,RebirthCamperRemoteStationCommandStatus.State.Applied);
    internal static bool TryArchiveRejectedIdentity(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,
        RebirthCamperRemoteStationCommand command,RebirthCamperRemoteStationCommandStatus.Observation observation)
        =>TryArchiveTerminalIdentity(actor,address,creation,lease,command,observation,RebirthCamperRemoteStationCommandStatus.State.Rejected);
    private static bool TryArchiveTerminalIdentity(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,
        RebirthCamperRemoteStationCommand command,RebirthCamperRemoteStationCommandStatus.Observation observation,RebirthCamperRemoteStationCommandStatus.State terminal)
    {
        try
        {
            if((terminal!=RebirthCamperRemoteStationCommandStatus.State.Applied&&terminal!=RebirthCamperRemoteStationCommandStatus.State.Rejected)||observation==null||observation.Status!=terminal||
                observation.RequestOperation!=command.Operation||observation.Job!=command.Job||observation.Kind!=command.Kind||
                !Resolve(actor,address,creation,lease,command,out var save,out var owner,out _,out var source,out var identity)||
                observation.Owner!=owner||observation.Creation!=creation||observation.Address!=address.ToString()||
                observation.RequestBinding!=(string)identity.Attribute("binding")||!Candidates(source)||!Exact(source,identity))return false;
            string target=Path.Combine(save,"REBIRTH","RemoteStationCommandHistory",owner,command.Operation.ToString("N")+".xml");
            if(source==target)return ArchiveInvocation(save,identity);
            // Full pre-effect sidecars use their own two-file archival. Native
            // terminal observations migrate only the identity, never a receipt.
            if(File.Exists(source+".refusal.xml")||File.Exists(target+".refusal.xml")||!Safe(Path.GetDirectoryName(target))||!Candidates(target)||File.Exists(target))return false;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if(!Safe(Path.GetDirectoryName(target))||
                !Resolve(actor,address,creation,lease,command,out var currentSave,out _,out _,out var currentSource,out var current)||
                save!=currentSave||source!=currentSource||!XNode.DeepEquals(identity,current)||!Candidates(source)||!Exact(source,identity))return false;
            File.Move(source,target); // one same-save atomic rename; no copy/delete rollback
            return TryReadExact(actor,address,creation,lease,command,out var binding)&&binding==observation.RequestBinding&&ArchiveInvocation(save,identity);
        }catch{return false;} // failed rename/readback retains the original identity
    }
    private static XElement RefusalBody(XElement identity,int deaths)
        =>new XElement("stationCommandBeforeEffectRefusal",new XAttribute("version",1),
            new XAttribute("deaths",deaths),new XElement(identity));
    internal static bool TryReadBeforeEffectRefusal(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out int deaths,out bool candidatePresent)
    {
        deaths=-1;candidatePresent=true;
        try
        {
            if(!Resolve(actor,address,creation,lease,command,out var save,out var owner,out _,out var path,out var body)||!Candidates(path)||!Exact(path,body))return false;
            string refusal=path+".refusal.xml";
            string historical=Path.Combine(save,"REBIRTH","RemoteStationCommandHistory",owner,command.Operation.ToString("N")+".xml.refusal.xml");
            if(refusal!=historical&&File.Exists(historical)){if(File.Exists(refusal))return false;refusal=historical;}
            candidatePresent=File.Exists(refusal);if(!candidatePresent)return false;
            var info=new FileInfo(refusal);if(!info.Exists||info.Length<1||info.Length>MaximumBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            XElement saved;
            using(var stream=new FileStream(refusal,FileMode.Open,FileAccess.Read,FileShare.Read))
            using(var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))saved=XElement.Load(reader);
            int value=(int?)saved.Attribute("deaths")??-1;
            if(value<0||!XNode.DeepEquals(saved,RefusalBody(body,value)))return false;
            deaths=value;return true;
        }catch{return false;}
    }
    // Two existing complete files are renamed, refusal FIRST. A crash between
    // the renames leaves the active identity and its exact historical refusal;
    // the reader can positively qualify that same original without replay.
    internal static bool TryArchiveBeforeEffectIdentity(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,
        RebirthCamperRemoteStationCommand command)
    {
        try
        {
            if(!TryReadBeforeEffectRefusal(actor,address,creation,lease,command,out int deaths,out _)||
                !Resolve(actor,address,creation,lease,command,out var save,out var owner,out _,out var source,out var identity)||!Exact(source,identity))return false;
            string target=Path.Combine(save,"REBIRTH","RemoteStationCommandHistory",owner,command.Operation.ToString("N")+".xml");
            if(source==target)return ArchiveInvocation(save,identity);
            if(File.Exists(target)||!Safe(Path.GetDirectoryName(target))||!Candidates(target))return false;
            Directory.CreateDirectory(Path.GetDirectoryName(target));if(!Safe(Path.GetDirectoryName(target)))return false;
            XElement refusal=RefusalBody(identity,deaths);
            if(File.Exists(source+".refusal.xml"))
            {
                if(File.Exists(target+".refusal.xml")||!Exact(source+".refusal.xml",refusal))return false;
                File.Move(source+".refusal.xml",target+".refusal.xml");
            }
            if(!Exact(target+".refusal.xml",refusal)||
                !Resolve(actor,address,creation,lease,command,out var currentSave,out _,out _,out var currentSource,out var current)||
                save!=currentSave||source!=currentSource||!XNode.DeepEquals(identity,current)||!Candidates(source)||!Exact(source,identity))return false;
            File.Move(source,target);
            return TryReadBeforeEffectRefusal(actor,address,creation,lease,command,out int saved,out _)&&saved==deaths&&ArchiveInvocation(save,identity);
        }catch{return false;} // retain every intermediate; no destructive rollback
    }
    internal static bool TrySaveBeforeEffectRefusal(RebirthCamperRemoteStationActions.BeforeEffectRefusal proof)
    {
        if(proof==null)return false;
        try
        {
            var actor=proof.Player;
            if(!Resolve(actor,proof.Address,proof.Creation,proof.Lease,proof.Command,out var save,out _,out var root,out var path,out var identity)||!Candidates(path)||!Exact(path,identity))return false;
            int deaths=actor.Died;
            if(deaths<0)return false;
            XElement body=RefusalBody(identity,deaths);string target=path+".refusal.xml";
            if(File.Exists(target))return Exact(target,body);
            byte[] payload=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(payload.Length>MaximumBytes)return false;
            int entries=0;long bytes=0;
            foreach(string directory in Directory.EnumerateDirectories(root))
            {
                if(++entries>MaximumRecords||!Safe(directory))return false;
                foreach(string file in Directory.EnumerateFileSystemEntries(directory))
                {
                    if(++entries>=MaximumRecords||!File.Exists(file)||!file.EndsWith(".xml",StringComparison.Ordinal))return false;
                    var info=new FileInfo(file);if(info.Length<1||info.Length>MaximumBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                    bytes=checked(bytes+info.Length);if(bytes+payload.Length>16L*1024*1024)return false;
                }
            }
            if(Directory.EnumerateFiles(root).Any())return false;
            string temp=target+".tmp";
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(payload,0,payload.Length);file.Flush(true);}
            if(!Resolve(actor,proof.Address,proof.Creation,proof.Lease,proof.Command,out var now,out _,out _,out var same,out var current)||save!=now||same!=path||!XNode.DeepEquals(identity,current)||!Exact(path,identity)||!Exact(temp,body))return false;
            File.Move(temp,target);
            return TryReadBeforeEffectRefusal(actor,proof.Address,proof.Creation,proof.Lease,proof.Command,out var observed,out _)&&observed==deaths;
        }catch{return false;} // unknown candidates are retained, never overwritten
    }
    // Caller invokes after native lease authentication and BEFORE Handle.
    // Only NewlyPublished permits its one ensuing Handle invocation. Existing
    // mapping is status-only even if the first process crashed before Handle.
    // A false result never permits Handle, resend under a fresh ID or rollback.
    internal static bool TryAdmit(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out string binding,out AdmissionDisposition disposition)
        =>TryAdmit(actor,address,creation,lease,command,out binding,out disposition,out _);
    internal static bool TryAdmit(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out string binding,out AdmissionDisposition disposition,out FreshAdmission fresh)
    {
        binding=null;disposition=AdmissionDisposition.Unknown;fresh=null;bool publishing=false;
        try
        {
            if(!Resolve(actor,address,creation,lease,command,out var save,out _,out var root,out var path,out var body)||!Candidates(path))return false;
            if(File.Exists(path))
            {
                if(!TryReadExact(actor,address,creation,lease,command,out binding))return false;
                disposition=AdmissionDisposition.ExistingStatusOnly;return true;
            }
            int entries=0;long bytes=0;
            if(Directory.Exists(root))
            {
                foreach(string directory in Directory.EnumerateDirectories(root))
                {
                    if(++entries>MaximumRecords||!Safe(directory))return false;
                    foreach(string file in Directory.EnumerateFileSystemEntries(directory))
                    {
                        if(++entries>MaximumRecords||!File.Exists(file)||!file.EndsWith(".xml",StringComparison.Ordinal))return false;
                        var info=new FileInfo(file);if(info.Length<1||info.Length>MaximumBytes||(info.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                        bytes=checked(bytes+info.Length);if(bytes>16L*1024*1024)return false;
                    }
                }
                if(Directory.EnumerateFiles(root).Any())return false;
            }
            // Reserve one file+possible owner directory before any allocation/write.
            if(entries>MaximumRecords-2||bytes+Encoding.UTF8.GetByteCount(body.ToString(SaveOptions.DisableFormatting))>16L*1024*1024)return false;
            if(!Protocol(body)||Encoding.UTF8.GetByteCount(InvocationBody(body).ToString(SaveOptions.DisableFormatting))>MaximumBytes||
                Encoding.UTF8.GetByteCount(RefusalBody(body,(int)body.Attribute("admittedDeaths")).ToString(SaveOptions.DisableFormatting))>MaximumBytes)return false;
            if(!admitting.Add(command.Operation))return false;publishing=true;
            Directory.CreateDirectory(Path.GetDirectoryName(path));if(!Safe(Path.GetDirectoryName(path)))return false;
            string temp=path+".tmp";byte[] payload=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(payload,0,payload.Length);file.Flush(true);}
            if(!Resolve(actor,address,creation,lease,command,out var now,out _,out _,out var same,out var current)||now!=save||same!=path||!XNode.DeepEquals(body,current)||!Exact(temp,body))return false;
            File.Move(temp,path);
            fresh=FreshAdmission.Capture(save,path,body,invocationMint);
            if(!TryReadExact(actor,address,creation,lease,command,out binding))
            {PublishUninvoked(fresh);fresh=null;return false;}
            disposition=AdmissionDisposition.NewlyPublished;return true;
        }catch{return false;} // Unknown/conflicting retained candidates never authorize effects.
        finally{if(publishing)admitting.Remove(command.Operation);}
    }
}






