internal sealed class XUiC_RebirthCookingWorkspace {}
internal static class RebirthCookingOwnerDelta{internal sealed class Plan{}}
internal static class RebirthStationGridIngredients{internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a!=null&&b!=null&&a.count==b.count&&(a.IsEmpty()&&b.IsEmpty()||a.itemValue.Token==b.itemValue.Token);}
internal static class RebirthCookingGridCustody{internal static Action Refresh;internal static void RefreshRecovery(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player)=>Refresh?.Invoke();}
internal static class RebirthCookingRemoteAdmission
{
 // ACTUAL_ORIGINAL
 private static readonly List<ClientOriginal> Clients=new List<ClientOriginal>();
 // ACTUAL_RESTORE
 private static ItemStack[] grid;private static int writes,reads,changeRead;private static bool suppressed,changeWrite;
 private static ItemStack[] Cells(bool filled)=>Enumerable.Range(0,12).Select(i=>i==0&&filled?new ItemStack(new ItemValue(1){Token="original"},5):ItemStack.Empty.Clone()).ToArray();
 private static ClientOriginal Setup()
 {
  Clients.Clear();var world=new World{Remote=true};var owner=new EntityPlayerLocal{world=world};world.Primary=owner;world.Registered=owner;GameManager.Instance=new GameManager{World=world};
  writes=reads=changeRead=0;suppressed=changeWrite=false;grid=Cells(false);RebirthCookingGridCustody.Refresh=null;
  var original=new ClientOriginal{Owner=owner,World=world,Creation=owner.Creation,RestoreGrid=true,Before=Cells(true),RestoreImage=Cells(true),
   Read=()=>{reads++;if(changeRead==reads)owner.Died++;return ItemStack.Clone(grid);},Write=(i,item)=>{writes++;if(!suppressed)grid[i]=item.Clone();if(changeWrite)owner.Died++;}};
  Clients.Add(original);return original;
 }
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 internal static void Exercise()
 {
  var original=Setup();Need(RestoreClientGrid(original)&&!original.RestoreGrid&&writes==1&&grid[0].count==5,"exact original projection completes");
  original=Setup();suppressed=true;Need(!RestoreClientGrid(original)&&original.RestoreGrid&&grid[0].IsEmpty(),"suppressed native setter retains restoration");
  suppressed=false;Need(RestoreClientGrid(original)&&!original.RestoreGrid&&grid[0].count==5,"same original resumes presentation only");
  original=Setup();changeRead=1;Need(!RestoreClientGrid(original)&&writes==0&&original.RestoreGrid,"life changed during read cannot reach setter");
  original=Setup();changeWrite=true;Need(!RestoreClientGrid(original)&&writes==1&&original.RestoreGrid,"life changed during setter cannot release restoration");
  original=Setup();var owner=original.Owner;RebirthCookingGridCustody.Refresh=()=>owner.Died++;Need(!RestoreClientGrid(original)&&original.RestoreGrid,"scope changed during nested recovery remains held");
  original=Setup();grid[0]=new ItemStack(new ItemValue(1){Token="later"},5);Need(!RestoreClientGrid(original)&&writes==0&&original.RestoreGrid,"later rich grid cannot be replaced");
  original=Setup();original.World.Registered=new EntityPlayer();Need(!RestoreClientGrid(original)&&reads==0&&writes==0,"replacement body cannot read or project old UI");
  original=Setup();RebirthCookingGridCustody.Refresh=()=>grid[0]=ItemStack.Empty.Clone();Need(!RestoreClientGrid(original)&&original.RestoreGrid,"nested recovery final postimage is required");
  Console.WriteLine("PASS actual cooking original grid projection: 9 assertions; native persistence remains NOT_RUN");
 }
}
internal static class Program{public static void Main()=>RebirthCookingRemoteAdmission.Exercise();}
