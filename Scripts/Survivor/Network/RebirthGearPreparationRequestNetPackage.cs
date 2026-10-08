using System;
using System.IO;
using System.Text;
using UnityEngine.Scripting;

// Original native saved owner marker only. Receipt upload may arrive later;
// a refused/uncertain request leaves the original hold and marker intact.
[Preserve]
public sealed class NetPackageRebirthGearPreparationRequest : NetPackage
{
    private static readonly RebirthGearPreparationRequestThrottle Throttle=new RebirthGearPreparationRequestThrottle();
    private int playerId;
    private string marker;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    public NetPackageRebirthGearPreparationRequest Setup(int id,string originalMarker)
    {
        if(id<=0||!RebirthGearPreparationMarker.TryRead(originalMarker,1f,out _,out _,out _))
            throw new ArgumentException("Invalid original gear preparation request.");
        playerId=id;marker=originalMarker;return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        marker=null;playerId=reader.ReadInt32();int length=reader.ReadUInt16();
        if(playerId<=0||length<1||length>RebirthGearPreparationMarker.MaximumKeyLength)
            throw new InvalidDataException("Invalid original gear preparation length.");
        var bytes=reader.ReadBytes(length);
        if(bytes.Length!=length)throw new InvalidDataException("Truncated original gear preparation request.");
        for(int i=0;i<bytes.Length;i++)if(bytes[i]>127)throw new InvalidDataException("Noncanonical original gear preparation request.");
        string value=Encoding.ASCII.GetString(bytes);
        if(!RebirthGearPreparationMarker.TryRead(value,1f,out _,out _,out _))
            throw new InvalidDataException("Invalid original gear preparation marker.");
        marker=value;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0||!RebirthGearPreparationMarker.TryRead(marker,1f,out _,out _,out _))
            throw new InvalidOperationException("Missing original gear preparation request.");
        byte[] bytes=Encoding.ASCII.GetBytes(marker);
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write((ushort)bytes.Length);((BinaryWriter)writer).Write(bytes);
    }
    public int GetLength()=>8+(marker?.Length??0);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(marker==null||world==null||world.IsRemote()||!ThreadManager.IsMainThread()||
            !ReferenceEquals(GameManager.Instance?.World,world)||Sender==null||!ValidEntityIdForSender(playerId))return;
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(manager==null||!manager.IsServer)return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        if(player==null||manager.Clients==null||!ReferenceEquals(manager.Clients.ForEntityId(playerId),Sender)||
            !Throttle.TryAdmit(world,manager,playerId,System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency))return;
        try
        {
            // Resolve response mapping before preparation has any saved effects.
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearOfferFragment));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearBoundSettled));
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthGearPreparationRefusal));
            if(RebirthRemoteGearAppliedConfirmation.TryGetBoundSettlement(player,Sender,marker,out var terminal))
            {
                if(RebirthRemoteGearAppliedConfirmation.TryGetBoundTerminalOriginal(player,Sender,marker,out _,out _)&&
                    !RebirthGearOfferServer.TrySendTerminalOriginal(player,Sender,marker,terminal))return;
                if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer||
                    !ReferenceEquals(GameManager.Instance?.World,world)||manager.Clients==null||!ReferenceEquals(manager.Clients.ForEntityId(playerId),Sender))return;
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthGearBoundSettled>().Setup(playerId,terminal),
                    _attachedToEntityId:playerId);
                return;
            }
            RebirthGearPreparationRefusal refusal;
            bool acknowledged=RebirthRemoteGearRefusalRetirement.TryConfirm(player,Sender,marker,out refusal);
            if(acknowledged||RebirthRemoteGearPreparationRefusal.TryRecordStale(player,Sender,marker,out refusal))
            {
                if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer||
                    !ReferenceEquals(GameManager.Instance?.World,world)||manager.Clients==null||
                    !ReferenceEquals(manager.Clients.ForEntityId(playerId),Sender))return;
                manager.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthGearPreparationRefusal>().Setup(playerId,refusal,acknowledged),
                    _attachedToEntityId:playerId);
                return;
            }
            // A staged refusal with an uncertain save remains the SAME pending original.
            if(!RebirthGearPreparationMarker.TryRead(marker,1f,out _,out _,out var originalIntent)||
                !RebirthRemoteGearInventorySource.TryResolve(player,Sender,originalIntent.CreationId,out var currentRecord)||
                currentRecord.Support.PendingGearPreparationRefusal!=null)return;
            if(!RebirthRemoteGearPreparation.TryReplayBoundRetained(player,Sender,marker,out _,out _)&&
                !RebirthRemoteGearPreparation.TryPrepareBound(player,Sender,marker,out _))return;
            if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!manager.IsServer||
                !ReferenceEquals(GameManager.Instance?.World,world)||manager.Clients==null||!ReferenceEquals(manager.Clients.ForEntityId(playerId),Sender))return;
            RebirthGearOfferServer.TrySendRetainedBound(player,Sender,marker);
        }
        catch { } // No replacement ID, rollback or custody release on uncertainty.
    }
}