// Actual tank class read/write body; native TileEntity base header and pooled
// binary adapters are explicit doubles. No native chunk/game execution claim.
using System;using System.IO;using System.Linq;
public enum StreamModeRead {Persistency,Network}
public enum StreamModeWrite {Persistency,Network}
public enum TileEntityType {Other}
public class Chunk{}
public struct Vector3i{}
public class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){}}
public class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter(Stream s):base(s){}}
class NoSeekStream:MemoryStream {public override bool CanSeek=>false;public override long Position {get{throw new NotSupportedException();}set{throw new NotSupportedException();}}}
public class TileEntity {
 private int version;public TileEntity(Chunk c){}public virtual TileEntityType GetTileEntityType()=>TileEntityType.Other;public int GetLegacyForkVersion()=>version;
 public virtual void read(PooledBinaryReader r,StreamModeRead mode){version=r.ReadUInt16();if(r.ReadInt32()!=12345)throw new InvalidDataException("native header changed");}
 public virtual void write(PooledBinaryWriter w,StreamModeWrite mode){w.Write((ushort)19);w.Write(12345);}public virtual void OnDestroy(){}public Vector3i ToWorldPos()=>new Vector3i();
}
static class AdvancedFarmingBinaryCompat {public static int ReadInt(PooledBinaryReader r)=>r.ReadInt32();public static void WriteInt(PooledBinaryWriter w,int n)=>w.Write(n);public static void WriteUShort(PooledBinaryWriter w,ushort n)=>w.Write(n);}
static class RebirthUtilities {public enum TileEntityRebirth {TileEntityWaterTankRebirth=25}public static int TotalGameSecondsPassed()=>321;}
static class AdvancedFarmingWaterProviderRegistry {public static void UnregisterProvider(Vector3i p){}}
// PRODUCTION_TANK
static class Fixture {
 private static void Check(bool yes,string why){if(!yes)throw new Exception(why);}
 private static byte[] Write(TileEntityWaterTankRebirth tank,StreamModeWrite mode){using(var s=new MemoryStream()){using(var w=new PooledBinaryWriter(s)){tank.write(w,mode);return s.ToArray();}}}
 private static TileEntityWaterTankRebirth Read(byte[] bytes,StreamModeRead mode){using(var s=new MemoryStream(bytes))using(var r=new PooledBinaryReader(s)){var tank=new TileEntityWaterTankRebirth(new Chunk());tank.read(r,mode);Check(s.Position==s.Length,"tank reader consumed wrong record span");return tank;}}
 public static void Main(){
  var tank=new TileEntityWaterTankRebirth(new Chunk()){waterCount=33,waterMax=400,timeLapsed=20,LastProcessedWorldSeconds=21,WaterIncarnation=Guid.NewGuid(),WaterExchangeOperation=Guid.NewGuid(),WaterExchangeRevision=7,WaterExchangeBefore=35,WaterExchangeAfter=34};
  var bytes=Write(tank,StreamModeWrite.Persistency);Check(BitConverter.ToUInt16(bytes,0)==0xA701&&BitConverter.ToInt32(bytes,2)==12345,"native base header rewritten beyond own version word");var decoded=Read(bytes,StreamModeRead.Persistency);Check(decoded.WaterIncarnation==tank.WaterIncarnation&&decoded.WaterExchangeOperation==tank.WaterExchangeOperation&&decoded.WaterExchangeRevision==7&&decoded.WaterExchangeBefore==35&&decoded.WaterExchangeAfter==34&&decoded.waterCount==33,"original historical tank stamp lost");
  var network=Write(tank,StreamModeWrite.Network);Check(network.Length==22&&BitConverter.ToUInt16(network,0)==19,"legacy network framing changed");var remote=Read(network,StreamModeRead.Network);Check(remote.WaterIncarnation==Guid.Empty&&remote.waterCount==33,"network payload consumed server-only stamp");
  var legacy=Read(network,StreamModeRead.Persistency);Check(legacy.WaterIncarnation==Guid.Empty&&legacy.waterMax==400,"legacy native tank record no longer loads");var upgraded=Read(Write(legacy,StreamModeWrite.Persistency),StreamModeRead.Persistency);Check(upgraded.WaterIncarnation!=Guid.Empty&&legacy.WaterIncarnation==upgraded.WaterIncarnation,"legacy tank save did not persist one stable incarnation");
  bool rejected=false;try{Read(bytes.Take(bytes.Length-3).ToArray(),StreamModeRead.Persistency);}catch(EndOfStreamException){rejected=true;}Check(rejected,"truncated reserved version silently fell back to legacy");
  var future=(byte[])bytes.Clone();future[0]=2;rejected=false;try{Read(future,StreamModeRead.Persistency);}catch(InvalidDataException){rejected=true;}Check(rejected,"unknown reserved version accepted as current");
  using(var stream=new NoSeekStream())using(var writer=new PooledBinaryWriter(stream)){rejected=false;try{tank.write(writer,StreamModeWrite.Persistency);}catch(IOException){rejected=true;}Check(rejected&&stream.Length==0,"unseekable persistency emitted ambiguous partial native header");}
  using(var stream=new NoSeekStream())using(var writer=new PooledBinaryWriter(stream)){tank.write(writer,StreamModeWrite.Network);Check(stream.Length==22,"network write acquired unnecessary seeking requirement");}
  Console.WriteLine("PASS actual tank persistency/network/legacy/version/truncation body; native base header and adapters doubled");
 }
}
