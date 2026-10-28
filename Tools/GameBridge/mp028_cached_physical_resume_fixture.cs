// AUTHORED_NOT_RUN. Complete retained cached physical partial. Native grant,
// owner prefix, base source lifecycle, codecs and viewer gates are adapters.
using System;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
class World{}
class ItemValue{}
class ItemStack{internal ItemValue itemValue;internal int count;internal ItemStack(ItemValue item,int count){itemValue=item;this.count=count;}}
class Recipe{internal int count=1;}
class Card{internal ItemValue outputItemValue;internal bool isInventoryFull,isCrafting;internal float craftingTimeLeft,totalCraftTimeLeft;internal Recipe GetRecipe()=>new Recipe();}
static class RebirthWorkstationCompletionOutbox{internal static string CurrentGuid(World world)=>"world";}
static class RebirthProductiveEarnedObservationBudget{
 internal static bool Admit=true;internal static int Live;
 internal sealed class Lease:IDisposable{bool disposed;internal Lease(){Live++;}public void Dispose(){if(!disposed){disposed=true;Live--;}}}
 internal static bool TryRetainDetached(long size,out IDisposable memory){memory=Admit?new Lease():null;return Admit;}
 internal static IDisposable TryEnterDecoder()=>new Lease();
}
static class RebirthWorkstationCompletionPublication{
 internal static Dictionary<string,XElement> Files=new Dictionary<string,XElement>();internal static bool Match=true,Publish=true;
 internal static long PublishedLength(string path)=>Files.ContainsKey(path)?100:-1;
 internal static XElement ReadPrimary(string path)=>Files.TryGetValue(path,out var value)?new XElement(value):null;
 internal static byte[] CopyPublishedBytes(string path)=>Files.ContainsKey(path)?new byte[100]:null;
 internal static bool MatchesPublishedBytes(string path,byte[] bytes)=>Match&&Files.ContainsKey(path)&&bytes?.Length==100;
 internal static bool Write(string path,XElement value){if(!Publish)return false;Files[path]=new XElement(value);return true;}
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal sealed class RemoteCachedPrimaryGrant{
  internal Guid Operation=Guid.NewGuid(),Ticket=Guid.NewGuid();internal string Original="original";internal long NativeVersion=9;internal bool CurrentValue=true,Native=true,RetainAllowed=true;
  internal World World=new World();internal Card Card=new Card();internal XElement Before=new XElement("nativeOwningBefore",new XAttribute("owner","creator"));
  internal XElement Full=new XElement("nativeOwningFullBoundary",new XAttribute("cachedTime",-.5f),new XAttribute("cachedTotalTime",20f),new XAttribute("cachedCrafting",true),new XElement("primaryNative","primary"));
  internal XElement CopyBefore()=>new XElement(Before);internal XElement CopyFull()=>new XElement(Full);internal long MeasureImage()=>100;
  internal bool CurrentWindow()=>CurrentValue;internal bool NativeCurrent()=>Native;internal IDisposable Retain()=>RetainAllowed?new RebirthProductiveEarnedObservationBudget.Lease():null;
 }
}
public static partial class RebirthRecipeOutputIntegration{
 internal sealed class RemoteOutputColdClient{
  internal RebirthWorkstationCompletionPipeline.RetainedCachedClientBinding RetainedCached;internal bool Before=true,Bound=true,FinalCallbackSaved,QueueOnlyAuthorized;
  internal Guid LocalOperation=Guid.NewGuid();internal string OriginalHash="owner-original",FramePath="frame";internal int Prefix=1;internal XElement Request=new XElement("cachedViewer",new XAttribute("generation","generation"));
  internal ItemValue Primary=new ItemValue();internal bool BeforeBindingCurrent()=>Before;internal bool BindingCurrent()=>Bound;internal bool TryPrimary(out ItemValue item){item=Primary;return true;}
 }
 internal static int Resumes;internal static bool ResumeAllowed=true;internal static bool ResumeOwningObservation(RebirthWorkstationCompletionPipeline.Observation observation){Resumes++;return ResumeAllowed;}
}
internal static partial class RebirthWorkstationCompletionPipeline{
 internal sealed class RetainedCachedClientBinding{
  internal RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant Grant=new RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant();internal Guid Action=Guid.NewGuid();internal string ActionPath="action";internal bool HostPrefixRestored=true,HostFinalOwnerRestored;
  internal XElement CopyPrepare()=>new XElement("cachedPhysicalPrepare");internal byte[] CopyActionImage()=>new byte[100];
 }
 internal sealed class Observation{internal RebirthRecipeOutputIntegration.RemoteOutputColdClient RemoteOutputRestart;internal bool RanUpdate;}
 internal sealed class Frame{internal Observation Observation;internal bool Attempted=true;}
 internal sealed class Stage{internal bool Saved=true;}
 internal sealed class Prefix{internal int Count=1;}
 internal sealed class ViewerPrefix{internal Stage Stage=new Stage();internal Prefix Prefix=new Prefix();}
 internal sealed class Viewer{internal ViewerPrefix Prefix=new ViewerPrefix();internal Stage After;}
 static Dictionary<Frame,Viewer> nativeViewerClients=new Dictionary<Frame,Viewer>();static Frame current;static bool ResumeGate=true;
 static bool CanResumeAdditionalOutputPrefix(Card card,int count)=>ResumeGate;
 static string Grid(ItemStack[] items)=>items.Length==1&&items[0].itemValue!=null&&items[0].count==1?"primary":"foreign";
 internal sealed partial class CachedPhysicalSource:IDisposable{
  internal readonly RebirthProductiveEarnedUiDeferredTransport.RemoteCachedPrimaryGrant Grant;internal readonly Guid Operation,Action;internal readonly string Path;internal readonly XElement Before,Full;internal long NativeVersion;internal Frame Frame;
  private IDisposable allocation,retention;private byte[] requestImage,preparedImage,readyImage;private string readyPath;private bool installed;
  internal World World=>Grant.World;internal Card Card=>Grant.Card;internal bool Prepared=>preparedImage!=null;internal bool Ready=>readyImage!=null;
  internal bool Current()=>allocation!=null&&retention!=null&&Grant.CurrentValue&&retainedPrefix.HostPrefixRestored&&requestImage!=null;
  public void Dispose(){allocation?.Dispose();retention?.Dispose();allocation=retention=null;}
 }
 static RetainedCachedClientBinding binding;static RebirthRecipeOutputIntegration.RemoteOutputColdClient owner;static int checks;
 static void Fresh(){if(RebirthProductiveEarnedObservationBudget.Live!=0)throw new Exception("Leaked allocation");nativeViewerClients.Clear();ResumeGate=true;RebirthRecipeOutputIntegration.ResumeAllowed=true;RebirthRecipeOutputIntegration.Resumes=0;RebirthWorkstationCompletionPublication.Match=RebirthWorkstationCompletionPublication.Publish=RebirthProductiveEarnedObservationBudget.Admit=true;RebirthWorkstationCompletionPublication.Files.Clear();
  binding=new RetainedCachedClientBinding();owner=new RebirthRecipeOutputIntegration.RemoteOutputColdClient{RetainedCached=binding};
  foreach(var part in new[]{"prepared","ready.generation"})RebirthWorkstationCompletionPublication.Files["action."+part]=new XElement("workstationCompletionIntent",new XAttribute("operation",binding.Grant.Operation.ToString("N")),new XAttribute("world","world"),new XElement("before",binding.Grant.CopyBefore()),new XElement(part=="prepared"?"cachedPhysicalPrepared":"cachedPhysicalReady",new XElement("ack",new XAttribute("stage",part=="prepared"?"cachedPhysicalPrepared":"cachedPhysicalReady"),new XAttribute("ticket",binding.Grant.Ticket.ToString("N")),new XAttribute("action",binding.Action.ToString("N")),new XAttribute("digest",binding.Grant.Original),new XAttribute("generation","generation"))));
 }
 static CachedPhysicalSource Capture()=>CachedPhysicalSource.FromRetainedPrefix(binding,owner);static void Need(bool value,string name){if(!value)throw new Exception(name);checks++;}
 public static void Main(){
  Fresh();using(var source=Capture())Need(source!=null&&source.Action==binding.Action&&source.Prepared&&source.Ready,"same action adopts saved Prepared/Ready");
  Fresh();binding.HostPrefixRestored=false;Need(Capture()==null,"host prefix proof required");
  Fresh();binding.Grant.Native=false;Need(Capture()==null,"actual native source required");
  Fresh();RebirthWorkstationCompletionPublication.Files.Remove("action.prepared");Need(Capture()==null,"missing Prepared refuses");
  Fresh();RebirthWorkstationCompletionPublication.Files["action.ready.generation"].Element("cachedPhysicalReady").Element("ack").SetAttributeValue("action",Guid.NewGuid().ToString("N"));Need(Capture()==null,"foreign action Ready refuses");
  Fresh();RebirthWorkstationCompletionPublication.Files["action.prepared"].Element("before").Elements().Single().SetAttributeValue("owner","foreign");Need(Capture()==null,"foreign original Before refuses");
  Fresh();owner.Before=false;Need(Capture()==null,"owner original before binding required");
  Fresh();RebirthProductiveEarnedObservationBudget.Admit=false;Need(Capture()==null,"allocation refusal");
  Fresh();binding.Grant.RetainAllowed=false;Need(Capture()==null&&RebirthProductiveEarnedObservationBudget.Live==0,"retention refusal cleans allocation");
  Fresh();using(var source=Capture()){
   RebirthWorkstationCompletionPublication.Publish=false;Need(!source.RestoreRetainedCardMetadata(owner)&&source.Card.outputItemValue==null,"metadata intent failure precedes UI writes");
   RebirthWorkstationCompletionPublication.Publish=true;Need(source.RestoreRetainedCardMetadata(owner)&&ReferenceEquals(source.Card.outputItemValue,owner.Primary)&&source.Card.isInventoryFull&&source.Card.craftingTimeLeft==-.5f&&source.Card.totalCraftTimeLeft==20f,"only original cached UI representation restored");
   var frame=new Frame{Observation=new Observation{RemoteOutputRestart=owner}};source.Frame=frame;nativeViewerClients.Add(frame,new Viewer());
   Need(source.BeginRetainedPrefix(out var selected)&&ReferenceEquals(selected,frame)&&ReferenceEquals(current,frame)&&RebirthRecipeOutputIntegration.Resumes==1,"same existing frame resumes through native observation");
   frame.Observation.RanUpdate=true;Need(!source.BeginRetainedPrefix(out _)&&RebirthRecipeOutputIntegration.Resumes==1,"actual native return cannot be replayed");
   frame.Observation.RanUpdate=false;nativeViewerClients[frame].Prefix.Stage.Saved=false;Need(!source.BeginRetainedPrefix(out _),"unsaved next prefix holds");
   nativeViewerClients[frame].Prefix.Stage.Saved=true;ResumeGate=false;Need(!source.BeginRetainedPrefix(out _),"full viewer resume gate required");
  }
  Fresh();owner.FinalCallbackSaved=owner.QueueOnlyAuthorized=binding.HostFinalOwnerRestored=true;using(var source=Capture()){
   var frame=new Frame{Observation=new Observation{RemoteOutputRestart=owner}};source.Frame=frame;nativeViewerClients.Add(frame,new Viewer{Prefix=null,After=new Stage()});
   Need(source.BeginRetainedPrefix(out _)&&RebirthRecipeOutputIntegration.Resumes==1,"final role resumes saved After without an insertion-prefix replay");
  }
  Fresh();owner.FinalCallbackSaved=binding.HostFinalOwnerRestored=true;Need(Capture()==null,"final role requires queue-only permission");
  Fresh();owner.FinalCallbackSaved=owner.QueueOnlyAuthorized=true;Need(Capture()==null,"final role requires distinct saved final host reply");
  Need(RebirthProductiveEarnedObservationBudget.Live==0,"all source and decoder leases released");
  Console.WriteLine("PASS "+checks+" retained cached physical-source assertions; native adapters excluded");
 }
}
// ACTUAL_CACHED_PHYSICAL_RESUME
