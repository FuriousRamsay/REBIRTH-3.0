using System;using System.IO;
namespace UnityEngine.Scripting{public class PreserveAttribute:Attribute{}}
public enum NetPackageDirection{ToClient}
public class PooledBinaryReader:BinaryReader{public PooledBinaryReader(Stream s):base(s){}}
public class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter(Stream s):base(s){}}
public abstract class NetPackage{public virtual NetPackageDirection PackageDirection=>NetPackageDirection.ToClient;public virtual void read(PooledBinaryReader r){}public virtual void write(PooledBinaryWriter w){}public virtual void ProcessPackage(World w,GameManager g){}}
public static class NetPackageManager{public static int GetPackageId(Type t)=>1;public static T GetPackage<T>()where T:new()=>new();}
public class NetPackagePlayerData{public object Setup(EntityPlayerLocal p)=>this;}
public class NetPackageRebirthGearPreparationRequest{public object Setup(int p,string marker)=>this;}
public static class RebirthBackpackLibraryClientViews{public static bool Request(World w,int p)=>true;}
