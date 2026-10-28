// Actual Quote/stock/serialization members injected. Explicit endpoint/runtime/
// offer/scope adapters; owner replica reads throw instead of faking funds.
interface IRebirthNpcExternalInventoryEndpoint {}
static class RebirthNpcTradeReach{internal static bool Allowed=true;internal static bool CanBegin(string actor,RebirthNpcStableId npc)=>Allowed;}
class QuoteEndpoint:IRebirthNpcExternalInventoryEndpoint {}
static class RebirthNpcPlayerInventoryEndpointService {public static bool TryGet(string actor,out string endpoint){endpoint="player:1";return actor=="actor";}}
static class RebirthNpcExternalInventoryEndpointRegistry
{
 public static int ReplicaReads;
 public static bool TryResolve(string endpoint,out IRebirthNpcExternalInventoryEndpoint value){value=new QuoteEndpoint();return endpoint=="player:1";}
 public static int GetNegotiatedQuantity(string endpoint,string item,int count,bool debit){ReplicaReads++;throw new Exception("server owner replica read");}
}
class RebirthNpcRuntimeState {public string ProfileId="fixture";}
static class RebirthNpcRuntimeRegistry
{
 public static bool TryGetEntityId(RebirthNpcStableId npc,out int id){id=1;return true;}
 public static bool TryGet(int id,out RebirthNpcRuntimeState runtime){runtime=new RebirthNpcRuntimeState();return true;}
}
class RebirthNpcTradeOffer {public int MaximumQuantity=10,BuyUnitPrice=3,SellUnitPrice=3;public string CurrencyItemKey="currency";}
static class RebirthNpcTradeOfferRegistry {public static bool TryResolve(string profile,string item,out RebirthNpcTradeOffer offer){offer=new RebirthNpcTradeOffer();return item=="goods";}}
static class RebirthNpcInventoryTransactionService {public static RebirthNpcInventorySnapshot Snapshot;public static RebirthNpcInventorySnapshot GetSnapshot(RebirthNpcStableId npc)=>Snapshot;}
static class RebirthNpcRecoveryGeneration
{
 public static bool Known=true;public static ulong Generation=1;
 public static ulong RequireCurrent()=>Generation;
 public static bool TryReadObservedGeneration(TradeWorld world,out ulong generation){generation=Generation;return Known;}
}
class RebirthNpcInteractionContext {public string ActorId="actor";public RebirthNpcStableId NpcId=new RebirthNpcStableId();}
class RebirthNpcInteractionCommandResponse {public bool Accepted;public string Detail,Payload;}
static class QuoteSourceFixture
{
 private sealed class QuoteRecord {public RebirthNpcTradeQuote Quote;public object Issued;}
 // Durable issuance has its own actual-source cohort; this adapter does not
 // certify native account, actor or journal publication from Quote alone.
 private static class HostQuote{internal static bool TryIssue(RebirthNpcTradeQuote quote,out object issued){issued=new object();return true;}}
 private const int MaxQuotes=512;private static readonly object Sync=new object();private static long quoted;
 private static readonly Dictionary<Guid,QuoteRecord> Quotes=new Dictionary<Guid,QuoteRecord>();private static readonly List<Guid> QuoteOrder=new List<Guid>();
 private static void PruneExpiredLocked(){}
 private static bool TryGet(Dictionary<string,string> values,string key,out string value)=>values.TryGetValue(key,out value)&&!string.IsNullOrWhiteSpace(value);
 private static RebirthNpcInteractionCommandResponse Reject(string text)=>new RebirthNpcInteractionCommandResponse{Detail=text};
 private static RebirthNpcInteractionCommandResponse Accept(string text,string payload)=>new RebirthNpcInteractionCommandResponse{Accepted=true,Detail=text,Payload=payload};
 private static string Escape(string text)=>(text??string.Empty).Replace("%","%25").Replace(";","%3B").Replace("=","%3D").Replace(",","%2C").Replace(":","%3A");
// ACTUAL_TRADE_QUOTE
// ACTUAL_TRADE_AVAILABLE
// ACTUAL_TRADE_SERIALIZE
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 internal static void Run()
 {
  var context=new RebirthNpcInteractionContext();var snapshot=new RebirthNpcInventorySnapshot{NpcId=context.NpcId,Revision=1,
   Quantities=new Dictionary<string,int>{{"goods",10},{"currency",20}},Reservations=new Dictionary<string,int>()};
  RebirthNpcInventoryTransactionService.Snapshot=snapshot;
  var request=new Dictionary<string,string>{{"endpoint","player:1"},{"item","goods"},{"direction","BuyFromNpc"},{"quantity","2"}};
  RebirthNpcTradeReach.Allowed=false;Need(!Quote(context,request).Accepted,"fresh distant trade quote admitted");RebirthNpcTradeReach.Allowed=true;
  var response=Quote(context,request);Need(response.Accepted&&RebirthNpcExternalInventoryEndpointRegistry.ReplicaReads==0,"quote depends on server owner replica funds");
  var wire=response.Payload.Split(';').Select(part=>part.Split(new[]{'='},2)).ToDictionary(part=>part[0],part=>part[1]);
  Need(RebirthNpcTradeQuoteWire.TryRead(wire,"actor","npc","player:1",out var issued)&&issued.Quantity==2&&issued.TotalPrice==6&&issued.RecoveryGeneration==1,"actual authority serialization lost complete quote binding");
  snapshot.Reservations["goods"]=2;snapshot.NativeStacks=new RebirthNpcNativeStackSet{Count=7};Need(!Quote(context,request).Accepted,"reserved/native rich NPC stock quoted as free");
  snapshot.Reservations.Clear();snapshot.NativeStacks=null;request["direction"]="SellToNpc";
  Need(Quote(context,request).Accepted&&RebirthNpcExternalInventoryEndpointRegistry.ReplicaReads==0,"sell quote depends on server owner items");
  snapshot.Reservations["currency"]=18;Need(!Quote(context,request).Accepted,"reserved NPC currency quoted as free");snapshot.Reservations.Clear();
  request["endpoint"]="player:2";Need(!Quote(context,request).Accepted,"foreign quoted endpoint admitted");request["endpoint"]="player:1";
  request["quantity"]="2147483648";Need(!Quote(context,request).Accepted,"quantity overflow accepted");
 }
}
