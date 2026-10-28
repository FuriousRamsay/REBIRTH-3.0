// AUTHORED_NOT_RUN. Actual original WAL, journal, checkpoint and full credit plan.
// Explicit boundary adapters: native item codec, PDS serialization, custody
// authentication and native setters. This cohort cannot qualify live grants.
using System;using System.IO;using System.Linq;using System.Text;using System.Xml;using System.Xml.Linq;using System.Collections.Generic;using System.Security.Cryptography;using System.Globalization;
public static class RebirthCamperTransferHistoryScope{public static bool TryGet(RebirthCamperTransferJournal journal,out History index){index=null;return false;}public class History{public RebirthCamperTransferJournal.Record FindPending(Guid vehicle)=>null;public IEnumerable<RebirthCamperTransferJournal.Record> FindApplied(Guid vehicle,string owner)=>new RebirthCamperTransferJournal.Record[0];public IEnumerable<RebirthCamperTransferJournal.Record> FindRejected(Guid vehicle,string owner)=>new RebirthCamperTransferJournal.Record[0];}}
public enum RebirthVehiclePartSourceLocation:byte{None,Backpack,Toolbelt}
public enum RebirthVehicleOwnerTransferResult{Pending,Applied,Rejected,Indeterminate}
public enum RebirthVehicleInventoryPlanResult{Ready,Pending,Rejected}
public class ItemClass{public int MaxCount=100;}
public class ItemValue{public string Data="item";public ItemClass ItemClass=new ItemClass();public ItemValue Clone()=>new ItemValue{Data=Data};}
public class ItemStack{public ItemValue itemValue;public int count;public ItemStack(ItemValue value,int amount){itemValue=value;count=amount;}public static ItemStack Empty=new ItemStack(new ItemValue{Data="empty"},0);public bool IsEmpty()=>count==0;public ItemStack Clone()=>new ItemStack(itemValue.Clone(),count);public bool CanMoveTo(XUiC_ItemStack.StackLocationTypes location)=>true;public bool CanStackPartlyWith(ItemStack incoming,out int available){available=100-count;return itemValue.Data==incoming.itemValue.Data&&available>0;}}
public class XUiC_ItemStack{public enum StackLocationTypes{Backpack,ToolBelt}}
public static class RebirthNativeItemCodec{public static string Encode(ItemValue value)=>value.Data;public static bool TryDecode(string text,out ItemValue value){value=string.IsNullOrEmpty(text)?null:new ItemValue{Data=text};return value!=null;}}
public static class ThreadManager{public static bool IsMainThread()=>true;}
public static class GameIO{public static string Root;public static string GetPlayerDataDir()=>Root;}
public class Grid{public ItemStack[] items;public bool[] SlotLocks=new bool[2];public bool IsLocked(int index)=>SlotLocks!=null&&index<SlotLocks.Length&&SlotLocks[index];}
public class Bag{public Grid ItemGrid=new Grid();public bool[] LockedSlots=new bool[2];}
public class Inventory{public Grid ItemGrid=new Grid();}
public class Buffs{public Dictionary<string,float> Values=new Dictionary<string,float>();public Action<float> AfterWrite;public float GetCustomVar(string key)=>Values.TryGetValue(key,out var value)?value:0f;public void SetCustomVar(string key,float value,bool sync){Values[key]=value;AfterWrite?.Invoke(value);}}
public class World{public string Guid=System.Guid.NewGuid().ToString();public EntityPlayerLocal Player;public EntityPlayerLocal GetPrimaryPlayer()=>Player;}
public class EntityPlayerLocal{public World world;public int Died;public Bag bag=new Bag();public Inventory inventory=new Inventory();public Buffs Buffs=new Buffs();}
public class GameManager{public static GameManager Instance;public World World;public bool SaveEnabled=true;public void SaveLocalPlayerData(){if(SaveEnabled)RebirthGearNativePlayerFile.Capture(World.Player);}}
public class Identity{public string CanonicalId="owner",StorageKey="owner";}
public static class RebirthStablePlayerIdentity{public static bool TryFromLocalPlatform(out Identity identity){identity=new Identity();return true;}}
public class NativeFile{public int deaths;public byte[] buffData;public ItemStack[] Bag,Belt;}
public static class RebirthGearNativePlayerFile{
 public static NativeFile File;public static Dictionary<Guid,RebirthVehicleOwnerTransferResult> Receipts=new Dictionary<Guid,RebirthVehicleOwnerTransferResult>();
 public static bool TryRead(Identity identity,out NativeFile file){file=File;return file!=null;}
 public static void Capture(EntityPlayerLocal player){Receipts=new Dictionary<Guid,RebirthVehicleOwnerTransferResult>();foreach(var pair in player.Buffs.Values){if(!pair.Key.StartsWith("rbVehicle_"))continue;var id=Guid.ParseExact(pair.Key.Substring(10),"N");Receipts[id]=pair.Value==1f?RebirthVehicleOwnerTransferResult.Applied:pair.Value==-1f?RebirthVehicleOwnerTransferResult.Rejected:pair.Value==2f?RebirthVehicleOwnerTransferResult.Indeterminate:RebirthVehicleOwnerTransferResult.Pending;}File=new NativeFile{deaths=player.Died,buffData=new byte[]{1},Bag=RebirthVehicleInventoryWrite.Snapshot(player.bag.ItemGrid.items),Belt=RebirthVehicleInventoryWrite.Snapshot(player.inventory.ItemGrid.items)};}
}
public static class RebirthGearPlayerDataInventory{public static ItemStack[] ReadSlots(NativeFile file,bool bag)=>bag?file.Bag:file.Belt;}
public static class RebirthVehicleReceiptReader{public static bool TryRead(byte[] data,IEnumerable<Guid> ids,out Dictionary<Guid,RebirthVehicleOwnerTransferResult> result){result=ids.ToDictionary(id=>id,id=>RebirthGearNativePlayerFile.Receipts.TryGetValue(id,out var value)?value:RebirthVehicleOwnerTransferResult.Pending);return data.Length>0;}}
public static class RebirthToolbeltCapacity{public static int GetOwnedSlotCount(EntityPlayerLocal player,int length)=>length;}
public static class RebirthStationGridIngredients{public static bool IsSameStackSnapshot(ItemStack left,ItemStack right)=>left!=null&&right!=null&&left.count==right.count&&left.itemValue.Data==right.itemValue.Data;}
public static class RebirthVehicleInventoryWrite{public static ItemStack[] Snapshot(ItemStack[] cells)=>cells?.Select(cell=>cell.Clone()).ToArray();public static bool SameSlots(ItemStack[] left,ItemStack[] right)=>left!=null&&right!=null&&left.Length==right.Length&&left.Select((cell,index)=>RebirthStationGridIngredients.IsSameStackSnapshot(cell,right[index])).All(equal=>equal);}
public class RebirthVehicleTransferPlan{public string ActorId="owner",CreationId="",OwnerScope="native-life:0";public Guid TransactionId=Guid.NewGuid();public List<RebirthVehicleTransferItem> Items=new List<RebirthVehicleTransferItem>();}
public static class RebirthVehicleOwnerCustody{
 public static bool Current=true;public static int Writes;public static Action AfterWrite;
 public static bool Matches(EntityPlayerLocal player,RebirthVehicleTransferPlan plan)=>Current&&ReferenceEquals(player,GameManager.Instance.World.Player);
 public static void WriteSlots(EntityPlayerLocal player,bool bag,ItemStack[] cells,IEnumerable<RebirthVehicleTransferItem> operations,Func<bool> current){if(!current())throw new Exception("revoked");if(bag)player.bag.ItemGrid.items=cells;else player.inventory.ItemGrid.items=cells;Writes++;AfterWrite?.Invoke();}
}
public static class RebirthCamperInventorySnapshot{
 public static byte[] Capture(ItemStack[] slots){using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){writer.Write(slots.Length);foreach(var stack in slots){writer.Write(stack.count);writer.Write(stack.itemValue.Data);}return stream.ToArray();}}
 public static ItemStack[] Restore(byte[] bytes){using(var reader=new BinaryReader(new MemoryStream(bytes))){var cells=new ItemStack[reader.ReadInt32()];for(int index=0;index<cells.Length;index++){int count=reader.ReadInt32();cells[index]=new ItemStack(new ItemValue{Data=reader.ReadString()},count);}return cells;}}
}
// ACTUAL_PRODUCTION
public static class Test{
 static int assertions;static void Need(bool value,string message){assertions++;if(!value)throw new Exception(message);}
 static ItemStack Stack(int count,string data="item")=>new ItemStack(new ItemValue{Data=data},count);
 static EntityPlayerLocal Fresh(){var player=new EntityPlayerLocal();player.bag.ItemGrid.items=new[]{Stack(10),Stack(20)};player.inventory.ItemGrid.items=new[]{Stack(0,"empty"),Stack(0,"empty")};player.world=new World{Player=player};GameManager.Instance=new GameManager{World=player.world};RebirthVehicleOwnerCustody.Current=true;RebirthVehicleOwnerCustody.Writes=0;RebirthVehicleOwnerCustody.AfterWrite=null;return player;}
 static RebirthVehicleTransferItem Debit(int slot,int count)=>new RebirthVehicleTransferItem(Guid.NewGuid(),true,"item",count,RebirthVehiclePartSourceLocation.Backpack,slot);
 static void Stage(EntityPlayerLocal player,IEnumerable<RebirthVehicleTransferItem> items,float stage){foreach(var item in items)player.Buffs.SetCustomVar("rbVehicle_"+item.ReceiptId.ToString("N"),stage,true);}
 static RebirthVehicleTransferPlan Prepare(EntityPlayerLocal player,out RebirthVehicleTransferItem[] items,out ItemStack[] after){items=new[]{Debit(0,3),Debit(1,4)};after=new[]{Stack(7),Stack(16)};var plan=new RebirthVehicleTransferPlan();plan.Items.AddRange(items);Need(RebirthVehicleOwnerOriginal.Prepare(player,plan,items,true,after),"actual WAL preparation");Stage(player,items,2f);RebirthGearNativePlayerFile.Capture(player);return plan;}
 public static void Main(string[] args){GameIO.Root=Path.Combine(args[0],"native-player");Directory.CreateDirectory(GameIO.Root);
  var player=Fresh();var plan=Prepare(player,out var items,out var after);player.bag.ItemGrid.items[0]=after[0].Clone();
  Need(RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out var retry)&&!retry,"saved applying prefix finishes");Need(RebirthVehicleInventoryWrite.SameSlots(after,player.bag.ItemGrid.items)&&RebirthVehicleOwnerCustody.Writes==1,"exact remaining debit postimage");Need(RebirthGearNativePlayerFile.Receipts.Values.All(value=>value==RebirthVehicleOwnerTransferResult.Applied),"native terminal checkpoint");
  Need(RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&!retry&&RebirthVehicleOwnerCustody.Writes==1,"saved complete original never debits again");
  player=Fresh();plan=Prepare(player,out items,out after);player.bag.ItemGrid.items[0]=Stack(8);Need(!RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&RebirthVehicleOwnerCustody.Writes==0,"third count retained");
  player=Fresh();plan=Prepare(player,out items,out after);player.bag.ItemGrid.items[0]=Stack(7,"different");Need(!RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&RebirthVehicleOwnerCustody.Writes==0,"third metadata retained");
  player=Fresh();plan=Prepare(player,out items,out after);Need(RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&retry&&RebirthVehicleOwnerCustody.Writes==0,"saved full before returns original retry");
  player=Fresh();plan=Prepare(player,out items,out after);player.bag.ItemGrid.items[0]=after[0].Clone();GameManager.Instance.SaveEnabled=false;Need(!RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&RebirthVehicleOwnerCustody.Writes==0,"unsaved observed prefix cannot write");
  player=Fresh();plan=Prepare(player,out items,out after);player.bag.ItemGrid.items[0]=after[0].Clone();RebirthGearNativePlayerFile.Receipts[items[0].ReceiptId]=RebirthVehicleOwnerTransferResult.Applied;Need(!RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&RebirthVehicleOwnerCustody.Writes==0,"mixed applied receipt cannot authorize partial replay");
  player=Fresh();plan=Prepare(player,out items,out after);player.bag.ItemGrid.items=RebirthVehicleInventoryWrite.Snapshot(after);Stage(player,items,1f);Need(RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&!retry&&RebirthVehicleOwnerCustody.Writes==0,"live complete after finishes only metadata");
  player=Fresh();plan=Prepare(player,out items,out after);player.bag.ItemGrid.items[0]=after[0].Clone();RebirthVehicleOwnerCustody.AfterWrite=()=>RebirthVehicleOwnerCustody.Current=false;Need(!RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&RebirthVehicleOwnerCustody.Writes==1,"native scope revocation retains unknown outcome");
  player=Fresh();var credit=new RebirthVehicleTransferItem(Guid.NewGuid(),false,"item",110,RebirthVehiclePartSourceLocation.None,-1);Need(RebirthVehicleOwnerCreditPlan.Prepare(new[]{Stack(90)},new[]{Stack(90)},1,credit,out var creditedBag,out var creditedBelt)==RebirthVehicleInventoryPlanResult.Pending&&creditedBag==null,"insufficient combined capacity has no candidate");
  credit=new RebirthVehicleTransferItem(Guid.NewGuid(),false,"item",20,RebirthVehiclePartSourceLocation.None,-1);Need(RebirthVehicleOwnerCreditPlan.Prepare(new[]{Stack(90)},new[]{Stack(90)},1,credit,out creditedBag,out creditedBelt)==RebirthVehicleInventoryPlanResult.Ready&&creditedBag[0].count==100&&creditedBelt[0].count==100,"bag then owned belt credit");Need(RebirthVehicleOwnerCreditPlan.Prepare(new[]{Stack(90)},new[]{Stack(90)},0,credit,out creditedBag,out creditedBelt)==RebirthVehicleInventoryPlanResult.Pending,"hidden belt excluded");
  player=Fresh();plan=Prepare(player,out items,out after);player.bag.ItemGrid.items=RebirthVehicleInventoryWrite.Snapshot(after);player.Buffs.AfterWrite=stage=>{if(stage==1f)player.inventory.ItemGrid.items[0]=Stack(1,"unrelated");};
  Need(!RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&RebirthVehicleOwnerCustody.Writes==0,"terminal metadata callback cannot qualify foreign opposite grid");player.Buffs.AfterWrite=null;
  Need(!RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&RebirthVehicleOwnerCustody.Writes==0,"live terminal marker alone cannot qualify foreign opposite grid on retry");
  player=Fresh();string beltPolicy=RebirthVehicleNativePolicy.Capture(player,false);player.inventory.ItemGrid.SlotLocks[0]=true;Need(RebirthVehicleNativePolicy.Capture(player,false)!=beltPolicy,"actual native belt lock captured");
  credit=new RebirthVehicleTransferItem(Guid.NewGuid(),false,"item",1,RebirthVehiclePartSourceLocation.None,-1);Need(RebirthVehicleOwnerCreditPlan.Prepare(new[]{Stack(100)},new[]{Stack(0,"empty")},1,credit,out creditedBag,out creditedBelt,index=>false,index=>true)==RebirthVehicleInventoryPlanResult.Pending&&creditedBag==null,"locked belt destination excluded");
  Need(RebirthVehicleOwnerCreditPlan.Prepare(new[]{Stack(0,"empty")},new[]{Stack(100)},1,credit,out creditedBag,out creditedBelt,index=>true,index=>false)==RebirthVehicleInventoryPlanResult.Pending,"locked bag destination excluded");
  player=Fresh();plan=Prepare(player,out items,out after);player.bag.ItemGrid.items[0]=after[0].Clone();player.bag.ItemGrid.SlotLocks[1]=true;Need(!RebirthVehicleOwnerOriginal.ResolveApplying(player,plan,items,out retry)&&RebirthVehicleOwnerCustody.Writes==0,"protected native source original refuses continuation");
  Console.WriteLine("PASS "+assertions+" actual original/checkpoint/journal/credit/policy assertions; explicit native file, codec, setter and custody adapters; live grants NOT qualified.");
 }
}
