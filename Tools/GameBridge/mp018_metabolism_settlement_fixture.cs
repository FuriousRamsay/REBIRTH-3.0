// AUTHORED_NOT_RUN. Actual settlement, grant, hold, owner application and permit.
// Repository storage/schema and native saved-owner witness are explicit adapters.
public static partial class RebirthMetabolismStateRepository {
 private static readonly object Sync=new object();
 private static readonly Dictionary<string,RebirthMetabolismState> States=new Dictionary<string,RebirthMetabolismState>();
 private static bool serverAuthority=true;private static World scopeWorld;private static int loadGeneration;
 private static string scopePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"metabolism.xml");
 private const long MaximumStoreBytes=64L*1024*1024;
 internal static bool SaveAllowed=true,SuppressSave,ThrowAfterSave,WrongSavedAfter;internal static int Saves;internal static Action AfterDecode;
 internal static RebirthMetabolismState State {
  get=>States.TryGetValue("account",out var state)?state:null;
  set{States["account"]=value;scopeWorld=GameManager.Instance.World;loadGeneration++;SaveAllowed=true;SuppressSave=ThrowAfterSave=WrongSavedAfter=false;Saves=0;AfterDecode=null;}
 }
 internal static bool TryGet(EntityPlayerLocal player,out RebirthMetabolismState state){state=State;return state!=null;}
 private static bool ScopeCurrentLocked()=>ReferenceEquals(scopeWorld,GameManager.Instance.World);
 private static string GetStablePlayerId(EntityPlayerLocal player)=>"account";
 private static bool TrySave(string reason,out string error){
  Saves++;error=null;if(!SaveAllowed)return false;if(SuppressSave)return true;
  var state=State;var root=new XElement("fixtureRepository",new XElement("player",new XAttribute("id","account"),new XAttribute("creation",state.SurvivorCreationId),
   new XAttribute("count",WrongSavedAfter?99:state.HydrationSlotItem.count),new XAttribute("data",state.HydrationSlotItem.Data),
   state.HydrationTransferReceipt==null?null:RebirthHydrationMetabolismReceiptCodec.Write(state.HydrationTransferReceipt,state.SurvivorCreationId)));
  new XDocument(root).Save(scopePath);if(ThrowAfterSave)throw new IOException("unknown publication result");return true;
 }
 private static Dictionary<string,RebirthMetabolismState> DecodeStore(XDocument document){
  var row=document.Root.Element("player");var state=new RebirthMetabolismState{SurvivorCreationId=(string)row.Attribute("creation"),HydrationSlotItem=new ItemStack{count=(int)row.Attribute("count"),Data=(string)row.Attribute("data")}};
  var receipt=row.Element("hydrationTransferReceipt");if(receipt!=null){if(!RebirthHydrationMetabolismReceiptCodec.TryRead(receipt,state.SurvivorCreationId,out var original))throw new InvalidDataException("fixture receipt invalid");state.HydrationTransferReceipt=original;}
  AfterDecode?.Invoke();return new Dictionary<string,RebirthMetabolismState>{{(string)row.Attribute("id"),state}};
 }
}
// PROGRAM_PREFIX
 private static RebirthHydrationTransferGrant Applied(bool returning=false){
  var grant=Ready(false,returning);Need(RebirthHydrationOwnerApply.Apply(grant)==RebirthGearOwnerApplySequence.Result.Applied,"fixture owner applied leg unavailable");
  RebirthHydrationOwnerCheckpoint.SavedApplied=true;return grant;
 }
 public static void Main(){
  var grant=Applied();var state=RebirthMetabolismStateRepository.State;int revision=state.Revision;
  Need(RebirthMetabolismStateRepository.TrySettleLocalHydration(grant)&&state.HydrationSlotItem.count==1&&state.HydrationTransferReceipt.Digest==original.Digest&&grant.Current(),"exact saved local metabolism leg failed");
  Need(state.Revision==revision+1&&NativeWrites.Calls==1&&RebirthHydrationOwnerHold.IsHeld(player),"joint saved leg repeated inventory or released custody");
  int saves=RebirthMetabolismStateRepository.Saves;Need(RebirthMetabolismStateRepository.HasSavedLocalHydration(grant)&&RebirthMetabolismStateRepository.Saves==saves&&state.Revision==revision+1,"saved-leg observation performed another write");
  Need(RebirthMetabolismStateRepository.TrySettleLocalHydration(grant)&&state.Revision==revision+1&&NativeWrites.Calls==1,"save-only settlement retry repeated mutation");
  grant=Applied(true);Need(RebirthMetabolismStateRepository.TrySettleLocalHydration(grant)&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty()&&player.bag.ItemGrid.items[0].count==1,"saved unequip metabolism leg failed");
  grant=Applied();RebirthHydrationOwnerCheckpoint.SavedApplied=false;Need(!RebirthMetabolismStateRepository.TrySettleLocalHydration(grant)&&RebirthMetabolismStateRepository.State.HydrationSlotItem.IsEmpty()&&RebirthMetabolismStateRepository.Saves==0,"unsaved owner result mutated metabolism");
  grant=Applied();state=RebirthMetabolismStateRepository.State;RebirthMetabolismStateRepository.SaveAllowed=false;
  Need(!RebirthMetabolismStateRepository.TrySettleLocalHydration(grant)&&state.HydrationSlotItem.count==1&&grant.Current(),"failed save discarded retained metabolism result");
  revision=state.Revision;RebirthMetabolismStateRepository.SaveAllowed=true;Need(RebirthMetabolismStateRepository.TrySettleLocalHydration(grant)&&state.Revision==revision&&NativeWrites.Calls==1,"failed-save retry repeated financial changes");
  grant=Applied();RebirthMetabolismStateRepository.ThrowAfterSave=true;Need(!RebirthMetabolismStateRepository.TrySettleLocalHydration(grant)&&grant.Current(),"unknown publication discarded warm original");
  state=RebirthMetabolismStateRepository.State;revision=state.Revision;RebirthMetabolismStateRepository.ThrowAfterSave=false;Need(RebirthMetabolismStateRepository.TrySettleLocalHydration(grant)&&state.Revision==revision&&NativeWrites.Calls==1,"unknown publication replayed debit");
  grant=Applied();RebirthMetabolismStateRepository.SuppressSave=true;Need(!RebirthMetabolismStateRepository.TrySettleLocalHydration(grant),"old final file qualified new operation");
  RebirthMetabolismStateRepository.SuppressSave=false;Need(RebirthMetabolismStateRepository.TrySettleLocalHydration(grant),"suppressed save could not retry metadata");
  grant=Applied();RebirthMetabolismStateRepository.WrongSavedAfter=true;Need(!RebirthMetabolismStateRepository.TrySettleLocalHydration(grant),"receipt with wrong saved hydration qualified");
  grant=Applied();RebirthMetabolismStateRepository.AfterDecode=()=>player.world.Primary=null;Need(!RebirthMetabolismStateRepository.TrySettleLocalHydration(grant)&&RebirthHydrationOwnerHold.IsHeld(player),"scope loss during saved decode qualified or released custody");
  Console.WriteLine("PASS actual warm local hydration settlement; repository schema/native PDS/remote and cold recovery are separate NOT RUN evidence");
 }
}
