// AUTHORED_NOT_RUN. Complete production cached prefix restorer and saved-stage
// reader. Native issue/ready/active-body/full insertion validators are adapters.
using System;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;using System.Text;
class PlayerDataFile{internal XElement Frame;}
class ClientInfo{internal PlayerDataFile latestPlayerData;}
class Recipe{internal int count=1,itemValueType=1;}
class ItemValue{internal int type=1;}
class ItemStack{internal int count=1;internal ItemValue itemValue=new ItemValue();}
static class RebirthProductiveEarnedUiRemote{
 internal static bool Extras=true;
 internal static ItemStack[] ReadGrid(XElement delivery,string name)=>new[]{new ItemStack()};
 internal static bool ValidateExtraOutputs(Recipe recipe,XElement delivery,ItemStack[] extras)=>Extras;
}
static class RebirthProductiveEarnedObservationBudget{
 internal static bool Admit=true;internal static int Live;
 internal sealed class Lease:IDisposable{bool closed;internal Lease(){Live++;}public void Dispose(){if(!closed){closed=true;Live--;}}}
 internal static bool TryRetainDetached(long size,out IDisposable memory){memory=Admit?new Lease():null;return Admit;}
}
static class RebirthWorkstationCompletionOriginal{internal static bool ValidKey(string value)=>value?.Length==64;}
static class RebirthProductiveEarnedUiOwnerFile{internal static bool MatchesDeferredOwnerFrame(PlayerDataFile data,XElement frame)=>data!=null&&XNode.DeepEquals(data.Frame,frame);}
static class RebirthWorkstationCompletionPublication{
 internal static Dictionary<string,XElement> Xml=new Dictionary<string,XElement>();internal static Dictionary<string,byte[]> Binary=new Dictionary<string,byte[]>();
 internal static XElement ReadPrimary(string path){if(Binary.ContainsKey(path))throw new System.IO.InvalidDataException("XML cannot read native bytes");return Xml.TryGetValue(path,out var value)?new XElement(value):null;}
 internal static byte[] CopyPublishedBytes(string path)=>ReadPrimary(path) is XElement value?Encoding.UTF8.GetBytes(value.ToString(SaveOptions.DisableFormatting)):null;
 internal static long PublishedLength(string path)=>Binary.TryGetValue(path,out var bytes)?bytes.LongLength:CopyPublishedBytes(path)?.LongLength??-1;
 internal static byte[] CopyRetainedNativeBytes(string path)=>Binary.TryGetValue(path,out var bytes)?(byte[])bytes.Clone():null;
 internal static bool MatchesPublishedBytes(string path,byte[] bytes){var actual=Binary.TryGetValue(path,out var raw)?raw:CopyPublishedBytes(path);return actual!=null&&bytes!=null&&actual.SequenceEqual(bytes);}
}
internal static class RebirthWorkstationCompletionPipeline{
 internal sealed class Cache{internal byte[] Native={1};internal XElement CurrentFull=new XElement("nativeOwningFullBoundary",new XElement("primaryNative","native-primary"));}
 internal sealed class Issue{internal Guid RetainedAction;internal Cache Cache=new Cache();}
 internal sealed class CachedPhysicalHostAction{
  internal Guid Operation=Guid.NewGuid(),Action=Guid.NewGuid();internal string Path="original",WorldGuid="world",SourceHash="source";
  internal Issue Issue;internal ClientInfo Sender=new ClientInfo();internal bool Native=true,Ready=true,SavedOwnerBeforeAction,PhysicalAccepted;internal int Leases;internal Recipe Recipe=new Recipe();
  internal CachedPhysicalHostAction(){Issue=new Issue{RetainedAction=Action};}
  internal bool Current()=>Native;
  internal IDisposable Retain()=>new Hold(this);
  sealed class Hold:IDisposable{CachedPhysicalHostAction action;internal Hold(CachedPhysicalHostAction value){action=value;value.Leases++;}public void Dispose(){if(action!=null){action.Leases--;action=null;}}}
  internal bool RestoreSavedBefore(RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.SavedViewerFile proof){SavedOwnerBeforeAction=Ready&&proof.Current()&&proof.Source.BeforeInventoryCurrent();return SavedOwnerBeforeAction;}
 }
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal static partial class NativeViewerPds{
  internal sealed partial class ViewerSource:IDisposable{
   internal RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction Cached;internal WeakReference<byte[]> CachedNativeHead;
   internal string Generation,BagLocks,BeltLocks,BeltPreferred,EquipmentPreferred,BeforeInventoryPath,InventoryPath;internal int BeltCount,PrefixCount,Workers;
   internal XElement Active,Delivery;internal IDisposable Memory,Retention;internal SavedViewerFile Before,Prefix,After;internal byte[] BeforeInventoryReceipt,InventoryReceipt;internal bool InventoryAccepted;
   internal Guid SourceOperation=>Cached.Operation;internal Guid SourceNonce=>Cached.Action;internal string SourcePath=>Cached.Path;internal string SourceHash=>Cached.SourceHash;internal string SourceWorldGuid=>Cached.WorldGuid;internal ClientInfo SourceSender=>Cached.Sender;
   internal bool Current()=>Memory!=null&&Retention!=null&&Cached.Current()&&RestoredCachedInputsCurrent();
   internal bool BeforeInventoryCurrent()=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(BeforeInventoryPath,BeforeInventoryReceipt);
   internal bool InventoryCurrent()=>InventoryAccepted&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(InventoryPath,InventoryReceipt);
   public void Dispose(){Memory?.Dispose();Retention?.Dispose();Memory=Retention=null;RestoredCachedInputs=null;}
  }
  internal sealed class SavedViewerFile{
   internal ViewerSource Source;internal XElement Frame,Request,PrefixBefore;internal string ReceiptPath,NativePath,Path,AcceptedWire,PrefixInventoryPath,Digest;internal int AcceptedWireLength;internal bool Accepted,Published,After;internal byte[] Native,Receipt,PrefixInventoryReceipt;internal WeakReference<PlayerDataFile> AcceptedHead;
   internal static SavedViewerFile Issue(ViewerSource source,bool after,XElement request)=>new SavedViewerFile{Source=source,After=after,Request=new XElement(request),Digest=RequestDigest(request),Frame=new XElement(request.Element("nativeOwnerFrame"))};
   internal bool Current()=>Source.Current()&&Accepted&&Published&&Native!=null&&Receipt!=null&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(ReceiptPath,Receipt)&&RebirthWorkstationCompletionPublication.MatchesPublishedBytes(NativePath,Native);
   internal bool PrefixInventoryCurrent()=>RebirthWorkstationCompletionPublication.MatchesPublishedBytes(PrefixInventoryPath,PrefixInventoryReceipt);
  }
  static object gate=new object();static Dictionary<string,ViewerSource> sources=new Dictionary<string,ViewerSource>();static bool Plan=true;
  static string CachedKey(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,string generation)=>action.Operation+":"+generation;
  static string RequestDigest(XElement request){var copy=new XElement(request);copy.Attribute("challenge")?.Remove();return Hash(Encoding.UTF8.GetBytes(copy.ToString(SaveOptions.DisableFormatting)));}
  static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}
  static string HydrationHash(byte[] bytes)=>Hash(bytes);
  static bool ValidLocks(string value)=>value=="locks";static bool ValidOwnerPreferences(ViewerSource source)=>true;
  static bool ValidateCachedActiveBefore(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement active)=>action.Native&&active.Name=="nativeOwningBefore";
  static bool ValidateViewerOutputPrefix(ViewerSource source,XElement request,int count)=>Plan&&source.Cached.SavedOwnerBeforeAction;
  static bool ValidateCachedPhysicalActor(RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action,XElement active,XElement delivery,ItemStack primary,ItemStack[] extras,bool inventory)=>Plan&&sources.TryGetValue(CachedKey(action,generation),out var source)&&ReferenceEquals(source.Cached,action)&&source.After?.Current()==true&&source.InventoryCurrent();
  static string generation=new string('a',64),root,digest,original="owner-original";static Guid local;static RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction action;static int checks;
  static XElement Frame(string bag)=>new XElement("nativeOwnerFrame",new XElement("bag",bag));
  static XElement Envelope(XElement body)=>new XElement("workstationCompletionIntent",new XAttribute("operation",action.Operation.ToString("N")),new XAttribute("world","world"),new XElement("before",body));
  static void Save(XElement request){
   string path=root+"."+(string)request.Attribute("stage");var native=new byte[]{0,1,2};
   RebirthWorkstationCompletionPublication.Xml[path+".intent"]=Envelope(new XElement(request));
   RebirthWorkstationCompletionPublication.Xml[path]=Envelope(new XElement("nativeViewerSaved",new XAttribute("source","source"),new XAttribute("nonce",action.Action.ToString("N")),new XAttribute("generation",generation),new XAttribute("digest",RequestDigest(request)),
    new XAttribute("retainedNative",path+".native"),new XAttribute("nativePath","player.ttp"),new XAttribute("nativeLength",native.Length),new XAttribute("nativeDigest",HydrationHash(native)),new XAttribute("acceptedWire","wire"),new XAttribute("acceptedWireLength",3),new XElement(request.Element("nativeOwnerFrame"))));
   RebirthWorkstationCompletionPublication.Binary[path+".native"]=native;
  }
  static void Fresh(){
   foreach(var value in sources.Values)value.Dispose();sources.Clear();
   if(RebirthProductiveEarnedObservationBudget.Live!=0)throw new Exception("Unreleased restore memory");
   Plan=RebirthProductiveEarnedUiRemote.Extras=true;RebirthProductiveEarnedObservationBudget.Admit=true;RebirthWorkstationCompletionPublication.Xml.Clear();RebirthWorkstationCompletionPublication.Binary.Clear();
   action=new RebirthWorkstationCompletionPipeline.CachedPhysicalHostAction();local=Guid.NewGuid();root="original.viewer."+generation;
   var active=new XElement("nativeOwningBefore");
   var before=new XElement("cachedViewer",new XAttribute("stage","before"),new XAttribute("action",action.Action.ToString("N")),new XAttribute("source","source"),new XAttribute("generation",generation),new XAttribute("bagLocks","locks"),new XAttribute("beltLocks","locks"),new XAttribute("beltCount",10),new XElement("activeBefore",active),Frame("before"));
   var prefix=new XElement(before);prefix.SetAttributeValue("stage","prefix-1");prefix.Element("nativeOwnerFrame").ReplaceWith(Frame("accepted"));prefix.Add(new XElement("nativeOutputPrefix",new XAttribute("operation",local.ToString("N")),new XAttribute("original",original),new XAttribute("count",1)));
   Save(before);Save(prefix);digest=RequestDigest(prefix);action.Sender.latestPlayerData=new PlayerDataFile{Frame=Frame("accepted")};
   RebirthWorkstationCompletionPublication.Xml[root+".inventory-before"]=Envelope(new XElement("nativeViewerInventoryBeforeAccepted",new XAttribute("generation",generation),Frame("before")));
   RebirthWorkstationCompletionPublication.Xml[root+".prefix-1.inventory"]=Envelope(new XElement("nativeViewerPrefixInventoryAccepted",new XAttribute("generation",generation),new XAttribute("count",1),new XAttribute("request",digest),new XElement("predecessor",Frame("before")),new XElement("accepted",Frame("accepted"))));
  }
  static bool Restore()=>RestoreRetainedCachedPrefix(action,generation,1,digest,local,original);
  static void Need(bool value,string name){if(!value)throw new Exception(name);checks++;}
  internal static void Run(){
   Fresh();Need(Restore(),"complete actual cached restorer");var source=sources.Values.Single();Need(source.Current()&&source.PrefixCount==1&&action.SavedOwnerBeforeAction,"saved prefix and Ready installed");
   Need(Restore()&&action.Leases==1&&RebirthProductiveEarnedObservationBudget.Live==1,"lost response retries without replacement");
   Need(!RestoreRetainedCachedPrefix(action,generation,1,digest,Guid.NewGuid(),original),"duplicate query cannot change local original");
   RebirthWorkstationCompletionPublication.Xml[root+".before.intent"].SetAttributeValue("changed",true);Need(!source.Current(),"pinned intent change refuses");
   Fresh();Need(RebirthProductiveEarnedObservationBudget.Live==0,"retained graph cleanup");
   action.Native=false;Need(!Restore(),"native identity refusal");
   Fresh();RebirthWorkstationCompletionPublication.Xml.Remove(root+".inventory-before");Need(!Restore()&&sources.Count==0&&action.Leases==0,"missing Before inventory rolls back");
   Fresh();action.Sender.latestPlayerData.Frame=Frame("foreign");Need(!Restore(),"actual current owner prefix mismatch");
   Fresh();action.Ready=false;Need(!Restore(),"missing historical Ready proof");
   Fresh();Plan=false;Need(!Restore(),"full insertion validator refusal");
   Fresh();RebirthWorkstationCompletionPublication.Xml[root+".prefix-1.inventory"].Element("before").Elements().Single().SetAttributeValue("request","foreign");Need(!Restore(),"inventory digest mismatch");
   Fresh();RebirthWorkstationCompletionPublication.Xml[root+".before.intent"].SetAttributeValue("world","foreign");Need(!Restore(),"foreign intent world");
   Fresh();RebirthWorkstationCompletionPublication.Binary.Remove(root+".before.native");Need(!Restore(),"missing native Before archive");
   Fresh();RebirthWorkstationCompletionPublication.Binary[root+".prefix-1.native"][0]=9;Need(!Restore(),"changed binary prefix digest");
   Fresh();RebirthProductiveEarnedObservationBudget.Admit=false;Need(!Restore(),"allocation refusal");
   Fresh();Need(!RestoreRetainedCachedPrefix(action,generation,0,digest,local,original),"invalid prefix count");
   Fresh();Need(!RestoreRetainedCachedPrefix(action,generation,1,"foreign",local,original),"foreign request digest");
   Fresh();Need(!RestoreRetainedCachedPrefix(action,"bad-generation",1,digest,local,original),"invalid generation");
   Fresh();RebirthWorkstationCompletionPublication.Xml[root+".inventory-before"].Element("before").Elements().Single().Element("nativeOwnerFrame").ReplaceWith(Frame("foreign"));Need(!Restore(),"foreign Before inventory frame");
   Need(sources.Count==0&&action.Leases==0&&RebirthProductiveEarnedObservationBudget.Live==0,"all refusal paths release temporary graph");
   Console.WriteLine("PASS "+checks+" actual cached host prefix restoration assertions; native adapters excluded");
  }
  static void FinalSetup(){
   Fresh();var after=new XElement(RebirthWorkstationCompletionPublication.Xml[root+".prefix-1.intent"].Element("before").Element("cachedViewer"));
   after.SetAttributeValue("stage","after");after.Element("nativeOutputPrefix").Remove();after.Element("nativeOwnerFrame").ReplaceWith(Frame("final"));
   after.Add(new XElement("physicalDelivery",new XElement("nativeOwningDelivery",new XAttribute("sink","Inventory"),new XElement("primaryNative","native-primary"),new XElement("extrasNative","native-extra"))),new XElement("nativeOutputOriginal",new XElement("nativeOutputPrefix",new XAttribute("operation",local.ToString("N")),new XAttribute("original",original),new XAttribute("count",2))));
   Save(after);digest=RequestDigest(after);action.Sender.latestPlayerData.Frame=Frame("final");
   RebirthWorkstationCompletionPublication.Xml[root+".inventory"]=Envelope(new XElement("nativeViewerInventoryAccepted",new XAttribute("generation",generation),Frame("before"),new XElement("after",Frame("final"))));
   RebirthWorkstationCompletionPublication.Xml[action.Path+".ready."+generation]=Envelope(new XElement("cachedPhysicalActionReady"));
  }
  static bool FinalRestore()=>RestoreRetainedCachedFinalOwner(action,generation,2,digest,local,original);
  internal static void RunFinal(){
   FinalSetup();Need(FinalRestore()&&sources.Values.Single().After.Current()&&!action.PhysicalAccepted,"final native owner restored without native physical acceptance");
   Need(FinalRestore()&&action.Leases==1&&RebirthProductiveEarnedObservationBudget.Live==1,"same final source retry does not replace graph");
   Need(!RestoreRetainedCachedFinalOwner(action,generation,1,digest,local,original),"different final count cannot reuse source");
   FinalSetup();RebirthWorkstationCompletionPublication.Xml.Remove(root+".after");Need(!FinalRestore(),"missing final PDS receipt refuses");
   FinalSetup();action.Sender.latestPlayerData.Frame=Frame("foreign");Need(!FinalRestore(),"different actual final owner projection refuses");
   FinalSetup();RebirthWorkstationCompletionPublication.Xml.Remove(root+".inventory");Need(!FinalRestore(),"missing final inventory observation refuses");
   FinalSetup();RebirthWorkstationCompletionPublication.Xml[root+".inventory"].Element("before").Elements().Single().Element("nativeOwnerFrame").ReplaceWith(Frame("foreign"));Need(!FinalRestore(),"different original inventory Before refuses");
   FinalSetup();Plan=false;Need(!FinalRestore()&&sources.Count==0&&action.Leases==0&&!action.PhysicalAccepted,"full plan refusal rolls back temporary lookup source");
   FinalSetup();action.Issue.Cache.CurrentFull.Element("primaryNative").Value="foreign";Need(!FinalRestore(),"different original cached primary refuses");
   FinalSetup();RebirthProductiveEarnedUiRemote.Extras=false;Need(!FinalRestore(),"native extra output definition must match");
   FinalSetup();RebirthWorkstationCompletionPublication.Xml[root+".after.intent"].Element("before").Element("cachedViewer").SetAttributeValue("source","foreign");Need(!FinalRestore(),"different final cached source refuses");
   Need(sources.Count==0&&action.Leases==0&&RebirthProductiveEarnedObservationBudget.Live==0,"all final refusal graphs released");
   Console.WriteLine("PASS "+checks+" actual cached final owner restore assertions; native adapters excluded");
  }
  // ACTUAL_SAVED_STAGE_READER
 }
}
class Program{static void Main()=>RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Run();}
// ACTUAL_CACHED_HOST_RESTORE
