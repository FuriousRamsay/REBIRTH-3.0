$ErrorActionPreference='Stop'
$source=Get-Content -Raw (Join-Path $PSScriptRoot '../../Scripts/Inventory/RebirthPlayerDataInventory.cs')
Add-Type -TypeDefinition ($source + @'
public enum StreamModeRead {Persistency}
public class PlayerDataFile {public MemoryStream bagData,inventoryData;}
public class ItemStack {public int count;}
public class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){} public void SetBaseStream(Stream s){active=s;} public Stream active; public new ushort ReadUInt16(){return (ushort)(ReadByte()|ReadByte()<<8);} public new short ReadInt16(){return (short)ReadUInt16();} public new byte ReadByte(){int n=active.ReadByte();if(n<0)throw new EndOfStreamException();return (byte)n;}}
public class ReaderPool {public PooledBinaryReader AllocSync(bool reset){return new PooledBinaryReader(new MemoryStream());}}
public static class MemoryPools {public static ReaderPool poolBinaryReader=new ReaderPool();}
public struct Vector2i {public int x,y;} public static class StreamUtils {public static Vector2i ReadVector2i(PooledBinaryReader r){return new Vector2i{x=r.ReadByte(),y=r.ReadByte()};}}
public class ItemStackGrid {public static ItemStack Last;public ItemStack item;public static int Calls; public static ItemStackGrid Read(PooledBinaryReader r,StreamModeRead mode){Calls++;r.ReadUInt16();StreamUtils.ReadVector2i(r);r.ReadInt16();Last=new ItemStack{count=r.ReadByte()};return new ItemStackGrid{item=Last};} public ItemStack[] CloneItems(){return new[]{new ItemStack{count=item.count}};}}
public static class Checks {
 static void A(bool c,string n){if(!c)throw new Exception(n);}
 public static void Run(){
 var d=new PlayerDataFile{bagData=new MemoryStream(new byte[]{2,2,0,1,1,1,0,9}),inventoryData=new MemoryStream(new byte[]{1,2,0,1,1,1,0,7,3})};d.bagData.Position=1;
 var b=RebirthPlayerDataInventory.ReadSlots(d,true);A(b!=null&&b[0].count==9,"bag framing");A(d.bagData.Position==1,"input position mutated");b[0].count=1;A(ItemStackGrid.Last.count==9,"snapshot aliased");
 A(RebirthPlayerDataInventory.ReadSlots(d,false)[0].count==7,"belt selected byte not consumed");
 d.inventoryData=new MemoryStream(new byte[]{1,2,0,1,1,1,0,7});A(RebirthPlayerDataInventory.ReadSlots(d,false)==null,"truncated selected byte accepted");
 d.bagData=new MemoryStream(new byte[]{2,2,0,1,1,1,0,9,0});A(RebirthPlayerDataInventory.ReadSlots(d,true)==null,"trailing bytes accepted");
 d.bagData=new MemoryStream(new byte[]{3,9});A(RebirthPlayerDataInventory.ReadSlots(d,true)==null,"unknown wrapper accepted");
 int calls=ItemStackGrid.Calls; d.bagData=new MemoryStream(new byte[]{2,2,0,170,1,170,0,9});A(RebirthPlayerDataInventory.ReadSlots(d,true)==null && ItemStackGrid.Calls==calls,"oversized dimensions reached decoder");
 d.bagData=new MemoryStream(new byte[]{2,2,0,2,2,1,0,9});A(RebirthPlayerDataInventory.ReadSlots(d,true)==null && ItemStackGrid.Calls==calls,"dimension count mismatch reached decoder");
 d.bagData=new MemoryStream(new byte[4194305]);A(RebirthPlayerDataInventory.ReadSlots(d,true)==null,"unbounded blob accepted");
 A(RebirthPlayerDataInventory.ReadSlots(null,true)==null,"missing data accepted");
 Console.WriteLine("PASS actual native snapshot adapter: detachment, unchanged source position, wrapper framing, truncated/trailing/unknown/oversized/null fail closed; native grid codec doubled.");
 }
}
'@)
[Checks]::Run()
