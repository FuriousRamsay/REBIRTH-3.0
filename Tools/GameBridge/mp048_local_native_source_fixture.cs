using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;
// Whole actual local native observer. Native serialization, marker decoding,
// platform identity, checkpoint policy and inventory codec are explicit adapters.
enum StreamModeRead{Persistency,FromServer}
class PooledBinaryReader{internal Stream BaseStream;}
class PlayerDataFile{internal const uint cFileVersion=99;internal const string EXT="ttp";internal byte[] bagData,inventoryData,buffData;internal bool bDead;internal int deaths;}
class World{internal EntityPlayerLocal Player;internal bool Remote;internal WorldState worldState=new WorldState();internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;internal EntityPlayer GetEntity(int id)=>Player?.entityId==id?Player:null;internal class WorldState{internal string Guid="world";}}
class EntityPlayer{internal World world;internal int entityId=42,Died;internal bool Dead,Spawned=true;internal string Creation="creation";internal Inventory bag=new Inventory(),inventory=new Inventory();internal Buffs Buffs=new Buffs();internal bool IsDead()=>Dead;internal bool IsSpawned()=>Spawned;}
class EntityPlayerLocal:EntityPlayer{}
class Inventory{internal Grid ItemGrid=new Grid();internal class Grid{internal byte[] items;}}
class Buffs{internal Dictionary<string,float> Values=new Dictionary<string,float>();internal float GetCustomVar(string key)=>Values.TryGetValue(key,out var value)?value:0;}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;internal int Saves;internal void SaveLocalPlayerData(){Saves++;}}
static class ThreadManager{internal static bool Main=true;internal static bool IsMainThread()=>Main;}
static class RebirthNpcLifecycle{internal static Func<bool> Boundary=()=>true;internal static bool PrepareNativeEpochBoundary()=>Boundary();}
// Lifecycle consumers are observation-only adapters. Native lineage is composed
// from both actual local owner-load and NativeReentry production partials.
static class RebirthDogLifecycleService{internal static int Observations;internal static void ObserveNativeOwnerReentries(){Observations++;}}
static class RebirthNpcSaveQuarantineScope{internal static string ScopeDirectory(string value)=>Path.GetFullPath(value);internal static bool SameDirectory(string a,string b)=>string.Equals(ScopeDirectory(a),ScopeDirectory(b),StringComparison.OrdinalIgnoreCase);}
static class RebirthNpcSaveQuarantineBundle{internal static int Observations;internal static void ObserveServerTradeNativeReentry(RebirthNpcTradeLocalOwnerLoad.NativeReentry source){if(source?.Current()==true)Observations++;}}
static class GameIO{internal static string Save=Path.Combine(Path.GetTempPath(),"rebirth-local-source-fixture");internal static string GetSaveGameDir()=>Save;internal static string GetPlayerDataDir()=>Path.Combine(Save,"Player");}
class Owner{internal string CanonicalId="owner";}
static class RebirthStablePlayerIdentity{internal static bool Available=true;internal static bool TryFromLocalPlatform(out Owner owner){owner=new Owner();return Available;}}
static class RebirthProductiveEarnedUiOwnerFile{internal static bool DeferredNativeOwnerPathSafe(string path)=>true;}
static class RebirthProductiveEarnedObservationBudget{internal static int Active;internal static bool TryRetainDetached(long count,out IDisposable memory){Active++;memory=new Reservation();return true;}class Reservation:IDisposable{public void Dispose(){Active--;}}}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayer player)=>player.Creation;}
static class RebirthCamperInventorySnapshot{internal static byte[] Capture(byte[] items)=>items.ToArray();}
static class RebirthGearPlayerDataInventory{internal static byte[] ReadSlots(PlayerDataFile data,bool bag)=>bag?data.bagData:data.inventoryData;}
static class RebirthGearNativePlayerFile{internal static PlayerDataFile Saved;internal static bool TryRead(Owner owner,out PlayerDataFile saved){saved=Saved;return saved!=null;}}
static class RebirthRemoteResourcePlacedStationSavedReceipt
{
 internal static byte[] Encode(Dictionary<string,float> values)=>Encoding.UTF8.GetBytes(string.Join(";",values.Select(p=>p.Key+"="+p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))));
 internal static Dictionary<string,float> Decode(byte[] data)=>Encoding.UTF8.GetString(data).Split(';').Select(s=>s.Split('=')).ToDictionary(p=>p[0],p=>float.Parse(p[1],System.Globalization.CultureInfo.InvariantCulture));
 internal static bool TryRead(byte[] data,string key,out float value,out bool present){var map=Decode(data);present=map.TryGetValue(key,out value);return true;}
 internal static bool TryRead(PlayerDataFile data,string key,out float value,out bool present)=>TryRead(data.buffData,key,out value,out present);
}
class RebirthNpcTradeOriginal{internal Guid Operation;internal string Image="immutable-original",Account="owner",Creation="creation",World="world";internal int Deaths;}
static class RebirthNpcTradeOwnerOriginalStore{internal static bool Retained=true;internal static RebirthNpcTradeOriginal Original;internal static bool IsRetained(EntityPlayerLocal player,RebirthNpcTradeOriginal original)=>Retained&&ReferenceEquals(original,Original);internal static bool TryReadRetained(EntityPlayerLocal player,Guid operation,out RebirthNpcTradeOriginal original){original=Original;return original?.Operation==operation&&IsRetained(player,original);}}
static class RebirthNpcTradePrefix{internal static int Changes(RebirthNpcTradeOriginal original)=>2;internal static bool Matches(RebirthNpcTradeOriginal original,byte[] bag,byte[] belt,int step)=>bag.SequenceEqual(new byte[]{1,2})&&belt.SequenceEqual(new byte[]{3});}
static class RebirthNpcTradeOwnerCheckpoint
{
 internal const string Held="held",HeldLife="life";internal static string Marker(Guid id)=>"phase_"+id.ToString("N");internal static string HoldMarker(Guid id)=>"hold_"+id.ToString("N");
 internal static bool TryReadRetainedStage(EntityPlayerLocal player,RebirthNpcTradeOriginal original,out int step,out int phase){var map=RebirthRemoteResourcePlacedStationSavedReceipt.Decode(RebirthGearNativePlayerFile.Saved.buffData);step=(int)map[RebirthNpcTradeOwnerExchange.Step(original.Operation)];phase=(int)map[Marker(original.Operation)];return true;}
}
static class RebirthNpcTradeOwnerExchange
{
 internal static string Step(Guid id)=>"step_"+id.ToString("N");
 internal class Release{internal bool Current()=>false;internal bool MatchesNative(Guid id,string account,string creation,string world,int deaths)=>false;}
}
static class RebirthNpcTradeOwnerLoad{internal static bool TryOperation(byte[] data,out Guid id){var keys=RebirthRemoteResourcePlacedStationSavedReceipt.Decode(data).Where(p=>p.Key.StartsWith("hold_")&&p.Value==1).ToArray();id=Guid.Empty;return keys.Length==1&&Guid.TryParseExact(keys[0].Key.Substring(5),"N",out id);}}
static class RebirthNpcTradeService
{
 internal static bool ResumeLocalLoaded(RebirthNpcTradeLocalOwnerLoad.Source source)=>false;
 internal class OwnerAdmission{internal class LocalPermission{internal RebirthNpcTradeOriginal Original;internal bool Allowed=true;internal bool Current()=>Allowed;}}
}
class RebirthNpcSaveQuarantineDomain{internal Func<bool> Current;internal Action Detach,Rollback;internal RebirthNpcSaveQuarantineDomain(string name,System.Xml.XmlDocument image,Func<bool> current,Action detach,Action rollback){Current=current;Detach=detach;Rollback=rollback;}}
class Program
{
 static int checks;static void Check(bool value){checks++;if(!value)throw new Exception("Local native source assertion "+checks);}
 static void Main()
 {
  Guid id=Guid.NewGuid();var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;
  var marks=new Dictionary<string,float>{{"held",1},{"life",0},{RebirthNpcTradeOwnerCheckpoint.HoldMarker(id),1},{RebirthNpcTradeOwnerCheckpoint.Marker(id),1},{RebirthNpcTradeOwnerExchange.Step(id),0}};
  player.Buffs.Values=marks;player.bag.ItemGrid.items=new byte[]{1,2};player.inventory.ItemGrid.items=new byte[]{3};
  var data=new PlayerDataFile{bagData=new byte[]{1,2},inventoryData=new byte[]{3},buffData=RebirthRemoteResourcePlacedStationSavedReceipt.Encode(marks)};RebirthGearNativePlayerFile.Saved=data;
  var original=RebirthNpcTradeOwnerOriginalStore.Original=new RebirthNpcTradeOriginal{Operation=id};
  string directory=GameIO.GetPlayerDataDir();
  Check(RebirthNpcTradeLocalOwnerLoad.EnterLoad(data,directory,"foreign",true)==null);
  Check(RebirthNpcTradeLocalOwnerLoad.EnterLoad(data,directory,"owner",false)==null);
  world.Remote=true;Check(RebirthNpcTradeLocalOwnerLoad.EnterLoad(data,directory,"owner",true)==null);world.Remote=false;
  RebirthNpcLifecycle.Boundary=()=>false;Check(RebirthNpcTradeLocalOwnerLoad.EnterLoad(data,directory,"owner",true)==null&&RebirthProductiveEarnedObservationBudget.Active==0);
  RebirthNpcLifecycle.Boundary=()=>{GameManager.Instance.World=new World();return true;};Check(RebirthNpcTradeLocalOwnerLoad.EnterLoad(data,directory,"owner",true)==null&&RebirthProductiveEarnedObservationBudget.Active==0);GameManager.Instance.World=world;
  RebirthNpcLifecycle.Boundary=()=>true;
  var load=RebirthNpcTradeLocalOwnerLoad.EnterLoad(data,directory,"owner",true);Check(load!=null&&RebirthProductiveEarnedObservationBudget.Active==1);
  Check(RebirthNpcTradeLocalOwnerLoad.EnterLoad(data,directory,"owner",true)==null);
  using var stream=new MemoryStream(new byte[8]);var reader=new PooledBinaryReader{BaseStream=stream};
  RebirthNpcTradeLocalOwnerLoad.ObserveOpen(Path.Combine(directory,"owner.ttp"),stream,true);stream.Position=5;
  Check(RebirthNpcTradeLocalOwnerLoad.EnterRead(data,reader,98,StreamModeRead.Persistency,true)==null);
  Check(RebirthNpcTradeLocalOwnerLoad.EnterRead(data,reader,99,StreamModeRead.FromServer,true)==null);
  Check(RebirthNpcTradeLocalOwnerLoad.EnterRead(data,new PooledBinaryReader{BaseStream=new MemoryStream()},99,StreamModeRead.Persistency,true)==null);
  var read=RebirthNpcTradeLocalOwnerLoad.EnterRead(data,reader,99,StreamModeRead.Persistency,true);Check(ReferenceEquals(load,read));
  stream.Position=stream.Length;RebirthNpcTradeLocalOwnerLoad.ObserveRead(true,read);RebirthNpcTradeLocalOwnerLoad.ObserveLoad(data,true,load);RebirthNpcTradeLocalOwnerLoad.EndLoad(load,null);
  Check(RebirthNpcTradeLocalOwnerLoad.Source.CaptureCurrent()==null);
  Check(RebirthNpcTradeLocalOwnerLoad.EnterLoad(data,directory,"owner",true)==null&&RebirthProductiveEarnedObservationBudget.Active==1);
  Check(RebirthNpcTradeLocalOwnerLoad.EnterHydration(data,player,StreamModeRead.FromServer,true)==null);
  var hydration=RebirthNpcTradeLocalOwnerLoad.EnterHydration(data,player,StreamModeRead.Persistency,true);Check(hydration!=null);
  RebirthNpcTradeLocalOwnerLoad.ObserveHydration(data,player,true,hydration);RebirthNpcTradeLocalOwnerLoad.EndHydration(hydration,false);
  var source=RebirthNpcTradeLocalOwnerLoad.Source.CaptureCurrent();Check(source!=null&&source.Operation==id&&source.Current(player));
  Check(RebirthNpcTradeLocalOwnerLoad.EnterLoad(data,directory,"owner",true)==null&&source.Current(player)&&RebirthProductiveEarnedObservationBudget.Active==1);
  Check(source.TryStage(out int step,out int phase)&&step==0&&phase==1&&source.Matches(original));
  var reentry=RebirthNpcTradeLocalOwnerLoad.NativeReentry.Capture(source);Check(reentry!=null&&reentry.Current()&&reentry.Matches(original));
  Check(!reentry.Matches(new RebirthNpcTradeOriginal{Image="foreign-original"}));
  RebirthNpcTradeLocalOwnerLoad.Update();Check(RebirthDogLifecycleService.Observations==1&&RebirthNpcSaveQuarantineBundle.Observations==1);
  player.bag.ItemGrid.items[0]=9;Check(!source.Current(player));player.bag.ItemGrid.items[0]=1;
  player.Died=1;Check(!source.Current(player)&&!reentry.Current());player.Died=0;
  player.Creation="other";Check(!source.Current(player));player.Creation="creation";
  string save=GameIO.Save;GameIO.Save=save+"other";Check(!source.Current(player));GameIO.Save=save;
  Check(source.PersistCurrent(original)&&GameManager.Instance.Saves==1);
  var quarantine=RebirthNpcTradeLocalOwnerLoad.PrepareQuarantine(world);Check(quarantine.Current());quarantine.Detach();Check(!source.Current(player));quarantine.Rollback();Check(source.Current(player));
  Check(!source.Transfer(new RebirthNpcTradeService.OwnerAdmission.LocalPermission{Original=original,Allowed=false}));
  Check(source.Transfer(new RebirthNpcTradeService.OwnerAdmission.LocalPermission{Original=original})&&RebirthNpcTradeLocalOwnerLoad.Source.CaptureCurrent()==null&&!reentry.Current()&&RebirthProductiveEarnedObservationBudget.Active==0);
  Console.WriteLine(checks+" actual local native observer assertions passed; native serialization/checkpoint/permission adapters NOT certified");
 }
}
