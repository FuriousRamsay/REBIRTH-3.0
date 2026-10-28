// AUTHORED_NOT_RUN. Actual reconnect/paired-load producers with actual XML writer.
// Native owner/tile load, peer/currentness and phase observations are explicit doubles.
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Security.Cryptography;
class World{}
class Recipe{}
class RecipeQueueItem{internal Recipe Recipe=new Recipe();}
class TileEntityWorkstation{internal RecipeQueueItem[] Queue={new RecipeQueueItem()};}
class EntityPlayer{}
class Connection{internal bool Dead;internal bool IsDisconnected()=>Dead;}
class ClientInfo{internal bool disconnecting;internal Connection[] netConnection={new Connection()};}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class GameIO{internal static string Save;internal static string GetSaveGameDir()=>Save;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();}
static class RebirthStationCompletionOriginalScope{internal static StringComparison PathComparison=>StringComparison.OrdinalIgnoreCase;}
static class RebirthWorkstationCompletionOriginal{internal static string Hash(string value){using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(value)));}}
static class RebirthWorkstationCompletionOutbox{internal static string CurrentGuid(World world)=>"fixture-world";internal static string Root(string value)=>Path.Combine(GameIO.Save,"RebirthData","Survivor","WorkstationCompletions",value);}
class RebirthStablePlayerIdentity{internal string StorageKey="owner",CanonicalId="account";internal static bool TryFromClientInfo(ClientInfo peer,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return peer!=null;}}
static class RebirthProductiveEarnedNativeBody{internal static bool MatchesCurrent(TileEntityWorkstation station,byte[] before,int offset)=>true;}
static class RebirthProductiveEarnedObservationBudget{internal static bool Available=true;sealed class Charge:IDisposable{public void Dispose(){}}internal static bool TryRetainDetached(long bytes,out IDisposable value){value=Available?new Charge():null;return value!=null;}}
// ACTUAL_PUBLICATION
static class Subject
{
 sealed class NativeFile{internal World world;internal byte[] native={1,2,3};internal Dictionary<string,float> buffs=new Dictionary<string,float>();}
 sealed class RemoteLoadedOwner
 {internal NativeFile File=new NativeFile();internal EntityPlayer Actor=new EntityPlayer();internal string Creation="life";internal int Deaths=2;internal bool Valid=true;
  internal ClientInfo Peer=new ClientInfo();internal Guid Generation=Guid.NewGuid();internal bool Current()=>Valid;internal ClientInfo CurrentPeer()=>Peer;internal Guid CaptureReconnectGeneration(Guid op)=>Generation;}
 sealed class LoadedStation{internal bool Valid=true;internal string ReceiptPath;internal bool Current()=>Valid;}
 sealed class PendingPublication{internal bool Saved=true;internal bool HistoricalSaved()=>Saved;}
 static bool PhaseBytes(Dictionary<string,float> buffs,string name,out float value){value=0;return !buffs.TryGetValue(name,out value)||value==0;}
 static string RecyclerFileDigest(string path)=>RebirthWorkstationCompletionOriginal.Hash(Convert.ToBase64String(File.ReadAllBytes(path)));
 sealed class ClosedAuthorityCompletion
 {
  internal object Attached;internal Connection Connection=new Connection{Dead=true};internal ClientInfo Peer=new ClientInfo();internal World World=GameManager.Instance.World;
  internal TileEntityWorkstation Station=new TileEntityWorkstation();internal RecipeQueueItem Row;internal Recipe Recipe;internal byte[] BeforeNative={0};internal int BeforeOffset;
  internal string Creation="life",Owner="owner",Account="account",WorldKey="fixture-world";internal int Deaths=2;
  internal Guid Operation=Guid.NewGuid(),Ticket=Guid.NewGuid(),OwnerGeneration;internal EntityPlayer Recipient=new EntityPlayer();internal byte[] Image;
  internal PendingPublication Pending=new PendingPublication();internal bool Valid=true;
  internal ClosedAuthorityCompletion(){Row=Station.Queue.Last();Recipe=Row.Recipe;Image=Encoding.UTF8.GetBytes(new XElement("recyclerClosedAuthorityOriginal",new XAttribute("operation",Operation.ToString("N"))).ToString(SaveOptions.DisableFormatting));}
  internal bool NativeLineageCurrent()=>Valid;internal bool Current()=>Valid;
  // ACTUAL_REJOIN
  // ACTUAL_PAIR
  internal bool Pair(LoadedStation tile,RemoteLoadedOwner owner,out ClientInfo peer,out Guid generation)=>QualifyPendingReconnectedPair(tile,owner,out peer,out generation);
 }
 static void Check(bool value,string label){if(!value)throw new Exception(label);}
 static RemoteLoadedOwner Owner(ClosedAuthorityCompletion source)=>new RemoteLoadedOwner{File=new NativeFile{world=source.World}};
 static string Receipt(ClosedAuthorityCompletion source,RemoteLoadedOwner owner,bool pair)=>Path.Combine(RebirthWorkstationCompletionOutbox.Root("recyclerPlaced"),source.Operation.ToString("N")+(pair?".closed-pending-pair.":".closed-owner-rejoin.")+owner.Generation.ToString("N")+".xml");
 static void CheckBefore(string path,ClosedAuthorityCompletion source)
 {var record=RebirthWorkstationCompletionPublication.ReadPrimary(path);Check(XNode.DeepEquals(record.Element("before")?.Elements().SingleOrDefault(),XElement.Parse(Encoding.UTF8.GetString(source.Image))),"actual writer accepts exact original Before");}
 static void Main()
 {
  GameIO.Save=Path.Combine(Path.GetTempPath(),"CodexTest-mp027-rejoin-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(GameIO.Save);
  var source=new ClosedAuthorityCompletion();var owner=Owner(source);
  Check(source.AdoptUnacceptedNativeOwner(owner),"actual unaccepted owner rejoin publishes");CheckBefore(Receipt(source,owner,false),source);
  Check(ReferenceEquals(source.Recipient,owner.Actor)&&ReferenceEquals(source.Connection,owner.Peer.netConnection[0])&&source.OwnerGeneration==owner.Generation,"only qualified loaded owner rebound");
  var paired=new ClosedAuthorityCompletion();owner=Owner(paired);string tilePath=Path.Combine(GameIO.Save,"native-tile-receipt");File.WriteAllText(tilePath,"explicit fixture native observation");
  var tile=new LoadedStation{ReceiptPath=tilePath};
  Check(paired.Pair(tile,owner,out var peer,out var generation)&&ReferenceEquals(peer,owner.Peer)&&generation==owner.Generation,"actual paired-load receipt published");CheckBefore(Receipt(paired,owner,true),paired);
  var blocked=new ClosedAuthorityCompletion();owner=Owner(blocked);blocked.Connection.Dead=false;
  Check(!blocked.AdoptUnacceptedNativeOwner(owner)&&!blocked.Pair(tile,owner,out peer,out generation),"live old connection cannot rebind");
  blocked.Connection.Dead=true;owner.File.buffs["rbRecyclerPlaced_"+blocked.Operation.ToString("N")]=2;
  Check(!blocked.AdoptUnacceptedNativeOwner(owner)&&!blocked.Pair(tile,owner,out peer,out generation),"entered native owner cannot use unaccepted path");
  owner.File.buffs.Clear();RebirthProductiveEarnedObservationBudget.Available=false;
  Check(!blocked.AdoptUnacceptedNativeOwner(owner)&&!blocked.Pair(tile,owner,out peer,out generation),"budget denial has no rebind");
  RebirthProductiveEarnedObservationBudget.Available=true;
  string unknown=Receipt(blocked,owner,false)+".tmp";File.WriteAllText(unknown,"foreign-stage");var originalRecipient=blocked.Recipient;
  Check(!blocked.AdoptUnacceptedNativeOwner(owner)&&ReferenceEquals(blocked.Recipient,originalRecipient)&&File.ReadAllText(unknown)=="foreign-stage","unknown publication preserves peer and evidence");
  Console.WriteLine("PASS MP027 actual reconnect producers/full writer; native observation doubles, topology NOT_RUN. Evidence: "+GameIO.Save);
 }
}
