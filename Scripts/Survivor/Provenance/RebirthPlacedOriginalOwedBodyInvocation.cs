using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private static readonly Dictionary<Guid,OwedPhysicalTrace> owedBodyIntents=new Dictionary<Guid,OwedPhysicalTrace>();private static World owedBodyWorld;private static string owedBodyDirectory;
    private static void RememberOwedBody(OwedPhysicalTrace t)
    {var world=GameManager.Instance?.World;if(!ReferenceEquals(owedBodyWorld,world)||owedBodyDirectory!=GameIO.GetSaveGameDir()){owedBodyIntents.Clear();owedBodyWorld=world;owedBodyDirectory=GameIO.GetSaveGameDir();}foreach(var old in owedBodyIntents.Values.ToArray()){var actor=world?.GetEntity(old.Observation.NativeOwnerEntity) as EntityPlayer;if(!ValidOwedPhysicalOwner(actor,old,true))owedBodyIntents.Remove(old.Child.Operation);}if(owedBodyIntents.Count<128||owedBodyIntents.ContainsKey(t.Child.Operation))owedBodyIntents[t.Child.Operation]=t;}
    private static string OwedInvocationPath(OwedPhysicalTrace t,bool index)=>Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original",index?"owed-body-invocation-index":"owed-body-invocations",(index?Hash(t.Creation):t.Child.Operation.ToString("N"))+".xml");
    private static bool SaveOwedInvocationAt(string path,OwedPhysicalTrace t,bool index=false)
    {
        try{if(File.Exists(path)||File.Exists(path+".bak")){if(new FileInfo(path).Length>Maximum)return false;var old=DecodeOwedPhysical(File.ReadAllText(path,new UTF8Encoding(false,true)));return index?old.Creation==t.Creation&&old.Child.World==t.Child.World&&old.Child.Owner==t.Child.Owner&&old.Child.Creation==t.Child.Creation&&old.Observation.NativeOwnerEntity==t.Observation.NativeOwnerEntity:old.Image==t.Image;}Directory.CreateDirectory(Path.GetDirectoryName(path));var pending=Directory.EnumerateFiles(Path.GetDirectoryName(path),Path.GetFileName(path)+".*.tmp").ToArray();if(pending.Length!=0){if(pending.Length!=1||new FileInfo(pending[0]).Length>Maximum||File.ReadAllText(pending[0],new UTF8Encoding(false,true))!=t.Image)return false;return RebirthDurableFileCommit.TryPublish(pending[0],path,out _);}string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var bytes=new UTF8Encoding(false,true).GetBytes(t.Image);using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}return RebirthDurableFileCommit.TryPublish(temp,path,out _)&&File.ReadAllText(path,new UTF8Encoding(false,true))==t.Image;}catch{return false;}
    }
    private static bool RecordOwedBodyInvocation(OwedPhysicalTrace t)=>t.Creation.Length!=0&&SaveOwedInvocationAt(OwedInvocationPath(t,false),t)&&SaveOwedInvocationAt(OwedInvocationPath(t,true),t,true);
    private static bool BeginOwedBodyInvocation(OwedPhysicalTrace t)
    {if(!HasDeferredPermit(true,t.Child.Operation,t.Image))return false;string path=OwedInvocationPath(t,false),folder=Path.GetDirectoryName(path);return !File.Exists(path)&&!File.Exists(path+".bak")&&(!Directory.Exists(folder)||!Directory.EnumerateFiles(folder,Path.GetFileName(path)+".*.tmp").Any())&&RecordOwedBodyInvocation(t);}
    private sealed class OwedRequestToken{internal object Parent;internal NetPackageRequestToSpawnEntity Packet;internal OwedRequestToken Previous;internal World World;internal string Directory;internal ClientInfo Sender;internal EntityPlayer Actor;internal OwedPhysicalTrace Trace;}
    [ThreadStatic]private static OwedRequestToken owedIncoming;
    private static object EnterOwedBodyRequest(NetPackageRequestToSpawnEntity packet,World world,object parent,out bool classified,out bool allow)
    {
        classified=false;allow=true;try
        {
            if(!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||packet?.Sender==null||packet.ecd==null||packet.ecd.entityClass!="Backpack".GetHashCode()||packet.ecd.id< -1)return parent;var sender=packet.Sender;var actor=world.GetEntity(sender.entityId) as EntityPlayer;if(actor==null||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(sender.entityId),sender)||!packet.ValidEntityIdForSender(actor.entityId))return parent;
            var detached=ReadEcd(Convert.ToBase64String(EcdBytes(packet.ecd)));bool fresh=detached.id==-1;detached.id=-1;string request=Convert.ToBase64String(EcdBytes(detached));OwedPhysicalTrace t=null;
            if(ReferenceEquals(owedBodyWorld,world)&&owedBodyDirectory==GameIO.GetSaveGameDir())t=owedBodyIntents.Values.FirstOrDefault(i=>i.Creation==request&&ValidOwedPhysicalOwner(actor,i,true));
            if(t==null){string path=Path.Combine(GameIO.GetSaveGameDir(),"rebirth","placed-original","owed-body-invocation-index",Hash(request)+".xml");if(File.Exists(path)&&new FileInfo(path).Length<=Maximum){var known=DecodeOwedPhysical(File.ReadAllText(path,new UTF8Encoding(false,true)));if(known.Creation==request&&ValidHistoricalOwedPhysicalOwner(actor,known,false)){classified=true;allow=false;return parent;}}return parent;}
            classified=true;allow=false;if(!fresh||!SaveOwedPhysical(t,true)||!BeginOwedBodyInvocation(t))return parent;
            var frame=new OwedRequestToken{Parent=parent,Packet=packet,Previous=owedIncoming,World=world,Directory=GameIO.GetSaveGameDir(),Sender=sender,Actor=actor,Trace=t};owedIncoming=frame;allow=true;return frame;
        }catch{if(classified)allow=false;return parent;}
    }
    private static bool ObserveOwedBodyAccepted(EntityBackpack body)
    {
        try
        {
            var f=owedIncoming;if(f!=null&&f.Packet?.ecd!=null&&f.Packet.ecd.id==body.entityId&&body.entityId>0&&ReferenceEquals(GameManager.Instance?.World,f.World)&&ReferenceEquals(f.World.GetEntity(body.entityId),body)&&f.Directory==GameIO.GetSaveGameDir()&&ReferenceEquals(f.Packet.Sender,f.Sender)&&ValidStoredOwedBody(f.World,f.Trace))return RebirthPlacedOriginalBodyPublication.AdmitOwed(f.World,f.Trace.Child.Operation,body,f.Trace.Creation);
            var p=physical;if(p!=null&&!p.Failed&&p.Context.Owed!=null&&ReferenceEquals(GameManager.Instance?.World,p.Context.World)&&p.Context.Directory==GameIO.GetSaveGameDir()&&!p.Context.World.IsRemote()&&p.Requested&&p.ConstructedCreation?.id==body.entityId&&body.entityId>0&&ReadOwedPhysical(p.Context.Owed.Operation,true,out var t))return RebirthPlacedOriginalBodyPublication.AdmitOwed(p.Context.World,t.Child.Operation,body,t.Creation);
        }catch{}return false;
    }
    private static bool ValidStoredOwedBody(World world,OwedPhysicalTrace t)
    {
        try
        {
            if(world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||world.worldState.Guid!=t.Child.World||!ReadChild(ChildPath(t.Child,true),out var original)||original.Phase!=1||!SameChild(original,t.Child)||!ReadOwedFatal(t.Child,out int entity,out string policy)||entity!=t.Observation.NativeOwnerEntity||policy!=t.Observation.Policy||!OwedSource(t.Child,out _,out var head,out _)||head.Sequence!=t.Child.Sequence-1||Hash(HeadBody(head))!=t.Child.Head)return false;
            string path=OwedInvocationPath(t,false);return File.Exists(path)&&new FileInfo(path).Length<=Maximum&&DecodeOwedPhysical(File.ReadAllText(path,new UTF8Encoding(false,true))).Image==t.Image;
        }catch{return false;}
    }
    internal sealed class OwedBodyDescriptor
    {internal Guid Operation;internal string World,Owner,Creation,Binding;internal int Deaths,NativeOwner;}
    internal static bool TryDescribeOwedBody(World world,Guid operation,string request,out OwedBodyDescriptor descriptor)
    {
        descriptor=null;try{if(world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||!ReadOwedPhysical(operation,true,out var t)||t.Creation!=request||t.Overflow||!ValidStoredOwedBody(world,t))return false;descriptor=new OwedBodyDescriptor{Operation=operation,World=t.Child.World,Owner=t.Child.Owner,Creation=t.Child.Creation,Binding=t.Digest,Deaths=t.Child.Deaths,NativeOwner=t.Observation.NativeOwnerEntity};return true;}catch{return false;}
    }
    private static void RetryOwedPhysical(EntityPlayerLocal player,OwedChild child)
    {try{if(OwedIdentity(player,child)&&player.Died>child.Deaths&&ReadOwedPhysical(child.Operation,false,out var t)&&SameChild(child,t.Child)){if(t.Creation.Length!=0&&!t.Overflow)QueueDeferredBody(player,true,t.Image);QueueNativeFinalTrace(player,true,t.Image);}}catch{}}
}