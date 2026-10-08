using System;using System.Collections.Generic;using System.Globalization;
class World{public EntityPlayerLocal Player=new EntityPlayerLocal();public EntityPlayerLocal GetPrimaryPlayer(){return Player;}}
class EntityPlayerLocal{public int entityId=9;}
class PersistentPlayerData{public object PrimaryId=new object();}
class GameManager{public static GameManager Instance=new GameManager();public World World=new World();public PersistentPlayerData Persistent=new PersistentPlayerData();public PersistentPlayerData GetPersistentLocalPlayer(){return Persistent;}}
enum RebirthCompanionInventoryAction{ToggleLock,UseOne,TransferToPlayer}
enum QuickStackRadialAction{CompanionInventoryPull=8}
class RebirthCompanionListEntry{public float Distance;}
class ConnectionManager{public bool IsServer;public int Sent;public void SendToServer(object p){Sent++;}}
class SingletonMonoBehaviour<T>{public static T Instance;}
class NetPackageManager{public static T GetPackage<T>()where T:new(){return new T();}}
class NetPackageRebirthCompanionInventoryAction{public object Setup(int id,PersistentPlayerData p,string target,RebirthCompanionInventoryAction a,int slot,int type,string key,int q){return this;}}
static class LogisticsTransferService{public static int Calls;public static string Target,Selection;public static void Request(QuickStackRadialAction a,string t,string s){if(a!=QuickStackRadialAction.CompanionInventoryPull)throw new Exception("wrong action");Calls++;Target=t;Selection=s;}}
class Subject{const float StorageDistance=5;public static bool Owned=true;public static float Distance;public static int Direct;static bool TryGetLocalOwnedEntry(string id,out RebirthCompanionListEntry e){e=new RebirthCompanionListEntry{Distance=Distance};return Owned;}static void ProcessInventoryAction(World w,int p,object u,string t,RebirthCompanionInventoryAction a,int s,int type,string key,int q){Direct++;}
// PRODUCTION_METHOD
}
class Check{static void Main(){foreach(bool host in new[]{false,true}){var c=new ConnectionManager{IsServer=host};SingletonMonoBehaviour<ConnectionManager>.Instance=c;Subject.Direct=0;LogisticsTransferService.Calls=0;Subject.RequestInventoryAction("N:dog",RebirthCompanionInventoryAction.TransferToPlayer,2,7,"custom|key",3);if(LogisticsTransferService.Calls!=1||Subject.Direct!=0||c.Sent!=0)throw new Exception("old route used");int slot,type,count;string key;if(!Subject.TryReadInventoryTransferSelection(LogisticsTransferService.Selection,out slot,out type,out count,out key)||slot!=2||type!=7||count!=3||key!="custom|key")throw new Exception("selection roundtrip");Subject.RequestInventoryAction("N:dog",RebirthCompanionInventoryAction.ToggleLock,2,7,"custom");if(Subject.Direct!=(host?1:0)||c.Sent!=(host?0:1))throw new Exception("lock route changed");Subject.Owned=false;Subject.RequestInventoryAction("N:other",RebirthCompanionInventoryAction.TransferToPlayer,2,7,"x",3);Subject.Owned=true;Subject.Distance=6;Subject.RequestInventoryAction("N:far",RebirthCompanionInventoryAction.TransferToPlayer,2,7,"x",3);Subject.Distance=0;if(LogisticsTransferService.Calls!=1)throw new Exception("ownership/distance bypass");}
foreach(string bad in new[]{"","1|2|3","-2|2|3|x","0|0|3|x","0|2|-1|x","0|2|2147483648|x","x|2|3|x"}){int s,t,q;string k;if(Subject.TryReadInventoryTransferSelection(bad,out s,out t,out q,out k))throw new Exception("invalid selection");}Console.WriteLine("PASS: host/client transfers use shared receipt path; lock routing retained; ownership/distance and malformed quantities rejected; item-key roundtrip");}}
