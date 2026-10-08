class ItemClass {public string Name;public string GetItemName(){return Name;}}
class ItemValue {public ItemClass ItemClass;public int Quality;public float Wear;public string Metadata;public bool IsEmpty(){return ItemClass==null;}public ItemValue Clone(){return new ItemValue{ItemClass=new ItemClass{Name=ItemClass.Name},Quality=Quality,Wear=Wear,Metadata=Metadata};}}
class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int c){itemValue=v;count=c;}}
class RebirthNativeItemCodec {
 public static bool FailDecode,WrongKey,MutateEncoding;
 public static string Encode(ItemValue value){using(var stream=new System.IO.MemoryStream())using(var writer=new System.IO.BinaryWriter(stream)){writer.Write(value.ItemClass.Name);writer.Write(value.Quality);writer.Write(value.Wear);writer.Write(value.Metadata);writer.Flush();if(MutateEncoding)value.Wear=999;return Convert.ToBase64String(stream.ToArray());}}
 public static bool TryDecode(string text,out ItemValue value){value=null;if(FailDecode)return false;try{using(var stream=new System.IO.MemoryStream(Convert.FromBase64String(text)))using(var reader=new System.IO.BinaryReader(stream)){value=new ItemValue{ItemClass=new ItemClass{Name=reader.ReadString()},Quality=reader.ReadInt32(),Wear=reader.ReadSingle(),Metadata=reader.ReadString()};if(WrongKey)value.ItemClass.Name="other";return true;}}catch{return false;}}
}
public class NativeStackFixture {
 public static string Run(){int checks=0;var owner=RebirthNpcStableId.NewId();var id=Guid.NewGuid();var value=new ItemValue{ItemClass=new ItemClass{Name="weapon"},Quality=5,Wear=17.5f,Metadata="mods|nested<&>"};var source=new ItemStack(value,3);RebirthNpcNativeStackRecord record;
 Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks++;};
 check(RebirthNpcNativeStackRecord.TryCapture(id,owner,source,out record)&&record.StackId==id&&record.Owner==owner.ToString()&&record.Count==3,"capture owner/stack/quantity");
 ItemStack decoded;check(record.TryDecode(out decoded)&&decoded.itemValue.Quality==5&&decoded.itemValue.Wear==17.5f&&decoded.itemValue.Metadata==value.Metadata&&decoded.count==3,"quality wear metadata quantity preserved");
 value.Wear=88;value.Metadata="changed";source.count=1;check(record.TryDecode(out decoded)&&decoded.itemValue.Wear==17.5f&&decoded.itemValue.Metadata=="mods|nested<&>"&&decoded.count==3,"source mutation cannot alter record");
 decoded.itemValue.Metadata="modified result";check(record.TryDecode(out decoded)&&decoded.itemValue.Metadata=="mods|nested<&>","decode results independent");
 var xml=record.Write();xml.SetAttributeValue("count",99);check(record.Count==3,"XML detached");
 RebirthNativeItemCodec.MutateEncoding=true;value.Wear=20;check(RebirthNpcNativeStackRecord.TryCapture(Guid.NewGuid(),owner,source,out var copy)&&value.Wear==20,"encoder gets clone");RebirthNativeItemCodec.MutateEncoding=false;
 RebirthNativeItemCodec.FailDecode=true;check(!RebirthNpcNativeStackRecord.TryCapture(id,owner,source,out copy)&&copy==null,"capture refuses unresolved payload");check(!record.TryDecode(out decoded)&&decoded==null,"decode failure no output");RebirthNativeItemCodec.FailDecode=false;
 RebirthNativeItemCodec.WrongKey=true;check(!RebirthNpcNativeStackRecord.TryCapture(id,owner,source,out copy)&&copy==null,"capture refuses resolved key mismatch");check(!record.TryDecode(out decoded)&&decoded==null,"decode key mismatch no output");RebirthNativeItemCodec.WrongKey=false;
 check(!RebirthNpcNativeStackRecord.TryCapture(Guid.Empty,owner,source,out copy),"missing stack id");check(!RebirthNpcNativeStackRecord.TryCapture(id,default(RebirthNpcStableId),source,out copy),"missing owner");
 source.count=0;check(!RebirthNpcNativeStackRecord.TryCapture(id,owner,source,out copy),"empty stack");source.count=1;source.itemValue=null;check(!RebirthNpcNativeStackRecord.TryCapture(id,owner,source,out copy),"missing ItemValue");
  RebirthNpcNativeStackSet set;check(RebirthNpcNativeStackSet.TryCreate(owner,7,new[]{record},out set)&&set.Owner==owner.ToString()&&set.Revision==7,"owner-bound set");
 check(!RebirthNpcNativeStackSet.TryCreate(owner,7,new[]{record,record},out set)&&set==null,"duplicate stack ID refused");
 check(!RebirthNpcNativeStackSet.TryCreate(RebirthNpcStableId.NewId(),7,new[]{record},out set),"foreign owner refused");
 check(RebirthNpcNativeStackSet.TryCreate(owner,0,new RebirthNpcNativeStackRecord[0],out set),"empty initial set");
 var setImage=set.Write();setImage.SetAttributeValue("revision",99);check(set.Revision==0,"detached set image");
 setImage=set.Write();setImage.SetAttributeValue("extra","x");check(!RebirthNpcNativeStackSet.TryRead(setImage,out set)&&set==null,"unknown set attribute refused");
 var huge=record.Write();huge.SetAttributeValue("count",int.MaxValue);RebirthNpcNativeStackRecord hugeRecord;check(RebirthNpcNativeStackRecord.TryRead(huge,out hugeRecord),"maximum record quantity parses");
 var second=record.Write();second.SetAttributeValue("id",Guid.NewGuid().ToString("N"));RebirthNpcNativeStackRecord secondRecord;RebirthNpcNativeStackRecord.TryRead(second,out secondRecord);
 check(!RebirthNpcNativeStackSet.TryCreate(owner,1,new[]{hugeRecord,secondRecord},out set),"semantic quantity total overflow refused");
 check(RebirthNpcNativeStackSet.TryCreate(owner,1,new[]{record,secondRecord},out set),"distinct native instances retained");
 var firstImage=set.Write();RebirthNpcNativeStackSet parsed;check(RebirthNpcNativeStackSet.TryRead(firstImage,out parsed)&&System.Xml.Linq.XNode.DeepEquals(firstImage,parsed.Write()),"deterministic set roundtrip");
 return "PASS "+checks+" whole actual native-stack class and actual stable-ID checks; ItemValue/stack/shared codec adapters doubled";
 }
}