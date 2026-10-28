// AUTHORED_NOT_RUN. Actual reagent service + actual complete outbox.
internal sealed class EntityPlayer {internal World world;}
internal sealed partial class World {internal object worldState=new object();internal TileEntityWorkstation Station;internal TileEntityWorkstation GetTileEntity(int position)=>Station;}
internal sealed partial class RebirthWorkstationCompletionOriginal {
 internal string RecipeName="original",Block="station";internal int Position=1;
 internal bool MatchesOwner(EntityPlayer owner)=>ThreadManager.IsMainThread()&&owner!=null&&!owner.world.IsRemote()&&ReferenceEquals(GameManager.Instance.World,owner.world);
}
internal sealed class BlockDefinition {internal string GetBlockName()=>"station";}
internal sealed class ItemStack {internal int Count;internal string Tag;}
internal sealed class TileEntityWorkstation {
 internal BlockDefinition block=new BlockDefinition();internal bool Accessed,Suppress;internal int Writes,Modified;internal Action DuringModified;
 private ItemStack[] output=new[]{new ItemStack{Count=1,Tag="original"}};
 internal ItemStack[] Output {get=>output;set{Writes++;if(!Suppress)output=value;}}
 internal bool IsUserAccessing()=>Accessed;internal void SetModified(){Modified++;DuringModified?.Invoke();}
}
internal static class RebirthCamperInventorySnapshot {
 internal static byte[] Capture(ItemStack[] cells)=>System.Text.Encoding.UTF8.GetBytes(string.Join(";",cells.Select(cell=>cell.Count+":"+cell.Tag)));
 internal static ItemStack[] Restore(byte[] bytes)=>System.Text.Encoding.UTF8.GetString(bytes).Split(';').Select(value=>{int colon=value.IndexOf(':');return new ItemStack{Count=int.Parse(value.Substring(0,colon)),Tag=value.Substring(colon+1)};}).ToArray();
}
internal static class RebirthMedicalLootCraftSignatureService {
 internal static string Choice="bonus";internal static int Choices,Plans;internal static bool Allow=true;
 internal static string ChooseWorkstationReagentOriginal(EntityPlayer owner,string recipe){Choices++;return Choice;}
 internal static bool TryPlanWorkstationReagentOriginal(TileEntityWorkstation tile,string reagent,out ItemStack[] after){Plans++;after=new[]{new ItemStack{Count=2,Tag="original+bonus"}};return Allow;}
}
internal static class RebirthWorkstationCompletionPipeline {internal static bool Allow=true;internal static bool CanSettleReagentOriginal(TileEntityWorkstation tile)=>Allow;}
internal static class RebirthWorkstationReagentPublication {
 internal static bool Saved;internal static int Observations;
 internal static bool TryRecover(RebirthWorkstationCompletionOutbox.State state)=>false;
 internal static bool ObserveSaved(RebirthWorkstationCompletionOutbox.State state){Observations++;if(!Saved||state.NativeReagentObserved?.Matches(state)!=true)return false;state.ReagentPublished=true;return RebirthWorkstationCompletionOutbox.Save(state);}
}
internal static class Program {
 private static EntityPlayer owner;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static RebirthWorkstationCompletionOutbox.State Fresh(){
  ThreadManager.Main=true;var world=new World{Station=new TileEntityWorkstation()};GameManager.Instance.World=world;owner=new EntityPlayer{world=world};
  RebirthWorkstationCompletionPublication.Writes=RebirthWorkstationCompletionPublication.FailAt=0;RebirthWorkstationCompletionPublication.BeforeRead=null;
  RebirthMedicalLootCraftSignatureService.Choice="bonus";RebirthMedicalLootCraftSignatureService.Choices=RebirthMedicalLootCraftSignatureService.Plans=0;RebirthMedicalLootCraftSignatureService.Allow=true;
  RebirthWorkstationCompletionPipeline.Allow=true;RebirthWorkstationReagentPublication.Saved=false;RebirthWorkstationReagentPublication.Observations=0;
  return RebirthWorkstationCompletionOutbox.Retain(new RebirthWorkstationCompletionOriginal(),"server",world,world.Station);
 }
 private static bool Run(RebirthWorkstationCompletionOutbox.State state)=>RebirthWorkstationReagentOriginal.Settle(state,owner);
 public static void Main(){
  var state=Fresh();Need(!Run(state)&&state.ReagentObserved&&!state.ReagentPublished&&owner.world.Station.Writes==1,"native observation incorrectly became saved publication");RebirthWorkstationReagentPublication.Saved=true;Need(Run(state)&&state.ReagentPublished&&owner.world.Station.Writes==1,"saved publication retry repeated native setter");
  state=Fresh();RebirthWorkstationCompletionPublication.FailAt=2;Need(!Run(state)&&owner.world.Station.Writes==0,"failed decision publication entered effect");RebirthWorkstationCompletionPublication.FailAt=0;Run(state);Need(RebirthMedicalLootCraftSignatureService.Choices==1&&owner.world.Station.Writes==1,"decision write retry rerolled bonus or duplicated effect");
  state=Fresh();RebirthWorkstationCompletionPublication.FailAt=3;Need(!Run(state)&&state.ReagentAttempted&&!state.ReagentSetterEntered&&owner.world.Station.Writes==0,"failed attempted publication entered setter");RebirthWorkstationCompletionPublication.FailAt=0;Run(state);Need(owner.world.Station.Writes==1&&RebirthMedicalLootCraftSignatureService.Plans==1,"attempted metadata retry replanned or repeated source");
  state=Fresh();RebirthWorkstationCompletionPublication.FailAt=4;Need(!Run(state)&&state.ReagentObserved&&!state.Saved&&owner.world.Station.Writes==1,"observed outcome write failure not retained");RebirthWorkstationCompletionPublication.FailAt=0;RebirthWorkstationReagentPublication.Saved=true;Need(Run(state)&&owner.world.Station.Writes==1,"observed metadata retry repeated material grant");
  state=Fresh();owner.world.Station.Suppress=true;Need(!Run(state)&&state.ReagentSetterEntered&&!state.ReagentObserved,"suppressed setter certified");Run(state);Need(owner.world.Station.Writes==1,"unknown suppressed native setter replayed");
  state=Fresh();owner.world.Station.DuringModified=()=>{throw new InvalidOperationException("after native setter");};bool threw=false;try{Run(state);}catch(InvalidOperationException){threw=true;}Need(threw&&state.ReagentSetterEntered&&!state.ReagentObserved,"throwing setter outcome not unknown");owner.world.Station.DuringModified=null;Run(state);Need(owner.world.Station.Writes==1,"throw-after-effect replayed material");
  state=Fresh();owner.world.Station.Accessed=true;Need(!Run(state)&&owner.world.Station.Writes==0,"accessed station mutated");owner.world.Station.Accessed=false;RebirthWorkstationCompletionPipeline.Allow=false;Need(!Run(state)&&owner.world.Station.Writes==0,"foreign custody station mutated");
  state=Fresh();RebirthMedicalLootCraftSignatureService.Choice="";Need(Run(state)&&state.Decision&&owner.world.Station.Writes==0,"durable no-bonus decision minted output");
  state=Fresh();var changed=RebirthWorkstationCompletionPublication.ReadPrimary(state.Path);changed.SetAttributeValue("decision",true);changed.Save(state.Path);Need(!Run(state)&&RebirthMedicalLootCraftSignatureService.Choices==0,"changed saved original authorized bonus decision");
  Console.WriteLine("PASS actual reagent settlement + outbox; native slot codec and chunk publication are adapters, topology NOT RUN");
 }
}
