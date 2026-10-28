// AUTHORED_NOT_RUN. Complete production identity factory; native load and witness currentness are adapters.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;
static class RebirthWorkstationCompletionPipeline{
 internal class Native{internal bool Live=true;internal object Tile=new object();internal bool Current()=>Live;}
 internal class RemoteOutputHostSource{internal bool Live=true;internal object World=new object();internal string Save;internal Guid Birth=Guid.NewGuid();internal byte[] DeferredAddBody={1};internal Native Native=new Native();internal XElement DeferredMembership,Request=new XElement("request");internal bool Current()=>Live;}
}
// DTO adapter for the same remote native source supplied by the scenario.
// It delegates source currentness; no authority-local source is manufactured.
static class RebirthProductiveEarnedUiDeferredTransport{
 internal sealed class LoadedMemberSource{
  internal readonly RebirthWorkstationCompletionPipeline.RemoteOutputHostSource Remote;
  internal object Local=>null;
  internal LoadedMemberSource(RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source){Remote=source;}
  internal object World=>Remote.World;internal string Save=>Remote.Save;internal Guid Birth=>Remote.Birth;
  internal byte[] DeferredAddBody=>Remote.DeferredAddBody;
  internal XElement DeferredMembership=>Remote.DeferredMembership;internal XElement Request=>Remote.Request;
  internal RebirthWorkstationCompletionPipeline.Native Native=>Remote.Native;
  internal bool Current()=>Remote.Current();
 }
}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int max)=>XElement.Parse(text);}
static class RebirthOriginalActionScope{internal static bool Validate(string owner,string creation)=>owner=="owner"&&creation=="creation";}
static class RebirthProductiveEarnedArtifact{internal static string WorldIdentity(object world)=>"world";internal static bool SafePath(string path)=>true;internal static bool FileMatches(string path,byte[] image)=>File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(image);}
static class RebirthProductiveEarnedRowProof{internal const int MaximumArtifactBytes=1048576;}
static class RebirthStationCompletionOriginalScope{internal static StringComparison PathComparison=>StringComparison.OrdinalIgnoreCase;}
static class RebirthProductiveEarnedObservationBudget{
 internal static bool Available=true;internal static int Held;
 class Lease:IDisposable{bool closed;public void Dispose(){if(!closed){closed=true;Held--;}}}
 internal static bool TryRetainDetached(long bytes,out IDisposable memory){memory=null;if(!Available)return false;memory=new Lease();Held++;return true;}
}
internal static partial class RebirthProductiveEarnedUiQueueOwner{
 internal class Witness:IDisposable{
  internal object World,Station,AuthorityLocalSource;internal Guid Operation,Birth;internal string OriginalOwnerKey,Creation,RecipeBinding,Save,WorldKey,Path,ClientPurpose;internal int StartingEntityId,Deaths,References;internal byte[] Image;internal bool Published,RowRetired;internal IDisposable Retention;internal RebirthWorkstationCompletionPipeline.RemoteOutputHostSource FinalOutputSource;
  internal bool SourceCurrent()=>FinalOutputSource.Live&&FinalOutputSource.Native.Live&&RebirthProductiveEarnedArtifact.FileMatches(Path,Image);public void Dispose(){Retention?.Dispose();Retention=null;}
 }
 static string ProofHash(byte[] bytes){using(var hash=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(bytes));}
 static int checks;static RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source;static XElement purpose;static string file;static string root;
 static void Need(bool value){if(!value)throw new Exception("loaded aggregate purpose "+checks);checks++;}
 static void Seed(){
  source=new RebirthWorkstationCompletionPipeline.RemoteOutputHostSource{Save=root};var operation=Guid.NewGuid();
  var original=new XElement("nativeQueueOwnerOriginal",new XAttribute("operation",operation.ToString("N")),new XAttribute("birth",source.Birth.ToString("N")),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XAttribute("entity",7),new XAttribute("deaths",2),new XAttribute("world","world"),new XAttribute("save",root),new XElement("recipeBinding","definition"),new XElement("beforeNative","before"),new XElement("acceptedNative","accepted"));
  var image=new UTF8Encoding(false,true).GetBytes(original.ToString(SaveOptions.DisableFormatting));file=Path.Combine(root,"RebirthProductiveEarnedUi","queueOwners","owner",operation.ToString("N")+".original");Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllBytes(file,image);
  purpose=new XElement("queueOwnerPurpose",new XAttribute("version",1),new XAttribute("operation",operation.ToString("N")),new XAttribute("birth",source.Birth.ToString("N")),new XAttribute("owner","owner"),new XAttribute("creation","creation"),new XAttribute("entity",7),new XAttribute("deaths",2),new XAttribute("world","world"),new XAttribute("recipe","recipe"),new XAttribute("definition",ProofHash(Encoding.UTF8.GetBytes("definition"))),new XAttribute("original",ProofHash(image)));Bind();
 }
 static string Text=>purpose.ToString(SaveOptions.DisableFormatting);
 static void Bind()=>source.DeferredMembership=new XElement("deferredNativeContribution",new XAttribute("recipient",Text),new XElement("members",new XElement("member",new XAttribute("source",Text))));
 static bool Resolve(out Witness witness)=>TryResolveLoadedAggregatePurpose(source,Text,out witness);
 public static void Main(){root=Path.Combine(Path.GetTempPath(),"rebirth-loaded-purpose-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
  Seed();Need(Resolve(out var witness));Need(witness.RowRetired&&witness.Published&&witness.References==1&&witness.SourceCurrent());witness.Dispose();Need(RebirthProductiveEarnedObservationBudget.Held==0);
  Seed();source.Live=false;Need(!Resolve(out _));
  Seed();source.Native.Live=false;Need(!Resolve(out _));
  Seed();source.DeferredAddBody=null;Need(!Resolve(out _));
  Seed();source.DeferredMembership=new XElement("different");Need(!Resolve(out _));
  Seed();File.WriteAllText(file+".tmp","unresolved");Need(!Resolve(out _));
  Seed();File.WriteAllText(file,"changed original");Need(!Resolve(out _));
  Seed();purpose.SetAttributeValue("world","other");Bind();Need(!Resolve(out _));
  Seed();purpose.SetAttributeValue("birth",Guid.NewGuid().ToString("N"));Bind();Need(!Resolve(out _));
  Seed();purpose.SetAttributeValue("definition","wrong");Bind();Need(!Resolve(out _));
  Seed();purpose.SetAttributeValue("entity",8);Bind();Need(!Resolve(out _));
  Seed();purpose.SetAttributeValue("deaths",-1);Bind();Need(!Resolve(out _));
  Seed();RebirthProductiveEarnedObservationBudget.Available=false;Need(!Resolve(out _));RebirthProductiveEarnedObservationBudget.Available=true;Need(RebirthProductiveEarnedObservationBudget.Held==0);
  Console.WriteLine("PASS "+checks+" loaded aggregate owner identity assertions; native producers excluded");
 }finally{if(Path.GetDirectoryName(root)!=Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)||!Path.GetFileName(root).StartsWith("rebirth-loaded-purpose-"))throw new InvalidDataException();Directory.Delete(root,true);}}
}
// ACTUAL_LOADED_AGGREGATE_PURPOSE
