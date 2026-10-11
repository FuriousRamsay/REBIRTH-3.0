using System;
using System.IO;

// Readback evidence only. Never treats latestPlayerData or an ACK as a saved inventory.
public static class RebirthBackpackLibrarySavedEvidence
{
    public static bool ContainsReceipt(byte[] bytes,RebirthBackpackLibraryReceipt receipt,bool applied)
    {
        if(bytes==null||bytes.Length>4194304||receipt==null)return false;
        try
        {
            using(var stream=new MemoryStream(bytes))using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                reader.SetBaseStream(stream);
                int version=reader.ReadByte();if(version!=EntityBuffs.Version||version<2)return false;
                int buffs=reader.ReadUInt16();
                for(int i=0;i<buffs;i++)new BuffValue().Read(reader,version);
                int variables=reader.ReadUInt16();string key=RebirthBackpackLibraryOwnerTransfer.ReceiptKey(receipt);bool found=false;
                for(int i=0;i<variables;i++)
                {
                    string name=reader.ReadString();float value=reader.ReadSingle();
                    if(name!=key)continue;
                    if(found||value!=(applied?1f:-1f))return false;found=true;
                }
                return found&&stream.Position==stream.Length;
            }
        }
        catch{return false;}
    }
    public static bool Matches(PlayerDataFile saved,RebirthBackpackLibraryReceipt receipt,bool applied)
    {
        if(saved==null||!saved.bLoaded||saved.buffData==null||!ContainsReceipt(saved.buffData.ToArray(),receipt,applied))return false;
        if(!applied)return true; // Durable rejection means this operation never acquired inventory custody.
        try
        {
            if(!receipt.TryGetImages(out _,out _,out _,out var expected))return false;
            var slots=receipt.IsCursor?new[]{saved.dragAndDropItem}:receipt.IsBag?RebirthPlayerDataInventory.ReadSlots(saved,true):RebirthPlayerDataInventory.ReadSlots(saved,false);
            if(receipt.IsBatchSale)
            {
                if(slots==null||!receipt.TryGetWallet(out var changes))return false;
                foreach(var change in changes)if(change.Slot>=slots.Length||!RebirthStationGridIngredients.IsSameStackSnapshot(slots[change.Slot],change.After))return false;
                return true;
            }
            int slot=receipt.InventorySlot;
            if(slots==null||slot<0||slot>=slots.Length||slots[slot]==null||slots[slot].count!=expected.count)return false;
            if(expected.count==0)return true;
            return slots[slot].itemValue!=null&&string.Equals(RebirthNativeItemCodec.Encode(slots[slot].itemValue),
                RebirthNativeItemCodec.Encode(expected.itemValue),StringComparison.Ordinal);
        }
        catch{return false;}
    }
    public static bool TryVerifyFromDisk(ClientInfo owner,RebirthBackpackLibraryReceipt receipt,bool applied)
    {
        if(owner?.InternalId==null||receipt==null)return false;
        try
        {
            var saved=new PlayerDataFile();saved.Load(GameIO.GetPlayerDataDir(),owner.InternalId.CombinedString);
            return Matches(saved,receipt,applied);
        }
        catch{return false;}
    }
    public static bool TryVerifyLocalFromDisk(EntityPlayerLocal owner,RebirthBackpackLibraryReceipt receipt,bool applied)
    {
        var game=GameManager.Instance;
        if(owner==null||owner.world==null||owner.world.IsRemote()||game==null||
            !ReferenceEquals(owner.world,game.World)||!ReferenceEquals(owner.world.GetPrimaryPlayer(),owner)||
            !ReferenceEquals(owner.world.GetEntity(owner.entityId),owner)||!RebirthWorldCharacterRepository.IsServerAuthority||receipt==null)return false;
        try
        {
            // Same native identity used by SaveLocalPlayerData, not a filename supplied by UI.
            var identity=game.getPersistentPlayerID(null);if(identity==null)return false;
            var saved=new PlayerDataFile();saved.Load(GameIO.GetPlayerDataDir(),identity.CombinedString);
            return Matches(saved,receipt,applied);
        }
        catch{return false;}
    }
}
