// AUTHORED_NOT_RUN. Runner injects actual deterministic checkpoint method.
// Native rich stack equality is an explicit double; this fixture does not
// qualify native setter, PDS, accepted upload or chunk publication ordering.
using System;using System.Linq;using System.IO;
class ItemStack {internal int Count;internal string Native;internal ItemStack(int count,string native){Count=count;Native=native;}}
class Fixture
{
 // PRODUCTION_READ_BOUNDARY
 // PRODUCTION_STAGE
 sealed class Slot {internal int Area,Index;internal Slot(int area,int index){Area=area;Index=index;}}
 sealed class Original
 {
  internal ItemStack[] BeforeBag,BeforeBelt,AfterBag,AfterBelt;internal Slot[] Slots;
  // PRODUCTION_CHECKPOINT
 }
 static bool Same(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.Count==b.Count&&a.Native==b.Native;
 static ItemStack[] Copy(ItemStack[] items)=>items.Select(x=>new ItemStack(x.Count,x.Native)).ToArray();
 static int assertions;static void Check(bool value,string name){assertions++;if(!value)throw new Exception(name);}
 static void Main()
 {
  var original=new Original{BeforeBag=new[]{new ItemStack(2,"seed-a|mods|metadata"),new ItemStack(5,"untouched")},
   AfterBag=new[]{new ItemStack(4,"seed-a|mods|metadata"),new ItemStack(5,"untouched")},
   BeforeBelt=new[]{new ItemStack(0,"empty"),new ItemStack(1,"tool|wear")},
   AfterBelt=new[]{new ItemStack(1,"attachment|quality|seed-b"),new ItemStack(1,"tool|wear")},
   Slots=new[]{new Slot(0,0),new Slot(1,0)}};
  for(int completed=0;completed<=2;completed++)
  {
   var bag=Copy(completed>0?original.AfterBag:original.BeforeBag);var belt=Copy(completed>1?original.AfterBelt:original.BeforeBelt);
   Check(original.MatchesOwnerCheckpoint(bag,belt,completed),"exact deterministic prefix "+completed);
   foreach(int wrong in Enumerable.Range(0,3).Where(x=>x!=completed))Check(!original.MatchesOwnerCheckpoint(bag,belt,wrong),"checkpoint cannot adopt another prefix");
   bag[1].Count++;Check(!original.MatchesOwnerCheckpoint(bag,belt,completed),"unrelated bag count must remain exact");bag[1].Count--;
   belt[1].Native="tool|other-wear";Check(!original.MatchesOwnerCheckpoint(bag,belt,completed),"unrelated full native item remains exact");
  }
  Check(!original.MatchesOwnerCheckpoint(Copy(original.BeforeBag),Copy(original.AfterBelt),1),"reverse mixed application cannot become a checkpoint");
  var rich=Copy(original.AfterBag);rich[0].Native="seed-c|mods|metadata";
  Check(!original.MatchesOwnerCheckpoint(rich,Copy(original.BeforeBelt),1),"equivalent type/count with another native seed refuses");
  Check(!original.MatchesOwnerCheckpoint(Copy(original.BeforeBag),Copy(original.BeforeBelt),-1),"negative checkpoint refuses");
  Check(!original.MatchesOwnerCheckpoint(Copy(original.AfterBag),Copy(original.AfterBelt),3),"out of range checkpoint refuses");
  Check(ValidOwnerCheckpointStage(1,0,2),"before phase has zero completed setters");
  Check(!ValidOwnerCheckpointStage(1,1,2),"before phase cannot hide a completed setter");
  Check(!ValidOwnerCheckpointStage(2,0,2),"slot phase cannot invent a zero-setter publication");
  Check(ValidOwnerCheckpointStage(2,1,2)&&ValidOwnerCheckpointStage(2,2,2),"slot phase retains each exact completed prefix");
  Check(!ValidOwnerCheckpointStage(3,1,2),"final phase refuses an incomplete prefix");
  Check(ValidOwnerCheckpointStage(3,2,2),"final phase requires all return cells");
  Check(ValidOwnerCheckpointStage(1,0,0)&&ValidOwnerCheckpointStage(3,0,0)&&!ValidOwnerCheckpointStage(2,0,0),"empty return uses real before and final publications without a fictitious setter");
  using(var native=new MemoryStream(new byte[64]))using(var foreign=new MemoryStream(new byte[64]))
  {
   native.Position=native.Length;foreign.Position=foreign.Length;
   Check(CompletedOwnerRead(native,native,true),"actual same native full-consumed read returns");
   Check(!CompletedOwnerRead(native,native,false),"skipped or exceptional native read never issues provenance");
   Check(!CompletedOwnerRead(foreign,native,true),"equal foreign full stream never adopts native read");
   native.Position--;Check(!CompletedOwnerRead(native,native,true),"partial native read remains unknown");
   Check(!CompletedOwnerRead(null,native,true),"absent reader remains unknown");
  }
  Console.WriteLine("PASS "+assertions+" extracted checkpoint assertions; native/save adapters remain unqualified");
 }
}
