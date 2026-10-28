using System;using System.IO;using System.Collections.Generic;
// Whole actual local admission/permission partial; native file/policy, session,
// journal, NPC saved plan and HostGrant are explicit producer adapters.
class World{internal EntityPlayerLocal Player;internal string Id="world";internal bool Remote;internal WorldState worldState=new WorldState();internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal EntityPlayer GetEntity(int id)=>Player?.entityId==id?Player:null;internal class WorldState{internal string Guid="world";}}
class EntityPlayer{internal World world;internal int entityId=42,Died;internal bool Dead;internal string Creation="creation";internal bool IsDead()=>Dead;internal bool IsSpawned()=>true;}
class EntityPlayerLocal:EntityPlayer{}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class GameIO{internal static string Save=Path.Combine(Path.GetTempPath(),"rebirth-local-admission-fixture");internal static string GetSaveGameDir()=>Save;}
class ConnectionManager{internal bool IsServer=true;}
static class SingletonMonoBehaviour<T> where T:new(){internal static T Instance=new T();}
class PlayerDataFile{}
class Identity{internal string CanonicalId="owner";}
static class RebirthStablePlayerIdentity{internal static bool TryFromLocalPlatform(out Identity owner){owner=new Identity();return true;}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer player)=>player.Creation;}
static class RebirthGearNativePlayerFile{internal static PlayerDataFile Saved=new PlayerDataFile();internal static bool TryRead(Identity owner,out PlayerDataFile saved){saved=Saved;return saved!=null;}}
class RebirthNpcInteractionContext{internal string SessionKey="session",ActorId="actor";internal Guid NpcId;}
enum RebirthNpcInteractionAction{Trade}
enum RebirthNpcInteractionResult{Allowed,Rejected}
static class RebirthNpcInteractionSessionService{internal static bool Allowed=true;internal static RebirthNpcInteractionContext Context;internal static RebirthNpcInteractionResult Authorize(string session,string actor,RebirthNpcInteractionAction action,out RebirthNpcInteractionContext context){context=Context;return Allowed?RebirthNpcInteractionResult.Allowed:RebirthNpcInteractionResult.Rejected;}}
class RebirthNpcTradeQuote{internal Guid QuoteId=Guid.NewGuid(),NpcId=Guid.NewGuid();internal string ActorId="actor";internal long ExpiresClockTicks=DateTime.UtcNow.AddMinutes(1).Ticks;internal ulong RecoveryGeneration=1;}
class RebirthNpcTradeOriginal{internal RebirthNpcTradeQuote Quote=new RebirthNpcTradeQuote();internal Guid Operation=>Quote.QuoteId;internal string World="world",Creation="creation",Account="owner";internal int NativeId=42,Deaths;internal RebirthNpcTradeQuote CopyQuote()=>Quote;internal bool MatchesQuote(RebirthNpcTradeQuote quote)=>ReferenceEquals(Quote,quote);}
static class RebirthNpcRecoveryGeneration{internal static ulong RequireCurrent()=>1;}
static class RebirthNpcTradeReach{internal static bool Allowed=true;internal static bool CanBegin(EntityPlayer actor,Guid npc)=>Allowed;}
static class RebirthNpcNetworkEpoch{internal static uint Epoch=7;internal static uint GetServerEpoch()=>Epoch;}
static class RebirthNpcTradeOwnerOriginalStore{internal static bool Retained=true;internal static bool IsRetained(EntityPlayerLocal player,RebirthNpcTradeOriginal original)=>Retained;}
static class RebirthNpcTradeOwnerCheckpoint{internal static int Step,Phase=1;internal static bool TryReadRetainedStage(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out int step,out int phase){step=Step;phase=Phase;return true;}}
static class RebirthNpcTradePrefix{internal static int Changes(RebirthNpcTradeOriginal original)=>2;}
class RebirthNpcTradeNpcPlan{internal static bool TryCreate(RebirthNpcTradeOriginal original,object snapshot,out RebirthNpcTradeNpcPlan plan){plan=new RebirthNpcTradeNpcPlan();return true;}}
static class RebirthNpcInventoryTransactionService{internal static object GetSnapshot(Guid npc)=>new object();}
static class RebirthNpcInventoryPersistenceStore{internal static bool Saved=true;internal static string CaptureTradeSaved(RebirthNpcTradeOriginal original,RebirthNpcTradeNpcPlan plan,bool after)=>Saved?"saved-npc":null;}
static class RebirthNpcTradeLocalOwnerLoad{internal class Source{internal EntityPlayerLocal Player;internal bool Valid=true;internal int Step,Phase=1;internal bool Matches(RebirthNpcTradeOriginal original)=>Valid;internal bool PersistCurrent(RebirthNpcTradeOriginal original)=>Valid;internal bool TryStage(out int step,out int phase){step=Step;phase=Phase;return Valid;}}}
public static partial class RebirthNpcTradeService
{
 private static object Sync=new object();private static Dictionary<Guid,QuoteRecord> Quotes=new Dictionary<Guid,QuoteRecord>();
 private class QuoteRecord{internal RebirthNpcTradeQuote Quote;internal bool Consumed;internal byte Resolution;internal object OwnerUnused,Issued;internal HostPrepared OwnerPreparation;}
 private static bool TradeScope()=>true;
 internal static class HostQuote{internal static bool Entered=true;internal static bool TryReadEntered(RebirthNpcTradeOriginal original,out object issued){issued=new object();return Entered;}}
 internal sealed partial class OwnerAdmission
 {
  internal readonly RebirthNpcTradeOriginal Original;internal readonly RebirthNpcTradeNpcPlan NpcPlan;internal readonly string SavedNpcBefore;
  private readonly World world;private readonly EntityPlayer actor;private readonly ConnectionManager manager;private readonly PlayerDataFile accepted;
  private readonly string session,actorKey,directory;private readonly uint epoch;private readonly int nativeId;private readonly bool reattached,local;private readonly QuoteRecord quote;
  internal static bool BeforeReady=true,AfterReady=true;
  private OwnerAdmission(RebirthNpcTradeOriginal original,RebirthNpcTradeNpcPlan plan,string saved,World world,EntityPlayer actor,object peer,object transport,ConnectionManager manager,PlayerDataFile accepted,string session,string actorKey,uint connection,uint epoch,QuoteRecord quote,bool reattached=false,bool local=false)
  {Original=original;NpcPlan=plan;SavedNpcBefore=saved;this.world=world;this.actor=actor;this.manager=manager;this.accepted=accepted;this.session=session;this.actorKey=actorKey;this.epoch=epoch;this.quote=quote;nativeId=actor.entityId;this.reattached=reattached;this.local=local;directory=Path.GetFullPath(GameIO.GetSaveGameDir());}
  private static bool Before(PlayerDataFile saved,RebirthNpcTradeOriginal original,EntityPlayer actor)=>BeforeReady;
  private static bool After(PlayerDataFile saved,RebirthNpcTradeOriginal original,EntityPlayer actor,float phase)=>AfterReady;
  private static bool RetainedPrefix(PlayerDataFile saved,RebirthNpcTradeOriginal original,EntityPlayer actor)=>BeforeReady;
  internal bool Current()=>CurrentLocal(null,false,null,2);
  internal bool CurrentReserved(HostPrepared prepared)=>CurrentLocal(prepared,false,null,2);
  internal sealed partial class SavedAfter{private readonly OwnerAdmission admission;private readonly HostPrepared.HostGrant grant;private readonly PlayerDataFile saved;private SavedAfter(OwnerAdmission admission,HostPrepared.HostGrant grant,PlayerDataFile saved){this.admission=admission;this.grant=grant;this.saved=saved;}internal bool Current()=>admission.CurrentLocal(grant.Prepared,true,saved,2);}
 }
 internal class HostPrepared
 {
  internal RebirthNpcTradeOriginal Original;internal bool GrantRetained=true;internal HostClosed ClosedRecord;
  internal bool TryIssueGrant(out HostGrant grant){GrantRetained=true;grant=HostGrant.Create(this);return true;}
  internal class RetainedSource{internal RebirthNpcTradeOriginal Original;internal RebirthNpcTradeNpcPlan NpcPlan=new RebirthNpcTradeNpcPlan();internal string SavedNpcBefore="saved-npc";internal bool Retained=true;internal bool IsRetained()=>Retained;}
  internal class HostGrant{internal HostPrepared Prepared;internal static HostGrant Create(HostPrepared p)=>new HostGrant{Prepared=p};internal bool IsRetained()=>Prepared.GrantRetained;}
 }
 internal class HostClosed{internal HostPrepared Prepared;internal bool CanRetireFromQuote=true;}
 internal static void Run()
 {
  int checks=0;void Check(bool value){checks++;if(!value)throw new Exception("Local admission assertion "+checks);}
  var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;
  var original=new RebirthNpcTradeOriginal();var context=new RebirthNpcInteractionContext{NpcId=original.Quote.NpcId};RebirthNpcInteractionSessionService.Context=context;
  var record=new QuoteRecord{Quote=original.Quote};Quotes.Add(original.Operation,record);
  ThreadManager.Main=false;Check(!OwnerAdmission.TryCaptureLocal(context,original,out _));ThreadManager.Main=true;
  RebirthNpcInteractionSessionService.Allowed=false;Check(!OwnerAdmission.TryCaptureLocal(context,original,out _));RebirthNpcInteractionSessionService.Allowed=true;
  original.Quote.ExpiresClockTicks=0;Check(!OwnerAdmission.TryCaptureLocal(context,original,out _));original.Quote.ExpiresClockTicks=DateTime.UtcNow.AddMinutes(1).Ticks;
  OwnerAdmission.BeforeReady=false;Check(!OwnerAdmission.TryCaptureLocal(context,original,out _));OwnerAdmission.BeforeReady=true;
  RebirthNpcTradeOwnerCheckpoint.Step=2;Check(!OwnerAdmission.TryCaptureLocal(context,original,out _));RebirthNpcTradeOwnerCheckpoint.Step=0;
  RebirthNpcTradeReach.Allowed=false;Check(!OwnerAdmission.TryCaptureLocal(context,original,out _));RebirthNpcTradeReach.Allowed=true;
  Check(OwnerAdmission.TryCaptureLocal(context,original,out var admission));
  var prepared=new HostPrepared{Original=original};record.Consumed=true;record.OwnerPreparation=prepared;
  Check(OwnerAdmission.LocalPermission.TryCapture(admission,prepared,out var permission)&&permission.Current());
  RebirthNpcTradeReach.Allowed=false;Check(permission.Current());RebirthNpcTradeReach.Allowed=true;
  var other=new HostPrepared{Original=new RebirthNpcTradeOriginal()};Check(!OwnerAdmission.LocalPermission.TryCapture(admission,other,out _));
  RebirthNpcTradeOwnerCheckpoint.Phase=2;RebirthNpcTradeOwnerCheckpoint.Step=4;Check(OwnerAdmission.SavedAfter.TryCaptureLocal(permission,out _));
  RebirthNpcTradeOwnerCheckpoint.Phase=3;Check(permission.AcknowledgementCurrent());
  RebirthNpcNetworkEpoch.Epoch++;Check(!permission.Current()&&!permission.AcknowledgementCurrent());RebirthNpcNetworkEpoch.Epoch--;
  record.Resolution=1;Check(!permission.Current());var closed=new HostClosed{Prepared=prepared};prepared.ClosedRecord=closed;Check(permission.ClosedCurrent(closed));record.Resolution=0;
  RebirthNpcTradeOwnerCheckpoint.Phase=1;RebirthNpcTradeOwnerCheckpoint.Step=2;
  var source=new RebirthNpcTradeLocalOwnerLoad.Source{Player=player,Phase=1,Step=2};var retained=new HostPrepared.RetainedSource{Original=original};
  HostQuote.Entered=false;Check(!OwnerAdmission.TryCaptureLocalRetained(source,retained,out _));HostQuote.Entered=true;
  Check(OwnerAdmission.TryCaptureLocalRetained(source,retained,out var reattached));
  prepared.GrantRetained=false;Check(!OwnerAdmission.LocalPermission.TryCaptureRetained(reattached,prepared,source,out _));
  prepared.GrantRetained=true;Check(OwnerAdmission.LocalPermission.TryCaptureRetained(reattached,prepared,source,out var restored)&&restored.Current());
  source.Valid=false;Check(!OwnerAdmission.LocalPermission.TryCaptureRetained(reattached,prepared,source,out _));
  Console.WriteLine(checks+" actual local admission assertions passed; native/session/journal/grant producers NOT certified");
 }
}
class Program{static void Main()=>RebirthNpcTradeService.Run();}
