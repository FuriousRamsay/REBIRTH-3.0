// Production payload helper compiled unchanged; native Write/Read and owner models doubled.
public static class ThreadManager {public static bool IsMainThread()=>true;}
public class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter():base(new MemoryStream()){}public void SetBaseStream(Stream stream){OutStream=stream;}}
public class PooledBinaryReader:BinaryReader {private BinaryReader current;public PooledBinaryReader():base(new MemoryStream()){}public void SetBaseStream(Stream stream){current=new BinaryReader(stream);}public override byte ReadByte()=>current.ReadByte();public override int ReadInt32()=>current.ReadInt32();public override byte[] ReadBytes(int count)=>current.ReadBytes(count);public override string ReadString()=>current.ReadString();}
public static class MemoryPools {public static WriterPool poolBinaryWriter=new WriterPool();public static ReaderPool poolBinaryReader=new ReaderPool();public class WriterPool{public PooledBinaryWriter AllocSync(bool value)=>new PooledBinaryWriter();}public class ReaderPool{public PooledBinaryReader AllocSync(bool value)=>new PooledBinaryReader();}}
public class ItemClass {public bool HasQuality=false;}
public class ItemValue {public int type=1,Quality;public ItemClass ItemClass=new ItemClass();}
public class ItemStack {public ItemStack Clone()=>new ItemStack{itemValue=itemValue,count=count};public ItemValue itemValue=new ItemValue();public int count=1;}
public class CraftCompleteData {public CraftCompleteData(){}public CraftCompleteData(int actor,ItemStack stack,string name,string scrap,int xp,ushort cycles){CrafterEntityID=actor;CraftedItemStack=stack;RecipeName=name;ItemScrapped=scrap;CraftExpGain=xp;RecipeUsedCount=cycles;}public int CrafterEntityID=7,CraftExpGain=3;public ushort RecipeUsedCount=1;public string RecipeName="nativeRecipe",ItemScrapped="";public ItemStack CraftedItemStack=new ItemStack();public void Write(PooledBinaryWriter writer){writer.Write(CrafterEntityID);writer.Write(CraftExpGain);writer.Write((int)RecipeUsedCount);writer.Write(RecipeName);writer.Write(ItemScrapped);writer.Write(CraftedItemStack.itemValue.type);writer.Write(CraftedItemStack.count);}public void Read(PooledBinaryReader reader){CrafterEntityID=reader.ReadInt32();CraftExpGain=reader.ReadInt32();RecipeUsedCount=(ushort)reader.ReadInt32();RecipeName=reader.ReadString();ItemScrapped=reader.ReadString();CraftedItemStack=new ItemStack{itemValue=new ItemValue{type=reader.ReadInt32()},count=reader.ReadInt32()};}}
public class Recipe {public int craftExpGain=3,itemValueType=1,count=1,craftingTier;public string GetName()=>"nativeRecipe";}
public static class XUiM_Recipes {public static object GetRecipes()=>new object();}
public class RebirthStationGridAdmission {public bool IsPerBatch=false;public int OriginalBatches=1;public string JobId=MobileRemoteRewardPayloadFixture.Operation.ToString("N"),CreationId="creation";public bool IsPublicationAttempted=true;public XElement Write()=>new XElement("stationAdmission",new XAttribute("job",JobId),new XAttribute("creation",CreationId),new XAttribute("batch",IsPerBatch),new XAttribute("batches",OriginalBatches));public bool TryGetMobileAddress(out int address){address=1;return true;}public bool TryMaterialize(object definitions,out Recipe recipe,out object before,out object after){recipe=new Recipe{count=IsPerBatch?10000:1};before=null;after=null;return true;}public static bool TryReadStored(XElement node,out RebirthStationGridAdmission admission){admission=new RebirthStationGridAdmission{IsPerBatch=(bool)node.Attribute("batch"),OriginalBatches=(int)node.Attribute("batches"),JobId=(string)node.Attribute("job"),CreationId=(string)node.Attribute("creation")};return node.Name=="stationAdmission"&&admission.JobId!=null&&admission.CreationId!=null;}}
public class Binding {public RebirthStationGridAdmission Admission=new RebirthStationGridAdmission();public EntityPlayerLocal Player;}
public class RebirthCamperStationTrainingPlan {public Binding Binding=new Binding();public bool IsCurrent()=>true;}
public static class RebirthStationCompletionReceipt {public static bool HasReservedMarker(CraftCompleteData data)=>false;}
public class Buffs {public Dictionary<string,float> CVars=new Dictionary<string,float>();public float GetCustomVar(string key)=>CVars.TryGetValue(key,out var value)?value:0;public void SetCustomVar(string key,float value,bool sync){CVars[key]=value;}}
public class EntityPlayerLocal {public int entityId=7,Calls;public bool Throw;public World world;public Buffs Buffs=new Buffs();public bool IsDead()=>false;public void GiveExp(CraftCompleteData reward){Calls++;if(Throw)throw new Exception("partial effect");}}
public class World {public EntityPlayerLocal Player;public bool Remote=true;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;}
public class GameManager {public static GameManager Instance=new GameManager();public World World;}
public static class RebirthCharacterCreationHoldService {public static bool IsHeld(EntityPlayerLocal player)=>false;}
public static class RebirthSurvivorRequestScope {public static bool Matches(string first,string second)=>first==second;}
public static class RebirthSurvivorClientState {public static string Creation="creation";public static string GetProjectedCreationId(EntityPlayerLocal player)=>Creation;}
public static class RebirthCamperStationNativeRewardReceipt {public static string Key(RebirthStationGridAdmission admission)=>"native_"+admission.JobId;}
public static class MobileRemoteRewardPayloadFixture {
 public static Guid Operation=Guid.ParseExact("0123456789abcdef0123456789abcdef","N");
 private static void Check(bool value,string label){if(!value)throw new Exception(label);}
 private static EntityPlayerLocal Player(){var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;return player;}
 public static void Run(){
  var player=Player();var plan=new RebirthCamperStationTrainingPlan{Binding=new Binding{Player=player}};
  Check(RebirthCamperStationRemoteRewardPayload.TryCreate(plan,new CraftCompleteData(),out var payload),"server bounded fixed payload created");
  Check(!RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Operation,payload)&&player.Calls==0,"authenticated outer transport2 required");
  string outer="rbCamperOwner_"+Operation.ToString("N");player.Buffs.SetCustomVar(outer,2,true);
  var trailing=new byte[payload.Length+1];Array.Copy(payload,trailing,payload.Length);Check(!RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Operation,trailing),"trailing frame bytes refused");
  Check(!RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Guid.NewGuid(),payload),"operation mismatch refused");
  RebirthSurvivorClientState.Creation="other";Check(!RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Operation,payload),"owner creation mismatch refused");RebirthSurvivorClientState.Creation="creation";
  Check(!RebirthCamperStationRemoteRewardPayload.TryCreate(plan,new CraftCompleteData{CraftExpGain=999},out _),"reward XP must match actual materialized native recipe");
  Check(RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Operation,payload)&&player.Calls==1,"native owner effect applies exactly once");
  Check(RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Operation,payload)&&player.Calls==1,"inner1 duplicate never replays");
  player=Player();player.Buffs.SetCustomVar(outer,2,true);player.Throw=true;
  Check(!RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Operation,payload)&&player.Calls==1&&player.Buffs.GetCustomVar("native_"+Operation.ToString("N"))==2,"partial GiveExp retains uncertain inner2");
  Check(!RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Operation,payload)&&player.Calls==1,"partial native reward never replays");
  player=Player();player.Buffs.SetCustomVar(outer,2,true);plan=new RebirthCamperStationTrainingPlan{Binding=new Binding{Player=player,Admission=new RebirthStationGridAdmission{IsPerBatch=true,OriginalBatches=7}}};Check(RebirthCamperStationRemoteRewardPayload.TryCreate(plan,new CraftCompleteData{CraftExpGain=21,RecipeUsedCount=7,CraftedItemStack=new ItemStack{count=70000}},out payload)&&payload[0]==2,"perbatch version2 preserves aggregate beyond UInt16 native stack");Check(RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Operation,payload)&&player.Calls==1,"exact original aggregate perbatch applies once");Check(RebirthCamperStationRemoteRewardPayload.TryApplyReward(player,Operation,payload)&&player.Calls==1,"perbatch duplicate receipt no replay");
  Console.WriteLine("PASS actual remote reward payload helper source fixture");
 }
}
