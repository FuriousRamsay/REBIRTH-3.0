using System;
public class World {public bool Remote;public bool IsRemote(){return Remote;}}
public class EntityVehicle {public World world;}
public class ConnectionManager {public bool IsServer;}
public class SingletonMonoBehaviour<T> {public static T Instance;}
public class RebirthVehicleAssemblyIndex {public static int Removed;public static void UnregisterEntity(EntityVehicle e){Removed++;}}
public class Check {
// METHODS
public static void Main(){for(int w=0;w<3;w++)for(int c=0;c<3;c++){
 var e=new EntityVehicle{world=w==0?null:new World{Remote=w==2}};
 SingletonMonoBehaviour<ConnectionManager>.Instance=c==0?null:new ConnectionManager{IsServer=c==1};
 RebirthVehicleAssemblyIndex.Removed=0;AfterEntityUnload(e);
 bool expected=w==2||(w==0&&c==2);
 if(RebirthVehicleAssemblyIndex.Removed!=(expected?1:0))throw new Exception("world "+w+" connection "+c);
}AfterEntityUnload(null);Console.WriteLine("PASS actual vehicle unload: nine world/connection combinations and null entity; server state retained");}
}
