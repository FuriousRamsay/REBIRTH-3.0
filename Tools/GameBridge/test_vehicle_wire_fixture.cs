using System;using System.IO;using System.Text;
class PooledBinaryReader:BinaryReader {public PooledBinaryReader(Stream s):base(s){}}class PooledBinaryWriter:BinaryWriter {public PooledBinaryWriter(Stream s):base(s){}}
class NetPackage {public virtual void write(PooledBinaryWriter w){w.Write((ushort)1);}public virtual void read(PooledBinaryReader r){}}
class PlatformUserIdentifierAbs {public static PlatformUserIdentifierAbs FromStream(BinaryReader r){r.ReadString();return new PlatformUserIdentifierAbs();}public void ToStream(BinaryWriter w){w.Write("test-user");}}
struct Vector3i {public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
enum RebirthVehicleAssemblyCarrier:byte {RepairableBlock,VehicleEntity}enum RebirthVehicleAssemblyAction:byte {Read,Install}enum RebirthVehiclePartSourceLocation:byte {None}enum RebirthVehicleRequestOutcome:byte {Applied}
class RebirthInstalledVehiclePart {public string ItemName,SerializedItemValue;public int Quality;public float UseTimes,Roll;}
class Request:NetPackage {const byte WireVersion=2;string expectedCreationId="11111111111111111111111111111111";Guid requestId=Guid.NewGuid(),assemblyId=Guid.NewGuid();int playerId=3,entityId=4,sourceSlot=-1;PlatformUserIdentifierAbs userId=new PlatformUserIdentifierAbs();RebirthVehicleAssemblyCarrier carrier;Vector3i position;long expectedRevision=7;RebirthVehicleAssemblyAction action;string slotId="Engine";RebirthInstalledVehiclePart offeredPart;RebirthVehiclePartSourceLocation sourceLocation;
// REQUEST
public static void Test()
{
 foreach(string creation in new[]{"", "11111111111111111111111111111111", "11111111-1111-1111-1111-111111111111", "legacy-"+new string('a',64)})
 foreach(int size in new[]{0,10,8192,262144,262145})
 {
  var source=new Request{expectedCreationId=creation,offeredPart=new RebirthInstalledVehiclePart{ItemName="part",SerializedItemValue=new string('A',size),Quality=4,UseTimes=5,Roll=.5f}};
  var m=new MemoryStream();source.write(new PooledBinaryWriter(m));if(source.GetLength()<m.Length)throw new Exception("Request estimate too small");m.Position=2;
  var target=new Request();bool rejected=false;try{target.read(new PooledBinaryReader(m));}catch(InvalidDataException){rejected=true;}
  if(rejected!=(size>262144))throw new Exception("Size bound failed");
  string normalized="";if(creation.Length>0&&!RebirthSurvivorRequestScope.TryNormalize(creation,out normalized))throw new Exception("Bad fixture identity");
  if(!rejected&&(target.expectedCreationId!=normalized||target.offeredPart.SerializedItemValue!=source.offeredPart.SerializedItemValue||target.requestId!=source.requestId||m.Position!=m.Length))throw new Exception("Request roundtrip");
 }
 foreach(string invalid in new[]{"legacy-"+new string('a',64)+"suffix", "legacy-"+new string('A',64), "00000000000000000000000000000000", "not-a-character"})
 {
  var m=new MemoryStream();var w=new BinaryWriter(m);w.Write(WireVersion);w.Write(invalid);m.Position=0;
  var target=new Request();bool rejected=false;try{target.read(new PooledBinaryReader(m));}catch(InvalidDataException){rejected=true;}
  if(!rejected||target.expectedCreationId!=null||target.requestId!=Guid.Empty)throw new Exception("Invalid identity retained or accepted");
 }
 foreach(byte version in new byte[]{0,1,3,255})
 {
  var target=new Request();bool rejected=false;try{target.read(new PooledBinaryReader(new MemoryStream(new[]{version})));}catch(InvalidDataException){rejected=true;}
  if(!rejected||target.expectedCreationId!=null||target.requestId!=Guid.Empty)throw new Exception("Invalid version retained or accepted");
 }
}}
class Snapshot:NetPackage {Guid requestId=Guid.NewGuid();RebirthVehicleAssemblyCarrier carrier;Vector3i position;int entityId=8;string payload,message;RebirthVehicleRequestOutcome outcome;
// SNAPSHOT
public static void Test(){foreach(int size in new[]{0,100,349528}){var source=new Snapshot{payload=new string('A',size),message="État 日本語"};var m=new MemoryStream();source.write(new PooledBinaryWriter(m));if(source.GetLength()<m.Length)throw new Exception("Snapshot estimate too small");m.Position=2;var target=new Snapshot();target.read(new PooledBinaryReader(m));if(target.payload!=source.payload||target.message!=source.message||m.Position!=m.Length)throw new Exception("Snapshot roundtrip");}}}
class Check {static void Main(){Request.Test();Snapshot.Test();Console.WriteLine("PASS: actual request/reply wire methods preserve item payload and Unicode text, reported lengths cover tested sizes, request rejects over-limit payload and malformed/overlong character scope, preserves GUID/legacy/disabled scopes, refuses obsolete wire versions. Platform identity/native transport substituted.");}}
