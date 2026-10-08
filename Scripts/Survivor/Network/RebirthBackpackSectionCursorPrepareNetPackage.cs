using System;
using System.IO;
using UnityEngine.Scripting;

// The bag wire flag selects the sell section; inventorySlot is always zero.
// Starts an immutable journal intent only. Owner application and saved-evidence settlement
// remain separate; this request never treats a client-provided stack as authoritative.
[Preserve]
public sealed class NetPackageRebirthBackpackSectionCursorPrepareRequest : NetPackage
{
    private int playerId, inventorySlot, librarySlot, quantity;
    private string creation;
    private long revision;
    private bool bag, deposit;
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    private bool IsValid()=>creation!=null&&revision>=0&&revision<long.MaxValue&&
        inventorySlot==0&&librarySlot>=0&&librarySlot<RebirthBackpackLibraryPolicy.MaxSlots&&
        quantity>0&&quantity<=ushort.MaxValue;
    public NetPackageRebirthBackpackSectionCursorPrepareRequest Setup(int entityId,Guid character,long gearRevision,
        bool fromBag,int ownerSlot,int storageSlot,int count,bool isDeposit)
        =>Setup(entityId,character.ToString("N"),gearRevision,fromBag,ownerSlot,storageSlot,count,isDeposit);
    public NetPackageRebirthBackpackSectionCursorPrepareRequest Setup(int entityId,string character,long gearRevision,
        bool fromBag,int ownerSlot,int storageSlot,int count,bool isDeposit)
    {
        playerId=entityId;RebirthSurvivorRequestScope.TryNormalize(character,out creation);revision=gearRevision;bag=fromBag;
        inventorySlot=ownerSlot;librarySlot=storageSlot;quantity=count;deposit=isDeposit;
        if(!IsValid())throw new ArgumentException("Invalid library preparation request.");
        return this;
    }
    public override void read(PooledBinaryReader reader)
    {
        creation=null;playerId=reader.ReadInt32();var character=RebirthBackpackLibraryCreationWire.Read(reader);revision=reader.ReadInt64();bag=reader.ReadBoolean();
        inventorySlot=reader.ReadInt32();librarySlot=reader.ReadInt32();quantity=reader.ReadInt32();deposit=reader.ReadBoolean();creation=character;
        if(!IsValid())throw new InvalidDataException("Invalid library preparation request.");
    }
    public override void write(PooledBinaryWriter writer)
    {
        if(!IsValid())throw new InvalidOperationException("Invalid library preparation request.");
        base.write(writer);var output=(BinaryWriter)writer;
        output.Write(playerId);RebirthBackpackLibraryCreationWire.Write(output,creation);output.Write(revision);output.Write(bag);
        output.Write(inventorySlot);output.Write(librarySlot);output.Write(quantity);output.Write(deposit);
    }
    public int GetLength()=>28+RebirthBackpackLibraryCreationWire.Length(creation);
    public override void ProcessPackage(World world,GameManager callbacks)
    {
        if(world==null||world.IsRemote()||Sender==null||!IsValid()||!ValidEntityIdForSender(playerId))return;
        var player=world.GetEntity(playerId) as EntityPlayer;
        try
        {
            // Refuse before preparing an intent when its offer cannot be delivered.
            NetPackageManager.GetPackageId(typeof(NetPackageRebirthBackpackLibraryOfferChunk));
            if(!RebirthBackpackLibraryServer.PrepareCursor(player,Sender,creation,revision,librarySlot,quantity,deposit,bag,out _))return;
            // Delivery reloads the same saved journal intent; failure leaves it recoverable.
            RebirthBackpackLibraryRecoveryDelivery.TrySend(player,Sender,creation);
        }
        catch(Exception error)
        {Log.Warning("[REBIRTH Library] Preparation request deferred: "+error.GetType().Name);}
    }
}