using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationGenerationRequest : NetPackage
{
    private int playerId,deaths;private Guid worldId;private bool ready;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthHydrationGenerationRequest Setup(RebirthHydrationGenerationClient.RequestScope scope)
    {
        ready=false;
        if(scope==null||!scope.Current())throw new InvalidOperationException("Current native hydration generation request scope required");
        playerId=scope.PlayerId;deaths=scope.Deaths;worldId=scope.WorldId;
        if(playerId<=0||deaths<0||worldId==Guid.Empty)throw new InvalidOperationException("Invalid hydration generation request identity");
        ready=true;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        ready=false;if(reader.ReadByte()!=1)throw new InvalidDataException("Unknown hydration generation request version");
        playerId=reader.ReadInt32();byte[] world=reader.ReadBytes(16);
        if(world.Length!=16)throw new InvalidDataException("Truncated hydration generation request world");
        worldId=new Guid(world);deaths=reader.ReadInt32();
        if(playerId<=0||deaths<0||worldId==Guid.Empty)throw new InvalidDataException("Invalid hydration generation request identity");
        ready=true;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(!ready)throw new InvalidOperationException("Missing hydration generation request");
        base.write(writer);var binary=(BinaryWriter)writer;binary.Write((byte)1);binary.Write(playerId);binary.Write(worldId.ToByteArray());binary.Write(deaths);
    }
    public int GetLength()=>27;
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(!ready||!ThreadManager.IsMainThread()||world==null||world.IsRemote()||manager?.IsServer!=true||
            !ReferenceEquals(world,GameManager.Instance?.World)||RebirthSurvivorMode.IsEnabledForCurrentWorld()||
            !Guid.TryParse(world.Guid,out var actualWorld)||actualWorld!=worldId||Sender==null||
            !Sender.loginDone||Sender.disconnecting||!ValidEntityIdForSender(playerId)||
            !ReferenceEquals(manager.Clients?.ForEntityId(playerId),Sender))return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        if(player==null||player is EntityPlayerLocal||player.Died!=deaths||player.IsDead()||!player.IsSpawned())return;
        RebirthHydrationGenerationServer.TrySend(player);
    }
}
