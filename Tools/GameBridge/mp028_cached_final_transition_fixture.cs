// AUTHORED_NOT_RUN. Complete production physical transition validator. The
// codec adapter preserves every represented field; actual native codec/PDS are excluded.
using System;using System.Linq;using System.Text;using System.Xml.Linq;
class Recipe{internal int count=1,itemValueType=1;}
class ItemValue{internal string Id;internal ItemValue(string id){Id=id;}}
class ItemStack{internal ItemValue itemValue;internal int count;internal ItemStack(string id,int count=1){itemValue=new ItemValue(id);this.count=count;}}
class Row{internal Recipe Recipe=new Recipe();internal short Multiplier=2;internal float CraftingTimeLeft,OneItemCraftTime=7;internal bool IsCrafting=true;internal int Quality=1,StartingEntityId=5;}
class Body{internal ItemStack[] Output;internal Row[] Queue;internal string Input="input",Fuel="fuel",Tools="tools",Completions="aggregate-ledger";}
class GameTimer{internal static GameTimer Instance=new GameTimer();internal ulong ticks=1000;}
static class GridCodec{
 internal static string Encode(ItemStack[] cells)=>new XElement("grid",cells.Select(cell=>new XElement("cell",new XAttribute("id",cell.itemValue.Id),new XAttribute("count",cell.count)))).ToString(SaveOptions.DisableFormatting);
 internal static ItemStack[] Decode(string value)=>XElement.Parse(value).Elements("cell").Select(cell=>new ItemStack((string)cell.Attribute("id"),(int)cell.Attribute("count"))).ToArray();
}
static class RebirthCamperInventorySnapshot{internal static byte[] Capture(ItemStack[] cells)=>Encoding.UTF8.GetBytes(GridCodec.Encode(cells));}
static class RebirthProductiveEarnedObservationBudget{internal sealed class Lease:IDisposable{public void Dispose(){}}internal static IDisposable TryEnterDecoder()=>new Lease();}
static class RebirthProductiveEarnedNativeBody{
 internal static byte[] Encode(Body body,ulong elapsed,out int offset){offset=0;return Encoding.UTF8.GetBytes(new XElement("body",new XAttribute("elapsed",elapsed),new XElement("input",body.Input),new XElement("fuel",body.Fuel),new XElement("tools",body.Tools),new XElement("completions",body.Completions),new XElement("output",GridCodec.Encode(body.Output)),new XElement("queue",body.Queue.Select(row=>new XElement("row",new XAttribute("recipe",row.Recipe!=null),new XAttribute("count",row.Multiplier),new XAttribute("time",row.CraftingTimeLeft),new XAttribute("one",row.OneItemCraftTime),new XAttribute("crafting",row.IsCrafting),new XAttribute("quality",row.Quality),new XAttribute("owner",row.StartingEntityId))))).ToString(SaveOptions.DisableFormatting));}
 internal static bool TryRead(byte[] bytes,int offset,out Body body,out ulong elapsed){var node=XElement.Parse(Encoding.UTF8.GetString(bytes));elapsed=(ulong)node.Attribute("elapsed");body=new Body{Input=node.Element("input").Value,Fuel=node.Element("fuel").Value,Tools=node.Element("tools").Value,Completions=node.Element("completions").Value,Output=GridCodec.Decode(node.Element("output").Value),Queue=node.Element("queue").Elements("row").Select(row=>new Row{Recipe=(bool)row.Attribute("recipe")?new Recipe():null,Multiplier=(short)row.Attribute("count"),CraftingTimeLeft=(float)row.Attribute("time"),OneItemCraftTime=(float)row.Attribute("one"),IsCrafting=(bool)row.Attribute("crafting"),Quality=(int)row.Attribute("quality"),StartingEntityId=(int)row.Attribute("owner")}).ToArray()};return offset==0;}
}
static class RebirthRemoteResourcePlacedStationQueue{internal static Row[] Rows;internal static bool TryDecode(string text,out Row[] rows){rows=Rows;return text=="recipe-snapshot"||text=="ui-snapshot";}}
static class RebirthWorkstationCompletionWitnessValidator{internal static bool Matches(XElement active,XElement delivery,int count)=>true;}
static class RebirthProductiveEarnedUiQueueOwner{internal static string Binding(Row row)=>row.Recipe==null?"empty":"recipe";}
static class RebirthProductiveEarnedUiRemote{
 internal static ItemStack[] ReadGrid(XElement node,string name)=>GridCodec.Decode(node.Element(name).Value);
 internal static bool ValidateExtraOutputs(Recipe recipe,XElement node,ItemStack[] extras)=>extras.Length==1&&extras[0].itemValue.Id=="extra"&&extras[0].count==1;
}
static class RebirthRecipeOutputIntegration{
 internal static bool PredictOwningOutput(ItemStack[] before,ItemStack primary,ItemStack[] extras,bool cached,out ItemStack[] merged){merged=before.Concat(new[]{primary}).Concat(extras).ToArray();return true;}
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal static long MeasurePublicationElement(XElement node)=>Encoding.UTF8.GetByteCount(node.ToString());
 internal static class NativeViewerPds{internal static bool Owner=true;internal static bool ValidateCachedPhysicalActor(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement active,XElement delivery,ItemStack primary,ItemStack[] extras,bool inventory)=>Owner;}
}
internal static partial class RebirthWorkstationCompletionPipeline{
 internal sealed class Cache{internal XElement Original,CurrentFull;internal bool Reserve=true;internal bool ReserveRetirementWorkspace(long bytes)=>Reserve;}
 internal sealed class Viewer{internal int entityId=99;}
 internal sealed class Issue{internal Cache Cache;internal Viewer Viewer=new Viewer();}
 internal sealed class CachedPhysicalHostAction{internal Issue Issue;internal bool PhysicalAccepted;internal bool Current()=>true;}
 static string RecipeBinding(Row row)=>RebirthProductiveEarnedUiQueueOwner.Binding(row);
 static CachedPhysicalHostAction action;static XElement active,delivery;static byte[] before;static Body proposed;static int checks;
 static Body Clone(Body body){RebirthProductiveEarnedNativeBody.TryRead(RebirthProductiveEarnedNativeBody.Encode(body,10,out _),0,out var clone,out _);return clone;}
 static void Setup(short count=2,bool inventory=false){
  RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Owner=true;
  var body=new Body{Output=new[]{new ItemStack("old")},Queue=new[]{new Row{Multiplier=count}}};RebirthRemoteResourcePlacedStationQueue.Rows=Clone(body).Queue;
  var full=new XElement("nativeOwningFullBoundary",new XElement("primaryNative",GridCodec.Encode(new[]{new ItemStack("primary")})));
  active=new XElement("nativeOwningBefore",new XAttribute("retry",true),new XAttribute("slot",0),new XAttribute("beforeCount",count),new XAttribute("nativeQueueCountBefore",count),new XAttribute("stationBirth","birth"),new XElement("recipeFull","recipe-snapshot"),new XElement("recipeNative","native-recipe"),new XElement("uiQueueFull","ui-snapshot"),new XElement("cachedPredecessor",full));
  foreach(string field in new[]{"operation","world","viewer","creation","deaths","x","y","z","block","startingOwner","startingOwnerKnown","startingOwnerKey","startingOwnerCreation","startingOwnerDeaths","extrasDefinition"})active.SetAttributeValue(field,"same");
  delivery=new XElement("nativeOwningDelivery",active.Attributes().Select(attribute=>new XAttribute(attribute)),new XAttribute("afterCount",count-1),new XAttribute("sink",inventory?"Inventory":"Output"),new XAttribute("nativeChildrenAccepted",true),new XElement("recipeFull","recipe-snapshot"),new XElement("recipeNative","native-recipe"),new XElement("cachedPredecessor",new XElement(full)),new XElement("primaryNative",GridCodec.Encode(new[]{new ItemStack("primary")})),new XElement("extrasNative",GridCodec.Encode(new[]{new ItemStack("extra")})));
  var after=inventory?body.Output:body.Output.Concat(new[]{new ItemStack("primary"),new ItemStack("extra")}).ToArray();
  foreach(var pair in new[]{new[]{"outputBefore",GridCodec.Encode(body.Output)},new[]{"outputAfter",GridCodec.Encode(after)},new[]{"bagBefore",GridCodec.Encode(new ItemStack[0])},new[]{"bagAfter",GridCodec.Encode(new ItemStack[0])},new[]{"beltBefore",GridCodec.Encode(new ItemStack[0])},new[]{"beltAfter",GridCodec.Encode(new ItemStack[0])}})delivery.Add(new XElement(pair[0],pair[1]));
  foreach(string name in new[]{"outputBefore","bagBefore","beltBefore"})active.Add(new XElement(name,delivery.Element(name).Value));
  action=new CachedPhysicalHostAction{Issue=new Issue{Cache=new Cache{Original=new XElement("workstationCompletionIntent",new XElement("before",new XElement(active))),CurrentFull=new XElement(full)}}};
  before=RebirthProductiveEarnedNativeBody.Encode(body,10,out _);proposed=Clone(body);proposed.Output=after;proposed.Queue[0].Multiplier--;
  if(count==1){var row=proposed.Queue[0];row.Recipe=null;row.CraftingTimeLeft=0;row.IsCrafting=false;row.Quality=1;row.StartingEntityId=99;row.OneItemCraftTime=0;}
  else proposed.Queue[0].CraftingTimeLeft=7;
 }
 static bool Validate(int phase=LoadedCachedFinalValidation,ulong elapsed=20)=>ValidateCachedPhysicalDelivery(action,active,delivery,before,0,RebirthProductiveEarnedNativeBody.Encode(proposed,elapsed,out _),0,phase);
 static void Need(bool value,string name){if(!value)throw new Exception(name);checks++;}
 public static void Main(){
  Setup();Need(Validate(),"complete output plus one queue decrement in one native image");Need(!action.PhysicalAccepted,"validation does not fabricate prior native acceptance");
  Need(!Validate(3),"ordinary queue-only phase cannot change output");Need(!Validate(2),"ordinary output phase cannot decrement queue");
  Setup();proposed.Input="foreign";Need(!Validate(),"unrelated input mutation refused");
  Setup();proposed.Completions="foreign";Need(!Validate(),"aggregate receipts cannot change");
  Setup();proposed.Queue[0].Multiplier--;Need(!Validate(),"second decrement refused");
  Setup();proposed.Queue[0].CraftingTimeLeft=99;Need(!Validate(),"queue timer must match original native continuation");
  Setup();action.Issue.Cache.Reserve=false;Need(!Validate(),"workspace refusal");
  Setup();RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Owner=false;Need(!Validate(),"full saved owner proof required");
  Setup();active.SetAttributeValue("viewer","foreign");Need(!Validate(),"original actor identity unchanged");
  Setup();Need(!Validate(LoadedCachedFinalValidation,1001),"future elapsed clock refused");
  Setup(1);Need(Validate(),"exhausted native row cleared exactly");proposed.Queue[0].StartingEntityId=5;Need(!Validate(),"empty row belongs to actual native viewer metadata");
  Setup(2,true);Need(Validate(),"inventory output keeps station grid unchanged");
  Setup();delivery.SetAttributeValue("sink","foreign");Need(!Validate(),"unknown sink refused");
  Setup();proposed.Output=new[]{new ItemStack("old"),new ItemStack("primary")};Need(!Validate(),"missing declared extra output refused");
  Console.WriteLine("PASS "+checks+" actual combined native transition assertions; native codec/PDS adapters excluded");
 }
}
// ACTUAL_CACHED_FINAL_TRANSITION
