using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthConsumptionAcknowledgement : NetPackage
{
    private int owner;
    private string creation;
    private Guid publication;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthConsumptionAcknowledgement Setup(int player,string character,Guid id)
    {
        if(id==Guid.Empty||!RebirthSurvivorRequestScope.TryNormalize(character,out creation))
            throw new ArgumentException("Invalid consumption acknowledgement identity.");
        owner=player;publication=id;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        creation=null;publication=Guid.Empty;owner=((BinaryReader)reader).ReadInt32();
        string character=RebirthBackpackLibraryCreationWire.Read((BinaryReader)reader);
        byte[] bytes=((BinaryReader)reader).ReadBytes(16);
        if(bytes.Length!=16)throw new InvalidDataException("Truncated consumption acknowledgement.");
        var id=new Guid(bytes);
        if(id==Guid.Empty||!RebirthSurvivorRequestScope.TryNormalize(character,out creation))
            throw new InvalidDataException("Invalid consumption acknowledgement.");
        publication=id;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(creation==null||publication==Guid.Empty)throw new InvalidOperationException("Missing consumption acknowledgement.");
        base.write(writer);((BinaryWriter)writer).Write(owner);
        RebirthBackpackLibraryCreationWire.Write((BinaryWriter)writer,creation);
        ((BinaryWriter)writer).Write(publication.ToByteArray());
    }
    public int GetLength()=>22+RebirthBackpackLibraryCreationWire.Length(creation);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(!RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled||!ThreadManager.IsMainThread()
            ||world==null||world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)
            ||Sender==null||publication==Guid.Empty||creation==null)return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager==null||!manager.IsServer||manager.Clients==null||Sender.entityId!=owner
            ||!ReferenceEquals(manager.Clients.ForEntityId(owner),Sender)||!ValidEntityIdForSender(owner))return;
        var player=world.GetEntity(owner) as EntityPlayer;
        if(player==null||!ReferenceEquals(player.world,world))return;
        RebirthConsumptionPublicationRetry.AcknowledgeAuthenticated(world,player,publication,creation);
    }
}

internal static class RebirthConsumptionAcknowledgement
{
    internal static bool TrySend(World world,NetPackageRebirthMetabolismConsumeResult packet,string creation)
    {
        int owner;Guid id;string normalized;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        var player=world?.GetPrimaryPlayer();
        if(!RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled||!ThreadManager.IsMainThread()
            ||world==null||!world.IsRemote()||!ReferenceEquals(GameManager.Instance?.World,world)
            ||manager==null||manager.IsServer||player==null||packet==null
            ||!packet.TryGetPublicationIdentity(out owner,out id)||player.entityId!=owner
            ||!RebirthSurvivorRequestScope.TryNormalize(creation,out normalized))return false;
        // Validate identity/package availability before any source application.
        var acknowledgement=NetPackageManager.GetPackage<NetPackageRebirthConsumptionAcknowledgement>().Setup(owner,normalized,id);
        if(!RebirthConsumptionPublicationRetry.ApplyForAcknowledgement(world,packet,creation))return false;
        if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)
            ||!ReferenceEquals(GameManager.Instance?.World,world)||manager.IsServer)return false;
        manager.SendToServer(acknowledgement);
        return true;
    }
}
