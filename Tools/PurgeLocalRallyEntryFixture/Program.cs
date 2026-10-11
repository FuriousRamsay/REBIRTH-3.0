using System;using System.Collections.Generic;using UnityEngine;
namespace UnityEngine {
public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;}public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));}
public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}}
public struct Rect {public float x,y,w,h;public Rect(float a,float b,float c,float d){x=a;y=b;w=c;h=d;}public static Rect zero=>default;public bool Contains(Vector3 v)=>v.x>=x&&v.x<x+w&&v.y>=y&&v.y<y+h;public static bool operator ==(Rect a,Rect b)=>a.x==b.x&&a.y==b.y&&a.w==b.w&&a.h==b.h;public static bool operator !=(Rect a,Rect b)=>!(a==b);public override bool Equals(object o)=>o is Rect r&&this==r;public override int GetHashCode()=>HashCode.Combine(x,y,w,h);}
}
struct Vector3i {internal int x,y,z;internal Vector3i(int a,int b,int c){x=a;y=b;z=c;}public static bool operator ==(Vector3i a,Vector3i b)=>a.x==b.x&&a.y==b.y&&a.z==b.z;public static bool operator !=(Vector3i a,Vector3i b)=>!(a==b);public override bool Equals(object o)=>o is Vector3i v&&this==v;public override int GetHashCode()=>HashCode.Combine(x,y,z);}
class EntityPlayer {internal Vector3 position;internal int entityId;}
class EntityPlayerLocal:EntityPlayer {}
class World {internal bool Remote;internal ulong worldTime=12;internal EntityPlayerLocal Player;internal bool IsRemote()=>Remote;internal EntityPlayerLocal GetPrimaryPlayer()=>Player;}
class GameManager {internal static GameManager Instance;internal World World;internal static string Tooltip;internal static void ShowTooltip(EntityPlayerLocal player,string text){Tooltip=text;}}
class Journal {internal EntityPlayer OwnerPlayer;internal Quest ActiveQuest;}
class Quest {internal enum PositionDataTypes {POIPosition}internal Journal OwnerJournal;internal int SharedOwnerID=-1;internal List<EntityPlayer> sharedWithList;internal Rect Area;internal Rect GetLocationRect()=>Area;internal Dictionary<PositionDataTypes,Vector3> PositionData=new Dictionary<PositionDataTypes,Vector3>();}
class ObjectiveRallyPoint {internal Quest OwnerQuest;internal bool Complete;internal Vector3i RallyPos=new Vector3i(1,2,3);internal int startTime=-1,endTime=-1,Calls;internal bool Activate;internal QuestEventManager.POILockoutReasonTypes Reason;internal void RallyPointActivate(Vector3 position,bool activate,QuestEventManager.POILockoutReasonTypes reason,ulong extra){Calls++;Activate=activate;Reason=reason;}}
class QuestEventManager {internal static QuestEventManager Current=new QuestEventManager();internal enum POILockoutReasonTypes {None,Bedroll,LandClaim,PlayerInside,QuestLock}internal POILockoutReasonTypes Reason;internal POILockoutReasonTypes CheckForPOILockouts(int player,Vector2 position,out ulong extra){extra=0;return Reason;}}
namespace Twitch {class TwitchManager {internal static bool HasInstance;internal static TwitchManager Current=new TwitchManager();internal bool IsVoting;}}
class GameUtils {internal static int WorldTimeToHours(ulong time)=>(int)time;}
class Localization {internal static string Get(string key)=>key+" {0} {1}";}
class RebirthPurgeReleasePolicy {internal static bool Enabled=true;}
class RebirthSandboxOptionManager {internal static RebirthSandboxOptionManager Current=new RebirthSandboxOptionManager();internal bool PoiClearTrackingEnabled=true;}
class RebirthPoiRemoteRallyOwner {internal static int Calls;internal static bool TryStart(ObjectiveRallyPoint objective,Vector3 position){Calls++;return true;} }
class Program {
static int checks;static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
static ObjectiveRallyPoint Setup(){var p=new EntityPlayerLocal{entityId=1};GameManager.Instance=new GameManager{World=new World{Player=p}};var q=new Quest{OwnerJournal=new Journal{OwnerPlayer=p}};q.PositionData.Add(Quest.PositionDataTypes.POIPosition,new Vector3(10,2,30));QuestEventManager.Current.Reason=QuestEventManager.POILockoutReasonTypes.None;Twitch.TwitchManager.HasInstance=false;return new ObjectiveRallyPoint{OwnerQuest=q};}
static void Main(){
var o=Setup();Check(!RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos)&&o.Calls==1&&o.Activate,"local original activation reaches deferred owner");
RebirthPurgeReleasePolicy.Enabled=false;o=Setup();Check(RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos)&&o.Calls==0,"disabled release preserves native flow");RebirthPurgeReleasePolicy.Enabled=true;
o=Setup();GameManager.Instance.World.Remote=true;Check(!RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos)&&o.Calls==0&&RebirthPoiRemoteRallyOwner.Calls==1,"remote input routes to authenticated transport before native party pruning");
o=Setup();Check(!RebirthPoiLocalRallyEntryHook.Invoke(o,new Vector3i(9,2,3))&&o.Calls==0,"wrong rally block cannot start reset");
o=Setup();o.OwnerQuest.SharedOwnerID=3;Check(!RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos)&&o.Calls==0,"shared guest cannot start owner reset");
o=Setup();o.Complete=true;Check(!RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos)&&o.Calls==0,"completed objective cannot restart reset");
o=Setup();o.OwnerQuest.OwnerJournal.ActiveQuest=new Quest();Check(!RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos)&&o.Calls==0,"another active quest prevents rally ownership");
o=Setup();Twitch.TwitchManager.HasInstance=true;Twitch.TwitchManager.Current.IsVoting=true;Check(!RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos)&&o.Calls==0,"native vote restriction retained");
foreach(int hour in new[]{7,8,19,20}){o=Setup();o.startTime=8;o.endTime=20;GameManager.Instance.World.worldTime=(ulong)hour;RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos);Check(o.Calls==(hour>=8&&hour<20?1:0),"daytime interval exact boundary "+hour);}
foreach(int hour in new[]{5,6,21,22}){o=Setup();o.startTime=22;o.endTime=6;GameManager.Instance.World.worldTime=(ulong)hour;RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos);Check(o.Calls==(hour>=22||hour<6?1:0),"overnight interval exact boundary "+hour);}
foreach(QuestEventManager.POILockoutReasonTypes reason in Enum.GetValues(typeof(QuestEventManager.POILockoutReasonTypes))){o=Setup();QuestEventManager.Current.Reason=reason;RebirthPoiLocalRallyEntryHook.Invoke(o,o.RallyPos);Check(o.Calls==1&&o.Reason==reason&&o.Activate==(reason==QuestEventManager.POILockoutReasonTypes.None),"native lockout retained "+reason);}
o=Setup();var q=o.OwnerQuest;Check(RebirthPoiLocalRallyOwner.EligibleParty(q)==null,"null native party preserved");q.sharedWithList=new List<EntityPlayer>{new EntityPlayer{entityId=2,position=new Vector3(14,0,0)},new EntityPlayer{entityId=3,position=new Vector3(15,0,0)}};var ids=RebirthPoiLocalRallyOwner.EligibleParty(q);Check(ids.Length==1&&ids[0]==2&&q.sharedWithList.Count==2,"distance eligibility excludes exact 15m without pruning native party");q.Area=new Rect(10,20,10,10);q.sharedWithList[0].position=new Vector3(10,999,20);q.sharedWithList[1].position=new Vector3(20,0,20);ids=RebirthPoiLocalRallyOwner.EligibleParty(q);Check(ids.Length==1&&ids[0]==2&&q.sharedWithList.Count==2,"POI rectangle uses X/Z and native exclusive upper bound without party mutation");
Console.WriteLine("RESULT "+checks+" PASS; exact current production entry and eligible-party extracts; native engine/Harmony doubled.");}
}