// Actual detached prefix implementation. Native ItemStack/frame adapters are
// the same explicitly disclosed adapters as the composed original cohort.
static class RebirthStationGridIngredients
{
 internal static bool IsSameStackSnapshot(ItemStack left,ItemStack right)=>left!=null&&right!=null&&left.count==right.count&&
     left.itemValue.type==right.itemValue.type&&left.itemValue.Rich==right.itemValue.Rich;
}
static class TradePrefixFixture
{
 internal static void Run(RebirthNpcTradeOriginal original)
 {
  void Need(bool value,string text){if(!value)throw new Exception(text);}
  var bag=original.CopySlots(true,false);var belt=original.CopySlots(false,false);
  int progress=0,total=RebirthNpcTradePrefix.Changes(original);
  Need(total>0&&RebirthNpcTradePrefix.Matches(original,bag,belt,0),"actual frozen Before is not prefix0");
  foreach(bool backpack in new[]{true,false})
  {
   var before=original.CopySlots(backpack,false);var after=original.CopySlots(backpack,true);var actual=backpack?bag:belt;
   for(int slot=0;slot<before.Length;slot++)
   {
    if(RebirthStationGridIngredients.IsSameStackSnapshot(before[slot],after[slot]))continue;
    Need(!RebirthNpcTradePrefix.Matches(original,bag,belt,progress*2+1),"entered odd prefix became replayable");
    actual[slot]=after[slot].Clone();progress++;
    Need(RebirthNpcTradePrefix.Matches(original,bag,belt,progress*2),"saved returned prefix lost frozen bag/belt order");
    Need(!RebirthNpcTradePrefix.Matches(original,bag,belt,(progress-1)*2),"advanced native prefix matched prior stage");
   }
  }
  Need(progress==total&&original.MatchesSlots(bag,belt,true),"prefix chain lost full frozen After");
  Need(!RebirthNpcTradePrefix.Matches(original,bag,belt,total*2+2),"out-of-range saved progress accepted");
  Need(!RebirthNpcTradePrefix.Matches(original,bag,belt,-2),"negative prefix accepted");
  Need(!RebirthNpcTradePrefix.Matches(original,null,belt,total*2),"missing native grid accepted");
  bag[0].count++;
  Need(!RebirthNpcTradePrefix.Matches(original,bag,belt,total*2),"unrelated native mutation accepted as exact original After");
 }
}
