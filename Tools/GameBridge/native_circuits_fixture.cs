using System;
using System.Collections.Generic;
using System.Linq;
public interface IRebirthCamperEnergyConsumer {string Id{get;} int Priority{get;} int DemandWatts{get;} float Distance{get;} void SetSupply(EntityVehicle vehicle,int power);}
public struct CircuitPosition { public int Value; public float ToVector3Center()=>Value; public override string ToString()=>Value.ToString(); }
public class EntityVehicle {}
public class TileEntityPowered {public bool Parent; public bool HasParent()=>Parent;}
public class PowerItem {
 public CircuitPosition Position; public TileEntityPowered TileEntity=new TileEntityPowered(); public PowerItem Parent;
 public List<PowerItem> Children=new List<PowerItem>(); public ushort RequiredPower; public bool IsPowered,Active,Authorized=true,Live=true; public int PowerTransitions; public bool DefenseNode;
 public virtual bool PowerChildren()=>true;
 public virtual void HandlePowerReceived(ref ushort amount){ushort used=(ushort)Math.Min(amount,RequiredPower);bool powered=used==RequiredPower;if(powered!=IsPowered)PowerTransitions++;IsPowered=powered;amount-=used;if(amount>0&&PowerChildren())foreach(var child in Children)child.HandlePowerReceived(ref amount);}
 public virtual void HandlePowerUpdate(bool active){}
}
public class PowerConsumer:PowerItem {public override void HandlePowerUpdate(bool active){Active=active&&IsPowered;if(PowerChildren())foreach(var child in Children)child.HandlePowerUpdate(active);}}
public class PowerConsumerSingle:PowerItem {public override void HandlePowerUpdate(bool active){Active=IsPowered;}}
public class PowerTrigger:PowerConsumer {public bool Open=true;public override bool PowerChildren()=>Open;}
public class PowerSource:PowerItem {}
public class PowerManager {public static bool HasInstance=true;public static PowerManager Instance=new PowerManager();public Dictionary<int,PowerItem> PowerItemDictionary=new Dictionary<int,PowerItem>();}
public static class RebirthCamperEnergyBounds {public static float CurrentCenter(EntityVehicle v)=>0;public static float Distance(float a,float b)=>Math.Abs(a-b);public static bool ContainsDistance(float d,float r)=>d<=r;}
public static class RebirthCamperEnergyEfficiency {public static int Demand(EntityVehicle v,TileEntityPowered t,int n)=>n;}
public static partial class RebirthCamperEnergyDeviceAdapter {
 private sealed class Session {public EntityVehicle Vehicle;public Guid Operation;public object World;}
 private static Dictionary<PowerItem,Session> owners=new Dictionary<PowerItem,Session>();
 private static bool Current(Session s)=>true;
 private static bool CurrentNode(Session s,PowerItem n)=>n.Live;
 private static bool Eligible(PowerItem n)=>n is PowerConsumer&&n.Children.Count==0&&n.Parent==null&&n.RequiredPower>0;
 private static bool Awake;
 private static bool EnemyNear(object world,CircuitPosition position)=>Awake;
 private static bool Defense(PowerItem n)=>n.DefenseNode;
 private static bool DeviceOwnerAuthorized(Session s,PowerItem n)=>n.Authorized;
 static void Need(bool v,string why){if(!v)throw new Exception(why);}
 static PowerItem Node(int id,ushort watts,bool single=false){return single?new PowerConsumerSingle{Position=new CircuitPosition{Value=id},RequiredPower=watts}:new PowerConsumer{Position=new CircuitPosition{Value=id},RequiredPower=watts};}
 static void Setup(params PowerItem[] roots){Awake=false;circuits.Clear();owners.Clear();PowerManager.Instance.PowerItemDictionary.Clear();foreach(var r in roots)PowerManager.Instance.PowerItemDictionary.Add(r.Position.Value,r);}
 public static void Run(){
  var session=new Session{Vehicle=new EntityVehicle()};
  var single=Node(1,7,true);Setup(single);var c=CircuitConsumers(session,10,true).Single();Need(c.DemandWatts==7,"Single-consumer native demand");c.SetSupply(session.Vehicle,7);Need(single.Active,"Single consumer not activated");int changes=single.PowerTransitions;c.SetSupply(session.Vehicle,7);Need(single.PowerTransitions==changes,"Healthy supply must not cycle power every tick");c.SetSupply(session.Vehicle,0);Need(!single.Active,"Partial supply must not activate");
  var zero=Node(12,0,true);Setup(zero);c=CircuitConsumers(session,20,false).Single();c.SetSupply(session.Vehicle,0);Need(!zero.Active,"Power-off cleanup activated a zero-watt one-shot device");
  var generic=Node(9,3);Setup(generic);c=CircuitConsumers(session,10,true).Single();c.SetSupply(session.Vehicle,3);Need(generic.Active,"Generic native electrical leaf omitted");
  var relay=new PowerItem{Position=new CircuitPosition{Value=2}};var lamp=Node(3,5);relay.Children.Add(lamp);lamp.Parent=relay;Setup(relay);c=CircuitConsumers(session,10,true).Single();c.SetSupply(session.Vehicle,5);Need(lamp.Active,"Bare relay did not propagate native activation");
  var trigger=new PowerTrigger{Position=new CircuitPosition{Value=4},RequiredPower=1};var trap=Node(5,10);trigger.Children.Add(trap);trap.Parent=trigger;Setup(trigger);c=CircuitConsumers(session,10,true).Single();Need(c.DemandWatts==11,"Circuit demand");c.SetSupply(session.Vehicle,11);Need(trap.Active,"Open switch branch");trigger.Open=false;c=CircuitConsumers(session,10,true).Single();Need(c.DemandWatts==1,"Closed switch must not consume downstream watts");c.SetSupply(session.Vehicle,1);Need(!trap.Active,"Closed switch must disable downstream device");
  trigger.Open=true;c=CircuitConsumers(session,10,false).Single();c.SetSupply(session.Vehicle,11);Need(!trap.Active,"Unavailable source must stay off");
  trigger.Open=true;c=CircuitConsumers(session,10,true).Single();c.SetSupply(session.Vehicle,11);trigger.Children.Clear();trap.Parent=null;
  CircuitConsumers(session,10,true).ToArray();Need(!trap.Active,"Detached old branch must not stay active");
  var defense=Node(10,8,true);defense.DefenseNode=true;Setup(defense);c=CircuitConsumers(session,20,true).Single();Need(c.DemandWatts==0,"Standalone defense standby demand");c.SetSupply(session.Vehicle,0);Need(!defense.Active,"Standalone defense standby activation");Awake=true;c=CircuitConsumers(session,20,true).Single();c.SetSupply(session.Vehicle,8);Need(defense.Active,"Standalone defense wake");
  CircuitNodeRemoved(defense);Need(!defense.Active&&circuits.Count==0,"Device removal cleanup");
  var unload=Node(11,4,true);Setup(unload);c=CircuitConsumers(session,20,true).Single();c.SetSupply(session.Vehicle,4);OnCircuitVehicleUnload(session.Vehicle);Need(!unload.Active&&circuits.Count==0,"Vehicle unload cleanup");
  var foreign=new PowerSource{Position=new CircuitPosition{Value=6}};Setup(foreign);Need(!CircuitConsumers(session,10,true).Any(),"Native power source must not become a consumer");
  var cycle=new PowerItem{Position=new CircuitPosition{Value=7}};cycle.Children.Add(cycle);Setup(cycle);Need(!CircuitConsumers(session,10,true).Any(),"Cycle must be refused");
  var denied=Node(8,2,true);denied.Authorized=false;Setup(denied);Need(!CircuitConsumers(session,10,true).Any(),"Foreign claim/owner must be refused");
  var outside=Node(50,2,true);Setup(outside);Need(!CircuitConsumers(session,10,true).Any(),"Root outside reach must be refused");
  Console.WriteLine("PASS actual circuit adapter with explicit native power doubles: single consumer, relay, switch on/off, downstream demand, insufficient/unavailable supply, source/cycle/owner/range refusal. Native gameplay NOT RUN.");
 }
}
