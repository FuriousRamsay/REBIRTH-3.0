using System;
using System.IO;

public static partial class RemoteResourceTransactions
{
    private sealed class ActionProjectionMemory:IDisposable
    {
        internal IDisposable Measurement,Hydration;
        public void Dispose(){Hydration?.Dispose();Hydration=null;Measurement?.Dispose();Measurement=null;}
    }
    // Actual already-read PDS bag/belt projection only. This parser measures
    // native item structure before GearPlayerDataInventory allocates detached
    // grids; its result is allocation information and never item permission.
    private static bool TryReserveActionOwnerProjection(PlayerDataFile data,out IDisposable memory)
    {
        memory=null;var lease=new ActionProjectionMemory();
        try
        {
            if(data?.bagData==null||data.inventoryData==null)return false;
            long length=checked(data.bagData.Length+data.inventoryData.Length);
            if(length<1||length>4L*1024*1024)return false;
            long measurement=checked(256L*RebirthNativeItemConformanceReader.MaximumNodes+528L*RebirthNativeItemConformanceReader.MaximumChildSlots+
                128L*(196608/5)+2L*RebirthNativeItemConformanceReader.MaximumAggregateStringBytes+196608L*4+65536+6*length);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(measurement,out lease.Measurement))return false;
            long emptyRemaining=1024;byte[] empty=CaptureActionFrame(new[]{ItemStack.Empty.Clone()},ref emptyRemaining);
            long graph=0,parser=0,footer=0;
            if(!MeasureActionNativeOwnerGrid(data.bagData,true,empty,ref graph,ref parser,ref footer)||!MeasureActionNativeOwnerGrid(data.inventoryData,false,empty,ref graph,ref parser,ref footer))return false;
            // Native grid + CloneItems + canonical replacement and temporary
            // exact boundary reader can coexist. Bag and belt return arrays
            // may remain live together through the complete image comparison.
            long hydration=checked(4*graph+footer+parser+65536);
            if(!RebirthProductiveEarnedObservationBudget.TryRetainDetached(hydration,out lease.Hydration))return false;
            memory=lease;lease=null;return true;
        }
        catch{return false;}
        finally{lease?.Dispose();}
    }
    private static bool MeasureActionNativeOwnerGrid(MemoryStream blob,bool bag,byte[] empty,ref long graph,ref long parser,ref long footer)
    {
        byte[] bytes=blob.ToArray();
        using(var input=new MemoryStream(bytes,false))using(var reader=new BinaryReader(input))
        {
            if(reader.ReadByte()!=(bag?2:1))return false;
            ushort version=reader.ReadUInt16();int x=reader.ReadInt32(),y=reader.ReadInt32(),count=reader.ReadInt16(),maximum=bag?169:20;
            if(version<1||version>2||x<0||y<0||x>maximum||y>maximum||count<0||count>maximum||(long)x*y!=count)return false;
            if(!MeasureActionNativeStackRows(reader,bytes,count,empty,ref graph,ref parser))return false;
            if(ReadActionNativeBoolean(reader))
            {
                int bits=ReadActionNative7Bit(reader);long byteCount=((long)bits+7)/8;
                if(byteCount>input.Length-input.Position)return false;
                footer=checked(footer+byteCount+64);input.Position+=byteCount;
            }
            if(ReadActionNativeBoolean(reader))
            {
                reader.ReadInt32(); // PreferenceTracker.PlayerID
                if(ReadActionNativeBoolean(reader)&&!MeasureActionNativeStackRows(reader,bytes,reader.ReadUInt16(),empty,ref footer,ref parser))return false;
                if(ReadActionNativeBoolean(reader))
                {
                    int values=reader.ReadUInt16();if(values>input.Length-input.Position)return false;
                    footer=checked(footer+64L+520L*values);
                    for(int item=0;item<values;item++)if(ReadActionNativeBoolean(reader)&&!MeasureActionNativeValue(reader,bytes,empty,ref footer,ref parser))return false;
                }
                if(ReadActionNativeBoolean(reader)&&!MeasureActionNativeStackRows(reader,bytes,reader.ReadUInt16(),empty,ref footer,ref parser))return false;
            }
            if(version>=2){reader.ReadUInt64();ReadActionNativeBoolean(reader);}
            if(!bag)reader.ReadByte(); // Inventory.holdingItemIdx
            // Full allocation-bearing footer is preflighted before native
            // hydration. Existing Gear native/canonical reader still decides
            // acceptance independently, including exact EOF and item registry.
            return input.Position==input.Length;
        }
    }
    private static bool ReadActionNativeBoolean(BinaryReader reader)
    {byte value=reader.ReadByte();if(value>1)throw new InvalidDataException();return value==1;}
    private static int ReadActionNative7Bit(BinaryReader reader)
    {
        uint value=0;
        for(int group=0;group<5;group++)
        {
            byte part=reader.ReadByte();if(group==4&&(part&248)!=0)throw new InvalidDataException();value|=(uint)(part&127)<<(group*7);
            if((part&128)==0){if(group>0&&(part&127)==0)throw new InvalidDataException();return checked((int)value);}
        }
        throw new InvalidDataException();
    }
    private static bool MeasureActionNativeStackRows(BinaryReader reader,byte[] bytes,int count,byte[] empty,ref long graph,ref long parser)
    {
        if(count<0||count>(reader.BaseStream.Length-reader.BaseStream.Position)/2)return false;
        graph=checked(graph+64L+544L*count);
        for(int slot=0;slot<count;slot++)if(reader.ReadUInt16()!=0&&!MeasureActionNativeValue(reader,bytes,empty,ref graph,ref parser))return false;
        return true;
    }
    private static bool MeasureActionNativeValue(BinaryReader reader,byte[] bytes,byte[] empty,ref long graph,ref long parser)
    {
        int start=checked((int)reader.BaseStream.Position);
        if(RebirthNativeItemConformanceReader.TryMeasureCanonicalV9PrefixStructure(bytes,start,out var item,out int consumed)&&consumed>0)
        {graph=checked(graph+item.CloneAllocationBound+64);parser=Math.Max(parser,item.ParserScratchBound);reader.BaseStream.Position=checked(start+consumed);return true;}
        int emptyLength=empty.Length-16;if(emptyLength<1||emptyLength>bytes.Length-start)return false;
        for(int index=0;index<emptyLength;index++)if(bytes[start+index]!=empty[16+index])return false;
        graph=checked(graph+512);reader.BaseStream.Position=checked(start+emptyLength);return true;
    }
}
