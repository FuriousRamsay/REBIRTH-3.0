using System;
using System.Linq;
using System.Collections.Generic;
partial class Audit {
    private static PowerItem[] CircuitCandidates(Session session)
    {
        try
        {
            if(!PowerManager.HasInstance)return Array.Empty<PowerItem>();
            var frame=RebirthCamperRelayWiring.Read(session.Vehicle);
            if(frame==null||frame.Links.Count==0)return Array.Empty<PowerItem>();
            var links=frame.Links.ToArray();
            var candidates=new List<PowerItem>();var seen=new HashSet<PowerItem>();
            foreach(var link in links)
                if(!link.Mounted&&PowerManager.Instance.PowerItemDictionary.TryGetValue(link.Position,out var node)&&
                    node!=null&&seen.Add(node))candidates.Add(node);
            return candidates.ToArray();
        }
        catch{return Array.Empty<PowerItem>();} // Matches Connected's fail-closed contract.
    }
    private static IEnumerable<IRebirthCamperEnergyConsumer> CircuitConsumers(Session session,float range,bool available,float ownerSaving)
    {
        var found=new HashSet<PowerItem>();
        foreach(var root in CircuitCandidates(session))
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
                Log.Out("[REBIRTH EnergyFlow] circuit-admitted vehicle="+session.Vehicle.entityId+
                    " root="+root.Position+" nodes="+nodes.Length+" available="+available);
            }
            int native=0,watts=0;
            var active=new Stack<PowerItem>();active.Push(root);
            while(active.Count>0)
            {
                var node=active.Pop();native=checked(native+node.RequiredPower);
                watts=checked(watts+RebirthCamperEnergyEfficiency.Demand(node.TileEntity,ownerSaving,node.RequiredPower));
                if(node.PowerChildren())foreach(var child in node.Children)active.Push(child);
            }
            if(native>ushort.MaxValue)continue; // Installed native power frame limit.
            // Keep native defense devices supplied while their own trigger is idle.
            // Cutting supply also cuts the turret's camera and powered sound.
            existing.NativeWatts=native;existing.Watts=watts;existing.Available=available;existing.RangeDistance=distance;
            found.Add(root);yield return existing;
        }
        foreach(var old in circuits.Values.Where(c=>ReferenceEquals(c.Session,session)&&!found.Contains(c.Root)).ToArray())
        {old.Clear();circuits.Remove(old.Root);}
    }

}