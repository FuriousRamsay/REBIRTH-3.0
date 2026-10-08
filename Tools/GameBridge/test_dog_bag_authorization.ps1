$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$s=[IO.File]::ReadAllText((Join-Path $root 'Scripts/NPC/Dog/RebirthDogStorageService.cs'))
$start=$s.IndexOf('    internal static bool Prefix(NetPackageBag')
$end=$s.IndexOf('    private static void Postfix', $start)
if($start -lt 0 -or $end -lt 0){throw 'Guard source missing'}
$method=$s.Substring($start,$end-$start)
$stub=@"
using System.Collections.Generic;
public class EntityPlayer { public int entityId; }
public class EntityRebirthDogCompanion { public int entityId=10; }
public class ClientInfo { public int entityId; }
public class NetPackageBag { public int entityId=10; public ClientInfo Sender; }
public class World { public bool Remote; public object Target; public EntityPlayer Player=new EntityPlayer{entityId=1}; public bool IsRemote(){return Remote;} public object GetEntity(int id){return id==10?Target:(id==Player.entityId?Player:null);} }
public static class RebirthDogStorageService { public static bool Open=true; public static bool IsOpenServer(int id){return Open;} }
public static class RebirthDogLifecycleService { public static bool Owned=true; public static bool IsOwnedBy(EntityRebirthDogCompanion d,EntityPlayer p){return Owned;} }
public class LockManager { public static LockManager Instance=new LockManager(); public Locks singleLocks=new Locks(); public struct LockEntry { public object Target; public ushort Channel; public LockEntry(object t,ushort c){Target=t;Channel=c;} } public class Locks { public bool Held=true; public int Owner=1; public bool TryGetByValue(LockEntry e,out int owner){owner=Owner;return Held&&e.Channel==0&&e.Target is EntityRebirthDogCompanion;} } }
public static class Fixture {
$method
static void Check(bool x,string s){if(!x)throw new System.Exception(s);}
public static void Run(){
var world=new World{Target=new EntityRebirthDogCompanion()};var packet=new NetPackageBag{Sender=new ClientInfo{entityId=1}};bool state;
Check(Prefix(packet,world,out state)&&state,"owner lease accepted");
packet.Sender=null;Check(!Prefix(packet,world,out state)&&!state,"unauthenticated rejected");packet.Sender=new ClientInfo{entityId=2};Check(!Prefix(packet,world,out state)&&!state,"absent sender player rejected");packet.Sender.entityId=1;
RebirthDogLifecycleService.Owned=false;Check(!Prefix(packet,world,out state)&&!state,"other owner rejected");RebirthDogLifecycleService.Owned=true;
LockManager.Instance.singleLocks.Owner=2;Check(!Prefix(packet,world,out state)&&!state,"other lock owner rejected");LockManager.Instance.singleLocks.Owner=1;
LockManager.Instance.singleLocks.Held=false;Check(!Prefix(packet,world,out state)&&!state,"no lease rejected");LockManager.Instance.singleLocks.Held=true;
RebirthDogStorageService.Open=false;Check(!Prefix(packet,world,out state)&&!state,"closed editor rejected");RebirthDogStorageService.Open=true;
world.Remote=true;Check(!Prefix(packet,world,out state)&&!state,"client mutation rejected");world.Remote=false;
world.Target=new object();Check(Prefix(packet,world,out state)&&!state,"other native bags unchanged");
Check(Prefix(packet,null,out state)&&!state,"native null world passthrough");
}
}
"@
Add-Type -TypeDefinition $stub
[Fixture]::Run()
'PASS actual dog bag prefix authorization branches; native world/lock/ownership adapters doubled, no game execution.'