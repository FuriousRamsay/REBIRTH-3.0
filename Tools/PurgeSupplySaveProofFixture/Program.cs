using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Threading;using Noemax.GZip;using UnityEngine;
class Program
{
 static int checks;static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
 static (World,RebirthPoiWorldSnapshot,RegionFileManager,EntitySupplyCrate,Chunk,RegionFile) Setup()
 {
  var root=Path.Combine(Path.GetTempPath(),"rebirth-supply-save-proof-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"Region"));var id=Guid.NewGuid();
  var binding=new RebirthPoiWorldBinding{WorldId=id,SaveDirectory=root,IsCurrent=true};
  var manager=new RegionFileManager{saveDirectory=Path.Combine(root,"Region")};var provider=new ChunkProviderGenerateWorld{m_RegionFileManager=manager};
  var world=new World{ChunkCache=new WorldChunkCache{ChunkProvider=provider}};GameManager.Instance=new GameManager{World=world};
  var account=new RebirthPurgeSupplyAccount(new string('a',64),75,75,1,0,1);
  var crate=new EntitySupplyCrate{world=world,onGround=true,Stamp=new RebirthPurgeSupplyCrateStamp(id,account.Player,1,account.Token(id))};
  var chunk=new Chunk{X=2,Z=-3,Key=123};chunk.entityLists[0].Add(crate);world.Chunk=chunk;
  var file=new RegionFile{Path=Path.Combine(manager.saveDirectory,"r.0.-1.7rg"),Rx=0,Rz=-1};
  var access=(RegionFileAccessMultipleChunks)manager.regionFileAccess;
  access.regionTable[manager.saveDirectory]=new RegionFileAccessMultipleChunks.Region{{new Vector2(0,-1),new RegionFileAccessMultipleChunks.RegionExtensions{{"7rg",file}}}};
  RebirthPurgeSupplyCrateSerialization.Authority=true;
  return(world,new RebirthPoiWorldSnapshot{WorldId=id,Binding=binding},manager,crate,chunk,file);
 }
 static void Save(RegionFileManager manager,RegionFile file)
 {
  var bytes=((RegionFileChunkSnapshot)manager.chunkMemoryStreamsToSave.Values.Single()).stream.ToArray();
  using(var raw=new MemoryStream()){raw.Write(bytes,0,8);using(var zip=new DeflateOutputStream(raw,3,true)){zip.Write(bytes,8,bytes.Length-8);zip.Restart();}File.WriteAllBytes(file.Path,raw.ToArray());}
 }
 static bool Confirm(RebirthPurgeSupplySaveProof proof)
 {
  for(int n=0;n<200;n++){UnityEngine.Time.realtimeSinceStartup+=3;if(proof.TryConfirm(out _))return true;Thread.Sleep(1);}return false;
 }
 static void Main()
 {
  var (world,snapshot,manager,crate,chunk,file)=Setup();
  crate.onGround=false;Check(!RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out _)&&manager.Saves==0,"falling crate cannot create saved delivery proof");crate.onGround=true;
  Check(RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out var proof)&&manager.Saves==1,"complete original native serialization queues one owned snapshot");
  Check(!File.Exists(file.Path)&&!Confirm(proof)&&!File.Exists(file.Path),"missing saved region cannot confirm and is never created by read verification");
  Check(!new RebirthPurgeSupplyDeliveryReceipt(proof).TryRead(out _),"constructing a receipt wrapper cannot manufacture completed save proof");
  Save(manager,file);Check(Confirm(proof),"actual native compression and real isolated saved bytes confirm exact whole snapshot");
  int reads=file.Reads;Check(proof.TryReceipt(out var receipt)&&receipt.TryRead(out var positive)&&positive.Token==crate.Stamp.Token,"production receipt exposes only positively confirmed original stamp");
  Check(proof.TryReceipt(out _)&&file.Reads==reads,"confirmed original receipt does not reread native saved bytes");
  snapshot.Binding.IsCurrent=false;Check(!receipt.TryRead(out _),"confirmed receipt loses authority on original world replacement");
  (world,snapshot,manager,crate,chunk,file)=Setup();manager.Partial=true;
  Check(!RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out _),"partial internally caught native serialization cannot establish save proof");
  (world,snapshot,manager,crate,chunk,file)=Setup();chunk.Throw=true;
  Check(!RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out _)&&manager.Saves==0,"direct native serialization exception prevents snapshot confirmation");
  (world,snapshot,manager,crate,chunk,file)=Setup();chunk.entityLists[0].Clear();
  Check(!RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out _)&&manager.Saves==0,"registered ownership without native chunk entity membership cannot prove delivery");
  (world,snapshot,manager,crate,chunk,file)=Setup();RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out proof);Save(manager,file);
  chunk.Data++;manager.SaveChunkSnapshot(chunk,true);Save(manager,file);
  Check(!Confirm(proof),"different complete saved snapshot cannot confirm original crate image");
  (world,snapshot,manager,crate,chunk,file)=Setup();RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out proof);Save(manager,file);
  var bytes=File.ReadAllBytes(file.Path);bytes[4]=46;File.WriteAllBytes(file.Path,bytes);
  Check(!Confirm(proof),"unknown native chunk version cannot authorize saved delivery");
  (world,snapshot,manager,crate,chunk,file)=Setup();RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out proof);Save(manager,file);snapshot.Binding.IsCurrent=false;
  Check(!Confirm(proof),"replaced binding withholds original saved bytes");
  (world,snapshot,manager,crate,chunk,file)=Setup();RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out proof);Save(manager,file);GameManager.Instance.World=new World();
  Check(!Confirm(proof),"replaced native world cannot consume old completed read");
  (world,snapshot,manager,crate,chunk,file)=Setup();RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out proof);Save(manager,file);crate.Dead=true;
  Check(!Confirm(proof),"dead current crate cannot consume an original saved delivery entitlement");
  (world,snapshot,manager,crate,chunk,file)=Setup();manager.saveDirectory=Path.GetTempPath();
  Check(!RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out _)&&manager.Saves==0,"native save directory outside original saved world is refused");
  (world,snapshot,manager,crate,chunk,file)=Setup();RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out proof);Save(manager,file);((RegionFileAccessMultipleChunks)manager.regionFileAccess).regionTable.Clear();
  Check(!Confirm(proof)&&File.Exists(file.Path),"uncached native region is not opened or recreated to manufacture proof");
  (world,snapshot,manager,crate,chunk,file)=Setup();RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out proof);Save(manager,file);RebirthPurgeSupplyCrateSerialization.Authority=false;
  Check(!Confirm(proof),"lost original crate authority withholds saved completion");
  (world,snapshot,manager,crate,chunk,file)=Setup();RebirthPurgeSupplySaveProof.TryQueue(crate,snapshot,out proof);Save(manager,file);
  file.Entered=new System.Threading.ManualResetEventSlim(false);file.Release=new System.Threading.ManualResetEventSlim(false);
  proof.TryConfirm(out _);
  Check(file.Entered.Wait(1000)&&!proof.MayRefresh,"original background read prevents proof replacement while still running");
  file.Release.Set();
  Check(Confirm(proof)&&!proof.MayRefresh,"confirmed original proof remains available without resnapshot replacement");
  Console.WriteLine("RESULT "+checks+" PASS; production save-proof helper, game Noemax compression, real isolated files. Native chunk serialization/queue/region access/current-world helpers explicitly doubled. No real game save, flight, or power-loss guarantee.");
 }
}
class Log{public static void Warning(string text){}}
class Entity{}class EntitySupplyCrate:Entity{public World world;public bool onGround;public Vector3 position;public RebirthPurgeSupplyCrateStamp Stamp;public bool IsSavedToFile()=>true;public bool Dead;public bool IsDead()=>Dead;}
class World{public WorldChunkCache ChunkCache;public Chunk Chunk;public object GetChunkFromWorldPos(object pos)=>Chunk;public static object worldToBlockPos(Vector3 pos)=>pos;}
class GameManager{public static GameManager Instance;public World World;}class ThreadManager{public static bool IsMainThread()=>true;}
class WorldChunkCache{public ChunkProviderGenerateWorld ChunkProvider;}class ChunkProviderGenerateWorld{public RegionFileManager m_RegionFileManager;}
class RebirthPoiWorldBinding{public Guid WorldId;public string SaveDirectory;public bool IsCurrent;}
class RebirthPoiWorldSnapshot{public Guid WorldId;public RebirthPoiWorldBinding Binding;}
class Chunk{public int X,Z,Data;public long Key;public bool Throw;public List<Entity>[] entityLists=Enumerable.Range(0,16).Select(_=>new List<Entity>()).ToArray();
 public void write(PooledBinaryWriter writer,bool network){if(Throw)throw new IOException("native serialization failed");writer.Write(X);writer.Write(0);writer.Write(Z);writer.Write((long)Data);foreach(var crate in entityLists.SelectMany(l=>l).OfType<EntitySupplyCrate>())crate.Stamp.Write(writer);}}
interface IRegionFileChunkSnapshot{}class RegionFileChunkSnapshot:IRegionFileChunkSnapshot{public MemoryStream stream;}class ChunkSnapshotUtil{}
class RegionFileManager{public string saveDirectory;public object snapshotUtil=new ChunkSnapshotUtil();public object regionFileAccess=new RegionFileAccessMultipleChunks();public Dictionary<long,IRegionFileChunkSnapshot> chunkMemoryStreamsToSave=new Dictionary<long,IRegionFileChunkSnapshot>();public int Saves;public bool Partial;
 public void SaveChunkSnapshot(Chunk chunk,bool unchanged){Saves++;var stream=new MemoryStream();var writer=new PooledBinaryWriter();writer.SetBaseStream(stream);writer.Write(new byte[]{116,116,99,0,47,0,0,0});if(!Partial)chunk.write(writer,false);chunkMemoryStreamsToSave[chunk.Key]=new RegionFileChunkSnapshot{stream=stream};}}
class RegionFileAccessMultipleChunks{public class Region:Dictionary<Vector2,RegionExtensions>{}public class RegionExtensions:Dictionary<string,RegionFile>{}public Dictionary<string,Region> regionTable=new Dictionary<string,Region>();}
class RegionFile{public string Path;public int Rx,Rz,Reads;public System.Threading.ManualResetEventSlim Entered,Release;public void GetPositionAndPath(out int rx,out int rz,out string path){rx=Rx;rz=Rz;path=Path;}public bool HasChunk(int x,int z)=>File.Exists(Path);public void ReadData(int x,int z,Stream target){Reads++;if(Entered!=null){Entered.Set();Release.Wait(5000);}var bytes=File.ReadAllBytes(Path);target.Write(bytes,0,bytes.Length);}}
class PooledBinaryWriter:BinaryWriter{public PooledBinaryWriter():base(new MemoryStream(),System.Text.Encoding.UTF8,true){}public void SetBaseStream(Stream stream){OutStream=stream;}}
class WriterPool{public PooledBinaryWriter AllocSync(bool reset)=>new PooledBinaryWriter();}class MemoryPools{public static WriterPool poolBinaryWriter=new WriterPool();}
class RebirthPurgeSupplyCrateSerialization{public static bool Authority;public static bool TryReadAuthoritative(EntitySupplyCrate crate,Guid world,out RebirthPurgeSupplyCrateStamp stamp){stamp=crate.Stamp;return Authority&&stamp.World==world;}}
namespace UnityEngine{public struct Vector3{public float x,y,z;}public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}public static class Time{public static float realtimeSinceStartup;}}