// AUTHORED_NOT_RUN. Actual hydration checkpoint/hold/receipt parser/models;
// native PDS writer/loader and native buffers are adapters. The gear checkpoint
// adapter below remains only to assert the hydration path does not invoke it.
internal sealed class Buffs {
 internal readonly Dictionary<string,float> Values=new Dictionary<string,float>();internal bool Suppress;
 internal float GetCustomVar(string name)=>Values.TryGetValue(name,out var value)?value:0;
 internal void SetCustomVar(string name,float value,bool persist){if(!Suppress)Values[name]=value;}
}
internal sealed partial class EntityPlayerLocal {internal Buffs Buffs=new Buffs();}
internal sealed partial class GameManager {internal void SaveLocalPlayerData()=>NativeStore.Save(World.Actor);}
internal sealed class NativeFile {internal int deaths;internal MemoryStream buffData;internal ItemStack[] Bag,Belt;}
internal static class NativeStore {
 internal static NativeFile File;internal static int Saves;internal static bool Suppress,ThrowAfterSave;
 internal static ItemStack[] Copy(ItemStack[] cells)=>cells.Select(cell=>new ItemStack{count=cell.count,Data=cell.Data}).ToArray();
 internal static byte[] BuffBytes(Buffs buffs){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
  writer.Write((byte)EntityBuffs.Version);writer.Write((ushort)0);writer.Write((ushort)buffs.Values.Count);foreach(var pair in buffs.Values){writer.Write(pair.Key);writer.Write(pair.Value);}writer.Flush();return stream.ToArray();}}
 internal static void Save(EntityPlayerLocal player){Saves++;if(Suppress)return;File=new NativeFile{deaths=player.Died,buffData=new MemoryStream(BuffBytes(player.Buffs)),Bag=Copy(player.bag.ItemGrid.items),Belt=Copy(player.inventory.ItemGrid.items)};if(ThrowAfterSave)throw new IOException("unknown native file save");}
}
internal static class RebirthGearNativePlayerFile {internal static int Reads;internal static Action AfterRead;internal static bool TryRead(RebirthStablePlayerIdentity owner,out NativeFile file){Reads++;file=NativeStore.File;AfterRead?.Invoke();return file!=null;}}
internal static class RebirthGearPlayerDataInventory {internal static ItemStack[] ReadSlots(NativeFile file,bool bag)=>bag?file.Bag:file.Belt;}
internal sealed class RebirthGearTransferState {
 internal string TransactionId;internal static bool TryCreate(string operation,string creation,string slot,long revision,RebirthGearInventoryPlan plan,out RebirthGearTransferState offer){offer=new RebirthGearTransferState{TransactionId=operation};return slot=="support"&&revision>=0&&plan.IsConserved();}
}
internal static class RebirthGearOwnerSaveCheckpoint {
 internal static int Calls;internal static bool Allow=true,SkipSave;
 internal static bool TryPersist(EntityPlayerLocal player,RebirthGearTransferState offer,RebirthGearOwnerReceipt stage,Func<bool> current){
  Calls++;if(!current())return false;player.Buffs.SetCustomVar("rbGear_"+offer.TransactionId,(float)stage,true);if(!SkipSave)GameManager.Instance.SaveLocalPlayerData();return Allow&&current();
 }
}
internal static class EntityBuffs {internal const int Version=3;}
internal sealed class BuffValue {internal void Read(PooledReader reader,int version){throw new InvalidDataException("This native-buffer adapter supports zero active buffs only");}}
internal sealed class PooledReader:IDisposable {
 private BinaryReader reader;internal void SetBaseStream(Stream stream){reader=new BinaryReader(stream);}
 internal byte ReadByte()=>reader.ReadByte();internal ushort ReadUInt16()=>reader.ReadUInt16();internal string ReadString()=>reader.ReadString();internal float ReadSingle()=>reader.ReadSingle();public void Dispose(){reader?.Dispose();}
}
internal sealed class Pool {internal PooledReader AllocSync(bool reset)=>new PooledReader();}
internal static class MemoryPools {internal static Pool poolBinaryReader=new Pool();}
internal static class Program {
 private static EntityPlayerLocal player;private static RebirthHydrationTransferOriginal original;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static ItemStack[] Cells(int length){var cells=new ItemStack[length];for(int i=0;i<length;i++)cells[i]=new ItemStack();return cells;}
 private static void Fresh(bool remote=false,bool returning=false){
  var world=new World{Remote=remote};player=new EntityPlayerLocal{world=world,bag=new Bag{ItemGrid=new Grid{items=Cells(44)},LockedSlots=new bool[44]},inventory=new Inventory{ItemGrid=new Grid{items=Cells(20)}}};world.Actor=world.Primary=player;GameManager.Instance.World=world;
  ThreadManager.Main=true;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager{IsServer=!remote};RebirthStablePlayerIdentity.Owner="account";RebirthRemoteResourceOwnerAction.Allowed=true;RebirthDogLifecycleService.Held=false;RemoteResourceClientTransactionCoordinator.Pending=false;RebirthToolbeltCapacity.Owned=4;RebirthGearInventorySnapshot.DuringCapture=null;
  player.bag.ItemGrid.items[3]=new ItemStack{count=3,Data=Convert.ToBase64String(new byte[]{7,9,3,25})};
  var fixtureHydration=returning?new Stack{Count=1,ItemData=player.bag.ItemGrid.items[3].Data}:new Stack();
  Need(RebirthGearInventorySnapshot.TryCapture(player.bag.ItemGrid.items,player.inventory.ItemGrid.items,4,out var snapshot)&&RebirthHydrationTransferPlan.TryCreate(snapshot.Bag,snapshot.Belt,null,4,fixtureHydration,true,returning?-1:3,out var plan)&&RebirthHydrationTransferOriginal.TryCreate(Guid.NewGuid(),Guid.Parse(world.Guid),"account",player.Creation,2,8,4,null,plan,out original)&&RebirthHydrationOwnerHold.TryAcquire(player,original),"fixture native original unavailable");
  NativeStore.File=null;NativeStore.Saves=0;NativeStore.Suppress=NativeStore.ThrowAfterSave=false;RebirthGearOwnerSaveCheckpoint.Calls=0;RebirthGearOwnerSaveCheckpoint.Allow=true;RebirthGearOwnerSaveCheckpoint.SkipSave=false;RebirthGearNativePlayerFile.Reads=0;RebirthGearNativePlayerFile.AfterRead=null;
  GameManager.Instance.PersistentOwner="account";
 }
 public static void Main(){
  Fresh();Need(!RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applied)&&NativeStore.Saves==0,"applied receipt without applying stage");
  Need(RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying)&&player.bag.ItemGrid.items[3].count==3,"native applying checkpoint changed items or failed");
  Need(!RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applied)&&player.Buffs.GetCustomVar(RebirthHydrationOwnerCheckpoint.Marker(original))==2f,"Before inventory advanced Applied receipt");
  Need(RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original),"exact saved applying preimage refused fresh grant observation");
  player.bag.ItemGrid.items[3].count=2;Need(!RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original),"saved Before admitted fresh grant over changed live source");player.bag.ItemGrid.items[3].count=3;
  RebirthGearNativePlayerFile.AfterRead=()=>player.bag.ItemGrid.items[3].count=2;Need(!RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original),"native read callback changed live source after fresh Before qualification");RebirthGearNativePlayerFile.AfterRead=null;player.bag.ItemGrid.items[3].count=3;
  // Applying may describe a retained partial application. That is sufficient
  // for warm reconciliation, but must never mint a fresh Before-only grant.
  NativeStore.File.Bag[3].count=2;
  Need(RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applying)&&
   !RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original),"saved applying postimage minted fresh grant permission");
  NativeStore.File.Bag[3].count=3;
  NativeStore.File.Bag[3].Data=Convert.ToBase64String(new byte[]{99});
  Need(!RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original),"foreign rich native preimage qualified fresh grant");
  NativeStore.File.Bag[3].Data=player.bag.ItemGrid.items[3].Data;
  player.bag.ItemGrid.items[3].count=2;Need(RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applied)&&RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied),"exact native owner applied leg not qualified");
  Need(!RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying),"applied receipt downgraded");
  Need(!RebirthHydrationOwnerCheckpoint.HasSavedBefore(player,original),"applied native terminal treated as fresh preimage");
  NativeStore.File.deaths++;Need(!RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied),"other-life native file qualified");NativeStore.File.deaths--;
  NativeStore.File.Bag[3].Data=Convert.ToBase64String(new byte[]{99});Need(!RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied),"different rich item native postimage qualified");
  Fresh();player.Buffs.Suppress=true;Need(!RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying)&&RebirthGearOwnerSaveCheckpoint.Calls==0,"suppressed digest marker reached native save");
  Fresh();GameManager.Instance.PersistentOwner="foreign";Need(!RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying)&&NativeStore.Saves==0,"native save identity disagreed with original platform owner");
  Fresh();NativeStore.ThrowAfterSave=true;Need(!RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying)&&NativeStore.File!=null,"unknown native checkpoint save lost final file");NativeStore.ThrowAfterSave=false;int applyingSaves=NativeStore.Saves;Need(RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applying)&&player.bag.ItemGrid.items[3].count==3&&NativeStore.Saves==applyingSaves,"positive saved metadata retry repeated publication or changed inventory");
  player.bag.ItemGrid.items[3].count=2;RebirthGearOwnerSaveCheckpoint.Allow=false;int saves=NativeStore.Saves;Need(RebirthHydrationOwnerCheckpoint.TryPersist(player,original,RebirthGearOwnerReceipt.Applied)&&NativeStore.Saves==saves+1&&RebirthGearOwnerSaveCheckpoint.Calls==0,"hydration native receipt depended on unrelated gear writer");
  int reads=RebirthGearNativePlayerFile.Reads;Need(RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied)&&RebirthGearNativePlayerFile.Reads==reads+1,"saved native receipts and grids did not use one file observation");
  string seenKey="rbHydrationSeen_"+original.Operation.ToString("N")+"_"+original.Digest;
  Need(RebirthGearReceiptReader.TryReadHydrationSeenStage(NativeStore.BuffBytes(player.Buffs),original.Operation,original.Digest,out var seenStage)&&seenStage==0f,"missing seen marker invented terminal reception");
  player.Buffs.Values[seenKey]=1f;Need(RebirthGearReceiptReader.TryReadHydrationSeenStage(NativeStore.BuffBytes(player.Buffs),original.Operation,original.Digest,out seenStage)&&seenStage==1f,"exact native terminal-seen receipt refused");
  player.Buffs.Values.Remove(seenKey);player.Buffs.Values[seenKey.ToUpperInvariant()]=1f;Need(!RebirthGearReceiptReader.TryReadHydrationSeenStage(NativeStore.BuffBytes(player.Buffs),original.Operation,original.Digest,out _),"noncanonical seen receipt qualified");player.Buffs.Values.Remove(seenKey.ToUpperInvariant());
  player.Buffs.Values[seenKey]=float.NaN;Need(!RebirthGearReceiptReader.TryReadHydrationSeenStage(NativeStore.BuffBytes(player.Buffs),original.Operation,original.Digest,out _),"malformed terminal-seen value qualified");player.Buffs.Values.Remove(seenKey);
  RebirthGearNativePlayerFile.AfterRead=()=>player.world.Primary=null;Need(!RebirthHydrationOwnerCheckpoint.HasSaved(player,original,RebirthGearOwnerReceipt.Applied),"lost native primary during saved read qualified");
  Console.WriteLine("PASS actual hydration native checkpoint/hold/receipt parser; PDS IO/buff buffers are adapters, topology NOT RUN");
 }
}
