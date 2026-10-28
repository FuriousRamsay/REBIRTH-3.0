class ItemStackImage{internal bool Empty=true;internal bool IsEmpty()=>Empty;}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayerLocal player)=>"creation";}
class Xui{internal PlayerUi playerUI=new PlayerUi();}
class PlayerUi{internal EntityPlayerLocal entityPlayer;}
class CookingStation{internal WorkstationData WorkstationData=new WorkstationData();internal XUiC_CraftingQueue craftingQueue=new XUiC_CraftingQueue();}
class XUiC_RebirthCookingWorkspace
{
 internal bool open=true;internal GuiWindow windowGroup=new GuiWindow();internal int pullUiGeneration;internal CookingStation station=new CookingStation();internal Xui xui=new Xui();
 internal TileEntityWorkstation CustodyStation=>station.WorkstationData.TileEntity;
 // ACTUAL_QUEUE_PROJECTION
}
static class RebirthCookingRemoteAdmission
{
 private sealed class ClientOriginal{internal bool AdmissionAccepted=true,TerminalProjection=true;internal World World;internal XUiC_RebirthCookingWorkspace Workspace;internal ItemStackImage[] After={new ItemStackImage()};internal Action<string> Reply;}
 private static readonly List<ClientOriginal> Clients=new List<ClientOriginal>();private const string Accepted="Cooking batch admitted.";
 // ACTUAL_FINISH
 private static void Need(bool value,string label){if(!value)throw new Exception(label);}
 internal static void Exercise()
 {
  for(int variant=0;variant<8;variant++)
  {
   Clients.Clear();var world=new World();var owner=new EntityPlayerLocal{world=world};world.Player=world.Registered=owner;GameManager.Instance.World=world;
   var recipe=new Recipe();recipe.ingredients[0].itemValue.Token="original";
   var tile=new TileEntityWorkstation{Queue=new[]{new RecipeQueueItem{Recipe=recipe,Multiplier=2,Quality=3}}};
   var workspace=new XUiC_RebirthCookingWorkspace();workspace.xui.playerUI.entityPlayer=owner;workspace.station.WorkstationData.TileEntity=tile;
   var card=new XUiC_RecipeStack();workspace.station.craftingQueue.Cards=new[]{card};int replies=0;
   var original=new ClientOriginal{World=world,Workspace=workspace,Reply=reply=>{if(reply!=Accepted)throw new Exception("wrong terminal reply");replies++;}};Clients.Add(original);
   if(variant==1)card.Accept=false;if(variant==2)card.OnSet=()=>owner.Died++;if(variant==3)card.OnSet=()=>tile.Queue[0].Multiplier++;
   if(variant==4)workspace.open=false;if(variant==5)world.Registered=new EntityPlayerLocal();if(variant==6)original.After[0].Empty=false;if(variant==7)world.Remote=true;
   bool success=variant==0||variant==4||variant==6||variant==7;
   Need(FinishAdmissionProjection(original)==success,"actual terminal projection result "+variant);
   Need(original.TerminalProjection!=success&&replies==(success?1:0),"terminal latch/reply follows actual queue projection "+variant);
   Need(Clients.Contains(original)==(!success||variant==6),"unpaid remainder retains original while completed empty original releases "+variant);
   if(variant==0)Need(card.Sets==1&&card.IsDirty&&ReferenceEquals(card.Recipe,recipe),"host-local card contains current authoritative native row");
   if(variant==1){card.Accept=true;Need(FinishAdmissionProjection(original)&&replies==1&&!Clients.Contains(original),"suppressed projection retries presentation only");}
   if(variant==7)Need(card.Sets==0,"remote native replica remains native transport presentation");
  }
  Console.WriteLine("PASS actual local admission terminal/card projection: 8 vectors; native persistence and topology NOT_RUN");
 }
}
static class Program{static void Main()=>RebirthCookingRemoteAdmission.Exercise();}
