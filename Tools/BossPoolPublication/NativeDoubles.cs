using System;using System.IO;using System.Text;
public class World {public WorldState worldState=new WorldState();}
public class WorldState {public string Guid=System.Guid.NewGuid().ToString("N");}
public class Chunk {public const int CurrentSaveVersion=47;public int X=2,Z=3;}
public class Entity {}
public static class GameManager {public static InstanceData Instance=new InstanceData();public class InstanceData{public World World;}}
public static class GameIO {public static string Directory="fixture";public static string GetSaveGameDir()=>Directory;}
public static class ThreadManager {public static bool Main=true;public static bool IsMainThread()=>Main;}
public enum EnumSpawnerSource:byte{Biome=1,StaticSpawner=2,Dynamic=3}
public enum StreamModeRead{Persistency}
public class PooledBinaryReader:IDisposable {
 BinaryReader value;public void SetBaseStream(Stream stream){value=new BinaryReader(stream,Encoding.UTF8,true);}
 public int ReadInt32()=>value.ReadInt32();public byte ReadByte()=>value.ReadByte();public ushort ReadUInt16()=>value.ReadUInt16();public ulong ReadUInt64()=>value.ReadUInt64();public byte[] ReadBytes(int n)=>value.ReadBytes(n);
 public static implicit operator BinaryReader(PooledBinaryReader r)=>r.value;public void Dispose()=>value.Dispose();
}
public static class MemoryPools{public static Pool poolBinaryReader=new Pool();public class Pool{public PooledBinaryReader AllocSync(bool reset)=>new PooledBinaryReader();}}
public class NativeStat{public float Value;}public class NativeStats{public NativeStat Health=new NativeStat();}
public class EntityCreationData{
 public static float HealthValue=1f;public NativeStats stats=new NativeStats();public int id,entityClass;public byte readFileVersion=38;public MemoryStream entityData;
 public void read(PooledBinaryReader reader,StreamModeRead mode){stats.Health.Value=HealthValue;id=reader.ReadInt32();entityClass=reader.ReadInt32();int n=reader.ReadInt32();if(n<0||n>65535)throw new InvalidDataException();var bytes=reader.ReadBytes(n);if(bytes.Length!=n)throw new EndOfStreamException();entityData=new MemoryStream(bytes,false);}
}
