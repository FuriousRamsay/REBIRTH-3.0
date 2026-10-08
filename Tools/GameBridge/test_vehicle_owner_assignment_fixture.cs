using System;
public class Vehicle {public object OwnerId;}
public class EntityVehicle {public Vehicle vehicle=new Vehicle();public int belongsPlayerId=-1;}
public class EntityPlayer {public int entityId=7;}
public class PersistentPlayerData {public object PrimaryId;}
public class PersistentList {public PersistentPlayerData Data;public PersistentPlayerData GetPlayerDataFromEntityID(int id){return Data;}}
public class GameManager {public static GameManager Instance;public PersistentList List;public PersistentList GetPersistentPlayerList(){return List;}}
class Check {
// METHODS
static void Main(){var player=new EntityPlayer();var entity=new EntityVehicle();if(HasPersistentOwner(player))throw new Exception("Missing manager accepted");GameManager.Instance=new GameManager{List=new PersistentList()};bool failed=false;try{AssignOwner(entity,player);}catch(InvalidOperationException){failed=true;}if(!failed||entity.belongsPlayerId!=-1||entity.vehicle.OwnerId!=null)throw new Exception("Missing owner mutated vehicle");GameManager.Instance.List.Data=new PersistentPlayerData();if(HasPersistentOwner(player))throw new Exception("Null persistent ID accepted");var id=new object();GameManager.Instance.List.Data.PrimaryId=id;if(!HasPersistentOwner(player))throw new Exception("Valid owner refused");AssignOwner(entity,player);if(entity.vehicle.OwnerId!=id||entity.belongsPlayerId!=7)throw new Exception("Owner identity lost");Console.WriteLine("PASS actual owner preflight/assignment: missing manager/record/ID refused without mutation; valid stable owner assigned. Native identity/list doubles.");}
}