using System;using System.Reflection;using HarmonyLib;
class Program{
 static int count;static void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
 static void Main(){
 var api=new RebirthPurgeSupplyModApi();api.InitMod(null);Check(Harmony.Calls==0,"disabled release installs no native hooks");
 RebirthPurgeReleasePolicy.Enabled=true;AccessTools.Missing="Tick";try{api.InitMod(null);throw new Exception("expected missing ABI");}catch(MissingMethodException){}
 Check(Harmony.Calls==0,"last missing target refuses whole installation before mutation");
 AccessTools.Missing=null;Harmony.FailAt=5;try{api.InitMod(null);throw new Exception("expected patch failure");}catch(InvalidOperationException){}
 Check(Harmony.Active==0&&Harmony.Rollbacks==1,"partial native installation rolls back its dedicated owner");
 Check(Harmony.LastId=="rebirth.purge.supply.persistence","rollback uses only supply installer identity");
 Harmony.FailAt=0;Harmony.Calls=0;api.InitMod(null);Check(Harmony.Active==11,"retry installs all eleven qualified hooks");
 api.InitMod(null);Check(Harmony.Calls==11,"successful initialization is idempotent");
 Console.WriteLine("RESULT "+count+" PASS; production installer; native ABI reflection and Harmony installation explicitly doubled. Root build separately qualifies game signatures; no live installation claim.");
 }}
static class RebirthPurgeReleasePolicy{public static bool Enabled;}
public class Mod{}interface IModApi{void InitMod(Mod mod);}
class PooledBinaryWriter{}class PooledBinaryReader{}enum StreamModeWrite{}enum StreamModeRead{}class DamageSource{}class DamageResponse{}class Entity{}class GameManager{}
class EntitySupplyCrate{public string GetActivationText()=>"";public void Write(PooledBinaryWriter x,StreamModeWrite y){}public void Read(byte x,PooledBinaryReader y,StreamModeRead z){}}
class EntityAlive{public string GetLootList()=>"";public void OnLockRequestServer(int x,PooledBinaryReader y,ushort z){}public void DamageEntity(DamageSource x,int y,bool z,float w){}public void ProcessDamageResponseLocal(DamageResponse x){}}
class NetPackageBag{public void ProcessPackage(World x,GameManager y){}}class NetPackageDamageEntity{public void ProcessPackage(World x,GameManager y){}}
class World{public void SpawnEntityInWorld(Entity x){}}class AIDirectorAirDropComponent{public void SpawnAirDrop(){}public void Tick(double x){}public void SpawnSupplyCrate(UnityEngine.Vector3 pos,ChunkManager.ChunkObserver observer){}}
static class RebirthPurgeSupplyCrateSerialization{public static void AfterWrite(){}public static void AfterRead(){}}
static class RebirthPurgeSupplyAccess{public static void AfterActivationText(){}public static void BeforeLock(){}public static void BeforeBag(){}public static void BeforeDamage(){}public static void BeforeDamageResponse(){}public static void BeforeDamagePacket(){}}
static class RebirthPurgeSupplySpawnScope{public static void BeforeSpawn(){}}
static class RebirthPurgeSupplyCoordinator{public static void BeforeScheduled(){}public static void BeforeDirectorTick(){}public static void BeforeFlightPaths(){}public static void BeforeCrate(){}public static void AfterCrate(){}}
namespace UnityEngine.Scripting{class PreserveAttribute:Attribute{}}
namespace HarmonyLib{
 static class AccessTools{public static string Missing;public static MethodInfo Method(Type type,string name,Type[] args=null)=>name==Missing?null:args==null?type.GetMethod(name):type.GetMethod(name,args);}
 class HarmonyMethod{public HarmonyMethod(MethodInfo method){}}
 class Harmony{public static int Calls,Active,Rollbacks,FailAt;public static string LastId;public Harmony(string id){LastId=id;}public void Patch(MethodInfo target,HarmonyMethod prefix=null,HarmonyMethod postfix=null,HarmonyMethod finalizer=null){Calls++;if(Calls==FailAt)throw new InvalidOperationException();Active++;}public void UnpatchSelf(){Active=0;Rollbacks++;}}
}
class AIAirDrop{public void CreateFlightPaths(){}}class ChunkManager{public class ChunkObserver{}}namespace UnityEngine{public struct Vector3{}}static class RebirthPurgeSupplyLoot{public static void BeforeLootList(){}}
