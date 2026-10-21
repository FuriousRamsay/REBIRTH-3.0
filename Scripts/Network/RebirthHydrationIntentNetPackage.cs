using System;
using System.IO;
using UnityEngine.Scripting;

[Preserve]
public sealed class NetPackageRebirthHydrationIntent : NetPackage
{
    private int playerId;private RebirthHydrationIntentFrame frame;private byte[] body;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthHydrationIntent Setup(RebirthHydrationProposalClient.Intent intent)
    {
        if(intent==null||!intent.Current()||!RebirthHydrationIntentFrame.TryCreate(intent.Operation,intent.Creation,intent.Deaths,intent.SourceIsBag,intent.SourceIndex,intent.Returning,intent.LockBitmap,out var value))throw new InvalidOperationException("Current hydration intent required");
        playerId=intent.Player.entityId;frame=value;body=value.Encode();return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        frame=null;body=null;playerId=reader.ReadInt32();int length=reader.ReadUInt16();
        if(playerId<=0||length<1||length>RebirthHydrationIntentFrame.MaximumBytes)throw new InvalidDataException("Invalid hydration request size");
        var bytes=reader.ReadBytes(length);if(bytes.Length!=length||!RebirthHydrationIntentFrame.TryDecode(bytes,out var value))throw new InvalidDataException("Invalid hydration request");
        body=bytes;frame=value;
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(playerId<=0||frame==null||body==null)throw new InvalidOperationException("Missing hydration intent");
        base.write(writer);((BinaryWriter)writer).Write(playerId);((BinaryWriter)writer).Write((ushort)body.Length);((BinaryWriter)writer).Write(body);
    }
    public int GetLength()=>8+(body?.Length??0);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        var manager=SingletonMonoBehaviour<ConnectionManager>.Instance;
        if(frame==null||!ThreadManager.IsMainThread()||world==null||world.IsRemote()||!ReferenceEquals(world,GameManager.Instance?.World)||manager?.IsServer!=true||
            Sender==null||!ValidEntityIdForSender(playerId)||!ReferenceEquals(manager.Clients?.ForEntityId(playerId),Sender))return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        if(player==null||player.Died!=frame.Deaths||!RebirthHydrationTransferScope.TryObserve(player,out var generation)||
            !RebirthSurvivorRequestScope.Matches(frame.Creation,generation))return;
        try
        {
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthHydrationProposalFragment));
            if(!ReferenceEquals(manager,SingletonMonoBehaviour<ConnectionManager>.Instance)||!ReferenceEquals(world,GameManager.Instance?.World)||
                !ReferenceEquals(manager.Clients?.ForEntityId(playerId),Sender)||player.Died!=frame.Deaths||
                !RebirthHydrationTransferScope.TryObserve(player,out generation)||!RebirthSurvivorRequestScope.Matches(frame.Creation,generation))return;
            var locks=new bool[frame.Locks.Length];for(int i=0;i<locks.Length;i++)locks[i]=frame.Locks[i]=='1';
            if(RebirthHydrationProposalServer.Proposal.TryPrepare(player,Sender,frame.Operation,frame.Bag,frame.Index,frame.Returning,locks,out var proposal))RebirthHydrationProposalSender.TrySend(proposal);
        }
        catch{ } // Original custody survives missing mappings/unknown publication.
    }
}
