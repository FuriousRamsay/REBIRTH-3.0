// AUTHORED_NOT_RUN. Actual hydration planner + actual shared plan/codec/copy.
// ACTUAL_PLAN_SOURCES
internal static class Program {
 private static string Native(string text)=>Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));
 private static RebirthGearInventoryPlan.Stack Cell(int count=0,string text="")=>new RebirthGearInventoryPlan.Stack{Count=count,ItemData=count==0?"":Native(text)};
 private static RebirthGearInventoryPlan.Stack[] Cells(int count){var result=new RebirthGearInventoryPlan.Stack[count];for(int i=0;i<count;i++)result[i]=Cell();return result;}
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 public static void Main(){
  var bag=Cells(44);var belt=Cells(20);var empty=Cell();bag[3]=Cell(3,"native-seed-quality-liquid325-metadata");belt[19]=Cell(1,"retired-native-backing");
  Need(RebirthHydrationTransferPlan.TryCreate(bag,belt,null,4,empty,true,3,out var plan)&&plan.IsConserved(),"exact bag plan refused");
  Need(plan.BagSlotsBefore==plan.BagSlotsAfter&&plan.BeltSlotsBefore==20&&plan.BeltSlotsAfter==20&&plan.Recovery.Count==0&&plan.Changes.Count==1&&plan.GearAfter.Count==1&&plan.GearAfter.ItemData==bag[3].ItemData&&plan.Changes[0].After.Count==2,"hydration changed capacity/native item or created overflow");
  string frozen=plan.GearAfter.ItemData;bag[3].Count=9;bag[3].ItemData=Native("mutated caller");Need(plan.Changes[0].Before.Count==3&&plan.GearAfter.ItemData==frozen,"caller alias mutated frozen original plan");
  var locks=new bool[44];locks[3]=true;Need(!RebirthHydrationTransferPlan.TryCreate(bag,belt,locks,4,empty,true,3,out _),"locked bag source admitted");
  Need(!RebirthHydrationTransferPlan.TryCreate(bag,belt,null,4,empty,false,19,out _),"retired belt backing admitted");belt[1]=Cell(1,"actual owned belt liquid");Need(RebirthHydrationTransferPlan.TryCreate(bag,belt,null,4,empty,false,1,out plan)&&plan.Changes[0].After.Count==0,"single owned belt item not moved exactly");
  Need(!RebirthHydrationTransferPlan.TryCreate(bag,belt,null,4,Cell(1,"occupied"),true,3,out _),"occupied hydration silently swapped");
  var hydration=Cell(1,"same native full liquid item");locks=new bool[44];locks[0]=true;Need(RebirthHydrationTransferPlan.TryCreate(bag,belt,locks,4,hydration,true,-1,out plan)&&plan.Changes[0].Index==1&&plan.GearAfter.Count==0&&plan.Changes[0].After.ItemData==hydration.ItemData,"return used locked cell or changed liquid");
  for(int i=0;i<bag.Length;i++)bag[i]=Cell(1,"full");Need(!RebirthHydrationTransferPlan.TryCreate(bag,belt,null,4,hydration,true,-1,out _),"full bag produced implicit overflow/drop");
  bag[0]=Cell();bag[2]=new RebirthGearInventoryPlan.Stack{Count=1,ItemData="invalid frame"};Need(!RebirthHydrationTransferPlan.TryCreate(bag,belt,null,4,hydration,true,-1,out _),"malformed unrelated native frame accepted");bag[2]=Cell();bag[1]=Cell(65536,"invalid count");Need(!RebirthHydrationTransferPlan.TryCreate(bag,belt,null,4,hydration,true,-1,out _),"invalid unrelated native count accepted");
  Console.WriteLine("PASS actual hydration item conservation plan; native owner actions/joint receipts remain NOT RUN");
 }
}
