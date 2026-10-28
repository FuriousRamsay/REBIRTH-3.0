// AUTHORED_NOT_RUN. Complete saved-owner capability and loaded-final source.
// Genuine saved PDS/native cache and the separately covered full validator are adapters.
using System;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;using System.Text;
class ClientInfo{internal XElement latestPlayerData;}
static class RebirthProductiveEarnedUiOwnerFile{internal static bool MatchesDeferredOwnerFrame(XElement current,XElement expected)=>XNode.DeepEquals(current,expected);}
static class RebirthProductiveEarnedObservationBudget{
 internal static bool Admit=true;internal static int Live;internal sealed class Lease:IDisposable{bool disposed;internal Lease(){Live++;}public void Dispose(){if(!disposed){disposed=true;Live--;}}}
 internal static bool TryRetainDetached(long bytes,out IDisposable memory){memory=Admit?new Lease():null;return Admit;}
}
static class RebirthProductiveEarnedNativeBody{internal static bool SameExceptElapsed(byte[] first,int offset,byte[] second,int secondOffset)=>offset==secondOffset&&first.SequenceEqual(second);}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 static long MeasurePublicationElement(XElement node)=>Encoding.UTF8.GetByteCount(node.ToString());
 internal static partial class NativeViewerPds{
  internal sealed class Stage{internal bool Saved=true;internal XElement Frame,Request=new XElement("cachedViewer");internal bool Current()=>Saved;}
  internal sealed class ViewerSource{
   internal RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction Cached;internal string Generation="generation";internal Dictionary<string,byte[]> RestoredCachedInputs=new Dictionary<string,byte[]>();
   internal bool CurrentValue=true,BeforeInventory=true,AfterInventory=true;internal Stage Before=new Stage(),After=new Stage();internal XElement Active=new XElement("nativeOwningBefore"),Delivery=new XElement("nativeOwningDelivery");
   internal bool Current()=>CurrentValue&&Cached.Current();internal bool BeforeInventoryCurrent()=>BeforeInventory;internal bool InventoryCurrent()=>AfterInventory;
   internal ClientInfo SourceSender=>Cached.Sender;
  }
  static object gate=new object();static Dictionary<string,ViewerSource> sources=new Dictionary<string,ViewerSource>();
  static string CachedKey(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,string generation)=>action.Action.ToString("N")+generation;
  internal static ViewerSource Seed(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action){sources.Clear();var frame=new XElement("nativeOwnerFrame",new XElement("bag","final"));action.Sender.latestPlayerData=new XElement(frame);var source=new ViewerSource{Cached=action};source.Before.Frame=new XElement("nativeOwnerFrame",new XElement("bag","before"));source.After.Frame=frame;sources.Add(CachedKey(action,source.Generation),source);return source;}
 }
}
internal static partial class RebirthWorkstationCompletionPipeline{
 internal sealed class Cache{
  internal byte[] Native={1,2,3};internal int Offset,Leases;internal bool Saved=true,NativeState=true,Retain=true;
  internal bool SavedCurrent()=>Saved;internal bool NativeCurrent()=>NativeState;internal IDisposable RetainResource()=>Retain?new Hold(this):null;
  sealed class Hold:IDisposable{Cache cache;internal Hold(Cache value){cache=value;value.Leases++;}public void Dispose(){if(cache!=null){cache.Leases--;cache=null;}}}
 }
 internal sealed class Issue{internal Guid RetainedAction;internal Cache Cache=new Cache();}
 internal sealed partial class CachedPhysicalHostAction{
  internal Guid Action=Guid.NewGuid();internal string BeforeGeneration="generation";internal Issue Issue;internal bool PhysicalAccepted,PhysicalFinalObserved,CurrentValue=true;internal ClientInfo Sender=new ClientInfo();
  internal CachedPhysicalHostAction(){Issue=new Issue{RetainedAction=Action};}internal bool Current()=>CurrentValue;
 }
 const int LoadedCachedFinalValidation=7;static bool AllowValidate=true;static int Validations;
 static bool ValidateCachedPhysicalDelivery(CachedPhysicalHostAction action,XElement active,XElement delivery,byte[] before,int offset,byte[] proposed,int proposedOffset,int phase){Validations++;return AllowValidate&&phase==LoadedCachedFinalValidation;}
 static CachedPhysicalHostAction action;static RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.ViewerSource owner;static int checks;
 static void Fresh(){if(action?.LoadedFinal!=null){action.LoadedFinal.Dispose();action.LoadedFinal=null;}if(RebirthProductiveEarnedObservationBudget.Live!=0)throw new Exception("Leaked source allocation");RebirthProductiveEarnedObservationBudget.Admit=AllowValidate=true;Validations=0;action=new CachedPhysicalHostAction();owner=RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Seed(action);}
 static void Need(bool value,string name){if(!value)throw new Exception(name);checks++;}
 public static void Main(){
  Fresh();Need(LoadedCachedFinalTransition.TryInstall(action)&&action.LoadedFinal.Current()&&!action.PhysicalAccepted&&action.Issue.Cache.Leases==1,"actual saved owner plus native Before installs separate role");
  var original=action.LoadedFinal;Need(LoadedCachedFinalTransition.TryInstall(action)&&ReferenceEquals(original,action.LoadedFinal)&&action.Issue.Cache.Leases==1,"same role installation is idempotent");
  Need(original.Validate(owner.Active,owner.Delivery,new byte[]{9},0)&&Validations==1,"complete native transition validator required");
  Need(!original.Validate(new XElement("foreign"),owner.Delivery,new byte[]{9},0)&&Validations==1,"foreign header cannot reach native validator");
  action.PhysicalFinalObserved=true;Need(!original.Current()&&!original.Validate(owner.Active,owner.Delivery,new byte[]{9},0),"actual native completion disables another queue transition");
  Fresh();owner.After.Saved=false;Need(!LoadedCachedFinalTransition.TryInstall(action),"positive final saved native PDS required");
  Fresh();owner.RestoredCachedInputs=null;Need(!LoadedCachedFinalTransition.TryInstall(action),"ordinary live source cannot assume loaded-final role");
  Fresh();action.Issue.RetainedAction=Guid.NewGuid();Need(!LoadedCachedFinalTransition.TryInstall(action),"different retained action refuses");
  Fresh();action.Issue.Cache.Saved=false;Need(!LoadedCachedFinalTransition.TryInstall(action),"actual saved native cache required");
  Fresh();RebirthProductiveEarnedObservationBudget.Admit=false;Need(!LoadedCachedFinalTransition.TryInstall(action)&&action.Issue.Cache.Leases==0,"budget refusal acquires no cache lease");
  Fresh();action.Issue.Cache.Retain=false;Need(!LoadedCachedFinalTransition.TryInstall(action)&&RebirthProductiveEarnedObservationBudget.Live==0,"retention refusal releases allocation");
  Fresh();LoadedCachedFinalTransition.TryInstall(action);action.Issue.Cache.Native[0]=8;Need(!action.LoadedFinal.Current(),"original native Before frozen independently");
  Fresh();LoadedCachedFinalTransition.TryInstall(action);action.Sender.latestPlayerData=new XElement("nativeOwnerFrame",new XElement("bag","foreign"));Need(!action.LoadedFinal.Current(),"actual native owner change invalidates role");
  Fresh();LoadedCachedFinalTransition.TryInstall(action);action.LoadedFinal.Dispose();action.LoadedFinal.Dispose();Need(action.Issue.Cache.Leases==0&&RebirthProductiveEarnedObservationBudget.Live==0,"source releases only its own leases once");action.LoadedFinal=null;
  Console.WriteLine("PASS "+checks+" actual loaded final cached source assertions; native producer adapters excluded");
 }
}
// ACTUAL_LOADED_CACHED_FINAL_SOURCE
