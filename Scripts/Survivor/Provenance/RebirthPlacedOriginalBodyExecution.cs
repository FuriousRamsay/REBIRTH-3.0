using System;
using System.IO;

internal static partial class RebirthPlacedOriginalDeathPartition
{
    private sealed class PreparedBodyExecution
    { internal NetPackageRequestToSpawnEntity Packet; internal EntityCreationData Creation; internal ClientInfo Sender; internal World World; internal string Directory; internal DeferredBody Original; }
    [ThreadStatic] private static PreparedBodyExecution preparedBodyExecution;
    internal static bool SamePreparedBodyObjects(object packet,object actualPacket,object creation,object actualCreation)=>packet!=null&&creation!=null&&ReferenceEquals(packet,actualPacket)&&ReferenceEquals(creation,actualCreation);

    // This predicate grants only purpose selection for this exact synchronous
    // native call. A different packet with identical bytes cannot inherit it.
    internal static bool IsExecutingPreparedBody(NetPackageRequestToSpawnEntity packet,World world)
    {
        try
        {
            var f=preparedBodyExecution;if(f==null||!ThreadManager.IsMainThread()||world?.IsRemote()!=false||!SamePreparedBodyObjects(f.Packet,packet,f.Creation,packet?.ecd)||!ReferenceEquals(f.World,world)||!ReferenceEquals(GameManager.Instance?.World,world)||f.Directory!=GameIO.GetSaveGameDir()||!ReferenceEquals(f.Sender,packet.Sender)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(f.Sender.entityId),f.Sender))return false;
            var actor=world.GetEntity(f.Sender.entityId) as EntityPlayer;if(!DeferredOwner(actor,f.Original)||!DeferredRead(DeferredPath(f.Original,true),out var saved)||saved.Phase!=1||!SameDeferred(saved,f.Original)||saved.Nonce!=f.Original.Nonce||saved.Permit!=f.Original.Permit)return false;
            var native=ReadEcd(Convert.ToBase64String(EcdBytes(packet.ecd)));native.id=-1;return Convert.ToBase64String(EcdBytes(native))==f.Original.Creation;
        }catch{return false;}
    }
    private static object EnterPreparedBody(NetPackageRequestToSpawnEntity packet,World world,out bool allow)
    {
        allow=false;if(!IsExecutingPreparedBody(packet,world)||packet.ecd.id!=-1)return null;var d=preparedBodyExecution.Original;var actor=world.GetEntity(packet.Sender.entityId) as EntityPlayer;
        if(d.Terminal)
        {
            var t=DecodeTerminalBody(d.Trace);if(!TerminalOwner(actor,t)||!BeginTerminalNativeInvocation(t))return null;
            var f=new TerminalRequestFrame{Previous=terminalIncoming,Packet=packet,World=world,Directory=GameIO.GetSaveGameDir(),Sender=packet.Sender,Body=t};terminalIncoming=f;allow=true;return f;
        }
        if(d.Owed)
        {
            var t=DecodeOwedPhysical(d.Trace);if(!ValidOwedPhysicalOwner(actor,t,true)||!SaveOwedPhysical(t,true)||!BeginOwedBodyInvocation(t))return null;
            var f=new OwedRequestToken{Previous=owedIncoming,Packet=packet,World=world,Directory=GameIO.GetSaveGameDir(),Sender=packet.Sender,Actor=actor,Trace=t};owedIncoming=f;allow=true;return f;
        }
        var intent=DecodeIntent(d.Trace);if(!RebirthPlacedOriginal.ValidatePartition(actor,intent.Observation.Original,intent.Observation.Policy,intent.Observation.NativeOwnerEntity)||!BeginBodyInvocation(intent))return null;
        var frame=new RequestFrame{Previous=incoming,Packet=packet,Sender=packet.Sender,Actor=actor,World=world,Directory=GameIO.GetSaveGameDir(),Original=intent.Observation.Original,Creation=d.Creation,NativeOwner=d.NativeOwner};incoming=frame;allow=true;return frame;
    }
    internal static void ReceiveBodyExecution(NetPackage request,World world,int entity,Guid operation,string trace,string nonce,string permit)
    {
        try
        {
            if(!ThreadManager.IsMainThread()||preparedBodyExecution!=null||world?.IsRemote()!=false||!ReferenceEquals(GameManager.Instance?.World,world)||request?.Sender==null||request.Sender.entityId!=entity||!request.ValidEntityIdForSender(entity)||!ReferenceEquals(SingletonMonoBehaviour<ConnectionManager>.Instance?.Clients.ForEntityId(entity),request.Sender))return;
            if(!DeferredRead(DeferredPath(new DeferredBody{Operation=operation},true),out var d)||d.Phase!=1||Hash(d.Trace)!=trace||d.Nonce!=nonce||d.Permit!=permit||!DeferredOwner(world.GetEntity(entity) as EntityPlayer,d))return;
            var ecd=ReadEcd(d.Creation);if(ecd.id!=-1)return;var packet=NetPackageManager.GetPackage<NetPackageRequestToSpawnEntity>().Setup(ecd);packet.Sender=request.Sender;
            preparedBodyExecution=new PreparedBodyExecution{Packet=packet,Creation=ecd,Sender=request.Sender,World=world,Directory=GameIO.GetSaveGameDir(),Original=d};
            try{packet.ProcessPackage(world,GameManager.Instance);}finally{preparedBodyExecution=null;}
        }catch{/* A durable attempted invocation is never replayed after uncertainty. */}
    }
}
[UnityEngine.Scripting.Preserve]
public sealed class NetPackageRebirthPlacedBodyExecute:NetPackage
{
    private int entity;private Guid operation;private string trace=string.Empty,nonce=string.Empty,permit=string.Empty;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthPlacedBodyExecute Setup(int owner,Guid original,string digest,string challenge,string permission){entity=owner;operation=original;trace=digest;nonce=challenge;permit=permission;return this;}
    public override void read(PooledBinaryReader reader){entity=reader.ReadInt32();var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();operation=new Guid(bytes);trace=reader.ReadString();nonce=reader.ReadString();permit=reader.ReadString();Validate();}
    private void Validate(){if(operation==Guid.Empty||trace.Length!=64||permit.Length!=64||!Guid.TryParseExact(nonce,"N",out var id)||id==Guid.Empty)throw new InvalidDataException();}
    public override void write(PooledBinaryWriter writer){base.write(writer);Validate();writer.Write(entity);writer.Write(operation.ToByteArray());writer.Write(trace);writer.Write(nonce);writer.Write(permit);}
    public override void ProcessPackage(World world,GameManager callbacks){RebirthPlacedOriginalDeathPartition.ReceiveBodyExecution(this,world,entity,operation,trace,nonce,permit);}
    public int GetLength()=>192;
}
