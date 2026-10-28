using System;
using System.IO;
using System.Text;
// AUTHORED_NOT_RUN. Entire actual owner inventory reader is injected.
// Native grid/item codec below is an explicit fixture grammar, not native schema or file authentication.
// ACTUAL_OWNER_BLOB_SOURCE
internal sealed class PlayerDataFile {internal MemoryStream bagData,inventoryData;}
internal enum StreamModeRead {Persistency}
internal sealed class ItemValue
{
 internal int Code;internal object ItemClass=new object();internal bool IsEmpty()=>Code==0;
 internal static ItemValue ReadOrNull(PooledBinaryReader reader)=>new ItemValue{Code=reader.ReadInt32()&255}; // Disclosed lossy native hydration double.
}
internal sealed class ItemStack {internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue value,int amount){itemValue=value;count=amount;}}
internal sealed class PooledBinaryReader:IDisposable
{
 private BinaryReader reader;internal Encoding Encoding=System.Text.Encoding.UTF8;
 internal void SetBaseStream(Stream stream){reader=new BinaryReader(stream);}
 internal byte ReadByte()=>reader.ReadByte();internal ushort ReadUInt16()=>reader.ReadUInt16();internal short ReadInt16()=>reader.ReadInt16();internal int ReadInt32()=>reader.ReadInt32();
 public void Dispose(){reader?.Dispose();}
}
internal sealed class ReaderPool {internal PooledBinaryReader AllocSync(bool ignored)=>new PooledBinaryReader();}
internal static class MemoryPools {internal static ReaderPool poolBinaryReader=new ReaderPool();}
internal struct Size {internal int x,y;}
internal static class StreamUtils {internal static Size ReadVector2i(PooledBinaryReader reader)=>new Size{x=reader.ReadInt32(),y=reader.ReadInt32()};}
internal sealed class ItemStackGrid
{
 private ItemStack[] slots;internal static int Reads;
 internal static ItemStackGrid Read(PooledBinaryReader reader,StreamModeRead mode)
 {
  Reads++;reader.ReadUInt16();StreamUtils.ReadVector2i(reader);int count=reader.ReadInt16();var grid=new ItemStackGrid{slots=new ItemStack[count]};
  for(int i=0;i<count;i++){int amount=reader.ReadUInt16();grid.slots[i]=new ItemStack(amount==0?new ItemValue():ItemValue.ReadOrNull(reader),amount);}return grid;
 }
 internal ItemStack[] CloneItems()=>slots;
}
internal static class RebirthNativeItemCodec
{
 internal static bool TryDecode(string image,out ItemValue value)
 {byte[] bytes=Convert.FromBase64String(image);value=bytes.Length==4?new ItemValue{Code=BitConverter.ToInt32(bytes,0)}:null;return value!=null;}
}
internal sealed class ChangingBlob:MemoryStream
{
 internal int Mode,MaximumCapacity;internal Action OnCopy;internal ChangingBlob(byte[] bytes):base(bytes,false){}
 public override void WriteTo(Stream destination)
 {
  if(Mode==1){destination.SetLength(4194305);return;}
  if(Mode==2){((MemoryStream)destination).Capacity=4194305;return;}
  if(Mode==3){destination.Position=4194305;destination.WriteByte(1);return;}
  if(Mode==4)
  {
   byte[] chunk=new byte[3*1024*1024];destination.Write(chunk,0,chunk.Length);MaximumCapacity=((MemoryStream)destination).Capacity;
   destination.Write(chunk,0,1024*1024);MaximumCapacity=Math.Max(MaximumCapacity,((MemoryStream)destination).Capacity);return;
  }
  base.WriteTo(destination);OnCopy?.Invoke();
 }
}
internal static class Program
{
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static byte[] Blob(bool bag)
 {
  using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
  {writer.Write((byte)(bag?2:1));writer.Write((ushort)1);writer.Write(1);writer.Write(1);writer.Write((short)1);writer.Write((ushort)2);writer.Write(0x01020304);if(!bag)writer.Write((byte)0);return stream.ToArray();}
 }
 public static void Main()
 {
  var bytes=Blob(true);var data=new PlayerDataFile{bagData=new MemoryStream(bytes,false),inventoryData=new MemoryStream(Blob(false),false)};data.bagData.Position=3;
  var slots=RebirthGearPlayerDataInventory.ReadSlots(data,true);
  Need(slots?.Length==1&&slots[0].count==2&&slots[0].itemValue.Code==0x01020304&&data.bagData.Position==3,"bounded snapshot changed source position or lost canonical native item bytes");
  Need(RebirthGearPlayerDataInventory.ReadSlots(data,false)?.Length==1,"belt footer/read projection refused");
  data.bagData.Dispose();Need(RebirthGearPlayerDataInventory.ReadSlots(data,true)==null,"disposed native blob escaped refusal");
  data.bagData=new MemoryStream(new byte[4194305],false);int prior=ItemStackGrid.Reads;Need(RebirthGearPlayerDataInventory.ReadSlots(data,true)==null&&ItemStackGrid.Reads==prior,"oversized owner blob reached native grid decoder");
  for(int mode=1;mode<=4;mode++)
  {
   var changed=new ChangingBlob(bytes){Mode=mode};data.bagData=changed;prior=ItemStackGrid.Reads;
   Need(RebirthGearPlayerDataInventory.ReadSlots(data,true)==null&&ItemStackGrid.Reads==prior,"changing/oversized snapshot reached parser: "+mode);
   Need(changed.MaximumCapacity<=4194304,"bounded destination overallocated capacity");
  }
  var replacing=new ChangingBlob(bytes);data.bagData=replacing;replacing.OnCopy=()=>data.bagData=new MemoryStream(bytes,false);prior=ItemStackGrid.Reads;
  Need(RebirthGearPlayerDataInventory.ReadSlots(data,true)==null&&ItemStackGrid.Reads==prior,"replacement stream adopted old detached bytes");
  data.bagData=new MemoryStream(bytes,false);Need(RebirthGearPlayerDataInventory.ReadSlots(data,true)?.Length==1,"reader did not recover for a new complete native snapshot");
  Console.WriteLine("PASS extracted bounded native owner reader; disclosed grid/codec adapters, no source/save authentication");
 }
}
