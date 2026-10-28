class PendingPlacement{internal bool IsPending;}
class CookingSlot{internal ItemStack ItemStack=new ItemStack(new ItemValue(1),5);}
class WindowGroup{internal bool isShowing=true;}
static class Localization{internal static string Get(string key)=>key;}
static class RebirthBackpackLibraryReservation{internal static bool Blocks;internal static bool BlocksResourceUse(EntityPlayer player)=>Blocks;}
static class RebirthCookingRemoteAdmission
{
 internal static bool Held,Throws;internal static int Begins;internal static Action<string> Reply;internal static ItemStack[] Before;internal static Func<ItemStack[]> Read;internal static Action<int,ItemStack> Write;
 internal static bool IsHeld(XUiC_RebirthCookingWorkspace workspace)=>Held;
 internal static void BeginClient(XUiC_RebirthCookingWorkspace workspace,EntityPlayer player,TileEntityWorkstation station,Recipe recipe,int batches,ItemStack[] before,Func<ItemStack[]> read,Action<int,ItemStack> write,Action<string> reply){Begins++;Before=before;Read=read;Write=write;Reply=reply;Held=true;if(Throws)throw new IOException("original publication failure");}
}
class XUiC_RebirthCookingWorkspace
{
 private bool pulling,submittingCook,open=true;private PendingPlacement pendingPullPlacement;private WindowGroup windowGroup=new WindowGroup();private Xui xui=new Xui();private XUiC_WorkstationWindowGroup station=new XUiC_WorkstationWindowGroup();private CookingSlot[] slots={new CookingSlot()};private int batch=2;private string status;private float refresh;private bool Preflight=true;private int Returns,Reconciles;
 private void Render(){}
 private bool TryCookingPreflight(bool full,out Recipe recipe,out string blocker){recipe=new Recipe();blocker="preflight refused";return Preflight;}
 private void ReconcileIngredientOrigins(){Reconciles++;}
 private bool ReturnIngredients(){Returns++;return true;}
 // ACTUAL_COOK
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 internal static void Exercise()
 {
  foreach(bool remote in new[]{false,true})for(int variant=0;variant<5;variant++)
  {
   var workspace=new XUiC_RebirthCookingWorkspace();workspace.xui.playerUI.entityPlayer=new EntityPlayerLocal{world=new World{Remote=remote}};
   RebirthCookingRemoteAdmission.Held=RebirthCookingRemoteAdmission.Throws=RebirthBackpackLibraryReservation.Blocks=false;RebirthCookingRemoteAdmission.Begins=0;
   if(variant==1)RebirthCookingRemoteAdmission.Held=true;if(variant==2)workspace.Preflight=false;if(variant==3)workspace.open=false;if(variant==4)RebirthCookingRemoteAdmission.Throws=true;
   workspace.Cook();Need(RebirthCookingRemoteAdmission.Begins==(variant==0||variant==4?1:0),"actual cooking route differs by owner topology or refusal "+remote+"/"+variant);
   if(variant==0){Need(workspace.submittingCook&&RebirthCookingRemoteAdmission.Before[0].count==5,"full original grid reaches shared admission before mutation");RebirthCookingRemoteAdmission.Before[0].count=99;Need(workspace.slots[0].ItemStack.count==5,"caller detached original cannot mutate live grid");RebirthCookingRemoteAdmission.Held=false;RebirthCookingRemoteAdmission.Reply("Cooking batch admitted.");Need(!workspace.submittingCook&&workspace.Reconciles==1,"positive shared admission releases submitting state");}
   if(variant==4)Need(workspace.submittingCook&&RebirthCookingRemoteAdmission.Held,"publication exception retains actual original admission hold");
  }
  Console.WriteLine("PASS actual Cook caller: local/remote original admission, exact refusal, detached grid and retained publication failure; native service remains separately qualified");
 }
}
static class Program{static void Main()=>XUiC_RebirthCookingWorkspace.Exercise();}
