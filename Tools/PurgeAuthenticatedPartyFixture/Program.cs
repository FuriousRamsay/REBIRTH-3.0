using System;using System.Collections.Generic;
namespace UnityEngine {
public struct Vector3{public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));}
public struct Rect{public float x,y,w,h;public Rect(float a,float b,float c,float d){x=a;y=b;w=c;h=d;}public static Rect zero=>default;public bool Contains(Vector3 v)=>v.x>=x&&v.x<x+w&&v.y>=y&&v.y<y+h;public static bool operator==(Rect a,Rect b)=>a.x==b.x&&a.y==b.y&&a.w==b.w&&a.h==b.h;public static bool operator!=(Rect a,Rect b)=>!(a==b);public override bool Equals(object o)=>o is Rect r&&this==r;public override int GetHashCode()=>HashCode.Combine(x,y,w,h);}}
class EntityPlayer{internal int entityId;internal World world;internal Party Party;internal QuestJournal QuestJournal;internal UnityEngine.Vector3 position;internal bool Dead;internal bool IsDead()=>Dead;internal bool IsSpawned()=>true;}
class EntityPlayerLocal:EntityPlayer{}
class Party{internal List<EntityPlayer> MemberList=new List<EntityPlayer>();}
class World{internal Dictionary<int,EntityPlayer> Entities=new Dictionary<int,EntityPlayer>();internal EntityPlayer GetEntity(int id)=>Entities.TryGetValue(id,out var p)?p:null;}
class GameManager{internal static GameManager Instance;internal World World;}
class ClientInfo{internal int entityId;internal PlayerDataFile latestPlayerData;}
class PlayerDataFile{internal int id;internal QuestJournal questJournal;}
class Clients{internal Dictionary<int,ClientInfo> Values=new Dictionary<int,ClientInfo>();internal ClientInfo ForEntityId(int id)=>Values.TryGetValue(id,out var c)?c:null;}
class ConnectionManager{internal Clients Clients=new Clients();}
class SingletonMonoBehaviour<T>{internal static T Instance;}
class QuestJournal{internal List<Quest> quests=new List<Quest>();}
class Quest{internal enum QuestState{InProgress,Failed}internal enum PositionDataTypes{POIPosition}internal int SharedOwnerID=-1,QuestCode=-12;internal string ID="clear",QuestUniqueId="original";internal QuestState CurrentState=QuestState.InProgress;internal bool RallyMarkerActivated;internal UnityEngine.Rect Area;internal Dictionary<PositionDataTypes,UnityEngine.Vector3> PositionData=new Dictionary<PositionDataTypes,UnityEngine.Vector3>();internal UnityEngine.Rect GetLocationRect()=>Area;}
class Program{
static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
sealed class Fixture{internal World World=new World();internal EntityPlayer Owner,Guest;internal Quest Quest,Shared;internal ConnectionManager Manager=new ConnectionManager();internal RebirthPoiAuthenticatedPartyScope Scope;internal Fixture(){GameManager.Instance=new GameManager{World=World};SingletonMonoBehaviour<ConnectionManager>.Instance=Manager;Owner=new EntityPlayer{entityId=1,world=World,QuestJournal=new QuestJournal()};Guest=new EntityPlayer{entityId=2,world=World,QuestJournal=new QuestJournal(),position=new UnityEngine.Vector3(14,0,0)};World.Entities.Add(1,Owner);World.Entities.Add(2,Guest);Quest=new Quest();Shared=new Quest{SharedOwnerID=1};foreach(var q in new[]{Quest,Shared})q.PositionData.Add(Quest.PositionDataTypes.POIPosition,new UnityEngine.Vector3(10,2,30));Owner.QuestJournal.quests.Add(Quest);Guest.QuestJournal.quests.Add(Shared);Manager.Clients.Values.Add(1,new ClientInfo{entityId=1,latestPlayerData=new PlayerDataFile{id=1,questJournal=Owner.QuestJournal}});Manager.Clients.Values.Add(2,new ClientInfo{entityId=2,latestPlayerData=new PlayerDataFile{id=2,questJournal=Guest.QuestJournal}});var party=new Party();Owner.Party=Guest.Party=party;party.MemberList.Add(Owner);party.MemberList.Add(Guest);}internal bool Capture()=>RebirthPoiAuthenticatedPartyScope.TryCapture(World,Quest,1,out Scope);}
static void Main(){
var f=new Fixture();Check(f.Capture()&&f.Scope.IsCurrent&&f.Scope.Accepted.Length==1&&f.Scope.Accepted[0]==2&&f.Scope.Eligible[0]==2,"authenticated remote guest is derived from server journals without local OwnerPlayer");f.Guest.position=new UnityEngine.Vector3(15,0,0);Check(f.Scope.IsCurrent&&f.Scope.Eligible.Length==0,"exact native 15m range boundary excludes guest without mutating party");
f=new Fixture();f.Quest.Area=new UnityEngine.Rect(10,20,10,10);f.Guest.position=new UnityEngine.Vector3(10,999,20);Check(f.Capture()&&f.Scope.Eligible.Length==1,"POI rectangle uses X/Z rather than height");f.Guest.position=new UnityEngine.Vector3(20,0,20);Check(f.Scope.Eligible.Length==0,"native rectangle upper edge is excluded");
f=new Fixture();f.Shared.SharedOwnerID=99;Check(f.Capture()&&f.Scope.Accepted.Length==0,"another owner's accepted quest cannot join original reset");
f=new Fixture();f.Shared.QuestUniqueId="other";Check(f.Capture()&&f.Scope.Accepted.Length==0,"same class and code with different unique quest cannot join");
f=new Fixture();f.Guest.QuestJournal.quests.Add(f.Shared);Check(!f.Capture(),"ambiguous duplicate shared quest refused");
f=new Fixture();f.Manager.Clients.Values[2].latestPlayerData.id=3;Check(!f.Capture(),"spoofed guest player data rejected");
f=new Fixture();f.Manager.Clients.Values[2].latestPlayerData.questJournal=new QuestJournal();Check(!f.Capture(),"different native guest journal reference rejected");
f=new Fixture();f.Capture();f.Guest.QuestJournal=new QuestJournal();Check(!f.Scope.IsCurrent&&f.Scope.Eligible==null,"replacement guest journal invalidates original scope");
f=new Fixture();f.Capture();f.Owner.Party.MemberList.Remove(f.Guest);Check(!f.Scope.IsCurrent,"party departure invalidates original scope");
f=new Fixture();f.Capture();f.World.Entities[2]=new EntityPlayer{entityId=2,world=f.World,Party=f.Owner.Party,QuestJournal=f.Guest.QuestJournal};Check(!f.Scope.IsCurrent,"reused guest entity ID cannot substitute original native actor");
f=new Fixture();f.Capture();f.Guest.Dead=true;Check(!f.Scope.IsCurrent,"dead participant invalidates original guest scope");
f=new Fixture();f.Shared.CurrentState=Quest.QuestState.Failed;Check(f.Capture()&&f.Scope.Accepted.Length==0,"failed guest quest is not an accepted reset participant");
f=new Fixture();f.Guest.QuestJournal.quests.Clear();f.Capture();f.Guest.QuestJournal.quests.Add(f.Shared);Check(!f.Scope.IsCurrent,"new acceptance cannot alter already captured participant set");
f=new Fixture();f.Capture();GameManager.Instance.World=new World();Check(!f.Scope.IsCurrent,"replacement world withdraws original party scope");
f=new Fixture();f.Owner.Party=null;Check(f.Capture()&&f.Scope.Accepted==null&&f.Scope.Eligible==null,"solo original owner preserves native null participant list");f.Owner.Party=new Party();Check(!f.Scope.IsCurrent,"joining party cannot mutate captured solo reset");
Console.WriteLine("RESULT "+checks+" PASS; production authenticated party scope; native world/party/player-data doubles.");}}