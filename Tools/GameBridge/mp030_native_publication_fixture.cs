using System;using System.Collections.Generic;
// AUTHORED_NOT_RUN. Native entity, health/fuel setters, metadata container,
// registration lookup, assembly codec and sync callbacks are explicit adapters.
internal sealed class RebirthVehicleAssembly {
 public Guid AssemblyId=Guid.NewGuid();public string ContentId="vehicle",Data="before";public object Performance;
 public RebirthVehicleAssembly DeepClone()=>new RebirthVehicleAssembly{AssemblyId=AssemblyId,ContentId=ContentId,Data=Data};
}
internal static class RebirthVehicleAssemblySerializer {
 public static string ToBase64(RebirthVehicleAssembly assembly)=>assembly==null?"":assembly.AssemblyId.ToString("N")+"|"+assembly.ContentId+"|"+assembly.Data;
 public static RebirthVehicleAssembly FromBase64(string text){var parts=text.Split('|');return new RebirthVehicleAssembly{AssemblyId=Guid.ParseExact(parts[0],"N"),ContentId=parts[1],Data=parts[2]};}
}
internal static class RebirthVehiclePerformanceCompiler {public static object Compile(RebirthVehicleAssembly assembly)=>new object();}
internal sealed class ItemValue {
 internal string Payload;internal bool Suppress;internal Action Callback;
 public void SetMetadata(string key,string value){if(!Suppress)Payload=value;Callback?.Invoke();}
 public bool TryGetMetadata(string key,out string value){value=Payload;return value!=null;}
}
internal sealed class Vehicle {
 internal ItemValue Native=new ItemValue();internal bool Detached,SuppressFuel;internal float Fuel=3;
 public ItemValue GetUpdatedItemValue()=>Detached?new ItemValue():Native;
 public float GetFuelLevel()=>Fuel;public void SetFuelLevel(float value){if(!SuppressFuel)Fuel=value;}
}
internal sealed class EntityVehicle {
 public int entityId=5;public World world;public Vehicle vehicle=new Vehicle();internal int health=10;internal bool SuppressHealth;internal Action Sync;
 public int Health {get=>health;set{if(!SuppressHealth)health=value;}}
 public void SendSyncData(int flags){Sync?.Invoke();}
}
internal struct Vector3i {public int x,y,z;}
internal sealed class TileEntityRepairableVehicleRebirth {
 public RebirthVehicleAssembly Assembly;public Action Modified;public Vector3i ToWorldPos()=>new Vector3i();public void SetModified(){Modified?.Invoke();}
}
internal sealed class World {
 public bool Remote;public EntityVehicle Entity;public TileEntityRepairableVehicleRebirth Tile;
 public bool IsRemote()=>Remote;public EntityVehicle GetEntity(int id)=>Entity;public TileEntityRepairableVehicleRebirth GetTileEntity(Vector3i position)=>Tile;
}
internal sealed class GameManager {public static GameManager Instance=new GameManager();public World World;}
internal sealed class ConnectionManager {public bool IsServer=true;}
internal static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
internal static class ThreadManager {internal static bool Main=true;public static bool IsMainThread()=>Main;}
internal static class Log {public static void Error(string text){}}
internal static class RebirthVehicleEntityPersistence {
 private const string AssemblyMetadataKey="RebirthVehicleAssembly";
 // ACTUAL_NATIVE_METADATA
}
internal static class RebirthVehicleAssemblyIndex {
 private static readonly object Sync=new object();
 private static readonly Dictionary<int,RebirthVehicleAssembly> Entities=new Dictionary<int,RebirthVehicleAssembly>();
 private static readonly Dictionary<Vector3i,TileEntityRepairableVehicleRebirth> Blocks=new Dictionary<Vector3i,TileEntityRepairableVehicleRebirth>();
 private static readonly Dictionary<Guid,string> Carriers=new Dictionary<Guid,string>();
 // ACTUAL_INDEX
}
internal static class Rollback {
 // ACTUAL_ROLLBACK
 internal static bool Entity(EntityVehicle entity,RebirthVehicleAssembly before,int? health,float? fuel)=>TryRestoreEntityMutation(entity,before,health,fuel,out _);
 internal static bool Block(TileEntityRepairableVehicleRebirth tile,RebirthVehicleAssembly before)=>TryRestoreBlockAssembly(tile,before,out _);
}
internal static class Program {
 private static EntityVehicle entity;private static TileEntityRepairableVehicleRebirth tile;private static RebirthVehicleAssembly before;
 private static void Need(bool value,string message){if(!value)throw new Exception(message);}
 private static void Fresh(){ThreadManager.Main=true;SingletonMonoBehaviour<ConnectionManager>.Instance=new ConnectionManager();before=new RebirthVehicleAssembly();var world=new World();entity=new EntityVehicle{world=world};tile=new TileEntityRepairableVehicleRebirth{Assembly=new RebirthVehicleAssembly()};world.Entity=entity;world.Tile=tile;GameManager.Instance.World=world;}
 public static void Main(){
  Fresh();Need(RebirthVehicleEntityPersistence.WriteAssemblyToVehicleItem(entity,before)&&entity.vehicle.Native.Payload==RebirthVehicleAssemblySerializer.ToBase64(before),"actual metadata did not reach owning native item");
  Fresh();entity.vehicle.Native.Suppress=true;Need(!RebirthVehicleEntityPersistence.WriteAssemblyToVehicleItem(entity,before),"suppressed metadata setter claimed publication");
  Fresh();entity.vehicle.Detached=true;Need(!RebirthVehicleEntityPersistence.WriteAssemblyToVehicleItem(entity,before),"detached metadata argument claimed native owning publication");
  Fresh();entity.vehicle.Native.Callback=()=>entity.vehicle.Native.Payload="changed";Need(!RebirthVehicleEntityPersistence.WriteAssemblyToVehicleItem(entity,before),"metadata listener replacement claimed publication");
  Fresh();Need(Rollback.Entity(entity,before,90,7)&&entity.Health==90&&entity.vehicle.Fuel==7,"exact entity rollback refused");
  Fresh();entity.SuppressHealth=true;Need(!Rollback.Entity(entity,before,90,null),"suppressed health rollback was reported restored");
  Fresh();entity.vehicle.SuppressFuel=true;Need(!Rollback.Entity(entity,before,null,7),"suppressed fuel rollback was reported restored");
  Fresh();entity.Sync=()=>entity.vehicle.Native.Payload="changed";Need(!Rollback.Entity(entity,before,null,null),"sync callback metadata replacement was reported restored");
  Fresh();entity.Sync=()=>entity.world.Entity=new EntityVehicle();Need(!Rollback.Entity(entity,before,null,null),"replacement native entity inherited rollback completion");
  Fresh();ThreadManager.Main=false;Need(!Rollback.Entity(entity,before,90,7)&&entity.Health==10&&entity.vehicle.Fuel==3,"worker rollback entered native setters");
  Fresh();GameManager.Instance.World=new World();Need(!Rollback.Entity(entity,before,90,7)&&entity.Health==10,"foreign World rollback entered native setters");
  Fresh();Need(Rollback.Block(tile,before)&&RebirthVehicleAssemblySerializer.ToBase64(tile.Assembly)==RebirthVehicleAssemblySerializer.ToBase64(before),"exact block rollback refused");
  Fresh();tile.Modified=()=>tile.Assembly=new RebirthVehicleAssembly();Need(!Rollback.Block(tile,before),"changed block rollback was reported restored");
  Fresh();tile.Modified=()=>GameManager.Instance.World.Tile=new TileEntityRepairableVehicleRebirth();Need(!Rollback.Block(tile,before),"replacement tile inherited rollback completion");
  Fresh();SingletonMonoBehaviour<ConnectionManager>.Instance=null;Need(!Rollback.Block(tile,before)&&tile.Assembly.AssemblyId!=before.AssemblyId,"missing authority entered block rollback");
  Console.WriteLine("PASS actual MP030 metadata/rollback methods; native engine/PDS/world-save/remote endpoint NOT RUN");
 }
}
