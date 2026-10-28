using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
// AUTHORED_NOT_RUN. Full production fuel engine + full actual binary journal.
// Native image/save/world/lease/identity adapters are explicit below. Filesystem
// publication and original history are real; native vehicles.dat is not modeled
// as genuine game evidence by this fixture.
namespace UnityEngine {public static class Time{public static float realtimeSinceStartup;}}
public static class ThreadManager{public static bool Main=true;public static bool IsMainThread()=>Main;}
public class WorldState{public string Guid="world";}
public class World{public WorldState worldState=new WorldState();public EntityVehicle Vehicle;public EntityPlayer Actor;public bool IsRemote()=>false;public object GetEntity(int id)=>id==1?(object)Actor:Vehicle!=null&&Vehicle.entityId==id?Vehicle:null;}
public class GameManager{public static GameManager Instance=new GameManager();public World World;public UserId getPersistentPlayerID(object unused)=>new UserId();}
public class UserId{public string CombinedString="owner";}
public static class GameIO{public static string Save;public static string GetSaveGameDir()=>Save;}
public class PlayerGrid{public ItemStack[] items=ItemStack.CreateArray(0);public object SlotLocks;}
public class PlayerInventory{public PlayerGrid ItemGrid=new PlayerGrid();public object LockedSlots;public int Length;}
public class EntityPlayer{public World world;public int entityId=1,Died;public string Creation="creation";public PlayerInventory bag=new PlayerInventory(),inventory=new PlayerInventory();public bool IsDead()=>false;}
public class EntityPlayerLocal:EntityPlayer{}
public class EntityVehicle{public World world;public int entityId=20;public byte NativeBody=7;public RebirthCamperModuleRuntime Runtime;}
public class ItemClass{public int MaxCount=100;}
public class ItemValue{public string Value="installed";public int type=1;public bool HasModSlots,Mods;public Dictionary<string,object> Metadata=new Dictionary<string,object>();public ItemClass ItemClass=new ItemClass();public bool IsEmpty()=>type==0;public bool HasMods()=>Mods;public bool HasMetadata(string key)=>Metadata.ContainsKey(key);public bool TryGetMetadata<T>(string key,out T value){if(Metadata.TryGetValue(key,out var raw)&&raw is T typed){value=typed;return true;}value=default(T);return false;}public void SetMetadata(string key,object value){Metadata[key]=value;}public ItemValue Clone()=>new ItemValue{Value=Value,type=type,HasModSlots=HasModSlots,Mods=Mods,Metadata=new Dictionary<string,object>(Metadata)};}
public class ItemStack{public ItemValue itemValue;public int count;public static ItemStack Empty=new ItemStack(new ItemValue{type=0},0);public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);public bool IsEmpty()=>count==0||itemValue.IsEmpty();public static ItemStack[] CreateArray(int n)=>Enumerable.Range(0,n).Select(_=>Empty.Clone()).ToArray();public static ItemStack[] Clone(ItemStack[] cells)=>cells.Select(s=>s.Clone()).ToArray();public static int AddToItemStackArray(ItemStack[] cells,ItemStack stack,int maximum){int left=stack.count;for(int i=0;i<cells.Length&&left>0;i++){var same=cells[i].Clone();same.count=stack.count;if(!cells[i].IsEmpty()&&!RebirthStationGridIngredients.IsSameStackSnapshot(same,stack))continue;int take=Math.Min(left,maximum-(cells[i].IsEmpty()?0:cells[i].count));if(take<1)continue;cells[i]=new ItemStack(stack.itemValue.Clone(),(cells[i].IsEmpty()?0:cells[i].count)+take);left-=take;}return left==0?0:-1;}}
public static class RebirthNativeItemCodec{public static string Encode(ItemValue v)=>v?.Value;public static bool TryDecode(string text,out ItemValue v){v=new ItemValue{Value=text};return text=="installed";}}
public class Recipe{public int Version=1,itemValueType=30,count=1,craftingTier,craftExpGain,unlockExpGain;public float craftingTime=2;public string craftingArea,craftingToolType,tooltip;public object tags,Effects;public bool UseIngredientModifier,IsTrackable,isQuest,isChallenge,IsTracked,IsLearnable,wildcardForgeCategory,wildcardCampfireCategory,IsScrap,materialBasedRecipe;public List<ItemStack> ingredients=new List<ItemStack>();public string GetName()=>"recipe"+itemValueType;}
public class RecipeQueueItem{public Recipe Recipe;public short Multiplier;public byte Quality;public float CraftingTimeLeft,OneItemCraftTime;public int StartingEntityId=-1,AmountToRepair;public bool IsCrafting;public ItemValue RepairItem;}
public class RebirthCamperStationState{public Guid InstallationId;public EntityVehicle BoundVehicle;public bool IsBurning;public long Revision;public ulong lastTickTime;public int Stock=50,InputSlotCount=3;public string[] MaterialNames=Array.Empty<string>();public ItemStack[] Input=ItemStack.CreateArray(3);public RecipeQueueItem[] Queue={new RecipeQueueItem(),new RecipeQueueItem()};public void setModified(){Revision++;}public void RestoreRevision(long r){Revision=r;}}
public class Installed{public RebirthCamperStationState State;public ItemStack InstalledItem=new ItemStack(new ItemValue(),1);}
public static class RebirthCamperCraftingStation{public const int ModuleSlots=1;}
public class Stations{public Installed Installed;public Installed Find(Guid id)=>Installed.State.InstallationId==id?Installed:null;public Installed GetSlot(int slot)=>slot==0?Installed:null;}
public class RebirthCamperModuleRuntime{public Guid Identity=Guid.NewGuid();public Stations Stations;public bool TransferPending;public int PersistCalls;public byte[] SerializedImage;public Action PersistHook;public void Persist(bool force){PersistCalls++;PersistHook?.Invoke();SerializedImage=RebirthCamperStationSnapshot.Capture(Stations.Installed.State.InstallationId,Stations.Installed.State);}public static bool TryGetExisting(EntityVehicle v,out RebirthCamperModuleRuntime r){r=v?.Runtime;return r!=null;}public static RebirthCamperModuleRuntime For(EntityVehicle v)=>v.Runtime;}
public static class RebirthCamperStationSnapshot{
 public static void WriteStack(BinaryWriter w,ItemStack s){w.Write(s.count);w.Write(s.itemValue.type);w.Write(s.itemValue.Value);w.Write(s.itemValue.HasModSlots);w.Write(s.itemValue.Mods);w.Write(s.itemValue.Metadata.Count);foreach(var pair in s.itemValue.Metadata.OrderBy(p=>p.Key,StringComparer.Ordinal)){w.Write(pair.Key);w.Write(pair.Value is int);if(pair.Value is int n)w.Write(n);else w.Write((string)pair.Value);}}
 public static ItemStack ReadStack(BinaryReader r){int count=r.ReadInt32();var v=new ItemValue{type=r.ReadInt32(),Value=r.ReadString(),HasModSlots=r.ReadBoolean(),Mods=r.ReadBoolean()};int n=r.ReadInt32();for(int i=0;i<n;i++){string key=r.ReadString();v.Metadata[key]=r.ReadBoolean()?(object)r.ReadInt32():r.ReadString();}return new ItemStack(v,count);}
 public static byte[] Capture(Guid id,RebirthCamperStationState s){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write(id.ToByteArray());w.Write(s.IsBurning);w.Write(s.Revision);w.Write(s.lastTickTime);w.Write(s.Stock);w.Write(s.InputSlotCount);w.Write(s.MaterialNames.Length);foreach(var name in s.MaterialNames)w.Write(name);w.Write(s.Input.Length);foreach(var cell in s.Input)WriteStack(w,cell);w.Write(s.Queue.Length);foreach(var row in s.Queue){w.Write(row.Multiplier);w.Write(row.Quality);w.Write(row.CraftingTimeLeft);w.Write(row.OneItemCraftTime);w.Write(row.StartingEntityId);w.Write(row.AmountToRepair);w.Write(row.IsCrafting);w.Write(row.RepairItem!=null);if(row.RepairItem!=null)WriteStack(w,new ItemStack(row.RepairItem,1));w.Write(row.Recipe!=null);if(row.Recipe!=null){w.Write(row.Recipe.itemValueType);w.Write(row.Recipe.IsScrap);w.Write(row.Recipe.craftingTime);w.Write(row.Recipe.ingredients.Count);foreach(var cell in row.Recipe.ingredients)WriteStack(w,cell);}}return m.ToArray();}}
 public static RebirthCamperStationState Restore(Guid id,ItemStack item,byte[] bytes){using(var m=new MemoryStream(bytes))using(var r=new BinaryReader(m)){if(new Guid(r.ReadBytes(16))!=id)throw new InvalidDataException();var s=new RebirthCamperStationState{InstallationId=id,IsBurning=r.ReadBoolean(),Revision=r.ReadInt64(),lastTickTime=r.ReadUInt64(),Stock=r.ReadInt32(),InputSlotCount=r.ReadInt32()};s.MaterialNames=Enumerable.Range(0,r.ReadInt32()).Select(_=>r.ReadString()).ToArray();s.Input=Enumerable.Range(0,r.ReadInt32()).Select(_=>ReadStack(r)).ToArray();s.Queue=Enumerable.Range(0,r.ReadInt32()).Select(_=>{var row=new RecipeQueueItem{Multiplier=r.ReadInt16(),Quality=r.ReadByte(),CraftingTimeLeft=r.ReadSingle(),OneItemCraftTime=r.ReadSingle(),StartingEntityId=r.ReadInt32(),AmountToRepair=r.ReadInt32(),IsCrafting=r.ReadBoolean()};if(r.ReadBoolean())row.RepairItem=ReadStack(r).itemValue;if(r.ReadBoolean()){row.Recipe=new Recipe{itemValueType=r.ReadInt32(),IsScrap=r.ReadBoolean(),craftingTime=r.ReadSingle()};row.Recipe.ingredients=Enumerable.Range(0,r.ReadInt32()).Select(i=>ReadStack(r)).ToList();}return row;}).ToArray();if(m.Position!=m.Length)throw new InvalidDataException();return s;}}
}
public class RebirthStablePlayerIdentity{public string StorageKey=new string('a',64);public static bool TryResolveServerEntity(EntityPlayer p,out RebirthStablePlayerIdentity owner){owner=new RebirthStablePlayerIdentity();return p!=null;}}
public struct RebirthCamperStationAddress{public Guid Vehicle,Installation;public RebirthCamperStationAddress(Guid v,Guid i){Vehicle=v;Installation=i;}public bool IsValid=>Vehicle!=Guid.Empty&&Installation!=Guid.Empty;public override string ToString()=>Vehicle.ToString("N")+":"+Installation.ToString("N");}
public static class RebirthCamperRemoteStationTransport{public static bool Current=true;public static bool IsCurrent(EntityPlayer p,RebirthCamperStationAddress a,string c,object lease)=>Current;}
public static class RebirthCamperCharacterAccess{public static string CurrentCreation(EntityPlayer actor)=>actor.Creation;}
public static class RebirthSurvivorMode{public static bool Enabled;public static bool IsEnabledForCurrentWorld()=>Enabled;}
public static class RebirthStationLiveAccess{public static object Lease=new object();public static bool TryGetMobileLease(EntityPlayer actor,RebirthCamperStationAddress address,string creation,out RebirthCamperStationState state,out object lease){state=actor?.world?.Vehicle?.Runtime?.Stations.Installed.State;lease=Lease;return RebirthCamperRemoteStationTransport.Current&&state!=null&&state.InstallationId==address.Installation;}}
public static class RebirthCamperStationWindowSession{public static int Projections;public static void MarkOriginalProjection(RebirthCamperStationState s){Projections++;}}
public class MaterialAllocation{public int Count,Slot;public string MaterialName;public ItemStack Snapshot()=>ItemStack.Empty.Clone();}
public class MaterialPlan{public List<MaterialAllocation> Inputs=new List<MaterialAllocation>();}
public static class RebirthStationGridIngredients{
 public static bool IsSameStackSnapshot(ItemStack a,ItemStack b){if(a==null||b==null)return a==b;using(var x=new MemoryStream())using(var y=new MemoryStream())using(var wx=new BinaryWriter(x))using(var wy=new BinaryWriter(y)){RebirthCamperStationSnapshot.WriteStack(wx,a);RebirthCamperStationSnapshot.WriteStack(wy,b);return x.ToArray().SequenceEqual(y.ToArray());}}
 public static bool TryPlanMaterial(EntityPlayer p,Recipe recipe,ItemStack[] input,int slots,string[] names,int batches,int tier,out MaterialPlan plan){plan=null;return false;}
 public static bool TryBuildDebitedGrid(MaterialPlan plan,ItemStack[] input,out ItemStack[] after){after=null;return false;}
}
public static class RebirthStationPlayerIngredients{
 public class Allocation{public bool IsBag;public int Slot;}
 public class Plan{public List<Allocation> Inputs=new List<Allocation>();public List<ItemStack> PaidSnapshots()=>new List<ItemStack>();public bool Matches(ItemStack[] a,ItemStack[] b,bool[] locks,bool[] beltLocks,int length)=>false;public bool TryGetImages(out ItemStack[] a,out ItemStack[] b,out ItemStack[] c,out ItemStack[] d){a=b=c=d=null;return false;}}
 public static bool UsesPlayerPayment(EntityPlayer p,RebirthCamperStationState s)=>false;
 public static bool TryPlan(EntityPlayer p,Recipe r,int n,int tier,ItemStack[] bag,ItemStack[] belt,bool[] locks,bool[] beltLocks,int length,out Plan plan){plan=null;return false;}
}
public static class RebirthCraftingIngredientQuantity{public static bool TryResolveTotal(EntityPlayer p,Recipe r,ItemStack ingredient,int tier,int batches,out int count){count=checked(ingredient.count*batches);return count>0;}}
public enum PassiveEffects{CraftingTime}
public static class EffectManager{public static float GetValue(PassiveEffects effect,float _originalValue,EntityPlayer _entity,Recipe _recipe,object tags)=>_originalValue;}
public static class XUiM_Recipes{public static float CraftingTimeModifier=1;}
public static class RebirthStationGridQueue{public static bool IsMarked(Recipe r)=>false;public static bool TryGetJobId(Recipe r,out string id){id=null;return false;}}
public static class RebirthRemoteResourceFetchedStationOriginal{public static bool IsCancellationCandidate(Recipe r)=>false;public static bool CancelFetched(EntityPlayer p,RebirthCamperStationAddress a,string c,Guid job)=>false;}
public static class RebirthCamperStationNativeActorPayment{
 public static bool IsInstalled=true;public static bool IsHeld(RebirthCamperStationState s)=>false;public static bool MatchesPendingOperation(RebirthCamperStationState s,Guid op)=>false;public static bool TryResume(EntityPlayer p,RebirthCamperStationState s,RebirthCamperStationAddress a,string c)=>false;public static bool[] Locks(object o)=>null;
 public static bool Begin(EntityPlayer p,RebirthCamperStationState s,RebirthCamperStationAddress a,string c,Guid op,ItemStack[] before,ItemStack[] after,ItemStack[] beltBefore,ItemStack[] beltAfter,RecipeQueueItem[] queue)=>false;
}
public static class RebirthStationNativeQueueEntry{
// ACTUAL_VACANT_SLOT
}
// ACTUAL_NATIVE_ORIGINAL
internal static partial class RebirthCamperStationNativeCrafting{
// Native recipe unlock/tool policy and the actor-only payment/cancel route are
// explicit adapters here. Station input Plan/Craft/Cancel and receipt arithmetic
// below are actual source, not reimplemented producers.
 static bool Allowed(EntityPlayer p,RebirthCamperStationState s,Recipe r,int tier)=>r!=null&&!r.IsScrap;
 static bool Resolve(EntityPlayer p,RebirthCamperStationAddress a,string c,out RebirthCamperStationState s,out object lease)=>RebirthStationLiveAccess.TryGetMobileLease(p,a,c,out s,out lease)&&!RebirthCamperStationFuelOriginal.IsHeld(s);
 static bool CancelActor(EntityPlayer p,RebirthCamperStationState s,RebirthCamperStationAddress a,string c,object lease,Guid job,int slot,List<ItemStack> refunds)=>false;
// ACTUAL_NATIVE_CONSTANTS
// ACTUAL_NATIVE_JOB
// ACTUAL_NATIVE_REFUND
// ACTUAL_NATIVE_PAID
// ACTUAL_NATIVE_PLAN
// ACTUAL_NATIVE_CRAFT
// ACTUAL_NATIVE_CANCEL
// ACTUAL_NATIVE_CANCEL_ID
}
public static class RebirthCamperEnergyRuntime{public static bool Held;public static bool IsHeld(EntityVehicle v)=>Held;}
public static class RebirthCamperEnergyCustody{public static RebirthCamperTransferJournal.Record Record;public static RebirthCamperTransferJournal.Record FindPending(Guid id)=>Record;}
public class GameTimer{public static GameTimer Instance=new GameTimer();public ulong ticks=100;}
public class RebirthCamperEnergyState{public long Revision;public int Mode;public static byte[] Encode(RebirthCamperEnergyState s)=>BitConverter.GetBytes(s.Revision);public static RebirthCamperEnergyState Decode(byte[] image,Guid id)=>new RebirthCamperEnergyState{Revision=BitConverter.ToInt64(image,0)};}
public static class RebirthCamperEnergyImmobility{public static bool Apply(EntityVehicle v,int mode)=>true;}
public class EnergyMutator{
 EntityVehicle vehicle;RebirthCamperEnergyState state=new RebirthCamperEnergyState();HashSet<Guid> holds=new HashSet<Guid>();Guid Identity=Guid.NewGuid();
 bool IsRegistered=true;ulong lastTick;int demand,supplied;double energyDemand;public int Writes,Suspensions;
 Func<EntityVehicle,bool> CustodyReady=v=>true;Action<EntityVehicle> SuspendConsumers;
 public EnergyMutator(EntityVehicle v){vehicle=v;SuspendConsumers=_=>Suspensions++;}
 static bool Current(EntityVehicle v)=>ReferenceEquals(GameManager.Instance.World,v.world)&&ReferenceEquals(v.world.Vehicle,v);
 static bool Same(byte[] a,byte[] b)=>a.SequenceEqual(b);void Persist(){Writes++;}
 public class Change{public Guid Identity;public long ExpectedRevision;public byte[] before,after;}
 public bool Prepare()=>CanPrepare(0,out _);
 public bool AttemptCommit()=>Commit(new Change{Identity=Identity,ExpectedRevision=0,before=BitConverter.GetBytes(0L),after=BitConverter.GetBytes(1L)});
// ACTUAL_ENERGY_HOLD
// ACTUAL_ENERGY_PREPARE
// ACTUAL_ENERGY_COMMIT
// ACTUAL_ENERGY_TICK_PREFIX
}
public static class RebirthCamperRemoteStationCommandStatus{public enum State{Unknown,Pending,Applied,Rejected}
// ACTUAL_BINDING
}
public static class RebirthCookingRemoteAdmission{public static XElement ReadProtocolXml(string text,int max){using(var r=XmlReader.Create(new StringReader(text),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=max}))return XElement.Load(r);}}
public static class Log{public static void Warning(string text){}}
public static class ModEvents{public struct SGameUpdateData{}public delegate void Handler(ref SGameUpdateData d);public class Event{public void RegisterHandler(Handler h){}}public static Event GameUpdate=new Event();}
public static class RebirthCamperTransferHistoryScope{public class Index{public RebirthCamperTransferJournal.Record FindPending(Guid id)=>null;public IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid id,string c)=>Array.Empty<RebirthCamperTransferJournal.Record>();public IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid id,string c)=>Array.Empty<RebirthCamperTransferJournal.Record>();}public static bool TryGet(RebirthCamperTransferJournal j,out Index i){i=null;return false;}}
public static class RebirthCamperSavedVehicle{
 public sealed class Expected:IDisposable{public int Id=20,Class=5;public byte[] Bag=new byte[]{7},Entity;public World World;public void Dispose(){}}
 public static byte[] NativeImage;public static bool AllowSave=true,AllowVerify=true;public static int Saves;public static Action OnSave;
 public static Expected Capture(EntityVehicle vehicle)=>new Expected{World=vehicle.world,Entity=vehicle.Runtime.SerializedImage.Concat(new[]{vehicle.NativeBody}).ToArray()};
 public static Expected RequestSave(EntityVehicle vehicle){var e=Capture(vehicle);Saves++;OnSave?.Invoke();if(AllowSave)NativeImage=(byte[])e.Entity.Clone();return e;}
 public static bool Verify(EntityVehicle vehicle,Expected e,Func<bool> current)=>e!=null&&AllowVerify&&current()&&NativeImage!=null&&NativeImage.SequenceEqual(e.Entity)&&current();
}
// ACTUAL_COMMAND
// ACTUAL_JOURNAL
// ACTUAL_FUEL
internal static partial class RebirthCamperStationFuelOriginal{
 internal static void FixtureCrash(EntityVehicle v){if(pending.TryGetValue(v,out var p))p.Expected?.Dispose();pending.Remove(v);restored.Remove(v);}
 internal static void FixtureStep(EntityVehicle v){UnityEngine.Time.realtimeSinceStartup+=1;RestorePending(v);}
 internal static void FixtureRestoreBefore(EntityVehicle v){var p=pending[v];v.Runtime.Stations.Installed.State=RebirthCamperStationSnapshot.Restore(p.Before.Station,v.Runtime.Stations.Installed.InstalledItem,p.Before.Image);v.Runtime.Stations.Installed.State.BoundVehicle=v;}
}
public static class Program{
 static EntityVehicle vehicle;static EntityPlayer actor;static RebirthCamperStationState state;static RebirthCamperStationAddress address;static RebirthCamperRemoteStationCommand command;static string root;
 static void Need(bool v,string text){if(!v)throw new Exception(text);}
 static void Setup(){
  GameIO.Save=Path.Combine(Path.GetTempPath(),"rebirth-mp071-native-adapter-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(GameIO.Save);
  var world=new World();vehicle=new EntityVehicle{world=world};world.Vehicle=vehicle;GameManager.Instance.World=world;actor=new EntityPlayer{world=world};world.Actor=actor;
  state=new RebirthCamperStationState{InstallationId=Guid.NewGuid(),BoundVehicle=vehicle};vehicle.Runtime=new RebirthCamperModuleRuntime{Stations=new Stations{Installed=new Installed{State=state}}};
  address=new RebirthCamperStationAddress(vehicle.Runtime.Identity,state.InstallationId);command=RebirthCamperRemoteStationCommand.Fuel(Guid.NewGuid(),true);root=Path.Combine(GameIO.Save,"REBIRTH","CamperFuelOriginals",address.Vehicle.ToString("N"));
  vehicle.Runtime.SerializedImage=RebirthCamperStationSnapshot.Capture(state.InstallationId,state);
  ThreadManager.Main=true;RebirthCamperRemoteStationTransport.Current=true;RebirthCamperSavedVehicle.AllowSave=true;RebirthCamperSavedVehicle.AllowVerify=true;RebirthCamperSavedVehicle.NativeImage=RebirthCamperSavedVehicle.Capture(vehicle).Entity;RebirthCamperSavedVehicle.Saves=0;RebirthCamperSavedVehicle.OnSave=null;
  RebirthCamperEnergyRuntime.Held=false;RebirthCamperEnergyCustody.Record=null;
  RebirthSurvivorMode.Enabled=false;RebirthCamperStationWindowSession.Projections=0;
 }
 static bool Begin()=>RebirthCamperStationFuelOriginal.Begin(actor,state,address,"creation",RebirthStationLiveAccess.Lease,command);
 static RebirthCamperRemoteStationCommandStatus.State Status()=>RebirthCamperStationFuelOriginal.ReadStatus(actor,address,"creation",command,out _,out _);
 static void Step(){RebirthCamperStationFuelOriginal.FixtureStep(vehicle);}
 static void Finish(){for(int n=0;n<8;n++)Step();Need(Status()==RebirthCamperRemoteStationCommandStatus.State.Applied&&!RebirthCamperStationFuelOriginal.IsHeld(state)&&state.IsBurning&&state.Stock==50,"original did not settle/release");}
 static string OriginalPath(int phase)=>Path.Combine(root,"journal",command.Operation.ToString("N")+"."+phase+".rcj");
 static Recipe SetupInput(bool local=false){Setup();if(local){actor=new EntityPlayerLocal{world=vehicle.world};vehicle.world.Actor=actor;}state.Input[0]=new ItemStack(new ItemValue{type=10},10);state.Input[0].itemValue.SetMetadata("paint","red");state.Input[1]=new ItemStack(new ItemValue{type=10},6);state.Input[1].itemValue.SetMetadata("paint","blue");var recipe=new Recipe();recipe.ingredients.Add(new ItemStack(new ItemValue{type=10},2));Guid op=Guid.NewGuid();command=new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Craft,op,op,recipe.GetName(),3,0);vehicle.Runtime.Persist(true);RebirthCamperSavedVehicle.NativeImage=RebirthCamperSavedVehicle.Capture(vehicle).Entity;return recipe;}
 static bool Craft(Recipe recipe,Action entered=null)=>RebirthCamperStationNativeCrafting.Craft(actor,address,"creation",command.Job,recipe,command.Batches,command.Tier,command,entered);
 static void SettleInput(){for(int n=0;n<8;n++)Step();Need(Status()==RebirthCamperRemoteStationCommandStatus.State.Applied&&!RebirthCamperStationFuelOriginal.IsHeld(state),"input original did not save/release");}
 static void InputCases(){
  var recipe=SetupInput();int entered=0;Need(Craft(recipe,()=>entered++)&&entered==1&&state.Input[0].count==10&&state.Queue.All(r=>r.Recipe==null),"native Craft changed input before durable intent/save");SettleInput();Need(state.Input[0].count==4&&state.Input[1].count==6&&state.Queue[1].Multiplier==3&&state.Queue[1].Recipe.ingredients[0].itemValue.TryGetMetadata("paint",out string color)&&color=="red"&&RebirthCamperStationWindowSession.Projections==1,"stock/queue/rich fragments not coupled");
  Guid job=command.Job;long revision=state.Revision;Need(!Craft(recipe)&&state.Input[0].count==4&&state.Revision==revision,"same input job debited twice");
  state.Queue[1].Multiplier=2;command=new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Cancel,Guid.NewGuid(),job,"",0,0);Need(RebirthCamperStationNativeCrafting.Cancel(actor,address,"creation",job,command)&&state.Input[0].count==4&&state.Queue[1].Recipe!=null,"native cancel refunded before original saved boundary");SettleInput();Need(state.Input[0].count==8&&state.Input[1].count==6&&state.Queue.All(r=>r.Recipe==null)&&RebirthCamperStationWindowSession.Projections==2,"partial native cancellation returned wrong suffix");
  recipe=SetupInput(true);Need(Craft(recipe),"local input route not admitted");RebirthCamperStationFuelOriginal.FixtureCrash(vehicle);SettleInput();Need(state.Input[0].count==4,"cold Prepared lost local input original");
  recipe=SetupInput();Need(Craft(recipe),"input Uncertain admission");Action<string,RebirthCamperTransferJournal.Record,bool> stop=(dir,record,published)=>{if(published&&dir==Path.Combine(root,"journal")&&record.State==RebirthCamperTransferJournal.Phase.Uncertain)throw new IOException("entered input rename");};RebirthCamperTransferJournal.PublicationFinished+=stop;Step();RebirthCamperTransferJournal.PublicationFinished-=stop;Need(File.Exists(OriginalPath(1))&&state.Input[0].count==10,"input entered boundary applied early");RebirthCamperStationFuelOriginal.FixtureCrash(vehicle);SettleInput();Need(state.Input[0].count==4,"cold Uncertain repeated/missed debit");
  recipe=SetupInput();Need(Craft(recipe),"input partial metadata admission");bool failed=false;vehicle.Runtime.PersistHook=()=>{if(state.Queue.Any(r=>r.Recipe!=null)&&!failed){failed=true;throw new IOException("input After metadata");}};Step();Need(failed&&state.Input[0].count==4&&Status()==RebirthCamperRemoteStationCommandStatus.State.Pending,"input Persist failure became saved terminal");SettleInput();Need(state.Input[0].count==4,"retry debited input again");
  recipe=SetupInput();Need(Craft(recipe),"old input command admission");SettleInput();state.Queue[1]=new RecipeQueueItem();state.Input[0].count=20;vehicle.Runtime.Persist(true);Need(!Craft(recipe)&&state.Input[0].count==20&&state.Queue.All(r=>r.Recipe==null)&&Status()==RebirthCamperRemoteStationCommandStatus.State.Applied,"completed old Craft replayed after queue emptied");
  recipe=SetupInput();var wrong=new RebirthCamperRemoteStationCommand(command.Kind,Guid.NewGuid(),command.Job,command.RecipeName,command.Batches,0);Need(!RebirthCamperStationNativeCrafting.Craft(actor,address,"creation",command.Job,recipe,3,0,wrong)&&state.Input[0].count==10,"noncanonical original/job invoked source debit");
  recipe=SetupInput();Need(Craft(recipe),"full cancel grid setup");SettleInput();job=command.Job;state.Input[0].count=100;state.Input[1].count=100;state.Input[2]=new ItemStack(new ItemValue{type=11},100);command=new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Cancel,Guid.NewGuid(),job,"",0,0);Need(!RebirthCamperStationNativeCrafting.Cancel(actor,address,"creation",job,command)&&state.Queue[1].Recipe!=null&&!File.Exists(OriginalPath(0)),"full cancellation grid published partial refund");
  // Material allocation planning is an explicit adapter in this fixture. These
  // cases compose actual material paid/refund arithmetic and cold delta proof.
  Setup();state.MaterialNames=new[]{"iron"};state.Input=ItemStack.CreateArray(4);state.Input[3]=new ItemStack(new ItemValue{type=99,Value="unit_iron"},100000);var paid=state.Input[3].Clone();paid.count=1;Guid materialJob=Guid.NewGuid();paid.itemValue.SetMetadata("rebirth.camper.native.job",materialJob.ToString("N"));paid.itemValue.SetMetadata("rebirth.camper.native.batches",3);paid.itemValue.SetMetadata("rebirth.camper.native.units",6);paid.itemValue.SetMetadata("rebirth.camper.native.material","unit_iron");paid.itemValue.SetMetadata("rebirth.camper.native.slot",3);var materialRecipe=new Recipe();materialRecipe.ingredients.Add(paid);var nextInput=ItemStack.Clone(state.Input);nextInput[3].count-=6;var nextQueue=(RecipeQueueItem[])state.Queue.Clone();nextQueue[1]=new RecipeQueueItem{Recipe=materialRecipe,Multiplier=3,Quality=0,CraftingTimeLeft=2,OneItemCraftTime=2,StartingEntityId=actor.entityId,IsCrafting=true};command=new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Craft,materialJob,materialJob,materialRecipe.GetName(),3,0);Need(RebirthCamperStationFuelOriginal.Begin(actor,state,address,"creation",RebirthStationLiveAccess.Lease,command,null,nextInput,nextQueue),"material original delta refused");SettleInput();Need(state.Input[3].count==99994,"material wide count truncated");state.Queue[1].Multiplier=2;command=new RebirthCamperRemoteStationCommand(RebirthCamperRemoteStationCommandKind.Cancel,Guid.NewGuid(),materialJob,"",0,0);Need(RebirthCamperStationNativeCrafting.Cancel(actor,address,"creation",materialJob,command),"material partial cancel refused");SettleInput();Need(state.Input[3].count==99998,"material refund repeated consumed prefix or lost width");
 }
 public static void Main(){
  Setup();Need(Begin()&&!state.IsBurning&&Status()==RebirthCamperRemoteStationCommandStatus.State.Pending,"intent changed fuel before native proof");
  RebirthCamperSavedVehicle.AllowVerify=false;Step();Need(!state.IsBurning&&RebirthCamperStationFuelOriginal.IsHeld(state),"unread native file granted effects");RebirthCamperSavedVehicle.AllowVerify=true;Finish();
  long rev=state.Revision;int saves=RebirthCamperSavedVehicle.Saves;Need(!Begin()&&state.Revision==rev&&RebirthCamperSavedVehicle.Saves==saves,"duplicate fuel replayed");
  command=RebirthCamperRemoteStationCommand.Fuel(Guid.NewGuid(),false);Need(Begin(),"second original refused");for(int n=0;n<8;n++)Step();Need(Status()==RebirthCamperRemoteStationCommandStatus.State.Applied&&!state.IsBurning,"second original did not settle");
  var oldId=Directory.EnumerateFiles(Path.Combine(root,"journal"),"*.0.rcj").Select(p=>Guid.ParseExact(Path.GetFileName(p).Substring(0,32),"N")).First(id=>id!=command.Operation);command=RebirthCamperRemoteStationCommand.Fuel(oldId,true);Need(!Begin()&&!state.IsBurning&&Status()==RebirthCamperRemoteStationCommandStatus.State.Applied,"old On replay overrode later Off");
  Setup();Need(Begin(),"cold Prepared admission");RebirthCamperStationFuelOriginal.FixtureCrash(vehicle);Finish();
  Setup();Need(Begin(),"after-save interruption admission");bool injected=false;Action<string,RebirthCamperTransferJournal.Record,bool> crash=(dir,record,published)=>{if(!injected&&published&&dir==Path.Combine(root,"journal")&&record.State==RebirthCamperTransferJournal.Phase.Applied){injected=true;throw new IOException("after terminal rename");}};
  RebirthCamperTransferJournal.PublicationFinished+=crash;for(int n=0;n<8;n++)Step();RebirthCamperTransferJournal.PublicationFinished-=crash;Need(injected&&Status()==RebirthCamperRemoteStationCommandStatus.State.Applied,"saved terminal boundary lost");RebirthCamperStationFuelOriginal.FixtureCrash(vehicle);Step();Need(!RebirthCamperStationFuelOriginal.IsHeld(state),"terminal cold release failed");
  Setup();Need(Begin(),"Uncertain admission");Action<string,RebirthCamperTransferJournal.Record,bool> stop=(dir,record,published)=>{if(published&&dir==Path.Combine(root,"journal")&&record.State==RebirthCamperTransferJournal.Phase.Uncertain)throw new IOException("entered rename");};
  RebirthCamperTransferJournal.PublicationFinished+=stop;Step();RebirthCamperTransferJournal.PublicationFinished-=stop;Need(File.Exists(OriginalPath(1))&&!state.IsBurning,"Uncertain boundary not reached");RebirthCamperStationFuelOriginal.FixtureCrash(vehicle);Finish();
  Setup();Need(Begin(),"mismatch admission");state.Stock++;for(int n=0;n<4;n++)Step();Need(!state.IsBurning&&RebirthCamperStationFuelOriginal.IsHeld(state)&&Status()!=RebirthCamperRemoteStationCommandStatus.State.Applied,"different stock overwritten");
  Setup();Need(Begin(),"native metadata retry admission");bool persistFailed=false;vehicle.Runtime.PersistHook=()=>{if(state.IsBurning&&!persistFailed){persistFailed=true;throw new IOException("live After but native metadata Before");}};Step();Need(persistFailed&&state.IsBurning&&Status()==RebirthCamperRemoteStationCommandStatus.State.Pending,"failed native metadata became Applied");Finish();Need(RebirthCamperSavedVehicle.NativeImage.SequenceEqual(RebirthCamperSavedVehicle.Capture(vehicle).Entity),"After native metadata was not republished before save");
  Setup();Need(Begin(),"native body observation admission");RebirthCamperSavedVehicle.AllowVerify=false;Step();int oldSaves=RebirthCamperSavedVehicle.Saves;vehicle.NativeBody=8;RebirthCamperSavedVehicle.AllowVerify=true;Finish();Need(vehicle.NativeBody==8&&RebirthCamperSavedVehicle.Saves>oldSaves,"changed native body restored or observation never refreshed");
  Setup();Need(Begin(),"staged original admission");File.WriteAllText(OriginalPath(1)+".unknown.tmp","retained");RebirthCamperStationFuelOriginal.FixtureCrash(vehicle);Need(!RebirthCamperStationFuelOriginal.RestorePending(vehicle)&&RebirthCamperStationFuelOriginal.IsHeld(state)&&File.Exists(OriginalPath(1)+".unknown.tmp"),"unknown staged data discarded or admitted");
  Setup();Need(Begin(),"false extension admission");File.WriteAllText(Path.Combine(root,"journal",Guid.NewGuid().ToString("N")+".0.xyz"),"retained");RebirthCamperStationFuelOriginal.FixtureCrash(vehicle);Need(!RebirthCamperStationFuelOriginal.RestorePending(vehicle),"foreign extension ignored");
  Setup();Need(Begin(),"owner scope admission");Finish();actor.Died++;Need(Status()==RebirthCamperRemoteStationCommandStatus.State.Applied,"station-only terminal required old actor life");actor.world.worldState.Guid="foreign";Need(Status()==RebirthCamperRemoteStationCommandStatus.State.Unknown,"foreign World acquired terminal");
  Setup();RebirthCamperRemoteStationTransport.Current=false;Need(!Begin()&&!state.IsBurning,"unauthenticated intent published");
  Setup();RebirthCamperEnergyRuntime.Held=true;Need(!Begin()&&!state.IsBurning,"fuel acquired native vehicle during energy hold");
  Setup();Need(Begin(),"mutual native save admission");RebirthCamperSavedVehicle.AllowVerify=false;var energy=new EnergyMutator(vehicle);Need(!energy.Hold(Guid.NewGuid())&&!energy.Prepare()&&!energy.AttemptCommit()&&energy.Writes==0,"energy mutation crossed fuel native save custody");energy.Tick();Need(energy.Suspensions==1&&energy.Writes==0,"energy Tick spent during fuel custody");RebirthCamperSavedVehicle.AllowVerify=true;Finish();Need(energy.Hold(Guid.NewGuid()),"settled fuel permanently blocked energy");
  Setup();Need(Begin(),"definition admission");vehicle.Runtime.Stations.Installed.InstalledItem.itemValue.Value="changed";for(int n=0;n<3;n++)Step();Need(!state.IsBurning&&RebirthCamperStationFuelOriginal.IsHeld(state),"changed station definition executed");
  InputCases();
  Console.WriteLine("PASS actual station-owned fuel/input producers, WAL/proof/recovery/status source with real journal IO; native save adapters are not native gameplay evidence");
 }
}
