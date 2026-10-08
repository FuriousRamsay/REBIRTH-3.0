using System.Collections.Generic;
public class ClientInfo{public PlatformIdentity PlatformId,CrossplatformId;public int ClientNumber,entityId;public bool loginDone=true,bAttachedToEntity=true,disconnecting;}
public class ClientInfoCollection{public Dictionary<int,ClientInfo> Entries=new Dictionary<int,ClientInfo>();public ClientInfo ForClientNumber(int n)=>Entries.TryGetValue(n,out var c)?c:null;}
public class ConnectionManager{public bool IsServer=true;public ClientInfoCollection Clients=new ClientInfoCollection();}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public class GameManager{public static GameManager Instance=new GameManager();public World World;public bool Editor;public bool IsEditMode()=>Editor;public PersistentPlayers Players=new PersistentPlayers();public PersistentPlayers GetPersistentPlayerList()=>Players;}
public class World{public bool Allowed=true;public System.Action OnPermission;public bool CanPlaceBlockAt(Vector3i pos,PersistentPlayerData p){OnPermission?.Invoke();return Allowed;}public bool Remote;public Dictionary<int,EntityPlayer> Actors=new Dictionary<int,EntityPlayer>();public bool IsRemote()=>Remote;public object GetEntity(int id)=>Actors.TryGetValue(id,out var p)?p:null;}
public class EntityPlayer{public int entityId;public World world;public bool Dead;public bool IsDead()=>Dead;}
public class PlatformIdentity{public string Value;public override bool Equals(object other)=>other is PlatformIdentity id&&id.Value==Value;public override int GetHashCode()=>Value.GetHashCode();}
public class PersistentPlayerData{public PlatformIdentity PrimaryId;}
public class PersistentPlayers{public System.Collections.Generic.Dictionary<int,PersistentPlayerData> Records=new System.Collections.Generic.Dictionary<int,PersistentPlayerData>();public PersistentPlayerData GetPlayerDataFromEntityID(int id)=>Records.TryGetValue(id,out var record)?record:null;}
public class Block{}
public class BlockPlantGrowingRebirth:Block{public bool Seed=true,Allowed=true;public System.Action OnPermission;public bool IsSeedStage(BlockValue v)=>Seed;public bool CanPlaceBlockAt(World w,Vector3i p,BlockValue v){OnPermission?.Invoke();return Allowed;}}
public class DerivedCrop:BlockPlantGrowingRebirth{}
public struct BlockValue{public uint rawData;public int damage;public Block Block;}
public class BlockLimitTracker{public static BlockLimitTracker instance=new BlockLimitTracker();public bool Allowed=true;public System.Action OnPermission;public bool CanAddBlock(BlockValue v,Vector3i p,out int response){response=0;OnPermission?.Invoke();return Allowed;}}
public static class SeedPlacementIntentCodecReview{public const int MaxSeedBytes=8192;public struct Intent{public int OriginalCount;public int Slot;public Vector3i Position;public BlockValue Target,ExpectedOld;public byte Flags;public sbyte Density;public long Texture;public byte[] Seed;}}
public class NetPackageSetBlock{}
public struct Vector3i{public int x,y,z;}
