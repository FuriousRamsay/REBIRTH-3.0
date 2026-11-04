// Controlled read-only diagnostic inputs. No game scene, persistent storage or award implementation.
using Newtonsoft.Json.Linq;
public class World{public bool Remote;public bool IsRemote()=>Remote;}
public class ItemClass{public string GetItemName()=>"ItemsWeaponsCleaver001_FR";}
public class ItemValue{public ItemClass ItemClass=new();public int type=12,Meta=3;public ushort Quality=2,Seed=42;public byte SelectedAmmoTypeIndex=0;public float UseTimes=17.5f;}
public class Inventory{public ItemValue holdingItemItemValue=new();}
public class EntityPlayer{public World world=new();public Inventory inventory=new();public int entityId=7;}
public class Owner{public string StorageKey="hash";}
public class Origin{public string CreationId="creation";}
public class RebirthSkillRuntimeState{public float Value=10,Progress=.0035f;}
public class Progression{public Dictionary<string,RebirthSkillRuntimeState> Skills=new(){["skill.knives"]=new()};}
public class Record{public Origin Origin=new();public bool IsComplete=true;public Progression Progression=new();}
public static class RebirthSkillAwardService{public static bool Eligible=true;public static Record Record=new();public static bool TryGetEligible(EntityPlayer p,out Owner o,out Record r){o=new();r=Record;return Eligible;}}
public static class RebirthProgressionRuntimeConfig{public static string ClassifyCombat(ItemValue i)=>"skill.knives";}
public static class RebirthSurvivorMode{public static bool IsEnabledForCurrentWorld()=>true;}
public static class RebirthCharacterCreationHoldService{public static bool Held;public static bool IsHeld(EntityPlayer p)=>Held;}
public static class RebirthWeaponSustainedDpsService{
public class Profile{public bool Valid=true;public string Detail="melee-live",ItemName="ItemsWeaponsCleaver001_FR",SkillId="skill.knives";public int Signature=123,MagazineSize=1;public float DamagePerAttack=13.2f,NormalAttackDps=26.4f,PowerAttackDps=36,SustainedDps=36,AttacksPerMinute=60,RoundsPerMinute=0,ReloadSeconds=0;}
public static bool Resolved=true,Throw;public static Profile Current=new();public static bool TryGetCombatProfile(EntityPlayer p,ItemValue i,out Profile profile){if(Throw)throw new InvalidOperationException("test-unavailable");profile=Current;return Resolved;}
public static JObject CaptureAuditState(EntityPlayer p)=>new(){["inFlight"]=2,["queuedSkills"]=1,["settled"]=3};
}
public class SdtdConsole{public static SdtdConsole Instance=new();public string Last;public void Output(string s)=>Last=s;}
static class Program{
static int n;static void Check(bool b,string message){if(!b)throw new Exception(message);n++;}
static JObject Run(EntityPlayer p){Subject.OutputCombatAudit(p);return JObject.Parse(SdtdConsole.Instance.Last.Substring("[REBIRTH Skill Audit] ".Length));}
static void Main(){var p=new EntityPlayer();var r=Run(p);Check((bool)r["eligible"]&&((string)r["ownerKey"])=="hash","eligible scope");Check((float)r["skillProgress"]==.0035f,"small fractional gain visible");Check((int)r["meta"]==3&&(float)r["useTimes"]==17.5f,"copied item metadata visible");Check((float)r["profile"]["sustainedDps"]==36&&(int)r["awards"]["inFlight"]==2,"profile and pending award visible");
RebirthSkillAwardService.Eligible=false;RebirthCharacterCreationHoldService.Held=true;r=Run(p);Check(!(bool)r["eligible"]&&(bool)r["creationHeld"],"readiness refusal still reports instead of failing snapshot admission");
RebirthSkillAwardService.Record=null;p.inventory=null;RebirthWeaponSustainedDpsService.Resolved=false;RebirthWeaponSustainedDpsService.Current=null;r=Run(p);Check(!(bool)r["complete"]&&r["item"].Type==JTokenType.Null&&!(bool)r["profileResolved"],"missing record and item produce actionable unavailable fields");
p.world=null;r=Run(p);Check(!(bool)r["remoteWorld"],"missing world safe");
RebirthWeaponSustainedDpsService.Throw=true;Subject.OutputCombatAudit(p);Check(SdtdConsole.Instance.Last.Contains("unavailable: InvalidOperationException: test-unavailable"),"native profile exception is visible and contained");
Console.WriteLine(n+" exact audit-method checks PASS (explicit diagnostic input/output adapters)");}}
