using System;
using System.IO;
using System.Linq;
using System.Text;

// Complete native values. Counts are Int32 in custody frames, never truncated
// to a compact network count. Empty values also retain the exact native image.
internal static class RebirthCamperEnergyItems
{
    internal static byte[] Write(ItemStack[] cells)
    {
        if(cells==null||cells.Length>189)throw new InvalidDataException("Bounded actor item cells required.");
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
        {
            writer.Write(cells.Length);
            foreach(var cell in cells)
            {
                if(cell?.itemValue==null||cell.count<0)throw new InvalidDataException("Complete native stack required.");
                string value=RebirthNativeItemCodec.Encode(cell.itemValue);
                if(value==null||value.Length>262144)throw new InvalidDataException("Native item image too large.");
                writer.Write(cell.count);writer.Write(value);
            }
            writer.Flush();if(stream.Length>8*1024*1024)throw new InvalidDataException("Actor frame too large.");return stream.ToArray();
        }
    }
    internal static ItemStack[] Read(byte[] bytes,int maximum)
    {
        if(bytes==null||bytes.Length>8*1024*1024)throw new InvalidDataException("Bounded item frame required.");
        using(var stream=new MemoryStream(bytes,false))using(var reader=new BinaryReader(stream,Encoding.UTF8,true))
        {
            int count=reader.ReadInt32();if(count<0||count>maximum)throw new InvalidDataException("Item dimensions changed.");
            var cells=new ItemStack[count];string empty=RebirthNativeItemCodec.Encode(ItemValue.None);
            for(int i=0;i<count;i++)
            {
                int amount=reader.ReadInt32();string value=reader.ReadString();ItemValue item;
                if(amount<0||value.Length>262144)throw new InvalidDataException("Item count/image invalid.");
                if(value==empty){if(amount!=0)throw new InvalidDataException("Empty item has count.");item=ItemValue.None.Clone();}
                else if(!RebirthNativeItemCodec.TryDecode(value,out item)||item==null||item.IsEmpty()||
                    RebirthNativeItemCodec.Encode(item)!=value)throw new InvalidDataException("Native item codec refused.");
                cells[i]=new ItemStack(item,amount);
            }
            if(stream.Position!=stream.Length)throw new InvalidDataException("Trailing actor bytes.");return cells;
        }
    }
    internal static bool Same(ItemStack[] a,ItemStack[] b)
    {try{return Write(a).SequenceEqual(Write(b));}catch{return false;}}
    internal static bool Same(ItemStack a,ItemStack b)=>Same(new[]{a},new[]{b});
    internal static ItemStack[] Copy(ItemStack[] value)=>Read(Write(value),value.Length);
    internal static bool[] Locks(PackedBoolArray value)
    {if(value==null)return null;var result=new bool[value.Length];for(int i=0;i<result.Length;i++)result[i]=value[i];return result;}
}

internal sealed class RebirthCamperEnergyActorImage
{
    internal ItemStack[] Bag,Belt;
    internal ItemStack Cursor;
    internal bool[] BagLocks;
    internal RebirthCamperEnergyActorImage Copy()=>new RebirthCamperEnergyActorImage {Bag=RebirthCamperEnergyItems.Copy(Bag),
        Belt=RebirthCamperEnergyItems.Copy(Belt),Cursor=RebirthCamperEnergyItems.Copy(new[]{Cursor})[0],BagLocks=BagLocks==null?null:(bool[])BagLocks.Clone()};
    internal bool Matches(RebirthCamperEnergyActorImage other)=>other!=null&&RebirthCamperEnergyItems.Same(Bag,other.Bag)&&
        RebirthCamperEnergyItems.Same(Belt,other.Belt)&&RebirthCamperEnergyItems.Same(Cursor,other.Cursor)&&
        (BagLocks==null||other.BagLocks==null?BagLocks==other.BagLocks:BagLocks.SequenceEqual(other.BagLocks));
    internal static RebirthCamperEnergyActorImage Capture(EntityPlayerLocal player)
    {
        var cursor=player?.PlayerUI?.xui?.DragAndDropWindow;
        if(cursor==null||player.bag?.ItemGrid.items==null||player.inventory?.ItemGrid.items==null||
            !RebirthCamperEnergyItems.Same(cursor.CurrentStack,player.DragAndDropItem))throw new InvalidDataException("Native actor cursor custody changed.");
        return new RebirthCamperEnergyActorImage {Bag=RebirthCamperEnergyItems.Copy(player.bag.ItemGrid.items),Belt=RebirthCamperEnergyItems.Copy(player.inventory.ItemGrid.items),
            Cursor=RebirthCamperEnergyItems.Copy(new[]{cursor.CurrentStack})[0],BagLocks=RebirthCamperEnergyItems.Locks(player.bag.LockedSlots)};
    }
    internal void Write(BinaryWriter writer)
    {
        CamperFrame.Write(writer,RebirthCamperEnergyItems.Write(Bag));CamperFrame.Write(writer,RebirthCamperEnergyItems.Write(Belt));
        CamperFrame.Write(writer,RebirthCamperEnergyItems.Write(new[]{Cursor}));writer.Write(BagLocks!=null);
        if(BagLocks!=null){writer.Write(BagLocks.Length);foreach(bool value in BagLocks)writer.Write(value);}
    }
    internal static RebirthCamperEnergyActorImage Read(BinaryReader reader)
    {
        var result=new RebirthCamperEnergyActorImage {Bag=RebirthCamperEnergyItems.Read(CamperFrame.Read(reader),169),
            Belt=RebirthCamperEnergyItems.Read(CamperFrame.Read(reader),20)};
        var cursor=RebirthCamperEnergyItems.Read(CamperFrame.Read(reader),1);if(cursor.Length!=1)throw new InvalidDataException("One native cursor required.");result.Cursor=cursor[0];
        if(reader.ReadBoolean()){int count=reader.ReadInt32();if(count!=result.Bag.Length)throw new InvalidDataException("Bag locks changed.");result.BagLocks=new bool[count];for(int i=0;i<count;i++)result.BagLocks[i]=reader.ReadBoolean();}
        return result;
    }
}
