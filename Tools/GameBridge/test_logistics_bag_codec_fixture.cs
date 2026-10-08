using System;using System.IO;
class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream s):base(s){}}
class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s){}}
struct Vector2i{public int x,y;public Vector2i(int a,int b){x=a;y=b;}}
class XUiC_ItemStack{public enum StackLocationTypes{Backpack}}
class ItemStack{public int type,count,seed;public string metadata="";public ItemStack Clone(){return new ItemStack{type=type,count=count,seed=seed,metadata=metadata};}public void Read(PooledBinaryReader r){type=r.ReadInt32();count=r.ReadInt32();seed=r.ReadInt32();metadata=r.ReadString();}public void Write(PooledBinaryWriter w){w.Write(type);w.Write(count);w.Write(seed);w.Write(metadata);}}
class Grid{public ItemStack[] items;public int Length{get{return items.Length;}}public ItemStack this[int i]{get{return items[i];}set{items[i]=value.Clone();}}}
class Bag{public static int largest,allocations;public object Preferences,Locks;public Grid ItemGrid;public Bag(Vector2i size,XUiC_ItemStack.StackLocationTypes location,object holder){int n=checked(size.x*size.y);allocations++;largest=Math.Max(largest,n);ItemGrid=new Grid{items=new ItemStack[n]};for(int i=0;i<n;i++)ItemGrid.items[i]=new ItemStack();}}
// CODEC
class Check{
static void A(bool value,string message){if(!value)throw new Exception(message);}
static byte[] Data(Action<PooledBinaryWriter> action){using(var m=new MemoryStream()){var w=new PooledBinaryWriter(m);action(w);w.Flush();return m.ToArray();}}
static Bag Read(byte[] data){using(var m=new MemoryStream(data)){Bag result=LogisticsBagCodec.Read(new PooledBinaryReader(m));A(m.Position==m.Length,"trailing data");return result;}}
static void Reject(byte[] data){try{Read(data);throw new Exception("invalid accepted");}catch(InvalidDataException){}catch(EndOfStreamException){}}
static int Main(){
foreach(int n in new[]{0,1,52,256}){
var source=new Bag(new Vector2i(n,1),XUiC_ItemStack.StackLocationTypes.Backpack,null){Preferences=new object(),Locks=new object()};
for(int i=0;i<n;i++){source.ItemGrid[i]=new ItemStack{type=i+1,count=i+2,seed=i+3,metadata="modded 日本語 "+i};}
Bag copy=LogisticsBagCodec.Capture(source);A(copy.ItemGrid.Length==n&&copy.Preferences==null&&copy.Locks==null,"captured irrelevant metadata");
if(n>0){source.ItemGrid[0].count=900;A(copy.ItemGrid[0].count==2,"capture not detached");}
byte[] bytes=Data(w=>LogisticsBagCodec.Write(w,copy));A(bytes[0]==3,"wrong envelope");Bag received=Read(bytes);
for(int i=0;i<n;i++)A(received.ItemGrid[i].type==i+1&&received.ItemGrid[i].count==i+2&&received.ItemGrid[i].seed==i+3&&received.ItemGrid[i].metadata=="modded 日本語 "+i,"item witness changed");
for(int len=0;len<bytes.Length;len++){byte[] cut=new byte[len];Array.Copy(bytes,cut,len);Reject(cut);}
}
int before=Bag.allocations;foreach(byte version in new byte[]{0,1,2,4,255})Reject(new[]{version});Reject(Data(w=>{w.Write((byte)3);w.Write((ushort)257);}));Reject(Data(w=>{w.Write((byte)3);w.Write(ushort.MaxValue);}));A(Bag.allocations==before,"invalid count/version allocated bag");A(Bag.largest<=256,"accepted excessive allocation");
var oversized=new Bag(new Vector2i(257,1),XUiC_ItemStack.StackLocationTypes.Backpack,null);try{LogisticsBagCodec.Capture(oversized);throw new Exception("oversize capture");}catch(InvalidDataException){}using(var m=new MemoryStream()){try{LogisticsBagCodec.Write(new PooledBinaryWriter(m),oversized);throw new Exception("oversize writer");}catch(InvalidDataException){}A(m.Length==0,"oversize writer emitted partial snapshot");}
A(LogisticsBagCodec.Capture(null)==null,"optional capture changed");Console.WriteLine("PASS actual logistics snapshot codec v3: bounded0/1/52/256slots, detached capture and full adapter item fields/Unicode, symmetric write/read, every truncation, old/native/unknown versions and oversized counts refused before bag allocation, optional null preserved. Native ItemStack/grid bindings doubled; not native Bag persistence.");return 0;}}