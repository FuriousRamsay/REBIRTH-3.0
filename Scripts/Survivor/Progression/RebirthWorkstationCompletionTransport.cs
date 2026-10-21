using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine.Scripting;

internal sealed class RebirthStationCompletionTransportFrame
{
    internal int Viewer,Total,Count,Index;internal Guid Operation;internal string Hash,Part;
    internal void Set(int viewer,Guid operation,RebirthRemoteResourceFragments.Body body,int index)
    {Viewer=viewer;Operation=operation;Total=body.Length;Count=body.Count;Index=index;Hash=body.Hash;Part=body.Part(index);}
    internal void Write(BinaryWriter writer)
    {writer.Write((uint)0x32534357);writer.Write(Viewer);writer.Write(Operation.ToByteArray());writer.Write(Total);writer.Write(Count);writer.Write(Index);writer.Write(Hash);writer.Write(Part);}
    internal void Read(BinaryReader reader)
    {
        if(reader.ReadUInt32()!=0x32534357)throw new InvalidDataException();Viewer=reader.ReadInt32();var bytes=reader.ReadBytes(16);if(bytes.Length!=16)throw new EndOfStreamException();Operation=new Guid(bytes);
        Total=reader.ReadInt32();Count=reader.ReadInt32();Index=reader.ReadInt32();Hash=RebirthCookingGridCustody.ReadWireString(reader,64);Part=RebirthCookingGridCustody.ReadWireString(reader,RebirthRemoteResourceFragments.ChunkText);
        if(Viewer<0||Operation==Guid.Empty||Total<1||Total>12*1024*1024||Count!=(Total+RebirthRemoteResourceFragments.ChunkBytes-1)/RebirthRemoteResourceFragments.ChunkBytes||Index<0||Index>=Count)throw new InvalidDataException();
    }
}
[Preserve]
public sealed class NetPackageRebirthStationCompletionRequestV2:NetPackage
{
    private readonly RebirthStationCompletionTransportFrame frame=new RebirthStationCompletionTransportFrame();
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToServer;
    internal NetPackageRebirthStationCompletionRequestV2 Setup(int viewer,Guid operation,RebirthRemoteResourceFragments.Body body,int index){frame.Set(viewer,operation,body,index);return this;}
    public override void write(PooledBinaryWriter writer){base.write(writer);frame.Write(writer);}
    public override void read(PooledBinaryReader reader)=>frame.Read(reader);
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthWorkstationCompletionPipeline.Receive(true,this,world,frame.Viewer,frame.Operation,frame.Hash,frame.Total,frame.Count,frame.Index,frame.Part);
    public int GetLength()=>0;
}
[Preserve]
public sealed class NetPackageRebirthStationCompletionAckV2:NetPackage
{
    private readonly RebirthStationCompletionTransportFrame frame=new RebirthStationCompletionTransportFrame();
    public override NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;
    internal NetPackageRebirthStationCompletionAckV2 Setup(int viewer,Guid operation,RebirthRemoteResourceFragments.Body body,int index){frame.Set(viewer,operation,body,index);return this;}
    public override void write(PooledBinaryWriter writer){base.write(writer);frame.Write(writer);}
    public override void read(PooledBinaryReader reader)=>frame.Read(reader);
    public override void ProcessPackage(World world,GameManager callbacks)=>RebirthWorkstationCompletionPipeline.Receive(false,this,world,frame.Viewer,frame.Operation,frame.Hash,frame.Total,frame.Count,frame.Index,frame.Part);
    public int GetLength()=>0;
}

internal static class RebirthWorkstationCompletionWitnessValidator
{
    internal static ItemStack[] EquipmentCells(XElement image)
    {
        string encoded=(string)image;
        if(encoded==null||encoded.Length>6*1024*1024)throw new InvalidDataException();
        byte[] bytes=Convert.FromBase64String(encoded);
        if(bytes.Length<1||bytes.Length>4*1024*1024||Convert.ToBase64String(bytes)!=encoded)throw new InvalidDataException();
        using(var stream=new MemoryStream(bytes,false))
        using(var reader=MemoryPools.poolBinaryReader.AllocSync(true))
        {
            reader.SetBaseStream(stream);
            if(reader.ReadByte()!=6)throw new InvalidDataException();
            ushort gridVersion=reader.ReadUInt16();var size=StreamUtils.ReadVector2i(reader);int count=reader.ReadInt16();
            if(gridVersion<1||gridVersion>2||count!=13||size.x!=13||size.y!=1)throw new InvalidDataException();
            long rowsStart=stream.Position;stream.Position=0;
            var equipment=Equipment.Read(reader,StreamModeRead.Persistency);
            if(stream.Position!=stream.Length||equipment==null||equipment.GetSlotCount()!=13)throw new InvalidDataException();
            // Rebind rich item images from their exact native spans. Native
            // ItemValue hydration can lose TextureFullArray through a struct property.
            stream.Position=rowsStart;
            int itemBudget=4*1024*1024;
            for(int i=0;i<count;i++)
            {
                int quantity=reader.ReadUInt16();
                if(quantity==0){if(!equipment.ItemGrid.items[i].IsEmpty())throw new InvalidDataException();continue;}
                long start=stream.Position;ItemValue.ReadOrNull(reader);int length=checked((int)(stream.Position-start));
                if(length<1||length>196608)throw new InvalidDataException();
                string itemImage=Convert.ToBase64String(bytes,checked((int)start),length);
                if(itemImage.Length>itemBudget||!RebirthNativeItemCodec.TryDecode(itemImage,out var item)||item==null||item.IsEmpty())throw new InvalidDataException();
                itemBudget-=itemImage.Length;equipment.ItemGrid.items[i]=new ItemStack(item,quantity);
            }
            using(var output=new MemoryStream())
            using(var writer=MemoryPools.poolBinaryWriter.AllocSync(true))
            {
                writer.SetBaseStream(output);equipment.Write(writer,StreamModeWrite.Persistency);writer.Flush();
                if(!output.ToArray().SequenceEqual(bytes))throw new InvalidDataException();
            }
            return ItemStack.Clone(equipment.ItemGrid.items);
        }
    }
    private static ItemStack[] Cells(XElement image)
    {
        string text=(string)image;if(text==null||text.Length>8*1024*1024)throw new InvalidDataException();
        byte[] bytes=Convert.FromBase64String(text);var cells=RebirthCamperInventorySnapshot.Restore(bytes);
        if(cells.Length>255||Convert.ToBase64String(RebirthCamperInventorySnapshot.Capture(cells))!=text||cells.Any(cell=>cell?.itemValue==null||cell.count<0))throw new InvalidDataException();
        return cells;
    }
    private static Dictionary<string,long> Quantities(IEnumerable<ItemStack> cells)
    {
        var result=new Dictionary<string,long>(StringComparer.Ordinal);
        foreach(var cell in cells)
        {
            if(cell==null||cell.count<0)throw new InvalidDataException();if(cell.IsEmpty())continue;
            string native=RebirthNativeItemCodec.Encode(cell.itemValue);result.TryGetValue(native,out long before);result[native]=checked(before+cell.count);
        }
        return result;
    }
    internal static bool Matches(XElement before,XElement delivery,int count)
    {
        try
        {
            var equipmentBefore=before.Element("equipmentBefore");
            var equipmentDeliveryBefore=delivery.Element("equipmentBefore");
            var equipmentAfter=delivery.Element("equipmentAfter");
            bool hasEquipment=equipmentBefore!=null||equipmentDeliveryBefore!=null||equipmentAfter!=null;
            ItemStack[] oldEquipment=Array.Empty<ItemStack>(),nextEquipment=Array.Empty<ItemStack>();
            if(hasEquipment)
            {
                if(equipmentBefore==null||equipmentDeliveryBefore==null||equipmentAfter==null||
                    (string)equipmentBefore!=(string)equipmentDeliveryBefore)return false;
                oldEquipment=EquipmentCells(equipmentBefore);nextEquipment=EquipmentCells(equipmentAfter);
            }
            if(!(bool)delivery.Attribute("nativeOriginalAccepted")&&!(bool)delivery.Attribute("nativeChildrenAccepted"))return false;
            var primary=Cells(delivery.Element("primaryNative"));if(primary.Length!=1||primary[0].count!=count||
                primary[0].itemValue.type!=(int)before.Attribute("primaryType")||primary[0].itemValue.ItemClass.HasQuality&&primary[0].itemValue.Quality!=(int)before.Attribute("quality"))return false;
            var extras=Cells(delivery.Element("extrasNative"));
            if(!ExtrasMatch((string)before.Attribute("extrasDefinition"),extras))return false;
            ItemStack[] old,next;
            if((string)delivery.Attribute("sink")=="Output")
            {if(hasEquipment&&(string)equipmentBefore!=(string)equipmentAfter)return false;
                old=Cells(delivery.Element("outputBefore"));next=Cells(delivery.Element("outputAfter"));}
            else
            {old=Cells(delivery.Element("bagBefore")).Concat(Cells(delivery.Element("beltBefore"))).Concat(oldEquipment).ToArray();
                next=Cells(delivery.Element("bagAfter")).Concat(Cells(delivery.Element("beltAfter"))).Concat(nextEquipment).ToArray();}
            if(old.Length!=next.Length)return false;
            var expected=Quantities(old.Concat(primary).Concat(extras));var actual=Quantities(next);
            if(expected.Count!=actual.Count)return false;
            foreach(var pair in expected)if(!actual.TryGetValue(pair.Key,out long value)||value!=pair.Value)return false;
            return true;
        }
        catch{return false;}
    }
    private static bool ExtrasMatch(string definition,ItemStack[] extras)
    {
        var expected=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        if(definition!="none")
        {
            if(definition==null||definition.Length>65536)return false;int offset=definition.IndexOf('|')+1;if(offset<1)return false;
            while(offset<definition.Length)
            {
                int colon=definition.IndexOf(':',offset);if(colon<offset||!int.TryParse(definition.Substring(offset,colon-offset),out int length)||length<1||length>256||colon+1+length>=definition.Length)return false;
                string name=definition.Substring(colon+1,length);int begin=colon+1+length;if(definition[begin]!=':')return false;int end=definition.IndexOf(';',begin+1);
                if(end<0||!int.TryParse(definition.Substring(begin+1,end-begin-1),out int count)||count<1)return false;
                expected.TryGetValue(name,out long old);expected[name]=checked(old+count);offset=end+1;
            }
        }
        var actual=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in extras)
        {if(item.IsEmpty()||item.itemValue.ItemClass==null)return false;string name=item.itemValue.ItemClass.GetItemName();actual.TryGetValue(name,out long old);actual[name]=checked(old+item.count);}
        return expected.Count==actual.Count&&expected.All(pair=>actual.TryGetValue(pair.Key,out long count)&&count==pair.Value);
    }
}
