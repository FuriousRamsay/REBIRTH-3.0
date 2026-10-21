using Platform;
using System;
using System.IO;
using UnityEngine.Scripting;
#nullable disable

[Preserve]
public sealed class NetPackageRebirthNpcInteractionRequestV2:NetPackage
{
    private const ushort RecoveryCodecVersion=2;
    private ulong recoveryGeneration;
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

    public NetPackageRebirthNpcInteractionRequestV2 Setup(int player,PlatformUserIdentifierAbs user,uint epoch,ulong id,
        RebirthNpcInteractionNetworkOperation op,int npc,string session,RebirthNpcInteractionCommand command)
    {
        recoveryGeneration=RebirthNpcRecoveryProjectionScope.Current;
        protocolVersion=RecoveryCodecVersion; connectionEpoch=epoch; playerEntityId=player; userId=user;
        requestId=id; operation=op; npcEntityId=npc; sessionKey=session??string.Empty;
        commandRequestId=command!=null?command.RequestId??string.Empty:string.Empty;
        payload=command!=null?command.Payload??string.Empty:string.Empty;
        kind=command!=null?command.Kind:0; revision=command!=null?command.ExpectedRevision:0; return this;
    }

    public override void read(PooledBinaryReader r)
    {
        BinaryReader b=(BinaryReader)r;
        protocolVersion=b.ReadUInt16(); recoveryGeneration=b.ReadUInt64(); connectionEpoch=b.ReadUInt32(); playerEntityId=b.ReadInt32();
        userId=PlatformUserIdentifierAbs.FromStream(b); requestId=b.ReadUInt64(); operation=(RebirthNpcInteractionNetworkOperation)b.ReadByte();
        npcEntityId=b.ReadInt32(); sessionKey=RebirthNpcNetworkFraming.ReadString(b,RebirthNpcNetworkFraming.MaxId);
        commandRequestId=RebirthNpcNetworkFraming.ReadString(b,RebirthNpcNetworkFraming.MaxId);
        kind=(RebirthNpcInteractionCommandKind)b.ReadByte(); payload=RebirthNpcNetworkFraming.ReadString(b,RebirthNpcNetworkFraming.MaxPayload);
        revision=b.ReadUInt32();
    }

    public override void write(PooledBinaryWriter w)
    {
        base.write(w); BinaryWriter b=(BinaryWriter)w;
        b.Write(protocolVersion); b.Write(recoveryGeneration); b.Write(connectionEpoch); b.Write(playerEntityId); userId.ToStream(b); b.Write(requestId);
        b.Write((byte)operation); b.Write(npcEntityId); RebirthNpcNetworkFraming.WriteString(b,sessionKey,RebirthNpcNetworkFraming.MaxId);
        RebirthNpcNetworkFraming.WriteString(b,commandRequestId,RebirthNpcNetworkFraming.MaxId); b.Write((byte)kind);
        RebirthNpcNetworkFraming.WriteString(b,payload,RebirthNpcNetworkFraming.MaxPayload); b.Write(revision);
    }

    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||userId==null||!ValidEntityIdForSender(playerEntityId)||!ValidUserIdForSender(userId))return;
        if((protocolVersion!=RecoveryCodecVersion||recoveryGeneration!=RebirthNpcRecoveryGeneration.RequireCurrent())||!RebirthNpcNetworkEpoch.ValidateConnectionEpoch(playerEntityId,connectionEpoch))return;
        if(!RebirthNpcRemoteCommandService.AcceptNonce(userId,connectionEpoch,requestId))return;
        if(operation==RebirthNpcInteractionNetworkOperation.Command&&RebirthNpcTradeService.TryRemoteOwnerCommand(this,userId,playerEntityId,connectionEpoch,sessionKey,
            new RebirthNpcInteractionCommand{RequestId=commandRequestId,SessionKey=sessionKey,Kind=kind,Payload=payload,ExpectedRevision=revision},out var nativeTradeResponse))
        {Reply(playerEntityId,connectionEpoch,requestId,null,nativeTradeResponse,string.Empty,false);return;}
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
        if(c!=null&&c.IsServer)c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthNpcInteractionResponseV2>()
            .Setup(RebirthNpcRecoveryGeneration.RequireCurrent(),RebirthNpcNetworkEpoch.GetServerEpoch(),epoch,id,projection,response,error),_attachedToEntityId:player);
    }
    public int GetLength()=>0;
}

