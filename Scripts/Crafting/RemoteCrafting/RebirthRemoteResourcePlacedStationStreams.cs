using System;
using System.IO;
using System.Text;
using System.Xml;

// Exact existing actor wire format, streamed without materializing three
// whole EnergyItems frames merely to count/write their enclosing lengths.
internal static class RebirthRemoteResourcePlacedStationStreams
{
    internal sealed class XmlGraphCounter:XmlWriter
    {
        private readonly XmlWriter writer;internal long GraphBytes;
        internal XmlGraphCounter(XmlWriter target){writer=target;}
        private void Text(int chars){GraphBytes=checked(GraphBytes+64+2L*chars);}
        public override WriteState WriteState=>writer.WriteState;
        public override string LookupPrefix(string ns)=>writer.LookupPrefix(ns);
        public override void Flush()=>writer.Flush();public override void Close()=>writer.Close();
        public override void WriteStartDocument()=>writer.WriteStartDocument();public override void WriteStartDocument(bool standalone)=>writer.WriteStartDocument(standalone);
        public override void WriteEndDocument()=>writer.WriteEndDocument();
        public override void WriteDocType(string name,string pubid,string sysid,string subset)=>throw new InvalidDataException();
        public override void WriteStartElement(string prefix,string name,string ns){GraphBytes=checked(GraphBytes+256);writer.WriteStartElement(prefix,name,ns);}
        public override void WriteEndElement()=>writer.WriteEndElement();public override void WriteFullEndElement()=>writer.WriteFullEndElement();
        public override void WriteStartAttribute(string prefix,string name,string ns){GraphBytes=checked(GraphBytes+128);writer.WriteStartAttribute(prefix,name,ns);}
        public override void WriteEndAttribute()=>writer.WriteEndAttribute();
        public override void WriteString(string text){Text(text?.Length??0);writer.WriteString(text);}
        public override void WriteWhitespace(string text){Text(text?.Length??0);writer.WriteWhitespace(text);}
        public override void WriteChars(char[] buffer,int index,int count){Text(count);writer.WriteChars(buffer,index,count);}
        public override void WriteRaw(char[] buffer,int index,int count){Text(count);writer.WriteRaw(buffer,index,count);}
        public override void WriteRaw(string text){Text(text?.Length??0);writer.WriteRaw(text);}
        public override void WriteCData(string text)=>throw new InvalidDataException();
        public override void WriteComment(string text)=>throw new InvalidDataException();
        public override void WriteProcessingInstruction(string name,string text)=>throw new InvalidDataException();
        public override void WriteEntityRef(string name)=>throw new InvalidDataException();
        public override void WriteCharEntity(char value){Text(1);writer.WriteCharEntity(value);}
        public override void WriteSurrogateCharEntity(char low,char high){Text(2);writer.WriteSurrogateCharEntity(low,high);}
        public override void WriteBase64(byte[] buffer,int index,int count){Text(checked((count+2)/3*4));writer.WriteBase64(buffer,index,count);}
    }
    internal sealed class Counter:Stream
    {
        private readonly long maximum;private long count;
        internal Counter(long limit){if(limit<1)throw new ArgumentOutOfRangeException();maximum=limit;}
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>count;public override long Position{get=>count;set=>throw new NotSupportedException();}
        public override void Write(byte[] buffer,int offset,int length){if(length<0||count>maximum-length)throw new InvalidDataException();count+=length;}
        public override void WriteByte(byte value){if(count==maximum)throw new InvalidDataException();count++;}
        public override void Flush(){}public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
    private static void Items(BinaryWriter writer,ItemStack[] items)
    {
        if(items==null||items.Length>189)throw new InvalidDataException();writer.Write(items.Length);
        foreach(var item in items)
        {
            if(item?.itemValue==null||item.count<0)throw new InvalidDataException();
            string image=RebirthNativeItemCodec.Encode(item.itemValue);
            if(image==null||image.Length>262144)throw new InvalidDataException();
            writer.Write(item.count);writer.Write(image);
        }
    }
    internal static long CountItems(ItemStack[] items)
    {
        using(var counter=new Counter(8*1024*1024))using(var writer=new BinaryWriter(counter,new UTF8Encoding(false,true),true))
        {Items(writer,items);writer.Flush();return counter.Length;}
    }
    private static void Frame(BinaryWriter writer,ItemStack[] items)
    {
        int length=checked((int)CountItems(items));writer.Write(length);long start=writer.BaseStream.Position;
        Items(writer,items);writer.Flush();if(writer.BaseStream.Position-start!=length)throw new InvalidDataException();
    }
    internal static void WriteActor(BinaryWriter writer,RebirthCamperEnergyActorImage actor)
    {
        if(actor?.Bag==null||actor.Bag.Length>169||actor.Belt==null||actor.Belt.Length>20||actor.Cursor==null||
            actor.BagLocks!=null&&actor.BagLocks.Length!=actor.Bag.Length)throw new InvalidDataException();
        Frame(writer,actor.Bag);Frame(writer,actor.Belt);Frame(writer,new[]{actor.Cursor});writer.Write(actor.BagLocks!=null);
        if(actor.BagLocks!=null){writer.Write(actor.BagLocks.Length);foreach(bool value in actor.BagLocks)writer.Write(value);}
    }
    internal static long CountActor(RebirthCamperEnergyActorImage actor)
    {
        using(var counter=new Counter(6*1024*1024))using(var writer=new BinaryWriter(counter,new UTF8Encoding(false,true),true))
        {WriteActor(writer,actor);writer.Flush();return counter.Length;}
    }
}
