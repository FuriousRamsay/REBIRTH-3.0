// AUTHORED_NOT_RUN. Complete production final queue observer/copy consumer.
// Genuine native invocation, current owner binding and codecs are adapters.
using System;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
class Card{internal int Count=2;internal int GetRecipeCount()=>Count;}
class ItemGrid{internal int[] items;}
class Bag{internal ItemGrid ItemGrid=new ItemGrid{items=new[]{2}};}
class Inventory{internal ItemGrid ItemGrid=new ItemGrid{items=new[]{3}};}
class Viewer{internal Bag bag=new Bag();internal Inventory inventory=new Inventory();internal byte[] Equipment={4};}
class Station{internal int[] Output={1};}
class Grid{internal int[] Cells={1};internal int[] GetSlots()=>Cells;}
public static partial class RebirthRecipeOutputIntegration{
 internal enum OwningSink{Output=1,Inventory=2}
 internal sealed class OwningObservation{
  internal RemoteOutputColdClient RemoteOutputRestart;internal Card Card;internal bool Accepted,OriginalAccepted,RemoteFinalQueueObserved,Updated,Taken,RanUpdate=true,CurrentValue=true;
  internal long NativeVersionAfter=9;internal int BeforeCount=3,AfterCount;internal Station Station=new Station();internal Grid Grid;internal Viewer Viewer=new Viewer();
  internal byte[] OutputAfter,BagAfter,BeltAfter,EquipmentAfter;internal OwningSink AcceptedSink;
  internal bool Current(bool after)=>CurrentValue;internal bool RecipeMatches()=>true;
 }
 internal sealed partial class RemoteOutputColdClient{
  internal bool FinalCallbackSaved=true,CallbackReturned=true,QueueOnlyAuthorized=true,Bound=true;internal Card Card;internal Guid Operation=Guid.NewGuid();internal int Prefix=2;internal XElement request;
  internal bool BindingCurrent()=>Bound;
 }
 class Original{internal RemoteOutputColdClient RemoteCold;internal int OutputPrefix=2;}
 class Pending{internal Original Source;internal bool NativeCallbackEntered=true,NativeContinuationEntered=true,Returned=true;}
 static Dictionary<Card,Pending> pendingUiOutputs=new Dictionary<Card,Pending>();static long NativeVersion=9;static bool StationStable=true,Budget=true;
 static long OwningNativeVersion(Card card)=>NativeVersion;static bool StationSideImages(OwningObservation observed)=>StationStable;
 static bool SameOwningGrid(byte[] image,int[] cells)=>image!=null&&image.SequenceEqual(cells.Select(value=>(byte)value));
 static byte[] OwningEquipmentNative(Viewer viewer)=>viewer.Equipment;
 static bool ChargeOwning(OwningObservation observed,long bytes)=>Budget;
 static bool CurrentAcceptedImages(OwningObservation observed)=>StationSideImages(observed)&&SameOwningGrid(observed.OutputAfter,observed.Station.Output)&&SameOwningGrid(observed.BagAfter,observed.Viewer.bag.ItemGrid.items)&&SameOwningGrid(observed.BeltAfter,observed.Viewer.inventory.ItemGrid.items)&&observed.EquipmentAfter.SequenceEqual(observed.Viewer.Equipment);
 static RemoteOutputColdClient source;static OwningObservation observation;static XElement saved;static int checks;
 static void Fresh(){
  NativeVersion=9;StationStable=Budget=true;pendingUiOutputs.Clear();var card=new Card();source=new RemoteOutputColdClient{Card=card};observation=new OwningObservation{RemoteOutputRestart=source,Card=card};
  saved=new XElement("nativeOwningDelivery",new XAttribute("operation",source.Operation.ToString("N")),new XAttribute("beforeCount",3),new XAttribute("afterCount",2),new XAttribute("sink","Inventory"),new XAttribute("nativeOriginalAccepted",true),new XElement("outputAfter","AQ=="),new XElement("bagAfter","Ag=="),new XElement("beltAfter","Aw=="),new XElement("equipmentAfter","BA=="));
  source.request=new XElement("cachedViewer",new XElement("physicalDelivery",saved));pendingUiOutputs.Add(card,new Pending{Source=new Original{RemoteCold=source}});
 }
 static bool Observe()=>source.ObserveSavedFinalQueue(observation);static void Need(bool value,string name){if(!value)throw new Exception(name);checks++;}
 public static void Main(){
  Fresh();Need(Observe()&&observation.RemoteFinalQueueObserved&&observation.Updated&&observation.AfterCount==2&&!observation.Accepted&&!observation.OriginalAccepted,"actual decrement observed without claiming a new insertion");
  Need(source.CopySavedFinalQueueDelivery(observation,out var evidence)&&XNode.DeepEquals(evidence,saved)&&(bool)evidence.Attribute("nativeOriginalAccepted"),"historical saved delivery copied under separate queue witness");
  Need(!Observe()&&observation.RemoteFinalQueueObserved,"same observation cannot be replaced");
  Fresh();observation.RanUpdate=false;Need(!Observe(),"actual native invocation required");
  Fresh();source.QueueOnlyAuthorized=false;Need(!Observe(),"host queue-only permission required");
  Fresh();source.CallbackReturned=false;Need(!Observe(),"positive callback return required");
  Fresh();source.Card.Count=1;Need(!Observe(),"two decrements refused");
  Fresh();NativeVersion=10;Need(!Observe(),"different native invocation version refused");
  Fresh();pendingUiOutputs[source.Card].NativeCallbackEntered=false;Need(!Observe(),"saved callback ownership required");
  Fresh();observation.Viewer.bag.ItemGrid.items=new[]{8};Need(!Observe()&&!observation.Updated,"current owner projection must match saved After");
  Fresh();Budget=false;Need(!Observe()&&!observation.RemoteFinalQueueObserved,"budget refusal retains unobserved state");
  Fresh();StationStable=false;Need(!Observe(),"unrelated station changes refused");
  Fresh();Observe();observation.Viewer.bag.ItemGrid.items=new[]{8};Need(!source.CopySavedFinalQueueDelivery(observation,out _),"changed accepted images cannot publish delivery");
  Fresh();Observe();source.FinalCallbackSaved=false;Need(!source.CopySavedFinalQueueDelivery(observation,out _),"prefix cannot assume final role");
  Fresh();source.Bound=false;Need(!Observe(),"stale actual source binding refused");
  Console.WriteLine("PASS "+checks+" actual saved final queue observation assertions; native producer adapters excluded");
 }
}
// ACTUAL_FINAL_QUEUE_OBSERVATION
