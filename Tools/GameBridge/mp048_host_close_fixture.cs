using System;using System.IO;using System.Collections.Generic;using System.Security.Cryptography;using System.Text;
// Whole actual HostClosed writer/readback/social handoff. Native permission,
// NPC saved state, host terminal and atomic social store are explicit adapters.
namespace Platform{class PlatformUserIdentifierAbs{}}
class NetPackage{}
class World{internal WorldState worldState=new WorldState();internal bool IsRemote()=>false;internal class WorldState{internal string Guid;}}
class GameManager{internal static GameManager Instance=new GameManager();internal World World=new World();}
static class GameIO{internal static string Save;internal static string GetSaveGameDir()=>Save;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
class RebirthNpcTradeQuote{internal Guid QuoteId,NpcId=Guid.NewGuid();internal string ActorId="actor";internal int Quantity=3;}
class RebirthNpcTradeOriginal{internal Guid Operation=Guid.NewGuid();internal string Digest=new string('a',64),World,Account="owner";internal RebirthNpcTradeQuote CopyQuote()=>new RebirthNpcTradeQuote{QuoteId=Operation};}
static class RebirthRemoteResourceDurableJournal{internal static string Digest(string text){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}
static class RebirthNpcInventoryPersistenceStore{internal static bool Saved=true;internal static string CaptureTradeSaved(RebirthNpcTradeOriginal original,object plan,bool after)=>Saved?"saved-npc-after":null;}
static class RebirthNpcTradeColdFence{internal static int Closed;internal static void ObserveClosed(RebirthNpcTradeService.HostClosed closed){Closed++;}}
enum RebirthNpcSocialEventKind{Trade}
static class RebirthNpcSocialGameplayGateway{internal static int Calls;internal static bool Publish(Guid id,Guid npc,string actor,string faction,RebirthNpcSocialEventKind kind,float amount,float confidence){Calls++;return true;}}
static class RebirthNpcSocialPersistenceStore{internal static bool Saved=true;internal static string Identity;internal static bool TrySaveTradeCompletion(Guid id,string identity,Func<bool> publish){Identity=identity;return Saved&&publish();}internal static bool IsTradeCompletionSaved(Guid id,string identity)=>Saved&&Identity==identity;}
static class RebirthNpcSaveQuarantineBundle{internal static void ObserveServerTradeCompletion(RebirthNpcTradeService.HostClosed closed){}}
public static partial class RebirthNpcTradeService
{
 private static object Sync=new object();private static Dictionary<Guid,Record> Quotes=new Dictionary<Guid,Record>();
 private class Record{internal bool Consumed=true;internal int Resolution;internal HostPrepared OwnerPreparation;}
 private static Guid Derive(Guid id,byte discriminator)=>id;
 internal class HostPrepared{internal RebirthNpcTradeOriginal Original;internal HostTerminal Terminal;internal string PathName,Image="prepared";internal object NpcPlan=new object();internal HostClosed ClosedRecord;internal bool Metadata=true,Acknowledged=true;internal bool MetadataCurrent(NetPackage p,Platform.PlatformUserIdentifierAbs u)=>Metadata;internal bool ObserveAcknowledgement(NetPackage p,Platform.PlatformUserIdentifierAbs u)=>Acknowledged;}
 internal class HostTerminal{internal string Digest=new string('b',64);internal bool Retained=true;internal bool IsRetained()=>Retained;}
 internal class OwnerAdmission{internal class LocalPermission{internal GrantValue Grant;internal bool Ack=true;internal bool AcknowledgementCurrent()=>Ack;internal class GrantValue{internal HostPrepared Prepared;}}}
 internal static void Run(string root)
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Host close assertion "+checks);}
  HostPrepared Seed()
  {
   GameIO.Save=root;string world=Guid.NewGuid().ToString();GameManager.Instance.World.worldState.Guid=world;
   var original=new RebirthNpcTradeOriginal{World=world};string directory=Path.Combine(root,"RebirthData","NpcTradeOriginals",Guid.Parse(world).ToString("N"),RebirthRemoteResourceDurableJournal.Digest(original.Account));Directory.CreateDirectory(directory);
   var p=new HostPrepared{Original=original,Terminal=new HostTerminal(),PathName=Path.Combine(directory,original.Operation.ToString("N")+".xml")};
   Quotes.Clear();Quotes.Add(original.Operation,new Record{OwnerPreparation=p});RebirthNpcInventoryPersistenceStore.Saved=true;RebirthNpcSocialPersistenceStore.Saved=true;return p;
  }
  OwnerAdmission.LocalPermission Permission(HostPrepared p)=>new OwnerAdmission.LocalPermission{Grant=new OwnerAdmission.LocalPermission.GrantValue{Prepared=p}};
  Check(!HostClosed.TryCloseLocal(null,out _));
  var p=Seed();var permission=Permission(p);p.Terminal.Retained=false;Check(!HostClosed.TryCloseLocal(permission,out _)&&!File.Exists(p.PathName+".closed"));
  p=Seed();permission=Permission(p);permission.Ack=false;Check(!HostClosed.TryCloseLocal(permission,out _)&&!File.Exists(p.PathName+".closed"));
  p=Seed();permission=Permission(p);RebirthNpcInventoryPersistenceStore.Saved=false;Check(!HostClosed.TryCloseLocal(permission,out _)&&!File.Exists(p.PathName+".closed"));
  p=Seed();permission=Permission(p);Check(HostClosed.TryCloseLocal(permission,out var closed)&&ReferenceEquals(p.ClosedRecord,closed)&&Quotes[p.Original.Operation].Resolution==1&&closed.CanRetireFromQuote);
  string canonical=File.ReadAllText(p.PathName+".closed");Check(canonical==closed.Image&&closed.TerminalDigest==p.Terminal.Digest);
  RebirthNpcSocialPersistenceStore.Saved=false;int published=RebirthNpcSocialGameplayGateway.Calls;Check(!closed.TrySaveSocialCompletion()&&RebirthNpcSocialGameplayGateway.Calls==published);
  Check(!closed.HasObservedSavedSocialCompletion);
  RebirthNpcSocialPersistenceStore.Saved=true;Check(closed.TrySaveSocialCompletion()&&RebirthNpcSocialPersistenceStore.Identity==p.Original.Operation.ToString("N")+":"+p.Original.Digest+":"+p.Terminal.Digest);
  Check(closed.HasObservedSavedSocialCompletion);
  string save=GameIO.Save;GameIO.Save=save+"foreign";Check(!closed.TrySaveSocialCompletion());GameIO.Save=save;
  p.Metadata=false;Check(!closed.CanReply(new NetPackage(),new Platform.PlatformUserIdentifierAbs()));p.Metadata=true;
  File.WriteAllText(p.PathName+".closed",canonical+" ");Check(!closed.CanRetireFromQuote&&!closed.TrySaveSocialCompletion());
  p=Seed();permission=Permission(p);File.WriteAllText(p.PathName+".closed","foreign");Check(!HostClosed.TryCloseLocal(permission,out _)&&File.ReadAllText(p.PathName+".closed")=="foreign"&&Quotes[p.Original.Operation].Resolution==0);
  p=Seed();permission=Permission(p);Quotes[p.Original.Operation].OwnerPreparation=new HostPrepared();Check(!HostClosed.TryCloseLocal(permission,out _)&&File.Exists(p.PathName+".closed")&&Quotes[p.Original.Operation].Resolution==0);
  p=Seed();Check(!HostClosed.TryClose(p,new string('c',64),new NetPackage(),new Platform.PlatformUserIdentifierAbs(),out _));
  Check(HostClosed.TryClose(p,p.Terminal.Digest,new NetPackage(),new Platform.PlatformUserIdentifierAbs(),out closed)&&closed.CanReply(new NetPackage(),new Platform.PlatformUserIdentifierAbs()));
  Check(!HostClosed.TryClose(p,p.Terminal.Digest,new NetPackage(),new Platform.PlatformUserIdentifierAbs(),out _));
  Console.WriteLine(checks+" actual host close assertions passed; native permission/NPC/terminal/social adapters NOT certified");
 }
}
class Program{static void Main(string[] args)=>RebirthNpcTradeService.Run(args[0]);}
