using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

internal static partial class RebirthCamperRemoteStationCommandStatusStore
{
    private static readonly HashSet<Guid> admitting=new HashSet<Guid>();
    private static readonly object invocationMint=new object();
    // Private one-use permission from a freshly published full identity. It
    // cannot be recreated from a cold identity, fence, status or missing file.
    internal sealed class FreshAdmission
    {
        internal readonly string Save,Path;private readonly XElement identity;
        private bool used,invoked;
        internal XElement Identity=>new XElement(identity);
        internal bool Invoked=>invoked;
        private FreshAdmission(string save,string path,XElement identity)
        {Save=save;Path=path;this.identity=new XElement(identity);}
        internal static FreshAdmission Capture(string save,string path,XElement identity,object permission)
        {if(!ReferenceEquals(permission,invocationMint))throw new InvalidOperationException("Fresh admission permission required.");return new FreshAdmission(save,path,identity);}
        internal bool Invoke(Func<bool> current,Func<bool> action)
        {
            if(!ThreadManager.IsMainThread()||used||current==null||action==null)return false;used=true;
            try{if(!PublishInvocation(this)||!current())return false;invoked=true;return action();}
            finally{if(!invoked)PublishUninvoked(this);}
        }
    }
    private static XElement ReadInvocationMetadata(string path)
    {
        var file=new FileInfo(path);
        if(!file.Exists||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0||!Safe(Path.GetDirectoryName(path)))throw new InvalidDataException("Bounded command invocation metadata required.");
        using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=MaximumBytes}))return XElement.Load(reader);
    }
    private static bool Protocol(XElement identity)=>identity.Name=="stationCommandIdentity"&&(int?)identity.Attribute("version")==2&&
        (int?)identity.Attribute("invocationProtocol")==1&&(int?)identity.Attribute("admittedDeaths") is int deaths&&deaths>=0;
    private static string InvocationPath(string save,XElement identity,bool historical=false)
    {
        string owner=(string)identity.Attribute("owner"),operation=(string)identity.Attribute("operation");
        if(owner==null||owner.Length!=64||!owner.All(Uri.IsHexDigit)||!Guid.TryParseExact(operation,"N",out var id)||id==Guid.Empty)throw new InvalidDataException();
        string path=System.IO.Path.Combine(save,"REBIRTH",historical?"RemoteStationCommandInvocationHistory":"RemoteStationCommandInvocations",owner,operation+".xml");
        if(!Safe(System.IO.Path.GetDirectoryName(path)))throw new InvalidDataException();return path;
    }
    private static XElement InvocationBody(XElement identity)=>new XElement("stationCommandInvocation",new XAttribute("version",1),new XElement(identity));
    // -1 unknown/staged/conflicting, 0 positively unentered new protocol,
    // 1 complete immutable before-handler fence. Legacy absence is never 0.
    private static int InvocationState(string save,XElement identity)
    {
        if(!Protocol(identity))return -1;
        int active=InvocationStateAt(InvocationPath(save,identity),identity),history=InvocationStateAt(InvocationPath(save,identity,true),identity);
        if(active<0||history<0||active==1&&history==1)return -1;
        return active==1||history==1?1:0;
    }
    private static int InvocationStateAt(string path,XElement identity)
    {
        string directory=System.IO.Path.GetDirectoryName(path);
        if(!Directory.Exists(directory))return 0;
        var candidates=Directory.EnumerateFileSystemEntries(directory,(string)identity.Attribute("operation")+"*").Take(3).ToArray();
        if(candidates.Length==0)return 0;
        if(candidates.Length!=1||candidates[0]!=path||!File.Exists(path))return -1;
        return XNode.DeepEquals(ReadInvocationMetadata(path),InvocationBody(identity))?1:-1;
    }
    // Independent immutable companion migration. Readers accept either exact
    // location, so identity/fence rename interruption never grants redispatch.
    private static bool ArchiveInvocation(string save,XElement identity)
    {
        if(!Protocol(identity))return true;
        int state=InvocationState(save,identity);if(state<0)return false;if(state==0)return true;
        string source=InvocationPath(save,identity),target=InvocationPath(save,identity,true);
        if(File.Exists(target))return true;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
        if(!Safe(System.IO.Path.GetDirectoryName(target))||InvocationState(save,identity)!=1||!Exact(source,InvocationBody(identity)))return false;
        File.Move(source,target);
        return InvocationState(save,identity)==1&&Exact(target,InvocationBody(identity));
    }
    private static bool Budget(string directory,int additionalBytes,int reserve=1)
    {
        if(!Safe(directory)||additionalBytes<0||additionalBytes>MaximumBytes)return false;
        string root=System.IO.Path.GetDirectoryName(directory);int count=0;long bytes=0;
        if(Directory.Exists(root))foreach(string owner in Directory.EnumerateFileSystemEntries(root))
        {
            if(!Directory.Exists(owner)||!Safe(owner)||++count>MaximumRecords*4)return false;
            foreach(string path in Directory.EnumerateFileSystemEntries(owner))
            {
                var file=new FileInfo(path);if(++count>MaximumRecords*4||!file.Exists||!path.EndsWith(".xml",StringComparison.Ordinal)||file.Length<1||file.Length>MaximumBytes||(file.Attributes&FileAttributes.ReparsePoint)!=0)return false;
                bytes=checked(bytes+file.Length);if(bytes+additionalBytes>64L*1024*1024)return false;
            }
        }
        return count+reserve<=MaximumRecords*4;
    }
    private static bool PublishInvocation(FreshAdmission fresh)
    {
        if(!Protocol(fresh.Identity)||!Safe(System.IO.Path.GetDirectoryName(fresh.Path))||!Candidates(fresh.Path)||!Exact(fresh.Path,fresh.Identity)||
            File.Exists(fresh.Path+".refusal.xml")||InvocationState(fresh.Save,fresh.Identity)!=0)return false;
        string path=InvocationPath(fresh.Save,fresh.Identity),directory=System.IO.Path.GetDirectoryName(path);var body=InvocationBody(fresh.Identity);
        byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(!Budget(directory,bytes.Length,2))return false;
        Directory.CreateDirectory(directory);if(!Safe(directory))return false;string temporary=path+".tmp";
        using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
        if(!Exact(temporary,body)||!Exact(fresh.Path,fresh.Identity)||File.Exists(fresh.Path+".refusal.xml"))return false;
        File.Move(temporary,path);
        return InvocationState(fresh.Save,fresh.Identity)==1&&Exact(fresh.Path,fresh.Identity)&&!File.Exists(fresh.Path+".refusal.xml");
    }
    // This is the actual sole new-protocol dispatch boundary. Finally can prove
    // no handler invocation on this warm fresh permission, including expired
    // lease or a fence publication failure. Handler exceptions stay uncertain.
    internal static bool InvokeFresh(FreshAdmission fresh,Func<bool> current,Func<bool> invoke)
    {
        if(!ThreadManager.IsMainThread()||fresh==null||current==null||invoke==null)return false;
        return fresh.Invoke(current,invoke);
    }
    private static bool PublishUninvoked(FreshAdmission fresh)
    {
        try
        {
            if(fresh==null||fresh.Invoked||!Protocol(fresh.Identity)||!Safe(System.IO.Path.GetDirectoryName(fresh.Path))||!Candidates(fresh.Path)||!Exact(fresh.Path,fresh.Identity))return false;
            int deaths=(int)fresh.Identity.Attribute("admittedDeaths");var body=RefusalBody(fresh.Identity,deaths);string target=fresh.Path+".refusal.xml";
            if(File.Exists(target))return Exact(target,body);
            byte[] bytes=Encoding.UTF8.GetBytes(body.ToString(SaveOptions.DisableFormatting));if(!Budget(System.IO.Path.GetDirectoryName(target),bytes.Length))return false;
            string temporary=target+".tmp";using(var file=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
            if(fresh.Invoked||!Exact(fresh.Path,fresh.Identity)||!Exact(temporary,body)||!Safe(System.IO.Path.GetDirectoryName(target)))return false;
            File.Move(temporary,target);return Exact(target,body);
        }
        catch{return false;} // Retain all ambiguous candidates and original identity.
    }
    internal static bool TrySaveNeverInvoked(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command)
    {
        try
        {
            if(admitting.Contains(command.Operation)||!Resolve(actor,address,creation,lease,command,out var save,out _,out _,out var path,out var identity)||!Candidates(path)||!Exact(path,identity)||!Protocol(identity)||
                InvocationState(save,identity)!=0)return false;
            // Full v2 admission + enforced durable dispatch fence is positive
            // protocol evidence. Missing legacy files or a staged fence is not.
            return PublishUninvoked(FreshAdmission.Capture(save,path,identity,invocationMint));
        }
        catch{return false;}
    }
    // A qualified new-protocol query reserves this exact original against a
    // delayed command before publishing its never-invoked refusal. It cannot
    // mint a dispatch permission for its caller or enter any effect service.
    internal static bool TryRefuseUnreceived(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command)
    {
        try
        {
            if(!RebirthCamperRemoteStationCommandStatus.HasNativeOriginal(command)||admitting.Contains(command.Operation))return false;
            if(!TryAdmit(actor,address,creation,lease,command,out _,out var disposition,out var fresh))return false;
            if(disposition==AdmissionDisposition.NewlyPublished)
                return fresh!=null&&PublishUninvoked(fresh)&&TryReadBeforeEffectRefusal(actor,address,creation,lease,command,out _,out _);
            return TrySaveNeverInvoked(actor,address,creation,lease,command);
        }
        catch{return false;}
    }
    internal static bool HasInvocation(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command)
    {
        try{return Resolve(actor,address,creation,lease,command,out var save,out _,out _,out var path,out var identity)&&Exact(path,identity)&&
            ((int?)identity.Attribute("version")==1||InvocationState(save,identity)==1);}
        catch{return false;}
    }
    internal static bool TryReadAdmittedDeaths(EntityPlayer actor,RebirthCamperStationAddress address,string creation,object lease,RebirthCamperRemoteStationCommand command,out int deaths)
    {
        deaths=-1;
        try
        {
            if(!Resolve(actor,address,creation,lease,command,out _,out _,out _,out var path,out var identity)||!Exact(path,identity)||!Protocol(identity))return false;
            deaths=(int)identity.Attribute("admittedDeaths");return deaths>=0;
        }
        catch{return false;}
    }
}
