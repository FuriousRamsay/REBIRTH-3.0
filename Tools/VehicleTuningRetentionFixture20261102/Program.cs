using System;
using System.Reflection;
using System.Runtime.CompilerServices;
public interface IModApi {void InitMod(Mod mod);} public class Mod {}
static class RebirthVehicleDriveAudit {}
static class RebirthHarmonyBootstrap {public static readonly System.Collections.Generic.HashSet<Type> Registered=new();public static bool PatchClassOnce(HarmonyLib.Harmony h,Type t)=>Registered.Add(t);}
namespace HarmonyLib { class Harmony {public Harmony(string id){}}  class HarmonyPatch:Attribute { public HarmonyPatch(Type t,string n){} } class HarmonyPrefix:Attribute{} class HarmonyPriority:Attribute {public int priority;public HarmonyPriority(int p){priority=p;} } static class Priority{public const int First=800;} }
class EntityVehicle {public bool isEntityRemote; public float Torque=5; public void PhysicsFixedUpdate(){} }
partial class RebirthVehicleTuningSession {
 public bool Valid=true; public EntityVehicle Entity; public float Chosen=17; public int Writes;
 public bool Tuned { get=>Settings[0].Tuned; set=>Settings[0].Tuned=value; }
 public void Prepare(){Settings.Add(new Setting{Tuned=true,Value="17",Read=()=>Entity.Torque.ToString(System.Globalization.CultureInfo.InvariantCulture),Write=text=>{Entity.Torque=float.Parse(text,System.Globalization.CultureInfo.InvariantCulture);Writes++;}});Settings.Add(new Setting{Tuned=true,Value="proposal",Read=null,Write=null});Settings.Add(new Setting{Tuned=false,Value="untuned",Read=()=>throw new Exception("untuned row read"),Write=text=>throw new Exception("untuned row write")});}
}
static class RebirthVehicleTuningUi {internal static readonly ConditionalWeakTable<EntityVehicle,RebirthVehicleTuningSession> Sessions=new();public static RebirthVehicleTuningSession Session;}
class Program {
 static int checks;static void Check(bool ok,string m){checks++;if(!ok)throw new Exception(m);}
 static readonly MethodInfo Hook=typeof(RebirthVehicleTuningPhysicsBaseline).GetMethod("Prefix",BindingFlags.Static|BindingFlags.NonPublic);
 static void Call(EntityVehicle v)=>Hook.Invoke(null,new object[]{v});
 static void Main(){
  new RebirthVehicleTuningRuntimeModApi().InitMod(new Mod());
  Check(RebirthHarmonyBootstrap.Registered.Contains(typeof(RebirthVehicleTuningPhysicsBaseline)),"actual ModApi installs retention hook");
  Check(RebirthHarmonyBootstrap.Registered.Contains(typeof(RebirthVehicleDriveAudit)),"actual ModApi installs bounded capture hook");
  Check(Hook.GetCustomAttribute<HarmonyLib.HarmonyPriority>().priority>400,"tuner must precede default restoration prefix");
  var v=new EntityVehicle();var s=new RebirthVehicleTuningSession{Entity=v};s.Prepare();RebirthVehicleTuningUi.Sessions.Add(v,s);RebirthVehicleTuningUi.Session=null;
  for(int i=0;i<120;i++){v.Torque=5;Call(v);Check(v.Torque==17,"closed-window baseline retention");float original=v.Torque;v.Torque*=2;Check(v.Torque==34,"restoration multiplier preserved");v.Torque=original;Check(v.Torque==17,"native scope restores chosen baseline");}
  Check(s.Writes==120,"one replacement for each actual drift");Call(v);Check(s.Writes==120,"unchanged field does not rewrite");
  s.Tuned=false;v.Torque=5;Call(v);Check(v.Torque==5,"reset untuned field remains native");s.Tuned=true;s.Valid=false;Call(v);Check(v.Torque==5,"invalid captured vehicle session is rejected");s.Valid=true;v.isEntityRemote=true;Call(v);Check(v.Torque==5,"remote entity excluded");Call(null);Call(new EntityVehicle());
  Console.WriteLine($"PASS {checks} production runtime-hook checks with test-only stand-ins. Includes closed-window drift over120ticks and restoration scaling. Not Unity driving acceptance.");
 }
}