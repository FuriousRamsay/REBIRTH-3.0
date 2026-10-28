// AUTHORED_NOT_RUN. Complete production final owner reader/terminal consumer.
// Common native read admission, codecs, host ACK authentication and immutable
// file publisher are explicit adapters; these are not native lifecycle tests.
using System;using System.Linq;using System.Text;using System.Xml.Linq;using System.Collections.Generic;
namespace Newtonsoft.Json.Linq{class JArray{}}
class Recipe{internal string GetName()=>"recipe";}class Row{internal Recipe Recipe=new Recipe();}class EntityPlayerLocal{}
static class RebirthRemoteResourcePlacedStationQueue{internal static bool TryDecode(string value,out Row[] rows){rows=new[]{new Row()};return value=="recipe";}}
static class RebirthProductiveEarnedObservationBudget{internal static bool Admit=true;internal sealed class Lease:IDisposable{public void Dispose(){}}internal static bool TryRetainDetached(long bytes,out IDisposable memory){memory=Admit?new Lease():null;return Admit;}}
static class RebirthWorkstationCompletionPublication{internal static XElement Frame,Prefix;internal static XElement ReadPrimary(string path)=>new XElement(path.EndsWith(".prefix-1")?Prefix:Frame);internal static byte[] CopyPublishedBytes(string path)=>new byte[]{1};}
static class RebirthProductiveEarnedUiOwnerFile{internal static XElement Frame;internal static XElement CaptureDeferredLiveFrame(EntityPlayerLocal actor,string[] recipes)=>recipes.Length==1&&recipes[0]=="recipe"?new XElement(Frame):null;}
static class RebirthWorkstationCompletionPipeline{internal static string RemoteOutputRequestHash(XElement request){var copy=new XElement(request);copy.Attribute("challenge")?.Remove();using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(copy.ToString(SaveOptions.DisableFormatting))));}}
public static partial class RebirthRecipeOutputIntegration{
 private sealed class Hold{internal Guid Operation;}
 private sealed class Scan{internal List<Hold> Holds=new List<Hold>();}
 private sealed class RemoteOutputOwnerLoad{internal EntityPlayerLocal Actor=new EntityPlayerLocal();internal string Owner="owner",Creation="creation";internal int Deaths=1;internal byte[] EquipmentFrame={3};internal Scan Scan=new Scan();internal bool Valid=true;internal bool Current()=>Valid;}
 internal sealed partial class RemoteOutputColdClient{
  private RemoteOutputOwnerLoad loaded;private IDisposable memory;private byte[] originalBytes,acceptedBytes,effectBytes,ownerSavedBytes,requestBytes;
  private string originalPath,acceptedPath,effectPath,ownerSavedPath,requestPath;private XElement original,remote,accepted,effect,request;
  private string deferredRequestPath;private byte[] deferredRequestBytes;private XElement deferredContribution;
  internal Guid Operation,LocalOperation;internal int Prefix;internal string OriginalHash,FramePath;internal bool Attached,QueueTerminalSaved,FinalCallbackSaved,CallbackReturned,ExternalCached;
  private bool BeforeBindingCurrent()=>loaded.Current()&&FinalFilesCurrent();
 }
 private static Dictionary<string,XElement> files=new Dictionary<string,XElement>();private static Dictionary<string,byte[]> writes=new Dictionary<string,byte[]>();private static bool Publish=true,Scrap=true;private static int checks;
 private static bool ReadPersonalOutputSidecar(string path,out byte[] bytes,out XElement image){if(!files.TryGetValue(path,out var found)){bytes=null;image=null;return false;}image=new XElement(found);bytes=UiOriginalBytes(image);return true;}
 private static byte[] UiOriginalBytes(XElement value)=>Encoding.UTF8.GetBytes(value.ToString(SaveOptions.DisableFormatting));
 private static string UiOriginalHash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes));}
 private static byte[] PersonalColdImage(XElement value,string name)=>Convert.FromBase64String(value.Element(name).Value);
 private static bool PersonalColdIdentity(XElement value,string name,string attrs,string children,Guid operation,string hash)=>value.Name==name&&(string)value.Attribute("operation")==operation.ToString("N")&&(string)value.Attribute("original")==hash;
 private static bool TryReadScrapCompletedEquipment(string path,Guid operation,string hash,Recipe recipe,int count,byte[] before,out byte[] after){after=new byte[]{3};return Scrap;}
 private static bool PublishPersonalColdBinding(string path,byte[] value){if(!Publish)return false;if(writes.TryGetValue(path,out var old))return old.SequenceEqual(value);writes[path]=value;return true;}
 private static XElement Grid(string name,byte value)=>new XElement(name,Convert.ToBase64String(new[]{value}));
 private static void Need(bool value,string name){if(!value)throw new Exception(name);checks++;}
 internal sealed partial class RemoteOutputColdClient{
  private static RemoteOutputColdClient Make(string effectRecipe="recipe",bool externalCached=false,bool wrongAction=false){
   files.Clear();writes.Clear();Publish=Scrap=RebirthProductiveEarnedObservationBudget.Admit=true;
   var local=Guid.NewGuid();var native=Guid.NewGuid();var image=new XElement("recipeOutputOriginal",new XElement("recipe","recipe"));var bytes=UiOriginalBytes(image);string hash=UiOriginalHash(bytes);
   var remote=new XElement("remoteOutputSource",new XAttribute("nonce","nonce"),new XAttribute("source","source"),new XAttribute("intentPath","frame"),new XElement("activeBefore",new XElement("nativeOwningBefore")));
   var accepted=new XElement("recipeOutputAccepted",Grid("bag",1),Grid("belt",2),Grid("equipment",3));var acceptedBytes=UiOriginalBytes(accepted);
   var returned=new XElement("recipeOutputNativeContinuationReturned",new XAttribute("operation",local.ToString("N")),new XAttribute("original",hash),new XAttribute("outputPrefix",1));files["original.native-continuation-returned"]=returned;
   files["original.native-continuation-entered"]=new XElement("recipeOutputNativeContinuationEntered",new XAttribute("operation",local.ToString("N")),new XAttribute("original",hash),new XAttribute("outputPrefix",1));
   var ownerFrame=new XElement("nativeOwnerFrame",Grid("bag",1),Grid("belt",2),Grid("equipment",3));RebirthProductiveEarnedUiOwnerFile.Frame=ownerFrame;
   var request=new XElement("nativeViewer",new XAttribute("stage","after"),new XAttribute("nonce","nonce"),new XAttribute("source","source"),new XAttribute("generation","generation"),new XAttribute("pdsDigest","native"),new XAttribute("pdsLength",100),new XElement(remote.Element("activeBefore")),ownerFrame,new XElement("sameOwnerEffect",new XElement("nativeViewerEffect",new XAttribute("recipe","recipe"))));
   if(effectRecipe==null)request.Element("sameOwnerEffect").Remove();else request.Element("sameOwnerEffect").Elements().Single().SetAttributeValue("recipe",effectRecipe);
   if(externalCached){remote.SetAttributeValue("cached",true);request.Name="cachedViewer";request.SetAttributeValue("action",wrongAction?"foreign":"nonce");}
   request.Add(new XElement("nativeOutputOriginal",new XElement("nativeOutputPrefix",new XAttribute("operation",local.ToString("N")),new XAttribute("original",hash),new XAttribute("count",1),new XAttribute("accepted",UiOriginalHash(acceptedBytes)))));
   var prefixRequest=new XElement(request);prefixRequest.SetAttributeValue("stage","prefix-1");prefixRequest.Element("nativeOutputOriginal").Remove();
   prefixRequest.Add(new XElement(request.Element("nativeOutputOriginal").Element("nativeOutputPrefix")),new XElement("deferredNativeContribution"));
   RebirthWorkstationCompletionPublication.Prefix=new XElement("workstationCompletionIntent",new XElement("before",prefixRequest));
   string digest=RebirthWorkstationCompletionPipeline.RemoteOutputRequestHash(request);var ack=new XElement("ack",new XAttribute("stage",externalCached?"cachedViewerAfterSaved":"nativeViewerAfterSaved"),new XAttribute("digest",digest),new XAttribute("nativeNonce","nonce"),new XAttribute("nativeSource","source"));
   var ownerSaved=new XElement("recipeOutputRemoteCallbackOwnerSaved",new XAttribute("version",1),new XAttribute("operation",local.ToString("N")),new XAttribute("original",hash),new XAttribute("returned",UiOriginalHash(UiOriginalBytes(returned))),new XAttribute("accepted",UiOriginalHash(acceptedBytes)),new XAttribute("outputPrefix",1),new XAttribute("nativeOperation",native.ToString("N")),new XAttribute("nonce","nonce"),new XAttribute("source","source"),new XAttribute("generation","generation"),new XAttribute("digest",digest),new XAttribute("nativeDigest","native"),new XAttribute("nativeLength",100),new XElement("request",request),new XElement("acknowledgment",ack));files["original.native-callback-owner-saved"]=ownerSaved;
   RebirthWorkstationCompletionPublication.Frame=new XElement("workstationCompletionIntent",new XElement("before",new XElement(request)));
   return ReadFinal(new RemoteOutputOwnerLoad(),"original",bytes,image,remote,local,native,hash,1,1,acceptedBytes,accepted,new byte[]{1},new XElement("effect"));
  }
  private XElement Terminal()=>new XElement("ack",new XAttribute("queueTerminal",true),new XAttribute("finalCallback",true),new XAttribute("nativeNonce","nonce"),new XAttribute("nativeSource","source"),new XAttribute("localOperation",LocalOperation.ToString("N")),new XAttribute("original",OriginalHash),new XAttribute("count",1),new XAttribute("prefixDigest",RebirthWorkstationCompletionPipeline.RemoteOutputRequestHash(request)),new XAttribute("challenge","first"));
  internal static void Run(){
   var identity=new XElement("remoteOutputSource",new XAttribute("nonce","action"),new XAttribute("source","source"));
   var cachedPrefix=new XElement("cachedViewer",new XAttribute("action","action"),new XAttribute("source","source"));
   Need(MatchesRemoteColdRequestSource(identity,cachedPrefix,true),"actual cached prefix uses action without nonce");
   cachedPrefix.SetAttributeValue("nonce","action");Need(MatchesRemoteColdRequestSource(identity,cachedPrefix,true),"actual cached final carries matching action and nonce");
   cachedPrefix.SetAttributeValue("nonce","foreign");Need(!MatchesRemoteColdRequestSource(identity,cachedPrefix,true),"conflicting cached final nonce refused");
   cachedPrefix.Attribute("nonce").Remove();cachedPrefix.Name="nativeViewer";Need(!MatchesRemoteColdRequestSource(identity,cachedPrefix,true),"cached source cannot change request role");
   var nativeRequest=new XElement("nativeViewer",new XAttribute("nonce","action"),new XAttribute("source","source"));Need(MatchesRemoteColdRequestSource(identity,nativeRequest,false),"native Add prefix keeps nonce identity");
   nativeRequest.SetAttributeValue("action","action");Need(!MatchesRemoteColdRequestSource(identity,nativeRequest,false),"cached action cannot masquerade as native Add request");
   cachedPrefix.Name="cachedViewer";cachedPrefix.SetAttributeValue("source","foreign");Need(!MatchesRemoteColdRequestSource(identity,cachedPrefix,true),"foreign cached source refused");
   Need(Make(null,true)?.ExternalCached==true,"actual saved cached final owner remains a distinct external action candidate");
   Need(Make(null,true,true)==null,"foreign cached action cannot qualify final owner source");
   Need(Make(null)?.FinalCallbackSaved==true,"deferred final frame uses immutable native recipe counters without direct XP source");
   Need(Make("foreign")==null,"direct effect cannot substitute different native recipe counters");
   var source=Make();Need(source!=null&&source.FinalCallbackSaved&&source.CallbackReturned,"positive final owner source");Need(source.FinalFilesCurrent(),"exact callback proof reread");
   source.loaded.Scan.Holds.Add(new Hold{Operation=source.LocalOperation});Need(source.AcceptQueueTerminal(source.Terminal())&&source.QueueTerminalSaved&&source.loaded.Scan.Holds.Count==0,"actual terminal ack releases only original hold");
   Need(!Encoding.UTF8.GetString(writes.Values.Single()).Contains("challenge="),"transport challenge excluded from immutable receipt");
   source=Make();var ack=source.Terminal();ack.SetAttributeValue("nativeNonce","other");Need(!source.AcceptQueueTerminal(ack)&&writes.Count==0,"foreign nonce refused");
   source=Make();ack=source.Terminal();ack.SetAttributeValue("queueTerminal",false);Need(!source.AcceptQueueTerminal(ack),"nonterminal ack cannot close consumed row");
   source=Make();source.loaded.Valid=false;Need(!source.AcceptQueueTerminal(source.Terminal()),"stale actual native owner source refused");
   source=Make();source.Attached=true;Need(!source.AcceptQueueTerminal(source.Terminal()),"live card original cannot metadata retire");
   source=Make();Publish=false;Need(!source.AcceptQueueTerminal(source.Terminal())&&!source.QueueTerminalSaved,"failed terminal publication retains original");
   source=Make();files["original.native-continuation-returned"].SetAttributeValue("outputPrefix",0);Need(!source.FinalFilesCurrent(),"changed returned callback refused");
   source=Make();Scrap=false;Need(!source.FinalFilesCurrent(),"unproved cosmetic delta refused");
   source=Make();files["original.native-callback-owner-saved"].SetAttributeValue("returned","foreign");Need(!source.FinalFilesCurrent(),"changed saved callback binding refused");
   Console.WriteLine("PASS "+checks+" actual final native owner reader/terminal assertions");
  }
 }
 public static void Main()=>RemoteOutputColdClient.Run();
}
// ACTUAL_FINAL_SOURCE
