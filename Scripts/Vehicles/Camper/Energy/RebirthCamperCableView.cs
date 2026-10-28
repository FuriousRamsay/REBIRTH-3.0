using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;

// Uses the native deforming wire mesh, material and sag simulation.
internal sealed class RebirthCamperCableView:MonoBehaviour
{
    internal EntityVehicle Vehicle;
    private readonly Dictionary<System.Guid,WireNode> cables=new Dictionary<System.Guid,WireNode>();
    private float next;
    private void LateUpdate()
    {
        if(!Vehicle||GameManager.IsDedicatedServer||Time.realtimeSinceStartup<next)return;next=Time.realtimeSinceStartup+.05f;
        bool show=RebirthCamperRelayWiring.WireToolHeld(Vehicle);
        RebirthCamperRelayWiring.Frame frame;
        try{frame=RebirthCamperRelayWiring.Read(Vehicle);}catch{return;}
        if(frame==null)return;
        var live=new HashSet<System.Guid>();
        // Share only this pass's successful relay lookups. Connection frame/stamp/tile
        // admission and transform positions are still observed for every external link.
        var relays=new RebirthCamperRelayWiring.RelayLookup();
        foreach(var link in frame.Links)
        {
            Transform relay=relays.Resolve(Vehicle,link.Relay);
            if(!relay)continue;
            Vector3 endpoint;
            if(link.Mounted)
            {
                if(!RebirthCamperTurretRuntime.For(Vehicle).Observe().Slots[link.Corner].HasTurret)continue;
                var target=Vehicle.RootTransform.GetComponentsInChildren<RebirthCamperWireTarget>(true).FirstOrDefault(t=>t.Kind==1&&t.Slot==link.Corner);
                if(!target)continue;endpoint=target.transform.position;
            }
            else
            {
                var tile=Vehicle.world.GetTileEntity(link.Position);
                if(tile==null||!RebirthCamperRelayWiring.Connected(Vehicle,tile,ref relays))continue;
                endpoint=link.Position.ToVector3Center()-Origin.position;
                if(tile is TileEntityPowered powered)endpoint+=powered.WireOffset;
            }
            if((endpoint-relay.position).sqrMagnitude>link.Length*link.Length)continue;
            live.Add(link.Stamp);
            if(!cables.TryGetValue(link.Stamp,out var node)||!node)
            {
                var prefab=Resources.Load<GameObject>("Prefabs/WireNode");if(!prefab)continue;
                node=Object.Instantiate(prefab).GetComponent<WireNode>();if(!node)continue;
                node.TogglePulse(false);node.ToggleMeshCollider(false);cables[link.Stamp]=node;
            }
            node.SourcePosition=relay.position;node.LocalPosition=endpoint;
            if(node.gameObject.activeSelf!=show)node.gameObject.SetActive(show);
        }
        foreach(var id in cables.Keys.Where(id=>!live.Contains(id)).ToArray()){if(cables[id])Object.Destroy(cables[id].gameObject);cables.Remove(id);}
    }
    private void OnDestroy(){foreach(var node in cables.Values)if(node)Object.Destroy(node.gameObject);cables.Clear();}
}
