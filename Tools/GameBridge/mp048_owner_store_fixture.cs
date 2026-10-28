// Explicit native owner/world/path adapters. This cohort exercises the whole
// production original store, not native persistence or authenticated transport.
class TradeWorldState {public string Guid=System.Guid.NewGuid().ToString();}
class TradeGrid {public ItemStack[] items;}
class TradeBag {public TradeGrid ItemGrid=new TradeGrid();}
class TradeWorld
{
 public TradeWorldState worldState=new TradeWorldState();public EntityPlayerLocal Player;
 public bool Remote;public bool IsRemote()=>Remote;
 public EntityPlayerLocal GetPrimaryPlayer()=>Player;public EntityPlayerLocal GetEntity(int id)=>Player!=null&&Player.entityId==id?Player:null;
}
class EntityPlayer{}
class EntityPlayerLocal:EntityPlayer
{
 public TradeWorld world;public int entityId=1,Died;public TradeBag bag=new TradeBag(),inventory=new TradeBag();public TradeBuffs Buffs=new TradeBuffs();public bool IsDead()=>false;
}
class TradeBuffs {public Dictionary<string,float> CVars=new Dictionary<string,float>();public void SetCustomVar(string key,float value,bool sync){CVars[key]=value;}public float GetCustomVar(string key)=>CVars.TryGetValue(key,out var value)?value:0;}
class PlayerDataFile {public int id,deaths;public bool bDead;public ItemStack[] Bag,Belt;public Dictionary<string,float> Markers;}
class GameManager
{
 public static GameManager Instance=new GameManager();public TradeWorld World;public int SaveCalls;public bool SaveAllowed=true;
 public void SaveLocalPlayerData()
 {
  SaveCalls++;if(!SaveAllowed)return;var player=World.Player;
  RebirthGearNativePlayerFile.Saved=new PlayerDataFile{id=player.entityId,deaths=player.Died,Bag=player.bag.ItemGrid.items.Select(cell=>cell.Clone()).ToArray(),
   Belt=player.inventory.ItemGrid.items.Select(cell=>cell.Clone()).ToArray(),Markers=new Dictionary<string,float>(player.Buffs.CVars)};
 }
}
static class RebirthGearNativePlayerFile {public static PlayerDataFile Saved;public static bool TryRead(RebirthStablePlayerIdentity identity,out PlayerDataFile file){file=Saved;return file!=null;}}
static class RebirthGearPlayerDataInventory {public static ItemStack[] ReadSlots(PlayerDataFile file,bool bag)=>(bag?file.Bag:file.Belt).Select(cell=>cell.Clone()).ToArray();}
static class RebirthRemoteResourcePlacedStationSavedReceipt
{
 public static bool TryRead(PlayerDataFile file,string key,out float value,out bool present){present=file.Markers.TryGetValue(key,out value);return true;}
}
static class GameIO {public static string Directory;public static string GetPlayerDataDir()=>Directory;}
class RebirthStablePlayerIdentity
{
 public string CanonicalId="owner",StorageKey=new string('a',64);
 public static bool TryFromLocalPlatform(out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}
}
static class RebirthCamperCharacterAccess {public static string CurrentCreation(EntityPlayerLocal player)=>"creation";}
static class RebirthToolbeltCapacity {public static int GetOwnedSlotCount(EntityPlayerLocal player,int length)=>length;}
static class RebirthVehicleNativePolicy {public static int Reads;public static bool IsLocked(EntityPlayerLocal player,bool bag,int slot){Reads++;return false;}}
// Explicit native saved-grid policy adapter; no host admission capability.
static class RebirthNpcTradeService{internal static class OwnerAdmission{internal static bool Policy=true;internal static bool MatchesNativePolicy(PlayerDataFile data,RebirthNpcTradeOriginal original,EntityPlayerLocal player)=>Policy;}}
static class RebirthNpcTradeOwnerExchange{internal static string Step(Guid operation)=>"step_"+operation.ToString("N");}
// ACTUAL_TRADE_OWNER_STORE
// ACTUAL_TRADE_OWNER_CHECKPOINT
static class StoreFixture
{
 static void Need(bool value,string message){if(!value)throw new Exception(message);}
 // Reflection invokes only the actual private capacity classifier; fixture
 // files below are disclosed bookkeeping seeds, never native close authority.
 static bool Capacity(string directory)=>(bool)typeof(RebirthNpcTradeOwnerOriginalStore).GetMethod("HasOriginalCapacity",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{directory});
 static void RetainedStages(EntityPlayerLocal player,RebirthNpcTradeOriginal original)
 {
  string marker=RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation),counter=RebirthNpcTradeOwnerExchange.Step(original.Operation);
  player.bag.ItemGrid.items=original.CopySlots(true,false);player.inventory.ItemGrid.items=original.CopySlots(false,false);
  player.Buffs.CVars[marker]=1;player.Buffs.CVars[counter]=0;GameManager.Instance.SaveLocalPlayerData();
  Need(RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out int step,out int phase)&&step==0&&phase==1,"saved native Before did not qualify retained stage0");
  var audit=RebirthNpcTradeOwnerOriginalStore.CaptureAuditState(player,original.Operation);
  Need((bool)audit.GetType().GetProperty("beforeMatches").GetValue(audit)&&
       (bool)audit.GetType().GetProperty("savedStage").GetValue(audit),"readonly original audit lost exact saved Before");
  player.bag.ItemGrid.items=original.CopySlots(true,true);player.inventory.ItemGrid.items=original.CopySlots(false,true);
  player.Buffs.CVars[marker]=2;player.Buffs.CVars[counter]=RebirthNpcTradePrefix.Changes(original)*2;GameManager.Instance.SaveLocalPlayerData();
  Need(RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out step,out phase)&&phase==2,"exact saved native After did not qualify");
  audit=RebirthNpcTradeOwnerOriginalStore.CaptureAuditState(player,original.Operation);
  Need((bool)audit.GetType().GetProperty("afterMatches").GetValue(audit)&&
       (bool)audit.GetType().GetProperty("savedStage").GetValue(audit),"readonly original audit lost exact saved After");
  RebirthGearNativePlayerFile.Saved.Markers[counter]=1;Need(!RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out _,out _),"odd saved native step resumed");GameManager.Instance.SaveLocalPlayerData();
  RebirthGearNativePlayerFile.Saved.id++;Need(!RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out _,out _),"foreign saved native actor ID qualified");GameManager.Instance.SaveLocalPlayerData();
  RebirthNpcTradeService.OwnerAdmission.Policy=false;Need(!RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out _,out _),"changed saved native policy qualified");RebirthNpcTradeService.OwnerAdmission.Policy=true;
  RebirthGearNativePlayerFile.Saved.Bag[0].count++;Need(!RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out _,out _),"changed saved native prefix qualified");GameManager.Instance.SaveLocalPlayerData();
  player.Buffs.CVars[marker]=3;GameManager.Instance.SaveLocalPlayerData();Need(RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out step,out phase)&&phase==3,"exact acknowledged After did not qualify");
  player.entityId++;GameManager.Instance.SaveLocalPlayerData();Need(RebirthNpcTradeOwnerOriginalStore.IsRetained(player,original)&&RebirthNpcTradeOwnerCheckpoint.TryReadRetainedStage(player,original,out _,out _),"historical original ID blocked current-ID same-life native stage");
  Need(!RebirthNpcTradeOwnerOriginalStore.TryRetain(player,original.CopyQuote(),out _),"historical endpoint silently replanned as a fresh quote");player.entityId--;
  Need(!RebirthNpcTradeOwnerOriginalStore.TryReadAcknowledgement(player,original,out _),"missing ACK became terminal metadata");
  string terminal=new string('b',64),ack=RebirthNpcTradeOwnerReceipt.Write(original.Operation,original.Digest,terminal,false);
  Need(RebirthNpcTradeOwnerOriginalStore.TryRetainReceipt(player,original,"ack",ack)&&RebirthNpcTradeOwnerOriginalStore.TryReadAcknowledgement(player,original,out var retained)&&retained==terminal,"own retained ACK lost terminal binding");
  string ackPath=Path.Combine(GameIO.Directory,"REBIRTH","NpcTradeOriginals",Guid.Parse(original.World).ToString("N"),new string('a',64),original.Operation.ToString("N")+".ack");
  File.WriteAllText(ackPath,RebirthNpcTradeOwnerReceipt.Write(original.Operation,original.Digest,terminal,true),new UTF8Encoding(false));
  Need(!RebirthNpcTradeOwnerOriginalStore.TryReadAcknowledgement(player,original,out _),"close receipt substituted for ACK");File.WriteAllText(ackPath,ack,new UTF8Encoding(false));
 }
 static void ClosedCapacity(string directory,RebirthNpcTradeOriginal original)
 {
  Guid operation=Guid.NewGuid();string terminal=new string('b',64);
  string receipt=RebirthNpcTradeOwnerReceipt.Write(operation,original.Digest,terminal,true);
  Need(RebirthNpcTradeOwnerReceipt.TryRead(receipt,out var id,out var digest,out var end,out bool closed)&&id==operation&&digest==original.Digest&&end==terminal&&closed,"closed receipt codec lost binding");
  Need(!RebirthNpcTradeOwnerReceipt.TryRead(receipt.Replace("closed=\"1\"","closed=\"01\""),out _,out _,out _,out _),"noncanonical closed receipt accepted");
  Need(!RebirthNpcTradeOwnerReceipt.TryRead(receipt.Replace(" />"," extra=\"1\" />"),out _,out _,out _,out _),"unknown receipt field accepted");
  string path=Path.Combine(directory,"capacity");Directory.CreateDirectory(path);
  for(int i=0;i<512;i++)File.WriteAllText(Path.Combine(path,Guid.NewGuid().ToString("N")+".xml"),"partial original");
  Need(!Capacity(path),"512 unknown originals did not retain active capacity");
  string selected=Directory.EnumerateFiles(path,"*.xml").First();operation=Guid.ParseExact(Path.GetFileNameWithoutExtension(selected),"N");
  var body=XElement.Parse(original.Image);body.SetAttributeValue("operation",operation.ToString("N"));string image=body.ToString(SaveOptions.DisableFormatting);
  digest=RebirthRemoteResourceDurableJournal.Digest(image);string prefix=Path.Combine(path,operation.ToString("N"));
  File.WriteAllText(selected,image,new UTF8Encoding(false));
  string ack=RebirthNpcTradeOwnerReceipt.Write(operation,digest,terminal,false),done=RebirthNpcTradeOwnerReceipt.Write(operation,digest,terminal,true);
  File.WriteAllText(prefix+".ack",ack,new UTF8Encoding(false));File.WriteAllText(prefix+".closed",done,new UTF8Encoding(false));
  Need(Capacity(path),"positive paired closed history still consumed active capacity");
  File.WriteAllText(prefix+".closed",done.Replace(digest,new string('c',64)),new UTF8Encoding(false));Need(!Capacity(path),"foreign closed original freed capacity");
  File.WriteAllText(prefix+".closed",done,new UTF8Encoding(false));File.WriteAllText(selected,image.Replace("version=\"1\"","version=\"2\""),new UTF8Encoding(false));
  Need(!Capacity(path),"same-size changed original ignored closed binding");File.WriteAllText(selected,image,new UTF8Encoding(false));
  File.WriteAllText(prefix+".ack",ack.Replace(terminal,new string('d',64)),new UTF8Encoding(false));Need(!Capacity(path),"different terminal pair freed unresolved capacity");
  File.WriteAllText(prefix+".ack",done,new UTF8Encoding(false));Need(!Capacity(path),"closed/closed pair replaced missing acknowledgement");
  File.WriteAllText(prefix+".ack",ack,new UTF8Encoding(false));Need(Capacity(path),"unchanged exact closed history did not recover its metadata classification");
  string unused=RebirthNpcTradeOwnerReceipt.WriteUnused(operation,digest,terminal);
  Need(RebirthNpcTradeOwnerReceipt.TryReadUnused(unused,out var unusedId,out var unusedOriginal,out var refusal)&&unusedId==operation&&unusedOriginal==digest&&refusal==terminal,"unused receipt lost binding");
  Need(!RebirthNpcTradeOwnerReceipt.TryReadUnused(unused.Replace("version=\"1\"","version=\"01\""),out _,out _,out _),"noncanonical unused receipt accepted");
  Need(!RebirthNpcTradeOwnerReceipt.TryReadUnused(done,out _,out _,out _),"paid close used as unused receipt");
  File.Delete(prefix+".ack");File.Delete(prefix+".closed");File.WriteAllText(prefix+".unused",unused,new UTF8Encoding(false));
  Need(Capacity(path),"positive unused history still consumed active capacity");
  File.WriteAllText(prefix+".unused",unused.Replace(digest,new string('e',64)),new UTF8Encoding(false));Need(!Capacity(path),"foreign unused original freed capacity");
  File.WriteAllText(prefix+".unused",unused,new UTF8Encoding(false));File.WriteAllText(selected,image.Replace("version=\"1\"","version=\"2\""),new UTF8Encoding(false));
  Need(!Capacity(path),"changed original ignored unused binding");File.WriteAllText(selected,image,new UTF8Encoding(false));Need(Capacity(path),"exact unused history failed reclassification");
 }
 internal static void Run()
 {
  string directory=Path.Combine(Path.GetTempPath(),"rebirth-trade-store-"+Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(directory);GameIO.Directory=directory;
  try
  {
   var world=new TradeWorld();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;
   player.bag.ItemGrid.items=new[]{new ItemStack(new ItemValue{type=2},6)};
   player.inventory.ItemGrid.items=new[]{new ItemStack(new ItemValue{type=0},1)};
   var quote=new RebirthNpcTradeQuote();
   Need(RebirthNpcTradeOwnerOriginalStore.TryRetain(player,quote,out var original),"first native owner original not retained");
   Need(RebirthVehicleNativePolicy.Reads>0&&original.MatchesSlots(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,false),"actual first native plan missing");
   string marker=RebirthNpcTradeOwnerCheckpoint.Marker(original.Operation);
   Need(RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,original)&&GameManager.Instance.SaveCalls==1,"native Before preparation did not require saved marker/full inventory");
   Need(RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,original)&&GameManager.Instance.SaveCalls==1,"positively saved Before unnecessarily saved again");
   RebirthGearNativePlayerFile.Saved.Markers[marker]=2;Need(!RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,original)&&GameManager.Instance.SaveCalls==1,"later saved phase overwritten by matching live Before");RebirthGearNativePlayerFile.Saved.Markers[marker]=1;
   string stepMarker=RebirthNpcTradeOwnerExchange.Step(original.Operation);RebirthGearNativePlayerFile.Saved.Markers[stepMarker]=1;
   Need(!RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,original)&&GameManager.Instance.SaveCalls==1,"saved entered step overwritten as fresh Before");RebirthGearNativePlayerFile.Saved.Markers[stepMarker]=0;
   player.Buffs.CVars[marker]=2f;Need(!RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,original)&&GameManager.Instance.SaveCalls==1,"later/unknown marker overwritten with Before");
   player.Buffs.CVars[marker]=1f;RebirthGearNativePlayerFile.Saved=null;GameManager.Instance.SaveAllowed=false;
   Need(!RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,original)&&original.MatchesSlots(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,false),"failed save certified Before or changed inventory");
   GameManager.Instance.SaveAllowed=true;Need(RebirthNpcTradeOwnerCheckpoint.TrySaveBefore(player,original),"same original saved Before could not resume");
   RebirthVehicleNativePolicy.Reads=0;player.bag.ItemGrid.items[0].count=5;
   Need(RebirthNpcTradeOwnerOriginalStore.TryRetain(player,quote,out var retained)&&retained.Image==original.Image&&RebirthVehicleNativePolicy.Reads==0,"retry replanned original from changed native cells");
   var altered=original.CopyQuote();altered.ExpiresClockTicks++;
   Need(!RebirthNpcTradeOwnerOriginalStore.TryRetain(player,altered,out _),"changed issued quote replaced original");
   player.Died++;Need(!RebirthNpcTradeOwnerOriginalStore.TryRetain(player,quote,out _),"old native life original silently adopted");player.Died--;
   string path=Directory.GetFiles(directory,"*.xml",SearchOption.AllDirectories).Single();
   File.WriteAllText(path,"partial interrupted original");
   Need(!RebirthNpcTradeOwnerOriginalStore.TryRetain(player,quote,out _)&&File.ReadAllText(path)=="partial interrupted original","unknown original overwritten or replanned");
   File.WriteAllText(path,original.Image,new UTF8Encoding(false));
   ClosedCapacity(directory,original);
   RetainedStages(player,original);
   world.Player=new EntityPlayerLocal{world=world};Need(!RebirthNpcTradeOwnerOriginalStore.TryRetain(player,quote,out _),"replaced primary owner entered trade capture");
  }
  finally
  {
   GameIO.Directory=null;GameManager.Instance.World=null;RebirthGearNativePlayerFile.Saved=null;
   if(Path.GetFileName(directory).StartsWith("rebirth-trade-store-",StringComparison.Ordinal)&&Path.GetDirectoryName(Path.GetFullPath(directory))==Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))Directory.Delete(directory,true);
   else throw new InvalidDataException("Unsafe fixture directory");
  }
 }
}
