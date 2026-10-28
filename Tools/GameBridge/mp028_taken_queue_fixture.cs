// AUTHORED_NOT_RUN. Saved native player file/codec and existing queue-source
// core are explicit adapters; complete actual take-proof + handoff composed.
using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Text;using System.Xml.Linq;
class ItemValue{internal Dictionary<string,object> Values=new Dictionary<string,object>();internal bool TryGetMetadata<T>(string name,out T value){if(Values.TryGetValue(name,out var data)&&data is T typed){value=typed;return true;}value=default(T);return false;}}
class ItemStack{internal ItemValue itemValue=new ItemValue();internal int count;internal ItemStack Clone()=>new ItemStack{count=count,itemValue=itemValue};internal static ItemStack[] Clone(ItemStack[] cells)=>cells.Select(cell=>cell.Clone()).ToArray();}
class Recipe{internal bool Prepared=true;internal List<ItemStack> ingredients=new List<ItemStack>{new ItemStack()};internal IReadOnlyList<ItemStack> Outputs=new[]{new ItemStack{count=4},new ItemStack{count=2}};}
class World{internal bool Remote;internal EntityPlayerLocal Player;internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;}
class Buffs{internal Dictionary<string,float> Values=new Dictionary<string,float>();internal float GetCustomVar(string key)=>Values.TryGetValue(key,out var value)?value:0;}
class EntityPlayerLocal{internal World world;internal int Died=2;internal bool Dead;internal Buffs Buffs=new Buffs();internal bool IsDead()=>Dead;}
class PlayerUi{internal EntityPlayerLocal entityPlayer;}
class Ui{internal PlayerUi playerUI=new PlayerUi();}
class XUiC_RecipeStack{internal Recipe recipe=new Recipe();internal Ui xui=new Ui();}
class Grid{internal ItemStack[] Cells={new ItemStack{count=9}};internal ItemStack[] GetSlots()=>Cells;}
class XUiC_RebirthCookingStation{internal Grid outputWindow=new Grid();}
class GameManager{internal static GameManager Instance=new GameManager();internal World World;}
static class ThreadManager{internal static bool IsMainThread()=>true;}
static class RebirthCamperCharacterAccess{internal static string CurrentCreation(EntityPlayerLocal player)=>"creation";}
class RebirthStablePlayerIdentity{internal string CanonicalId="account",StorageKey="owner";internal static bool TryResolveServerEntity(EntityPlayerLocal player,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
class PlayerDataFile{internal int deaths=2;internal float Phase=3;internal byte[] Native={1,2,3,4,5,6};}
static class RebirthGearNativePlayerFile{internal static bool Available=true;internal static PlayerDataFile Data;internal static bool TryRead(RebirthStablePlayerIdentity identity,out PlayerDataFile data){data=Data;return Available;}}
static class RebirthProductiveEarnedUiOwnerFile{internal static byte[] DeferredNativeOwnerImage(PlayerDataFile data)=>data.Native;}
static class RebirthStationPublicationRecord{internal static string Digest(byte[] native)=>Convert.ToBase64String(native);}
internal static class RebirthCookingOutputDelivery{
 private const string Prefix="rebirth.cooking.delivery.",Receipt="rbCookingTake_",Held="rbCookingTakeHeld";
 private static bool HasPrepared(Recipe recipe)=>recipe?.Prepared==true;
 private static string WorldKey(World world)=>"world";
 private static bool SavedPhase(PlayerDataFile data,string receipt,out float phase){phase=data.Phase;return true;}
 private static bool TryReadPreparedOutputs(Recipe recipe,out IReadOnlyList<ItemStack> outputs){outputs=recipe.Outputs;return recipe.Prepared;}
 private static string Image(ItemStack[] cells)=>string.Join(",",cells.Select(cell=>cell.count));
 // ACTUAL_TAKE_PROOF
}
internal static class RebirthCookingMillingPublication{
 internal static int Admissions;internal static XElement Proof;internal static ItemStack[] Output;
 // Existing actual queue projection/admission/native source is independently
 // covered by mpA_cooking_milling_publication and mp028_milling_saved_output.
 private static bool PublishUiCore(XUiC_RecipeStack card,XUiC_RebirthCookingStation window,ItemStack[] output,XElement taken){Admissions++;Proof=taken;Output=output;return true;}
 // ACTUAL_TAKE_HANDOFF
}
class Cases{
 static int checks;static void Need(bool value,string label){checks++;if(!value)throw new Exception(label);}
 static XUiC_RecipeStack Fresh(){RebirthGearNativePlayerFile.Available=true;RebirthGearNativePlayerFile.Data=new PlayerDataFile();RebirthCookingMillingPublication.Admissions=0;var world=new World();var player=new EntityPlayerLocal{world=world};world.Player=player;GameManager.Instance.World=world;var card=new XUiC_RecipeStack();card.xui.playerUI.entityPlayer=player;var marker=card.recipe.ingredients[0].itemValue;string prefix="rebirth.cooking.delivery.";var token=Guid.NewGuid();marker.Values["rebirth.cooking.queue.token"]=token.ToString("N");marker.Values[prefix+"owner"]="account";marker.Values[prefix+"creation"]="creation";marker.Values[prefix+"deaths"]=2;marker.Values[prefix+"world"]="world";player.Buffs.Values["rbCookingTake_"+token.ToString("N")]=3;return card;}
 public static void Main(){var card=Fresh();Need(RebirthCookingOutputDelivery.TryReadSavedTakeProof(card,out var proof)&&(int)proof.Attribute("phase")==3&&(string)proof.Element("combinedOutputs")=="4,2","positive saved terminal lost combined original");var window=new XUiC_RebirthCookingStation();Need(RebirthCookingMillingPublication.PublishOwnerTakenUi(card,window)&&RebirthCookingMillingPublication.Admissions==1&&RebirthCookingMillingPublication.Output[0].count==9&&!ReferenceEquals(RebirthCookingMillingPublication.Output,window.outputWindow.Cells),"queue handoff inserted prepared outputs or lost current unchanged station output");
  card=Fresh();RebirthGearNativePlayerFile.Data.Phase=2;Need(!RebirthCookingMillingPublication.PublishOwnerTakenUi(card,window)&&RebirthCookingMillingPublication.Admissions==0,"unsettled owner allowed queue retirement");
  card=Fresh();RebirthGearNativePlayerFile.Available=false;Need(!RebirthCookingOutputDelivery.TryReadSavedTakeProof(card,out proof),"runtime marker substituted for saved native file");
  card=Fresh();card.xui.playerUI.entityPlayer.Died++;Need(!RebirthCookingOutputDelivery.TryReadSavedTakeProof(card,out proof),"different life inherited taken original");
  card=Fresh();card.recipe.ingredients[0].itemValue.Values["rebirth.cooking.delivery.owner"]="foreign";Need(!RebirthCookingOutputDelivery.TryReadSavedTakeProof(card,out proof),"foreign owner original retired queue");
  card=Fresh();card.xui.playerUI.entityPlayer.Buffs.Values["rbCookingTakeHeld"]=1;Need(!RebirthCookingOutputDelivery.TryReadSavedTakeProof(card,out proof),"held native owner allowed queue retirement");
  card=Fresh();card.xui.playerUI.entityPlayer.world.Remote=true;Need(!RebirthCookingOutputDelivery.TryReadSavedTakeProof(card,out proof),"remote client borrowed host saved file");
  Console.WriteLine("PASS "+checks+" actual saved Take proof/handoff assertions; native file/codec/queue core explicitly adapted.");}
}
