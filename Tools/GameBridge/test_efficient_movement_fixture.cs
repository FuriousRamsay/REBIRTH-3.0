using System;
class TagGroup { public class Global {} }
struct FastTags<T> { public string Name;public static FastTags<T> Parse(string n){return new FastTags<T>{Name=n};}public bool Test_AnySet(FastTags<T> t){return Name==t.Name;} }
public class EntityPlayer { internal FastTags<TagGroup.Global> CurrentMovementTag; }
static class RebirthSurvivorMode { public static bool Enabled=true;public static bool IsEnabledForCurrentWorld(){return Enabled;} }
static class RebirthBackgroundBonusService {public static bool Owns=true;public static bool HasBonus(EntityPlayer p,string id){return Owns;} }
static class RebirthResourceSignatureService {public static float GetTuning(string id,string key,float fallback){return .85f;} }
static class RebirthMetabolismConfig {public static float EnergyExtremeUsePerRealMinute=4,EnergyHighUsePerRealMinute=3,EnergyModerateUsePerRealMinute=2;}
namespace UnityEngine {static class Mathf {public static float Min(float a,float b){return Math.Min(a,b);}public static float Clamp(float x,float a,float b){return Math.Max(a,Math.Min(b,x));}}}
// CODEC
class Check {
 static void Test(string tag,float input,float expected){var p=new EntityPlayer{CurrentMovementTag=FastTags<TagGroup.Global>.Parse(tag)};float result=RebirthEfficientMovementService.Apply(p,input);if(Math.Abs(result-expected)>.0001)throw new Exception(tag+" "+result);}
 static void Main(){Test("running",3,2.55f);Test("jumping",2,1.7f);Test("swimming",3,2.55f);Test("swimmingRun",4,3.4f);Test("walking",1,1);Test("climbing",3,3);Test("idle",4,4);Test("mining",4,4);Test("attack",4,4);Test("running",4,3.55f);Test("running",1,.85f);RebirthSurvivorMode.Enabled=false;Test("running",3,3);RebirthSurvivorMode.Enabled=true;RebirthBackgroundBonusService.Owns=false;Test("running",3,3);Console.WriteLine("PASS actual Efficient Movement helper: movement15%, nonmovement exclusions, mixed activity cap, mode and entitlement");}
}