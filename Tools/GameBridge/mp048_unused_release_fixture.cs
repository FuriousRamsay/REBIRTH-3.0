// Actual release method; explicit native hold/checkpoint/receipt/token adapters.
// These vectors certify branch requirements, never native response provenance.
class Buffs{internal Dictionary<string,float> Values=new Dictionary<string,float>();internal float GetCustomVar(string key)=>Values.TryGetValue(key,out var value)?value:0;}
class EntityPlayerLocal{internal Buffs Buffs=new Buffs();}
static class RebirthNpcTradeOwnerCheckpoint{internal static bool Saved=true;internal static string Marker(Guid id)=>"phase"+id;internal static bool TrySaveBefore(EntityPlayerLocal p,RebirthNpcTradeOriginal o)=>Saved;}
static class RebirthNpcTradeOwnerOriginalStore{internal static bool Durable=true;internal static bool TryRetainReceipt(EntityPlayerLocal p,RebirthNpcTradeOriginal o,string purpose,string receipt)=>Durable&&purpose=="unused";}
static class RebirthNpcInteractionNetworkClient{internal sealed class NativeTradeUnused{internal RebirthNpcTradeOriginal Original;internal bool Valid=true;internal string Receipt="receipt";internal bool Current()=>Valid;}}
static class RebirthNpcTradeOwnerExchange
{
 internal sealed class LocalUnusedBefore{internal RebirthNpcTradeOriginal Original;internal bool Valid=true;internal bool Current()=>Valid;}
 private sealed class Hold{internal EntityPlayerLocal Player;internal RebirthNpcTradeOriginal Original;internal object Grant,LocalGrant;internal int Progress;internal bool Entered,NeedsSave,Valid=true,Before=true;}
 private static Dictionary<Guid,Hold> holds=new Dictionary<Guid,Hold>();
 internal static string Step(Guid id)=>"step"+id;
 private static bool Current(Hold hold)=>hold.Valid;
 private static bool Prefix(Hold hold,int progress)=>progress==0&&hold.Before;
 // Disclosed native marker-clear/save/upload adapter. Full native behavior
 // belongs to the owner integration scenario, not this release branch cohort.
 private static bool ClearHeldStamp(Hold hold)=>ClearSaved;
 private static bool ClearSaved=true;
 // ACTUAL_UNUSED_RELEASE
 internal static void Run()
 {
  var original=new RebirthNpcTradeOriginal{Operation=Guid.NewGuid()};var player=new EntityPlayerLocal();
  player.Buffs.Values[RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation)]=1;
  var token=new RebirthNpcInteractionNetworkClient.NativeTradeUnused{Original=original};
  Hold Seed(){holds.Clear();ClearSaved=true;RebirthNpcTradeOwnerCheckpoint.Saved=RebirthNpcTradeOwnerOriginalStore.Durable=true;token.Valid=true;var h=new Hold{Player=player,Original=original};holds.Add(original.Operation,h);return h;}
  void Refused(){if(TryReleaseUnused(player,token)||!holds.ContainsKey(original.Operation))throw new Exception("Unused owner custody released without exact no-use proof");}
  var hold=Seed();hold.Grant=new object();Refused();
  hold=Seed();hold.LocalGrant=new object();Refused();
  hold=Seed();hold.Progress=1;Refused();
  hold=Seed();hold.Entered=true;Refused();
  hold=Seed();hold.NeedsSave=true;Refused();
  hold=Seed();hold.Valid=false;Refused();
  hold=Seed();hold.Before=false;Refused();
  Seed();token.Valid=false;Refused();
  Seed();RebirthNpcTradeOwnerCheckpoint.Saved=false;Refused();
  Seed();RebirthNpcTradeOwnerOriginalStore.Durable=false;Refused();
  Seed();player.Buffs.Values[Step(original.Operation)]=1;Refused();player.Buffs.Values[Step(original.Operation)]=0;
  Seed();player.Buffs.Values[RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation)]=2;Refused();player.Buffs.Values[RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation)]=1;
  Seed();ClearSaved=false;Refused();
  Seed();if(!TryReleaseUnused(player,token)||holds.Count!=0)throw new Exception("Exact untouched unused hold did not release");
  Console.WriteLine("14 actual unused owner-release branch assertions passed; native adapters not certified");
 }
}
