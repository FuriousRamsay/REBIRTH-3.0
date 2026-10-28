// Native stack codec/max-stack doubles only. This does NOT prove accepted
// spawn identity, native upload/file publication, markers, guards or settlement.
public sealed class Number{public int Value=10;}
public sealed class ItemClass{public Number Stacknumber=new Number();}
public sealed class ItemValue{public string Metadata;public ItemClass ItemClass=new ItemClass();}
public sealed class ItemStack
{
 public int count;public ItemValue itemValue=new ItemValue();public static ItemStack Empty=>new ItemStack();
 public bool IsEmpty()=>count==0;public ItemStack Clone()=>new ItemStack{count=count,itemValue=new ItemValue{Metadata=itemValue.Metadata,ItemClass=itemValue.ItemClass}};
 public static ItemStack[] Clone(ItemStack[] value)=>value.Select(item=>item.Clone()).ToArray();
 public bool CanStackPartlyWith(ItemStack value,out int capacity){capacity=itemValue.Metadata==value.itemValue.Metadata?Math.Max(0,itemValue.ItemClass.Stacknumber.Value-count):0;return capacity>0;}
}
public static class RebirthNativeItemCodec{public static string Encode(ItemValue value)=>Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value.Metadata??string.Empty));}
public static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&(a.count==0||a.itemValue.Metadata==b.itemValue.Metadata);}
public static class RebirthLogisticsTransferJournal{public sealed class Record{public ItemStack[] OriginalOwnerBag,OriginalOwnerBelt,BeforeOwner,DebitedOwner,AfterOwner;public string OwnerDebitProof;public int Outcome;}}
public static class RebirthLogisticsDeathJournal{/* ACTUAL_DEATH_BOUNDS */public sealed class Parcel{public ItemStack[] Before,After;public bool[] Locks;}public sealed class Record{public ItemStack[] Before,After,PreservedOwed;public bool[] Locks;public int OwnerPhase,Outcome;public List<Parcel> Parcels=new List<Parcel>();}}
public static class MpADeathPartitionFixture
{
 static ItemStack S(string metadata,int count)=>new ItemStack{count=count,itemValue=new ItemValue{Metadata=metadata}};
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main()
 {
  var original=new RebirthLogisticsTransferJournal.Record{OriginalOwnerBag=new[]{S("q6:ammo",4),S("untouched",1)},OriginalOwnerBelt=new[]{S("belt:rich",1)},BeforeOwner=new[]{S("q6:ammo",4),ItemStack.Empty},DebitedOwner=new[]{S("q6:ammo",2),ItemStack.Empty},AfterOwner=new[]{S("q6:ammo",2),ItemStack.Empty},Outcome=1};
  Check(RebirthLogisticsDeathPartition.TryExpected(original,2,out var expected)&&expected[0].count==2&&expected[1].count==1,"exact sparse debit projects over whole original bag");
  Check(RebirthLogisticsDeathPartition.SurvivedUnchanged(original,2,expected,original.OriginalOwnerBelt),"None-mode survival accepts exact original paid native owner image");
  Check(!RebirthLogisticsDeathPartition.SurvivedUnchanged(original,2,new[]{S("q6:ammo",1),S("untouched",1)},original.OriginalOwnerBelt),"None-mode random native loss is not an unchanged survival receipt");
  Check(!RebirthLogisticsDeathPartition.SurvivedUnchanged(original,2,new[]{S("untouched",1),S("q6:ammo",2)},original.OriginalOwnerBelt),"survival requires exact slots, not only matching totals");
  Check(!RebirthLogisticsDeathPartition.SurvivedUnchanged(original,2,expected,new[]{S("belt:degraded",1)}),"belt degradation never becomes an unchanged survival proof");
  Check(RebirthLogisticsDeathPartition.Conserved(expected,original.OriginalOwnerBelt,new[]{S("q6:ammo",2),S("untouched",1)},new[]{ItemStack.Empty,ItemStack.Empty},new[]{S("belt:rich",1)}),"accepted original bag plus remaining native belt conserves");
  Check(!RebirthLogisticsDeathPartition.Conserved(expected,original.OriginalOwnerBelt,new[]{S("q1:ammo",2),S("untouched",1)},new[]{ItemStack.Empty,ItemStack.Empty},new[]{S("belt:rich",1)}),"same quantities different native metadata refuse");
  Check(!RebirthLogisticsDeathPartition.Conserved(expected,original.OriginalOwnerBelt,new[]{S("q6:ammo",1),S("untouched",1)},new[]{ItemStack.Empty,ItemStack.Empty},new[]{S("belt:rich",1)}),"random loss stays unknown");
  var death=new RebirthLogisticsDeathJournal.Record{Before=new[]{S("q6:ammo",2),S("untouched",1)},Locks=new[]{false,false},OwnerPhase=2,Outcome=1};
  Check(RebirthLogisticsDeathPartition.TryPlan(original,death,out var after)&&after[0].count==2,"paid deposit never refunds committed debit");
  original.AfterOwner[0].count=6;
  Check(RebirthLogisticsDeathPartition.TryPlan(original,death,out after)&&after[0].count==6&&after[1].itemValue.Metadata=="untouched","only original retained owed positive credits enter physical backpack");
  original.Outcome=death.Outcome=2;
  Check(RebirthLogisticsDeathPartition.TryPlan(original,death,out after)&&after[0].count==4,"proved abort refunds exact original debit once into original physical custody");
  death.OwnerPhase=3;
  Check(RebirthLogisticsDeathPartition.TryPlan(original,death,out after)&&after[0].count==2,"native phase3 already delivered so no second credit");
  death.OwnerPhase=2;original.Outcome=death.Outcome=1;death.Before=new[]{S("full:unrelated",10)};death.Locks=new[]{false};
  Check(!RebirthLogisticsDeathPartition.TryPlan(original,death,out after),"full original physical backpack cannot invent capacity");
  death.Parcels.Add(new RebirthLogisticsDeathJournal.Parcel{Before=new[]{ItemStack.Empty,ItemStack.Empty},Locks=new[]{false,false}});
  Check(RebirthLogisticsDeathPartition.TryPlanAll(original,death,out var all)&&all[0][0].itemValue.Metadata=="full:unrelated"&&all[1][0].itemValue.Metadata=="q6:ammo"&&all[1][0].count==4,"one original owed delta spans approved native parcel capacity");
  death.Parcels.Add(new RebirthLogisticsDeathJournal.Parcel{Before=new[]{ItemStack.Empty},Locks=new[]{false}});
  Check(!RebirthLogisticsDeathPartition.TryPlanAll(original,death,out all),"additional parcels cannot exceed actual original owed native-stack budget");
  // Byte-fit callback is a disclosed capacity double. Production supplies an
  // actual detached native Bag/ECD serializer, not this character budget.
  original.BeforeOwner=original.DebitedOwner=new[]{ItemStack.Empty,ItemStack.Empty};original.AfterOwner=new[]{S("rich-A",1),S("rich-B",1)};
  death=new RebirthLogisticsDeathJournal.Record{Before=new[]{ItemStack.Empty,ItemStack.Empty,ItemStack.Empty},Locks=new[]{false,false,false},OwnerPhase=2,Outcome=1};
  bool Fits(int body,ItemStack[] image)=>image.Where(item=>!item.IsEmpty()).Sum(item=>item.itemValue.Metadata.Length)<=6;
  Check(!RebirthLogisticsDeathPartition.TryPlanAll(original,death,out all,Fits),"slot-valid rich plan must honor actual supplied body byte fit");
  death.Parcels.Add(new RebirthLogisticsDeathJournal.Parcel{Before=new[]{ItemStack.Empty,ItemStack.Empty},Locks=new[]{false,false}});
  Check(RebirthLogisticsDeathPartition.TryPlanAll(original,death,out all,Fits)&&all[0][0].itemValue.Metadata=="rich-A"&&all[0][1].IsEmpty()&&all[1][0].itemValue.Metadata=="rich-B","byte-limited rich items distribute to original extra parcel despite free primary slots");
  death.After=all[0];death.Parcels[0].After=all[1];
  Check(RebirthLogisticsDeathPartition.ValidateFrozenPlan(original,death),"frozen distribution validates exact original native positive output independently of later body header sizes");
  death.Parcels[0].After[0].count=2;
  Check(!RebirthLogisticsDeathPartition.ValidateFrozenPlan(original,death),"frozen extra parcel cannot multiply original owed quantity");
  original.OriginalOwnerBag=original.BeforeOwner=original.DebitedOwner=Enumerable.Range(0,8).Select(_=>ItemStack.Empty).ToArray();
  original.AfterOwner=Enumerable.Range(0,8).Select(i=>S("rich-"+i,1)).ToArray();
  death=new RebirthLogisticsDeathJournal.Record{Before=new[]{ItemStack.Empty,ItemStack.Empty,ItemStack.Empty},Locks=new[]{false,false,false},OwnerPhase=2,Outcome=1};
  for(int i=0;i<7;i++)death.Parcels.Add(new RebirthLogisticsDeathJournal.Parcel{Before=new[]{ItemStack.Empty},Locks=new[]{false}});
  Check(RebirthLogisticsDeathPartition.TryParcelLimit(original,2,out int bound)&&bound==8,"entity budget derives from eight actual original owed native stacks");
  Check(RebirthLogisticsDeathPartition.TryPlanAll(original,death,out all,Fits)&&all.Sum(image=>image.Count(item=>!item.IsEmpty()))==8,"byte fit supports more than three extras without increasing native grid sizes");
  death.After=all[0];for(int i=0;i<death.Parcels.Count;i++)death.Parcels[i].After=all[i+1];
  Check(RebirthLogisticsDeathPartition.ValidateFrozenPlan(original,death),"many-body frozen mapping conserves exact original paid outputs");
  original.AfterOwner[0].count=ushort.MaxValue;
  Check(!RebirthLogisticsDeathPartition.TryParcelLimit(original,2,out _),"unbounded overstack quantity cannot manufacture an entity budget");
  Console.WriteLine("PASS actual pure original death partition/owed plan");
 }
}
