using System;
using System.Collections.Generic;
using System.Linq;

public static partial class RebirthCamperEnergyDeviceAdapter
{
    // Feed existing native circuit roots. Never insert a phantom source, change
    // wires, toggle switches, consume ammunition, or replace native activation.
    private sealed class NativeCircuit : IRebirthCamperEnergyConsumer
    {
        internal Session Session;
        internal PowerItem Root;
        internal PowerItem[] Nodes;
        internal PowerItem[][] Children;
        internal int NativeWatts,Watts;
        internal float RangeDistance;
        internal bool Available;
        public string Id=>"native-circuit:"+Root.Position;
        public int Priority=>Nodes.Any(Defense)?0:1;
        public int DemandWatts=>Watts;
        public float Distance=>RangeDistance;
        internal bool Current()
        {
            if(!RebirthCamperEnergyDeviceAdapter.Current(Session)||Root.Parent!=null||Root.TileEntity.HasParent()
                ||!RebirthCamperRelayWiring.Connected(Session.Vehicle,Root.TileEntity))return false;
            for(int i=0;i<Nodes.Length;i++)
                if(!CurrentNode(Session,Nodes[i])||Nodes[i] is PowerSource||
                    !Nodes[i].Children.SequenceEqual(Children[i])||
                    !DeviceOwnerAuthorized(Session,Nodes[i]))return false;
            return true;
        }
        public void SetSupply(EntityVehicle vehicle,int suppliedWatts)
        {
            if(!ReferenceEquals(vehicle,Session.Vehicle)||!Current())return;
            bool supplied=Available&&suppliedWatts>=Watts&&Session.Operation==Guid.Empty;
            if(!supplied){Clear();return;}
            // Clear only branches behind a closed native switch/trigger. Do
            // not cycle healthy nodes off/on each tick (sounds and ActivateOnce
            // callbacks must retain their normal native transition behavior).
            var receiving=new HashSet<PowerItem>();var pending=new Stack<PowerItem>();pending.Push(Root);
            while(pending.Count>0)
            {
                var node=pending.Pop();receiving.Add(node);
                if(node.PowerChildren())foreach(var child in node.Children)pending.Push(child);
            }
            foreach(var node in Nodes)if(!receiving.Contains(node)){ushort none=0;node.HandlePowerReceived(ref none);}
            ushort amount=(ushort)NativeWatts;
            Root.HandlePowerReceived(ref amount);
            foreach(var node in Nodes)if(!node.IsPowered)node.HandlePowerUpdate(false);
            Root.HandlePowerUpdate(true);
            UpdateBareRelays(Root,true);
        }
        private static void UpdateBareRelays(PowerItem node,bool active)
        {
            // Base PowerItem has no activation body. Its relay children still
            // need the same native virtual update that consumers propagate.
            if(node.GetType()==typeof(PowerItem)&&node.PowerChildren())
                foreach(var child in node.Children)child.HandlePowerUpdate(active);
            if(node.PowerChildren())foreach(var child in node.Children)UpdateBareRelays(child,active);
        }
        internal void Clear()
        {
            if(!RebirthCamperEnergyDeviceAdapter.Current(Session)||Root.Parent!=null||Root.TileEntity.HasParent())return;
            // A rewired circuit can invalidate the retained topology. Stop only
            // still-current original nodes that remain here or were detached;
            // never overwrite a node handed to another upstream supplier.
            foreach(var node in Nodes)
            {
                if(!CurrentNode(Session,node)||!DeviceOwnerAuthorized(Session,node))continue;
                var parent=node;var seen=new HashSet<PowerItem>();
                while(parent.Parent!=null&&seen.Add(parent))parent=parent.Parent;
                if(!ReferenceEquals(parent,Root)&&node.Parent!=null)continue;
                // The previously supplied root can gain a new native child when
                // the player wires a relay/switch. Clear our old power frame so
                // the expanded native circuit can be admitted on this tick.
                // Non-root branches with changed children remain untouched.
                if(!ReferenceEquals(node,Root)&&node.Children.Any(child=>!Nodes.Contains(child)))continue;
                // Native single-use consumers ignore the update argument and
                // activate on IsPowered alone. A zero-watt one must not receive
                // a fresh activation from our power-OFF cleanup.
                if(node is PowerConsumerSingle&&node.RequiredPower==0)continue;
                ushort none=0;node.HandlePowerReceived(ref none);node.HandlePowerUpdate(false);
            }
        }
    }
    private static readonly Dictionary<PowerItem,NativeCircuit> circuits=new Dictionary<PowerItem,NativeCircuit>();
    private static IEnumerable<IRebirthCamperEnergyConsumer> CircuitConsumers(Session session,float range,bool available)
    {
        var found=new HashSet<PowerItem>();
        if(PowerManager.HasInstance)foreach(var root in PowerManager.Instance.PowerItemDictionary.Values.ToArray())
        {
            // Explicit POI-lighting leases above keep their switch policy.
            if(root==null||owners.ContainsKey(root)||root is PowerSource||root.Parent!=null||root.TileEntity==null||
                root.TileEntity.HasParent()||!CurrentNode(session,root)||!RebirthCamperRelayWiring.Connected(session.Vehicle,root.TileEntity))continue;
            float distance=RebirthCamperEnergyBounds.Distance(RebirthCamperEnergyBounds.CurrentCenter(session.Vehicle),root.Position.ToVector3Center());
            if(!RebirthCamperEnergyBounds.ContainsDistance(distance,range))continue;
            if(circuits.TryGetValue(root,out var existing)&&!ReferenceEquals(existing.Session,session))continue;
            if(!TryCircuit(session,root,out var nodes,out var children))continue;
            if(existing!=null&&!existing.Current()){existing.Clear();circuits.Remove(root);existing=null;}
            if(existing==null)
            {
                if(root.IsPowered&&root.RequiredPower>0)continue; // Do not take over another live supplier.
                existing=new NativeCircuit{Session=session,Root=root,Nodes=nodes,Children=children};circuits.Add(root,existing);
            }
            int native=0,watts=0;
            var active=new Stack<PowerItem>();active.Push(root);
            while(active.Count>0)
            {
                var node=active.Pop();native=checked(native+node.RequiredPower);
                watts=checked(watts+RebirthCamperEnergyEfficiency.Demand(session.Vehicle,node.TileEntity,node.RequiredPower));
                if(node.PowerChildren())foreach(var child in node.Children)active.Push(child);
            }
            if(native>ushort.MaxValue)continue; // Installed native power frame limit.
            // Preserve the existing standalone-defense standby policy. Wired
            // circuits use their native switches/triggers rather than disabling
            // unrelated lamps because a defense shares the same circuit.
            bool standby=nodes.Length==1&&Defense(root)&&!EnemyNear(session.World,root.Position);
            existing.NativeWatts=native;existing.Watts=standby?0:watts;existing.Available=available&&!standby;existing.RangeDistance=distance;
            found.Add(root);yield return existing;
        }
        foreach(var old in circuits.Values.Where(c=>ReferenceEquals(c.Session,session)&&!found.Contains(c.Root)).ToArray())
        {old.Clear();circuits.Remove(old.Root);}
    }
    private static bool TryCircuit(Session session,PowerItem root,out PowerItem[] nodes,out PowerItem[][] children)
    {
        nodes=null;children=null;var result=new List<PowerItem>();var seen=new HashSet<PowerItem>();var pending=new Stack<PowerItem>();pending.Push(root);
        while(pending.Count>0)
        {
            var node=pending.Pop();
            if(!seen.Add(node)||node is PowerSource||!CurrentNode(session,node)||!DeviceOwnerAuthorized(session,node)||
                node!=root&&node.Parent==null||owners.ContainsKey(node))return false;
            result.Add(node);
            foreach(var child in node.Children){if(child==null||!ReferenceEquals(child.Parent,node))return false;pending.Push(child);}
        }
        nodes=result.ToArray();children=nodes.Select(n=>n.Children.ToArray()).ToArray();return true;
    }
    private static void CircuitNodeRemoved(PowerItem node)
    {
        if(node==null)return;
        foreach(var circuit in circuits.Values.Where(c=>c.Nodes.Contains(node)).ToArray())
        {circuit.Clear();circuits.Remove(circuit.Root);}
    }
    internal static void OnCircuitVehicleUnload(EntityVehicle vehicle)=>SuspendCircuits(vehicle,true);
    private static void SuspendCircuits(EntityVehicle vehicle,bool release)
    {
        foreach(var circuit in circuits.Values.Where(c=>ReferenceEquals(c.Session.Vehicle,vehicle)).ToArray())
        {circuit.Clear();if(release)circuits.Remove(circuit.Root);}
    }
}
