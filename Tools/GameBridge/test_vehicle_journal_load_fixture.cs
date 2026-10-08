using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
class GameIO {public static string DirectoryPath;public static string GetSaveGameDir(){return DirectoryPath;}}
class Log {public static void Warning(string s){}}
public struct Vector3i {public int x,y,z;public Vector3i(int a,int b,int c){x=a;y=b;z=c;}}
public enum RebirthVehicleAssemblyCarrier:byte {RepairableBlock,VehicleEntity}
public enum RebirthVehicleRequestOutcome:byte {Rejected,Applied,ReadOnly,Indeterminate}
public class RebirthVehicleAssembly {public int Value;public RebirthVehicleAssembly DeepClone(){return new RebirthVehicleAssembly{Value=Value};}}
public class RebirthVehicleAssemblySerializer {
 public static RebirthVehicleAssembly FromBase64(string s){Convert.FromBase64String(s);return new RebirthVehicleAssembly();}
 public static string ToBase64(RebirthVehicleAssembly a){return "AA==";}
}
public class RebirthVehicleRequestResult {public RebirthVehicleAssemblyCarrier Carrier;public Vector3i Position;public int EntityId;public RebirthVehicleAssembly Snapshot;public string Message,ActorId,Fingerprint;public RebirthVehicleRequestOutcome Outcome;}
// SOURCE
class Test {
 static string PathName;
 static string Entry(Guid id){return "<request id='"+id+"' carrier='0' entityId='0' outcome='1' x='1' y='2' z='3' actor='owner' fingerprint='request'/>";}
 static void Load(string entries){File.WriteAllText(PathName,"<rebirthVehicleRequestJournal format='1'>"+entries+"</rebirthVehicleRequestJournal>");RebirthVehicleRequestJournal.Clear();}
 static void Blocked(){
 string before=File.ReadAllText(PathName);RebirthVehicleRequestResult result;
 if(RebirthVehicleRequestJournal.IsAvailable||Begin(Guid.NewGuid(),"owner","request",out result))throw new Exception("admitted invalid store");
 Remember(Guid.NewGuid(),new RebirthVehicleRequestResult());
 if(File.ReadAllText(PathName)!=before)throw new Exception("overwrote corrupt store");
 }
 static long sessionGeneration;
 static bool Begin(Guid id,string actor,string fingerprint,out RebirthVehicleRequestResult result){long current;bool ok=RebirthVehicleRequestJournal.TryBegin(id,actor,fingerprint,out result,out current);if(ok)sessionGeneration=current;return ok;}
 static void Remember(Guid id,RebirthVehicleRequestResult result){RebirthVehicleRequestResult ignored;Begin(id,result.ActorId,result.Fingerprint,out ignored);RebirthVehicleRequestJournal.Remember(id,result,sessionGeneration);}
 static void Main(){
 string dir=Path.Combine(Path.GetTempPath(),"rebirth-journal-load-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);GameIO.DirectoryPath=dir;PathName=Path.Combine(dir,"RebirthVehicleRequestJournal.xml");
 try {
 Guid id=Guid.NewGuid();Load(Entry(id));RebirthVehicleRequestResult result;
 if(!RebirthVehicleRequestJournal.IsAvailable||!RebirthVehicleRequestJournal.TryGet(id,out result))throw new Exception("valid record lost");
 GameIO.DirectoryPath="";
 if(RebirthVehicleRequestJournal.IsAvailable||RebirthVehicleRequestJournal.TryGet(id,out result)||Begin(Guid.NewGuid(),"owner","empty",out result))throw new Exception("missing directory reused previous world");
 GameIO.DirectoryPath=dir;
 if(!RebirthVehicleRequestJournal.IsAvailable||!RebirthVehicleRequestJournal.TryGet(id,out result))throw new Exception("directory restoration failed");

 Load(Entry(id).Replace("carrier='0'","carrier='999'"));Blocked();
 Load(Entry(id).Replace("/>"," snapshot='not-base64'/>"));Blocked();
 Load(Entry(id).Replace("actor='owner'","actor=' '"));Blocked();
 Load(Entry(id).Replace("fingerprint='request'","fingerprint=''"));Blocked();
 Load(Entry(id)+"<unexpected/>");Blocked();
 Load(Entry(id)+Entry(id));Blocked();
 string many=Entry(id);for(int i=0;i<513;i++)many+=Entry(Guid.NewGuid());Load(many+Entry(id));Blocked();
 File.Delete(PathName);File.WriteAllText(PathName+".bak","prior");RebirthVehicleRequestJournal.Clear();
 if(RebirthVehicleRequestJournal.IsAvailable)throw new Exception("missing primary admitted");
 File.Delete(PathName+".bak");RebirthVehicleRequestJournal.Clear();
 if(!Begin(Guid.NewGuid(),"owner","new",out result))throw new Exception("fresh journal blocked");
 // Exercise admission/replay in this disposable directory, never the live save.
 Guid transaction=Guid.NewGuid();
 if(!Begin(transaction,"owner","transfer",out result))throw new Exception("first admission failed");
 if(Begin(transaction,"owner","transfer",out result))throw new Exception("duplicate admitted");
 var original=new RebirthVehicleRequestResult {ActorId="owner",Fingerprint="transfer",Message="original",Snapshot=new RebirthVehicleAssembly{Value=7}};
 Remember(transaction,original);original.Message="mutated";
 if(!RebirthVehicleRequestJournal.TryGet(transaction,out result)||object.ReferenceEquals(original,result)||result.Message!="original")throw new Exception("result not isolated");
 result.Message="caller mutation";result.Snapshot.Value=99;
 if(!RebirthVehicleRequestJournal.TryGet(transaction,out result)||result.Message!="original"||result.Snapshot.Value!=7)throw new Exception("read leaked journal reference");
 if(Begin(transaction,"owner","transfer",out result)||result==null)throw new Exception("duplicate result unavailable");
 result.ActorId="mutated owner";result.Snapshot.Value=88;
 if(!RebirthVehicleRequestJournal.TryGet(transaction,"owner","transfer",out result)||result.Snapshot.Value!=7)throw new Exception("admission leaked journal reference");
 RebirthVehicleRequestJournal.Clear();
 if(!RebirthVehicleRequestJournal.TryGet(transaction,"owner","transfer",out result)||result.Message!="original")throw new Exception("replay not persisted");
 if(RebirthVehicleRequestJournal.TryGet(transaction,"different-owner","transfer",out result))throw new Exception("owner mismatch accepted");
 RebirthVehicleRequestJournal.Clear();
 Guid sessionId=Guid.NewGuid();long oldSession,newSession;
 if(!RebirthVehicleRequestJournal.TryBegin(sessionId,"owner","session",out result,out oldSession))throw new Exception("old session admission");
 RebirthVehicleRequestJournal.Clear();
 if(!RebirthVehicleRequestJournal.TryBegin(sessionId,"owner","session",out result,out newSession))throw new Exception("new session admission");
 if(RebirthVehicleRequestJournal.Remember(sessionId,new RebirthVehicleRequestResult{ActorId="owner",Fingerprint="session"},oldSession))throw new Exception("old session published");
 RebirthVehicleRequestJournal.Abort(sessionId,oldSession);
 long ignoredSession;
 if(RebirthVehicleRequestJournal.TryBegin(sessionId,"owner","session",out result,out ignoredSession))throw new Exception("old abort released new request");
 RebirthVehicleRequestJournal.Abort(sessionId,newSession);
 if(!RebirthVehicleRequestJournal.TryBegin(sessionId,"owner","session",out result,out ignoredSession))throw new Exception("current abort did not release");
 // A directory at the temporary-file path deterministically prevents writing.
 Load(Entry(id));
 if(!RebirthVehicleRequestJournal.IsAvailable)throw new Exception("pre-save unavailable");
 Directory.CreateDirectory(PathName+".tmp");
 Guid failedWriteId=Guid.NewGuid();
 string durableBeforeFailure=File.ReadAllText(PathName);
 Remember(failedWriteId,new RebirthVehicleRequestResult {ActorId="owner",Fingerprint="save-failure",Message="known in memory"});
 if(RebirthVehicleRequestJournal.IsAvailable||Begin(Guid.NewGuid(),"owner","after-failure",out result))throw new Exception("save failure admitted more mutations");
 if(!RebirthVehicleRequestJournal.TryGet(failedWriteId,out result)||result.Message!="known in memory")throw new Exception("failed write lost known live outcome");
 result.Message="caller mutation";
 if(!RebirthVehicleRequestJournal.TryGet(failedWriteId,out result)||result.Message!="known in memory")throw new Exception("failed-store replay exposed mutable result");
 if(File.ReadAllText(PathName)!=durableBeforeFailure)throw new Exception("failed write altered durable primary");
 Directory.Delete(PathName+".tmp");
 Console.WriteLine("PASS: valid load, malformed fields/payload, duplicate beyond retention, corrupt-file preservation, orphan backup, fresh admission, missing-directory isolation, failed-write admission blocking/live replay isolation/unchanged durable primary, read/admission result isolation");
 } finally {RebirthVehicleRequestJournal.Clear();Directory.Delete(dir,true);}
 }
}
