using System;using System.IO;using System.Collections.Generic;
// Actual codec control flow, disclosed native item/registry doubles.
public class EntityPlayer{}
public class ItemClass{public int MaxCount=5;public string GetItemName(){return "pack";}}
public class ItemValue{
 public int type;public ItemClass ItemClass=new ItemClass();public string Payload="";
 public Dictionary<string,string> Meta=new Dictionary<string,string>();
 public Dictionary<string,string> Metadata {get{return Meta;}}
 public bool TryGetMetadata(string key,out string value){return Meta.TryGetValue(key,out value);}
 public void SetMetadata(string key,string value){Meta[key]=value;}
 public ItemValue Clone(){return new ItemValue{type=type,Payload=Payload,Meta=new Dictionary<string,string>(Meta)};}
}
public class ItemStack{
 public int count;public ItemValue itemValue=new ItemValue();
 public bool IsEmpty(){return count<1||itemValue.type==0;}
 public static ItemStack[] CreateArray(int n){var a=new ItemStack[n];for(int i=0;i<n;i++)a[i]=new ItemStack();return a;}
 public void Write(BinaryWriter w){w.Write((ushort)count);if(count>0){w.Write(itemValue.type);w.Write(itemValue.Payload);}}
 public void Read(BinaryReader r){count=r.ReadUInt16();if(count>0){itemValue.type=r.ReadInt32();itemValue.Payload=r.ReadString();}}
}
public static class RebirthSurvivorGearService{public static bool TryGetEquippedBackpackItem(EntityPlayer p,out ItemValue v){v=null;return false;}}
public static class RebirthBackpackLibraryPolicy{public static int CapacityForBackpack(string id){return 6;}public static bool IsLearningMaterial(ItemValue v){return v!=null&&v.type==1;}}
// PRODUCTION_CLASS
public static class Check{
 static void A(bool b,string n){if(!b)throw new Exception(n);}
 public static void Main(){
 var pack=new ItemValue{type=8};ItemStack[] c;ItemValue next;
 A(RebirthBackpackLibraryContents.TryRead(pack,out c)&&c.Length==6,"old pack empty defaults");
 c[2].count=3;c[2].itemValue.type=1;c[2].itemValue.Payload="seed/provenance";
 A(RebirthBackpackLibraryContents.TryWrite(pack,c,out next)&&pack.Meta.Count==0,"detached encode");
 A(RebirthBackpackLibraryContents.TryRead(next,out c)&&c[2].count==3&&c[2].itemValue.Payload=="seed/provenance","roundtrip");
 c[0].count=1;c[0].itemValue.type=0;A(!RebirthBackpackLibraryContents.TryWrite(next,c,out pack)&&pack==null,"positive empty identity rejected");
 c[0].itemValue.type=1;c[0].count=6;A(!RebirthBackpackLibraryContents.TryWrite(next,c,out pack)&&pack==null,"overstack write refused");c[0].count=1;
 c[0].itemValue.type=2;A(!RebirthBackpackLibraryContents.TryWrite(next,c,out pack),"nonlearning rejected");
 string encoded=next.Meta[RebirthBackpackLibraryContents.MetadataKey];byte[] bytes=Convert.FromBase64String(encoded);
 var excess=(byte[])bytes.Clone();excess[6]=6;next.SetMetadata(RebirthBackpackLibraryContents.MetadataKey,Convert.ToBase64String(excess));A(!RebirthBackpackLibraryContents.TryRead(next,out c)&&c==null,"overstack read refused");
 bytes[0]=2;next.SetMetadata(RebirthBackpackLibraryContents.MetadataKey,Convert.ToBase64String(bytes));A(!RebirthBackpackLibraryContents.TryRead(next,out c)&&c==null,"unsupported version");
 bytes[0]=1;bytes[1]=7;next.SetMetadata(RebirthBackpackLibraryContents.MetadataKey,Convert.ToBase64String(bytes));A(!RebirthBackpackLibraryContents.TryRead(next,out c),"capacity mismatch");
 next.SetMetadata(RebirthBackpackLibraryContents.MetadataKey,encoded+"!");A(!RebirthBackpackLibraryContents.TryRead(next,out c),"invalid base64");
 bytes=Convert.FromBase64String(encoded);Array.Resize(ref bytes,bytes.Length+1);next.SetMetadata(RebirthBackpackLibraryContents.MetadataKey,Convert.ToBase64String(bytes));A(!RebirthBackpackLibraryContents.TryRead(next,out c),"trailing bytes");
 next.SetMetadata(RebirthBackpackLibraryContents.MetadataKey,new string('A',65537));A(!RebirthBackpackLibraryContents.TryRead(next,out c),"bounded payload");
 var crowded=new ItemValue{type=8};for(int i=0;i<255;i++)crowded.Meta["field"+i]="value";
 var empty=ItemStack.CreateArray(6);A(!RebirthBackpackLibraryContents.TryWrite(crowded,empty,out pack)&&pack==null&&crowded.Meta.Count==255,"new library field exceeds native byte limit without mutation");
 crowded.Meta.Remove("field254");A(RebirthBackpackLibraryContents.TryWrite(crowded,empty,out pack)&&pack.Metadata.Count==255,"last native metadata field allowed");
 A(RebirthBackpackLibraryContents.TryWrite(pack,empty,out next)&&next.Metadata.Count==255,"replace existing library field at limit");
 crowded.Meta["a"]="v";crowded.Meta["b"]="v";A(!RebirthBackpackLibraryContents.TryRead(crowded,out c),"oversized backpack metadata refused");
 empty[0].count=1;empty[0].itemValue.type=1;for(int i=0;i<256;i++)empty[0].itemValue.Meta["field"+i]="value";
 A(!RebirthBackpackLibraryContents.TryWrite(new ItemValue{type=8},empty,out next)&&next==null,"oversized stored item metadata refused");
 Console.WriteLine("PASS actual library codec: detached roundtrip/count/payload, absent defaults, malformed/version/capacity/nonlearning/positive-empty/trailing/size rejection. Native serializer and custody not exercised.");
 }
}