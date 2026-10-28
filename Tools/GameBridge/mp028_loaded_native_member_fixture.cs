// AUTHORED_NOT_RUN. Complete production member source/terminal; native codecs,
// actual load, full transition validators and publication backend are adapters.
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;
class Recipe{internal int count=1;}
class Row{internal Recipe Recipe=new Recipe();internal int StartingEntityId=7,Multiplier=2;}
class Body{internal Row[] Queue={new Row()};internal object[] Completions=new object[0];}
class Argument{internal object CraftedItemStack=new object();}
struct Position{internal int x,y,z;}
static partial class RebirthWorkstationCompletionPipeline{
 internal class Native{internal bool Live=true,Identity=true;internal bool Current()=>Live;internal bool IdentityCurrent()=>Identity;}
 internal class RemoteOutputHostSource{internal bool Live=true;internal object World=new object();internal string Save,Path="root/current.xml";internal Guid Birth=Guid.NewGuid();internal Position Position=new Position{x=1,y=2,z=3};internal XElement DeferredMembership,Request=new XElement("request");internal Native Native=new Native();internal bool Current()=>Live;}
 internal static string CachedFileHash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Convert.ToBase64String(bytes)))).Replace("-","").ToLowerInvariant();}
}
static class RebirthProductiveEarnedUiQueueOwner{internal class Witness{internal bool Live=true;internal Guid Operation=Guid.NewGuid(),Birth;internal int StartingEntityId=7,Deaths=2;internal string RecipeBinding="definition",OriginalOwnerKey="owner",Creation="creation",Purpose="purpose";internal bool SourceCurrent()=>Live;internal IDisposable RetainPublishedSource()=>Live?new Lease():null;class Lease:IDisposable{public void Dispose(){}}}internal static string Export(Witness witness)=>witness.Purpose;}
static class RebirthWorkstationCompletionOutbox{internal static string CurrentGuid(object world)=>"world";internal static string Root(string role)=>System.IO.Path.Combine("completion",role);}
static class RebirthWorkstationCompletionOriginal{internal static string Hash(string value){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}internal static bool ValidKey(string value)=>value?.Length==64&&value.All(c=>c>='0'&&c<='9'||c>='a'&&c<='f');}
static class RebirthWorkstationCompletionWitnessValidator{internal static bool Valid=true;internal static bool Matches(XElement before,XElement delivery,int count)=>Valid;}
static class RebirthOriginalActionScope{internal static bool Validate(string owner,string creation)=>(owner=="viewer"||owner==new string('d',64))&&creation=="creation";}
static class RebirthProductiveEarnedArtifact{internal static bool Authority=true;internal static bool RemoteAuthority(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation)=>Authority&&actor!=null&&peer!=null&&station!=null&&creation=="creation";internal static bool SafePath(string path)=>true;internal static string WorldIdentity(object world)=>"world";internal static bool FileMatches(string path,byte[] bytes)=>File.Exists(path)&&File.ReadAllBytes(path).SequenceEqual(bytes);}
class EntityPlayer{internal object world;internal int Died=2;}
class ClientInfo{internal string Owner=new string('d',64);}
struct Vector3i{internal int x,y,z;}
class TileEntityWorkstation{internal Vector3i ToWorldPos()=>new Vector3i{x=1,y=2,z=3};}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class GameIO{internal static string Save;internal static string GetSaveGameDir()=>Save;}
static class RebirthStablePlayerIdentity{internal class Identity{internal string StorageKey;}internal static bool TryFromClientInfo(ClientInfo peer,out Identity identity){identity=new Identity{StorageKey=peer.Owner};return true;}}
static class RebirthLogisticsEndpointIdentity{internal static string Birth;internal static bool TryGetIncarnation(object world,TileEntityWorkstation station,out string birth){birth=Birth;return true;}}
static class RebirthProductiveEarnedWire{internal enum Status{UiTerminalSaved=7}internal class Reply{internal Guid Operation,Nonce,Birth;internal string World,Owner,Creation,Digest,QueueOwner;internal int Deaths;internal Vector3i Position;internal Status State;}}
internal static partial class RebirthProductiveEarnedUiRemote{internal static bool Extras=true;internal static object[] ReadGrid(XElement node,string name)=>new object[]{new object()};internal static bool ValidateExtraOutputs(Recipe recipe,XElement delivery,object[] extras)=>Extras;static string Hash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}internal static bool QueryFixture(EntityPlayer actor,ClientInfo peer,TileEntityWorkstation station,string creation,Guid operation,Guid nonce,string hash,out RebirthProductiveEarnedWire.Reply reply)=>TryQueryLoadedDeferredTerminal(actor,peer,station,creation,operation,nonce,hash,out reply);}
static class RebirthProductiveEarnedUiCoordinator{internal static bool AllowWrite=true;internal static bool WriteRemoteExact(string path,byte[] bytes){if(!AllowWrite)return false;File.WriteAllBytes(path,bytes);return true;}}
static class RebirthCookingRemoteAdmission{internal static XElement ReadProtocolXml(string text,int max)=>XElement.Parse(text);}
static class RebirthProductiveEarnedUiOriginal{
 internal class Earned{internal byte[] AfterNative={1};internal int AfterElapsedOffset,RowIndex;}
 internal class UiFrame{internal Guid Operation,Birth;internal string Owner,Creation="creation",World="world",OwnerPurpose="purpose";internal int Deaths=2;internal Earned Earned=new Earned();}
 internal static UiFrame Frame;internal static byte[] Image;internal static UiFrame ReadUiArtifact(byte[] image){if(!Image.SequenceEqual(image))throw new InvalidDataException();return Frame;}
}
static class RebirthProductiveEarnedRowProof{internal const int MaximumArtifactBytes=1048576;}
static class RebirthProductiveEarnedNativeBody{internal static bool Valid=true;internal static Body Value=new Body();internal static bool TryRead(byte[] native,int offset,out Body body,out ulong elapsed){body=Value;elapsed=0;return Valid;}}
static class RebirthRemoteResourcePlacedStationNativeReader{internal static bool TryReadCompletionArgument(byte[] bytes,out Argument argument){argument=new Argument();return true;}}
static class RebirthProductiveEarnedObservationBudget{
 internal static bool Available=true;internal static int Held;class Lease:IDisposable{bool closed;public void Dispose(){if(!closed){closed=true;Held--;}}}
 internal static bool TryRetainDetached(long bytes,out IDisposable allocation){allocation=null;if(!Available)return false;Held++;allocation=new Lease();return true;}
 internal static bool TryResizeDetached(IDisposable allocation,long bytes)=>Available&&allocation!=null;
}
static class RebirthWorkstationCompletionPublication{
 internal const int MaximumBytes=24*1024*1024;
 internal static readonly Dictionary<string,XElement> Files=new Dictionary<string,XElement>();internal static bool WriteAllowed=true;internal static string RefusedSuffix;internal static int Writes;
 internal static XElement ReadPrimary(string path)=>new XElement(Files[path]);internal static byte[] CopyPublishedBytes(string path)=>Files.TryGetValue(path,out var node)?Encoding.UTF8.GetBytes(node.ToString(SaveOptions.DisableFormatting)):null;
 internal static long PublishedLength(string path)=>CopyPublishedBytes(path)?.Length??0;
 internal static bool MatchesPublishedBytes(string path,byte[] bytes)=>CopyPublishedBytes(path)?.SequenceEqual(bytes)==true;
 internal static void RequireNativeSaveFile(string path){if(path!="native-region")throw new InvalidDataException();}
 internal static bool Write(string path,XElement node){if(!WriteAllowed||RefusedSuffix!=null&&path.EndsWith(RefusedSuffix))return false;Files[path]=new XElement(node);Writes++;return true;}
}
internal static partial class RebirthProductiveEarnedUiDeferredTransport{
 internal sealed partial class HostContribution{internal Guid Operation=Guid.NewGuid();internal RebirthWorkstationCompletionPipeline.RemoteOutputHostSource LoadedOwnerSource;internal object AuthorityLoadedOriginal;internal object World=>LoadedOwnerSource.World;internal LoadedMemberSource LoadedMemberSource=>LoadedOwnerSource==null?null:new LoadedMemberSource(LoadedOwnerSource);internal bool LoadedIdentityCurrent()=>LoadedOwnerSource?.Current()==true&&LoadedOwnerSource.Native?.IdentityCurrent()==true;}
 // The native source/identity flags remain the preexisting explicit adapters.
 // This wrapper carries them unchanged into the shared member grammar.
 internal sealed class LoadedMemberSource{
  readonly RebirthWorkstationCompletionPipeline.RemoteOutputHostSource source;
  internal LoadedMemberSource(RebirthWorkstationCompletionPipeline.RemoteOutputHostSource value){source=value;}
  internal object World=>source.World;internal string Save=>source.Save;internal Guid Birth=>source.Birth;
  internal Position Position=>source.Position;internal XElement DeferredMembership=>source.DeferredMembership;
  internal XElement Request=>source.Request;internal RebirthWorkstationCompletionPipeline.Native Native=>source.Native;
  internal bool Current()=>source.Current();
 }
 internal sealed class HostTerminalPublication{
  internal bool Valid=true;internal LoadedNativeMember Member;internal long GroupEvidenceBound=>1024;internal bool Current()=>Valid;internal bool IncludesLoadedNativeMember(LoadedNativeMember member)=>Valid&&ReferenceEquals(Member,member);
  internal LoadedUiMember Ui;internal bool IncludesLoadedUiMember(LoadedUiMember member)=>Valid&&ReferenceEquals(Ui,member);
  internal XElement Evidence;internal IDisposable EnterGroupPublication(long bytes)=>new Workspace();internal XElement CopyGroupEvidence()=>Evidence==null?new XElement("nativeOwnerGroup"):new XElement(Evidence);class Workspace:IDisposable{public void Dispose(){}}
 }
 static bool Delta=true,NativeArgument=true,AddBody=true;
 static bool PhysicalSame=true;static bool NativePhysicalSame(object[] left,object[] right)=>PhysicalSame;
 static string HydrationHash(byte[] bytes){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
 static bool MemberDelta(XElement member,object[] completions,Recipe recipe,int owner,object primary)=>Delta;
 static bool NativeArgumentMatches(Argument argument,Recipe recipe,int owner)=>NativeArgument;
 static bool TryDescribeRemoteNativeAddBody(Recipe recipe,int owner,byte[] body,int offset,XElement contribution,out byte[] expected,out int elapsed){expected=new byte[]{1};elapsed=0;return AddBody;}
 static byte[] ReadBytes(XElement node,string name)=>Convert.FromBase64String((string)node.Element(name));
 static Guid ReadGuid(XElement node,string name)=>Guid.ParseExact((string)node.Attribute(name),"N");
 static long MeasurePublicationElement(XElement node)=>Encoding.UTF8.GetByteCount(node.ToString(SaveOptions.DisableFormatting));
 static HostContribution aggregate;static RebirthProductiveEarnedUiQueueOwner.Witness creator;static XElement member;static string path;static int checks;
 static XElement Envelope(Guid operation,XElement before)=>new XElement("workstationCompletionIntent",new XAttribute("operation",operation.ToString("N")),new XAttribute("world","world"),new XElement("before",before));
 static XElement Read(string suffix)=>RebirthWorkstationCompletionPublication.Files[path+suffix];
 static void Seed(){
  RebirthWorkstationCompletionPublication.Files.Clear();RebirthWorkstationCompletionPublication.WriteAllowed=true;RebirthWorkstationCompletionPublication.RefusedSuffix=null;RebirthWorkstationCompletionPublication.Writes=0;
  RebirthProductiveEarnedObservationBudget.Available=true;Delta=NativeArgument=AddBody=RebirthProductiveEarnedNativeBody.Valid=true;RebirthProductiveEarnedNativeBody.Value=new Body();
  aggregate=new HostContribution{LoadedOwnerSource=new RebirthWorkstationCompletionPipeline.RemoteOutputHostSource()};creator=new RebirthProductiveEarnedUiQueueOwner.Witness();var operation=Guid.NewGuid();var nonce=Guid.NewGuid();path=System.IO.Path.Combine(RebirthWorkstationCompletionOutbox.Root("server"),"remoteNative",operation.ToString("N")+".xml");
  var before=new XElement("nativeOwningBefore",new XAttribute("world","world"),new XAttribute("slot",0),new XAttribute("beforeCount",2),new XAttribute("nativeElapsedOffset",0),new XAttribute("x",1),new XAttribute("y",2),new XAttribute("z",3),new XAttribute("startingOwner",7),new XAttribute("startingOwnerKey","owner"),new XAttribute("startingOwnerCreation","creation"),new XAttribute("startingOwnerDeaths",2),new XElement("nativeBefore","AQ=="));
  member=new XElement("member",new XAttribute("kind","native"),new XAttribute("operation",operation.ToString("N")),new XAttribute("source","purpose"),new XAttribute("contribution",1),new XElement("nativeOriginal",new XElement(before)),new XElement("nativeAddArgument","AQ=="),new XElement("beforeComplete","Ag=="),new XElement("afterComplete","Aw=="));
  RebirthWorkstationCompletionPublication.Files[path]=Envelope(operation,new XElement("remoteNativeOriginal",new XAttribute("nonce",nonce.ToString("N")),new XAttribute("birth",aggregate.LoadedOwnerSource.Birth.ToString("N")),new XAttribute("ownerPurpose","purpose"),new XAttribute("viewer","viewer"),new XAttribute("creation","creation"),new XElement(before)));
  string hash=RebirthWorkstationCompletionPipeline.CachedFileHash(RebirthWorkstationCompletionPublication.CopyPublishedBytes(path));
  RebirthWorkstationCompletionPublication.Files[path+".saved"]=Envelope(operation,new XElement("remoteNativeBeforeSaved",new XAttribute("source",hash),new XAttribute("nonce",nonce.ToString("N")),new XAttribute("nativePath","native-region"),new XAttribute("payload",new string('b',64)),new XAttribute("offset",8),new XAttribute("length",100),new XAttribute("tile",new string('c',64))));
  RebirthWorkstationCompletionPublication.Files[path+".contribution"]=Envelope(operation,new XElement("deferredNativeContribution",new XAttribute("operation",operation.ToString("N")),new XAttribute("nonce",nonce.ToString("N")),new XAttribute("original",hash),new XAttribute("source","purpose"),new XElement("members",new XElement(member)),new XElement("nativeAddArgument","AQ=="),new XElement("beforeComplete","Ag=="),new XElement("afterComplete","Aw==")));
  RebirthWorkstationCompletionPublication.Files[path+".station-saved"]=Envelope(operation,new XElement("remoteNativeStationSaved",new XAttribute("version",1),new XAttribute("phase",3),new XAttribute("source",hash),new XAttribute("nonce",nonce.ToString("N")),new XAttribute("birth",aggregate.LoadedOwnerSource.Birth.ToString("N")),new XAttribute("native",new string('a',64)),new XAttribute("payload",new string('b',64)),new XAttribute("tile",new string('c',64)),new XAttribute("offset",8),new XAttribute("length",100),new XAttribute("nativePath","native-region")));
  aggregate.LoadedOwnerSource.DeferredMembership=new XElement("deferredNativeContribution",new XElement("members",new XElement(member)));
 }
 static bool Capture(out LoadedNativeMember value)=>LoadedNativeMember.TryCapture(aggregate,member,creator,out value);
 static void Need(bool value){if(!value)throw new Exception("loaded member assertion "+checks);checks++;}
 public static void Main(string[] args){
  if(args.Length==1&&args[0]=="ui"){RunUi();return;}
  if(args.Length==1&&args[0]=="query"){RunUiQuery();return;}
  if(args.Length==1&&args[0]=="authority"){RunAuthority();return;}
  Seed();Need(Capture(out var value)&&value.Current()&&value.Matches(member));var lease=value.Retain();value.Dispose();Need(value.Current());lease.Dispose();Need(!value.Current()&&RebirthProductiveEarnedObservationBudget.Held==0);
  Seed();RebirthWorkstationCompletionPublication.Files.Remove(path+".station-saved");Need(!Capture(out _));
  Seed();Read(".station-saved").Element("before").Element("remoteNativeStationSaved").SetAttributeValue("phase",2);Need(!Capture(out _));
  Seed();aggregate.LoadedOwnerSource.Native.Live=false;Need(!Capture(out _));
  Seed();aggregate.LoadedOwnerSource.Native.Identity=false;Need(!Capture(out _));
  Seed();creator.Live=false;Need(!Capture(out _));
  Seed();aggregate.LoadedOwnerSource.DeferredMembership.Element("members").RemoveNodes();Need(!Capture(out _));
  Seed();Read(".saved").Element("before").Elements().Single().SetAttributeValue("source","foreign");Need(!Capture(out _));
  Seed();Read(".contribution").Element("before").Elements().Single().SetAttributeValue("original","foreign");Need(!Capture(out _));
  Seed();RebirthProductiveEarnedNativeBody.Valid=false;Need(!Capture(out _));
  Seed();Delta=false;Need(!Capture(out _));
  Seed();NativeArgument=false;Need(!Capture(out _));
  Seed();AddBody=false;Need(!Capture(out _));
  Seed();RebirthProductiveEarnedNativeBody.Value.Queue[0].Multiplier=3;Need(!Capture(out _));
  Seed();aggregate.LoadedOwnerSource.Position=new Position{x=9,y=2,z=3};Need(!Capture(out _));
  Seed();Need(Capture(out value));Read(".saved").SetAttributeValue("changed",true);Need(!value.Current());value.Dispose();
  Seed();RebirthProductiveEarnedObservationBudget.Available=false;Need(!Capture(out _));
  Seed();Need(Capture(out value));var proof=new HostTerminalPublication{Member=value};Need(value.SaveTerminal(proof)&&value.TerminalSaved);int writes=RebirthWorkstationCompletionPublication.Writes;Need(value.SaveTerminal(proof)&&RebirthWorkstationCompletionPublication.Writes==writes);value.Dispose();
  Seed();Need(Capture(out value));proof=new HostTerminalPublication{Member=value};RebirthWorkstationCompletionPublication.RefusedSuffix=".query-terminal";Need(!value.SaveTerminal(proof)&&!value.TerminalSaved);RebirthWorkstationCompletionPublication.RefusedSuffix=null;Need(value.SaveTerminal(proof)&&value.HistoricalTerminalSaved);value.Dispose();
  Need(RebirthProductiveEarnedObservationBudget.Held==0);Console.WriteLine("PASS "+checks+" loaded native member assertions; native/load/publication adapters excluded");
 }
}
// ACTUAL_LOADED_NATIVE_MEMBER
// ACTUAL_LOADED_UI_MEMBER
// ACTUAL_LOADED_UI_QUERY
// ACTUAL_LOADED_AUTHORITY_MEMBER
