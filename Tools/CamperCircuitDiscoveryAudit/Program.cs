using System;
using System.Linq;
using System.Collections.Generic;
interface IRebirthCamperEnergyConsumer {}
class EntityVehicle { public int entityId=1; }
struct Position { public int Value; public int ToVector3Center()=>Value; public override string ToString()=>Value.ToString(); }
class Tile {public bool Parent,Connected=true; public bool HasParent()=>Parent;}
class PowerItem {public Position Position;public Tile TileEntity=new();public PowerItem Parent;public bool IsPowered;public int RequiredPower=1;public List<PowerItem> Children=new();public bool PowerChildren()=>true;}
class PowerSource:PowerItem {}
class PowerManager {public static bool HasInstance=true;public static PowerManager Instance=new();public Dictionary<Position,PowerItem> PowerItemDictionary=new();}
static class RebirthCamperRelayWiring {
 public class Link{public bool Mounted;public Position Position;}
 public class Frame{public List<Link> Links=new();}
 public static Dictionary<EntityVehicle,Frame> Frames=new();public static bool Malformed;
 public static Frame Read(EntityVehicle v){if(Malformed)throw new Exception("Malformed");return Frames.TryGetValue(v,out var f)?f:null;}
 public static bool Connected(EntityVehicle v,Tile t)=>t.Connected;
}
static class RebirthCamperEnergyBounds {public static int CurrentCenter(EntityVehicle v)=>0;public static float Distance(int a,int b)=>Math.Abs(a-b);public static bool ContainsDistance(float d,float r)=>d<=r;}
static class RebirthCamperEnergyEfficiency {public static int Demand(Tile t,float f,int power)=>power;}
static class Log {public static void Out(string s){} }
partial class Audit {
 class Session{public EntityVehicle Vehicle=new();}
 class NativeCircuit:IRebirthCamperEnergyConsumer {public Session Session;public PowerItem Root;public PowerItem[] Nodes;public PowerItem[][] Children;public int NativeWatts,Watts;public bool Available;public float RangeDistance;public bool Current()=>true;public void Clear(){clears++;}}
 static int clears;
 static Dictionary<PowerItem,NativeCircuit> circuits=new();static Dictionary<PowerItem,Session> owners=new();
 static bool CurrentNode(Session s,PowerItem p)=>PowerManager.HasInstance&&PowerManager.Instance.PowerItemDictionary.TryGetValue(p.Position,out var n)&&ReferenceEquals(n,p);
 static bool TryCircuit(Session s,PowerItem p,out PowerItem[] nodes,out PowerItem[][] children){nodes=new[]{p};children=new[]{p.Children.ToArray()};return true;}
 static Session Reset(){PowerManager.HasInstance=true;PowerManager.Instance=new();RebirthCamperRelayWiring.Frames.Clear();RebirthCamperRelayWiring.Malformed=false;circuits.Clear();owners.Clear();clears=0;return new Session();}
 static PowerItem Add(Session s,int pos,bool mounted=false){var p=new PowerItem{Position=new Position{Value=pos}};PowerManager.Instance.PowerItemDictionary[p.Position]=p;if(!RebirthCamperRelayWiring.Frames.TryGetValue(s.Vehicle,out var f))RebirthCamperRelayWiring.Frames[s.Vehicle]=f=new();f.Links.Add(new(){Mounted=mounted,Position=p.Position});return p;}
 static int Consume(Session s)=>CircuitConsumers(s,100,true,0).Count();
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(){
  var s=Reset();Check(Consume(s)==0,"no links admit nothing");Add(s,1,true);Check(Consume(s)==0,"mounted link excluded");
  s=Reset();var p=Add(s,1);RebirthCamperRelayWiring.Frames[s.Vehicle].Links.Add(new(){Position=p.Position});Check(CircuitCandidates(s).Length==1&&Consume(s)==1,"duplicate endpoint admitted once");
  RebirthCamperRelayWiring.Malformed=true;Check(Consume(s)==0&&clears==1&&circuits.Count==0,"malformed frame still cleans old circuit");
  s=Reset();p=Add(s,1);Consume(s);PowerManager.Instance.PowerItemDictionary.Remove(p.Position);Check(Consume(s)==0&&clears==1,"removed endpoint cleans old circuit");
  s=Reset();p=Add(s,1);Consume(s);var replacement=new PowerItem{Position=p.Position};PowerManager.Instance.PowerItemDictionary[p.Position]=replacement;Check(Consume(s)==1&&circuits.ContainsKey(replacement)&&!circuits.ContainsKey(p),"replacement resolved fresh and old circuit cleared");
  replacement.Parent=new PowerItem();Check(Consume(s)==0&&!circuits.Any(),"new parent prevents root admission and cleans old circuit");
  s=Reset();p=Add(s,1);var snapshot=CircuitCandidates(s);RebirthCamperRelayWiring.Frames[s.Vehicle].Links.Clear();Check(snapshot.Length==1&&CircuitCandidates(s).Length==0,"candidate array independent from mutable links");
  s=Reset();p=Add(s,1);Consume(s);var other=new Session();RebirthCamperRelayWiring.Frames[other.Vehicle]=RebirthCamperRelayWiring.Frames[s.Vehicle];Check(Consume(other)==0&&circuits[p].Session==s,"other camper cannot take over existing circuit");
  s=Reset();p=Add(s,1);p.TileEntity.Connected=false;Check(Consume(s)==0,"live connection guard retained");
  s=Reset();p=Add(s,1);PowerManager.HasInstance=false;Check(Consume(s)==0,"absent power manager safe");
  Console.WriteLine("All scoped discovery boundaries passed; actual candidate/consumer methods, dependency stubs.");
 }
}
