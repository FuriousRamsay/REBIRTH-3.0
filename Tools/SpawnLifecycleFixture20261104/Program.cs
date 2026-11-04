using System;using System.Reflection;
class HarmonyPriority:Attribute {public HarmonyPriority(int p){}}
static class Priority {public const int First=0;}
enum RespawnType {NewGame,EnterMultiplayer,LoadedGame,Died,JoinMultiplayer}
class Xui {public bool IsReady=true;}
class UI {public Xui xui=new Xui();}
class EntityPlayerLocal {public bool spawned;public int entityId=171;public string position="retained-position";public UI PlayerUI=new UI();public bool IsSpawned()=>spawned;}
class PlayerMoveController {public EntityPlayerLocal entityPlayerLocal=new EntityPlayerLocal();public RespawnType respawnReason=RespawnType.LoadedGame;public float respawnTime;}
static class Time {public static float realtimeSinceStartup;}
static class Log {public static int Lines;public static void Out(string s){Lines++;}}
static class RebirthConsoleInputGuardRuntime {public static bool Blocking;public static int Clears;public static bool BlocksGameplayInput()=>Blocking;public static void ClearGameplayMovement(EntityPlayerLocal p){Clears++;}}
static class RebirthPurgeTruckStart {public static bool Enabled=true,Active,Ready;public static int ReadyCalls,PresentationCalls;public static bool StartHandoffActive(EntityPlayerLocal p)=>p!=null&&Active;public static bool BeforeWorldReady(PlayerMoveController c){ReadyCalls++;return Ready;}public static void PresentStart(EntityPlayerLocal p){PresentationCalls++;}}
static class ConsoleGuard {
    public static bool Prefix(PlayerMoveController __instance)
    {
        // Native Update owns updateRespawn and its readiness/timeouts too.
        // Console input exclusivity must never suspend that loading lifecycle.
        var player=__instance!=null?__instance.entityPlayerLocal:null;
        if(player==null||!player.IsSpawned())return true;
        if (!RebirthConsoleInputGuardRuntime.BlocksGameplayInput())
            return true;

        RebirthConsoleInputGuardRuntime.ClearGameplayMovement(
            __instance != null ? __instance.entityPlayerLocal : null);
        // The console's UGUI update, text field, history, submit and close handlers run elsewhere.
        // Do not reset global keyboard state or stop the window manager to suppress gameplay.
        return false;
    }
}
internal static class RebirthPurgeInitialWorldReadiness
{
    private static void Postfix(PlayerMoveController __instance,ref bool __result)
    {if(__result&&(__instance.respawnReason==RespawnType.NewGame||__instance.respawnReason==RespawnType.EnterMultiplayer))__result=RebirthPurgeTruckStart.BeforeWorldReady(__instance);}
}
internal static class RebirthPurgeNativeSpawnTrace
{
    private static float next;
    private static void Postfix(PlayerMoveController __instance){var player=__instance.entityPlayerLocal;if(RebirthPurgeTruckStart.StartHandoffActive(player))RebirthPurgeTruckStart.PresentStart(player);}
    [HarmonyPriority(Priority.First)]
    private static void Prefix(PlayerMoveController __instance)
    {
        var player=__instance.entityPlayerLocal;
        if(!RebirthPurgeTruckStart.Enabled||player==null||!RebirthPurgeTruckStart.StartHandoffActive(player)||player.IsSpawned()||Time.realtimeSinceStartup<next)return;
        next=Time.realtimeSinceStartup+5;
        Log.Out("[PurgeTruckStart] step=wait.native-respawn player="+player.entityId+" reason="+__instance.respawnReason+" timer="+__instance.respawnTime+" position="+player.position+" uiReady="+player.PlayerUI.xui.IsReady);
    }
}
class Program {
 static int checks;
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
 static void Call(Type type,string name,params object[] values)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,values);
 static bool WorldReady(PlayerMoveController c,bool native){object[] values={c,native};Call(typeof(RebirthPurgeInitialWorldReadiness),"Postfix",values);return (bool)values[1];}
 static void Main(){
 var c=new PlayerMoveController();RebirthConsoleInputGuardRuntime.Blocking=true;
 Check(ConsoleGuard.Prefix(c),"open console cannot block native respawn before Spawned");
 int advances=0;for(int i=0;i<120;i++)if(ConsoleGuard.Prefix(c))advances++;
 Check(advances==120,"native loading lifecycle continues across console-open frames");
 c.entityPlayerLocal.spawned=true;Check(!ConsoleGuard.Prefix(c),"console still blocks spawned gameplay input");
 Check(RebirthConsoleInputGuardRuntime.Clears==1,"spawned console input clears native movement");
 RebirthConsoleInputGuardRuntime.Blocking=false;Check(ConsoleGuard.Prefix(c),"closed console allows spawned native update");
 RebirthConsoleInputGuardRuntime.Blocking=true;c.entityPlayerLocal.spawned=false;Check(ConsoleGuard.Prefix(c),"close-frame input block cannot stall loading");
 Check(ConsoleGuard.Prefix(null),"missing controller does not invent loading hold");
 c.entityPlayerLocal=null;Check(ConsoleGuard.Prefix(c),"missing player does not invent loading hold");c.entityPlayerLocal=new EntityPlayerLocal();
 RebirthPurgeTruckStart.Ready=false;
 Check(WorldReady(c,true)&&RebirthPurgeTruckStart.ReadyCalls==0,"LoadedGame bypasses truck readiness entirely");
 Check(!WorldReady(c,false)&&RebirthPurgeTruckStart.ReadyCalls==0,"native unready load remains native unready");
 foreach(var reason in new[]{RespawnType.Died,RespawnType.JoinMultiplayer}){c.respawnReason=reason;Check(WorldReady(c,true),reason+" bypasses first-truck readiness");}
 c.respawnReason=RespawnType.NewGame;Check(!WorldReady(c,true)&&RebirthPurgeTruckStart.ReadyCalls==1,"fresh spawn retains actual initial truck readiness");
 c.respawnReason=RespawnType.EnterMultiplayer;RebirthPurgeTruckStart.Ready=true;Check(WorldReady(c,true)&&RebirthPurgeTruckStart.ReadyCalls==2,"fresh remote entry preserves starter handoff");
 c.respawnReason=RespawnType.LoadedGame;RebirthPurgeTruckStart.Active=false;Time.realtimeSinceStartup=10;
 Call(typeof(RebirthPurgeNativeSpawnTrace),"Prefix",c);Call(typeof(RebirthPurgeNativeSpawnTrace),"Postfix",c);
 Check(Log.Lines==0&&RebirthPurgeTruckStart.PresentationCalls==0,"ordinary reload neither traces nor presents truck startup");
 RebirthPurgeTruckStart.Active=true;Call(typeof(RebirthPurgeNativeSpawnTrace),"Prefix",c);Call(typeof(RebirthPurgeNativeSpawnTrace),"Postfix",c);
 Check(Log.Lines==1&&RebirthPurgeTruckStart.PresentationCalls==1,"active unfinished starter handoff retains presentation");
 RebirthPurgeTruckStart.Active=false;Time.realtimeSinceStartup=20;Call(typeof(RebirthPurgeNativeSpawnTrace),"Prefix",c);Call(typeof(RebirthPurgeNativeSpawnTrace),"Postfix",c);
 Check(Log.Lines==1&&RebirthPurgeTruckStart.PresentationCalls==1,"completed starter stops trace and presentation");
 Console.WriteLine(checks+" PASS; actual production guard/readiness/trace methods, native adapters doubled");
 }
}
