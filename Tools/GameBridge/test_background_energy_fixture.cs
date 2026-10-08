using System;
class TagGroup { public class Global {} }
struct FastTags<T> { public int Bits; public FastTags(int bits){Bits=bits;} public bool Test_AnySet(FastTags<T> other){return (Bits&other.Bits)!=0;} }
class Stat { public float Value; }
class Stats { public Stat Stamina=new Stat(); }
class EntityPlayer { public FastTags<TagGroup.Global> CurrentMovementTag; public Stats Stats=new Stats(); }
class RebirthMetabolismState { public float LastStamina=-1, SmoothedActivity; }
static class Mathf { public static float Max(float a,float b){return Math.Max(a,b);} public static float Clamp(float v,float min,float max){return Math.Max(min,Math.Min(max,v));} public static float Lerp(float a,float b,float t){return a+(b-a)*t;} }
static class RebirthMetabolismConfig {
 public static float EnergyLightUsePerRealMinute=1,EnergyModerateUsePerRealMinute=2,EnergyHighUsePerRealMinute=3,EnergyExtremeUsePerRealMinute=4,EnergyPerStaminaSpent=.05f;
}
static class RebirthEfficientMovementService { public static float Apply(EntityPlayer p,float e){return e;} }
class Check {
 static FastTags<TagGroup.Global> TagSwimmingRun=new FastTags<TagGroup.Global>(1),TagRunning=new FastTags<TagGroup.Global>(2),TagSwimming=new FastTags<TagGroup.Global>(4),TagClimbing=new FastTags<TagGroup.Global>(8),TagJumping=new FastTags<TagGroup.Global>(16),TagWalking=new FastTags<TagGroup.Global>(32);
 // ACTIVITY
 static void Equal(float actual,float expected,string why){if(Math.Abs(actual-expected)>.0001)throw new Exception(why+": "+actual+" != "+expected);}
 static float Energy(float last,float now,float minutes){var p=new EntityPlayer();p.Stats.Stamina.Value=now;var s=new RebirthMetabolismState{LastStamina=last};float h,e;ResolveActivity(p,s,minutes,out h,out e);return e;}
 static void Main(){
 // Both costs saturate the same activity tier: only the direct net-spend component changes.
 Equal(Energy(100,90,1),4.5f,"ordinary cutting");
 Equal(Energy(100,92.5f,1),4.375f,"Logger net refund");
 Equal(Energy(100,90,1)-Energy(100,92.5f,1),.125f,"25 percent direct component reduction, not total Energy");
 Equal(Energy(100,90,.5f)-Energy(100,92.5f,.5f),.25f,"per-minute conversion");
 Equal(Energy(100,100,1),0,"recovery hides both costs from net sampling");
 Equal(Energy(-1,90,1),0,"first observation has no expenditure baseline");
 Equal(Energy(100,90,0),4,"zero interval excludes division");
 Console.WriteLine("PASS actual ResolveActivity: Logger net-spend Energy coupling, rate conversion, recovery/first-sample limitations, zero interval");
 }
}