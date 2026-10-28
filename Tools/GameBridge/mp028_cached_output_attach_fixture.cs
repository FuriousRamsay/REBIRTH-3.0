// AUTHORED_NOT_RUN. Complete production attachment coordinator. Actual native
// grant/owner/observation and the separately covered physical source are adapters.
using System;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
class XUiC_RecipeStack{}
class Window{}
class Station{internal object[] Queue={new object()};}
static class RebirthProductiveEarnedObservationBudget{
 internal static bool Admit=true;internal static int Live;
 internal sealed class Lease:IDisposable{bool disposed;internal Lease(){Live++;}public void Dispose(){if(!disposed){disposed=true;Live--;}}}
 internal static bool TryRetainDetached(long size,out IDisposable memory){memory=Admit?new Lease():null;return Admit;}
}
static class RebirthWorkstationCompletionPublication{
 internal static Dictionary<string,XElement> Files=new Dictionary<string,XElement>();
 internal static XElement ReadPrimary(string path)=>Files.TryGetValue(path,out var value)?new XElement(value):null;
}
static class RebirthProductiveEarnedUiQueueOwner{
 internal sealed class Witness{internal string OriginalOwnerKey="creator",Creation="creation";internal int Deaths=2;internal object NativeHydration=new object();internal bool Published=true;internal bool SourcePublished()=>Published;}
 internal static Witness Value;internal static bool Allow=true;internal static string Purpose;
 internal static bool TryObserveRemoteNativeSource(XUiC_RecipeStack card,Station station,int index,object row,string purpose,out Witness owner){Purpose=purpose;owner=Value;return Allow;}
}
static class RebirthProductiveEarnedUiDeferredTransport{
 internal static bool Session=true;
 internal static bool TryGetCurrentNativeUiSession(Window window,out long session){session=12;return Session;}
}
public static partial class RebirthRecipeOutputIntegration{
 internal sealed class AdditionalOutputPrefix{internal int Count=1;}
 internal sealed class RemoteOutputColdClient{
  internal RebirthWorkstationCompletionPipeline.RetainedCachedClientBinding RetainedCached;
  internal Guid Operation,LocalOperation=Guid.NewGuid();internal string FramePath="frame";internal bool FinalCallbackSaved,QueueOnlyAuthorized,Attached,Active=true,Bind=true,Attach=true,ThrowAfterPending,Pending;
  internal XElement Remote,PrefixRequest;internal XUiC_RecipeStack Card;internal int Prefix=1;
  internal XElement Source=>new XElement(Remote);internal XElement Request=>new XElement(PrefixRequest);
  internal bool BeforeBindingCurrent()=>Active&&!Attached;internal bool MatchesCard(XUiC_RecipeStack card)=>ReferenceEquals(RetainedCached.Grant.Card,card);
  internal bool BindNativeCard(XUiC_RecipeStack card,RebirthProductiveEarnedUiQueueOwner.Witness owner){if(!Bind)return false;Attached=true;Card=card;return true;}
  internal object[] Outputs()=>new object[0];
  internal AdditionalOutputPrefix AttachOriginal(out bool final){final=FinalCallbackSaved;if(ThrowAfterPending){Pending=true;throw new InvalidOperationException("explicit late refusal adapter");}if(!Attach)return null;Pending=true;return final?null:new AdditionalOutputPrefix();}
  internal void UndoUnenteredBinding(){if(!Pending){Attached=false;Card=null;}}
 }
 internal sealed class OwningObservation:IDisposable{
  internal static int Live;bool disposed;internal OwningObservation(){Live++;}
  internal int BeforeCount=3;internal string RecipeFull="recipe",UiQueueFull="queue";internal byte[] BagBefore,BeltBefore,EquipmentBefore,ExtrasNative;internal bool ExtrasBound;
  internal XElement CachedPredecessor;internal RemoteOutputColdClient RemoteOutputRestart;
  internal bool Current()=>true;internal bool RecipeMatches()=>true;
  public void Dispose(){if(!disposed){disposed=true;Live--;}}
 }
 internal static bool ObservationAllowed=true;internal static int ObservedCount=3;internal static string ObservedQueue="queue";internal static int Ends;
 internal static OwningObservation BeginOwningObservation(XUiC_RecipeStack card,bool retry,Guid operation)=>ObservationAllowed&&retry?new OwningObservation{BeforeCount=ObservedCount,UiQueueFull=ObservedQueue}:null;
 internal static void EndOwningObservation(OwningObservation scope){Ends++;}
 internal static byte[] OwningGrid(object[] items)=>new byte[]{9};
 internal static bool HasRetainedCachedPendingOutput(RemoteOutputColdClient source)=>source.Pending;
}
internal static partial class RebirthWorkstationCompletionPipeline{
 internal sealed class Grant{internal XUiC_RecipeStack Card=new XUiC_RecipeStack();internal Window Window=new Window();internal Station Station=new Station();internal string OwnerPurpose="original-owner-purpose",Original="original";}
 internal sealed class RetainedCachedClientBinding{internal Grant Grant=new Grant();internal Guid Action=Guid.NewGuid();internal bool HostPrefixRestored=true,HostFinalOwnerRestored;}
 internal sealed class Frame{
  internal RebirthRecipeOutputIntegration.OwningObservation Observation;internal XElement Intent;internal string Path,Owner,Creation,RemoteNativeSourceHash;internal RebirthProductiveEarnedUiQueueOwner.Witness EarlierOwner;
  internal bool Prepared,Attempted,Saved;internal Guid RemoteNativeNonce;internal CachedPhysicalSource CachedPhysical;
 }
 internal sealed class ViewerClientStage{internal XElement Request,Frame;internal string Digest,NativeDigest;internal int NativeLength;internal bool Sent,Captured,Armed,Saved;}
 internal sealed class ViewerOutputPrefix{internal RebirthRecipeOutputIntegration.AdditionalOutputPrefix Prefix;internal ViewerClientStage Stage;internal bool HeaderSent;}
 internal sealed class NativeViewerClient:IDisposable{
  internal Frame Frame;internal string Generation,BagLocks,BeltLocks,BeltPreferred,EquipmentPreferred;internal long Session;internal object Connection;internal IDisposable Memory;internal int BeltCount;internal ViewerClientStage Before,After;internal ViewerOutputPrefix Prefix;
  internal static bool Allow=true;internal bool Current()=>Allow&&(Prefix!=null||After?.Saved==true);public void Dispose(){Memory?.Dispose();Memory=null;}
 }
 internal sealed class CachedPhysicalSource:IDisposable{
  internal static bool Allow=true,Metadata=true;internal static int Live;bool disposed;internal Frame Frame;
  internal CachedPhysicalSource(){Live++;}
  internal static CachedPhysicalSource FromRetainedPrefix(RetainedCachedClientBinding binding,RebirthRecipeOutputIntegration.RemoteOutputColdClient source)=>Allow?new CachedPhysicalSource():null;
  internal bool RestoreRetainedCardMetadata(RebirthRecipeOutputIntegration.RemoteOutputColdClient source)=>Metadata;
  public void Dispose(){if(!disposed){disposed=true;Live--;}}
 }
 static Dictionary<Guid,Frame> frames=new Dictionary<Guid,Frame>();static Dictionary<XUiC_RecipeStack,Frame> cards=new Dictionary<XUiC_RecipeStack,Frame>();
 static Dictionary<Guid,CachedPhysicalSource> cachedPhysicalSources=new Dictionary<Guid,CachedPhysicalSource>();static Dictionary<Frame,NativeViewerClient> nativeViewerClients=new Dictionary<Frame,NativeViewerClient>();
 static XElement ActiveBefore(XElement intent)=>intent?.Element("before")?.Elements().SingleOrDefault();
 static object CurrentNativeServerConnection()=>new object();static string RemoteOutputRequestHash(XElement request)=>"digest";static void RememberRemoteOutputPrefixAudit(NativeViewerClient viewer){}
 static RebirthRecipeOutputIntegration.RemoteOutputColdClient source;static int checks;
 static void Clear(){foreach(var value in nativeViewerClients.Values)value.Dispose();foreach(var value in frames.Values.Distinct())value.Observation.Dispose();foreach(var value in cachedPhysicalSources.Values)value.Dispose();nativeViewerClients.Clear();frames.Clear();cards.Clear();cachedPhysicalSources.Clear();}
 static void Fresh(){Clear();if(RebirthProductiveEarnedObservationBudget.Live!=0||RebirthRecipeOutputIntegration.OwningObservation.Live!=0||CachedPhysicalSource.Live!=0)throw new Exception("Coordinator leaked temporary graph");
  RebirthProductiveEarnedObservationBudget.Admit=RebirthProductiveEarnedUiQueueOwner.Allow=RebirthProductiveEarnedUiDeferredTransport.Session=RebirthRecipeOutputIntegration.ObservationAllowed=NativeViewerClient.Allow=CachedPhysicalSource.Allow=CachedPhysicalSource.Metadata=true;
  RebirthRecipeOutputIntegration.ObservedCount=3;RebirthRecipeOutputIntegration.ObservedQueue="queue";RebirthRecipeOutputIntegration.Ends=0;RebirthProductiveEarnedUiQueueOwner.Value=new RebirthProductiveEarnedUiQueueOwner.Witness();RebirthWorkstationCompletionPublication.Files.Clear();
  var binding=new RetainedCachedClientBinding();var active=new XElement("nativeOwningBefore",new XAttribute("slot",0),new XAttribute("startingOwnerKey","creator"),new XAttribute("startingOwnerCreation","creation"),new XAttribute("startingOwnerDeaths",2),new XAttribute("beforeCount",3),new XElement("recipeFull","recipe"),new XElement("uiQueueFull","queue"),new XElement("bagBefore","AQ=="),new XElement("beltBefore","Ag=="),new XElement("equipmentBefore","Aw=="),new XElement("cachedPredecessor",new XElement("nativeOwningFullBoundary")));
  var request=new XElement("cachedViewer",new XAttribute("generation","generation"),new XAttribute("bagLocks","locks"),new XAttribute("beltLocks","locks"),new XAttribute("beltCount",10),new XAttribute("pdsDigest","native"),new XAttribute("pdsLength",3),new XElement("activeBefore",active),new XElement("nativeOwnerFrame",new XElement("bag","accepted")));
  source=new RebirthRecipeOutputIntegration.RemoteOutputColdClient{Operation=Guid.NewGuid(),RetainedCached=binding,Remote=new XElement("remoteOutputSource",new XElement("activeBefore",active)),PrefixRequest=request};
  RebirthWorkstationCompletionPublication.Files["frame"]=new XElement("workstationCompletionIntent",new XAttribute("prepared",true),new XAttribute("attempted",true),new XAttribute("nativeNonce",binding.Action.ToString("N")),new XAttribute("nativeSource",binding.Grant.Original),new XElement("before",active));
  RebirthWorkstationCompletionPublication.Files["frame.viewer.generation.before"]=new XElement("workstationCompletionIntent",new XElement("before",new XElement(request)));
 }
 static bool Attach()=>TryAttachRetainedCachedOutput(source);static void Need(bool value,string name){if(!value)throw new Exception(name);checks++;}
 public static void Main(){
  Fresh();Need(Attach()&&source.Attached&&source.Pending&&frames.Count==1&&nativeViewerClients.Count==1&&cachedPhysicalSources.Count==1,"complete attachment installs same pending graph");
  var frame=frames.Values.Single();Need(frame.RemoteNativeNonce==source.RetainedCached.Action&&ReferenceEquals(frame.CachedPhysical,cachedPhysicalSources[source.Operation])&&RebirthProductiveEarnedUiQueueOwner.Purpose==source.RetainedCached.Grant.OwnerPurpose,"original action and native owner purpose retained");
  Need(nativeViewerClients[frame].Before.Saved&&nativeViewerClients[frame].Prefix.Stage.Saved&&frame.Observation.ExtrasBound&&frame.Observation.CachedPredecessor!=null,"saved viewer prefix and cached role installed");
  Need(!Attach()&&frames.Count==1&&source.Pending,"already attached action cannot be replaced");
  Fresh();Need(RebirthProductiveEarnedObservationBudget.Live==0&&CachedPhysicalSource.Live==0,"retained graph cleanup");
  source.RetainedCached.HostPrefixRestored=false;Need(!Attach()&&!source.Attached,"host source required");
  Fresh();RebirthProductiveEarnedUiQueueOwner.Value.Published=false;Need(!Attach(),"published original owner required");
  Fresh();RebirthProductiveEarnedUiQueueOwner.Value.NativeHydration=null;Need(!Attach(),"actual native owner hydration required");
  Fresh();RebirthProductiveEarnedUiQueueOwner.Value.Creation="foreign";Need(!Attach(),"wrong owner incarnation refuses");
  Fresh();CachedPhysicalSource.Allow=false;Need(!Attach()&&!source.Attached,"physical source refusal");
  Fresh();source.Bind=false;Need(!Attach()&&CachedPhysicalSource.Live==0,"failed native card binding disposes source");
  Fresh();CachedPhysicalSource.Metadata=false;Need(!Attach()&&!source.Attached&&cachedPhysicalSources.Count==0,"metadata failure rolls back unentered graph");
  Fresh();RebirthRecipeOutputIntegration.ObservationAllowed=false;Need(!Attach()&&!source.Pending&&CachedPhysicalSource.Live==0,"missing native observation holds without outputs");
  Fresh();RebirthRecipeOutputIntegration.ObservedCount=4;Need(!Attach()&&RebirthRecipeOutputIntegration.OwningObservation.Live==0,"wrong original count disposes observation");
  Fresh();RebirthRecipeOutputIntegration.ObservedQueue="foreign";Need(!Attach(),"changed UI queue refuses");
  Fresh();RebirthProductiveEarnedObservationBudget.Admit=false;Need(!Attach()&&CachedPhysicalSource.Live==0,"viewer budget refusal releases new physical source");
  Fresh();RebirthWorkstationCompletionPublication.Files["frame"].SetAttributeValue("nativeNonce",Guid.NewGuid().ToString("N"));Need(!Attach()&&RebirthProductiveEarnedObservationBudget.Live==0,"foreign frame action releases workspace");
  Fresh();RebirthWorkstationCompletionPublication.Files.Remove("frame.viewer.generation.before");Need(!Attach()&&RebirthProductiveEarnedObservationBudget.Live==0,"missing saved Before releases viewer");
  Fresh();source.Attach=false;Need(!Attach()&&!source.Pending&&!source.Attached&&frames.Count==0,"unentered original refusal rolls back fully");
  Fresh();source.ThrowAfterPending=true;Need(!Attach()&&source.Pending&&source.Attached&&frames.Count==1&&nativeViewerClients.Count==1&&cachedPhysicalSources.Count==1,"late refusal retains exact pending graph");
  Fresh();NativeViewerClient.Allow=false;Need(!Attach()&&source.Pending&&frames.Count==1&&nativeViewerClients.Count==1,"registered viewer refusal preserves original custody");
  Fresh();source.FinalCallbackSaved=source.QueueOnlyAuthorized=source.RetainedCached.HostFinalOwnerRestored=true;Need(Attach()&&nativeViewerClients.Values.Single().After.Saved&&nativeViewerClients.Values.Single().Prefix==null,"final callback attaches saved After without replaying prefix");
  Fresh();source.FinalCallbackSaved=source.RetainedCached.HostFinalOwnerRestored=true;Need(!Attach()&&!source.Attached&&!source.Pending,"missing queue-only authority refuses before attachment");
  Fresh();source.FinalCallbackSaved=source.QueueOnlyAuthorized=true;Need(!Attach()&&!source.Pending,"ordinary host prefix cannot authorize a final callback");
  Clear();Need(RebirthProductiveEarnedObservationBudget.Live==0&&RebirthRecipeOutputIntegration.OwningObservation.Live==0&&CachedPhysicalSource.Live==0,"all refusal and retained graphs release once");
  Console.WriteLine("PASS "+checks+" actual cached attachment coordinator assertions; native adapters excluded");
 }
}
// ACTUAL_CACHED_OUTPUT_ATTACH
