using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
partial class Audit
{
 class PoiLookupCache{public World World;public PrefabInstance Poi;public Vector3i[] RankedLights,RankedPositions;public Vector3i RankedOrigin,RankedSize;public float RankedUntil;}
 static ConditionalWeakTable<EntityVehicle,PoiLookupCache> poiLookups=new();
 static Dictionary<EntityVehicle,object> sessions=new();static int checks;
 static PowerItem[] PoiLightNodes(EntityVehicle v,PrefabInstance p){var c=poiLookups.GetValue(v,_=>new());c.World=v.world;c.Poi=p;return v.Nodes;}
 static bool Current(object s)=>true;
 static bool PlainAuthorized(object s,Vector3i p)=>true;
 static bool Eligible(PowerItem n){checks++;return n.Allowed;}
 static bool PoiOwnerAuthorized(EntityVehicle v,PowerItem n)=>n.Authorized;
 static int passed;
 static void Check(bool b,string name){if(!b)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
 static Vector3i[] Next(EntityVehicle v,PrefabInstance p){UnityEngine.Time.realtimeSinceStartup+=1;return RankedPoiLights(v,p);}
 static void Main()
 {
  var v=new EntityVehicle();var p=new PrefabInstance{boundingBoxSize=new(100,10,100)};
  v.Nodes=new[]{new PowerItem(5,5),new PowerItem(50,50),new PowerItem(90,90)};v.world.Nodes=v.Nodes;
  var first=Next(v,p);Check(first.Length==3&&first[0]==new Vector3i(50,0,50),"original center-first spread retained");
  int before=checks;var second=Next(v,p);Check(ReferenceEquals(first,second)&&checks==before+3,"unchanged positions reuse rank after all eligibility checks");
  v.Nodes=v.Nodes.Reverse().ToArray();v.world.Nodes=v.Nodes;Check(ReferenceEquals(first,Next(v,p)),"enumeration order cannot invalidate exact set");
  v.Nodes[0].Authorized=false;var denied=Next(v,p);Check(denied.Length==2&&!ReferenceEquals(first,denied),"ownership denial removes rank position");
  v.Nodes[0].Authorized=true;Check(Next(v,p).Length==3,"restored authorization re-enters ranking");
  v.Nodes=v.Nodes.Append(new PowerItem(25,25)).ToArray();v.world.Nodes=v.Nodes;Check(Next(v,p).Length==4,"new light changes rank input");
  v.world.Nodes=v.Nodes.Skip(1).ToArray();Check(Next(v,p).Length==3,"removed live tile excluded despite cached candidate");
  v.world.Nodes=v.Nodes.Select(n=>new PowerItem(n.Position.x,n.Position.z)).ToArray();Check(Next(v,p).Length==0,"replaced live tiles reject stale candidate references");
  v.Nodes=v.world.Nodes;var restored=Next(v,p);Check(restored.Length==4,"current replacement nodes admitted");
  var replacement=v.Nodes.Select(n=>new PowerItem(n.Position.x,n.Position.z)).ToArray();v.Nodes=replacement;v.world.Nodes=replacement;
  Check(ReferenceEquals(restored,Next(v,p)),"same positions with new valid tile identities safely reuse geometry only");
  p.boundingBoxPosition=new(100,0,100);var moved=RankedPoiLights(v,p);Check(!ReferenceEquals(restored,moved),"changed bounds bypass short cache and rebuild");
  p.boundingBoxSize=new(110,10,100);Check(!ReferenceEquals(moved,RankedPoiLights(v,p)),"changed extent bypasses short cache");
  var context=Next(v,p);v.world=new World{Nodes=v.Nodes};Check(!ReferenceEquals(context,Next(v,p)),"world change cannot reuse rank observation");
  var plain=new TileEntityLight{Position=new(12,0,12)};RebirthCamperWorldLighting.Items.Add(plain);var withPlain=Next(v,p);
  Check(withPlain.Length==5,"plain lamp participates in exact input");v.world.Denied=plain.Position;
  Check(Next(v,p).Length==4,"plain claim denial invalidates rank");
  Check(RankedPoiLights(v,null).Length==0,"null POI stays empty");
  Console.WriteLine(passed+" extracted rank boundaries passed; simulated world dependencies.");
 }
}
struct Vector3i:IEquatable<Vector3i>{public int x,y,z;public Vector3i(int x,int y,int z){this.x=x;this.y=y;this.z=z;}public bool Equals(Vector3i v)=>x==v.x&&y==v.y&&z==v.z;public override bool Equals(object v)=>v is Vector3i p&&Equals(p);public override int GetHashCode()=>HashCode.Combine(x,y,z);public static bool operator==(Vector3i a,Vector3i b)=>a.Equals(b);public static bool operator!=(Vector3i a,Vector3i b)=>!a.Equals(b);}
class EntityVehicle{public World world=new();public PowerItem[] Nodes=Array.Empty<PowerItem>();public object GetOwner()=>this;}
class World{public PowerItem[] Nodes=Array.Empty<PowerItem>();public object GetTileEntity(Vector3i p)=>Nodes.FirstOrDefault(n=>n.Position==p)?.TileEntity;public Vector3i? Denied;public EnumLandClaimOwner GetLandClaimOwner(Vector3i p,object owner)=>Denied.HasValue&&Denied.Value==p?EnumLandClaimOwner.Other:EnumLandClaimOwner.None;}
enum EnumLandClaimOwner{None,Self,Other}
class PrefabInstance{public Vector3i boundingBoxPosition,boundingBoxSize;}
class PowerItem{public Vector3i Position;public object TileEntity=new();public bool Allowed=true,Authorized=true;public PowerItem(int x,int z){Position=new(x,0,z);}}
class TileEntityLight{public Vector3i Position;public Vector3i ToWorldPos()=>Position;}
static class RebirthCamperWorldLighting{public static List<TileEntityLight> Items=new();public static IEnumerable<TileEntityLight> Lights(World w,PrefabInstance p)=>Items;}
class GameManager{public static GameManager Instance=new();public GameManager GetPersistentPlayerList()=>this;public object GetPlayerData(object p)=>p;}
static class RebirthGameBridgeFrames{public static bool IsRecording=>false;}
static class Log{public static void Out(string s){} }
namespace UnityEngine{static class Time{public static float realtimeSinceStartup;public static int frameCount;}}