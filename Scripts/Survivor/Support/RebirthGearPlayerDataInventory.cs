using System;
using System.IO;
using System.Text;

// Gear custody needs the exact saved/uploaded ItemValue image. Native
// ItemValue.ReadData loses TextureFullArray through a struct-valued property;
// the existing canonical codec restores that field on detached values only.
internal static class RebirthGearPlayerDataInventory
{
    internal static ItemStack[] ReadSlots(PlayerDataFile data,bool bag)
    {
        try
        {
            MemoryStream blob=bag?data?.bagData:data?.inventoryData;
            if(blob==null)return null;
            long originalLength=blob.Length;
            if(originalLength<=0||originalLength>4194304)return null;
            using(var copy=new BoundedOwnerBlob())
            {
            // WriteTo preserves the native source position. Its destination is
            // independently bounded even if the source changes after Length.
            blob.WriteTo(copy);
            if(copy.Length!=originalLength||blob.Length!=originalLength||
                !ReferenceEquals(blob,bag?data?.bagData:data?.inventoryData))return null;
            byte[] bytes=copy.GetBuffer();
            using(var stream=new MemoryStream(bytes,0,checked((int)copy.Length),false))
            using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
            {
                Encoding previous=reader.Encoding;
                try
                {
                    reader.Encoding=new UTF8Encoding(false,false);reader.SetBaseStream(stream);
                    if(reader.ReadByte()!=(bag?2:1))return null;
                    long gridStart=stream.Position;
                    ushort version=reader.ReadUInt16();var size=StreamUtils.ReadVector2i(reader);int count=reader.ReadInt16();
                    int maximum=bag?169:20;
                    if(version<1||version>2||size.x<0||size.y<0||size.x>maximum||size.y>maximum||count<0||count>maximum||(long)size.x*size.y!=count)return null;
                    long rowsStart=stream.Position;
                    // Keep native grid/footer validation, including locks/preferences.
                    stream.Position=gridStart;var grid=ItemStackGrid.Read(reader,StreamModeRead.Persistency);
                    if(!bag)reader.ReadByte();
                    if(stream.Position!=stream.Length)return null;
                    var slots=grid.CloneItems();if(slots==null||slots.Length!=count)return null;
                    stream.Position=rowsStart;
                    int budget=4*1024*1024;
                    for(int i=0;i<count;i++)
                    {
                        int stackCount=reader.ReadUInt16();
                        if(stackCount==0){if(slots[i]==null||slots[i].count!=0)return null;continue;}
                        long start=stream.Position;
                        // Native reader determines the exact item boundary in this
                        // already-validated detached grid; never read live items.
                        ItemValue.ReadOrNull(reader);
                        int length=checked((int)(stream.Position-start));
                        if(length<=0||length>196608)return null;
                        string image=Convert.ToBase64String(bytes,checked((int)start),length);
                        if(image.Length>budget||!RebirthNativeItemCodec.TryDecode(image,out var value)||value==null||value.IsEmpty()||value.ItemClass==null)return null;
                        budget-=image.Length;
                        if(slots[i]==null||slots[i].count!=stackCount)return null;
                        // Replace only the detached hydrated value. Count, shape,
                        // ordering and all native item bytes remain exact.
                        slots[i]=new ItemStack(value,stackCount);
                    }
                    return slots;
                }
                finally{reader.Encoding=previous;}
            }
            }
        }
        catch{return null;}
    }
    private sealed class BoundedOwnerBlob:MemoryStream
    {
        private const int Maximum=4194304;
        private void Check(long end){if(end<0||end>Maximum)throw new InvalidDataException("Native owner inventory exceeds bound.");}
        private void Ensure(long end)
        {
            Check(end);
            if(end>Capacity)Capacity=checked((int)Math.Min(Maximum,Math.Max(end,Math.Max(256L,2L*Capacity))));
        }
        public override int Capacity {get=>base.Capacity;set{Check(value);base.Capacity=value;}}
        public override void Write(byte[] buffer,int offset,int count){Ensure(checked(Position+count));base.Write(buffer,offset,count);}
        public override void WriteByte(byte value){Ensure(checked(Position+1));base.WriteByte(value);}
        public override void SetLength(long value){Ensure(value);base.SetLength(value);}
    }
}
