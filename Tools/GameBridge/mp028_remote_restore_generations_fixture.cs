// AUTHORED_NOT_RUN. Complete actual prefix receiver restoration. Native load,
// original/peer authority, rich validator and allocator are named adapters.
using System;using System.Linq;using System.Xml.Linq;using System.Collections.Generic;
class PlayerDataFile{internal XElement Frame;}
class ClientInfo{internal PlayerDataFile latestPlayerData;}
static class RebirthProductiveEarnedObservationBudget{internal sealed class Lease:IDisposable{public void Dispose(){}}internal static bool TryRetainDetached(long bytes,out IDisposable value){value=new Lease();return true;}}
static class RebirthWorkstationCompletionOriginal{internal static bool ValidKey(string value)=>value?.Length==64;}
static class RebirthProductiveEarnedUiOwnerFile{internal static bool MatchesDeferredOwnerFrame(PlayerDataFile data,XElement frame)=>data!=null&&XNode.DeepEquals(data.Frame,frame);}
static class RebirthWorkstationCompletionPublication{
 internal static readonly Dictionary<string,XElement> Xml=new Dictionary<string,XElement>();internal static readonly Dictionary<string,byte[]> Native=new Dictionary<string,byte[]>();
 internal static XElement ReadPrimary(string path)=>Xml.TryGetValue(path,out var value)?new XElement(value):null;
 internal static byte[] CopyPublishedBytes(string path){if(Native.ContainsKey(path))throw new System.IO.InvalidDataException("Binary native source cannot use XML reader");return Xml.ContainsKey(path)?new byte[]{8}:null;}
 internal static byte[] CopyRetainedNativeBytes(string path)=>Native.TryGetValue(path,out var bytes)?(byte[])bytes.Clone():null;
}
static class RebirthWorkstationCompletionPipeline{
 internal sealed class Load{internal bool IdentityCurrent()=>true;}
 internal sealed class RemoteOutputHostSource{internal int PrefixCount=2;internal Guid Operation,Nonce;internal string Hash="source",InventoryPath="inventory",FinalBeforeGeneration;internal byte[] Inventory={8},DeferredAddBody;internal XElement Request,Before,RootBefore;internal bool FinalCallbackSaved=true;internal Load Native=new Load();internal bool Current()=>true;}
 internal sealed class NativeHostOriginal{internal Guid Operation,Nonce;internal string SourceHash="source",Path="original";internal ClientInfo Sender=new ClientInfo();internal bool CurrentViewer()=>true;internal IDisposable Retain()=>new RebirthProductiveEarnedObservationBudget.Lease();}
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal static bool AllowLoadedAttach,AllowLoadedFinal;internal static int LoadedCommitted,LoadedRolledBack;
 // Explicit native source/qualifier adapters. The complete production restore
 // must commit or roll back their transaction; these do not certify native load.
 internal sealed class LoadedNativeContribution:IDisposable{bool committed;internal bool QualifyFinalPhysical(RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source)=>AllowLoadedFinal;internal void Commit(){committed=true;LoadedCommitted++;}public void Dispose(){if(!committed)LoadedRolledBack++;}}
 internal static bool TryAttachLoadedNativeContribution(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source,out LoadedNativeContribution attachment){attachment=AllowLoadedAttach?new LoadedNativeContribution():null;return attachment!=null;}
 internal static partial class NativeViewerPds{
  class ViewerSource:IDisposable{
   internal RebirthWorkstationCompletionPipeline.NativeHostOriginal Original;internal string Generation,BagLocks,BeltLocks,BeltPreferred,EquipmentPreferred;internal int BeltCount,PrefixCount;
   internal XElement Active,Delivery,DirectEffect;internal IDisposable Memory,Retention;internal SavedViewerFile Before,After,Prefix;internal string BeforeInventoryPath,InventoryPath;internal byte[] BeforeInventoryReceipt,InventoryReceipt;internal bool InventoryAccepted;
   internal Guid SourceOperation=>Original.Operation;internal Guid SourceNonce=>Original.Nonce;internal string SourceWorldGuid=>"world";internal string SourceHash=>Original.SourceHash;internal string SourcePath=>Original.Path;internal ClientInfo SourceSender=>Original.Sender;
   internal bool Current()=>true;internal bool BeforeInventoryCurrent()=>BeforeInventoryReceipt!=null;public void Dispose(){Memory?.Dispose();Retention?.Dispose();}
  }
  class SavedViewerFile{
   internal ViewerSource Source;internal XElement Request,Frame,PrefixBefore;internal string ReceiptPath,NativePath,Path,AcceptedWire,PrefixInventoryPath;internal int AcceptedWireLength;internal byte[] Receipt,Native,PrefixInventoryReceipt;internal bool Accepted,Published;internal WeakReference<PlayerDataFile> AcceptedHead;
   internal bool Current()=>Accepted&&Published&&Native!=null&&Receipt!=null;internal bool PrefixInventoryCurrent()=>PrefixInventoryReceipt!=null;
   internal static SavedViewerFile Issue(ViewerSource source,bool after,XElement request)=>new SavedViewerFile{Source=source,Request=request,Frame=new XElement(request.Element("nativeOwnerFrame"))};
  }
  static readonly object gate=new object();static readonly Dictionary<string,ViewerSource> sources=new Dictionary<string,ViewerSource>();static readonly Dictionary<RebirthWorkstationCompletionPipeline.NativeHostOriginal,SavedViewerFile> initialBefore=new Dictionary<RebirthWorkstationCompletionPipeline.NativeHostOriginal,SavedViewerFile>();
  static string Key(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,string gen)=>original.Operation+gen;
  static string RequestDigest(XElement request){using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(request.ToString(SaveOptions.DisableFormatting))));}
  static string HydrationHash(byte[] value){using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(value));}
  static bool ValidLocks(string value)=>value=="locks";static bool ValidOwnerPreferences(ViewerSource source)=>true;
  static bool ValidateViewerOutputPrefix(ViewerSource source,XElement request,int count)=>true;
  static bool ValidateSavedSameOwner(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,XElement contribution,out object proof){proof=null;var source=initialBefore[original].Source;return sources.TryGetValue(Key(original,source.Generation),out var installed)&&ReferenceEquals(installed,source)&&source.After?.Current()==true&&
   (string)source.Before.Frame.Element("bag")=="active"&&(string)initialBefore[original].Frame.Element("bag")=="initial";}
  static readonly string Initial=new string('1',64),Active=new string('2',64);static int checks;
  static void Need(bool value,string text){if(!value)throw new Exception(text);checks++;}
  static XElement Frame(string name)=>new XElement("nativeOwnerFrame",new XElement("bag",name));
  static XElement Request(string gen,string stage,XElement before,string frame,Guid nonce)=>new XElement("nativeViewer",new XAttribute("generation",gen),new XAttribute("stage",stage),new XAttribute("nonce",nonce.ToString("N")),new XAttribute("source","source"),new XAttribute("bagLocks","locks"),new XAttribute("beltLocks","locks"),new XAttribute("beltCount",10),new XElement("activeBefore",new XElement(before)),Frame(frame));
  static void SaveStage(RebirthWorkstationCompletionPipeline.NativeHostOriginal original,XElement request){
   string path="original.viewer."+(string)request.Attribute("generation")+"."+(string)request.Attribute("stage");var bytes=new byte[]{1,2,3};
   RebirthWorkstationCompletionPublication.Xml[path+".intent"]=new XElement("workstationCompletionIntent",new XElement("before",new XElement(request)));
   RebirthWorkstationCompletionPublication.Xml[path]=new XElement("workstationCompletionIntent",new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("world","world"),new XElement("before",new XElement("nativeViewerSaved",new XAttribute("source","source"),new XAttribute("nonce",original.Nonce.ToString("N")),new XAttribute("generation",(string)request.Attribute("generation")),new XAttribute("digest",RequestDigest(request)),new XAttribute("retainedNative",path+".native"),new XAttribute("nativePath","player.ttp"),new XAttribute("acceptedWire","wire"),new XAttribute("acceptedWireLength",3),new XAttribute("nativeLength",3),new XAttribute("nativeDigest",HydrationHash(bytes)),new XElement(request.Element("nativeOwnerFrame")))));
   RebirthWorkstationCompletionPublication.Native[path+".native"]=bytes;
  }
  static RebirthWorkstationCompletionPipeline.RemoteOutputHostSource Setup(out RebirthWorkstationCompletionPipeline.NativeHostOriginal original){
   sources.Clear();initialBefore.Clear();RebirthWorkstationCompletionPublication.Xml.Clear();RebirthWorkstationCompletionPublication.Native.Clear();
   original=new RebirthWorkstationCompletionPipeline.NativeHostOriginal{Operation=Guid.NewGuid(),Nonce=Guid.NewGuid()};
   var root=new XElement("nativeOwningBefore",new XAttribute("kind","initial"));var active=new XElement("nativeOwningBefore",new XAttribute("kind","active"));
   var first=Request(Initial,"before",root,"initial",original.Nonce);var current=Request(Active,"before",active,"active",original.Nonce);var final=Request(Active,"after",active,"final",original.Nonce);
   final.Add(new XElement("physicalDelivery",new XElement("nativeOwningDelivery")),new XElement("sameOwnerEffect",new XElement("nativeViewerEffect",new XElement("initialViewerBefore",new XElement(first)))));
   SaveStage(original,first);SaveStage(original,current);SaveStage(original,final);original.Sender.latestPlayerData=new PlayerDataFile{Frame=Frame("final")};
   foreach(var request in new[]{first,current})RebirthWorkstationCompletionPublication.Xml["original.viewer."+(string)request.Attribute("generation")+".inventory-before"]=new XElement("workstationCompletionIntent",new XElement("before",new XElement("nativeViewerInventoryBeforeAccepted",new XAttribute("generation",(string)request.Attribute("generation")),new XElement(request.Element("nativeOwnerFrame")))));
   RebirthWorkstationCompletionPublication.Xml["inventory"]=new XElement("workstationCompletionIntent",new XElement("before",new XElement("nativeViewerInventoryAccepted",Frame("active"),new XElement("after",Frame("final")))));
   return new RebirthWorkstationCompletionPipeline.RemoteOutputHostSource{Operation=original.Operation,Nonce=original.Nonce,Request=final,Before=active,RootBefore=root,FinalBeforeGeneration=Initial};
  }
  internal static void Run(){
   var authority=Setup(out var original);Need(RestoreRemoteOutputPrefix(original,authority),"distinct saved initial and active generations restored");var source=sources.Values.Single();
   Need((string)source.Before.Frame.Element("bag")=="active"&&(string)initialBefore[original].Frame.Element("bag")=="initial","active physical plan never aliases initial giveExp Before");
   Need(source.Before.ReceiptPath.Contains(Active)&&initialBefore[original].ReceiptPath.Contains(Initial),"native archive receipts use each exact request generation");
   authority=Setup(out original);RebirthWorkstationCompletionPublication.Xml["original.viewer."+Initial+".before"].Element("before").Element("nativeViewerSaved").SetAttributeValue("generation",Active);Need(!RestoreRemoteOutputPrefix(original,authority),"foreign first Before generation refused");
   authority=Setup(out original);RebirthWorkstationCompletionPublication.Xml.Remove("original.viewer."+Active+".inventory-before");Need(!RestoreRemoteOutputPrefix(original,authority),"missing active native inventory proof refused");
   authority=Setup(out original);RebirthWorkstationCompletionPublication.Xml["inventory"].Element("before").Element("nativeViewerInventoryAccepted").Element("nativeOwnerFrame").Element("bag").Value="initial";Need(!RestoreRemoteOutputPrefix(original,authority),"initial bag cannot replace active physical Before");
   authority=Setup(out original);RebirthWorkstationCompletionPublication.Native.Remove("original.viewer."+Initial+".before.native");Need(!RestoreRemoteOutputPrefix(original,authority),"missing first native archive refused");
   authority=Setup(out original);original.Sender.latestPlayerData.Frame=Frame("foreign");Need(!RestoreRemoteOutputPrefix(original,authority),"different actual native peer owner frame refused");
   authority=DeferredSetup(out original);AllowLoadedAttach=AllowLoadedFinal=true;
   Need(RestoreRemoteOutputPrefix(original,authority)&&sources.Values.Single().DirectEffect==null&&LoadedCommitted==1&&LoadedRolledBack==0,"deferred final commits distinct loaded source without direct XP effect");
   authority=DeferredSetup(out original);AllowLoadedAttach=true;AllowLoadedFinal=false;
   Need(!RestoreRemoteOutputPrefix(original,authority)&&sources.Count==0&&initialBefore.Count==0&&LoadedCommitted==0&&LoadedRolledBack==1,"failed deferred physical qualification removes temporary viewer and rolls back only new attachment");
   authority=DeferredSetup(out original);AllowLoadedAttach=false;AllowLoadedFinal=true;
   Need(!RestoreRemoteOutputPrefix(original,authority)&&sources.Count==0&&initialBefore.Count==0&&LoadedCommitted==0,"missing loaded Add cannot be replaced by final PDS");
   authority=DeferredSetup(out original);AllowLoadedAttach=AllowLoadedFinal=true;
   authority.Request.Add(new XElement("sameOwnerEffect",new XElement("nativeViewerEffect")));SaveStage(original,authority.Request);
   Need(!RestoreRemoteOutputPrefix(original,authority)&&sources.Count==0&&LoadedRolledBack==1,"deferred final rejects mixed direct-effect role");
   Console.WriteLine("PASS "+checks+" actual independent native generation restore assertions");
  }
  static RebirthWorkstationCompletionPipeline.RemoteOutputHostSource DeferredSetup(out RebirthWorkstationCompletionPipeline.NativeHostOriginal original){
   var authority=Setup(out original);AllowLoadedAttach=AllowLoadedFinal=false;LoadedCommitted=LoadedRolledBack=0;
   authority.DeferredAddBody=new byte[]{1};authority.RootBefore=new XElement(authority.Before);authority.FinalBeforeGeneration=Active;
   authority.Request.Element("sameOwnerEffect").Remove();SaveStage(original,authority.Request);return authority;
  }
 }
}
class Program{static void Main()=>RebirthProductiveEarnedUiDeferredTransport.NativeViewerPds.Run();}
// ACTUAL_RESTORE_SOURCE
