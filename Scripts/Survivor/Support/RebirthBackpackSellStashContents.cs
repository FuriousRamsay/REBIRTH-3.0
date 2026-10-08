using System;
using System.IO;

// Sell stash travels in the physical backpack ItemValue, including native drops/trades and gear custody.
public static class RebirthBackpackSellStashContents
{
    public const string MetadataKey="rebirth.backpack.sellstash.v1";
    private const int MaxBytes=98304;
    private const int MaxText=131072;

    public static bool TryReadEquipped(EntityPlayer player,out ItemValue backpack,out ItemStack[] contents)
    {
        backpack=null;contents=null;
        ItemValue item;
        if(!RebirthSurvivorGearService.TryGetEquippedBackpackItem(player,out item)||!TryRead(item,out contents))return false;
        backpack=item;return true;
    }
    public static bool TryRead(ItemValue backpack,out ItemStack[] contents)
    {
        contents=null;
        if(backpack?.ItemClass==null||backpack.Metadata!=null&&backpack.Metadata.Count>byte.MaxValue)return false;
        int capacity=RebirthBackpackSellStashPolicy.CapacityForBackpack(backpack.ItemClass.GetItemName());
        if(capacity<=0)return false;
        if(backpack.Metadata==null||!backpack.Metadata.ContainsKey(MetadataKey))
        { contents=ItemStack.CreateArray(capacity);return true; }
        var stored=backpack.Metadata[MetadataKey];
        // A corrupt reserved key is not an absent old-save store; native getter can throw on null.
        if(stored==null||stored.GetTypeTag()!=TypedMetadataValue.TypeTag.String||!(stored.GetValue() is string))return false;
        string encoded;
        if(!backpack.TryGetMetadata(MetadataKey,out encoded))return false;
        if(string.IsNullOrEmpty(encoded)||encoded.Length>MaxText)return false;
        try
        {
            ItemStack[] candidate;
            if(!RebirthNativeItemConformanceReader.TryDecodeStackArrayV1(encoded,capacity,MaxBytes,MaxText,out candidate))return false;
            foreach(var stack in candidate)
                if(stack.count>0&&(stack.IsEmpty()||!RebirthBackpackSellStashPolicy.IsStorableItem(stack.itemValue)||stack.count>stack.itemValue.ItemClass.MaxCount))return false;
            contents=candidate;return true;
        }
        catch{return false;}
    }

    // Return a detached candidate. Inventory and persisted gear are unchanged until caller commits.
    public static bool TryWrite(ItemValue backpack,ItemStack[] contents,out ItemValue candidate)
    {
        candidate=null;
        ItemStack[] prior;
        if(!TryRead(backpack,out prior)||contents==null||contents.Length!=prior.Length)return false;
        try
        {
            foreach(var stack in contents)
            {
                if(stack==null||stack.count<0||stack.count>ushort.MaxValue)return false;
                if(stack.count>0&&(stack.IsEmpty()||!RebirthBackpackSellStashPolicy.IsStorableItem(stack.itemValue)||stack.count>stack.itemValue.ItemClass.MaxCount))return false;
                if(stack.itemValue?.Metadata!=null&&stack.itemValue.Metadata.Count>byte.MaxValue)return false;
            }
            string encoded;
            if(!RebirthNativeItemConformanceReader.TryEncodeStackArrayV1(contents,MaxBytes,MaxText,out encoded))return false;
            var next=backpack.Clone();next.SetMetadata(MetadataKey,encoded);
            if(next.Metadata!=null&&next.Metadata.Count>byte.MaxValue||!RebirthBackpackStoragePayload.Fits(next))return false;
            string storedImage; ItemStack[] verified;
            if(!next.TryGetMetadata(MetadataKey,out storedImage)||storedImage!=encoded||!TryRead(next,out verified))return false;
            candidate=next;return true;
        }
        catch{return false;}
    }
}