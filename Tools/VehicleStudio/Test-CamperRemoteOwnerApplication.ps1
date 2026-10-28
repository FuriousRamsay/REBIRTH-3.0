$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Initialize-TemporaryFiles.ps1')
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../Scripts/Vehicles/Camper/RebirthCamperRemoteOwnerApplication.cs'))
$source=$source.Substring(0,$source.IndexOf('[Preserve]'))
$source=[regex]::Replace($source,'(?m)^using [^;]+;\r?\n','')
$fixture=@'
using System;using System.Collections;using System.Collections.Generic;using UnityEngine;
namespace UnityEngine {public static class Time {public static float realtimeSinceStartup;}}
public static class ThreadManager {public static bool IsMainThread()=>true;}
public class World {public bool Remote;public EntityPlayerLocal Player;public EntityPlayer ServerPlayer;public bool IsRemote()=>Remote;public EntityPlayerLocal GetPrimaryPlayer()=>Player;public object GetEntity(int id)=>ServerPlayer;}
public class EntityPlayer {public int entityId=8;public World world;public bool IsDead()=>false;}
public class Buffs {public Dictionary<string,float> Values=new Dictionary<string,float>();public float GetCustomVar(string k)=>Values.TryGetValue(k,out var f)?f:0;public void SetCustomVar(string k,float f,bool sync){Values[k]=f;}}
public class EntityPlayerLocal:EntityPlayer {public Buffs Buffs=new Buffs();}
public class Identity {public string CombinedString="native-owner";}
public class ClientInfo {public Identity InternalId=new Identity();public void SendPackage(object p){}}
public class Clients {public ClientInfo Sender=new ClientInfo();public ClientInfo ForEntityId(int id)=>Sender;}
public class ConnectionManager {public Clients Clients=new Clients();public int Publications;public Dictionary<string,float> Saved=new Dictionary<string,float>();public void SendToServer(NetPackagePlayerData data,bool now){Publications++;Saved=new Dictionary<string,float>(data.Player.Buffs.Values);}}
public static class SingletonMonoBehaviour<T> where T:new(){public static T Instance=new T();}
public class NetPackagePlayerData {public EntityPlayerLocal Player;public NetPackagePlayerData Setup(EntityPlayerLocal p){Player=p;return this;}}
public class NetPackageRebirthCamperOwnerEffect {public object Setup(int p,Guid op,RebirthCamperRemoteOwnerEffectKind kind,byte[] bytes)=>this;}
public static class NetPackageManager {public static int GetPackageId(Type t)=>throw new NullReferenceException("Native mappings not initialized during ModAPI startup");public static T GetPackage<T>() where T:new()=>new T();}
public class GameManager {public static GameManager Instance=new GameManager();public World World;public List<IEnumerator> Coroutines=new List<IEnumerator>();public void StartCoroutine(IEnumerator task){Coroutines.Add(task);}}
public static class RebirthCamperRemoteTransport {public static void AssertFrame(byte[] data){if(data==null||data.Length>8388608)throw new ArgumentException();}}
public static class RebirthCamperSavedCursor {public static bool VerifyPublishedReceipt(string owner,Guid op,Func<bool> current,string prefix){var saved=SingletonMonoBehaviour<ConnectionManager>.Instance.Saved;return current()&&saved.TryGetValue(prefix+op.ToString("N"),out var f)&&f==1;}}
public static class Log {public static void Warning(string message){}}
public static class OwnerApplicationFixture {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){
  var remote=new World{Remote=true};var client=new EntityPlayerLocal{world=remote};remote.Player=client;
  var serverWorld=new World();var server=new EntityPlayer{world=serverWorld};serverWorld.ServerPlayer=server;GameManager.Instance.World=serverWorld;
  int writes=0;bool succeed=true;
  RebirthCamperRemoteOwnerApplication.RegisterOwnerApplication(p=>"creation",(p,op,k,payload)=>{Check(p.Buffs.GetCustomVar("rbCamperOwner_"+op.ToString("N"))==2,"Intent receipt missing before native effect");writes++;return succeed;});
  var operation=Guid.NewGuid();bool ack=false;
  Check(RebirthCamperRemoteOwnerApplication.SendOwnerApplication(server,operation,RebirthCamperRemoteOwnerEffectKind.Reward,new byte[]{1},a=>ack=a),"Authenticated server effect refused");
  RebirthCamperRemoteOwnerApplication.Receive(remote,8,operation,RebirthCamperRemoteOwnerEffectKind.Reward,new byte[]{1});
  Check(writes==1&&client.Buffs.GetCustomVar("rbCamperOwner_"+operation.ToString("N"))==1,"Completed effect not acknowledged as receipt1");
  RebirthCamperRemoteOwnerApplication.Receive(remote,8,operation,RebirthCamperRemoteOwnerEffectKind.Reward,new byte[]{1});
  Check(writes==1&&SingletonMonoBehaviour<ConnectionManager>.Instance.Publications==2,"Completed duplicate re-applied native effect");
  Check(!GameManager.Instance.Coroutines[0].MoveNext()&&ack,"Exact published native receipt failed server acknowledgment");
  operation=Guid.NewGuid();succeed=false;bool called=false;ack=true;
  Check(RebirthCamperRemoteOwnerApplication.SendOwnerApplication(server,operation,RebirthCamperRemoteOwnerEffectKind.Refund,new byte[]{2},a=>{called=true;ack=a;}),"Second effect refused");
  RebirthCamperRemoteOwnerApplication.Receive(remote,8,operation,RebirthCamperRemoteOwnerEffectKind.Refund,new byte[]{2});
  Check(client.Buffs.GetCustomVar("rbCamperOwner_"+operation.ToString("N"))==2,"Incomplete effect became receipt1");
  RebirthCamperRemoteOwnerApplication.Receive(remote,8,operation,RebirthCamperRemoteOwnerEffectKind.Refund,new byte[]{2});Check(writes==2,"Partial effect replayed native write");
  Check(GameManager.Instance.Coroutines[1].MoveNext(),"Partial receipt prematurely acknowledged");UnityEngine.Time.realtimeSinceStartup=31;
  Check(!GameManager.Instance.Coroutines[1].MoveNext()&&called&&!ack,"Partial timeout became successful acknowledgment");
  operation=Guid.NewGuid();RebirthCamperRemoteOwnerApplication.Receive(remote,9,operation,RebirthCamperRemoteOwnerEffectKind.Refund,new byte[]{2});
  Check(writes==2,"Wrong local player accepted effect");
 }
}
'@
Add-Type -TypeDefinition ($fixture+"`n"+$source)
[OwnerApplicationFixture]::Run()
Write-Output 'PASS actual owner effect adapter: receipt2 precedes native root callback, intended completion alone marks1, duplicate1 acknowledges without replay, partial2 retains/no ack, timeout retains, wrong local target refused. Native network/save/effects are explicit doubles; root effect payloads and multiplayer remain unqualified.'
