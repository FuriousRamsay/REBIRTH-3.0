using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// Exact installed TileEntity v19 / workstation v50 Persistency framing.
// Detached parsing only; omitted recipe authoring fields remain omitted.
internal static class RebirthRemoteResourcePlacedStationNativeReader
{
    // Detached actual Add argument format. The count prefix is parser framing,
    // not a reconstructed native receipt or proof of any live mutation.
    internal static bool TryReadCompletionArgument(byte[] argument,out CraftCompleteData value)
    {
        value=null;
        try
        {
            if(argument==null||argument.Length<16||argument.Length>256*1024)return false;
            var bytes=new byte[argument.Length+2];bytes[0]=1;Buffer.BlockCopy(argument,0,bytes,2,argument.Length);
            using(var measured=new Reader(bytes,true)){measured.Complete();if(measured.Position!=bytes.Length)return false;}
            using(var reader=new Reader(bytes)){var rows=reader.Complete();if(reader.Position!=bytes.Length||rows.Length!=1)return false;value=rows[0];return true;}
        }
        catch{return false;}
    }
    internal sealed class Image
    {
        internal Vector3i LocalPosition;internal ulong HeatClock,Clock;internal bool Burning,PlayerPlaced;
        internal float BurnTime;internal float[] MeltTimes;internal ItemStack[] Fuel,Input,Tools,Output,LastInput;
        internal RecipeQueueItem[] Queue;internal CraftCompleteData[] Completions;
        internal int ClockOffset,InputStart,InputLength,QueueStart,QueueLength;
    }
    private sealed class Reader:IDisposable
    {
        internal readonly byte[] Bytes;internal readonly MemoryStream Stream;internal readonly BinaryReader Binary;
        internal int Items,Stacks,Strings;internal readonly bool MeasureOnly;internal long Graph,Scratch;
        internal Reader(byte[] bytes,bool measureOnly=false){Bytes=bytes;MeasureOnly=measureOnly;Stream=new MemoryStream(bytes,false);Binary=new BinaryReader(Stream,new UTF8Encoding(false,true));}
        internal int Position=>checked((int)Stream.Position);
        internal byte Byte()=>Binary.ReadByte();internal ushort U16()=>Binary.ReadUInt16();internal short I16()=>Binary.ReadInt16();
        internal int I32()=>Binary.ReadInt32();internal ulong U64()=>Binary.ReadUInt64();
        internal bool Bool(){byte value=Byte();if(value>1)throw new InvalidDataException();return value==1;}
        internal float Float(){float value=Binary.ReadSingle();if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException();return value;}
        internal string Text()
        {
            uint length=0;int groups=0;byte part;
            do{part=Byte();if(groups==4&&(part&248)!=0)throw new InvalidDataException();length|=(uint)(part&127)<<(groups*7);groups++;if(groups>5)throw new InvalidDataException();}while((part&128)!=0);
            if(groups>1&&(part&127)==0||length>65536||(Strings=checked(Strings+(int)length))>131072||length>Stream.Length-Stream.Position)throw new InvalidDataException();
            var bytes=Binary.ReadBytes((int)length);if(bytes.Length!=length)throw new EndOfStreamException();var text=new UTF8Encoding(false,true).GetString(bytes);Graph=checked(Graph+32+2L*text.Length);return text;
        }
        internal ItemValue Item()
        {
            if(MeasureOnly)
            {
                if(++Items>8192||!RebirthNativeItemConformanceReader.TryMeasureCanonicalV9PrefixStructure(Bytes,Position,out var measured,out int size))throw new InvalidDataException();
                Graph=checked(Graph+Math.Max(512,measured.CloneAllocationBound));Scratch=Math.Max(Scratch,measured.ParserScratchBound+Bytes.LongLength);Stream.Position+=size;return null;
            }
            if(++Items>8192||!RebirthNativeItemConformanceReader.TryReadCanonicalV9Prefix(Bytes,Position,out var value,out int consumed))throw new InvalidDataException();
            Stream.Position+=consumed;return value;
        }
        internal ItemStack Stack()
        {if(++Stacks>8192)throw new InvalidDataException();Graph=checked(Graph+64);int count=U16();if(count==0){Graph=checked(Graph+512);return MeasureOnly?null:ItemStack.Empty.Clone();}var value=Item();if(!MeasureOnly&&(value==null||value.IsEmpty()))throw new InvalidDataException();return MeasureOnly?null:new ItemStack(value,count);}
        internal ItemStack[] Array(){int count=Byte();Graph=checked(Graph+32+8L*count);var array=MeasureOnly?null:new ItemStack[count];for(int i=0;i<count;i++){var stack=Stack();if(!MeasureOnly)array[i]=stack;}return array;}
        internal Recipe Recipe()
        {
            if(U16()!=1)throw new InvalidDataException();int type=I32(),count=I32();bool scrap=Bool();float time=Float();int experience=I32();string area=Text();int ingredients=I32();
            if(type<=0||ItemClass.GetForId(type)==null||count<1||time<0||experience<0||ingredients<0||ingredients>8192-Stacks)throw new InvalidDataException();
            Graph=checked(Graph+512+16L*ingredients);var recipe=MeasureOnly?null:new Recipe{itemValueType=type,count=count,IsScrap=scrap,craftingTime=time,craftExpGain=experience,craftingArea=area};
            for(int i=0;i<ingredients;i++){var item=Stack();if(!MeasureOnly)recipe.ingredients.Add(item);}return recipe;
        }
        internal RecipeQueueItem[] Queue()
        {
            int count=Byte();if(count<1)throw new InvalidDataException();Graph=checked(Graph+32+520L*count);var queue=MeasureOnly?null:new RecipeQueueItem[count];
            for(int i=0;i<count;i++)
            {
                if(U16()!=2)throw new InvalidDataException();short multiplier=I16();bool crafting=Bool();float left=Float();if(multiplier<0)throw new InvalidDataException();
                ItemValue repair=null;ushort amount=0;if(Bool()){repair=Item();if(!MeasureOnly&&repair==null)throw new InvalidDataException();amount=U16();}
                byte quality=Byte();int entity=I32();float time=Float();Recipe recipe=null;if(Bool())recipe=Recipe();
                if(!MeasureOnly)queue[i]=new RecipeQueueItem{Multiplier=multiplier,IsCrafting=crafting,CraftingTimeLeft=left,RepairItem=repair,AmountToRepair=amount,Quality=quality,StartingEntityId=entity,OneItemCraftTime=time,Recipe=recipe};
            }
            return queue;
        }
        internal CraftCompleteData[] Complete()
        {
            int count=I16();if(count<0||count>8192-Stacks||count>(Stream.Length-Stream.Position)/16)throw new InvalidDataException();Graph=checked(Graph+32+520L*count);var values=MeasureOnly?null:new CraftCompleteData[count];
            for(int i=0;i<count;i++)
            {
                if(U16()!=1)throw new InvalidDataException();
                int entity=I32();var stack=Stack();string name=Text();int experience=I32();ushort used=U16();string scrapped=Text();
                if(!MeasureOnly)values[i]=new CraftCompleteData{CrafterEntityID=entity,CraftedItemStack=stack,RecipeName=name,CraftExpGain=experience,RecipeUsedCount=used,ItemScrapped=scrapped};
            }
            return values;
        }
        public void Dispose(){Binary.Dispose();Stream.Dispose();}
    }
    internal static bool TryRead(byte[] bytes,out Image image)
    {
        image=null;if(bytes==null||bytes.Length<32||bytes.Length>8*1024*1024)return false;
        try
        {
            if(!TryMeasure(bytes,out long graph,out long scratch)||checked(3*graph+scratch+bytes.LongLength*4)>
                RebirthRemoteResourcePlacedStationArtifact.MaximumOperationWorkspace)return false;
            using(var reader=new Reader(bytes))
            {
                if(reader.U16()!=19)return false;var value=new Image{LocalPosition=new Vector3i(reader.I32(),reader.I32(),reader.I32()),HeatClock=reader.U64()};
                if(reader.Byte()!=50)return false;value.ClockOffset=reader.Position;value.Clock=reader.U64();value.Fuel=reader.Array();
                value.InputStart=reader.Position;value.Input=reader.Array();value.InputLength=reader.Position-value.InputStart;value.Tools=reader.Array();value.Output=reader.Array();
                value.QueueStart=reader.Position;value.Queue=reader.Queue();value.QueueLength=reader.Position-value.QueueStart;value.Completions=reader.Complete();
                value.Burning=reader.Bool();value.BurnTime=reader.Float();int melts=reader.Byte();value.MeltTimes=new float[melts];for(int i=0;i<melts;i++)value.MeltTimes[i]=reader.Float();
                value.PlayerPlaced=reader.Bool();value.LastInput=reader.Array();if(reader.Position!=bytes.Length||!Encode(value).SequenceEqual(bytes))return false;image=value;return true;
            }
        }
        catch{image=null;return false;}
    }
    // Exact framing walk before native item hydration. This is a memory/support
    // check only; TryRead still requires the canonical native re-encoding.
    internal static bool TryMeasure(byte[] bytes,out long graph,out long scratch)
    {
        graph=0;scratch=0;
        if(bytes==null||bytes.Length<32||bytes.Length>8*1024*1024)return false;
        try
        {
            using(var reader=new Reader(bytes,true))
            {
                if(reader.U16()!=19)return false;reader.I32();reader.I32();reader.I32();reader.U64();if(reader.Byte()!=50)return false;reader.U64();
                reader.Array();reader.Array();reader.Array();reader.Array();reader.Queue();reader.Complete();
                reader.Bool();reader.Float();int melts=reader.Byte();reader.Graph=checked(reader.Graph+512+32+4L*melts);
                for(int index=0;index<melts;index++)reader.Float();reader.Bool();reader.Array();
                if(reader.Position!=bytes.Length)return false;graph=reader.Graph;scratch=Math.Max(reader.Scratch,bytes.LongLength*4+131072);return true;
            }
        }
        catch{graph=0;scratch=0;return false;}
    }
    // Exact ToServer framing measure only. No native reader or graph is
    // invoked; this is not native acceptance or financial permission.
    internal static bool TryMeasureToServer(byte[] bytes,out int elapsedOffset,out long graph,out long scratch)
    {
        elapsedOffset=-1;graph=scratch=0;if(bytes==null||bytes.Length<30||bytes.Length>8*1024*1024)return false;
        try
        {
            using(var reader=new Reader(bytes,true))
            {
                reader.I32();reader.I32();reader.I32();if(reader.Byte()!=50)return false;
                reader.Array();reader.Array();reader.Array();reader.Array();reader.Queue();reader.Complete();reader.Bool();reader.Float();
                int melts=reader.Byte();reader.Graph=checked(reader.Graph+512+32+4L*melts);for(int index=0;index<melts;index++)reader.Float();reader.Bool();
                elapsedOffset=reader.Position;reader.U64();reader.Array();if(reader.Position!=bytes.Length)return false;
                graph=reader.Graph;scratch=Math.Max(reader.Scratch,bytes.LongLength*4+131072);return true;
            }
        }
        catch{elapsedOffset=-1;graph=scratch=0;return false;}
    }
    private sealed class BoundedStream:MemoryStream
    {
        public override void Write(byte[] buffer,int offset,int count)
        {if(count<0||Position>8*1024*1024-count)throw new InvalidDataException();base.Write(buffer,offset,count);}
        public override void WriteByte(byte value)
        {if(Position>=8*1024*1024)throw new InvalidDataException();base.WriteByte(value);}
    }
    internal static byte[] Encode(Image value)
    {
        using(var stream=new BoundedStream())using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
        {
            Encoding previous=writer.Encoding;
            try
            {
                writer.Encoding=new UTF8Encoding(false,false);writer.SetBaseStream(stream);writer.Write((ushort)19);
                writer.Write(value.LocalPosition.x);writer.Write(value.LocalPosition.y);writer.Write(value.LocalPosition.z);writer.Write(value.HeatClock);writer.Write((byte)50);writer.Write(value.Clock);
                WriteArray(writer,value.Fuel);WriteArray(writer,value.Input);WriteArray(writer,value.Tools);WriteArray(writer,value.Output);
                writer.Write((byte)value.Queue.Length);foreach(var row in value.Queue)row.Write(writer);
                writer.Write((short)value.Completions.Length);foreach(var completion in value.Completions)completion.Write(writer);
                writer.Write(value.Burning);writer.Write(value.BurnTime);writer.Write((byte)value.MeltTimes.Length);foreach(float melt in value.MeltTimes)writer.Write(melt);
                writer.Write(value.PlayerPlaced);WriteArray(writer,value.LastInput);writer.Flush();return stream.ToArray();
            }
            finally{writer.Encoding=previous;}
        }
    }
    private static void WriteArray(PooledBinaryWriter writer,ItemStack[] values)
    {writer.Write((byte)values.Length);foreach(var item in values)item.Write(writer);}
}
