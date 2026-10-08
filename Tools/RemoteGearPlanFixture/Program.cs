using System;using System.Collections.Generic;using System.Linq;
using Stack=RebirthGearInventoryPlan.Stack;
public class ItemClass {public string Name;public string GetItemName()=>Name;}
public class ItemValue {public int type;public ushort Seed;public ItemClass ItemClass;}
public class EntityPlayer { public int entityId=7; }
public class ClientInfo {}
public class Attr {public float Current=10;}
public class Progress {public Dictionary<string,Attr> Attributes=new Dictionary<string,Attr>{{"strength",new Attr()},{"constitution",new Attr()}};}
public class RebirthWorldSupportState {public RebirthGearSettlement LastGearSettlement;public RebirthGearTransferState LastGearSettlementOriginal;public long GearRevision=2;public RebirthGearTransferState PendingGearTransfer;public object PendingLibraryTransfer,PendingMusicTransfer;public RebirthGearTransferPhase GearTransferPhase;public Dictionary<string,string> EquippedGearBySlot=new Dictionary<string,string>(),EquippedGearItemDataBySlot=new Dictionary<string,string>();}
public class Origin {public string CreationId="11111111111111111111111111111111";}
public class RebirthWorldCharacterRecord {public string StablePlayerKey="owner";public RebirthWorldSupportState Support=new RebirthWorldSupportState();public Origin Origin=new Origin();public Progress Progression=new Progress();}
public class Profile {public string Kind="survivor_gear",GearSlotId;public float GearMinStrength=5,GearMinConstitution=5;public int GearToolbeltSlotBonus;}
public class Metabolism {public object HydrationSlotItem;}
public static class RebirthMetabolismStateRepository {public static bool TryGet(EntityPlayer p,out M m){m=null;return false;}public class M {public S HydrationSlotItem;}public class S {public bool IsEmpty()=>false;}}
public static class RebirthSurvivorDefinitionRegistry {public static Dictionary<string,Profile> Profiles=new Dictionary<string,Profile>{{"pack",new Profile{GearSlotId="backpack"}},{"biggerPack",new Profile{GearSlotId="backpack"}},{"belt",new Profile{GearSlotId="belt",GearToolbeltSlotBonus=2}}};public static bool TryGetSupportByGearItem(string s,out Profile p)=>Profiles.TryGetValue(s,out p);}
public static class RebirthWorldCharacterService {public static RebirthWorldCharacterRecord Record;public static RebirthStablePlayerIdentity Identity=new RebirthStablePlayerIdentity();public static int Dirty;public static bool TryGetIdentity(EntityPlayer p,out RebirthStablePlayerIdentity i){i=Identity;return i!=null;}public static void MarkDirty(RebirthWorldCharacterRecord r,string why){Dirty++;}public static bool TryGet(EntityPlayer p,out RebirthWorldCharacterRecord r){r=Record;return r!=null;}}
public static class RebirthSurvivorGearService {public const string SupportSlotId="support",BeltSlotId="belt";public static int GetDesiredPhysicalBagSlots(RebirthWorldCharacterRecord r,string slot,string id)=>slot=="backpack"?(id==null?52:id=="biggerPack"?78:65):52;public static int GetToolbeltBonus(EntityPlayer p)=>0;}
public static class RebirthToolbeltCapacity {public const int MaximumSlots=18;public static int GetSlotsForLevel(int n)=>4;}
public static class RebirthBackgroundStorageService {public static int ToolbeltBonus(EntityPlayer p)=>0;}
public static class RebirthNativeItemCodec {public static bool TryDecode(string s,out ItemValue v){v=null;if(s=="AQ==")v=new ItemValue{type=1,Seed=3,ItemClass=new ItemClass{Name="pack"}};if(s=="Ag==")v=new ItemValue{type=2,Seed=3,ItemClass=new ItemClass{Name="belt"}};if(s=="Aw==")v=new ItemValue{type=3,Seed=3,ItemClass=new ItemClass{Name="biggerPack"}};return v!=null;}}
public static class RebirthSurvivorRequestScope {public static bool Matches(string a,string b)=>a==b&&!string.IsNullOrEmpty(a);public static bool TryNormalize(string s,out string n){n=s;return Guid.TryParse(s,out var g)&&g!=Guid.Empty;}}
public class RebirthGearInventorySnapshot {public static bool TryCaptureEncoded(Stack[] bag,Stack[] belt,int owned,out RebirthGearInventorySnapshot s){s=null;if(!RebirthGearEncodedSnapshot.TryCopy(bag,belt,owned,out var b,out var t))return false;s=new(){Bag=b,Belt=t,OwnedBeltSlots=owned};return true;}public Stack[] Bag,Belt;public int OwnedBeltSlots=4;public bool IsUsableSource(bool bag,int i)=>i>=0&&i<(bag?Bag.Length:OwnedBeltSlots);}
public static class RebirthRemoteGearInventorySource {public static bool TryResolve(EntityPlayer p,ClientInfo c,string creation,out RebirthWorldCharacterRecord r){r=RebirthWorldCharacterService.Record;return r!=null&&creation==r.Origin.CreationId;}public static RebirthGearInventorySnapshot Snapshot;public static bool TryCapture(EntityPlayer p,ClientInfo c,string creation,out RebirthGearInventorySnapshot s){s=Snapshot;return creation==RebirthWorldCharacterService.Record.Origin.CreationId&&s!=null;}}
public class RebirthStablePlayerIdentity {public string StorageKey="owner",CanonicalId="owner";public static bool TryFromClientInfo(ClientInfo c,out RebirthStablePlayerIdentity identity){identity=new();return c!=null;}}
public static class RebirthWorldCharacterRepository {
 public static bool Cached=true,Saved=true,ThrowAfterSave,ThrowBeforeSave;public static int Writes;public static Action OnSave;
 public static bool IsCurrentCachedRecord(RebirthWorldCharacterRecord r)=>Cached&&ReferenceEquals(r,RebirthWorldCharacterService.Record);
 public static bool SaveIfDirty(RebirthStablePlayerIdentity i,string why){Writes++;if(ThrowBeforeSave){Saved=false;throw new Exception("before write");}OnSave?.Invoke();if(ThrowAfterSave)throw new Exception("uncertain completed write");return Saved;}
 public static bool HasSavedGearTransfer(RebirthStablePlayerIdentity i,RebirthGearTransferState s,RebirthGearTransferPhase phase)=>Saved&&RebirthGearTransferSavedWitness.Matches(RebirthWorldCharacterService.Record.Support,s.CreationId,s,phase);
 public static bool HasSavedGearSettlement(RebirthStablePlayerIdentity i,RebirthGearSettlement s)=>Saved&&s!=null&&RebirthWorldCharacterService.Record.Support.PendingGearTransfer==null;
 public static void Reset(){Cached=true;Saved=true;ThrowAfterSave=false;ThrowBeforeSave=false;Writes=0;OnSave=null;RebirthWorldCharacterService.Identity=new RebirthStablePlayerIdentity();RebirthWorldCharacterService.Dirty=0;}
}
public static class RebirthGearPlayerFileWitness {public static bool Rejected=false;public static bool HasRejected(RebirthStablePlayerIdentity i,RebirthGearTransferState s)=>Rejected;public static bool Applied=true;public static bool HasApplied(RebirthStablePlayerIdentity i,RebirthGearTransferState s)=>Applied;}
public static class EntityBuffs {public const int Version=3;}
public class BuffValue {public void Read(MarkerReader r,int version){r.ReadInt32();}}
public sealed class MarkerReader:System.IDisposable {private System.IO.BinaryReader r;public void SetBaseStream(System.IO.Stream s){r=new System.IO.BinaryReader(s,System.Text.Encoding.UTF8,true);}public byte ReadByte()=>r.ReadByte();public ushort ReadUInt16()=>r.ReadUInt16();public int ReadInt32()=>r.ReadInt32();public string ReadString()=>r.ReadString();public float ReadSingle()=>r.ReadSingle();public void Dispose()=>r?.Dispose();}
public class MarkerPool {public MarkerReader AllocSync(bool reset)=>new MarkerReader();}
public static class MemoryPools {public static MarkerPool poolBinaryReader=new MarkerPool();}
public class NativeWorldState {public string Guid=System.Guid.NewGuid().ToString("N");}
public class World {public bool Remote;public NativeWorldState worldState=new();public bool IsRemote()=>Remote;}
public class GameManager {public static GameManager Instance=new();public World World=new();}
public class Clients {public ClientInfo Sender;public ClientInfo ForEntityId(int id)=>Sender;}
public class ConnectionManager {public bool IsServer=true;public Clients Clients=new();}
public static class SingletonMonoBehaviour<T> {public static T Instance;}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public static class RebirthGearPreparationPlayerFileWitness {public static bool Original=true;public static bool HasOriginal(RebirthStablePlayerIdentity owner,Guid world,string marker,RebirthGearPreparationIntent intent)=>Original;}
class Program {
 static int checks;static EntityPlayer player=new EntityPlayer();static ClientInfo sender=new ClientInfo();
 static Stack S(string v="",int n=0)=>new Stack{ItemData=v,Count=n};
 static void Reset(){ThreadManager.Main=true;GameManager.Instance=new();SingletonMonoBehaviour<ConnectionManager>.Instance=new(){Clients=new(){Sender=sender}};RebirthGearPreparationPlayerFileWitness.Original=true;RebirthGearPlayerFileWitness.Rejected=false;RebirthGearPlayerFileWitness.Applied=true;RebirthWorldCharacterRepository.Reset();RebirthWorldCharacterService.Record=new RebirthWorldCharacterRecord();RebirthRemoteGearInventorySource.Snapshot=new RebirthGearInventorySnapshot{Bag=Enumerable.Range(0,52).Select(_=>S()).ToArray(),Belt=Enumerable.Range(0,20).Select(_=>S()).ToArray()};RebirthRemoteGearInventorySource.Snapshot.Bag[7]=S("AQ==",2);}
 static bool Build(out RebirthGearTransferState offer,int type=1,long rev=2,string creation="11111111111111111111111111111111")=>RebirthRemoteGearEquipPlan.TryBuild(player,sender,creation,type,3,rev,out offer);
 static void Check(bool x,string why){if(!x)throw new Exception(why);checks++;}
 static void UnequipChecks(){
 Reset();var state=RebirthWorldCharacterService.Record.Support;var snap=RebirthRemoteGearInventorySource.Snapshot;state.EquippedGearBySlot["backpack"]="pack";state.EquippedGearItemDataBySlot["backpack"]="AQ==";var creation=RebirthWorldCharacterService.Record.Origin.CreationId;var id=Guid.NewGuid();
 Check(RebirthGearPreparationIntent.TryCreateUnequip(creation,id,2,"backpack",snap,out var intent),"unequip original full preimage admitted");
 Check(RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out var offer)&&state.PendingGearTransfer==null,"actual server unequip pure builder preserves custody");
 Check(offer.TryGetPlan(out var plan)&&plan.GearBefore.ItemData=="AQ=="&&plan.GearBefore.Count==1&&plan.GearAfter.Count==0&&plan.BagSlotsAfter==52,"unequip exact persisted item and base capacity");
 Check(RebirthGearPreparationOfferBinding.Matches(intent,snap,offer),"actual owner binding replays complete unequip operation");
 Check(RebirthGearUnequipReturnSlot.TryFind(plan,out var returnedIndex,out var returnedData)&&returnedIndex==0&&returnedData=="AQ==","exact unequip return selects newly filled cell over existing identical stack");
 RebirthGearTransferState.TryCreate(id.ToString("N"),creation,"belt",2,plan,out var wrongSlot);Check(!RebirthGearPreparationOfferBinding.Matches(intent,snap,wrongSlot),"unequip foreign offer slot refused");
 var nativeWorld=Guid.Parse(GameManager.Instance.World.worldState.Guid);RebirthGearPreparationMarker.TryEncode(nativeWorld,snap.OwnedBeltSlots,intent,out var marker);Check(RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out var saved)&&ReferenceEquals(state.PendingGearTransfer,saved)&&RebirthWorldCharacterRepository.Writes==1,"bound unequip uses actual prepared original journal");Check(RebirthGearPreparationOfferBinding.MatchesBound(intent,snap,saved,nativeWorld),"saved unequip marker digest binds original owner offer");
 Reset();state=RebirthWorldCharacterService.Record.Support;snap=RebirthRemoteGearInventorySource.Snapshot;RebirthGearPreparationIntent.TryCreateUnequip(creation,Guid.NewGuid(),2,"backpack",snap,out intent);Check(!RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out _),"empty equipped slot cannot manufacture return");
 state.EquippedGearBySlot["backpack"]="pack";Check(!RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out _),"name-only gear cannot manufacture native item");
 state.EquippedGearItemDataBySlot["backpack"]="Ag==";Check(!RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out _),"stored native item must match server item name");
 state.EquippedGearItemDataBySlot["backpack"]="AQ==";state.GearRevision=3;Check(!RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out _),"stale unequip revision refused");state.GearRevision=2;
 state.PendingLibraryTransfer=new();Check(!RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out _),"library custody conflict refuses unequip");state.PendingLibraryTransfer=null;
 snap.Bag[8]=S("Ag==",1);Check(!RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out _),"changed physical original refuses unequip");
 Reset();state=RebirthWorldCharacterService.Record.Support;snap=RebirthRemoteGearInventorySource.Snapshot;state.EquippedGearBySlot["belt"]="belt";state.EquippedGearItemDataBySlot["belt"]="Ag==";RebirthGearPreparationIntent.TryCreateUnequip(creation,Guid.NewGuid(),2,"belt",snap,out intent);Check(RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out offer)&&offer.TryGetPlan(out plan)&&plan.BeltSlotsAfter==4&&plan.GearBefore.ItemData=="Ag==","unequip belt removes gear slot bonus");
 Reset();state=RebirthWorldCharacterService.Record.Support;snap=RebirthRemoteGearInventorySource.Snapshot;state.EquippedGearBySlot["backpack"]="biggerPack";state.EquippedGearItemDataBySlot["backpack"]="Aw==";snap.Bag=Enumerable.Range(0,78).Select(_=>S("AQ==",1)).ToArray();RebirthGearPreparationIntent.TryCreateUnequip(creation,Guid.NewGuid(),2,"backpack",snap,out intent);Check(RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out offer)&&offer.TryGetPlan(out plan)&&plan.Recovery.Count==27&&plan.Recovery.Any(r=>r.Origin==RebirthGearInventoryPlan.RecoveryOrigin.DisplacedGear&&r.Item.ItemData=="Aw=="),"full occupied shrink conserves retired tail and displaced gear in recovery");Check(RebirthGearPreparationOfferBinding.Matches(intent,snap,offer),"owner binding verifies complete occupied-tail recovery operation");
 Check(!RebirthGearUnequipReturnSlot.TryFind(plan,out _,out _),"recovery-only gear never selects old matching physical cell");
 Check(!RebirthGearUnequipReturnSlot.TryFind(null,out _,out _),"absent return plan refused");
 }
 static void OriginalIntentChecks(){
 var original=Guid.ParseExact("33333333333333333333333333333333","N");
 Reset();Check(!RebirthRemoteGearEquipPlan.TryBuild(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,Guid.Empty,out _)&&RebirthWorldCharacterService.Record.Support.PendingGearTransfer==null,"empty original identity rejected without mutation");
 Check(RebirthRemoteGearEquipPlan.TryBuild(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,original,out var built)&&built.TransactionId==original.ToString("N"),"builder preserves original intent identity");
 Check(RebirthWorldCharacterRepository.Writes==0&&RebirthWorldCharacterService.Record.Support.PendingGearTransfer==null,"original identity builder remains read-only");
 Reset();Check(RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,original,out var prepared)&&prepared.TransactionId==original.ToString("N")&&RebirthWorldCharacterRepository.Writes==1,"saved preparation keeps original identity");
 Check(RebirthRemoteGearPreparation.TryReplay(player,sender,prepared.CreationId,out var replay)&&System.Xml.Linq.XNode.DeepEquals(prepared.ToXml(),replay.ToXml()),"original preparation replay keeps exact payload");
 Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,prepared.CreationId,1,3,2,original,out _)&&RebirthWorldCharacterRepository.Writes==1,"pending original is replayed not rebuilt");
 Reset();RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,original,out _)&&RebirthWorldCharacterService.Record.Support.PendingGearTransfer.TransactionId==original.ToString("N"),"unknown preparation retains client original identity");
 var pending=RebirthWorldCharacterService.Record.Support.PendingGearTransfer;
 Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,pending.CreationId,1,3,2,Guid.NewGuid(),out _)&&ReferenceEquals(pending,RebirthWorldCharacterService.Record.Support.PendingGearTransfer)&&RebirthWorldCharacterRepository.Writes==1,"unknown original refuses replacement before save");
 RebirthWorldCharacterRepository.Saved=true;Check(RebirthRemoteGearPreparation.TryReplay(player,sender,pending.CreationId,out replay)&&replay.TransactionId==original.ToString("N")&&RebirthWorldCharacterRepository.Writes==1,"later saved witness replays original with no new write");
 }
 static void RetiredIntentChecks(){
 Reset();Build(out var old);var settlement=RebirthGearSettlement.Create(old,false);var st=RebirthWorldCharacterService.Record.Support;st.GearRevision=settlement.GearRevision;st.LastGearSettlement=settlement;
 Check(!RebirthRemoteGearEquipPlan.TryBuild(player,sender,old.CreationId,1,3,st.GearRevision,Guid.ParseExact(old.TransactionId,"N"),out _)&&st.PendingGearTransfer==null,"settled identity cannot be used for another original intent");
 old.TryGetPlan(out var plan);RebirthGearTransferState.TryCreate(old.TransactionId,old.CreationId,old.SlotId,st.GearRevision,plan,out var reused);bool saved=false;
 Check(!RebirthGearTransferJournal.Prepare(st,reused,old.CreationId,()=>{saved=true;return true;})&&!saved&&st.PendingGearTransfer==null,"direct journal refuses settled identity before writing pending");
 Check(RebirthRemoteGearEquipPlan.TryBuild(player,sender,old.CreationId,1,3,st.GearRevision,Guid.NewGuid(),out _),"distinct new original remains available after settlement");
 }
 static void InventoryIntentChecks(){
 Reset();var snap=RebirthRemoteGearInventorySource.Snapshot;var id=Guid.NewGuid();var creation=RebirthWorldCharacterService.Record.Origin.CreationId;
 Check(RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,true,7,snap,out var intent),"original source intent admitted");
 Check(RebirthGearPreparationIntent.TryRead(intent.Write(),out var cold)&&cold.TransactionId==id&&cold.MatchesInventory(snap),"strict intent cold roundtrip");
 Check(RebirthRemoteGearEquipPlan.TryBuild(player,sender,cold,out var offer)&&offer.TransactionId==id.ToString("N"),"intent reaches actual builder");
 snap.Bag[8]=S("AQ==",1);Check(!cold.MatchesInventory(snap)&&!RebirthRemoteGearEquipPlan.TryBuild(player,sender,cold,out _),"untouched other slot binds inventory digest");
 snap.Bag[8]=S();snap.Belt[19]=S("Ag==",1);Check(!cold.MatchesInventory(snap),"retired tail bound by intent");snap.Belt[19]=S();
 snap.OwnedBeltSlots=5;Check(!cold.MatchesInventory(snap),"owned capacity bound by intent");snap.OwnedBeltSlots=4;
 var xml=intent.Write();xml.Add(new System.Xml.Linq.XAttribute("extra",1));Check(!RebirthGearPreparationIntent.TryRead(xml,out _),"unknown intent attribute refused");
 xml=intent.Write();xml.SetAttributeValue("digest",new string('g',64));Check(!RebirthGearPreparationIntent.TryRead(xml,out _),"invalid digest refused");
 xml=intent.Write();xml.Add(new System.Xml.Linq.XElement("nested"));Check(!RebirthGearPreparationIntent.TryRead(xml,out _),"nested intent refused");
 Check(!RebirthGearPreparationIntent.TryCreate(creation,Guid.Empty,2,1,3,true,7,snap,out _),"empty intent GUID refused");
 Check(!RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,false,19,snap,out _),"retired slot cannot be original source");
 snap.Bag[8]=S(" A Q = = ",1);Check(!RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,true,7,snap,out _),"noncanonical encoded bytes refused");snap.Bag[8]=S();
 // Duplicate type/seed must not move the first matching stack instead of the selected one.
 snap.Belt[0]=S("AQ==",4);Check(RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,true,7,snap,out intent)&&RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out offer),"selected duplicate source admitted");
 offer.TryGetPlan(out var plan);Check(plan.Changes.Any(c=>c.IsBag&&c.Index==7&&c.After.Count==1)&&!plan.Changes.Any(c=>!c.IsBag&&c.Index==0&&c.After.Count==3),"exact original position determines debit");
 Reset();snap=RebirthRemoteGearInventorySource.Snapshot;RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,true,7,snap,out intent);
 Check(RebirthRemoteGearPreparation.TryPrepare(player,sender,intent,out offer)&&offer.TransactionId==id.ToString("N"),"intent saved through actual preparation");
 Reset();snap=RebirthRemoteGearInventorySource.Snapshot;RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,true,7,snap,out intent);snap.Bag[8]=S("Ag==",1);
 Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,intent,out _)&&RebirthWorldCharacterRepository.Writes==0,"changed original upload refused before save");
 Reset();snap=RebirthRemoteGearInventorySource.Snapshot;RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,true,7,snap,out intent);RebirthWorldCharacterRepository.OnSave=()=>snap.Bag[8]=S("Ag==",1);
 Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,intent,out _)&&RebirthWorldCharacterService.Record.Support.PendingGearTransfer.TransactionId==id.ToString("N"),"changed upload across save cannot deliver and retains original");
 }
 static void OfferIntentBindingChecks(){
 Reset();var snap=RebirthRemoteGearInventorySource.Snapshot;var creation=RebirthWorldCharacterService.Record.Origin.CreationId;var id=Guid.NewGuid();
 RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,true,7,snap,out var intent);RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out var offer);
 Check(RebirthGearPreparationOfferBinding.Matches(intent,snap,offer),"exact original offer binds whole operation");
 var savedWorld=Guid.NewGuid();RebirthGearPreparationMarker.TryEncode(savedWorld,snap.OwnedBeltSlots,intent,out var savedMarker);
 string requestDigest;using(var hash=System.Security.Cryptography.SHA256.Create())requestDigest=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(savedMarker))).Replace("-",string.Empty).ToLowerInvariant();
 Check(!RebirthGearPreparationOfferBinding.MatchesBound(intent,snap,offer,savedWorld),"legacy unbound offer cannot adopt original bound hold");
 offer.TryBindPreparationRequest(requestDigest,out var boundOffer);
 Check(RebirthGearPreparationOfferBinding.MatchesBound(intent,snap,boundOffer,savedWorld),"bound original marker digest accepts exact operation");
 Check(!RebirthGearPreparationOfferBinding.MatchesBound(intent,snap,boundOffer,Guid.NewGuid()),"different saved world cannot adopt bound original");
 Check(!RebirthGearPreparationOfferBinding.MatchesBound(intent,snap,boundOffer,Guid.Empty),"absent saved world cannot adopt bound original");
 offer.TryBindPreparationRequest(new string('a',64),out var wrongDigestOffer);Check(!RebirthGearPreparationOfferBinding.MatchesBound(intent,snap,wrongDigestOffer,savedWorld),"wrong marker digest cannot adopt exact inventory plan");
 snap.Bag[8]=S("Ag==",1);Check(!RebirthGearPreparationOfferBinding.MatchesBound(intent,snap,boundOffer,savedWorld),"changed inventory cannot adopt correct marker digest");snap.Bag[8]=S();
 offer.TryGetPlan(out var plan);
 RebirthGearTransferState.TryCreate(Guid.NewGuid().ToString("N"),creation,offer.SlotId,2,plan,out var other);Check(!RebirthGearPreparationOfferBinding.Matches(intent,snap,other),"foreign offer GUID refused");
 RebirthGearTransferState.TryCreate(id.ToString("N"),creation,"belt",2,plan,out other);Check(!RebirthGearPreparationOfferBinding.Matches(intent,snap,other),"native source profile slot mismatch refused");
 RebirthGearTransferState.TryCreate(id.ToString("N"),creation,offer.SlotId,3,plan,out other);Check(!RebirthGearPreparationOfferBinding.Matches(intent,snap,other),"rebased original revision refused");
 snap.Bag[8]=S("Ag==",1);Check(!RebirthGearPreparationOfferBinding.Matches(intent,snap,offer),"changed original image cannot adopt offer");snap.Bag[8]=S();
 // A conserved offer with the same GUID can still select a different duplicate.
 snap.Belt[0]=S("AQ==",4);RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,true,7,snap,out intent);
 RebirthRemoteGearEquipPlan.TryBuild(player,sender,creation,1,3,2,id,out other);
 Check(!RebirthGearPreparationOfferBinding.Matches(intent,snap,other),"conserved wrong duplicate debit refused");
 RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out offer);Check(RebirthGearPreparationOfferBinding.Matches(intent,snap,offer),"selected duplicate exact offer accepted");
 RebirthGearPreparationIntent.TryCreate(creation,id,2,2,3,true,7,snap,out var wrongType);Check(!RebirthGearPreparationOfferBinding.Matches(wrongType,snap,offer),"claimed type must match native source bytes");
 RebirthGearPreparationIntent.TryCreate(creation,id,2,1,4,true,7,snap,out var wrongSeed);Check(!RebirthGearPreparationOfferBinding.Matches(wrongSeed,snap,offer),"claimed seed must match native source bytes");
 }
 static void OriginalMarkerChecks(){
 Reset();var snap=RebirthRemoteGearInventorySource.Snapshot;var creation=RebirthWorldCharacterService.Record.Origin.CreationId;var id=Guid.NewGuid();var world=Guid.NewGuid();RebirthGearPreparationIntent.TryCreate(creation,id,2,1,3,true,7,snap,out var original);
 Check(RebirthGearPreparationMarker.TryEncode(world,4,original,out var key)&&key.Length<RebirthGearPreparationMarker.MaximumKeyLength,"bounded original marker encoded");
 Check(RebirthGearPreparationMarker.TryRead(key,1f,out var parsedWorld,out var owned,out var parsed)&&parsedWorld==world&&owned==4&&System.Xml.Linq.XNode.DeepEquals(original.Write(),parsed.Write()),"whole intent/world/ownedslots exact marker roundtrip");
 Check(!RebirthGearPreparationMarker.TryRead(key,0,out _,out _,out _),"absent marker not active");
 Check(!RebirthGearPreparationMarker.TryRead(key,float.NaN,out _,out _,out _),"NaN marker refused");
 Check(!RebirthGearPreparationMarker.TryRead(key,2,out _,out _,out _),"other stage never original intent");
 Check(!RebirthGearPreparationMarker.TryEncode(Guid.Empty,4,original,out _),"empty world refuses marker");
 Check(!RebirthGearPreparationMarker.TryEncode(world,19,original,out _),"invalid owned belt refuses marker");
 Check(!RebirthGearPreparationMarker.TryRead(key.Substring(0,key.Length-1),1,out _,out _,out _),"truncated marker refused");
 Check(!RebirthGearPreparationMarker.TryRead(key.ToUpperInvariant(),1,out _,out _,out _),"noncanonical dictionary casing refused");
 Check(!RebirthGearPreparationMarker.TryRead(RebirthGearPreparationMarker.Prefix+"ff",1,out _,out _,out _),"invalid UTF8 refused");
 Check(!RebirthGearPreparationMarker.TryRead(RebirthGearPreparationMarker.Prefix+new string('a',5000),1,out _,out _,out _),"oversize marker refused before allocation");
 RebirthGearPreparationMarker.TryEncode(Guid.NewGuid(),4,original,out var otherWorld);Check(otherWorld!=key,"original saved worlds never share marker");
 }
 static byte[] MarkerBlob(params (string key,float value)[] vars){using var s=new System.IO.MemoryStream();using(var writer=new System.IO.BinaryWriter(s,System.Text.Encoding.UTF8,true)){writer.Write((byte)EntityBuffs.Version);writer.Write((ushort)0);writer.Write((ushort)vars.Length);foreach(var v in vars){writer.Write(v.key);writer.Write(v.value);}}return s.ToArray();}
 static void NativeMarkerReaderChecks(){
 Reset();var world=Guid.NewGuid();RebirthGearPreparationIntent.TryCreate(RebirthWorldCharacterService.Record.Origin.CreationId,Guid.NewGuid(),2,1,3,true,7,RebirthRemoteGearInventorySource.Snapshot,out var intent);RebirthGearPreparationMarker.TryEncode(world,4,intent,out var marker);var blob=MarkerBlob(("unrelated",7),(marker,1));
 Check(RebirthGearPreparationMarkerReader.TryRead(blob,out var key,out var savedWorld,out var owned,out var savedIntent)&&key==marker&&savedWorld==world&&owned==4&&System.Xml.Linq.XNode.DeepEquals(intent.Write(),savedIntent.Write()),"actual blob reader retains exact original marker");
 Check(!RebirthGearPreparationMarkerReader.TryRead(MarkerBlob((marker,1),(marker,1)),out _,out _,out _,out _),"duplicate original markers refuse");
 Check(!RebirthGearPreparationMarkerReader.TryRead(MarkerBlob((marker,1),("_rbgearintent_v2_unknown",1)),out _,out _,out _,out _),"unknown original marker version refuses");
 Check(!RebirthGearPreparationMarkerReader.TryRead(MarkerBlob((marker,1),(marker.ToUpperInvariant(),1)),out _,out _,out _,out _),"case-insensitive duplicate ambiguity refuses");
 Check(!RebirthGearPreparationMarkerReader.TryRead(MarkerBlob((marker,0)),out _,out _,out _,out _),"zero cannot prove saved intent");
 Check(!RebirthGearPreparationMarkerReader.TryRead(blob.Concat(new byte[]{0}).ToArray(),out _,out _,out _,out _),"trailing native blob bytes refuse");
 Check(!RebirthGearPreparationMarkerReader.TryRead(blob.Take(blob.Length-1).ToArray(),out _,out _,out _,out _),"truncated native blob refuses");
 blob[0]=2;Check(!RebirthGearPreparationMarkerReader.TryRead(blob,out _,out _,out _,out _),"wrong installed blob version refuses");
 Check(!RebirthGearPreparationMarkerReader.TryRead(MarkerBlob(),out _,out _,out _,out _),"no marker not proof");
 Check(RebirthGearPreparationMarkerReader.HasNoOriginal(MarkerBlob())&&RebirthGearPreparationMarkerReader.HasNoOriginal(MarkerBlob(("unrelated",1))),"fully parsed marker absence accepts unrelated variables");
 Check(!RebirthGearPreparationMarkerReader.HasNoOriginal(MarkerBlob((marker,1)))&&!RebirthGearPreparationMarkerReader.HasNoOriginal(MarkerBlob((marker,0))),"present and zero intent keys cannot prove retirement");
 Check(!RebirthGearPreparationMarkerReader.HasNoOriginal(MarkerBlob(("_RBGEARINTENT_V2_unknown",0))),"unknown case variant intent family cannot prove retirement");
 var absent=MarkerBlob();Check(!RebirthGearPreparationMarkerReader.HasNoOriginal(absent.Concat(new byte[]{0}).ToArray())&&!RebirthGearPreparationMarkerReader.HasNoOriginal(absent.Take(absent.Length-1).ToArray()),"absent marker malformed native blob refuses");
 absent[0]=2;Check(!RebirthGearPreparationMarkerReader.HasNoOriginal(absent)&&!RebirthGearPreparationMarkerReader.HasNoOriginal(null),"wrong native version and missing bytes refuse absence proof");
 }
 static void PreparationDigestChecks(){
 Reset();Build(out var original);string digest=new string('a',64);
 Check(original.PreparationRequestDigest==null&&(string)original.ToXml().Attribute("version")=="1","legacy unbound format preserved");
 Check(original.TryBindPreparationRequest(digest,out var bound)&&bound.PreparationRequestDigest==digest&&(string)bound.ToXml().Attribute("version")=="2"&&original.PreparationRequestDigest==null,"strict immutable original request binding");
 Check(RebirthGearTransferState.TryRead(bound.ToXml(),out var cold)&&cold.PreparationRequestDigest==digest,"bound original request survives cold read");
 Check(bound.TryBindPreparationRequest(digest,out var same)&&ReferenceEquals(bound,same),"original binding idempotent");
 Check(!bound.TryBindPreparationRequest(new string('b',64),out _),"original binding cannot be replaced");
 Check(!original.TryBindPreparationRequest(new string('A',64),out _)&&!original.TryBindPreparationRequest("short",out _),"noncanonical digest refused");
 var xml=bound.ToXml();xml.Element("originalRequest").Remove();Check(!RebirthGearTransferState.TryRead(xml,out _),"version2 requires original request binding");
 xml=bound.ToXml();xml.SetAttributeValue("version",1);Check(!RebirthGearTransferState.TryRead(xml,out _),"version1 cannot hide bound request");
 xml=bound.ToXml();xml.Element("originalRequest").Add(new System.Xml.Linq.XAttribute("extra",1));Check(!RebirthGearTransferState.TryRead(xml,out _),"unknown binding attributes refused");
 xml=bound.ToXml();xml.Add(new System.Xml.Linq.XElement(xml.Element("originalRequest")));Check(!RebirthGearTransferState.TryRead(xml,out _),"duplicate binding refused");
 }
 static string BoundMarker(){RebirthGearPreparationIntent.TryCreate(RebirthWorldCharacterService.Record.Origin.CreationId,Guid.NewGuid(),2,1,3,true,7,RebirthRemoteGearInventorySource.Snapshot,out var intent);RebirthGearPreparationMarker.TryEncode(Guid.Parse(GameManager.Instance.World.worldState.Guid),4,intent,out var marker);return marker;}
 static void AuthenticatedBoundPreparationChecks(){
 Reset();var marker=BoundMarker();Check(RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out var offer)&&offer.PreparationRequestDigest!=null&&RebirthWorldCharacterRepository.Writes==1,"authenticated original marker binds saved prepared plan");
 Check(!RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out _)&&RebirthWorldCharacterRepository.Writes==1,"bound retry cannot replan original pending");
 Check(RebirthRemoteGearPreparation.TryReplayBound(player,sender,marker,out var replay)&&replay.PreparationRequestDigest==offer.PreparationRequestDigest&&RebirthWorldCharacterRepository.Writes==1,"bound saved retry returns original without write");
 RebirthGearPreparationPlayerFileWitness.Original=false;Check(RebirthRemoteGearPreparation.TryReplayBound(player,sender,marker,out _),"replay does not demand obsolete original native preimage");RebirthGearPreparationPlayerFileWitness.Original=true;
 Check(!RebirthRemoteGearPreparation.TryReplayBound(player,sender,BoundMarker(),out _),"different original request cannot replay retained transaction");
 RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearPreparation.TryReplayBound(player,sender,marker,out _),"unsaved bound candidate cannot replay");RebirthWorldCharacterRepository.Saved=true;
 SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Sender=new();Check(!RebirthRemoteGearPreparation.TryReplayBound(player,sender,marker,out _),"old bound session cannot replay");SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Sender=sender;
 var nativeGuid=GameManager.Instance.World.worldState.Guid;GameManager.Instance.World.worldState.Guid=Guid.NewGuid().ToString("N");Check(!RebirthRemoteGearPreparation.TryReplayBound(player,sender,marker,out _),"foreign saved world cannot replay");GameManager.Instance.World.worldState.Guid=nativeGuid;
 Reset();marker=BoundMarker();RebirthGearPreparationPlayerFileWitness.Original=false;Check(!RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out _)&&RebirthWorldCharacterRepository.Writes==0,"missing final original native witness refuses save");
 Reset();marker=BoundMarker();SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Sender=new();Check(!RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out _)&&RebirthWorldCharacterRepository.Writes==0,"old session cannot prepare bound original");
 Reset();marker=BoundMarker();GameManager.Instance.World.worldState.Guid=Guid.NewGuid().ToString("N");Check(!RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out _)&&RebirthWorldCharacterRepository.Writes==0,"foreign original marker world cannot prepare");
 Reset();marker=BoundMarker();RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out _)&&RebirthWorldCharacterService.Record.Support.PendingGearTransfer.PreparationRequestDigest!=null,"uncertain prepare retains bound original request");
 Reset();marker=BoundMarker();RebirthWorldCharacterRepository.OnSave=()=>RebirthGearPreparationPlayerFileWitness.Original=false;Check(!RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out _)&&RebirthWorldCharacterService.Record.Support.PendingGearTransfer.PreparationRequestDigest!=null,"native original file loss across save withholds offer and retains binding");
 }
 static void ColdRecoveryBindingChecks(){
 Reset();var snap=RebirthRemoteGearInventorySource.Snapshot;snap.Belt[19]=S("Ag==",1);
 var creation=RebirthWorldCharacterService.Record.Origin.CreationId;var world=Guid.NewGuid();
 RebirthGearPreparationIntent.TryCreate(creation,Guid.NewGuid(),2,1,3,true,7,snap,out var intent);
 RebirthRemoteGearEquipPlan.TryBuild(player,sender,intent,out var offer);offer.TryGetPlan(out var plan);
 RebirthGearPreparationMarker.TryEncode(world,4,intent,out var marker);string digest;
 using(var hash=System.Security.Cryptography.SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(marker))).Replace("-",string.Empty).ToLowerInvariant();
 offer.TryBindPreparationRequest(digest,out offer);
 Check(RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,snap,world,4,0,out var original)&&intent.MatchesInventory(original),"untouched cold original reconstructed");
 Check(RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,snap,world,4,-1,out _),"untouched rejected original reconstructed");
 RebirthGearEncodedSnapshot.TryCopy(snap.Bag,snap.Belt,4,out var bag,out var belt);
 var current=new RebirthGearInventorySnapshot{Bag=Enumerable.Range(0,plan.BagSlotsAfter).Select(i=>i<bag.Length?bag[i]:S()).ToArray(),Belt=belt,OwnedBeltSlots=4};
 Check(RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,2,out _),"expanded applying preimage reconstructed");
 Check(!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,0,out _),"untouched receipt cannot authorize unexplained backing expansion");
 var debit=plan.Changes.First(c=>c.IsBag&&c.Index==7);current.Bag[7]=S(debit.After.ItemData,debit.After.Count);
 Check(RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,2,out original)&&intent.MatchesInventory(original),"partial applying original reconstructed");
 Check(!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,0,out _)&&!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,-1,out _),"untouched receipt cannot authorize mixed image");
 Check(!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,1,out _),"applied receipt requires full postimage");
 foreach(var change in plan.Changes)(change.IsBag?current.Bag:current.Belt)[change.Index]=S(change.After.ItemData,change.After.Count);
 Check(RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,1,out original)&&original.Bag[7].Count==2&&original.Belt[19].Count==1,"full postimage reconstructs original selected stack and retired tail");
 original.Bag[7].Count=123;Check(current.Bag[7].Count==1,"reconstructed original detached from native current image");
 Check(!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,Guid.NewGuid(),4,1,out _),"foreign saved world refuses cold binding");
 Check(!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,5,1,out _),"wrong original owned capacity refuses full digest");
 Check(!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,float.NaN,out _)&&!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,3,out _),"unknown receipt refuses cold binding");
 current.Bag[8]=S("Ag==",1);Check(!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,1,out _),"unchanged slot mutation refuses original full digest");current.Bag[8]=S();
 current.Bag[current.Bag.Length-1]=S("Ag==",1);Check(!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,offer,current,world,4,1,out _),"unplanned expansion item cannot disappear during reconstruction");current.Bag[current.Bag.Length-1]=S();
 offer.TryGetRecoveryManifest(out var manifest);var publication=Guid.Parse((string)manifest.ToXml().Elements().First().Attribute("id"));
 RebirthGearRecoveryAttempt.TryCreate(publication,900,1,45,1,0,1800,100,7,out var attempt);Check(offer.TryAppendRecoveryAttempt(attempt,out var attempted),"original cold recovery attempt retained");
 Check(!RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,attempted,current,world,4,2,out _),"publication attempt cannot reconcile without Applied owner receipt");
 Check(RebirthGearPreparationRecoveryBinding.TryRecoverOriginal(intent,attempted,current,world,4,1,out _)&&attempted.HasRecoveryAttempts&&attempted.TryGetRecoveryAttempt(publication,out _),"original committed recovery provenance survives pure comparison");
 Check(RebirthGearEncodedSnapshot.TryCopy(snap.Bag,snap.Belt,4,out var copied,out _)&&!ReferenceEquals(copied[7],snap.Bag[7]),"canonical encoded factory deep copy");
 snap.Bag[8]=S(" A Q = = ",1);Check(!RebirthGearEncodedSnapshot.TryCopy(snap.Bag,snap.Belt,4,out _,out _),"noncanonical encoded cells refused");snap.Bag[8]=S("AQ==",0);Check(!RebirthGearEncodedSnapshot.TryCopy(snap.Bag,snap.Belt,4,out _,out _),"empty encoded cell cannot carry hidden bytes");
 }
 static void RetainedPhaseReplayChecks(){
 Reset();var marker=BoundMarker();Check(RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out var offer),"retained phase original prepared");
 var support=RebirthWorldCharacterService.Record.Support;int writes=RebirthWorldCharacterRepository.Writes;
 Check(RebirthRemoteGearPreparation.TryReplayBoundRetained(player,sender,marker,out var copy,out var phase)&&phase==RebirthGearTransferPhase.Prepared&&copy.TransactionId==offer.TransactionId,"retained Prepared exact original");
 support.GearTransferPhase=RebirthGearTransferPhase.OwnerApplied;
 Check(!RebirthRemoteGearPreparation.TryReplayBound(player,sender,marker,out _),"prepared-only replay does not rewind OwnerApplied");
 Check(RebirthRemoteGearPreparation.TryReplayBoundRetained(player,sender,marker,out copy,out phase)&&phase==RebirthGearTransferPhase.OwnerApplied&&copy.PreparationRequestDigest==offer.PreparationRequestDigest,"retained OwnerApplied exact original");
 support.GearTransferPhase=RebirthGearTransferPhase.GearCommitted;support.GearRevision++;offer.TryGetPlan(out var plan);support.EquippedGearBySlot[offer.SlotId]="pack";support.EquippedGearItemDataBySlot[offer.SlotId]=plan.GearAfter.ItemData;
 Check(RebirthRemoteGearPreparation.TryReplayBoundRetained(player,sender,marker,out copy,out phase)&&phase==RebirthGearTransferPhase.GearCommitted&&copy.TransactionId==offer.TransactionId&&RebirthWorldCharacterRepository.Writes==writes,"retained committed original no write/revision change");
 support.GearRevision--;Check(!RebirthRemoteGearPreparation.TryReplayBoundRetained(player,sender,marker,out _,out _),"committed wrong revision refuses retained lookup");support.GearRevision++;
 RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearPreparation.TryReplayBoundRetained(player,sender,marker,out _,out _),"unsaved committed phase refuses retained lookup");RebirthWorldCharacterRepository.Saved=true;
 support.GearTransferPhase=(RebirthGearTransferPhase)3;Check(!RebirthRemoteGearPreparation.TryReplayBoundRetained(player,sender,marker,out _,out _),"unknown retained phase refuses");
 }
 static void BoundTerminalChecks(){
 Reset();var marker=BoundMarker();RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out var offer);
 var terminal=RebirthGearSettlement.Create(offer,false);
 var retainedNode=new System.Xml.Linq.XElement("support",terminal.Write(),RebirthGearTerminalOriginalPersistence.Write(terminal,offer));
 Check(RebirthGearTerminalOriginalPersistence.TryRead(retainedNode,terminal,out var retainedOriginal)&&System.Xml.Linq.XNode.DeepEquals(retainedOriginal.ToXml(),offer.ToXml()),"terminal original retains exact full original operation");
 var duplicateOriginal=new System.Xml.Linq.XElement(retainedNode);duplicateOriginal.Add(new System.Xml.Linq.XElement(duplicateOriginal.Element("gearSettlementOriginal")));Check(!RebirthGearTerminalOriginalPersistence.TryRead(duplicateOriginal,terminal,out _),"duplicate terminal originals refuse");
 Check(RebirthGearTerminalOriginalPersistence.TryRead(new System.Xml.Linq.XElement("support"),terminal,out var absentOriginal)&&absentOriginal==null,"legacy terminal absence has no invented original");
 var anotherOriginalMarker=BoundMarker();Check(!terminal.MatchesOriginalMarker(anotherOriginalMarker),"terminal cannot borrow a new original request");
 var rejectedState=new RebirthWorldSupportState{PendingGearTransfer=offer,GearRevision=2};
 Check(RebirthGearTransferJournal.CancelRejected(rejectedState,offer.TransactionId,offer.CreationId,()=>true)&&ReferenceEquals(rejectedState.LastGearSettlementOriginal,offer),"bound rejection saves original plan with terminal");
 var rollbackState=new RebirthWorldSupportState{PendingGearTransfer=offer,GearRevision=2};Check(!RebirthGearTransferJournal.CancelRejected(rollbackState,offer.TransactionId,offer.CreationId,()=>false)&&rollbackState.LastGearSettlementOriginal==null&&ReferenceEquals(rollbackState.PendingGearTransfer,offer),"failed terminal save restores original retention state");
 var node=new System.Xml.Linq.XElement("support",terminal.Write());
 Check((string)terminal.Write().Attribute("version")=="2"&&terminal.PreparationRequestDigest==offer.PreparationRequestDigest&&terminal.MatchesOriginalMarker(marker),"terminal retains exact original request digest");
 Check(RebirthGearSettlement.TryRead(node,3,out var cold)&&cold.MatchesOriginalMarker(marker),"bound terminal cold roundtrip");
 Check(RebirthGearTerminalWireCodec.TryEncode(terminal,out var payload)&&RebirthGearTerminalWireCodec.TryDecode(payload,out var wireTerminal)&&wireTerminal.MatchesOriginalMarker(marker),"actual canonical bound terminal wire roundtrip");
 Check(!RebirthGearTerminalWireCodec.TryDecode(payload.Take(payload.Length-1).ToArray(),out _),"truncated terminal wire refuses");
 Check(!RebirthGearTerminalWireCodec.TryDecode(new byte[]{255},out _)&&!RebirthGearTerminalWireCodec.TryDecode(new byte[1025],out _),"invalid UTF8 and oversize wire refuse");
 Check(!RebirthGearTerminalWireCodec.TryDecode(System.Text.Encoding.UTF8.GetBytes(" "+System.Text.Encoding.UTF8.GetString(payload)),out _),"noncanonical terminal whitespace refuses");
 Check(!RebirthGearTerminalWireCodec.TryDecode(System.Text.Encoding.UTF8.GetBytes("<!DOCTYPE gearSettlement [<!ENTITY x 'a'>]>"+System.Text.Encoding.UTF8.GetString(payload)),out _),"terminal DTD refuses");
 var changed=new System.Xml.Linq.XElement(node);changed.Element("gearSettlement").Attribute("requestDigest").Remove();Check(!RebirthGearSettlement.TryRead(changed,3,out _),"version2 terminal requires binding");
 changed=new(node);changed.Element("gearSettlement").SetAttributeValue("requestDigest",new string('A',64));Check(!RebirthGearSettlement.TryRead(changed,3,out _),"noncanonical terminal digest refuses");
 changed=new(node);changed.Element("gearSettlement").SetAttributeValue("version",1);Check(!RebirthGearSettlement.TryRead(changed,3,out _),"legacy terminal cannot hide binding");
 Check(!terminal.MatchesOriginalMarker(BoundMarker()),"different original request cannot match terminal");
 Reset();Build(out var unbound);var legacy=RebirthGearSettlement.Create(unbound,true);Check((string)legacy.Write().Attribute("version")=="1"&&RebirthGearSettlement.TryRead(new System.Xml.Linq.XElement("support",legacy.Write()),3,out var old)&&old.PreparationRequestDigest==null&&!old.MatchesOriginalMarker(marker),"legacy terminal remains readable without invented binding");
 Check(!RebirthGearTerminalWireCodec.TryEncode(legacy,out _),"legacy unbound receipt cannot enter bound wire");
 Reset();marker=BoundMarker();RebirthRemoteGearPreparation.TryPrepareBound(player,sender,marker,out offer);terminal=RebirthGearSettlement.Create(offer,false);
 var support=RebirthWorldCharacterService.Record.Support;support.PendingGearTransfer=null;support.GearRevision=terminal.GearRevision;support.LastGearSettlement=terminal;int writes=RebirthWorldCharacterRepository.Writes;
 Check(!RebirthRemoteGearAppliedConfirmation.TryGetBoundTerminalOriginal(player,sender,marker,out _,out _),"legacy missing original cannot invent cold terminal plan");
 support.LastGearSettlementOriginal=offer;Check(RebirthRemoteGearAppliedConfirmation.TryGetBoundTerminalOriginal(player,sender,marker,out var savedOriginal,out var originalOutcome)&&System.Xml.Linq.XNode.DeepEquals(savedOriginal.ToXml(),offer.ToXml())&&ReferenceEquals(originalOutcome,terminal)&&RebirthWorldCharacterRepository.Writes==writes,"saved bound terminal original lookup is detached and effect free");
 Check(RebirthRemoteGearAppliedConfirmation.TryGetBoundSettlement(player,sender,marker,out var saved)&&ReferenceEquals(saved,terminal)&&RebirthWorldCharacterRepository.Writes==writes,"saved terminal lookup no replan/write");
 RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearAppliedConfirmation.TryGetBoundSettlement(player,sender,marker,out _),"unsaved terminal cannot clear original");RebirthWorldCharacterRepository.Saved=true;
 SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Sender=new();Check(!RebirthRemoteGearAppliedConfirmation.TryGetBoundSettlement(player,sender,marker,out _),"old native session cannot fetch terminal");SingletonMonoBehaviour<ConnectionManager>.Instance.Clients.Sender=sender;
 GameManager.Instance.World.worldState.Guid=Guid.NewGuid().ToString("N");Check(!RebirthRemoteGearAppliedConfirmation.TryGetBoundSettlement(player,sender,marker,out _),"foreign world cannot fetch original terminal");
 }
 static void Main(){
 UnequipChecks();
 BoundTerminalChecks();
 RetainedPhaseReplayChecks();
 ColdRecoveryBindingChecks();
 AuthenticatedBoundPreparationChecks();
 PreparationDigestChecks();
 NativeMarkerReaderChecks();
 OriginalMarkerChecks();
 OfferIntentBindingChecks();
 InventoryIntentChecks();
 RetiredIntentChecks();
 OriginalIntentChecks();
 Reset();Check(Build(out var offer),"valid pack refused");Check(offer.ExpectedRevision==2&&offer.SlotId=="backpack"&&offer.TryGetPlan(out var p)&&p.IsConserved(),"offer invalid");
 offer.TryGetPlan(out p);Check(p.GearAfter.ItemData=="AQ=="&&p.GearAfter.Count==1&&p.Changes.Any(c=>c.IsBag&&c.Index==7&&c.After.Count==1),"exact source/count lost");
 Check(p.BagSlotsAfter==65&&RebirthWorldCharacterService.Record.Support.GearRevision==2&&RebirthWorldCharacterService.Record.Support.EquippedGearBySlot.Count==0&&RebirthRemoteGearInventorySource.Snapshot.Bag[7].Count==2,"read-only builder mutated");
 Reset();Check(!Build(out _,rev:1),"stale revision accepted");
 Reset();Check(!Build(out _,creation:"22222222222222222222222222222222"),"stale creation accepted");
 Reset();RebirthWorldCharacterService.Record.Progression.Attributes["strength"].Current=4;Check(!Build(out _),"requirements bypassed");
 Reset();RebirthWorldCharacterService.Record.Support.PendingMusicTransfer=new object();Check(!Build(out _),"pending music bypassed");
 Reset();RebirthWorldCharacterService.Record.Support.PendingLibraryTransfer=new object();Check(!Build(out _),"pending library bypassed");
 Reset();Build(out var pending);RebirthWorldCharacterService.Record.Support.PendingGearTransfer=pending;Check(!Build(out _),"pending gear replaced");
 Reset();Check(!Build(out _,type:2),"missing exact item accepted");
 Reset();var snap=RebirthRemoteGearInventorySource.Snapshot;snap.Bag[7]=S();snap.Belt[3]=S("Ag==",1);Check(Build(out offer,type:2),"owned belt source refused");offer.TryGetPlan(out p);Check(p.BeltSlotsAfter==6&&p.GearAfter.ItemData=="Ag==","belt capacity wrong");
 Reset();snap=RebirthRemoteGearInventorySource.Snapshot;snap.Bag[7]=S();snap.Belt[19]=S("AQ==",1);Check(!Build(out _),"retired slot used as equip source");
 Reset();var st=RebirthWorldCharacterService.Record.Support;st.EquippedGearBySlot["backpack"]="biggerPack";st.EquippedGearItemDataBySlot["backpack"]="Aw==";Check(Build(out offer),"displaced gear refused");offer.TryGetPlan(out p);Check(p.GearBefore.ItemData=="Aw=="&&p.Changes.Any(c=>c.After.ItemData=="Aw==")&&p.IsConserved(),"displaced exact gear lost");
 Reset();st=RebirthWorldCharacterService.Record.Support;st.EquippedGearBySlot["backpack"]="biggerPack";Check(!Build(out _),"name-only equipped metadata guessed");
 Reset();snap=RebirthRemoteGearInventorySource.Snapshot;snap.Belt[19]=S("Ag==",1);Check(Build(out offer),"retired overflow refused");offer.TryGetPlan(out p);Check(p.Recovery.Any(r=>r.SourceIndex==19&&r.Item.ItemData=="Ag==")&&p.IsConserved(),"retired tail lost");
 Reset();Check(Build(out offer),"witness offer build");st=RebirthWorldCharacterService.Record.Support;
 Check(!RebirthGearTransferSavedWitness.Matches(st,RebirthWorldCharacterService.Record.Origin.CreationId,offer,RebirthGearTransferPhase.Prepared),"absent pending witnessed");
 Check(!RebirthGearTransferJournal.Prepare(st,offer,offer.CreationId,()=>false)&&ReferenceEquals(st.PendingGearTransfer,offer),"failed preparation keeps original intent without successful delivery");
 Check(RebirthGearTransferJournal.Prepare(st,offer,offer.CreationId,()=>true),"prepare refused");
 Check(RebirthGearTransferSavedWitness.Matches(st,offer.CreationId,offer,RebirthGearTransferPhase.Prepared),"exact prepared witness refused");
 Check(!RebirthGearTransferSavedWitness.Matches(st,"22222222222222222222222222222222",offer,RebirthGearTransferPhase.Prepared),"other creation witnessed");
 st.GearRevision++;Check(!RebirthGearTransferSavedWitness.Matches(st,offer.CreationId,offer,RebirthGearTransferPhase.Prepared),"wrong revision witnessed");st.GearRevision--;
 st.PendingMusicTransfer=new object();Check(!RebirthGearTransferSavedWitness.Matches(st,offer.CreationId,offer,RebirthGearTransferPhase.Prepared),"conflicting custody witnessed");st.PendingMusicTransfer=null;
 offer.TryGetPlan(out p);RebirthGearTransferState.TryCreate(Guid.NewGuid().ToString("N"),offer.CreationId,offer.SlotId,offer.ExpectedRevision,p,out var other);
 Check(!RebirthGearTransferSavedWitness.Matches(st,offer.CreationId,other,RebirthGearTransferPhase.Prepared),"different transaction witnessed");
 Check(!RebirthGearTransferJournal.Prepare(st,other,offer.CreationId,()=>true)&&ReferenceEquals(st.PendingGearTransfer,offer),"retry replaced offer");
 Check(RebirthGearTransferJournal.MarkOwnerApplied(st,offer.TransactionId,offer.CreationId,()=>true),"owner stage refused");
 Check(!RebirthGearTransferSavedWitness.Matches(st,offer.CreationId,offer,RebirthGearTransferPhase.Prepared)&&RebirthGearTransferSavedWitness.Matches(st,offer.CreationId,offer,RebirthGearTransferPhase.OwnerApplied),"phase boundary ignored");
 Check(RebirthGearTransferJournal.CommitGear(st,offer.TransactionId,offer.CreationId,_=>"pack",()=>true)&&RebirthGearTransferSavedWitness.Matches(st,offer.CreationId,offer,RebirthGearTransferPhase.GearCommitted),"committed witness refused");
 st.EquippedGearItemDataBySlot["backpack"]="Ag==";Check(!RebirthGearTransferSavedWitness.Matches(st,offer.CreationId,offer,RebirthGearTransferPhase.GearCommitted),"wrong committed native data witnessed");
 Reset();Check(RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out offer),"durable preparation refused");
 st=RebirthWorldCharacterService.Record.Support;Check(ReferenceEquals(st.PendingGearTransfer,offer)&&RebirthWorldCharacterRepository.Writes==1&&RebirthWorldCharacterService.Dirty==1&&st.EquippedGearBySlot.Count==0&&RebirthRemoteGearInventorySource.Snapshot.Bag[7].Count==2,"prepare changed inventory/gear or omitted write");
 Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,offer.CreationId,1,3,2,out _)&&ReferenceEquals(st.PendingGearTransfer,offer)&&RebirthWorldCharacterRepository.Writes==1,"second request replaced unresolved transaction");
 Reset();RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out offer)&&offer==null&&RebirthWorldCharacterService.Record.Support.PendingGearTransfer!=null,"uncertain saved offer retained but not delivered");
 var retainedFailed=RebirthWorldCharacterService.Record.Support.PendingGearTransfer;var writesFailed=RebirthWorldCharacterRepository.Writes;
 Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,retainedFailed.CreationId,1,3,2,out _)&&ReferenceEquals(RebirthWorldCharacterService.Record.Support.PendingGearTransfer,retainedFailed)&&RebirthWorldCharacterRepository.Writes==writesFailed,"failed witness retry cannot replace original transaction or replan");
 Check(!RebirthRemoteGearPreparation.TryReplay(player,sender,retainedFailed.CreationId,out _),"retained intent cannot be delivered without final saved witness");
 RebirthWorldCharacterRepository.Saved=true;
 Check(RebirthRemoteGearPreparation.TryReplay(player,sender,retainedFailed.CreationId,out var witnessedRetry)&&witnessedRetry.TransactionId==retainedFailed.TransactionId&&RebirthWorldCharacterRepository.Writes==writesFailed,"later available witness replays original identity without newwrite");
 Reset();Check(Build(out var thrownOffer),"exception retention setup");var thrownState=RebirthWorldCharacterService.Record.Support;bool prepareThrew=false;
 try{RebirthGearTransferJournal.Prepare(thrownState,thrownOffer,thrownOffer.CreationId,()=>throw new InvalidOperationException("uncertain save"));}catch(InvalidOperationException){prepareThrew=true;}
 Check(prepareThrew&&ReferenceEquals(thrownState.PendingGearTransfer,thrownOffer),"exception does not erase original prepared intent");
 thrownOffer.TryGetPlan(out var thrownPlan);RebirthGearTransferState.TryCreate(Guid.NewGuid().ToString("N"),thrownOffer.CreationId,thrownOffer.SlotId,thrownOffer.ExpectedRevision,thrownPlan,out var replacementAfterThrow);bool replacementSave=false;
 Check(!RebirthGearTransferJournal.Prepare(thrownState,replacementAfterThrow,thrownOffer.CreationId,()=>{replacementSave=true;return true;})&&!replacementSave&&ReferenceEquals(thrownState.PendingGearTransfer,thrownOffer),"other transaction after unknownwrite refused before save");
 Reset();RebirthWorldCharacterRepository.ThrowBeforeSave=true;Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out _),"failed write escaped");
 Reset();RebirthWorldCharacterRepository.ThrowAfterSave=true;Check(RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out _),"exact saved witness after uncertain write refused");
 Reset();RebirthWorldCharacterService.Identity.StorageKey="other";Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out _)&&RebirthWorldCharacterRepository.Writes==0,"other owner saved");
 Reset();RebirthWorldCharacterRepository.Cached=false;Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out _)&&RebirthWorldCharacterRepository.Writes==0,"retired record saved");
 Reset();RebirthWorldCharacterRepository.OnSave=()=>RebirthRemoteGearInventorySource.Snapshot.Bag[7].Count=1;Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out _),"changed preimage offered");
 Reset();RebirthWorldCharacterRepository.OnSave=()=>RebirthWorldCharacterService.Identity.StorageKey="other";Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out _),"changed owner offered");
 Reset();RebirthWorldCharacterRepository.OnSave=()=>RebirthWorldCharacterService.Record.Support.GearRevision++;Check(!RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out _),"changed revision offered");
 Reset();Check(RebirthRemoteGearPreparation.TryPrepare(player,sender,RebirthWorldCharacterService.Record.Origin.CreationId,1,3,2,out offer),"replay setup refused");
 Check(RebirthRemoteGearPreparation.TryReplay(player,sender,offer.CreationId,out var replay)&&replay.TransactionId==offer.TransactionId&&System.Xml.Linq.XNode.DeepEquals(replay.ToXml(),offer.ToXml())&&!ReferenceEquals(replay,offer),"saved offer replay replaced payload");
 RebirthRemoteGearInventorySource.Snapshot.Bag[7].Count=1;Check(RebirthRemoteGearPreparation.TryReplay(player,sender,offer.CreationId,out _),"owner postimage prevented receipt replay");
 RebirthRemoteGearInventorySource.Snapshot=null;Check(RebirthRemoteGearPreparation.TryReplay(player,sender,offer.CreationId,out _)&&RebirthWorldCharacterRepository.Writes==1,"missing upload prevented receipt replay or caused write");
 Check(!RebirthRemoteGearPreparation.TryReplay(player,sender,"22222222222222222222222222222222",out _),"wrong creation replayed");
 RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearPreparation.TryReplay(player,sender,offer.CreationId,out _),"unsaved pending replayed");RebirthWorldCharacterRepository.Saved=true;
 RebirthWorldCharacterService.Record.Support.GearTransferPhase=RebirthGearTransferPhase.OwnerApplied;Check(!RebirthRemoteGearPreparation.TryReplay(player,sender,offer.CreationId,out _),"later phase resent owner offer");
 Reset();Build(out offer);offer.TryGetPlan(out p);
 var bag=RebirthRemoteGearInventorySource.Snapshot.Bag;var belt=RebirthRemoteGearInventorySource.Snapshot.Belt;bool current=true;int applying=0,writes=0,applied=0;
 Func<bool> saveIntent=()=>{applying++;return true;};
 Action expand=()=>{if(bag.Length<p.BagSlotsAfter){var grown=Enumerable.Range(0,p.BagSlotsAfter).Select(_=>S()).ToArray();Array.Copy(bag,grown,bag.Length);bag=grown;}};
 Action<RebirthGearInventoryPlan.Change> write=c=>{writes++;(c.IsBag?bag:belt)[c.Index]=S(c.After.ItemData,c.After.Count);};
 Func<bool> saveDone=()=>{applied++;return true;};
 Check(RebirthGearOwnerApplySequence.Execute(p,false,()=>current,()=>bag,()=>belt,saveIntent,expand,write,saveDone)==RebirthGearOwnerApplySequence.Result.Applied&&applying==1&&applied==1&&writes>0,"owner sequence refused valid operation");
 int previousWrites=writes;Check(RebirthGearOwnerApplySequence.Execute(p,true,()=>current,()=>bag,()=>belt,saveIntent,expand,write,saveDone)==RebirthGearOwnerApplySequence.Result.Applied&&writes==previousWrites,"applied retry debited twice");
 current=false;Check(RebirthGearOwnerApplySequence.Execute(p,true,()=>current,()=>bag,()=>belt,saveIntent,expand,write,saveDone)==RebirthGearOwnerApplySequence.Result.NeedsReconciliation&&writes==previousWrites,"retired owner mutated");
 Check(RebirthGearOwnerApplySequence.Execute(p,false,()=>current,()=>bag,()=>belt,saveIntent,expand,write,saveDone)==RebirthGearOwnerApplySequence.Result.Conflict,"untouched retired owner not refused");
 Reset();Build(out offer);offer.TryGetPlan(out p);bag=RebirthRemoteGearInventorySource.Snapshot.Bag;belt=RebirthRemoteGearInventorySource.Snapshot.Belt;current=true;writes=0;applied=0;
 Check(RebirthGearOwnerApplySequence.Execute(p,false,()=>current,()=>bag,()=>belt,()=>{current=false;return true;},expand,write,saveDone)==RebirthGearOwnerApplySequence.Result.NeedsReconciliation&&writes==0&&applied==0,"lost binding during intent write mutated");
 current=true;Check(RebirthGearOwnerApplySequence.Execute(p,false,()=>current,()=>bag,()=>belt,saveIntent,()=>{expand();current=false;},write,saveDone)==RebirthGearOwnerApplySequence.Result.NeedsReconciliation&&writes==0,"lost binding in expansion mutated slots");
 current=true;Check(RebirthGearOwnerApplySequence.Execute(p,true,()=>current,()=>bag,()=>belt,saveIntent,expand,c=>{write(c);current=false;},saveDone)==RebirthGearOwnerApplySequence.Result.NeedsReconciliation&&writes==1&&applied==0,"lost binding in native write claimed applied");
 current=true;Check(RebirthGearOwnerApplySequence.Execute(p,true,()=>current,()=>bag,()=>belt,saveIntent,expand,write,()=>{applied++;current=false;return true;})==RebirthGearOwnerApplySequence.Result.NeedsReconciliation,"lost binding during applied save claimed current success");
 Reset();Build(out offer);offer.TryGetPlan(out p);bag=RebirthRemoteGearInventorySource.Snapshot.Bag;belt=RebirthRemoteGearInventorySource.Snapshot.Belt;
 Check(!p.MatchesAppliedInventory(bag,belt),"unexpanded preimage proved applied");
 expand();Check(!p.MatchesAppliedInventory(bag,belt),"expanded preimage proved applied");
 foreach(var change in p.Changes)write(change);Check(p.MatchesAppliedInventory(bag,belt),"exact postimage refused");
 bag[7]=S("AQ==",2);Check(!p.MatchesAppliedInventory(bag,belt),"source not debited accepted");bag[7]=S("AQ==",1);
 belt[19]=S("Ag==",1);Check(!p.MatchesAppliedInventory(bag,belt),"unplanned retired tail accepted");belt[19]=S();
 bag[bag.Length-1]=S("Ag==",1);Check(!p.MatchesAppliedInventory(bag,belt),"unplanned expansion item accepted");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;st.PendingGearTransfer=offer;
 Check(!RebirthRemoteGearAppliedConfirmation.TryConfirm(player,sender,offer.CreationId,"wrong")&&st.GearTransferPhase==RebirthGearTransferPhase.Prepared,"wrong acknowledgement advanced");
 RebirthGearPlayerFileWitness.Applied=false;Check(!RebirthRemoteGearAppliedConfirmation.TryConfirm(player,sender,offer.CreationId,offer.TransactionId)&&st.GearTransferPhase==RebirthGearTransferPhase.Prepared,"unverified owner file advanced");RebirthGearPlayerFileWitness.Applied=true;
 RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearAppliedConfirmation.TryConfirm(player,sender,offer.CreationId,offer.TransactionId),"unsaved offer confirmed");RebirthWorldCharacterRepository.Saved=true;
 Check(RebirthRemoteGearAppliedConfirmation.TryConfirm(player,sender,offer.CreationId,offer.TransactionId)&&st.GearTransferPhase==RebirthGearTransferPhase.OwnerApplied&&st.GearRevision==2&&st.EquippedGearBySlot.Count==0,"verified acknowledgement refused or prematurely committed gear");
 Check(RebirthRemoteGearAppliedConfirmation.TryConfirm(player,sender,offer.CreationId,offer.TransactionId)&&st.GearRevision==2,"repeated confirmation changed revision");
 st.GearTransferPhase=RebirthGearTransferPhase.GearCommitted;Check(!RebirthRemoteGearAppliedConfirmation.TryConfirm(player,sender,offer.CreationId,offer.TransactionId),"committed phase rewound");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;st.PendingGearTransfer=offer;RebirthWorldCharacterRepository.ThrowBeforeSave=true;
 Check(!RebirthRemoteGearAppliedConfirmation.TryConfirm(player,sender,offer.CreationId,offer.TransactionId)&&st.GearTransferPhase==RebirthGearTransferPhase.Prepared,"failed phase write advanced");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;st.PendingGearTransfer=offer;RebirthWorldCharacterRepository.OnSave=()=>RebirthWorldCharacterService.Identity.StorageKey="other";
 Check(!RebirthRemoteGearAppliedConfirmation.TryConfirm(player,sender,offer.CreationId,offer.TransactionId),"changed owner confirmed");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;st.PendingGearTransfer=offer;
 Check(!RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,offer.CreationId,offer.TransactionId),"prepared offer committed");
 st.GearTransferPhase=RebirthGearTransferPhase.OwnerApplied;RebirthGearPlayerFileWitness.Applied=false;
 Check(!RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,offer.CreationId,offer.TransactionId)&&st.EquippedGearBySlot.Count==0,"missing native receipt committed");RebirthGearPlayerFileWitness.Applied=true;
 RebirthWorldCharacterRepository.ThrowBeforeSave=true;Check(!RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,offer.CreationId,offer.TransactionId)&&st.GearTransferPhase==RebirthGearTransferPhase.OwnerApplied&&st.GearRevision==2&&st.EquippedGearBySlot.Count==0,"failed commit save lost custody");
 RebirthWorldCharacterRepository.ThrowBeforeSave=false;RebirthWorldCharacterRepository.Saved=true;
 Check(RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,offer.CreationId,offer.TransactionId)&&st.GearRevision==3&&st.EquippedGearBySlot["backpack"]=="pack"&&st.EquippedGearItemDataBySlot["backpack"]=="AQ=="&&ReferenceEquals(st.PendingGearTransfer,offer),"exact gear commit failed or recovery intent lost");
 RebirthGearPlayerFileWitness.Applied=false;Check(RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,offer.CreationId,offer.TransactionId)&&st.GearRevision==3,"committed retry demanded obsolete inventory or incremented twice");
 Check(!RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,offer.CreationId,"other"),"wrong committed transaction accepted");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;st.PendingGearTransfer=offer;st.GearTransferPhase=RebirthGearTransferPhase.OwnerApplied;
 RebirthSurvivorDefinitionRegistry.Profiles["pack"].GearSlotId="belt";Check(!RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,offer.CreationId,offer.TransactionId)&&st.GearRevision==2,"wrong-slot current profile committed");RebirthSurvivorDefinitionRegistry.Profiles["pack"].GearSlotId="backpack";
 RebirthWorldCharacterRepository.OnSave=()=>RebirthWorldCharacterService.Identity.StorageKey="other";Check(!RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,offer.CreationId,offer.TransactionId)&&st.GearTransferPhase==RebirthGearTransferPhase.OwnerApplied,"changed owner commit accepted");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;st.PendingGearTransfer=offer;
 Check(!RebirthGearTransferJournal.CancelRejected(st,offer.TransactionId,offer.CreationId,()=>false)&&st.LastGearSettlement==null&&st.PendingGearTransfer==offer&&st.GearRevision==2,"failed rejection save restores terminal receipt");
 Check(RebirthGearTransferJournal.CancelRejected(st,offer.TransactionId,offer.CreationId,()=>true)&&!st.LastGearSettlement.Applied&&st.LastGearSettlement.TransactionId==offer.TransactionId&&st.LastGearSettlement.GearRevision==3,"rejection saves terminal identity");
 var terminalNode=new System.Xml.Linq.XElement("support",st.LastGearSettlement.Write());Check(RebirthGearSettlement.TryRead(terminalNode,3,out var terminal)&&terminal.TransactionId==offer.TransactionId,"terminal receipt roundtrip");
 Check(!RebirthGearSettlement.TryRead(terminalNode,2,out _)&&RebirthGearSettlement.TryRead(new System.Xml.Linq.XElement("support"),3,out var absent)&&absent==null,"future receipt refuses and legacy absence accepted");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;st.PendingGearTransfer=offer;st.GearTransferPhase=RebirthGearTransferPhase.OwnerApplied;Check(RebirthRemoteGearAppliedConfirmation.TryCommit(player,sender,offer.CreationId,offer.TransactionId),"empty recovery commit setup");
 RebirthWorldCharacterRepository.ThrowBeforeSave=true;Check(!RebirthRemoteGearAppliedConfirmation.TryFinishEmptyRecovery(player,sender,offer.CreationId,offer.TransactionId)&&st.PendingGearTransfer==offer&&st.LastGearSettlement==null,"failed terminal save retains empty recovery custody");RebirthWorldCharacterRepository.ThrowBeforeSave=false;RebirthWorldCharacterRepository.Saved=true;
 Check(RebirthRemoteGearAppliedConfirmation.TryFinishEmptyRecovery(player,sender,offer.CreationId,offer.TransactionId)&&st.PendingGearTransfer==null&&st.LastGearSettlement.Applied&&st.LastGearSettlement.GearRevision==3,"empty recovery saves applied terminal result");
 Check(RebirthRemoteGearAppliedConfirmation.TryGetSettlement(player,sender,offer.CreationId,offer.TransactionId,out var savedTerminal)&&savedTerminal.Applied,"verified saved terminal lookup");
 RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearAppliedConfirmation.TryGetSettlement(player,sender,offer.CreationId,offer.TransactionId,out _),"unsaved terminal lookup refuses");RebirthWorldCharacterRepository.Saved=true;
 Check(!RebirthRemoteGearAppliedConfirmation.TryGetSettlement(player,sender,offer.CreationId,Guid.NewGuid().ToString("N"),out _),"foreign terminal lookup refuses");
 var recoveryPlan=new RebirthGearInventoryPlan{BagSlotsBefore=52,BagSlotsAfter=52,BeltSlotsBefore=4,BeltSlotsAfter=4,GearBefore=new RebirthGearInventoryPlan.Stack{ItemData="AQ==",Count=1},GearAfter=new RebirthGearInventoryPlan.Stack{ItemData="Ag==",Count=1}};
 recoveryPlan.Changes.Add(new RebirthGearInventoryPlan.Change{IsBag=true,Index=0,Before=new RebirthGearInventoryPlan.Stack{ItemData="Ag==",Count=1}});recoveryPlan.Recovery.Add(new RebirthGearInventoryPlan.RecoveryEntry{Origin=RebirthGearInventoryPlan.RecoveryOrigin.DisplacedGear,SourceIndex=-1,Item=new RebirthGearInventoryPlan.Stack{ItemData="AQ==",Count=1}});
 Check(RebirthGearTransferState.TryCreate(Guid.NewGuid().ToString("N"),offer.CreationId,"backpack",2,recoveryPlan,out var recoveryOffer),"recovery journal offer");recoveryOffer.TryGetRecoveryManifest(out var recoveryManifest);var publicationId=Guid.Parse((string)recoveryManifest.ToXml().Elements().First().Attribute("id"));
 Check(RebirthGearRecoveryAttempt.TryCreate(publicationId,900,1.5f,45,-3,90,1800,123,7,out var attempt),"native placement intent creation");
 var recoveryState=new RebirthWorldSupportState{GearRevision=3,PendingGearTransfer=recoveryOffer,GearTransferPhase=RebirthGearTransferPhase.GearCommitted};recoveryState.EquippedGearBySlot["backpack"]="newPack";recoveryState.EquippedGearItemDataBySlot["backpack"]="Ag==";
 Check(!RebirthGearTransferJournal.RecordRecoveryAttempt(recoveryState,recoveryOffer.TransactionId,recoveryOffer.CreationId,attempt,()=>false)&&ReferenceEquals(recoveryState.PendingGearTransfer,recoveryOffer),"failed attempt save rolls back intent");
 Check(RebirthGearTransferJournal.RecordRecoveryAttempt(recoveryState,recoveryOffer.TransactionId,recoveryOffer.CreationId,attempt,()=>true)&&recoveryState.PendingGearTransfer.TryGetRecoveryAttempt(publicationId,out var restoredAttempt)&&restoredAttempt.EntityId==900&&restoredAttempt.X==1.5f&&restoredAttempt.WorldTime==123,"saved attempt retained with exact placement");
 int repeatSaves=0;Check(!RebirthGearTransferJournal.RecordRecoveryAttempt(recoveryState,recoveryOffer.TransactionId,recoveryOffer.CreationId,attempt,()=>{repeatSaves++;return true;})&&repeatSaves==0,"existing attempted publication cannot be republished");
 var recoverySaved=RebirthGearTransferPersistence.Write(3,recoveryState.PendingGearTransfer,RebirthGearTransferPhase.GearCommitted);Check(RebirthGearTransferPersistence.TryRead(recoverySaved,out var recoveryRevision,out var recoveredPending,out var recoveryPhase)&&recoveryRevision==3&&recoveryPhase==RebirthGearTransferPhase.GearCommitted&&recoveredPending.TryGetRecoveryAttempt(publicationId,out _),"attempt survives actual journal serialization");
 recoverySaved.SetAttributeValue("phase",0);recoverySaved.SetAttributeValue("revision",2);Check(!RebirthGearTransferPersistence.TryRead(recoverySaved,out _,out _,out _),"attempt cannot load as prepared custody");
 Check(!RebirthGearRecoveryAttempt.TryCreate(publicationId,901,float.NaN,1,1,0,1800,1,out _)&&!RebirthGearRecoveryAttempt.TryCreate(publicationId,-1,1,1,1,0,1800,1,out _),"invalid placement and entity ID refuse");
 RebirthGearRecoveryAttempt.TryCreate(Guid.NewGuid(),901,1,1,1,0,1800,1,out var foreignAttempt);Check(!recoveryState.PendingGearTransfer.TryAppendRecoveryAttempt(foreignAttempt,out _),"foreign publication refuses");
 RebirthGearRecoveryAttempt.TryCreate(publicationId,901,1,1,1,0,3600,1,out var wrongLifetime);Check(!recoveryOffer.TryAppendRecoveryAttempt(wrongLifetime,out _),"timed item lifetime cannot become backpack lifetime");
 Reset();st=RebirthWorldCharacterService.Record.Support;st.GearRevision=3;st.GearTransferPhase=RebirthGearTransferPhase.GearCommitted;st.PendingGearTransfer=recoveryOffer;st.EquippedGearBySlot["backpack"]="newPack";st.EquippedGearItemDataBySlot["backpack"]="Ag==";
 RebirthGearRecoveryAttempt.TryCreate(publicationId,900,1,2,3,0,1800,123,8,out var wrongOwnerAttempt);int writesBeforeOwner=RebirthWorldCharacterRepository.Writes;
 Check(!RebirthRemoteGearAppliedConfirmation.TryRecordRecoveryAttempt(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId,wrongOwnerAttempt,out _)&&RebirthWorldCharacterRepository.Writes==writesBeforeOwner&&ReferenceEquals(st.PendingGearTransfer,recoveryOffer),"foreign owner attempt rejected before persistence");
 RebirthWorldCharacterRepository.OnSave=()=>player.entityId=8;
 Check(!RebirthRemoteGearAppliedConfirmation.TryRecordRecoveryAttempt(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId,attempt,out _)&&ReferenceEquals(st.PendingGearTransfer,recoveryOffer),"owner entity changed during save cannot return checkpoint");player.entityId=7;RebirthWorldCharacterRepository.OnSave=null;
 RebirthWorldCharacterRepository.Saved=false;Check(!RebirthRemoteGearAppliedConfirmation.TryRecordRecoveryAttempt(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId,attempt,out _)&&ReferenceEquals(st.PendingGearTransfer,recoveryOffer),"unsaved original cannot begin recovery attempt");RebirthWorldCharacterRepository.Saved=true;
 RebirthWorldCharacterRepository.ThrowBeforeSave=true;Check(!RebirthRemoteGearAppliedConfirmation.TryRecordRecoveryAttempt(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId,attempt,out _)&&ReferenceEquals(st.PendingGearTransfer,recoveryOffer),"failed authenticated attempt checkpoint rolls back");RebirthWorldCharacterRepository.ThrowBeforeSave=false;RebirthWorldCharacterRepository.Saved=true;
 Check(RebirthRemoteGearAppliedConfirmation.TryRecordRecoveryAttempt(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId,attempt,out var authenticatedCheckpoint)&&ReferenceEquals(st.PendingGearTransfer,authenticatedCheckpoint)&&authenticatedCheckpoint.TryGetRecoveryAttempt(publicationId,out _),"authenticated final-file witnessed attempt checkpoint");
 Check(!RebirthRemoteGearAppliedConfirmation.TryRecordRecoveryAttempt(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId,attempt,out _),"authenticated retry cannot republish existing attempt");
 int prematureFinishes=0;Check(!RebirthGearTransferJournal.FinishRecovery(st,recoveryOffer.TransactionId,recoveryOffer.CreationId,()=>{prematureFinishes++;return true;})&&prematureFinishes==0,"nonempty recovery cannot finish from attempted spawn alone");
 var beforeReceipt=st.PendingGearTransfer;
 Check(!RebirthGearTransferJournal.RecordRecoveryPublicationReceipt(st,recoveryOffer.TransactionId,recoveryOffer.CreationId,publicationId,()=>false)&&ReferenceEquals(st.PendingGearTransfer,beforeReceipt),"failed publication receipt save restores original custody");
 Check(RebirthGearTransferJournal.RecordRecoveryPublicationReceipt(st,recoveryOffer.TransactionId,recoveryOffer.CreationId,publicationId,()=>true)&&st.PendingGearTransfer.HasRecoveryPublicationReceipt(publicationId),"publication receipt journal saves immutable afterimage");
 int duplicateReceiptWrites=0;Check(!RebirthGearTransferJournal.RecordRecoveryPublicationReceipt(st,recoveryOffer.TransactionId,recoveryOffer.CreationId,publicationId,()=>{duplicateReceiptWrites++;return true;})&&duplicateReceiptWrites==0,"duplicate publication receipt refuses before write");
 st.PendingGearTransfer=beforeReceipt;
 st.PendingGearTransfer.TryAppendRecoveryPublicationReceipt(publicationId,out var finishedReceiptOffer);st.PendingGearTransfer=finishedReceiptOffer;Check(!RebirthGearTransferJournal.FinishRecovery(st,recoveryOffer.TransactionId,recoveryOffer.CreationId,()=>false)&&ReferenceEquals(st.PendingGearTransfer,finishedReceiptOffer),"failed receipt terminal save preserves pending recovery");
 Check(!RebirthRemoteGearAppliedConfirmation.TryFinishEmptyRecovery(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId)&&ReferenceEquals(st.PendingGearTransfer,finishedReceiptOffer),"empty-only finish refuses nonempty recovery with receipts");
 RebirthWorldCharacterRepository.Saved=false;int beforeUnsavedFinish=RebirthWorldCharacterRepository.Writes;Check(!RebirthRemoteGearAppliedConfirmation.TryFinishRecovery(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId)&&RebirthWorldCharacterRepository.Writes==beforeUnsavedFinish&&ReferenceEquals(st.PendingGearTransfer,finishedReceiptOffer),"unsaved publication receipts cannot settle");RebirthWorldCharacterRepository.Saved=true;
 RebirthWorldCharacterRepository.ThrowBeforeSave=true;Check(!RebirthRemoteGearAppliedConfirmation.TryFinishRecovery(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId)&&ReferenceEquals(st.PendingGearTransfer,finishedReceiptOffer)&&st.GearTransferPhase==RebirthGearTransferPhase.GearCommitted,"failed nonempty terminal save preserves receipt custody");RebirthWorldCharacterRepository.ThrowBeforeSave=false;RebirthWorldCharacterRepository.Saved=true;
 Check(RebirthRemoteGearAppliedConfirmation.TryFinishRecovery(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId)&&st.PendingGearTransfer==null&&st.LastGearSettlement!=null,"authenticated saved publication receipts permit terminal settlement");
 Check(RebirthRemoteGearAppliedConfirmation.TryGetSettlement(player,sender,recoveryOffer.CreationId,recoveryOffer.TransactionId,out var recoveredSettlement)&&recoveredSettlement.Applied,"nonempty recovery terminal readable for owner response");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;RebirthGearTransferJournal.Prepare(st,offer,offer.CreationId,()=>true);
 int rejectWrites=RebirthWorldCharacterRepository.Writes;
 Check(!RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId)&&ReferenceEquals(st.PendingGearTransfer,offer)&&RebirthWorldCharacterRepository.Writes==rejectWrites,"no independent rejected owner witness cannot cancel");
 RebirthGearPlayerFileWitness.Rejected=true;RebirthWorldCharacterRepository.Saved=false;
 Check(!RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId)&&ReferenceEquals(st.PendingGearTransfer,offer),"unsaved prepared intent cannot cancel");RebirthWorldCharacterRepository.Saved=true;
 st.GearTransferPhase=RebirthGearTransferPhase.OwnerApplied;
 Check(!RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId),"owner applied cannot be rejected");st.GearTransferPhase=RebirthGearTransferPhase.Prepared;
 st.PendingLibraryTransfer=new object();Check(!RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId),"competing library custody cannot cancel");st.PendingLibraryTransfer=null;
 RebirthWorldCharacterRepository.ThrowBeforeSave=true;
 Check(!RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId)&&ReferenceEquals(st.PendingGearTransfer,offer)&&st.GearRevision==2&&st.LastGearSettlement==null,"failed rejection terminal write retains original pending and revision");RebirthWorldCharacterRepository.ThrowBeforeSave=false;RebirthWorldCharacterRepository.Saved=true;
 Check(RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId)&&st.PendingGearTransfer==null&&st.GearRevision==3&&st.LastGearSettlement!=null&&!st.LastGearSettlement.Applied,"saved rejection terminal clears intent once without equipping");
 int terminalWrites=RebirthWorldCharacterRepository.Writes;
 Check(!RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId)&&RebirthWorldCharacterRepository.Writes==terminalWrites&&st.GearRevision==3,"repeated rejection does not increment revision");
 Check(RebirthRemoteGearAppliedConfirmation.TryGetSettlement(player,sender,offer.CreationId,offer.TransactionId,out var rejectedTerminal)&&!rejectedTerminal.Applied,"saved rejected terminal can replay");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;RebirthGearTransferJournal.Prepare(st,offer,offer.CreationId,()=>true);RebirthGearPlayerFileWitness.Rejected=true;
 RebirthWorldCharacterRepository.OnSave=()=>RebirthWorldCharacterService.Identity.StorageKey="other";
 Check(!RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId)&&ReferenceEquals(st.PendingGearTransfer,offer)&&st.GearRevision==2,"identity replacement during rejection save cannot settle");  Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;RebirthGearTransferJournal.Prepare(st,offer,offer.CreationId,()=>true);RebirthGearPlayerFileWitness.Rejected=true;
 RebirthWorldCharacterRepository.OnSave=()=>RebirthWorldCharacterRepository.Saved=false;
 Check(!RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId)&&ReferenceEquals(st.PendingGearTransfer,offer)&&st.GearRevision==2,"missing terminal file witness cannot release rejected custody");
 Reset();Build(out offer);st=RebirthWorldCharacterService.Record.Support;RebirthGearTransferJournal.Prepare(st,offer,offer.CreationId,()=>true);RebirthGearPlayerFileWitness.Rejected=true;RebirthWorldCharacterRepository.ThrowAfterSave=true;
 Check(RebirthRemoteGearAppliedConfirmation.TryCancelRejected(player,sender,offer.CreationId,offer.TransactionId)&&st.PendingGearTransfer==null&&st.GearRevision==3,"uncertain completed rejection save resolves through terminal witness");var uniqueSnapshot=new RebirthGearInventorySnapshot{Bag=Enumerable.Range(0,52).Select(_=>S()).ToArray(),Belt=Enumerable.Range(0,20).Select(_=>S()).ToArray(),OwnedBeltSlots=4};
uniqueSnapshot.Bag[8]=S("AQ==",2);Check(RebirthGearUniqueSource.TryFind(uniqueSnapshot,"AQ==",out var uniqueBag,out var uniqueIndex)&&uniqueBag&&uniqueIndex==8,"full-data unique bag cell selected");
uniqueSnapshot.Belt[2]=S("AQ==",1);Check(!RebirthGearUniqueSource.TryFind(uniqueSnapshot,"AQ==",out uniqueBag,out uniqueIndex)&&uniqueIndex==-1,"identical cross-inventory sources refuse ambiguity");
uniqueSnapshot.Bag[8]=S();Check(RebirthGearUniqueSource.TryFind(uniqueSnapshot,"AQ==",out uniqueBag,out uniqueIndex)&&!uniqueBag&&uniqueIndex==2,"full-data unique belt cell selected");
uniqueSnapshot.Belt[19]=S("AQ==",1);Check(RebirthGearUniqueSource.TryFind(uniqueSnapshot,"AQ==",out _,out uniqueIndex)&&uniqueIndex==2,"retired belt matching bytes cannot become source");
uniqueSnapshot.Belt[2]=S();Check(!RebirthGearUniqueSource.TryFind(uniqueSnapshot,"AQ==",out _,out _),"only retired belt source refused");
uniqueSnapshot.Bag[8]=S("Ag==",1);Check(!RebirthGearUniqueSource.TryFind(uniqueSnapshot,"AQ==",out _,out _),"different full bytes never match source");
Check(!RebirthGearUniqueSource.TryFind(null,"AQ==",out _,out _)&&!RebirthGearUniqueSource.TryFind(uniqueSnapshot,null,out _,out _),"missing source evidence refused");Console.WriteLine("PASS "+checks+" actual builder/planner/codec/envelope/journal/witness/preparation checks; source admission, native decoding, repository I/O and services are doubles. No live gear custody.");
 }
}