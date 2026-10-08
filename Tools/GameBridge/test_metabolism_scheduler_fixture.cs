using System;
using System.Collections.Generic;
class EntityPlayer {public int Id;public int entityId=>Id;}
class PlayerList {public List<EntityPlayer> list=new List<EntityPlayer>();}
class World {public PlayerList Players=new PlayerList();}
class GameManager {public static GameManager Instance=new GameManager();public World World;}
class Time {public static float realtimeSinceStartup;}
class Mathf {public static float Max(float a,float b){return Math.Max(a,b);}}
class RebirthMetabolismConfig {public static float UpdateRealSeconds=1f;}
class RebirthSurvivorMode {public static bool Enabled=true;public static bool IsEnabledForCurrentWorld(){return Enabled;}}
class RebirthMetabolismService {public static bool IsServerAuthority=true;public static List<int> Calls=new List<int>();public static int FailId;public static void Tick(EntityPlayer p){Calls.Add(p.Id);if(p.Id==FailId)throw new Exception("player failure");}}
class RebirthLogSettings {public static bool HarmonyPatchLoggingEnabled=false;}
class Log {public static void Warning(string s){}}
class ModEvents {public struct SGameUpdateData {}}
class Check {static float nextWorldTick;
// METHODS
static void Assert(bool p,string label){if(!p)throw new Exception(label);}
static void Main(){var data=new ModEvents.SGameUpdateData();var world=new World();GameManager.Instance.World=world;world.Players.list.Add(new EntityPlayer{Id=1});world.Players.list.Add(new EntityPlayer{Id=2});world.Players.list.Add(new EntityPlayer{Id=3});
OnGameUpdate(ref data);Assert(RebirthMetabolismService.Calls.Count==3&&RebirthMetabolismService.Calls[1]==2&&RebirthMetabolismService.Calls[2]==3,"all host and remote entities scheduled");
Time.realtimeSinceStartup=.5f;OnGameUpdate(ref data);Assert(RebirthMetabolismService.Calls.Count==3,"cadence prevents repeated scan");
Time.realtimeSinceStartup=1f;OnGameUpdate(ref data);Assert(RebirthMetabolismService.Calls.Count==6,"next cadence includes all players");
Time.realtimeSinceStartup=2f;RebirthMetabolismService.IsServerAuthority=false;OnGameUpdate(ref data);Assert(RebirthMetabolismService.Calls.Count==6,"client cannot simulate server metabolism");
RebirthMetabolismService.IsServerAuthority=true;RebirthSurvivorMode.Enabled=false;OnGameUpdate(ref data);Assert(RebirthMetabolismService.Calls.Count==6,"base mode excluded");
RebirthSurvivorMode.Enabled=true;GameManager.Instance.World=null;OnGameUpdate(ref data);Assert(RebirthMetabolismService.Calls.Count==6,"world teardown safe");
GameManager.Instance.World=world;Time.realtimeSinceStartup=3;RebirthMetabolismService.FailId=2;world.Players.list.Insert(0,null);OnGameUpdate(ref data);Assert(RebirthMetabolismService.Calls.Count==9&&RebirthMetabolismService.Calls[8]==3,"null and failing player do not block later players");Console.WriteLine("PASS actual metabolism scheduler: all player IDs, cadence, client authority, mode, teardown and per-player failure isolation; Tick/native adapters doubled, actual reserve loss not tested");}}