using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

#nullable disable

[Preserve]
public sealed class NetPackageRebirthMetabolismConsumeRequest : NetPackage
{
    private int playerId;
    private int itemType;
    private ushort seed;
    private bool autoSip;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToServer; } }

    public NetPackageRebirthMetabolismConsumeRequest Setup(int entityId, ItemValue itemValue, bool isAutoSip)
    {
        playerId = entityId;
        itemType = itemValue != null ? itemValue.type : 0;
        seed = itemValue != null ? itemValue.Seed : (ushort)0;
        autoSip = isAutoSip;
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        playerId = binary.ReadInt32();
        itemType = binary.ReadInt32();
        seed = binary.ReadUInt16();
        autoSip = binary.ReadBoolean();
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        // Bind through BinaryWriter. PooledBinaryWriter exposes ReadOnlySpan overloads in
        // current 3.x that are incompatible with RebirthUtils' legacy compile surface (CS7069).
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(playerId);
        binary.Write(itemType);
        binary.Write(seed);
        binary.Write(autoSip);
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if (world == null || world.IsRemote() || !ValidEntityIdForSender(playerId))
            return;
        EntityPlayer player = world.GetEntity(playerId) as EntityPlayer;
        if (player == null)
            return;
        RebirthMetabolismSourceKind sourceKind;
        int sourceSlot; ItemStack source;
        bool found=RebirthMetabolismService.TryFindMatchingStack(player,itemType,seed,out sourceKind,out sourceSlot,out source);
        ItemStack before=found?source.Clone():null;
        RebirthConsumeResult result = RebirthMetabolismService.ConsumeMatchingInventoryItem(player, itemType, seed, autoSip);
        ItemStack after=found?(sourceKind==RebirthMetabolismSourceKind.Backpack
            ?player.bag.ItemGrid.items[sourceSlot]:player.inventory.ItemGrid.items[sourceSlot]).Clone():null;
        ConnectionManager c = SingletonMonoBehaviour<ConnectionManager>.Instance;
        if (c != null && c.IsServer)
            c.SendPackage(NetPackageManager.GetPackage<NetPackageRebirthMetabolismConsumeResult>()
                .Setup(result).WithInventory(playerId,sourceKind,sourceSlot,before,after), _attachedToEntityId: playerId);
    }

    public int GetLength() { return 16; }
}

[Preserve]
public sealed class NetPackageRebirthMetabolismConsumeResult : NetPackage
{
    private RebirthConsumeResult result;
    private Guid publication;
    private int ownerId,sourceSlot;
    private RebirthMetabolismSourceKind sourceKind;
    private ItemStack before,after;
    private string beforeItem,afterItem;

    public override NetPackageDirection PackageDirection { get { return NetPackageDirection.ToClient; } }

    // Dormant retry component access; existing receive/send paths do not invoke it.
    internal bool TryGetPublicationIdentity(out int owner,out Guid id)
    { owner=ownerId;id=publication;return publication!=Guid.Empty&&before!=null&&after!=null; }
    internal bool ApplyPublicationForAcknowledgement(World world)
        =>RebirthConsumptionOwnerPublication.WasApplied(world,ownerId,publication)
            ||RebirthConsumptionOwnerPublication.Apply(world,ownerId,publication,sourceKind,sourceSlot,before,after);

    public NetPackageRebirthMetabolismConsumeResult Setup(RebirthConsumeResult value)
    {
        result = value;
        publication=Guid.Empty;before=after=null;beforeItem=afterItem=null;
        return this;
    }
    internal NetPackageRebirthMetabolismConsumeResult WithInventory(int owner,RebirthMetabolismSourceKind kind,int slot,ItemStack prior,ItemStack next)
    {
        if(prior==null||next==null||prior.count==next.count&&prior.itemValue.Equals(next.itemValue))return this;
        ownerId=owner;sourceKind=kind;sourceSlot=slot;publication=Guid.NewGuid();
        before=prior.Clone();after=next.Clone();
        beforeItem=RebirthNativeItemCodec.Encode(before.itemValue);afterItem=RebirthNativeItemCodec.Encode(after.itemValue);
        return this;
    }

    public override void read(PooledBinaryReader reader)
    {
        BinaryReader binary = (BinaryReader)reader;
        result.Success = binary.ReadBoolean();
        result.Failure = (RebirthMetabolismConsumeFailure)binary.ReadByte();
        result.ConsumedMl = binary.ReadSingle();
        result.RemainingMl = binary.ReadSingle();
        result.AddedEntryId = binary.ReadInt32();
        result.CreatedEmptyItem = binary.ReadBoolean();
        result.Message = binary.ReadString();
        publication=new Guid(binary.ReadBytes(16));before=after=null;beforeItem=afterItem=null;
        if(publication!=Guid.Empty)
        {
            ownerId=binary.ReadInt32();sourceKind=(RebirthMetabolismSourceKind)binary.ReadByte();sourceSlot=binary.ReadInt32();
            int priorCount=binary.ReadInt32();beforeItem=binary.ReadString();int nextCount=binary.ReadInt32();afterItem=binary.ReadString();
            ItemValue prior,next;
            if(priorCount<1||nextCount<0||beforeItem.Length>1048576||afterItem.Length>1048576
                ||!RebirthNativeItemCodec.TryDecode(beforeItem,out prior))
                throw new InvalidDataException("Invalid consumption inventory publication.");
            before=new ItemStack(prior,priorCount);
            if(nextCount==0)
            {
                // The canonical item reader deliberately admits registered nonempty items
                // only. A final serving instead publishes the native empty slot image.
                var empty=ItemStack.Empty.Clone();
                if(!string.Equals(afterItem,RebirthNativeItemCodec.Encode(empty.itemValue),StringComparison.Ordinal))
                    throw new InvalidDataException("Invalid empty consumption inventory publication.");
                after=empty;
            }
            else
            {
                if(!RebirthNativeItemCodec.TryDecode(afterItem,out next))
                    throw new InvalidDataException("Invalid consumption inventory publication.");
                after=new ItemStack(next,nextCount);
            }
        }
    }

    public override void write(PooledBinaryWriter writer)
    {
        base.write(writer);
        BinaryWriter binary = (BinaryWriter)writer;
        binary.Write(result.Success);
        binary.Write((byte)result.Failure);
        binary.Write(result.ConsumedMl);
        binary.Write(result.RemainingMl);
        binary.Write(result.AddedEntryId);
        binary.Write(result.CreatedEmptyItem);
        binary.Write(result.Message ?? string.Empty);
        binary.Write(publication.ToByteArray());
        if(publication!=Guid.Empty)
        {
            binary.Write(ownerId);binary.Write((byte)sourceKind);binary.Write(sourceSlot);
            binary.Write(before.count);binary.Write(beforeItem);binary.Write(after.count);binary.Write(afterItem);
        }
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        if(world==null||!world.IsRemote())return;
        if(publication!=Guid.Empty&&RebirthConsumptionPublicationRetry.AcknowledgementProtocolEnabled)
        {
            bool alreadyApplied=RebirthConsumptionOwnerPublication.WasApplied(world,ownerId,publication);
            string creation=RebirthSurvivorClientState.GetProjectedCreationId(world.GetPrimaryPlayer());
            if(!RebirthConsumptionAcknowledgement.TrySend(world,this,creation)||alreadyApplied)return;
            RebirthMetabolismUiFeedback.Receive(result);
            return;
        }
        if(publication!=Guid.Empty&&!RebirthConsumptionOwnerPublication.Apply(world,ownerId,publication,sourceKind,sourceSlot,before,after))return;
        RebirthMetabolismUiFeedback.Receive(result);
    }

    public int GetLength() { return 64 + (result.Message?.Length??0)*2+(beforeItem?.Length??0)*2+(afterItem?.Length??0)*2; }
}

