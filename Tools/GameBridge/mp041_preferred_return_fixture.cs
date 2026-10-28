// Native containers and item compatibility are explicit adapters; the planner below is actual production.
static class RebirthToolbeltCapacity{internal static int GetOwnedSlotCount(EntityPlayerLocal player,int maximum)=>Math.Min(player.inventory.Length,maximum);}
class RebirthCamperEnergyActorImage{internal ItemStack[] Bag,Belt;}
static class PreferredReturnCases
{
 // PRODUCTION_PREFERRED_RETURN
 static readonly ItemClass normal=new ItemClass();
 static ItemStack S(int count,string rich)=>new ItemStack(new ItemValue{type=count==0?0:1,Metadata=rich,ItemClass=normal},count);
 static EntityPlayerLocal Actor(int belt)=>new EntityPlayerLocal{inventory=new Inventory{Length=belt,preferredItemTypes=new int[belt]}};
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static void Main()
 {
  var player=Actor(1);var image=new RebirthCamperEnergyActorImage{Bag=new[]{S(5,"A"),S(8,"B")},Belt=new[]{S(0,"")}};
  var incoming=S(4,"B");var outer=new ItemValue{type=99,Metadata="outer"};RebirthCookingPartialMergeScope.Incoming=outer;
  Check(PlanPreferred(player,image,incoming)==0&&image.Bag[0].count==5&&image.Bag[1].count==10&&image.Belt[0].count==2&&image.Belt[0].itemValue.Metadata=="B","return preserves unrelated rich material and routes only exact remaining quantity");
  Check(incoming.count==4&&incoming.itemValue.Metadata=="B"&&ReferenceEquals(RebirthCookingPartialMergeScope.Incoming,outer),"detached incoming and outer native merge scope preserved");
  image=new RebirthCamperEnergyActorImage{Bag=new[]{S(5,"A"),S(8,"B")},Belt=new[]{S(10,"C")}};
  Check(PlanPreferred(player,image,S(4,"B"))==2&&image.Bag[0].count==5&&image.Bag[1].count==10&&image.Belt[0].count==10,"full owner retains exact remaining material in grid custody");
  player=Actor(1);image=new RebirthCamperEnergyActorImage{Bag=new[]{S(10,"C")},Belt=new[]{S(0,""),S(0,"")}};
  Check(PlanPreferred(player,image,S(12,"B"))==2&&image.Belt[0].count==10&&image.Belt[1].IsEmpty(),"return does not consume hidden belt capacity");
  Console.WriteLine("PASS actual preferred cooking return: 4 assertions");
 }
}
