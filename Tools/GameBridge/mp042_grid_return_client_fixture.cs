// AUTHORED_NOT_RUN. Actual TickReturn/ClientOriginal; explicit native adapters.
internal sealed class XUiC_RebirthCookingWorkspace {}
internal static class RebirthStationGridIngredients {internal static bool IsSameStackSnapshot(ItemStack a,ItemStack b)=>a.count==b.count&&(a.IsEmpty()&&b.IsEmpty()||a.itemValue.Token==b.itemValue.Token);}
internal static class RebirthNativeItemCodec {internal static string Encode(ItemValue value)=>value.Token??"rich-default";}
internal static class RebirthCookingOwnerDelta {
 internal sealed class Plan {internal Guid Operation=Guid.NewGuid();internal string World="world",Creation="creation",Owner="owner";internal int Deaths=2;internal string Receipt=>"receipt";}
 internal static Plan Decoded;internal static int Credits,Advances,Retirements;internal static bool Credited,Acknowledged;
 internal static Plan Decode(string text)=>Decoded;
 internal static bool Advance(EntityPlayerLocal player,Plan p,int phase,Func<bool> current,Func<bool> clear){Advances++;if(phase<1||!current())return false;if(!Credited){Credits++;Credited=true;}return clear()&&current();}
 internal static bool Retire(EntityPlayerLocal player,Plan p){Retirements++;return true;}
 internal static bool AbandonRejectedWithoutEffect(EntityPlayerLocal player,Plan p)=>Acknowledged;
}
internal static class RebirthCookingGridCustody {
 internal static int Prepared,Completed,Rejected;internal static bool Rejects=true,Completes=true;
 internal static bool TryExternalGrid(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,Guid op,out ItemStack[] before,out ItemStack[] after){before=after=null;return false;}
 internal static void PrepareExternal(XUiC_RebirthCookingWorkspace workspace,EntityPlayerLocal player,Guid op,string receipt,ItemStack[] before,ItemStack[] after,RebirthCookingOwnerDelta.Plan p,Guid parent){Prepared++;}
 internal static bool WithExternalDelta(Guid op,Func<bool> effect)=>effect();
 internal static bool CompleteExternal(Guid op){Completed++;return Completes;}
 internal static bool RejectUnappliedExternal(Guid op,EntityPlayerLocal player){Rejected++;return Rejects;}
}
internal static class RebirthCookingSessionService {
 internal static Action<string> Pending;
 internal static void Request(EntityPlayerLocal player,string action,string recipe="",string book="",string magazine="",ItemValue item=null,int count=0,Action<string> reply=null){Pending=reply;}
}
internal static class RebirthCookingRemoteAdmission {
// ACTUAL_ORIGINAL
 private static readonly List<ClientOriginal> Clients=new List<ClientOriginal>();
// ACTUAL_TICK_RETURN
 private static ItemStack[] grid;private static bool suppress,changeLife,changeOnRead;private static int reads,writes;
 private static ClientOriginal Setup(){
  Clients.Clear();ThreadReset();var world=new World{Remote=true};var player=new EntityPlayerLocal{world=world};world.Primary=player;world.Registered=player;GameManager.Instance=new GameManager{World=world};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();
  grid=new[]{new ItemStack(new ItemValue(1),5),ItemStack.Empty.Clone()};suppress=changeLife=changeOnRead=false;reads=writes=0;
  RebirthCookingOwnerDelta.Decoded=new RebirthCookingOwnerDelta.Plan();RebirthCookingOwnerDelta.Credits=RebirthCookingOwnerDelta.Advances=RebirthCookingOwnerDelta.Retirements=0;RebirthCookingOwnerDelta.Credited=RebirthCookingOwnerDelta.Acknowledged=false;
  RebirthCookingGridCustody.Prepared=RebirthCookingGridCustody.Completed=RebirthCookingGridCustody.Rejected=0;RebirthCookingGridCustody.Rejects=RebirthCookingGridCustody.Completes=true;
  var original=new ClientOriginal{Owner=player,World=world,Creation="creation",Operation=Guid.NewGuid(),Carrier=new ItemValue(1),After=new[]{new ItemStack(new ItemValue(1),5)},Read=()=>{reads++;if(changeOnRead&&reads==2)player.Died++;return grid;},Write=(i,item)=>{writes++;if(!suppress)grid[i]=item;if(changeLife)player.Died++;},Reply=s=>{}};Clients.Add(original);return original;
 }
 private static void ThreadReset(){} // Main-thread adapter always true in shared native doubles.
 private static void Need(bool value,string text){if(!value)throw new Exception(text);}
 private static void Deliver(string prefix)=>RebirthCookingSessionService.Pending(prefix+Convert.ToBase64String(Encoding.UTF8.GetBytes("explicit-plan-adapter")));
 internal static void ExerciseReturnClient(){
  var o=Setup();TickReturn(o);Deliver("returned:");Need(grid.All(i=>i.IsEmpty())&&Clients.Count==0&&RebirthCookingGridCustody.Completed==1&&RebirthCookingOwnerDelta.Credits==1,"exact grid return did not settle");
  o=Setup();o.World.Remote=false;TickReturn(o);Need(GameManager.Instance.Saves==1&&SingletonMonoBehaviour<ConnectionManager>.Instance.Sends==0,"host-local return uses actual local save instead of remote player-data upload");Deliver("returned:");Need(Clients.Count==0&&RebirthCookingOwnerDelta.Credits==1,"host-local original return reaches shared owner/grid handshake");
  o=Setup();suppress=true;TickReturn(o);Deliver("returned:");Need(grid[0].count==5&&Clients.Contains(o)&&RebirthCookingGridCustody.Completed==0&&RebirthCookingOwnerDelta.Retirements==0,"suppressed grid setter accepted as completed return");
  suppress=false;TickReturn(o);Deliver("returned:");Need(Clients.Count==0&&RebirthCookingOwnerDelta.Credits==1,"grid recovery repeated owner credit");
  o=Setup();RebirthCookingGridCustody.Completes=false;TickReturn(o);Deliver("returned:");Need(Clients.Contains(o)&&o.ReturnPlan!=null&&o.ExpectedReturn!=Guid.Empty&&grid.All(i=>i.IsEmpty())&&RebirthCookingOwnerDelta.Credits==1,"failed grid completion forgot paid original");RebirthCookingGridCustody.Completes=true;TickReturn(o);Deliver("returned:");Need(Clients.Count==0&&RebirthCookingOwnerDelta.Credits==1,"grid terminal metadata retry repeated owner credit");
  o=Setup();changeLife=true;TickReturn(o);Deliver("returned:");Need(Clients.Contains(o)&&RebirthCookingGridCustody.Completed==0,"life changed during grid setter retired original");
  o=Setup();changeOnRead=true;TickReturn(o);Deliver("returned:");Need(Clients.Contains(o)&&writes==0&&grid[0].count==5&&RebirthCookingGridCustody.Completed==0,"life changed during opaque grid read still reached setter");
  for(int variant=0;variant<6;variant++){
   o=Setup();TickReturn(o);if(variant==0)o.Owner.Died++;if(variant==1)o.Owner.Owner="foreign";if(variant==2)o.World.Registered=new EntityPlayer();if(variant==3)o.Owner.Dead=true;if(variant==4)o.ExpectedReturn=Guid.NewGuid();if(variant==5)GameManager.Instance.World=new World();
   Deliver("returned:");Need(RebirthCookingGridCustody.Prepared==0&&RebirthCookingOwnerDelta.Credits==0&&Clients.Contains(o),"stale reply prepared financial grid intent "+variant);
  }
  o=Setup();o.ExpectedReturn=RebirthCookingOwnerDelta.Decoded.Operation;TickReturn(o);Deliver("retry-return:");Need(o.ExpectedReturn!=Guid.Empty&&RebirthCookingGridCustody.Rejected==0,"unsaved rejection acknowledgement forgot expected original");
  RebirthCookingOwnerDelta.Acknowledged=true;RebirthCookingGridCustody.Rejects=false;TickReturn(o);Deliver("retry-return:");Need(o.ExpectedReturn!=Guid.Empty,"grid rejection refusal discarded expected original");
  RebirthCookingGridCustody.Rejects=true;TickReturn(o);Deliver("retry-return:");Need(o.ExpectedReturn==Guid.Empty&&RebirthCookingGridCustody.Rejected==2,"positive rejection did not clear same original");
  o=Setup();SingletonMonoBehaviour<ConnectionManager>.Instance.Throw=true;TickReturn(o);Need(!o.Waiting&&Clients.Contains(o),"send exception stranded original waiting flag");
  Console.WriteLine("PASS actual detached return callback and grid postimages; native owner setters, saves and topology remain adapters/NOT RUN");
 }
}
internal static class Program {public static void Main()=>RebirthCookingRemoteAdmission.ExerciseReturnClient();}
