using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
// AUTHORED_NOT_RUN. Actual immutable plan codec and saved delta predicate.
internal sealed class ItemStack {internal string Rich="rich-original";internal int count=1;internal bool IsEmpty()=>count==0;}
internal static class RebirthStationGridIngredients {internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.Rich==b.Rich&&a.count==b.count;}
internal static class RebirthCamperInventorySnapshot {
 internal static byte[] Capture(ItemStack[] items){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(items.Length);foreach(var i in items){w.Write(i.Rich);w.Write(i.count);}w.Flush();return m.ToArray();}}
 internal static ItemStack[] Restore(byte[] bytes){using(var m=new MemoryStream(bytes))using(var r=new BinaryReader(m)){var a=new ItemStack[r.ReadInt32()];for(int i=0;i<a.Length;i++)a[i]=new ItemStack{Rich=r.ReadString(),count=r.ReadInt32()};if(m.Position!=m.Length)throw new InvalidDataException();return a;}}
}
internal static class RebirthSurvivorRequestScope {internal static bool TryNormalize(string s,out string normalized){normalized=s;return s=="creation";}}
internal sealed class RebirthStablePlayerIdentity {internal string CanonicalId="owner";}
internal sealed class NativeFile {internal int deaths=2;internal ItemStack[] Bag,Belt;}
internal static class RebirthGearNativePlayerFile {internal static NativeFile File;internal static bool Available=true;internal static int Reads;internal static bool TryRead(RebirthStablePlayerIdentity id,out NativeFile saved){Reads++;saved=File;return Available;}}
internal static class RebirthGearPlayerDataInventory {internal static ItemStack[] ReadSlots(NativeFile file,bool bag)=>bag?file.Bag:file.Belt;}
internal static class RebirthCookingRemoteAdmissionBackend {internal static int Phase=2;internal static bool Present=true,Available=true;internal static bool NativePhase(NativeFile file,string receipt,out float phase,out bool present){phase=Phase;present=Present;return Available;}}
internal static class RebirthCamperStationPlayerSavedSource {
 internal static int Copies;internal static bool Available=true;
 internal static bool TryCopySavedSource(NativeFile file,out ItemStack[] bag,out ItemStack[] belt,out bool[] locks){Copies++;if(!ReferenceEquals(file,RebirthGearNativePlayerFile.File))throw new Exception("different file supplied source");bag=file.Bag;belt=file.Belt;locks=new bool[bag.Length];return Available;}
}
internal static partial class RebirthCookingOwnerDelta {
// ACTUAL_PLAN
// ACTUAL_CODEC_SAVED
 internal static void Exercise(){
  for(int variant=0;variant<8;variant++){
   var p=new Plan{Operation=Guid.NewGuid(),World=Guid.NewGuid().ToString("N"),Owner="owner",Creation="creation",SourceHash=new string('a',64),Deaths=2,
    BeforeBag=new[]{new ItemStack()},AfterBag=new[]{new ItemStack{count=4}},BeforeBelt=new ItemStack[0],AfterBelt=new ItemStack[0]};
   string image=Encode(p);var parsed=Decode(image);Need(parsed.Deaths==2&&Encode(parsed)==image&&parsed.AfterBag[0].count==4,"version 2 rich life plan roundtrip failed");
   var id=new RebirthStablePlayerIdentity();RebirthGearNativePlayerFile.File=new NativeFile{Bag=p.AfterBag,Belt=p.AfterBelt};RebirthGearNativePlayerFile.Available=true;RebirthCookingRemoteAdmissionBackend.Phase=2;RebirthCookingRemoteAdmissionBackend.Present=true;
   Need(Saved(id,p,2),"matching saved refund rejected");
   switch(variant){case 0:RebirthGearNativePlayerFile.File.deaths=1;break;case 1:RebirthGearNativePlayerFile.File.deaths=3;break;case 2:p.Deaths=-1;break;
    case 3:id.CanonicalId="foreign";break;case 4:RebirthGearNativePlayerFile.Available=false;break;case 5:RebirthCookingRemoteAdmissionBackend.Phase=1;break;
    case 6:RebirthCookingRemoteAdmissionBackend.Present=false;break;case 7:RebirthGearNativePlayerFile.File.Bag=new[]{new ItemStack{Rich="foreign-rich",count=4}};break;}
   Need(!Saved(id,p,2),"unqualified saved refund accepted "+variant);
   if(variant==7){RebirthCookingRemoteAdmissionBackend.Phase=3;Need(Saved(id,p,3),"saved retirement receipt revoked by later inventory movement");RebirthGearNativePlayerFile.File.deaths++;Need(!Saved(id,p,3),"saved retirement crossed native life");}
   p.Deaths=-1;string legacy=Encode(p);var old=Decode(legacy);Need(old.Deaths==-1&&Encode(old)==legacy&&!Saved(new RebirthStablePlayerIdentity(),old,2),"legacy plan lost or authorized fresh financial effect");
  }
  var plan=new Plan{Operation=Guid.NewGuid(),World=Guid.NewGuid().ToString("N"),Owner="owner",Creation="creation",SourceHash=new string('a',64),Deaths=2,BeforeBag=new ItemStack[0],AfterBag=new ItemStack[0],BeforeBelt=new ItemStack[0],AfterBelt=new ItemStack[0]};
  foreach(int variant in new[]{0,1,2,3}){
   var root=XElement.Parse(Encode(plan));if(variant==0)root.Attribute("deaths").Remove();if(variant==1)root.SetAttributeValue("deaths",-1);if(variant==2)root.SetAttributeValue("version",3);if(variant==3)root.SetAttributeValue("extra","unknown");
   bool threw=false;try{Decode(root.ToString(SaveOptions.DisableFormatting));}catch{threw=true;}Need(threw,"malformed/unknown plan admitted "+variant);
  }
  plan.Overflow=new[]{new ItemStack{Rich="overflow-rich",count=3}};plan.OverflowOwnerEntity=77;
  string overflowImage=Encode(plan);var overflow=Decode(overflowImage);
  Need(overflow.Deaths==2&&overflow.OverflowOwnerEntity==77&&overflow.Overflow.Length==1&&overflow.Overflow[0].Rich=="overflow-rich"&&overflow.Overflow[0].count==3&&Encode(overflow)==overflowImage,"version 3 frozen overflow/life/native owner identity lost");
  foreach(string key in new[]{"deaths","overflowEntity"}){var node=XElement.Parse(overflowImage);node.Attribute(key).Remove();bool refused=false;try{Decode(node.ToString(SaveOptions.DisableFormatting));}catch{refused=true;}Need(refused,"overflow missing original native scope accepted "+key);}
  plan.Deaths=-1;bool invalid=false;try{Encode(plan);}catch{invalid=true;}Need(invalid,"legacy life cannot author native overflow");
  UnspentCases();
  RejectedCases();
  Console.WriteLine("PASS actual cooking owner delta life codec/saved/unspent predicates; native persistence/cold recovery NOT RUN");
 }
 private static void UnspentCases(){
  for(int variant=0;variant<9;variant++){
   var plan=new Plan{Deaths=2,Owner="owner"};var owner=new RebirthStablePlayerIdentity();
   RebirthGearNativePlayerFile.File=new NativeFile{Bag=new[]{new ItemStack()},Belt=new ItemStack[0]};RebirthGearNativePlayerFile.Available=true;RebirthGearNativePlayerFile.Reads=0;
   RebirthCookingRemoteAdmissionBackend.Available=RebirthCookingRemoteAdmissionBackend.Present=true;RebirthCookingRemoteAdmissionBackend.Phase=0;RebirthCamperStationPlayerSavedSource.Available=true;RebirthCamperStationPlayerSavedSource.Copies=0;
   switch(variant){case 1:RebirthCookingRemoteAdmissionBackend.Present=false;break;case 2:RebirthCookingRemoteAdmissionBackend.Available=false;break;
    case 3:RebirthCookingRemoteAdmissionBackend.Phase=1;break;case 4:RebirthGearNativePlayerFile.File.deaths=1;break;case 5:plan.Deaths=-1;break;
    case 6:owner.CanonicalId="foreign";break;case 7:RebirthGearNativePlayerFile.Available=false;break;case 8:RebirthCamperStationPlayerSavedSource.Available=false;break;}
   bool result=TryReadUnspentSource(owner,plan,out var bag,out var belt,out var locks);
   Need(result==(variant==0||variant==1),"unspent source qualification incorrect "+variant);
   Need(RebirthCamperStationPlayerSavedSource.Copies==((variant==0||variant==1||variant==8)?1:0),"unqualified file hydrated replacement source");
   if(result)Need(RebirthGearNativePlayerFile.Reads==1&&ReferenceEquals(bag,RebirthGearNativePlayerFile.File.Bag)&&locks.Length==bag.Length,"source/phase did not use same native head");
  }
 }
 private static void Need(bool value,string m){if(!value)throw new Exception(m);}
}
internal static class Program {public static void Main()=>RebirthCookingOwnerDelta.Exercise();}
