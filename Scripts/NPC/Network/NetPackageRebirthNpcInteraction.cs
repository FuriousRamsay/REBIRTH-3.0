using Platform;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Scripting;

#nullable disable

public enum RebirthNpcInteractionNetworkOperation : byte { Open=0, Command=1, Refresh=2, Close=3 }

public static class RebirthNpcInteractionNetworkClient
{
    private sealed class PendingRequest
    {
        public Action<RebirthNpcInteractionProjection,RebirthNpcInteractionCommandResponse,string> Callback;
        public long DeadlineUtcTicks;
        public uint ConnectionEpoch;
    }

    private const int MaxPending = 128;
    private static readonly long TimeoutTicks = TimeSpan.FromSeconds(10).Ticks;
    private static readonly object Sync = new object();
    private static readonly Dictionary<ulong,PendingRequest> Pending = new Dictionary<ulong,PendingRequest>();

    public static void RequestOpen(int npcEntityId, Action<RebirthNpcInteractionProjection,RebirthNpcInteractionCommandResponse,string> callback)
    { Send(RebirthNpcInteractionNetworkOperation.Open,npcEntityId,null,null,callback); }
    public static void RequestCommand(RebirthNpcInteractionCommand command, Action<RebirthNpcInteractionProjection,RebirthNpcInteractionCommandResponse,string> callback)
    { Send(RebirthNpcInteractionNetworkOperation.Command,-1,command!=null?command.SessionKey:null,command,callback); }
    public static void RequestRefresh(string sessionKey,Action<RebirthNpcInteractionProjection,RebirthNpcInteractionCommandResponse,string> callback)
    { Send(RebirthNpcInteractionNetworkOperation.Refresh,-1,sessionKey,null,callback); }
    public static void RequestClose(string sessionKey)
    { Send(RebirthNpcInteractionNetworkOperation.Close,-1,sessionKey,null,null); }

    private static void Send(RebirthNpcInteractionNetworkOperation op,int npcEntityId,string session,
        RebirthNpcInteractionCommand command,Action<RebirthNpcInteractionProjection,RebirthNpcInteractionCommandResponse,string> callback)
    {
        SweepExpired();
        EntityPlayerLocal player=GameManager.Instance!=null&&GameManager.Instance.World!=null?GameManager.Instance.World.GetPrimaryPlayer():null;
        PersistentPlayerData pp=GameManager.Instance!=null?GameManager.Instance.GetPersistentLocalPlayer():null;
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(player==null||pp==null||pp.PrimaryId==null||c==null)
        { if(callback!=null)callback(null,null,"Local player identity is unavailable."); return; }

        ulong id=RebirthNpcRemoteCommandService.NextClientNonce();
        uint connectionEpoch = c.IsServer ? 0u : RebirthNpcNetworkEpoch.ClientConnectionEpoch;
        if(!c.IsServer && connectionEpoch==0)
        { if(callback!=null)callback(null,null,"NPC network session is not established."); return; }

        if(callback!=null)
        {
            lock(Sync)
            {
                if(Pending.Count>=MaxPending)
                { callback(null,null,"Too many NPC interaction requests are awaiting replies."); return; }
                Pending[id]=new PendingRequest{Callback=callback,DeadlineUtcTicks=DateTime.UtcNow.Ticks+TimeoutTicks,ConnectionEpoch=connectionEpoch};
            }
        }

        if(c.IsServer)
        {
            NetPackageRebirthNpcInteractionRequest.ProcessLocal(player.entityId,pp.PrimaryId,id,op,npcEntityId,session,command);
            return;
        }
        c.SendToServer(NetPackageManager.GetPackage<NetPackageRebirthNpcInteractionRequest>()
            .Setup(player.entityId,pp.PrimaryId,connectionEpoch,id,op,npcEntityId,session,command));
    }

    internal static void Receive(uint connectionEpoch,ulong id,RebirthNpcInteractionProjection projection,
        RebirthNpcInteractionCommandResponse response,string error)
    {
        PendingRequest pending=null;
        lock(Sync)
        {
            if(Pending.TryGetValue(id,out pending))
            {
                if(pending.ConnectionEpoch!=connectionEpoch) return;
                Pending.Remove(id);
            }
        }
        if(pending!=null&&pending.Callback!=null)
            pending.Callback(projection,response,error??string.Empty);
    }

    internal static void ReceiveLocal(ulong id,RebirthNpcInteractionProjection projection,
        RebirthNpcInteractionCommandResponse response,string error)
    {
        PendingRequest pending=null;
        lock(Sync){if(Pending.TryGetValue(id,out pending))Pending.Remove(id);}
        if(pending!=null&&pending.Callback!=null) pending.Callback(projection,response,error??string.Empty);
    }

    public static void Update() { SweepExpired(); }

    private static void SweepExpired()
    {
        List<PendingRequest> expired=null;
        long now=DateTime.UtcNow.Ticks;
        lock(Sync)
        {
            List<ulong> remove=null;
            foreach(KeyValuePair<ulong,PendingRequest> pair in Pending)
            {
                if(pair.Value.DeadlineUtcTicks>now)continue;
                if(remove==null)remove=new List<ulong>();
                if(expired==null)expired=new List<PendingRequest>();
                remove.Add(pair.Key); expired.Add(pair.Value);
            }
            if(remove!=null)for(int i=0;i<remove.Count;i++)Pending.Remove(remove[i]);
        }
        if(expired!=null)for(int i=0;i<expired.Count;i++)
            if(expired[i].Callback!=null)expired[i].Callback(null,null,"NPC interaction request timed out.");
    }

    public static void Reset(){lock(Sync)Pending.Clear();}
}

[Preserve]
public sealed class NetPackageRebirthNpcInteractionRequest:NetPackage
{
    private ushort protocolVersion;
    private uint connectionEpoch;
    private int playerEntityId,npcEntityId;
    private PlatformUserIdentifierAbs userId;
    private ulong requestId;
    private RebirthNpcInteractionNetworkOperation operation;
    private string sessionKey,commandRequestId,payload;
    private RebirthNpcInteractionCommandKind kind;
    private uint revision;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;

    public NetPackageRebirthNpcInteractionRequest Setup(int player,PlatformUserIdentifierAbs user,uint epoch,ulong id,
        RebirthNpcInteractionNetworkOperation op,int npc,string session,RebirthNpcInteractionCommand command)
    {
        protocolVersion=RebirthNpcNetworkProtocol.CurrentVersion; connectionEpoch=epoch; playerEntityId=player; userId=user;
        requestId=id; operation=op; npcEntityId=npc; sessionKey=session??string.Empty;
        commandRequestId=command!=null?command.RequestId??string.Empty:string.Empty;
        payload=command!=null?command.Payload??string.Empty:string.Empty;
        kind=command!=null?command.Kind:0; revision=command!=null?command.ExpectedRevision:0; return this;
    }

    public override void read(PooledBinaryReader r)
    {
        BinaryReader b=(BinaryReader)r;
        protocolVersion=b.ReadUInt16(); connectionEpoch=b.ReadUInt32(); playerEntityId=b.ReadInt32();
        userId=PlatformUserIdentifierAbs.FromStream(b); requestId=b.ReadUInt64(); operation=(RebirthNpcInteractionNetworkOperation)b.ReadByte();
        npcEntityId=b.ReadInt32(); sessionKey=RebirthNpcNetworkFraming.ReadString(b,RebirthNpcNetworkFraming.MaxId);
        commandRequestId=RebirthNpcNetworkFraming.ReadString(b,RebirthNpcNetworkFraming.MaxId);
        kind=(RebirthNpcInteractionCommandKind)b.ReadByte(); payload=RebirthNpcNetworkFraming.ReadString(b,RebirthNpcNetworkFraming.MaxPayload);
        revision=b.ReadUInt32();
    }

    public override void write(PooledBinaryWriter w)
    {
        base.write(w); BinaryWriter b=(BinaryWriter)w;
        b.Write(protocolVersion); b.Write(connectionEpoch); b.Write(playerEntityId); userId.ToStream(b); b.Write(requestId);
        b.Write((byte)operation); b.Write(npcEntityId); RebirthNpcNetworkFraming.WriteString(b,sessionKey,RebirthNpcNetworkFraming.MaxId);
        RebirthNpcNetworkFraming.WriteString(b,commandRequestId,RebirthNpcNetworkFraming.MaxId); b.Write((byte)kind);
        RebirthNpcNetworkFraming.WriteString(b,payload,RebirthNpcNetworkFraming.MaxPayload); b.Write(revision);
    }

    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||userId==null||!ValidEntityIdForSender(playerEntityId)||!ValidUserIdForSender(userId))return;
        if(!RebirthNpcNetworkProtocol.IsSupported(protocolVersion)||!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(playerEntityId,connectionEpoch))return;
        if(!RebirthNpcRemoteCommandService.AcceptNonce(userId,connectionEpoch,requestId))return;
        Process(world,playerEntityId,userId,connectionEpoch,requestId,operation,npcEntityId,sessionKey,
            new RebirthNpcInteractionCommand{RequestId=commandRequestId,SessionKey=sessionKey,Kind=kind,Payload=payload,ExpectedRevision=revision},false);
    }

    internal static void ProcessLocal(int player,PlatformUserIdentifierAbs user,ulong id,RebirthNpcInteractionNetworkOperation op,
        int npc,string session,RebirthNpcInteractionCommand command)
    { Process(GameManager.Instance.World,player,user,0u,id,op,npc,session,command,true); }

    private static void Process(World world,int player,PlatformUserIdentifierAbs user,uint epoch,ulong id,
        RebirthNpcInteractionNetworkOperation op,int npcEntity,string session,RebirthNpcInteractionCommand command,bool local)
    {
        EntityPlayer actor=world.GetEntity(player) as EntityPlayer;
        if(actor==null){Reply(player,epoch,id,null,null,"Player is unavailable.",local);return;}
        string actorId=user.ToString(); RebirthNpcPlayerInventoryEndpointService.Ensure(actor,actorId);
        RebirthNpcInteractionProjection projection=null; RebirthNpcInteractionCommandResponse response=null; string error=string.Empty;
        if(op==RebirthNpcInteractionNetworkOperation.Open)
        {
            EntityRebirthNPC npc=world.GetEntity(npcEntity) as EntityRebirthNPC;
            if(npc==null||npc.RebirthRuntimeState==null)error="NPC is unavailable.";
            else { RebirthNpcInteractionResult result=RebirthNpcInteractionPresentationService.Open(actorId,npc.RebirthRuntimeState.StableId,out projection); if(result!=RebirthNpcInteractionResult.Allowed)error="Interaction rejected: "+result; }
        }
        else if(op==RebirthNpcInteractionNetworkOperation.Command)
        { response=RebirthNpcInteractionCommandRouter.Dispatch(command,actorId); if(response!=null&&response.Status==RebirthNpcInteractionCommandStatus.Accepted)RebirthNpcInteractionPresentationService.TryRefresh(session,actorId,out projection); }
        else if(op==RebirthNpcInteractionNetworkOperation.Refresh)
        { if(!RebirthNpcInteractionPresentationService.TryRefresh(session,actorId,out projection))error="Interaction session expired or is unavailable."; }
        else if(op==RebirthNpcInteractionNetworkOperation.Close)RebirthNpcInteractionSessionService.Close(session,actorId);
        Reply(player,epoch,id,projection,response,error,local);
    }

    private static void Reply(int player,uint epoch,ulong id,RebirthNpcInteractionProjection projection,
        RebirthNpcInteractionCommandResponse response,string error,bool local)
    {
        if(local){RebirthNpcInteractionNetworkClient.ReceiveLocal(id,projection,response,error);return;}
        ConnectionManager c=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(c!=null&&c.IsServer)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcInteractionResponse>()
            .Setup(RebirthNpcNetworkEpoch.GetServerEpoch(),epoch,id,projection,response,error),_attachedToEntityId:player);
    }
    public int GetLength()=>0;
}

[Preserve]
public sealed class NetPackageRebirthNpcInteractionResponse:NetPackage
{
    private uint worldEpoch,connectionEpoch;
    private ulong requestId;
    private RebirthNpcInteractionProjection projection;
    private RebirthNpcInteractionCommandResponse response;
    private string error;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;

    public NetPackageRebirthNpcInteractionResponse Setup(uint world,uint connection,ulong id,
        RebirthNpcInteractionProjection p,RebirthNpcInteractionCommandResponse r,string e)
    {worldEpoch=world;connectionEpoch=connection;requestId=id;projection=p;response=r;error=RebirthNpcNetworkProtocol.BoundDetail(e);return this;}

    public override void write(PooledBinaryWriter w)
    {
        base.write(w); BinaryWriter b=(BinaryWriter)w; b.Write(worldEpoch); b.Write(connectionEpoch); b.Write(requestId);
        RebirthNpcNetworkFraming.WriteString(b,error,RebirthNpcNetworkProtocol.MaxDetailLength); WriteProjection(b,projection); WriteResponse(b,response);
    }
    public override void read(PooledBinaryReader r)
    {
        worldEpoch=r.ReadUInt32(); connectionEpoch=r.ReadUInt32(); requestId=r.ReadUInt64();
        error=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkProtocol.MaxDetailLength); projection=ReadProjection(r); response=ReadResponse(r);
    }
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer)return;
        if(!RebirthNpcNetworkEpoch.AcceptClientEpoch(worldEpoch)||connectionEpoch!=RebirthNpcNetworkEpoch.ClientConnectionEpoch)return;
        RebirthNpcInteractionNetworkClient.Receive(connectionEpoch,requestId,projection,response,error);
    }
    public int GetLength()=>0;

    private static void WriteProjection(BinaryWriter w,RebirthNpcInteractionProjection p)
    {
        w.Write(p!=null); if(p==null)return;
        RebirthNpcNetworkFraming.WriteString(w,p.SessionKey,RebirthNpcNetworkFraming.MaxId);
        RebirthNpcNetworkFraming.WriteString(w,p.ActorId,RebirthNpcNetworkFraming.MaxId);
        w.Write(p.NpcId.High);w.Write(p.NpcId.Low);
        RebirthNpcNetworkFraming.WriteString(w,p.DisplayName,RebirthNpcNetworkFraming.MaxLabel);
        RebirthNpcNetworkFraming.WriteString(w,p.ProfileId,RebirthNpcNetworkFraming.MaxId);
        RebirthNpcNetworkFraming.WriteString(w,p.StatusText,RebirthNpcNetworkFraming.MaxLabel);
        w.Write(p.RuntimeRevision);w.Write(p.ExpiresClockTicks);w.Write((uint)p.Permissions);
        int n=p.Entries!=null?Math.Min(p.Entries.Length,RebirthNpcNetworkFraming.MaxInteractionEntries):0; w.Write((byte)n);
        for(int i=0;i<n;i++)
        {
            RebirthNpcInteractionMenuEntry e=p.Entries[i]??new RebirthNpcInteractionMenuEntry();
            w.Write((byte)e.Kind); RebirthNpcNetworkFraming.WriteString(w,e.Id,RebirthNpcNetworkFraming.MaxId);
            RebirthNpcNetworkFraming.WriteString(w,e.Label,RebirthNpcNetworkFraming.MaxLabel); w.Write(e.Enabled);
            RebirthNpcNetworkFraming.WriteString(w,e.DisabledReason,RebirthNpcNetworkFraming.MaxLabel);
        }
    }

    private static RebirthNpcInteractionProjection ReadProjection(PooledBinaryReader r)
    {
        if(!r.ReadBoolean())return null;
        RebirthNpcInteractionProjection p=new RebirthNpcInteractionProjection
        {
            SessionKey=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxId),
            ActorId=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxId),
            NpcId=new RebirthNpcStableId(r.ReadUInt64(),r.ReadUInt64()),
            DisplayName=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxLabel),
            ProfileId=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxId),
            StatusText=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxLabel),
            RuntimeRevision=r.ReadUInt32(),ExpiresClockTicks=r.ReadInt64(),Permissions=(RebirthNpcInteractionPermissions)r.ReadUInt32()
        };
        int n=RebirthNpcNetworkFraming.ReadByteCount(r,RebirthNpcNetworkFraming.MaxInteractionEntries,"interaction entries");
        List<RebirthNpcInteractionMenuEntry> entries=new List<RebirthNpcInteractionMenuEntry>(n);
        for(int i=0;i<n;i++)entries.Add(new RebirthNpcInteractionMenuEntry
        {
            Kind=(RebirthNpcInteractionCommandKind)r.ReadByte(),
            Id=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxId),
            Label=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxLabel),Enabled=r.ReadBoolean(),
            DisabledReason=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxLabel)
        });
        p.Entries=entries.ToArray();return p;
    }

    private static void WriteResponse(BinaryWriter w,RebirthNpcInteractionCommandResponse r)
    {
        w.Write(r!=null);if(r==null)return;w.Write((byte)r.Status);w.Write((byte)r.Authorization);
        RebirthNpcNetworkFraming.WriteString(w,r.Detail,RebirthNpcNetworkProtocol.MaxDetailLength);
        RebirthNpcNetworkFraming.WriteString(w,r.ResponsePayload,RebirthNpcNetworkFraming.MaxPayload);
        w.Write(r.NpcId.High);w.Write(r.NpcId.Low);RebirthNpcNetworkFraming.WriteString(w,r.ActorId,RebirthNpcNetworkFraming.MaxId);
    }
    private static RebirthNpcInteractionCommandResponse ReadResponse(PooledBinaryReader r)
    {
        if(!r.ReadBoolean())return null;
        return new RebirthNpcInteractionCommandResponse
        {
            Status=(RebirthNpcInteractionCommandStatus)r.ReadByte(),Authorization=(RebirthNpcInteractionResult)r.ReadByte(),
            Detail=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkProtocol.MaxDetailLength),
            ResponsePayload=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxPayload),
            NpcId=new RebirthNpcStableId(r.ReadUInt64(),r.ReadUInt64()),ActorId=RebirthNpcNetworkFraming.ReadString(r,RebirthNpcNetworkFraming.MaxId)
        };
    }
}
