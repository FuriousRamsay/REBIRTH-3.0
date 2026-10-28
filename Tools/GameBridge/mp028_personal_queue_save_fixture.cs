// AUTHORED_NOT_RUN. Actual queue observer/capture/save/release methods are
// composed with the owner-prefix observer and create-only publication. Native
// UI, queue/PDS serialization, native save calls and live grids are adapters.
public static partial class RebirthRecipeOutputIntegration
{
 // Station queue publication has its own complete actual-method cohort.
 private static bool RegisterUiOutputStationQueue(UiOutputPending pending)=>false;
 private static void PumpStationOutputQueues(){}
 private static void PumpRemoteOutputColdDiscovery(){} // Independent actual remote join/cold source cohort.
 private static void PumpStationOutputBefores(){} // Independent placed pre-generation source.
 // PERSONAL_PENDING_SOURCE
 // ACTUAL_QUEUE_OBSERVER
 private static UiOutputPending QueueFresh(out PlayerDataFile data)
 {
  var source=Fresh(out data);
  var pending=new UiOutputPending{World=source.World,Actor=source.Actor,Recipe=source.Recipe,Count=source.Count,Deaths=source.Deaths,
   Creation=source.Creation,Path=source.Path,Hash=UiOriginalHash(source.Original),Operation=source.Operation,Source=source,Returned=true};
  pendingUiOutputs.Add(source.Scope.Card,pending);ObserveUiOutputQueue(source.Scope.Card);return pending;
 }
 private static object QueueCapture(UiOutputPending pending,PlayerDataFile data)
 {ObserveRecipeOutputOwnerFromPlayer(data,pending.Actor,true);EnterRecipeOutputOwnerSave(data,GameIO.Root,"account",true,out var state);return state;}
 public static void RunPersonalQueueCases()
 {
  var pending=QueueFresh(out var data);var source=pending.Source;
  ObserveUiOutputQueue(source.Scope.Card);
  Need(GameManager.Instance.SaveRequests==1,"native queue observation did not request one existing player save or retried it in the same frame");
  Need(pending.QueueObserved&&pendingUiOutputs.TryGetValue(source.Scope.Card,out _)&&!source.Disposed,"live queue decrement released unsaved personal original");
  var state=QueueCapture(pending,data);WriteNative(data);ObserveRecipeOutputOwnerSave(true,state);
  Need(pending.QueueSaved&&pending.Source==null&&source.Disposed&&!pendingUiOutputs.TryGetValue(source.Scope.Card,out _),"matching full native inventory+queue save did not release source");
  var receipt=XElement.Load(source.Path+".queue-owner-saved");
  Need((string)receipt.Attribute("nativeDigest")==UiOriginalHash(data.Native)&&(string)receipt.Attribute("original")==UiOriginalHash(source.Original)&&
   (int)receipt.Attribute("outputPrefix")==1&&(int)receipt.Attribute("afterCount")==0,"saved queue proof lost native/original/prefix binding");
  pending=QueueFresh(out data);source=pending.Source;state=QueueCapture(pending,data);WriteNative(data);ObserveRecipeOutputOwnerSave(false,state);
  Need(!pending.QueueSaved&&pending.Source==source&&!source.Disposed,"skipped native save released personal source");
  pending=QueueFresh(out data);source=pending.Source;data.craftingData.Queue="foreign";state=QueueCapture(pending,data);WriteNative(data);ObserveRecipeOutputOwnerSave(true,state);
  Need(!pending.QueueSaved&&pending.Source==source,"different native crafting queue qualified saved output acceptance");
  pending=QueueFresh(out data);source=pending.Source;ObserveRecipeOutputOwnerFromPlayer(data,pending.Actor,true);data.Native=new byte[]{44};
  EnterRecipeOutputOwnerSave(data,GameIO.Root,"account",true,out state);WriteNative(data);ObserveRecipeOutputOwnerSave(true,state);
  Need(!pending.QueueSaved&&pending.Source==source,"mutated full native PDS between capture and save qualified queue");
  pending=QueueFresh(out data);source=pending.Source;state=QueueCapture(pending,data);File.WriteAllBytes(System.IO.Path.Combine(GameIO.Root,"account.ttp"),new byte[]{44});ObserveRecipeOutputOwnerSave(true,state);
  Need(!pending.QueueSaved&&pending.Source==source,"contradictory final native file qualified saved queue");
  pending=QueueFresh(out data);source=pending.Source;state=QueueCapture(pending,data);WriteNative(data);ThreadManager.Main=false;ObserveRecipeOutputOwnerSave(true,state);
  Need(pending.QueueSaved&&pending.Source==source&&!source.Disposed,"worker completion mutated native card ownership");
  ThreadManager.Main=true;var update=new ModEvents.SGameUpdateData();PumpPersonalOutputQueues(ref update);
  Need(pending.Source==null&&source.Disposed,"registered game-update pump did not finish deferred saved release without another card Update");
  pending=QueueFresh(out data);source=pending.Source;state=QueueCapture(pending,data);WriteNative(data);pending.Actor.Died++;ObserveRecipeOutputOwnerSave(true,state);
  Need(pending.QueueSaved&&pending.Source==null&&source.Disposed,"positive complete saved original could not retire its old-life metadata");
  pending=QueueFresh(out data);source=pending.Source;pending.Actor.Died++;RegisterUiOutputPersonalQueue(pending);
  Need(!pending.QueueSaved&&pending.Source==source&&!source.Disposed,"life change without positive saved fact retired uncertain original");
  pending=QueueFresh(out data);source=pending.Source;state=QueueCapture(pending,data);WriteNative(data);pending.Actor.Died++;pending.Actor.Creation="foreign";ObserveRecipeOutputOwnerSave(true,state);
  Need(pending.QueueSaved&&pending.Source==source&&!source.Disposed,"historical saved fact released a foreign character's card association");
  pending=QueueFresh(out data);source=pending.Source;source.OwnerCurrent=false;state=QueueCapture(pending,data);WriteNative(data);ObserveRecipeOutputOwnerSave(true,state);
  Need(!pending.QueueSaved&&pending.Source==source,"unqualified live owner image acquired queue-save witness");
  pending=QueueFresh(out data);source=pending.Source;ObserveRecipeOutputOwnerFromPlayer(data,pending.Actor,true);
  EnterRecipeOutputOwnerSave(data,GameIO.Root,"foreign",true,out state);WriteNative(data);ObserveRecipeOutputOwnerSave(true,state);
  Need(!pending.QueueSaved&&pending.Source==source,"foreign native player filename released personal output");
  pending=QueueFresh(out data);source=pending.Source;data.NativePrefix=0;state=QueueCapture(pending,data);WriteNative(data);ObserveRecipeOutputOwnerSave(true,state);
  Need(!pending.QueueSaved&&pending.Source==source,"absent saved original prefix marker qualified complete queue acceptance");
  pending=QueueFresh(out data);source=pending.Source;int saves=GameManager.Instance.SaveRequests;SaveUiOutputPersonalPrefix(source,0);
  Need(source.Actor.Buffs.GetCustomVar(PersonalOutputReceipt(source.Operation))==1&&GameManager.Instance.SaveRequests==saves+1,"accepted actual source did not request native marker prefix save");
  state=QueueCapture(pending,data);WriteNative(data);ObserveRecipeOutputOwnerSave(true,state);
  Need(pending.Source==null&&source.Actor.Buffs.GetCustomVar(PersonalOutputReceipt(source.Operation))==0,"same-life saved terminal did not clear its own native prefix marker");
  pending=QueueFresh(out data);source=pending.Source;state=QueueCapture(pending,data);WriteNative(data);ThreadManager.Main=false;ObserveRecipeOutputOwnerSave(true,state);
  ThreadManager.Main=true;source.OwnerCurrent=false;RegisterUiOutputPersonalQueue(pending);
  Need(pending.QueueSaved&&pending.Source==null&&source.Disposed,"positive saved queue terminal remained held after legitimate same-life inventory movement");
  Console.WriteLine("MP028 personal queue save: 19 assertions authored, actual observer/save/release/publication; native adapters remain explicit.");
 }
}
class PersonalQueueCases{static void Main()=>RebirthRecipeOutputIntegration.RunPersonalQueueCases();}
