using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

internal static partial class RebirthProductiveEarnedUiOwnerFile
{
    private sealed class CountNative:Stream
    {
        private readonly long maximum;
        internal CountNative(long maximum=Maximum){if(maximum<1||maximum>Maximum)throw new InvalidDataException();this.maximum=maximum;}
        internal long Count;public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>Count;public override long Position{get=>Count;set=>throw new NotSupportedException();}
        public override void Flush(){}public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count){if(count<0||Count>maximum-count)throw new InvalidDataException();Count+=count;}
        public override void WriteByte(byte value){if(Count>=maximum)throw new InvalidDataException();Count++;}
    }
    private static long MeasureNative(Action<PooledBinaryWriter> action,long maximum=Maximum)
    {using(var count=new CountNative(maximum))using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true)){writer.SetBaseStream(count);action(writer);writer.Flush();return count.Count;}}
    internal static bool TryMeasureDeferredLiveFrame(EntityPlayerLocal actor,string[] recipes,out int bytes)
    {
        bytes=0;
        try
        {
            if(!ThreadManager.IsMainThread()||actor==null||recipes==null||recipes.Length<1||recipes.Length>4||recipes.Any(value=>string.IsNullOrEmpty(value)||value.Length>256))return false;
            long encoded=16384;
            foreach(string recipe in recipes)
            {
                long size=MeasureNative(writer=>{actor.Progression.Write(writer);actor.QuestJournal.Write(writer);actor.challengeJournal.Write(writer);
                    writer.Write(actor.totalItemsCrafted);writer.Write(actor.Buffs.GetCustomVar("_craftCount_"+recipe));});
                encoded=checked(encoded+4*((size+2)/3));
            }
            foreach(var slots in new[]{actor.bag.ItemGrid.items,actor.inventory.ItemGrid.items})
            {
                if(slots==null||slots.Length>255)return false;
                long size=MeasureNative(writer=>{var encoding=writer.Encoding;try{writer.Encoding=new UTF8Encoding(false,false);writer.Write(slots.Length);
                    foreach(var stack in slots){if(stack?.itemValue==null||stack.count<0)throw new InvalidDataException();writer.Write(stack.count);stack.itemValue.Write(writer);}}
                    finally{writer.Encoding=encoding;}});
                encoded=checked(encoded+4*((size+2)/3));
            }
            long equipmentSize=MeasureNative(writer=>actor.equipment.Write(writer,StreamModeWrite.Persistency));
            encoded=checked(encoded+4*((equipmentSize+2)/3));
            if(encoded>Maximum)return false;bytes=(int)encoded;return true;
        }
        catch{return false;}
    }
    internal static XElement CaptureDeferredLiveFrame(EntityPlayerLocal actor,string[] recipes)
    {
        var frame=new XElement("nativeOwnerFrame",new XAttribute("version",2),new XElement("recipes",recipes.Distinct(StringComparer.Ordinal).Select(recipe=>
            new XElement("recipe",new XAttribute("name",recipe),new XElement("projection",Convert.ToBase64String(Live(actor,recipe)))))),
            new XElement("bag",Convert.ToBase64String(Slots(actor.bag.ItemGrid.items))),new XElement("belt",Convert.ToBase64String(Slots(actor.inventory.ItemGrid.items))),
            new XElement("equipment",Convert.ToBase64String(RebirthRecipeOutputIntegration.OwningEquipmentNative(actor))));
        if(Encoding.UTF8.GetByteCount(frame.ToString(SaveOptions.DisableFormatting))>Maximum)throw new InvalidDataException();return frame;
    }
    internal static XElement CaptureDeferredConsumedFrame(DeferredWitness[] members,bool after)
    {
        if(members==null||members.Length<1||members.Length>4||members.Any(value=>!value.Consumed||!value.Current()||value.Receipt==null))throw new InvalidDataException();
        var first=members[0];var bag=after?first.BagAfter:first.BagBefore;var belt=after?first.BeltAfter:first.BeltBefore;
        var equipment=after?first.EquipmentAfter:first.EquipmentBefore;
        if(equipment==null||members.Any(value=>(after?value.EquipmentAfter:value.EquipmentBefore)==null||!(after?value.EquipmentAfter:value.EquipmentBefore).SequenceEqual(equipment)))throw new InvalidDataException();
        if(bag==null||belt==null||members.Any(value=>!(after?value.BagAfter:value.BagBefore).SequenceEqual(bag)||!(after?value.BeltAfter:value.BeltBefore).SequenceEqual(belt)))throw new InvalidDataException();
        var frame=new XElement("nativeOwnerFrame",new XAttribute("version",2),new XElement("recipes",members.GroupBy(value=>value.Receipt.Receipt.RecipeName,StringComparer.Ordinal).Select(group=>
        {var projection=after?group.First().After:group.First().Before;if(projection==null||group.Any(value=>!(after?value.After:value.Before).SequenceEqual(projection)))throw new InvalidDataException();
            return new XElement("recipe",new XAttribute("name",group.Key),new XElement("projection",Convert.ToBase64String(projection)));})),
            new XElement("bag",Convert.ToBase64String(bag)),new XElement("belt",Convert.ToBase64String(belt)),new XElement("equipment",Convert.ToBase64String(equipment)));
        if(Encoding.UTF8.GetByteCount(frame.ToString(SaveOptions.DisableFormatting))>Maximum)throw new InvalidDataException();return frame;
    }
}
