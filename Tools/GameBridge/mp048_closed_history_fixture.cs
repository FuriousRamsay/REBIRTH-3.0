// AUTHORED_NOT_RUN. Entire production closed-history reader. Native admission,
// original codec, saved NPC plan and social persistence are explicit adapters.
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;
using Platform;
namespace Platform{class PlatformUserIdentifierAbs{public override string ToString()=>"user";}}
class WorldState{internal string Guid=System.Guid.NewGuid().ToString();}
class World{internal bool Remote;internal WorldState worldState=new WorldState();internal EntityPlayer Actor;internal bool IsRemote()=>Remote;internal object GetEntity(int id)=>id==7?Actor:null;}
class EntityPlayer{internal World world;internal string Creation="creation";}
class EntityPlayerLocal:EntityPlayer{}
class ClientInfo{internal int entityId=7;internal bool disconnecting,loginDone=true,bAttachedToEntity=true;internal string Account="account";}
class NetPackage{internal ClientInfo Sender;internal bool Auth=true;internal bool ValidEntityIdForSender(int id)=>Auth&&id==7;internal bool ValidUserIdForSender(Platform.PlatformUserIdentifierAbs user)=>Auth&&user!=null;}
class Clients{internal ClientInfo Peer;internal ClientInfo ForEntityId(int player)=>player==7?Peer:null;}
class ConnectionManager{internal bool IsServer=true;internal Clients Clients=new Clients();}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class GameIO{internal static string Root;internal static string GetSaveGameDir()=>Root;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class RebirthNpcNetworkEpoch{internal static bool Valid=true;internal static bool ValidateConnectionEpoch(int player,uint connection)=>Valid&&player==7&&connection==1;}
static class RebirthStablePlayerIdentity{internal class Identity{internal string CanonicalId;}internal static bool TryFromClientInfo(ClientInfo peer,out Identity identity){identity=new Identity{CanonicalId=peer.Account};return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer actor)=>actor.Creation;}
static class RebirthRemoteResourceDurableJournal{internal static string Digest(string text){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}
class Quote{internal string ActorId="user";}
class RebirthNpcTradeOriginal{
 internal static readonly Dictionary<string,RebirthNpcTradeOriginal> Images=new Dictionary<string,RebirthNpcTradeOriginal>();
 internal Guid Operation=Guid.NewGuid();internal string Account="account",Creation="creation",World;internal string Image=>Operation.ToString("N");internal string Digest=>RebirthRemoteResourceDurableJournal.Digest(Image);internal Quote CopyQuote()=>new Quote();internal static RebirthNpcTradeOriginal Read(string image)=>Images[image];
}
static class RebirthNpcTradePrefix{internal static int Changes(RebirthNpcTradeOriginal original)=>2;}
static class RebirthNpcTradeLocalOwnerLoad{
 internal class Source{internal EntityPlayerLocal Player;internal RebirthNpcTradeOriginal Original;internal int Step=4,Phase=3;internal bool Match=true,Persist=true;internal bool Matches(RebirthNpcTradeOriginal original)=>Match&&ReferenceEquals(Original,original);internal bool PersistCurrent(RebirthNpcTradeOriginal original)=>Persist&&Matches(original);internal bool TryStage(out int step,out int phase){step=Step;phase=Phase;return true;}}
}
static class RebirthNpcTradeOwnerOriginalStore{internal static string Ack;internal static bool TryReadAcknowledgement(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out string digest){digest=Ack;return Ack!=null;}}
static class RebirthNpcInventoryPersistenceStore{
 internal static bool Plan=true,After=true;internal static bool TryReadTradePlanFromSavedBefore(RebirthNpcTradeOriginal original,string before,out object plan){plan=new object();return Plan&&before=="saved-before";}internal static bool MatchesTradeSavedImage(RebirthNpcTradeOriginal original,object plan,string image,bool after)=>After&&image=="saved-after";
}
static class RebirthNpcTradeColdFence{internal static int Closed;internal static void ObserveClosed(RebirthNpcTradeClosedHistory history){Closed++;}}
static class RebirthNpcSaveQuarantineBundle{internal static void ObserveServerTradeCompletion(RebirthNpcTradeClosedHistory history){}}
static class RebirthNpcTradeService{
 internal static bool Social=true;internal static int SocialCalls;internal static string PathName;
 internal static bool TrySaveClosedSocial(RebirthNpcTradeOriginal original,string path,string terminal){SocialCalls++;return Social;}
 internal static bool IsClosedSocialSaved(RebirthNpcTradeOriginal original,string terminal)=>Social;
 internal static class HostPrepared{
  internal class RetainedSource{
   internal static bool Capture=true,History=true;internal RebirthNpcTradeOriginal Original;
   internal static bool TryCaptureLocal(RebirthNpcTradeLocalOwnerLoad.Source source,out RetainedSource retained){retained=Capture?new RetainedSource{Original=source.Original}:null;return retained!=null;}
   internal bool TryClosedHistory(out string terminal){terminal=RebirthRemoteResourceDurableJournal.Digest(File.ReadAllText(PathName+".terminal"));return History&&RebirthNpcTradeClosedHistory.IsClosedPreparation(PathName,Original,File.ReadAllText(PathName));}
  }
 }
}
// ACTUAL_CLOSED_HISTORY
class Program{
 static int checks;static string root,path,terminal;static RebirthNpcTradeOriginal original;static EntityPlayerLocal actor;static ClientInfo peer;static NetPackage packet;static RebirthNpcTradeLocalOwnerLoad.Source source;
 static void Need(bool value){if(!value)throw new Exception("closed history "+checks);checks++;}
 static void Seed(){
  var world=new World();actor=new EntityPlayerLocal{world=world};world.Actor=actor;GameManager.Instance.World=world;peer=new ClientInfo();packet=new NetPackage{Sender=peer};SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Peer=peer;
  RebirthNpcNetworkEpoch.Valid=true;RebirthNpcInventoryPersistenceStore.Plan=RebirthNpcInventoryPersistenceStore.After=true;RebirthNpcTradeService.HostPrepared.RetainedSource.Capture=RebirthNpcTradeService.HostPrepared.RetainedSource.History=RebirthNpcTradeService.Social=true;RebirthNpcTradeService.SocialCalls=0;
  original=new RebirthNpcTradeOriginal{World=world.worldState.Guid};RebirthNpcTradeOriginal.Images.Add(original.Image,original);source=new RebirthNpcTradeLocalOwnerLoad.Source{Original=original,Player=actor};
  path=Path.Combine(root,"RebirthData","NpcTradeOriginals",Guid.Parse(original.World).ToString("N"),RebirthRemoteResourceDurableJournal.Digest(original.Account),original.Operation.ToString("N")+".xml");Directory.CreateDirectory(Path.GetDirectoryName(path));RebirthNpcTradeService.PathName=path;
  var prepared=new XElement("npcTradePrepared",new XAttribute("version",1),new XAttribute("originalDigest",original.Digest),new XElement("original",original.Image),new XElement("savedNpcBefore","saved-before")).ToString(SaveOptions.DisableFormatting);string digest=RebirthRemoteResourceDurableJournal.Digest(prepared);File.WriteAllText(path,prepared);
  File.WriteAllText(path+".grant",new XElement("npcTradeOwnerGrant",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),new XAttribute("preparationDigest",digest)).ToString(SaveOptions.DisableFormatting));
  string body=new XElement("npcTradeTerminal",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),new XAttribute("preparationDigest",digest),new XElement("savedNpcAfter","saved-after")).ToString(SaveOptions.DisableFormatting);File.WriteAllText(path+".terminal",body);terminal=RebirthRemoteResourceDurableJournal.Digest(body);RebirthNpcTradeOwnerOriginalStore.Ack=terminal;
  File.WriteAllText(path+".closed",new XElement("npcTradeClosed",new XAttribute("version",1),new XAttribute("operation",original.Operation.ToString("N")),new XAttribute("originalDigest",original.Digest),new XAttribute("preparationDigest",digest),new XAttribute("terminalDigest",terminal)).ToString(SaveOptions.DisableFormatting));
 }
 static bool Local(out RebirthNpcTradeClosedHistory history)=>RebirthNpcTradeClosedHistory.TryReadLocal(source,original,out history);
 static bool Remote(out RebirthNpcTradeClosedHistory history)=>RebirthNpcTradeClosedHistory.TryRead(packet,new Platform.PlatformUserIdentifierAbs(),7,1,original.Operation,original.Digest,terminal,out history);
 public static void Main(){root=Path.Combine(Path.GetTempPath(),"rh-closed-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);GameIO.Root=root;try{
  Seed();Need(Local(out var history));Need(history.TrySaveSocialCompletion()&&RebirthNpcTradeService.SocialCalls==1);Need(Remote(out history));Need(history.Operation==original.Operation&&history.OriginalDigest==original.Digest&&history.TerminalDigest==terminal);
  foreach(string suffix in new[]{"",".grant",".terminal",".closed"}){Seed();File.Delete(path+suffix);Need(!Remote(out _)&&RebirthNpcTradeService.SocialCalls==0);}
  foreach(string suffix in new[]{"",".grant",".terminal",".closed"}){Seed();string saved=File.ReadAllText(path+suffix);File.WriteAllText(path+suffix,saved+" ");Need(!Remote(out _)&&File.ReadAllText(path+suffix)==saved+" ");}
  Seed();Need(!RebirthNpcTradeClosedHistory.TryRead(packet,new Platform.PlatformUserIdentifierAbs(),7,1,original.Operation,new string('0',64),terminal,out _));
  Seed();terminal=new string('0',64);Need(!Remote(out _));Seed();packet.Auth=false;Need(!Remote(out _));
  Seed();actor.world.Remote=true;Need(!Remote(out _));Seed();SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer=false;Need(!Remote(out _));
  Seed();RebirthNpcNetworkEpoch.Valid=false;Need(!Remote(out _));Seed();peer.loginDone=false;Need(!Remote(out _));Seed();peer.Account="foreign";Need(!Remote(out _));Seed();actor.Creation="foreign";Need(!Remote(out _));
  Seed();RebirthNpcInventoryPersistenceStore.Plan=false;Need(!Remote(out _));Seed();RebirthNpcInventoryPersistenceStore.After=false;Need(!Remote(out _));
  Seed();Need(Local(out history));RebirthNpcTradeService.Social=false;Need(!history.TrySaveSocialCompletion());RebirthNpcTradeService.Social=true;Need(history.TrySaveSocialCompletion());
  Seed();source.Phase=2;Need(!Local(out _));Seed();source.Step=3;Need(!Local(out _));Seed();source.Persist=false;Need(!Local(out _));Seed();source.Match=false;Need(!Local(out _));Seed();RebirthNpcTradeOwnerOriginalStore.Ack="wrong";Need(!Local(out _));
  Seed();RebirthNpcTradeService.HostPrepared.RetainedSource.Capture=false;Need(!Local(out _));Seed();RebirthNpcTradeService.HostPrepared.RetainedSource.History=false;Need(!Local(out _));
  Seed();Need(Local(out history));File.WriteAllText(path+".closed","changed");Need(!history.TrySaveSocialCompletion()&&RebirthNpcTradeService.SocialCalls==0&&File.ReadAllText(path+".closed")=="changed");
  Console.WriteLine("PASS "+checks+" complete production closed trade history assertions; native/social adapters excluded");
 }finally{if(Path.GetDirectoryName(root)!=Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar)||!Path.GetFileName(root).StartsWith("rh-closed-"))throw new InvalidDataException();Directory.Delete(root,true);}}
}
