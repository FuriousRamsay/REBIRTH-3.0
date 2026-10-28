// Actual heat merge/unique selection and native ModMaxUseTimes bodies are
// injected by the deferred runner. Native serialization/effect lookup doubled.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
enum PassiveEffects {DegradationMax=1,Damage=2}
static class Mathf {public static int RoundToInt(float n)=>(int)Math.Round(n);public static int Max(int a,int b)=>Math.Max(a,b);}
class ItemClass{}
class ItemValue {
 public struct Stat {public PassiveEffects type;public short value;public bool isBoosted;}
 public Stat[] Stats;public float UseTimes;public string Mods="mod",Texture="texture";public Dictionary<string,object> Metadata=new Dictionary<string,object>();public ItemClass ItemClass=new ItemClass();
 public int MaxUseTimes=>ModMaxUseTimes(Stats.Where(x=>x.type==PassiveEffects.DegradationMax).Select(x=>(int)x.value).FirstOrDefault(),this);
 public bool IsEmpty()=>false;public bool TryGetMetadata(string k,out string v){v=null;if(!Metadata.TryGetValue(k,out var x)||!(x is string))return false;v=(string)x;return true;}public bool TryGetMetadata(string k,out float v){v=0;if(!Metadata.TryGetValue(k,out var x)||!(x is float))return false;v=(float)x;return true;}public void SetMetadata(string k,string v){Metadata[k]=v;}public void RemoveMetaData(string k){Metadata.Remove(k);}
// NATIVE_MOD_MAX
}
class ItemStack {public ItemValue itemValue;public int count;public ItemStack(ItemValue v,int n){itemValue=v;count=n;}public bool IsEmpty()=>count==0;}
class World{}
class EntityPlayer {public int entityId=1;}
class PlayerDataFile{public int deaths;}
class GameManager {public static GameManager Instance=new GameManager();public World World=new World();}
static class GameIO {public static string GetSaveGameDir()=>"test-original-world";}
class RebirthStablePlayerIdentity {public static bool TryResolveServerEntity(EntityPlayer player,out RebirthStablePlayerIdentity identity){identity=new RebirthStablePlayerIdentity();return true;}}
class RebirthCamperEnergyActorImage {public ItemStack[] Bag,Belt;}
static class RebirthCamperEnergySavedOwner {public static bool Ready;public static RebirthCamperEnergyActorImage Actor;public static bool TryRead(RebirthStablePlayerIdentity identity,out RebirthCamperEnergyActorImage actor,out PlayerDataFile saved){actor=Actor;saved=new PlayerDataFile();return Ready;}}
static class RebirthNativeItemCodec {
 public static string Encode(ItemValue v){using(var s=new MemoryStream())using(var w=new BinaryWriter(s)){w.Write(v.UseTimes);w.Write(v.Mods);w.Write(v.Texture);w.Write(v.Stats.Length);foreach(var x in v.Stats){w.Write((int)x.type);w.Write(x.value);w.Write(x.isBoosted);}w.Write(v.Metadata.Count);foreach(var x in v.Metadata){w.Write(x.Key);w.Write(x.Value is float);if(x.Value is float f)w.Write(f);else w.Write((string)x.Value);}return Convert.ToBase64String(s.ToArray());}}
 public static bool TryDecode(string text,out ItemValue v){v=null;try{using(var s=new MemoryStream(Convert.FromBase64String(text)))using(var r=new BinaryReader(s)){v=new ItemValue{UseTimes=r.ReadSingle(),Mods=r.ReadString(),Texture=r.ReadString()};v.Stats=new ItemValue.Stat[r.ReadInt32()];for(int i=0;i<v.Stats.Length;i++)v.Stats[i]=new ItemValue.Stat{type=(PassiveEffects)r.ReadInt32(),value=r.ReadInt16(),isBoosted=r.ReadBoolean()};int n=r.ReadInt32();for(int i=0;i<n;i++){string k=r.ReadString();v.Metadata[k]=r.ReadBoolean()?(object)r.ReadSingle():r.ReadString();}return s.Position==s.Length;}}catch{return false;}}
}
static class RebirthItemProvenanceAdapter {public static bool TryApplyOwnedCorrection(ItemValue target,ItemValue source){foreach(var entry in source.Metadata)if(entry.Key.StartsWith("RebirthProv")||entry.Key=="RebirthDurabilityOriginalMaxUseTimes")target.Metadata[entry.Key]=entry.Value;return true;}}
static class RebirthWorkmanshipSignatureService {public const string HeatTreatmentBonusId="background_bonus.heat_treatment";}
static class RebirthMetalHeatTreatmentCorrection {
 private const string OriginalKey="RebirthHeatOriginalV1",AppliedKey="RebirthHeatAppliedV1";private const int MaximumImage=262144;
// RECEIPT_TYPE
// UPLOAD_TYPE
 private static readonly Dictionary<int,Upload> Uploads=new Dictionary<int,Upload>();
// PRODUCTION_METHODS
 public static bool Select(ItemStack[] bag,ItemStack[] belt,Receipt r,bool after)=>Unique(bag,belt,r,after)!=null;
 public static bool SupportedStat(ItemValue item)=>Stat(item,out _,out _,out _);
 public static bool Prove(EntityPlayer owner,Receipt r,bool after)=>NativeProof(owner,r,after);
 public static void UploadForFixture(EntityPlayer owner,Receipt r,ItemStack[] bag,World world=null){Uploads[owner.entityId]=new Upload{World=world??GameManager.Instance.World,Player=owner,Creation=r.Creation,Deaths=r.Deaths,Directory=GameIO.GetSaveGameDir(),Bag=bag,Belt=new ItemStack[0]};}
}
static class Fixture {
 static void Check(bool value,string text){if(!value)throw new Exception(text);}
 static ItemValue Copy(ItemValue v){if(!RebirthNativeItemCodec.TryDecode(RebirthNativeItemCodec.Encode(v),out var result))throw new Exception();return result;}
 static void Main(){var op=Guid.NewGuid();var before=new ItemValue{UseTimes=10,Stats=new[]{new ItemValue.Stat{type=PassiveEffects.DegradationMax,value=100},new ItemValue.Stat{type=PassiveEffects.Damage,value=7}}};before.Metadata["Other"]="keep";before.Metadata["DurabilityModifier"]=0.8f;before.SetMetadata("RebirthHeatOriginalV1",op.ToString("N"));var after=Copy(before);after.Stats[0].value=115;after.Metadata["RebirthProvVersion"]="2";after.Metadata["RebirthProvBonus"]="background_bonus.heat_treatment";after.Metadata["RebirthProvBatch"]="metal-original:"+op.ToString("N");after.SetMetadata("RebirthHeatAppliedV1",op.ToString("N"));var r=new RebirthMetalHeatTreatmentCorrection.Receipt{Operation=op,Before=RebirthNativeItemCodec.Encode(before),After=RebirthNativeItemCodec.Encode(after)};
  var current=Copy(before);current.UseTimes=25;string original=RebirthNativeItemCodec.Encode(current);
  Check(RebirthMetalHeatTreatmentCorrection.TryMerge(current,r,out var merged),"native correction refused");Check(current.MaxUseTimes==80&&merged.MaxUseTimes==92,"marker-only durability result");Check(merged.UseTimes==25&&merged.Mods==current.Mods&&merged.Texture==current.Texture&&(string)merged.Metadata["Other"]=="keep"&&(float)merged.Metadata["DurabilityModifier"]==0.8f&&merged.Stats[1].value==7,"unrelated native state changed");Check(RebirthNativeItemCodec.Encode(current)==original,"current native value mutated before publication");
  merged.UseTimes=30;Check(RebirthMetalHeatTreatmentCorrection.TryMerge(merged,r,out var repeated)&&repeated.MaxUseTimes==92&&repeated.UseTimes==30,"retry compounded durability or repaired wear");
  var changed=Copy(before);changed.Mods="new mod";Check(!RebirthMetalHeatTreatmentCorrection.TryMerge(changed,r,out _),"delayed correction overwrote unrelated mod change");
  var other=Copy(before);other.SetMetadata("RebirthHeatOriginalV1",Guid.NewGuid().ToString("N"));Check(!RebirthMetalHeatTreatmentCorrection.TryMerge(other,r,out _),"sibling item substituted");
  Check(RebirthMetalHeatTreatmentCorrection.Select(new[]{new ItemStack(before,1)},new ItemStack[0],r,false),"original one item not selected");Check(!RebirthMetalHeatTreatmentCorrection.Select(new[]{new ItemStack(before,1)},new[]{new ItemStack(Copy(before),1)},r,false),"ambiguous siblings selected");
  Check(!RebirthMetalHeatTreatmentCorrection.OriginalAdmittedOutput(false,before,before,new[]{new ItemStack(before,1)},op),"failed original output authorized heat");
  Check(!RebirthMetalHeatTreatmentCorrection.OriginalAdmittedOutput(true,before,before,new ItemStack[0],op),"full destination without original output authorized heat");
  Check(!RebirthMetalHeatTreatmentCorrection.OriginalAdmittedOutput(true,before,Copy(before),new[]{new ItemStack(before,1)},op),"different output invocation substituted");
  Check(RebirthMetalHeatTreatmentCorrection.OriginalAdmittedOutput(true,before,before,new[]{new ItemStack(Copy(before),1)},op),"successful original output not admitted");
  var owner=new EntityPlayer();RebirthCamperEnergySavedOwner.Ready=true;RebirthCamperEnergySavedOwner.Actor=new RebirthCamperEnergyActorImage{Bag=new[]{new ItemStack(before,1)},Belt=new ItemStack[0]};
  Check(!RebirthMetalHeatTreatmentCorrection.Prove(owner,r,false),"file without authenticated original upload admitted");
  RebirthMetalHeatTreatmentCorrection.UploadForFixture(owner,r,new[]{new ItemStack(before,1)},new World());Check(!RebirthMetalHeatTreatmentCorrection.Prove(owner,r,false),"old-world upload admitted");
  RebirthMetalHeatTreatmentCorrection.UploadForFixture(owner,r,new[]{new ItemStack(before,1)});Check(RebirthMetalHeatTreatmentCorrection.Prove(owner,r,false),"matching original upload and final file rejected");
  RebirthCamperEnergySavedOwner.Ready=false;Check(!RebirthMetalHeatTreatmentCorrection.Prove(owner,r,false),"native upload without final file publication admitted");RebirthCamperEnergySavedOwner.Ready=true;
  RebirthCamperEnergySavedOwner.Actor.Bag=new[]{new ItemStack(other,1)};Check(!RebirthMetalHeatTreatmentCorrection.Prove(owner,r,false),"different final-file item admitted");
  RebirthMetalHeatTreatmentCorrection.UploadForFixture(owner,r,new[]{new ItemStack(after,1)});RebirthCamperEnergySavedOwner.Actor.Bag=new[]{new ItemStack(after,1)};Check(RebirthMetalHeatTreatmentCorrection.Prove(owner,r,true),"matching native after publication rejected");
  var forged=Copy(after);forged.Stats[1].value=99;r.After=RebirthNativeItemCodec.Encode(forged);Check(!RebirthMetalHeatTreatmentCorrection.TryMerge(before,r,out _),"unowned native stat accepted");
  var duplicate=Copy(before);duplicate.Stats=new[]{duplicate.Stats[0],duplicate.Stats[0]};Check(!RebirthMetalHeatTreatmentCorrection.SupportedStat(duplicate),"ambiguous degradation rows accepted");
  Console.WriteLine("PASS actual native heat merge/selection: native max-use92 vs80, unrelated identity/wear preserved, once-only retry, ambiguity and unowned-field refusal; effect lookup/codec doubled");
 }
}
