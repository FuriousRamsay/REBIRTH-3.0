public class ItemValue {public string Data="";public ItemValue Clone(){return new ItemValue{Data=Data};}}
public class ItemStack {public ItemValue itemValue=new ItemValue();public int count;public ItemStack Clone(){return new ItemStack{itemValue=itemValue.Clone(),count=count};}public static ItemStack[] CreateArray(int n){var a=new ItemStack[n];for(int i=0;i<n;i++)a[i]=new ItemStack();return a;}}
public static class LayoutFixture {
static int checks;static void Check(bool b,string name){checks++;if(!b)throw new System.Exception(name);}
public static string Run(){
for(int materials=0;materials<=6;materials++){
var legacy=ItemStack.CreateArray(3+materials);for(int i=0;i<legacy.Length;i++){legacy[i].count=i+1;legacy[i].itemValue.Data="payload"+i;}
ItemStack[] physical;Check(RebirthStationInputLayout.TryReadPhysical(legacy,3,materials,out physical)&&physical.Length==3,"legacy projection excludes totals");physical[0].count=999;Check(legacy[0].count==1,"legacy projection detached");
ItemStack[] expanded;Check(RebirthStationInputLayout.TryExpand(legacy,materials,out expanded),"legacy expand");Check(expanded.Length==9+materials,"exact layout length");
for(int i=0;i<legacy.Length;i++){int target=i<3?i:9+i-3;Check(expanded[target].count==legacy[i].count&&expanded[target].itemValue.Data==legacy[i].itemValue.Data&&!object.ReferenceEquals(expanded[target].itemValue,legacy[i].itemValue),"exact detached payload position");}
for(int i=3;i<9;i++)Check(expanded[i].count==0,"new physical slots empty");
ItemStack[] again;Check(RebirthStationInputLayout.TryExpand(expanded,materials,out again),"already migrated accepted");for(int i=0;i<expanded.Length;i++)Check(again[i].count==expanded[i].count&&again[i].itemValue.Data==expanded[i].itemValue.Data,"idempotent layout");
ItemStack[] grid;Check(RebirthStationInputLayout.TryPhysicalGrid(expanded,materials,out grid)&&grid.Length==9,"physical grid excludes material totals");grid[0].itemValue.Data="edited";Check(expanded[0].itemValue.Data=="payload0"&&legacy[0].itemValue.Data=="payload0","grid detached");
}
ItemStack[] rejected;Check(!RebirthStationInputLayout.TryExpand(ItemStack.CreateArray(4),0,out rejected),"unexpected shape held");Check(!RebirthStationInputLayout.TryExpand(ItemStack.CreateArray(3),-1,out rejected),"negative material count held");var broken=ItemStack.CreateArray(3);broken[1]=null;Check(!RebirthStationInputLayout.TryExpand(broken,0,out rejected),"missing slot held");broken=ItemStack.CreateArray(3);broken[1].count=-1;Check(!RebirthStationInputLayout.TryExpand(broken,0,out rejected),"negative count held");return "PASS "+checks+" actual layout-migration checks with explicit native ItemStack clone doubles; not live migration";
}}