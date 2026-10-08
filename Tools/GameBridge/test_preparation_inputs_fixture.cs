using System;
class ItemValue {public string Metadata;public int Seed,Quality;public bool Equals(ItemValue other){return other!=null&&Metadata==other.Metadata&&Seed==other.Seed&&Quality==other.Quality;}}
class ItemStack {public int count;public ItemValue itemValue;}
class Slot {public ItemStack ItemStack;}
class Workspace {
 internal ItemStack[] preparationInputs;internal Slot[] slots;internal int preparationBatch,batch;
// PRODUCTION_CLASS
 internal bool Matches()=>PreparationInputsMatch();
}
class Check {
 static ItemStack Stack(string metadata="A",int seed=12,int quality=2,int count=4)=>new ItemStack{count=count,itemValue=new ItemValue{Metadata=metadata,Seed=seed,Quality=quality}};
 static void Assert(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Main(){var w=new Workspace{batch=2,preparationBatch=2,slots=new[]{new Slot{ItemStack=Stack()}},preparationInputs=new[]{Stack()}};Assert(w.Matches(),"Equal native payload accepted");w.slots[0].ItemStack.itemValue.Metadata="B";Assert(!w.Matches(),"Same-type/count metadata change cancels");w.slots[0].ItemStack=Stack(seed:13);Assert(!w.Matches(),"Seed change cancels");w.slots[0].ItemStack=Stack(quality:3);Assert(!w.Matches(),"Quality change cancels");w.slots[0].ItemStack=Stack(count:5);Assert(!w.Matches(),"Count change cancels");w.slots[0].ItemStack=Stack();w.batch=3;Assert(!w.Matches(),"Batch change cancels");w.batch=2;w.slots[0].ItemStack.itemValue=null;Assert(!w.Matches(),"Malformed item rejected");w.slots[0]=null;Assert(!w.Matches(),"Missing slot rejected");w.preparationInputs=null;Assert(!w.Matches(),"Cancelled snapshot rejected");Console.WriteLine("PASS actual PreparationInputsMatch with disclosed native equality double: full payload, metadata, seed, quality, counts, batch and missing snapshots");}
}
