// AUTHORED_NOT_RUN. Actual classification/span methods are injected by runner.
// Native Load/ToPlayer, rich-stack codec, buffs and immutable primary storage
// are explicit doubles. These cases do not qualify the native hook/install chain.
using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Xml.Linq;
class Fixture
{
 class ItemStack{internal int Count;internal string Native;internal ItemStack(int count,string native){Count=count;Native=native;}}
 class Slot{internal int Area,Index;internal Slot(int area,int index){Area=area;Index=index;}}
 class Buffs{internal Dictionary<string,float> Values=new Dictionary<string,float>();internal float GetCustomVar(string key)=>Values.TryGetValue(key,out var value)?value:0;}
 class Actor{internal Buffs Buffs=new Buffs();}
 class Original
 {
  internal Guid Operation=Guid.NewGuid();internal string WorldKey="actual-world",Receipt="receipt",Checkpoint="checkpoint";internal byte[] Image;
  internal ItemStack[] BeforeBag,AfterBag,BeforeBelt,AfterBelt;internal Slot[] Slots;internal bool Live=true;internal bool Current()=>Live;
  // PRODUCTION_CHECKPOINT
 }
 class LoadedOwner
 {
  internal byte[] buffs;internal Actor actor=new Actor();internal ItemStack[] bag,belt;internal bool Live=true;internal bool Current()=>Live;
  internal bool MatchesOriginalCheckpoint(Original original,int phase,int completed)=>Live&&PhaseBytes(buffs,original.Receipt,out float p)&&p==phase&&
   PhaseBytes(buffs,original.Checkpoint,out float n)&&n==completed&&actor.Buffs.GetCustomVar(original.Receipt)==phase&&
   actor.Buffs.GetCustomVar(original.Checkpoint)==completed&&original.MatchesOwnerCheckpoint(bag,belt,completed);
 }
 class LoadedStation{internal string Stage="after";internal bool Live=true;internal bool Current()=>Live;}
 static class RebirthWorkstationCompletionOutbox{internal static string Root(string role)=>"actual-original-root";}
 static class RebirthWorkstationCompletionPublication{internal static XElement Attempt;internal static XElement ReadPrimary(string path)=>Attempt;}
 // Explicit buff codec double; classification receives frozen native stamps.
 static bool PhaseBytes(byte[] bytes,string key,out float value){value=BitConverter.ToInt32(bytes,key=="receipt"?0:4);return true;}
 static bool Same(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.Count==b.Count&&a.Native==b.Native;
 static bool UnresolvedAttempt;
 static bool HasUnresolvedOwnerSlotAttempt(Original original,int completed)=>UnresolvedAttempt&&completed<original.Slots.Length;
 // PRODUCTION_STAGE
 // PRODUCTION_UNIQUE_SPAN
 // PRODUCTION_TILE_SPAN
 // PRODUCTION_CLASSIFY
 static ItemStack[] Copy(ItemStack[] a)=>a.Select(x=>new ItemStack(x.Count,x.Native)).ToArray();
 static int assertions;static void Check(bool value,string name){assertions++;if(!value)throw new Exception(name);}
 static void Stamp(LoadedOwner owner,int phase,int completed)
 {owner.buffs=BitConverter.GetBytes(phase).Concat(BitConverter.GetBytes(completed)).ToArray();owner.actor.Buffs.Values["receipt"]=phase;owner.actor.Buffs.Values["checkpoint"]=completed;}
 static XElement Attempt(Original o,int ordinal)=>new XElement("workstationCompletionIntent",new XAttribute("operation",o.Operation.ToString("N")),new XAttribute("world",o.WorldKey),
  new XElement("before",XElement.Parse(Encoding.UTF8.GetString(o.Image))),new XElement("recyclerOwnerPublication",new XElement("slotAttempt",new XAttribute("ordinal",ordinal),
   new XAttribute("area",o.Slots[ordinal].Area),new XAttribute("slot",o.Slots[ordinal].Index))));
 static void Main()
 {
  using(var stream=new MemoryStream(new byte[]{9,1,2,1,2,3,9}))
  {stream.Position=3;Check(HasUniqueNativeSpan(stream,new byte[]{1,2,3})&&stream.Position==3,"unique actual marker span preserves native reader");
   Check(!HasUniqueNativeSpan(stream,new byte[]{1,2})&&stream.Position==3,"duplicate marker never selects an original");
   Check(!HasUniqueNativeSpan(stream,new byte[]{8})&&stream.Position==3,"absent marker is no financial proof");
   Check(MatchesNativeTileSpan(stream,new byte[]{1,2,3})&&stream.Position==3,"exact native factory span preserves reader");
   Check(!MatchesNativeTileSpan(stream,new byte[]{1,2,4})&&stream.Position==3,"different native factory span refuses");}
  var o=new Original{BeforeBag=new[]{new ItemStack(1,"rich-source"),new ItemStack(3,"unrelated")},AfterBag=new[]{new ItemStack(2,"rich-source"),new ItemStack(3,"unrelated")},
   BeforeBelt=new[]{new ItemStack(0,"empty")},AfterBelt=new[]{new ItemStack(1,"rich-attachment")},Slots=new[]{new Slot(0,0),new Slot(1,0)}};
  o.Image=Encoding.UTF8.GetBytes(new XElement("placedRecyclerOriginal",new XAttribute("operation",o.Operation.ToString("N"))).ToString(SaveOptions.DisableFormatting));
  var owner=new LoadedOwner{bag=Copy(o.BeforeBag),belt=Copy(o.BeforeBelt)};var station=new LoadedStation();
  Stamp(owner,1,0);Check(TryClassifyLoadedOwner(owner,station,o,out int phase,out int n,out bool observed)&&phase==1&&n==0&&!observed,"exact persisted before checkpoint");
  UnresolvedAttempt=true;Check(!TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed),"saved older prefix cannot replay an entered unsaved next setter");UnresolvedAttempt=false;
  owner.bag=Copy(o.AfterBag);RebirthWorkstationCompletionPublication.Attempt=null;
  Check(!TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed),"next rich slot without exact attempted original remains unknown");
  RebirthWorkstationCompletionPublication.Attempt=Attempt(o,0);
  Check(TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed)&&n==1&&observed,"actual saved next slot with old stamp advances without setter replay");
  RebirthWorkstationCompletionPublication.Attempt.Element("recyclerOwnerPublication").Element("slotAttempt").SetAttributeValue("ordinal",1);
  Check(!TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed),"another original slot attempt refuses");
  RebirthWorkstationCompletionPublication.Attempt=Attempt(o,0);station.Stage="before";
  Check(!TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed),"unspent native principal cannot release a returned slot");station.Stage="after";
  owner.bag[1].Count++;Check(!TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed),"unrelated native owner cell change refuses");owner.bag[1].Count--;
  owner.bag[0].Native="another-native-seed";Check(!TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed),"same count with different rich item refuses");
  owner.bag=Copy(o.AfterBag);owner.belt=Copy(o.AfterBelt);Stamp(owner,3,2);
  Check(TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed)&&phase==3&&n==2&&!observed,"actual persisted final checkpoint");
  Stamp(owner,3,1);Check(!TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed),"final stamp cannot hide incomplete checkpoint membership");
  owner.bag=Copy(o.BeforeBag);owner.belt=Copy(o.BeforeBelt);Stamp(owner,0,0);station.Stage="before";
  Check(TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed)&&phase==0&&!observed,"actual full native unused pair can start before publication");
  station.Stage="after";Check(!TryClassifyLoadedOwner(owner,station,o,out phase,out n,out observed),"zero stamp with spent station remains unknown");
  Console.WriteLine("PASS "+assertions+" extracted recycler cold boundary assertions; native producers remain unqualified");
 }
}
